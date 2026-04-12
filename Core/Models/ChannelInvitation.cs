namespace Core.Models;

public class ChannelInvitation : BaseEntity
{
    public Guid ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    
    public Guid InviterId { get; set; }
    public User Inviter { get; set; } = null!;
    
    public Guid InviteeId { get; set; }
    public User Invitee { get; set; } = null!;
    
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public string? Message { get; set; } // Дополнительное сообщение от приглашающего
}
public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4
}