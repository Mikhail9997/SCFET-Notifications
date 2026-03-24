using Core.Models;

namespace Core.Interfaces;

public interface INotificationReplyRepository:IRepository<NotificationReply>
{
    Task<PagedResult<NotificationReply>> GetNotificationsReplyByNotificationId(Guid notificationId, NotificationFilterEntity filter);
}