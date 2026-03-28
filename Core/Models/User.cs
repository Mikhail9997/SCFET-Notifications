namespace Core.Models;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public string? TelegramId { get; set; }
    public string? ChatId { get; set; }
    public string? RefreshToken { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public UserRole Role { get; set; }
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    // Навигация
    public ICollection<Notification> SentNotifications { get; set; } = new List<Notification>();
    public ICollection<NotificationReceiver> ReceivedNotifications { get; set; } = new List<NotificationReceiver>();
    public ICollection<NotificationReply> Replies { get; set; } = new List<NotificationReply>();
}

public enum UserRole
{
    Student = 1,
    Teacher = 2,
    Administrator = 3,
    Parent = 4
}