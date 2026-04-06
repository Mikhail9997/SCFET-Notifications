using Core.Models;

namespace Core.Interfaces;

public interface IUserFavoriteNotificationRepository:IRepository<UserFavoriteNotification>
{
    Task<IReadOnlyCollection<UserFavoriteNotification>> GetAllByUserIdAsync(Guid userId);
    Task<IReadOnlyCollection<UserFavoriteNotification>> GetAllByNotificationIdAsync(Guid notificationId);
    Task<PagedResult<UserFavoriteNotification>> GetMyAsync(Guid userId, FilterEntity filter);
    Task<UserFavoriteNotification?> GetAsync(Guid userId, Guid notificationId);
}