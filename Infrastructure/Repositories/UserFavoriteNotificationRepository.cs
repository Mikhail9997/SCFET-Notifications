using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserFavoriteNotificationRepository: BaseRepository<UserFavoriteNotification>, IUserFavoriteNotificationRepository
{
    public UserFavoriteNotificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<UserFavoriteNotification>> GetAllByUserIdAsync(Guid userId)
    {
        return await _context.UserFavoriteNotifications
            .Include(f => f.User)
            .Include(f => f.Notification)
            .Where(f => f.UserId == userId)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<UserFavoriteNotification>> GetAllByNotificationIdAsync(Guid notificationId)
    {
        return await _context.UserFavoriteNotifications
            .Include(f => f.User)
            .Include(f => f.Notification)
            .Where(f => f.NotificationId == notificationId)
            .ToListAsync();
    }

    public async Task<PagedResult<UserFavoriteNotification>> GetMyAsync(Guid userId, FilterEntity filter)
    {
        var query = _context.UserFavoriteNotifications
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Notification)
                .ThenInclude(n => n.Receivers)
            .Include(f => f.Notification)
                .ThenInclude(n => n.Sender)
            .Where(f => f.UserId == userId)
            .AsSplitQuery();
        
        (query, int totalCount) = await ApplyFiltersAsync(query, filter);
        
        var items = await query.ToListAsync();
        
        return new PagedResult<UserFavoriteNotification>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<UserFavoriteNotification?> GetAsync(Guid userId, Guid notificationId)
    {
        return await _context.UserFavoriteNotifications
            .FirstOrDefaultAsync(f => f.UserId == userId && f.NotificationId == notificationId);
    }
    
    private async Task<(IQueryable<UserFavoriteNotification> favorites, int totalCount)> ApplyFiltersAsync(IQueryable<UserFavoriteNotification> query,
        FilterEntity filter)
    {
        if (filter.StartDate.HasValue)
        {
            var startDate = new DateTime(
                filter.StartDate.Value.Year,
                filter.StartDate.Value.Month,
                filter.StartDate.Value.Day,
                0, 0, 0, DateTimeKind.Utc);
        
            query = query.Where(n => n.CreatedAt >= startDate);
        }

        if (filter.EndDate.HasValue)
        {
            var endDate = new DateTime(
                filter.EndDate.Value.Year,
                filter.EndDate.Value.Month,
                filter.EndDate.Value.Day,
                23, 59, 59, DateTimeKind.Utc);
        
            query = query.Where(n => n.CreatedAt <= endDate);
        }
        var totalCount = await query.CountAsync();
        
        switch (filter.SortBy)
        {
            case SortBy.Title:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(f => f.Notification.Title)
                    : query.OrderByDescending(f => f.Notification.Title);
                break;
                
            case SortBy.CreatedAt:
            default:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(f => f.CreatedAt)
                    : query.OrderByDescending(f => f.CreatedAt);
                break;
        }

        var favorites = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return (favorites, totalCount);
    }
}