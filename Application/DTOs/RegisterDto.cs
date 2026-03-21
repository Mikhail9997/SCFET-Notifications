using Core.Models;

namespace Application.DTOs;

public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? GroupId { get; set; } // Для студентов - привязка к группе
    public string? TelegramId { get; set; } = string.Empty;
    public string? ChatId { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}