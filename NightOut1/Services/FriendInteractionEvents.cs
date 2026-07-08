namespace NightOut.Services;

public static class FriendInteractionEvents
{
    public static event Action<int>? PendingCountChanged;

    public static int PendingCount { get; private set; }

    public static void SetPendingCount(int count)
    {
        if (count < 0)
            count = 0;

        PendingCount = count;
        PendingCountChanged?.Invoke(PendingCount);
    }

    public static void IncrementPendingCount()
    {
        SetPendingCount(PendingCount + 1);
    }

    public static void RefreshLater()
    {
        PendingCountChanged?.Invoke(PendingCount);
    }
}
