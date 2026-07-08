namespace NightOut.Services;

public static class EventInteractionEvents
{
    public static event Action<int>? UnreadCountChanged;

    public static int UnreadCount { get; private set; }

    public static void SetUnreadCount(int count)
    {
        if (count < 0)
            count = 0;

        UnreadCount = count;
        UnreadCountChanged?.Invoke(UnreadCount);
    }

    public static void IncrementUnread(int amount = 1)
    {
        if (amount <= 0)
            amount = 1;

        SetUnreadCount(UnreadCount + amount);
    }

    public static void Clear()
    {
        SetUnreadCount(0);
    }
}
