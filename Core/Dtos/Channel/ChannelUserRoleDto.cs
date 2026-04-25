using Core.Models;

namespace Core.Dtos.Channel;

public class ChannelUserRoleDto
{
    public Guid UserId { get; set; }
    public ChannelRole Role { get; set; }
}