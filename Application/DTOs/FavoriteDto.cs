namespace Application.DTOs;

public class FavoriteDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string SenderAvatarUrl  { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public bool IsPersonal { get; set; }
    public bool AllowReplies { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
    public bool IsEnable { get; set; }
}