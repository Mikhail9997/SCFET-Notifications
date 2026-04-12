namespace Application.DTOs;

public class ChannelStatisticsDto
{
    public int TotalMembers { get; set; }
    public int TotalInvitations { get; set; }
    public int PendingInvitations { get; set; }
    public int AcceptedInvitations { get; set; }
    public int DeclinedInvitations { get; set; }
}