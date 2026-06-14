using NightOut.Models;

namespace NightOut.Services;

public static class NotificationEvents
{
    public static event Action<NightOutNotification>? NotificationReceived;
    public static event Action<int>? UnreadCountChanged;

    public static int UnreadCount { get; private set; }

    public static void RaiseNotificationReceived(NightOutNotification notification)
    {
        NotificationReceived?.Invoke(notification);
    }

    public static void SetUnreadCount(int count)
    {
        if (count < 0)
            count = 0;

        UnreadCount = count;
        UnreadCountChanged?.Invoke(UnreadCount);
    }
}
