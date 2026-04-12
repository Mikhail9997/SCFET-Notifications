namespace Core.Models;

public class ChannelMessage : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    
    public Guid ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    
    public Guid? ReplyToMessageId { get; set; }
    public ChannelMessage? ReplyToMessage { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Навигационные свойства
    public ICollection<ChannelMessage> Replies { get; set; } = new List<ChannelMessage>();
}