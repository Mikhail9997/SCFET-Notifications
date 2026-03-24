using Core.Models;

namespace Application.DTOs;

public class NotificationDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public bool IsPersonal { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
}