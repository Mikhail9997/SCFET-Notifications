namespace TelegramBotEmployee.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public string? TelegramId { get; set; } = string.Empty;
    public string? ChatId { get; set; } = string.Empty;
}

public enum UserRole
{
    Student = 1,
    Teacher = 2,
    Administrator = 3
}