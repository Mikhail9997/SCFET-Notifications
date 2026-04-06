using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class NotificationReplyRepository:BaseRepository<NotificationReply>, INotificationReplyRepository
{
    public NotificationReplyRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<NotificationReply?> GetByIdAsync(Guid id)
    {
        return await _context.NotificationReplies
            .Include(nr => nr.Notification)
            .ThenInclude(n => n.Receivers)
            .Include(nr => nr.User)
            .FirstOrDefaultAsync(nr => nr.Id == id);
    }

    public async Task<PagedResult<NotificationReply>> GetNotificationsReplyByNotificationId(Guid notificationId, FilterEntity filter)
    {
        IQueryable<NotificationReply> query = _context.NotificationReplies
            .AsNoTracking()
            .Include(nr => nr.User)
            .Where(nr => nr.NotificationId == notificationId);

        (query, int totalCount) = await ApplyFiltersAsync(query, filter);

        List<NotificationReply> notificationReplies = await query.ToListAsync();
        
        return new PagedResult<NotificationReply>
        {
            Items = notificationReplies,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
    
    private async Task<(IQueryable<NotificationReply> notificationsReplies, int totalCount)> ApplyFiltersAsync(IQueryable<NotificationReply> query,
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