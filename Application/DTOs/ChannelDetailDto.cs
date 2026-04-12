namespace Application.DTOs;

public class ChannelDetailDto : ChannelDto
{
    public List<ChannelMemberDto> Members { get; set; } = new();
    public List<ChannelInvitationDto> PendingInvitations { get; set; } = new();
    public ChannelStatisticsDto Statistics { get; set; } = new();
}