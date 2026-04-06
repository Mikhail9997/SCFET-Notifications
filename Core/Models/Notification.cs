namespace Core.Models;

public class Notification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string? ImageUrl { get; set; } = string.Empty;
    public bool AllowReplies { get; set; }
    
    // Навигация
    public ICollection<NotificationReceiver> Receivers { get; set; } = new List<NotificationReceiver>();
    public ICollection<NotificationReply> Replies { get; set; } = new List<NotificationReply>();
    public ICollection<UserFavoriteNotification> FavoriteByUsers { get; set; } = new List<UserFavoriteNotification>();
}

public enum NotificationType
{
    Info = 1,
    Warning = 2,
    Urgent = 3,
    Event = 4
}