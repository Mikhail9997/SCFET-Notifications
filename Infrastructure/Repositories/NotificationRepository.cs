using System.Linq.Expressions;
using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class NotificationRepository : BaseRepository<Notification>, INotificationRepository
{
    public NotificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(Guid userId)
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .Where(n => n.Receivers.Any(r => r.UserId == userId))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationsWithReceiversAsync(Guid notificationId)
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .ThenInclude(r => r.User)
            .Where(n => n.Id == notificationId)
            .ToListAsync();
    }
    
    public override async Task<Notification?> GetByIdAsync(Guid id)
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
                .ThenInclude(r => r.User)
            .Include(n => n.FavoriteByUsers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public override async Task<IReadOnlyList<Notification>> GetAllAsync()
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<PagedResult<Notification>> GetBySenderIdAsync(Guid senderId, FilterEntity filter)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .Where(n => n.SenderId == senderId)
            .AsSplitQuery();
        
        (query, int totalCount) = await ApplyFiltersAsync(query, filter);
        
        var notifications = await query.ToListAsync();
        
        return new PagedResult<Notification>
        {
            Items = notifications,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
    
    public async Task<PagedResult<Notification>> GetUserNotificationsAsync(
        Guid userId, FilterEntity filter)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .Include(n => n.FavoriteByUsers)
            .Where(n => n.Receivers.Any(r => r.UserId == userId))
            .AsSplitQuery();

        (query, int totalCount) = await ApplyFiltersAsync(query, filter);
        
        var notifications = await query.ToListAsync();

        return new PagedResult<Notification>
        {
            Items = notifications,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Notification?> GetByIdWithReceiversAsync(Guid id)
    {
        return await _context.Notifications
            .Include(n => n.Sender)
            .Include(n => n.Receivers)
            .FirstOrDefaultAsync(n => n.Id == id);
    }
    
    private async Task<(IQueryable<Notification> notifications, int totalCount)> ApplyFiltersAsync(IQueryable<Notification> query,
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
                    ? query.OrderBy(n => n.Title)
                    : query.OrderByDescending(n => n.Title);
                break;
                
            case SortBy.CreatedAt:
            default:
                query = filter.SortOrder == SortOrder.Ascending
                    ? query.OrderBy(n => n.CreatedAt)
                    : query.OrderByDescending(n => n.CreatedAt);
                break;
        }

        var notifications = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return (notifications, totalCount);
    }
}