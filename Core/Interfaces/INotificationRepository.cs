using Core.Models;

namespace Core.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(Guid userId);
    Task<IReadOnlyList<Notification>> GetNotificationsWithReceiversAsync(Guid notificationId);
    Task<PagedResult<Notification>> GetBySenderIdAsync(Guid senderId, FilterEntity filter);
    Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid userId, FilterEntity filter);
    Task<Notification?> GetByIdWithReceiversAsync(Guid id);
}