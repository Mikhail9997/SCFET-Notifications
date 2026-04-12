using Core.Models;

namespace Application.DTOs;

public class UpdateMemberRoleDto
{
    public Guid ChannelId { get; set; }
    public Guid UserId { get; set; }
    public ChannelRole NewRole { get; set; }
}