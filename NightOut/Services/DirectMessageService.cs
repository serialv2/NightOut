using Newtonsoft.Json;
using NightOut.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using static Supabase.Postgrest.Constants;
using SupabaseClient = Supabase.Client;

namespace NightOut.Services;

public class DirectMessageService(SupabaseClient supabase, IAuthService auth, INotificationService notifications) : IDirectMessageService
{
    private RealtimeChannel? _channel;

    public async Task<List<ConversationSummary>> GetConversationsAsync()
    {
        try
        {
            var resp = await supabase.Rpc("get_conversations", new { });
            if (string.IsNullOrEmpty(resp?.Content)) return [];
            return JsonConvert.DeserializeObject<List<ConversationSummary>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] GetConversations erreur : {ex}");
            return [];
        }
    }

    public async Task<List<DirectMessage>> GetMessagesAsync(string partnerId, int limit = 40, DateTime? before = null)
    {
        try
        {
            var resp = await supabase.Rpc("get_direct_messages", new
            {
                p_partner_id = partnerId,
                p_limit = limit,
                p_before = (before ?? DateTime.UtcNow).ToString("O")
            });

            if (string.IsNullOrEmpty(resp?.Content)) return [];

            var msgs = JsonConvert.DeserializeObject<List<DirectMessage>>(resp.Content) ?? [];
            var me = auth.GetCurrentUserId();

            foreach (var m in msgs)
                m.IsMine = m.SenderId == me;

            msgs.Reverse();
            return msgs;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] GetMessages erreur : {ex}");
            return [];
        }
    }

    public async Task<DirectMessage?> SendTextAsync(string receiverId, string content)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;

        try
        {
            var now = DateTime.UtcNow;

            var result = await supabase.From<DirectMessage>()
                .Insert(new DirectMessage
                {
                    SenderId = me,
                    ReceiverId = receiverId,
                    Content = content,
                    Type = "text",
                    CreatedAt = now
                });

            var msg = result?.Models?.FirstOrDefault();

            if (msg != null)
            {
                msg.IsMine = true;

                if (msg.CreatedAt == default)
                    msg.CreatedAt = DateTime.UtcNow;

                await notifications.PushAsync(
                    receiverId,
                    "private_message",
                    me,
                    msg.Id,
                    "direct_message",
                    content);
            }

            return msg;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] SendText erreur : {ex}");
            return null;
        }
    }

    public async Task<DirectMessage?> SendMediaAsync(string receiverId, string mediaUrl, string type)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;

        try
        {
            var now = DateTime.UtcNow;

            var result = await supabase.From<DirectMessage>()
                .Insert(new DirectMessage
                {
                    SenderId = me,
                    ReceiverId = receiverId,
                    MediaUrl = mediaUrl,
                    Type = type,
                    CreatedAt = now
                });

            var msg = result?.Models?.FirstOrDefault();

            if (msg != null)
            {
                msg.IsMine = true;

                if (msg.CreatedAt == default)
                    msg.CreatedAt = DateTime.UtcNow;

                await notifications.PushAsync(receiverId, "private_message", me, msg.Id, "direct_message");
            }

            return msg;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] SendMedia erreur : {ex}");
            return null;
        }
    }

    public async Task<DirectMessage?> SendLocationAsync(string receiverId, double lat, double lng, string barName)
    {
        var content = $"{lat:F6},{lng:F6}|{barName}";
        var me = auth.GetCurrentUserId();

        if (me == null) return null;

        try
        {
            var now = DateTime.UtcNow;

            var result = await supabase.From<DirectMessage>()
                .Insert(new DirectMessage
                {
                    SenderId = me,
                    ReceiverId = receiverId,
                    Content = content,
                    Type = "location",
                    CreatedAt = now
                });

            var msg = result?.Models?.FirstOrDefault();

            if (msg != null)
            {
                msg.IsMine = true;

                if (msg.CreatedAt == default)
                    msg.CreatedAt = DateTime.UtcNow;

                await notifications.PushAsync(receiverId, "private_message", me, msg.Id, "direct_message");
            }

            return msg;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] SendLocation erreur : {ex}");
            return null;
        }
    }

    public async Task MarkConversationReadAsync(string partnerId)
    {
        try
        {
            await supabase.Rpc("mark_conversation_read", new { p_partner_id = partnerId });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] MarkConversationRead RPC erreur : {ex.Message}");
        }

        try
        {
            var latestNotifications = await notifications.GetNotificationsAsync(150);

            var unreadDirectNotifications = latestNotifications
                .Where(n => !n.IsRead)
                .Where(n => n.Type is "private_message" or "direct_message")
                .Where(n => !string.IsNullOrWhiteSpace(partnerId) && n.ActorId == partnerId)
                .ToList();

            foreach (var notification in unreadDirectNotifications)
            {
                if (string.IsNullOrWhiteSpace(notification.Id))
                    continue;

                await supabase.From<NightOutNotification>()
                    .Filter("id", Operator.Equals, notification.Id)
                    .Set(n => n.IsReadRaw, true)
                    .Set(n => n.ReadAt, DateTime.UtcNow)
                    .Update();
            }

            var remainingUnread = latestNotifications.Count(n =>
                !n.IsRead &&
                n.Type is "private_message" or "direct_message" &&
                !unreadDirectNotifications.Any(r => r.Id == n.Id));

            DirectMessageEvents.SetUnreadCount(remainingUnread);
            DirectMessageEvents.RaiseConversationsChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] Mark notifications read erreur : {ex.Message}");
        }
    }

    public async void SubscribeToMessages(string partnerId, Action<DirectMessage> onReceived)
    {
        UnsubscribeMessages();

        var me = auth.GetCurrentUserId();
        if (me == null) return;

        try
        {
            _channel = supabase.Realtime.Channel($"dm:{me}:{partnerId}");
            _channel.Register(new PostgresChangesOptions("public", "direct_messages"));

            _channel.AddPostgresChangeHandler(ListenType.Inserts, (_, change) =>
            {
                try
                {
                    var msg = change.Model<DirectMessage>();
                    if (msg == null) return;

                    var relevant =
                        (msg.SenderId == partnerId && msg.ReceiverId == me) ||
                        (msg.SenderId == me && msg.ReceiverId == partnerId);

                    if (!relevant) return;

                    msg.IsMine = msg.SenderId == me;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        onReceived(msg);
                        DirectMessageEvents.RaiseConversationsChanged();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DM] Realtime handler erreur : {ex.Message}");
                }
            });

            await _channel.Subscribe();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DM] Subscribe erreur : {ex}");
        }
    }

    public void UnsubscribeMessages()
    {
        try { _channel?.Unsubscribe(); } catch { }
        _channel = null;
    }
}