namespace TelegramBotAdministrators.Models;

public class LoginResponseDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class AuthResponse<T>
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public T? Data {get; set; }
}