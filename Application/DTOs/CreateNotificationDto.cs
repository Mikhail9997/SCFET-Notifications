using Core.Models;
using Microsoft.AspNetCore.Http;

namespace Application.DTOs;

public class CreateNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public List<Guid>? TargetUserIds { get; set; }
    public Guid? TargetGroupId { get; set; }
    public IFormFile? Image { get; set; }
}