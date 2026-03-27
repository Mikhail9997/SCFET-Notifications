using Core.Models;

namespace Application.DTOs;

public class SentNotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPersonal { get; set; }
    public bool AllowReplies { get; set; }
    public int TotalReceivers { get; set; }
    public int ReadReceivers { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
}