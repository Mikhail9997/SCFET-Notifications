namespace Application.DTOs;

public class ReplyDto
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string UserAvatarUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}