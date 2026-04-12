namespace Application.DTOs;

public class ReplyMessageDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}