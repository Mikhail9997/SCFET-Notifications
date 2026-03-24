using Core.Models;
using Microsoft.AspNetCore.Http;

namespace Application.DTOs;

public class NotificationActionDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool AllowReplies { get; set; }
    public NotificationType Type { get; set; }
    public List<Guid>? TargetUserIds { get; set; }
    public Guid? TargetGroupId { get; set; }
    public IFormFile? Image { get; set; }
}