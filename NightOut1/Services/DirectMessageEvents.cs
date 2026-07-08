namespace NightOut.Services;

public static class DirectMessageEvents
{
    public static event Action<int>? UnreadCountChanged;
    public static event Action? ConversationsChanged;

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
        ConversationsChanged?.Invoke();
    }

    public static void RaiseConversationsChanged()
    {
        ConversationsChanged?.Invoke();
    }
}
