using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ChannelUserRepository : BaseRepository<ChannelUser>, IChannelUserRepository
{
    public ChannelUserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ChannelUser?> GetByChannelAndUserAsync(Guid channelId, Guid userId)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.Channel)
            .Include(cu => cu.User)
            .FirstOrDefaultAsync(cu => cu.ChannelId == channelId && cu.UserId == userId);
    }

    public async Task<List<ChannelUser>> GetChannelMembersAsync(Guid channelId)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.User)
            .Where(cu => cu.ChannelId == channelId)
            .OrderBy(cu => cu.Role)
            .ThenBy(cu => cu.User.LastName)
            .ToListAsync();
    }

    public async Task<List<ChannelUser>> GetUserMembershipsAsync(Guid userId)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.Channel)
            .Where(cu => cu.UserId == userId)
            .OrderBy(cu => cu.Channel.Name)
            .ToListAsync();
    }

    public async Task<bool> IsUserInChannelAsync(Guid channelId, Guid userId)
    {
        return await _context.ChannelUsers
            .AnyAsync(cu => cu.ChannelId == channelId && 
                           cu.UserId == userId);
    }

    public async Task<ChannelRole?> GetUserRoleInChannelAsync(Guid channelId, Guid userId)
    {
        var channelUser = await _context.ChannelUsers
            .FirstOrDefaultAsync(cu => cu.ChannelId == channelId && 
                                       cu.UserId == userId);
        
        return channelUser?.Role;
    }

    public async Task<List<Channel>> GetUserChannelsWithDetailsAsync(Guid userId)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.Channel)
                .ThenInclude(c => c.Owner)
            .Include(cu => cu.Channel)
                .ThenInclude(c => c.ChannelUsers)
            .Where(cu => cu.UserId == userId)
            .Select(cu => cu.Channel)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<int> GetChannelMembersCountAsync(Guid channelId)
    {
        return await _context.ChannelUsers
            .Where(cu => cu.ChannelId == channelId)
            .CountAsync();
    }

    public override async Task<ChannelUser?> GetByIdAsync(Guid id)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.Channel)
            .Include(cu => cu.User)
            .FirstOrDefaultAsync(cu => cu.Id == id);
    }
}