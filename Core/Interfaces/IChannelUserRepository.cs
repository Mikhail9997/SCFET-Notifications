using Core.Dtos;
using Core.Models;

namespace Core.Interfaces;

public interface IChannelUserRepository : IRepository<ChannelUser>
{
    Task<ChannelUser?> GetByChannelAndUserAsync(Guid channelId, Guid userId);
    Task<List<ChannelUser>> GetChannelMembersAsync(Guid channelId);
    Task<PagedResult<ChannelUser>> GetChannelMembersPaginatedAsync(Guid channelId, Guid currentUserId, ChannelMemberFilter filter);
    Task<List<ChannelUser>> GetUserMembershipsAsync(Guid userId);
    Task<bool> IsUserInChannelAsync(Guid channelId, Guid userId);
    Task<ChannelRole?> GetUserRoleInChannelAsync(Guid channelId, Guid userId);
    Task<List<Channel>> GetUserChannelsWithDetailsAsync(Guid userId);
    Task<int> GetChannelMembersCountAsync(Guid channelId);
}