using Core.Models;

namespace Application.Messages.Kafka;

public class NotificationKafkaMessage
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public List<Guid> RecipientUserIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ImageUrl { get; set; } = string.Empty;
}