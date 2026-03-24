namespace Core.Models;

public class NotificationReply:BaseEntity
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; }
    
    // Навигационные свойства
    public Notification Notification { get; set; }
    public User User { get; set; }
}