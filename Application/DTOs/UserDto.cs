using Core.Models;

namespace Application.DTOs;

public class UserDto:BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public UserRole Role { get; set; }
    public Guid? GroupId { get; set; }
    public GroupDto? Group { get; set; }
    public string? DeviceToken { get; set; }
    public string? ChatId { get; set; }
}