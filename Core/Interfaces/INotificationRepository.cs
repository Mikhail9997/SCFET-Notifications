using Core.Models;

namespace Core.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(Guid userId);
    Task<IReadOnlyList<Notification>> GetNotificationsWithReceiversAsync(Guid notificationId);
    Task<PagedResult<Notification>> GetBySenderIdAsync(Guid senderId, NotificationFilterEntity filter);
    Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid userId, NotificationFilterEntity filter);
    Task<Notification?> GetByIdWithReceiversAsync(Guid id);
}