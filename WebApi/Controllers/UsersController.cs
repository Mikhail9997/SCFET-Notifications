using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Events;
using Application.Services;
using AutoMapper;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController: ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IKafkaProducer _producer;
    private readonly IConfiguration _configuration;
    private readonly RedisService _redis;
    private readonly IAvatarService _avatarService;

    public UsersController(IUserRepository userRepository, ICurrentUserService currentUserService, 
        IMapper mapper, IKafkaProducer producer, IConfiguration configuration,
        RedisService redis, IAvatarService avatarService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _producer = producer;
        _configuration = configuration;
        _redis = redis;
        _avatarService = avatarService;
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        User? user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        var profileDto = _mapper.Map<ProfileDto>(user);
        profileDto.AvatarUrl = await _avatarService.GetAvatarUrl(user.AvatarPresetKey);
        
        return Ok(profileDto);
    }
    
    [HttpGet("students")]
    [Authorize(Roles = "Teacher,Administrator")]
    public async Task<IActionResult> GetStudents([FromQuery] FilterDto query)
    {
        string cacheKey = $"students_{JsonSerializer.Serialize(query)}";
        int hours = int.Parse(_configuration["Redis:Hours"] ?? "1");
        int minutes = int.Parse(_configuration["Redis:Minutes"] ?? "5");
        // Пытаемся взять из кэша
        var cachedResult = await _redis.GetAsync<List<UserDto>>(cacheKey);
        if (cachedResult != null && cachedResult.Any())
        {
            return Ok(cachedResult.Select(s => new
            {
                UserId = s.Id,
                s.FirstName,
                s.LastName,
                s.Email,
                s.PhoneNumber,
                Group = s.Group?.Name,
                GroupId = s.GroupId,
                Role = UserRole.Student.ToString()
            }));
        }
        // сначала пытаемся взять из кеша всех студентов
        List<User>? students = await _redis.GetAsync<List<User>>("students");
        if (students == null || !students.Any())
        {
            students = (List<User>)await _userRepository.GetUsersByRoleAsync(UserRole.Student);
            //Кешируем результат
            await _redis.SetAsync("students", _mapper.Map<List<UserDto>>(students), TimeSpan.FromHours(hours));
        }
        students = (List<User>)await _userRepository.FilterAsync(_mapper.Map<FilterEntity>(query), students);
        // Кешируем результат с фильтрацией
        await _redis.SetAsync(cacheKey, _mapper.Map<List<UserDto>>(students), TimeSpan.FromMinutes(minutes));
        return Ok(students.Select(s => new
        {
            UserId = s.Id,
            s.FirstName,
            s.LastName,
            s.Email,
            s.PhoneNumber,
            Group = s.Group?.Name,
            GroupId = s.GroupId,
            Role = UserRole.Student.ToString()
        }));
    }
    
    [HttpGet("teachers")]
    [Authorize(Roles = "Administrator, Teacher")]
    public async Task<IActionResult> GetTeachers([FromQuery] FilterDto query)
    {
        string cacheKey = $"teachers_{JsonSerializer.Serialize(query)}";
        int hours = int.Parse(_configuration["Redis:Hours"] ?? "1");
        int minutes = int.Parse(_configuration["Redis:Minutes"] ?? "5");
        
        // Пытаемся взять из кэша
        var cachedResult = await _redis.GetAsync<List<UserDto>>(cacheKey);
        if (cachedResult != null && cachedResult.Any())
        {
            return Ok(cachedResult.Select(t => new
            {
                UserId = t.Id,
                t.FirstName,
                t.LastName,
                t.Email,
                t.PhoneNumber,
                Role = UserRole.Teacher.ToString()
            }));
        }
        //сначала пытаемся взять из кеша всех учителей
        List<User>? teachers = await _redis.GetAsync<List<User>>("teachers");
        if (teachers == null || !teachers.Any())
        {
            teachers = (List<User>)await _userRepository.GetUsersByRoleAsync(UserRole.Teacher);
            //Кешируем результат
            await _redis.SetAsync("teachers", _mapper.Map<List<UserDto>>(teachers), TimeSpan.FromHours(hours));
        }
        teachers = (List<User>)await _userRepository.FilterAsync(_mapper.Map<FilterEntity>(query), teachers);
        // Кешируем результат с фильтрацией
        await _redis.SetAsync(cacheKey, _mapper.Map<List<UserDto>>(teachers), TimeSpan.FromMinutes(minutes));
        return Ok(teachers.Select(t => new
        {
            UserId = t.Id,
            t.FirstName,
            t.LastName,
            t.Email,
            t.PhoneNumber,
            Role = UserRole.Teacher.ToString()
        }));
    }
    
    [HttpGet("administrators")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> GetAdministrators([FromQuery] FilterDto query)
    {
        string cacheKey = $"admins_{JsonSerializer.Serialize(query)}";
        int hours = int.Parse(_configuration["Redis:Hours"] ?? "1");
        int minutes = int.Parse(_configuration["Redis:Minutes"] ?? "5");
        
        // Пытаемся взять из кэша
        var cachedResult = await _redis.GetAsync<List<UserDto>>(cacheKey);
        if (cachedResult != null && cachedResult.Any())
        {
            return Ok(cachedResult.Select(a => new
            {
                UserId = a.Id,
                a.FirstName,
                a.LastName,
                a.Email,
                a.PhoneNumber,
                a.ChatId,
                Role = UserRole.Administrator.ToString()
            }));
        }
        //сначала пытаемся взять из кеша всех админов
        List<User>? admins = await _redis.GetAsync<List<User>>("admins");
        if (admins == null || !admins.Any())
        {
            admins = (List<User>)await _userRepository.GetUsersByRoleAsync(UserRole.Administrator);
            //Кешируем результат
            await _redis.SetAsync("admins", _mapper.Map<List<UserDto>>(admins), TimeSpan.FromHours(hours));
        }
        admins = (List<User>)await _userRepository.FilterAsync(_mapper.Map<FilterEntity>(query), admins);
        // Кешируем результат с фильтрацией
        await _redis.SetAsync(cacheKey, _mapper.Map<List<UserDto>>(admins), TimeSpan.FromMinutes(minutes));
        return Ok(admins.Select(a => new
        {
            UserId = a.Id,
            a.FirstName,
            a.LastName,
            a.PhoneNumber,
            a.Email,
            a.ChatId,
            Role = UserRole.Administrator.ToString()
        }));
    }
    
    [HttpGet("parents")]
    [Authorize(Roles = "Administrator, Teacher")]
    public async Task<IActionResult> GetParents([FromQuery] FilterDto query)
    {
        string cacheKey = $"parents_{JsonSerializer.Serialize(query)}";
        int hours = int.Parse(_configuration["Redis:Hours"] ?? "1");
        int minutes = int.Parse(_configuration["Redis:Minutes"] ?? "5");
        
        // Пытаемся взять из кэша
        var cachedResult = await _redis.GetAsync<List<UserDto>>(cacheKey);
        if (cachedResult != null && cachedResult.Any())
        {
            return Ok(cachedResult.Select(p => new
            {
                UserId = p.Id,
                p.FirstName,
                p.LastName,
                p.Email,
                p.PhoneNumber,
                Role = p.Role.ToString()
            }));
        }
        //сначала пытаемся взять из кеша всех родителей
        List<User>? parents = await _redis.GetAsync<List<User>>("parents");
        if (parents == null || !parents.Any())
        {
            parents = (List<User>)await _userRepository.GetUsersByRoleAsync(UserRole.Parent);
            //Кешируем результат
            await _redis.SetAsync("parents", _mapper.Map<List<UserDto>>(parents), TimeSpan.FromHours(hours));
        }
        parents = (List<User>)await _userRepository.FilterAsync(_mapper.Map<FilterEntity>(query), parents);
        // Кешируем результат с фильтрацией
        await _redis.SetAsync(cacheKey, _mapper.Map<List<UserDto>>(parents), TimeSpan.FromMinutes(minutes));
        return Ok(parents.Select(p => new
        {
            UserId = p.Id,
            p.FirstName,
            p.LastName,
            p.Email,
            p.PhoneNumber,
            Role = p.Role.ToString()
        }));
    }
    
    [HttpGet("administrators-common")]
    public async Task<IActionResult> GetAdministratorsCommon()
    {
        var administrators = await _userRepository.GetUsersByRoleAsync(UserRole.Administrator);
        return Ok(administrators.Select(u => new UserCommonDto()
        {
            UserId = u.Id,
            IsActive = u.IsActive,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            ChatId = u.ChatId,
        }));
    }

    [HttpGet("students-common")]
    public async Task<IActionResult> GetStudentsCommon()
    {
        var students = await _userRepository.GetUsersByRoleAsync(UserRole.Student);
        
        return Ok(students.Select(u => new UserCommonDto()
        {
            UserId = u.Id,
            IsActive = u.IsActive,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            ChatId = u.ChatId,
        }));
    }
    
    [HttpGet("teachers-common")]
    public async Task<IActionResult> GetTeachersCommon()
    {
        var teachers = await _userRepository.GetUsersByRoleAsync(UserRole.Teacher);
        
        return Ok(teachers.Select(u => new UserCommonDto()
        {
            UserId = u.Id,
            IsActive = u.IsActive,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            ChatId = u.ChatId,
        }));
    }

    [HttpGet("parents-common")]
    public async Task<IActionResult> GetParentsCommon()
    {
        IReadOnlyList<User> parents = await _userRepository.GetUsersByRoleAsync(UserRole.Parent);
        
        return Ok(parents.Select(u => new UserCommonDto()
        {
            UserId = u.Id,
            IsActive = u.IsActive,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            ChatId = u.ChatId,
        }));
    }
    
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateDto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден", success=false });

        // Проверка уникальности email
        if (!await _userRepository.IsEmailUniqueAsync(updateDto.Email, user.Id))
            return BadRequest(new { message = "Пользователь с таким email уже существует", success=false });

        user.FirstName = updateDto.FirstName;
        user.LastName = updateDto.LastName;
        user.Email = updateDto.Email;
        user.PhoneNumber = updateDto.PhoneNumber;

        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "Профиль успешно обновлен", success=true });
    }
    
    [Authorize(Roles = "Administrator")]
    [HttpPost("{userId}/device-token")]
    public async Task<IActionResult> UpdateDeviceToken(Guid userId, [FromBody] DeviceTokenDto tokenDto)
    {
        if (_currentUserService.UserId != userId && _currentUserService.Role != UserRole.Administrator)
            return Forbid("Нет прав для обновления device token");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        user.DeviceToken = tokenDto.Token;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "Device token обновлен" });
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("{userId}/activate")]
    public async Task<IActionResult> ActivateUser(Guid userId)
    {
        if (_currentUserService.UserId != userId && _currentUserService.Role != UserRole.Administrator)
        {
            return Unauthorized(new {message = "Нет прав для активирования", success = false});
        }
        
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден или уже деактивирован", success = false});

        if (user.IsActive)
        {
            return BadRequest(new {message = "Пользователь уже активирован", success = false});
        }

        user.IsActive = true;
        await _userRepository.UpdateAsync(user);
        
        //очищаем кеш
        await ClearCache(user.Role);
        
        //Отправляем сообщение в кафку
        var userActivatedEvent = new UserIsActiveEvent()
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            ChatId = user.ChatId ?? string.Empty,
            IsActive = true,
            UserId = user.Id
        };
        var message = JsonSerializer.Serialize(userActivatedEvent);
        var topic = _configuration["Kafka:Topics:UserIsActive"] ?? "notifications.userIsActiveChange";
        await _producer.ProduceAsync(topic, message);
        
        return Ok(new { message = "Пользователь успешно активирован", success = true});
    }
    
    [Authorize(Roles = "Administrator")]
    [HttpPut("{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId)
    {
        if (_currentUserService.UserId != userId && _currentUserService.Role != UserRole.Administrator)
        {
            return Unauthorized(new {message = "Нет прав для активирования", success = false});
        }
        
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден или уже деактивирован", success = false});

        if (!user.IsActive)
        {
            return BadRequest(new {message = "Пользователь уже деактивирован", success = false});
        }

        user.IsActive = false;
        await _userRepository.UpdateAsync(user);
        
        //очищаем кеш
        await ClearCache(user.Role);
        
        //Отправляем сообщение в кафку
        var userActivatedEvent = new UserIsActiveEvent()
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            ChatId = user.ChatId ?? string.Empty,
            IsActive = false,
            UserId = user.Id
        };
        var message = JsonSerializer.Serialize(userActivatedEvent);
        var topic = _configuration["Kafka:Topics:UserIsActive"] ?? "notifications.userIsActiveChange";
        await _producer.ProduceAsync(topic, message);
        
        return Ok(new { message = "Пользователь успешно деактивирован", success = true});
    }

    [Authorize(Roles = "Administrator")]
    [HttpDelete("{userId}/remove")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        if (_currentUserService.UserId != userId && _currentUserService.Role != UserRole.Administrator)
        {
            return Unauthorized(new {message = "Нет прав для активирования", success = false});
        }
        try
        {
            var userToDelete = await _userRepository.GetByIdAsync(userId);
            if (userToDelete == null)
                return NotFound(new { message = "Пользователь не найден или уже деактивирован", success = false});
        
            await _userRepository.DeleteAsync(userToDelete);
        
            //очищаем кеш
            await ClearCache(userToDelete.Role);
            
            //Отправляем сообщение в кафку
            var userActivatedEvent = new UserIsActiveEvent()
            {
                FirstName = userToDelete.FirstName,
                LastName = userToDelete.LastName,
                Email = userToDelete.Email,
                PhoneNumber = userToDelete.PhoneNumber,
                Role = userToDelete.Role.ToString(),
                ChatId = userToDelete.ChatId ?? string.Empty,
                IsActive = false,
                UserId = userToDelete.Id
            };
            var message = JsonSerializer.Serialize(userActivatedEvent);
            var topic = _configuration["Kafka:Topics:UserIsActive"] ?? "notifications.userIsActiveChange";
            await _producer.ProduceAsync(topic, message);
            
            return Ok(new { message = "Пользователь успешно деактивирован", success = true});
        }
        catch
        {
            return StatusCode(500 ,new {Message = "Произошла внутренняя ошибка сервера", success = false});
        }
    }

    private async Task ClearCache(UserRole role)
    {
        switch (role)
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
            case UserRole.Parent:
                await _redis.RemoveAsync("parents");
                break;
        }
    }
}