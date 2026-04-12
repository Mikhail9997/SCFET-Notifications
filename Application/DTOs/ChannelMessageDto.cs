using Core.Models;

namespace Application.DTOs;

public class ChannelMessageDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid ChannelId { get; set; }
    
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public UserRole SenderRole { get; set; }
    public ChannelRole? SenderChannelRole { get; set; }
    
    public Guid? ReplyToMessageId { get; set; }
    public ReplyMessageDto? ReplyToMessage { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public bool IsEdited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}