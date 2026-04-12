using Core.Models;

namespace Application.DTOs;

public class ChannelMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public UserRole UserRole { get; set; }
    public string UserRoleText => GetUserRoleText();
    public ChannelRole ChannelRole { get; set; }
    public string ChannelRoleText => GetChannelRoleText();
    
    private string GetUserRoleText()
    {
        return UserRole switch
        {
            UserRole.Student => "Студент",
            UserRole.Teacher => "Учитель",
            UserRole.Administrator => "Администратор",
            UserRole.Parent => "Родитель",
            _ => "Неизвестно"
        };
    }
    
    private string GetChannelRoleText()
    {
        return ChannelRole switch
        {
            ChannelRole.Member => "Участник",
            ChannelRole.Moderator => "Модератор",
            ChannelRole.Admin => "Администратор",
            ChannelRole.Owner => "Владелец",
            _ => "Неизвестно"
        };
    }
}