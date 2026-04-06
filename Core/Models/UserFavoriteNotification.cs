namespace Core.Models;

public class UserFavoriteNotification
{
    public Guid UserId { get; set; }
    public Guid NotificationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационные свойства
    public User User { get; set; }
    public Notification Notification { get; set; }
}