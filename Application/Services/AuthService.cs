using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Configurations;
using Application.DTOs;
using Application.Events;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly RedisService _redis;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher, 
        IKafkaProducer kafkaProducer, RedisService redis, IOptions<JwtSettings> jwtOptions)
    {
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _kafkaProducer = kafkaProducer;
        _redis = redis;
        _jwtSettings = jwtOptions.Value;
    }
    
    public async Task<AuthResponse<LoginResponseDto>> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByEmailAsync(loginDto.Email);
        if (user == null || !_passwordHasher.VerifyPassword(loginDto.Password, user.PasswordHash))
            return new AuthResponse<LoginResponseDto>()
            {
                LoginResult = LoginResult.IncorrectData
            };

        if (!user.IsActive)
        {
            return new AuthResponse<LoginResponseDto>()
            {
                LoginResult = LoginResult.NotActivated
            };
        }
        
        // если входит администратор через телеграм бота
        if (loginDto.ChatId != null && !string.IsNullOrEmpty(loginDto.ChatId))
        {
            //проверяем не занят ли уже этот аккаунт другим пользователем
            if (user.Role == UserRole.Administrator && !string.IsNullOrEmpty(user.ChatId) && user.ChatId != loginDto.ChatId)
            {
                return new AuthResponse<LoginResponseDto>()
                {
                    LoginResult = LoginResult.AlreadyInUse
                };
            }
            user.ChatId = loginDto.ChatId;
        }
        
        // Генерация токенов
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        
        // Сохранение refresh токена
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
        await _userRepository.UpdateAsync(user);
        
        var authResponseDto = new LoginResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString(),
            GroupName = user.Group?.Name,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AuthPeriod = _jwtSettings.RefreshTokenExpiryDays
        };
        return new AuthResponse<LoginResponseDto>()
        {
            LoginResult = LoginResult.Success,
            Data = authResponseDto
        };
    }
    
    public async Task<RegistrationResult> RegisterAsync(RegisterDto registerDto, Guid? currentUserId = null)
    {
        // Валидация пароля
        if (registerDto.Password != registerDto.ConfirmPassword)
            return RegistrationResult.PasswordsDoNotMatch;

        // Проверка уникальности email
        if (!await _userRepository.IsEmailUniqueAsync(registerDto.Email))
            return RegistrationResult.EmailAlreadyExists;

        // Валидация роли (только администраторы могут создавать учителей и администраторов)
        if (currentUserId.HasValue)
        {
            var currentUser = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (currentUser?.Role != UserRole.Administrator && registerDto.Role != UserRole.Student)
                return RegistrationResult.InsufficientPermissions;
        }
        else
        {
            // Самостоятельная регистрация разрешена только для студентов
            if (registerDto.Role != UserRole.Student)
                return RegistrationResult.InsufficientPermissions;
        }

        // Валидация группы (только студенты могут быть привязаны к группе)
        if (registerDto.Role != UserRole.Student && registerDto.GroupId.HasValue)
            return RegistrationResult.InvalidGroupAssignment;

        if (registerDto.Role == UserRole.Student && registerDto.GroupId.HasValue)
        {
            var group = await _groupRepository.GetByIdAsync(registerDto.GroupId.Value);
            if (group == null)
                return RegistrationResult.GroupNotFound;
        }

        //Валидация токена (студенты могут иметь только один аккаунт, а учителя несколько)
        if(registerDto.Role == UserRole.Student && string.IsNullOrEmpty(registerDto.TelegramId))
        {
            return RegistrationResult.DeviceTokenNullError;
        }

        if (!await _userRepository.IsDeviceTokenUniqueAsync(registerDto.TelegramId) && registerDto.Role == UserRole.Student)
        {
            return RegistrationResult.DeviceTokenAlreadyExists;
        }
        
        // Создание пользователя
        var user = new User
        {
            Email = registerDto.Email,
            PasswordHash = _passwordHasher.HashPassword(registerDto.Password),
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            Role = registerDto.Role,
            GroupId = registerDto.Role == UserRole.Student ? registerDto.GroupId : null,
            DeviceToken = registerDto.TelegramId ?? null,
            IsActive = registerDto.IsActive ?? false,
            ChatId = registerDto.ChatId ?? null
        };

        await _userRepository.AddAsync(user);

        //очищаем кеш
        switch (user.Role)
        {
            case UserRole.Student:
                await _redis.RemoveAsync("students");
                await _redis.RemoveAsync("groups");
                break;
            case UserRole.Teacher:
                await _redis.RemoveAsync("teachers");
                break;
            case UserRole.Administrator:
                await _redis.RemoveAsync("admins");
                break;
        }

        //Отправляем событие в кафку
        var kafkaMessage = new UserRegisterEvent()
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id
        };
        
        var messageJson = JsonSerializer.Serialize(kafkaMessage);
        await _kafkaProducer.ProduceAsync("notifications.register", messageJson);
        
        return RegistrationResult.Success;
    }

    // Регистрация для администраторов и учителей без аунтификации
    public async Task<RegistrationResult> RegisterAsync(RegisterDto registerDto)
    {
        // Валидация пароля
        if (registerDto.Password != registerDto.ConfirmPassword)
            return RegistrationResult.PasswordsDoNotMatch;
        
        // Проверка уникальности email
        if (!await _userRepository.IsEmailUniqueAsync(registerDto.Email))
            return RegistrationResult.EmailAlreadyExists;
        
        //Валидация роли(регистрация разрешена только для учителей и администраторов)
        if (registerDto.Role == UserRole.Student) return RegistrationResult.InsufficientPermissions;
        
        // Валидация группы (только студенты могут быть привязаны к группе)
        if (registerDto.Role != UserRole.Student && registerDto.GroupId.HasValue)
            return RegistrationResult.InvalidGroupAssignment;
        
        // Создание пользователя
        var user = new User
        {
            Email = registerDto.Email,
            PasswordHash = _passwordHasher.HashPassword(registerDto.Password),
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            Role = registerDto.Role,
            GroupId = registerDto.Role == UserRole.Student ? registerDto.GroupId : null,
            DeviceToken = registerDto.TelegramId ?? null,
            IsActive = registerDto.IsActive ?? false,
            ChatId = registerDto.ChatId ?? null
        };
        //очищаем кеш
        switch (user.Role)
        {
            case UserRole.Teacher:
                await _redis.RemoveAsync("teachers");
                break;
            case UserRole.Administrator:
                await _redis.RemoveAsync("admins");
                break;
        }
        
        //Отправляем событие в кафку
        var kafkaMessage = new UserRegisterEvent()
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id
        };
        
        await _userRepository.AddAsync(user);
        
        var messageJson = JsonSerializer.Serialize(kafkaMessage);
        await _kafkaProducer.ProduceAsync("notifications.register", messageJson);
        
        return RegistrationResult.Success;
    }
    
    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);

        return true;
    }

    public async Task<bool> CheckEmailExist(string email)
    {
        if (await _userRepository.GetByEmailAsync(email) is not null)
        {
            return true;
        }

        return false;
    }

    public async Task<TokenDto> RefreshTokenAsync(string accessToken, string refreshToken)
    {
        var principal = _jwtTokenGenerator.GetPrincipalFromExpiredToken(accessToken);
        if (principal == null)
            throw new SecurityTokenException("Invalid access token");

        var userId = principal.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
        var user = await _userRepository.GetByIdAsync(Guid.Parse(userId));

        if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new SecurityTokenException("Invalid refresh token");

        // Генерация новых токенов
        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        
        // Обновление refresh токена
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
        await _userRepository.UpdateAsync(user);

        return new TokenDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = _jwtSettings.RefreshTokenExpiryDays
        };
    }

    public async Task RevokeRefreshTokenAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userRepository.UpdateAsync(user);
        }
    }
    
    public async Task<bool> LogoutAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null) return false;

        // Отзываем refresh токен
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        
        if (user.ChatId != null || !string.IsNullOrEmpty(user.ChatId))
        {
            user.ChatId = null;
        }

        await _userRepository.UpdateAsync(user);
        return true;
    }
}

public enum RegistrationResult
{
    Success,
    EmailAlreadyExists,
    PasswordsDoNotMatch,
    InsufficientPermissions,
    InvalidGroupAssignment,
    GroupNotFound,
    DeviceTokenNullError,
    DeviceTokenAlreadyExists
}

public enum LoginResult
{
    Success,
    NotActivated,
    IncorrectData,
    AlreadyInUse
}