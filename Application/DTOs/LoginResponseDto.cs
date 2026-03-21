using Application.Services;
using Core.Models;

namespace Application.DTOs;

public class LoginResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = String.Empty;
    public string? GroupName { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int AuthPeriod { get; set; }
}

public class AuthResponse<T>
{
    public LoginResult LoginResult { get; set; }
    public T? Data {get; set; }
}