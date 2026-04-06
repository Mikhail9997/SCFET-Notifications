using Core.Models;

namespace Application.Utils;

public static class NotificationUtils
{
    public static bool IsPersonal(HashSet<Guid> receivers, Guid currentReceiver)
    {
        return receivers.Count <= 2 && receivers.Contains(currentReceiver);
    }

    public static bool IsFavorite(HashSet<Guid> favorites, Guid userId)
    {
        return favorites.Contains(userId);
    }
}