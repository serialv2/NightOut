namespace NightOut.Services;

public static class GroupUnreadEvents
{
    public static event Action<int>? UnreadCountChanged;

    public static int UnreadCount { get; private set; }

    public static void Increment(int delta = 1)
    {
        SetUnreadCount(UnreadCount + delta);
    }

    public static void SetUnreadCount(int count)
    {
        if (count < 0)
            count = 0;

        UnreadCount = count;
        UnreadCountChanged?.Invoke(UnreadCount);
    }
}
