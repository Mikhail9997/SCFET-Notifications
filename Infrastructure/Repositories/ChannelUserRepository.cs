using Core.Dtos;
using Core.Dtos.Channel;
using Core.Dtos.Filters;
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

    public async Task<PagedResult<ChannelUser>> GetChannelMembersPaginatedAsync(Guid channelId, 
        Guid currentUserId,
        ChannelMemberFilter filter)
    {
        var query = _context.ChannelUsers
            .AsNoTracking()
            .Include(cu => cu.User)
            .Where(cu => cu.ChannelId == channelId)
            .AsSplitQuery();
        
        // применяем фильтры
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(cu => 
                cu.User.FirstName.ToLower().Contains(searchTerm) ||
                cu.User.LastName.ToLower().Contains(searchTerm) ||
                cu.User.Email.ToLower().Contains(searchTerm) ||
                (cu.User.FirstName.ToLower() + " " + cu.User.LastName.ToLower()).Contains(searchTerm) ||
                (cu.User.LastName.ToLower() + " " + cu.User.FirstName.ToLower()).Contains(searchTerm));
        }
        
        // Получаем общее количество
        var totalCount = await query.CountAsync();
        
        // Применяем сортировку с приоритетом
        query = ApplyPrioritySorting(query, currentUserId);
        
        // Применяем пагинацию
        var members = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
        
        return new PagedResult<ChannelUser>
        {
            Items = members,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
    
    private IQueryable<ChannelUser> ApplyPrioritySorting(IQueryable<ChannelUser> query, 
        Guid currentUserId)
    {
        // Сортировка с приоритетами:
        // 1. Владелец канала (Owner)
        // 2. Текущий пользователь (если не владелец)
        // 3. Администраторы
        // 4. Модераторы
        // 5. Участники
        // Внутри каждой группы - по имени
        
        return query
            .OrderBy(cu => cu.Channel.OwnerId == cu.UserId ? 0 : 1) // Владелец первый
            .ThenBy(cu => cu.UserId == currentUserId && cu.Channel.OwnerId != cu.UserId ? 0 : 1) // Текущий пользователь второй
            .ThenBy(cu => cu.Role == ChannelRole.Owner ? 0 :
                          cu.Role == ChannelRole.Admin ? 1 :
                          cu.Role == ChannelRole.Moderator ? 2 : 3) // Затем по роли
            .ThenBy(cu => cu.User.LastName) // Затем по фамилии
            .ThenBy(cu => cu.User.FirstName); // Затем по имени
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

    public async Task<List<ChannelUserRoleDto>> GetUsersRolesInChannelAsync(
        Guid channelId, IEnumerable<Guid> userIds)
    {
        var userIdsSet = userIds.ToHashSet();
        
        if (!userIdsSet.Any())
            return new List<ChannelUserRoleDto>();

        return await _context.ChannelUsers
            .Where(cu => cu.ChannelId == channelId && userIdsSet.Contains(cu.UserId))
            .Select(cu => new ChannelUserRoleDto
            {
                UserId = cu.UserId,
                Role = cu.Role
            })
            .ToListAsync();
    }

    public override async Task<ChannelUser?> GetByIdAsync(Guid id)
    {
        return await _context.ChannelUsers
            .Include(cu => cu.Channel)
            .Include(cu => cu.User)
            .FirstOrDefaultAsync(cu => cu.Id == id);
    }
}