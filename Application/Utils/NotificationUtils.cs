using Core.Models;

namespace Application.Utils;

public static class NotificationUtils
{
    public static bool IsPersonal(HashSet<Guid> receivers, Guid currentReceiver)
    {
        return receivers.Count <= 2 && receivers.Contains(currentReceiver);
    }
}