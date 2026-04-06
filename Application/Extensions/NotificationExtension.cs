using Core.Models;

namespace Application.Extensions;

public static class NotificationExtension
{
    public static bool IsFavorite(this Notification notification, Guid userId)
    {
        return notification.FavoriteByUsers.Any(f => f.UserId == userId);
    }
}