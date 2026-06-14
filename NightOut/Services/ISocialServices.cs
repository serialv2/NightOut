using NightOut.Models;

namespace NightOut.Services;

// ══════════════════════════════════════════════════════════════
// IDirectMessageService
// ══════════════════════════════════════════════════════════════
public interface IDirectMessageService
{
    Task<List<ConversationSummary>> GetConversationsAsync();
    Task<List<DirectMessage>> GetMessagesAsync(string partnerId, int limit = 40, DateTime? before = null);
    Task<DirectMessage?> SendTextAsync(string receiverId, string content);
    Task<DirectMessage?> SendMediaAsync(string receiverId, string mediaUrl, string type);
    Task<DirectMessage?> SendLocationAsync(string receiverId, double lat, double lng, string barName);
    Task MarkConversationReadAsync(string partnerId);
    void SubscribeToMessages(string partnerId, Action<DirectMessage> onReceived);
    void UnsubscribeMessages();
}

// ══════════════════════════════════════════════════════════════
// ISquadService
// ══════════════════════════════════════════════════════════════
public interface ISquadService
{
    Task<List<SquadSummary>> GetMySquadsAsync();
    Task<Squad?> CreateSquadAsync(string name, string icon = "🎉");
    Task<bool> AddMemberAsync(string squadId, string userId);
    Task<bool> RemoveMemberAsync(string squadId, string userId);
    Task<bool> LeaveSquadAsync(string squadId);
    Task<List<SquadMember>> GetMembersAsync(string squadId);
    Task<List<SquadMessage>> GetMessagesAsync(string squadId, int limit = 40, DateTime? before = null);
    Task<SquadMessage?> SendTextAsync(string squadId, string content);
    Task<SquadMessage?> SendMediaAsync(string squadId, string mediaUrl, string type);
    Task<SquadPlan?> CreatePlanAsync(string squadId, string title, string[] barIds, DateTime? meetingTime);
    Task<SquadPlan?> GetActivePlanAsync(string squadId);
    void SubscribeToMessages(string squadId, Action<SquadMessage> onReceived);
    void UnsubscribeMessages();
}

// ══════════════════════════════════════════════════════════════
// INotificationService
// ══════════════════════════════════════════════════════════════
public interface INotificationService
{
    Task<List<NightOutNotification>> GetNotificationsAsync(int limit = 30);

    Task<int> GetUnreadCountAsync();

    Task<int> GetUnreadCountByTypeAsync(params string[] types);

    Task MarkAllReadAsync();

    Task PushAsync(
        string userId,
        string type,
        string? actorId = null,
        string? entityId = null,
        string? entityType = null,
        string? body = null);

    void SubscribeToNotifications(Action<NightOutNotification> onReceived);

    void Unsubscribe();
}
// ══════════════════════════════════════════════════════════════
// IUserStatusService
// ══════════════════════════════════════════════════════════════
public interface IUserStatusService
{
    Task SetStatusAsync(string status, string? barId = null);
    Task<UserStatus?> GetStatusAsync(string userId);
    Task<List<UserStatus>> GetFriendsStatusesAsync(IEnumerable<string> userIds);
    Task GoOnlineAsync();
    Task GoOutAsync(string barId);
    Task GoOfflineAsync();
    /// <summary>
    /// Prolonge expires_at de 5 minutes. Appelé par HeartbeatService toutes les 2 min.
    /// Ne fait rien si l'utilisateur n'est pas connecté ou si le statut est déjà offline.
    /// </summary>
    Task HeartbeatAsync();
}
