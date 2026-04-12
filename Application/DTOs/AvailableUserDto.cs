using Core.Models;

namespace Application.DTOs;

public class AvailableUserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl{ get; set; }
    public UserRole Role { get; set; }
    public string RoleText => GetRoleText();
    public string FullName => $"{FirstName} {LastName}".Trim();
    
    private string GetRoleText()
    {
        return Role switch
        {
            UserRole.Student => "Студент",
            UserRole.Teacher => "Учитель",
            UserRole.Administrator => "Администратор",
            UserRole.Parent => "Родитель",
            _ => "Неизвестно"
        };
    }
}