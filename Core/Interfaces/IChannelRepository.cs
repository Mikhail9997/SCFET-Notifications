using Core.Models;

namespace Core.Interfaces;

public interface IChannelRepository:IRepository<Channel>
{
    Task<Channel?> GetByNameAsync(string name);
    Task<List<Channel>> FilterAsync(string name);
    Task<Channel?> GetByIdWithDetailsAsync(Guid id);
    Task<List<Channel>> GetUserOwnedChannelsAsync(Guid userId);
    Task<PagedResult<Channel>> GetUserChannelsPaginatedAsync(Guid userId, ChannelFilterEntity filter);
    Task<PagedResult<Channel>> GetAllChannelsPaginatedAsync(ChannelFilterEntity filter);
}