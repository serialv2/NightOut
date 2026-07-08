namespace NightOut.Services;

public static class NotificationNavigationService
{
    private static readonly object SyncRoot = new();

    public static string? PendingEntityType { get; private set; }
    public static string? PendingEntityId { get; private set; }
    public static string? PendingActorId { get; private set; }
    public static string? PendingTitle { get; private set; }
    public static string? PendingType { get; private set; }
    public static string? PendingNotificationId { get; private set; }

    public static bool HasPendingNavigation =>
        !string.IsNullOrWhiteSpace(PendingType) ||
        !string.IsNullOrWhiteSpace(PendingEntityType) ||
        !string.IsNullOrWhiteSpace(PendingEntityId) ||
        !string.IsNullOrWhiteSpace(PendingNotificationId);

    public static void Set(
        string? entityType,
        string? entityId,
        string? actorId,
        string? title = null,
        string? type = null,
        string? notificationId = null)
    {
        lock (SyncRoot)
        {
            PendingEntityType = Normalize(entityType);
            PendingEntityId = Normalize(entityId);
            PendingActorId = Normalize(actorId);
            PendingTitle = Normalize(title);
            PendingType = Normalize(type);
            PendingNotificationId = Normalize(notificationId);
        }
    }

    public static void SetFromDictionary(IDictionary<string, string> data)
    {
        if (data.Count == 0)
            return;

        var type = Get(data, "type", "notification_type", "gcm.notification.type");
        var entityType = Get(data, "entity_type", "entityType", "EntityType", "gcm.notification.entity_type");
        var entityId = Get(data, "entity_id", "entityId", "EntityId", "project_id", "projectId", "gcm.notification.entity_id");
        var actorId = Get(data, "actor_id", "actorId", "ActorId", "gcm.notification.actor_id");
        var title = Get(data, "title", "Title", "gcm.notification.title");
        var notificationId = Get(data, "notification_id", "notificationId", "NotificationId", "gcm.notification.notification_id");

        if (string.IsNullOrWhiteSpace(type) &&
            string.IsNullOrWhiteSpace(entityType) &&
            string.IsNullOrWhiteSpace(entityId) &&
            string.IsNullOrWhiteSpace(notificationId))
            return;

        Set(entityType, entityId, actorId, title, type, notificationId);
    }

    public static async Task ProcessPendingAsync()
    {
        string? type;
        string? entityType;
        string? entityId;
        string? actorId;
        string? title;
        string? notificationId;

        lock (SyncRoot)
        {
            type = PendingType;
            entityType = PendingEntityType;
            entityId = PendingEntityId;
            actorId = PendingActorId;
            title = PendingTitle;
            notificationId = PendingNotificationId;
            ClearNoLock();
        }

        if (string.IsNullOrWhiteSpace(type) &&
            string.IsNullOrWhiteSpace(entityType) &&
            string.IsNullOrWhiteSpace(entityId) &&
            string.IsNullOrWhiteSpace(notificationId))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigateAsync(type, entityType, entityId, actorId, title);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] Erreur navigation : {ex.Message}");
        }
    }

    public static void Clear()
    {
        lock (SyncRoot)
            ClearNoLock();
    }

    private static void ClearNoLock()
    {
        PendingEntityType = null;
        PendingEntityId = null;
        PendingActorId = null;
        PendingTitle = null;
        PendingType = null;
        PendingNotificationId = null;
    }

    private static async Task NavigateAsync(
        string? type,
        string? entityType,
        string? entityId,
        string? actorId,
        string? title)
    {
        type = Normalize(type);
        entityType = Normalize(entityType);
        entityId = Normalize(entityId);
        actorId = Normalize(actorId);
        title = Normalize(title);

        if (type is "private_message" or "direct_message")
        {
            if (!string.IsNullOrWhiteSpace(actorId))
            {
                await Shell.Current.GoToAsync("ConversationPage", true, new Dictionary<string, object>
                {
                    ["PartnerId"] = actorId,
                    ["PartnerName"] = string.IsNullOrWhiteSpace(title) ? "Conversation" : title,
                    ["PartnerAvatarUrl"] = string.Empty
                });
                return;
            }

            await Shell.Current.GoToAsync("//MessagesPage", true);
            return;
        }

        if ((type is "group_message" or "group_media" or "group_photo" or "group_video") &&
            !string.IsNullOrWhiteSpace(entityId))
        {
            await Shell.Current.GoToAsync($"GroupDetailPage?groupId={Uri.EscapeDataString(entityId)}", true);
            return;
        }

        if ((entityType is "friend_group" or "group") && !string.IsNullOrWhiteSpace(entityId))
        {
            await Shell.Current.GoToAsync($"GroupDetailPage?groupId={Uri.EscapeDataString(entityId)}", true);
            return;
        }

        if (type is "official_event_created" || entityType is "official_event")
        {
            if (!string.IsNullOrWhiteSpace(entityId))
                await Shell.Current.GoToAsync($"OfficialEventDetailPage?eventId={Uri.EscapeDataString(entityId)}", true);
            else
                await Shell.Current.GoToAsync("//EventsPage", true);

            return;
        }

        if (type is "group_event" or "group_event_response" or "group_outings_today" ||
            entityType is "group_outing" or "group_outings_today")
        {
            await Shell.Current.GoToAsync("NotificationsPage", true);
            return;
        }

        if (type is "ephemeral_event_friend" or "ephemeral_event_group" or "ephemeral_event_cancelled" ||
            entityType is "ephemeral_event")
        {
            await Shell.Current.GoToAsync("EphemeralEventsPage", true);
            return;
        }

        if (type is "friend_request" or "friend_accepted")
        {
            await Shell.Current.GoToAsync("//FriendsPage", true);
            return;
        }

        await Shell.Current.GoToAsync("NotificationsPage", true);
    }

    private static string? Get(IDictionary<string, string> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
