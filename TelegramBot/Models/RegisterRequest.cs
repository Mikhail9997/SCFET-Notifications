namespace TelegramBot.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public Guid? GroupId { get; set; }
    public string? ChatId { get; set; } = string.Empty;
    public string? TelegramId { get; set; } = string.Empty;
}

public class RegisterError
{
    public string Message { get; set; } = string.Empty;
    public RegistrationResult Type { get; set; }
}

public enum UserRole
{
    Student = 1,
    Teacher = 2,
    Administrator = 3
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
    DeviceTokenAlreadyExists,
    UnknownError
}