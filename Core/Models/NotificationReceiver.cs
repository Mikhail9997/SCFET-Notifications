namespace Core.Models;

public class NotificationReceiver : BaseEntity
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsRead { get; set; }
    
    // Навигация
    public Notification Notification { get; set; } = null!;
    public User User { get; set; } = null!;
}