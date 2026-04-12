using Core.Models;

namespace Application.DTOs;

public class AddMembersDto
{
    public List<Guid> UserIds { get; set; } = new();
    public ChannelRole InitialRole { get; set; } = ChannelRole.Member;
}