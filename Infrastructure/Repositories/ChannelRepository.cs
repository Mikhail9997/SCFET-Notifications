using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ChannelRepository : BaseRepository<Channel>, IChannelRepository
{
    public ChannelRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Channel?> GetByNameAsync(string name)
    {
        return await _context.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }

    public async Task<List<Channel>> FilterAsync(string name)
    {
        return await _context.Channels
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Channel?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.Channels
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .ThenInclude(cu => cu.User)
            .Include(c => c.Invitations)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Channel>> GetUserOwnedChannelsAsync(Guid userId)
    {
        return await _context.Channels
            .Include(c => c.ChannelUsers)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<PagedResult<Channel>> GetUserChannelsPaginatedAsync(Guid userId, ChannelFilterEntity filter)
    {
        var query = _context.Channels
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .Where(c => c.ChannelUsers.Any(cu => cu.UserId == userId))
            .AsSplitQuery();

        (query, int totalCount) = await ApplyChannelFiltersAsync(query, filter);
        
        var channels = await query.ToListAsync();

        return new PagedResult<Channel>
        {
            Items = channels,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PagedResult<Channel>> GetAllChannelsPaginatedAsync(ChannelFilterEntity filter)
    {
        var query = _context.Channels
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .AsSplitQuery();

        (query, int totalCount) = await ApplyChannelFiltersAsync(query, filter);
        
        var channels = await query.ToListAsync();

        return new PagedResult<Channel>
        {
            Items = channels,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    private async Task<(IQueryable<Channel> channels, int totalCount)> ApplyChannelFiltersAsync(
    IQueryable<Channel> query, ChannelFilterEntity filter)
    {
        // Поиск по названию или описанию
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(c => 
                c.Name.ToLower().Contains(searchTerm) || 
                (c.Description != null && c.Description.ToLower().Contains(searchTerm)));
        }

        // Фильтр по дате создания
        if (filter.StartDate.HasValue)
        {
            var startDate = new DateTime(
                filter.StartDate.Value.Year,
                filter.StartDate.Value.Month,
                filter.StartDate.Value.Day,
                0, 0, 0, DateTimeKind.Utc);
        
            query = query.Where(c => c.CreatedAt >= startDate);
        }

        if (filter.EndDate.HasValue)
        {
            var endDate = new DateTime(
                filter.EndDate.Value.Year,
                filter.EndDate.Value.Month,
                filter.EndDate.Value.Day,
                23, 59, 59, DateTimeKind.Utc);
        
            query = query.Where(c => c.CreatedAt <= endDate);
        }

        var totalCount = await query.CountAsync();
        
        // Сортировка
        switch (filter.SortBy)
        {
            case ChannelSortBy.Name:
            case ChannelSortBy.Title:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(c => c.Name)
                    : query.OrderByDescending(c => c.Name);
                break;
                
            case ChannelSortBy.MembersCount:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(c => c.ChannelUsers.Count)
                    : query.OrderByDescending(c => c.ChannelUsers.Count);
                break;
                
            case ChannelSortBy.CreatedAt:
            default:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(c => c.CreatedAt)
                    : query.OrderByDescending(c => c.CreatedAt);
                break;
        }

        var channels = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return (channels, totalCount);
    }
    
    public override async Task<Channel?> GetByIdAsync(Guid id)
    {
        return await _context.Channels
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public override async Task<IReadOnlyList<Channel>> GetAllAsync()
    {
        return await _context.Channels
            .Include(c => c.Owner)
            .Include(c => c.ChannelUsers)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}