namespace Core.Models;

public class Channel:BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    
    // Навигационные свойства
    public ICollection<ChannelUser> ChannelUsers { get; set; } = new List<ChannelUser>();
    public ICollection<ChannelInvitation> Invitations { get; set; } = new List<ChannelInvitation>();
    public ICollection<ChannelMessage> Messages { get; set; } = new List<ChannelMessage>();
}