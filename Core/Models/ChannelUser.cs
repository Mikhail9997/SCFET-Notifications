namespace Core.Models;

public class ChannelUser : BaseEntity
{
    public Guid ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public ChannelRole Role { get; set; }
}

public enum ChannelRole
{
    Member = 1,
    Moderator = 2,
    Admin = 3,
    Owner = 4
}