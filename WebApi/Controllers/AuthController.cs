using System.IdentityModel.Tokens.Jwt;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Services;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    private readonly AuthService _authService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(AuthService authService, ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }
    
    [HttpGet("debug-headers")]
    public IActionResult DebugHeaders()
    {
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
    
        return Ok(new
        {
            Headers = headers,
            AuthorizationHeader = Request.Headers["Authorization"].FirstOrDefault(),
            HasAuthorizationHeader = Request.Headers.ContainsKey("Authorization")
        });
    }
    
    [HttpGet("debug-token")]
    public IActionResult DebugToken()
    {
        // Получаем заголовок Authorization из HttpContext
        var authorizationHeader = Request.Headers["Authorization"].FirstOrDefault();
    
        Console.WriteLine($"Authorization header: {authorizationHeader}");
    
        string token;
    
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return BadRequest("No Authorization header provided");
        }
    
        // Обрабатываем оба варианта: с Bearer и без
        if (authorizationHeader.StartsWith("Bearer "))
        {
            token = authorizationHeader.Substring("Bearer ".Length).Trim();
        }
        else
        {
            token = authorizationHeader.Trim();
        }
    
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest("No token provided");
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
        
            return Ok(new
            {
                Valid = true,
                Subject = jwtToken.Subject,
                Issuer = jwtToken.Issuer,
                Audience = jwtToken.Audiences,
                Expires = jwtToken.ValidTo,
                Claims = jwtToken.Claims.Select(c => new { c.Type, c.Value })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Valid = false, Error = ex.Message });
        }
    }

    [HttpGet("test-authorization")]
    [Authorize]
    public ActionResult TestAuthorization()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        return Ok();
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto loginDto)
    {
        var result = await _authService.LoginAsync(loginDto);

        return result.LoginResult switch
        {
            LoginResult.IncorrectData => Unauthorized(new { message = "Неверный email или пароль", success = false }),
            LoginResult.NotActivated => Unauthorized(new { message = "аккаунт не активирован", success = false }),
            LoginResult.Success => Ok(new {message = "Вы успешно вошли в аккаунт", data = result.Data, success = true}),
            _ => BadRequest(new { message = "неизвестная ошибка", success = false })
        };
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody]RegisterDto registerDto)
    {
        // Для самостоятельной регистрации - только студенты
        // Администраторы могут регистрировать через другой метод
        if (registerDto.Role != UserRole.Student)
            return Forbid("Самостоятельная регистрация разрешена только для студентов");

        var result = await _authService.RegisterAsync(registerDto, null);

        return result switch
        {
            RegistrationResult.Success => Ok(new { message = "Регистрация успешна" }),
            RegistrationResult.EmailAlreadyExists => BadRequest(new { message = "Пользователь с таким email уже существует" }),
            RegistrationResult.PasswordsDoNotMatch => BadRequest(new { message = "Пароли не совпадают" }),
            RegistrationResult.InvalidGroupAssignment => BadRequest(new { message = "Некорректное назначение группы" }),
            RegistrationResult.GroupNotFound => BadRequest(new { message = "Группа не найдена" }),
            RegistrationResult.InsufficientPermissions => BadRequest(new { message = "Самостоятельная регистрация разрешена только студентам." }),
            RegistrationResult.DeviceTokenNullError => BadRequest(new {message = "Для студента нужно указать telegramId", Type = result}),
            RegistrationResult.DeviceTokenAlreadyExists => BadRequest(new {message = "Пользователь с таким telegramId уже существует", Type = result}),
            _ => BadRequest(new { message = "Ошибка регистрации" })
        };
    }
    
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var success = await _authService.ChangePasswordAsync(
            _currentUserService.UserId.Value,
            changePasswordDto.CurrentPassword,
            changePasswordDto.NewPassword);

        if (!success)
            return BadRequest(new { message = "Неверный текущий пароль" });

        return Ok(new { message = "Пароль успешно изменен" });
    }
    
    [HttpPost("register-by-admin")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> RegisterByAdmin(RegisterDto registerDto)
    {
        var result = await _authService.RegisterAsync(registerDto, _currentUserService.UserId);

        return result switch
        {
            RegistrationResult.Success => Ok(new { message = "Пользователь успешно зарегистрирован" }),
            RegistrationResult.EmailAlreadyExists => BadRequest(new { message = "Пользователь с таким email уже существует" }),
            RegistrationResult.PasswordsDoNotMatch => BadRequest(new { message = "Пароли не совпадают" }),
            RegistrationResult.InvalidGroupAssignment => BadRequest(new { message = "Некорректное назначение группы" }),
            RegistrationResult.GroupNotFound => BadRequest(new { message = "Группа не найдена" }),
            _ => BadRequest(new { message = "Ошибка регистрации" })
        };
    }

    [HttpPost("register-employee")]
    public async Task<IActionResult> RegisterEmployee(RegisterDto registerDto)
    {
        // Регистрация для администраторов и учителей без аунтификации
        var result = await _authService.RegisterAsync(registerDto);

        return result switch
        {
            RegistrationResult.Success => Ok(new { message = "Пользователь успешно зарегистрирован", success = true }),
            RegistrationResult.EmailAlreadyExists => BadRequest(new { message = "Пользователь с таким email уже существует", success = false  }),
            RegistrationResult.PasswordsDoNotMatch => BadRequest(new { message = "Пароли не совпадают", success = false }),
            RegistrationResult.InvalidGroupAssignment => BadRequest(new { message = "Некорректное назначение группы" , success = false}),
            RegistrationResult.GroupNotFound => BadRequest(new { message = "Группа не найдена", success = false }),
            RegistrationResult.InsufficientPermissions => BadRequest(new { message = "Разрешена регистрация только для учителей и администраторов", success = false }),
            _ => BadRequest(new { message = "Ошибка регистрации", success = false })
        };
    }
    
    [HttpGet("check-email-exist/{email}")]
    public async Task<IActionResult> CheckEmailExist(string email)
    {
        try
        {
            if (await _authService.CheckEmailExist(email))
            {
                return Ok(new { message = $"Пользователь с email:{email} существует" });
            }
            return BadRequest(new { message = $"Пользователь с email:{email} не существует" });
        }
        catch(Exception ex)
        {
            return BadRequest(new { message = $"Внутренняя ошибка сервера" });
        }
    }

    //[Authorize]
    [HttpPut("{userId}/logout")]
    public async Task<IActionResult> Logout(Guid userId)
    {
        //if (!_currentUserService.UserId.HasValue)
            //return Unauthorized();

        var success = await _authService.LogoutAsync(userId);
        
        if (!success)
            return BadRequest(new { message = "Пользователь не найден" });

        return Ok(new { message = "Успешно" });
    }
}