using Core.Models;

namespace Application.DTOs;

public class NotificationReceiverDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsRead { get; set; }
}