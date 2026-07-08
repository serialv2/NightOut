namespace NightOut.Services;

public static class GroupUnreadEvents
{
    private const string ReadAllGroupsKey = "nightout_groups_last_read_all";

    public static event Action<int>? UnreadCountChanged;

    public static int UnreadCount { get; private set; }

    public static DateTime LastAllGroupsReadUtc
    {
        get
        {
            var raw = Microsoft.Maui.Storage.Preferences.Get(ReadAllGroupsKey, string.Empty);
            return long.TryParse(raw, out var ticks)
                ? new DateTime(ticks, DateTimeKind.Utc)
                : DateTime.MinValue;
        }
    }

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

    public static void MarkAllGroupsRead()
    {
        Microsoft.Maui.Storage.Preferences.Set(ReadAllGroupsKey, DateTime.UtcNow.Ticks.ToString());
        SetUnreadCount(0);
    }
}
