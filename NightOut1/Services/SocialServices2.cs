using Newtonsoft.Json;
using NightOut.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using static Supabase.Postgrest.Constants;
using SupabaseClient = Supabase.Client;

namespace NightOut.Services;

// ══════════════════════════════════════════════════════════════
// NotificationService
// ══════════════════════════════════════════════════════════════
public class NotificationService(SupabaseClient supabase, IAuthService auth) : INotificationService
{
    private RealtimeChannel? _channel;

    public async Task<List<NightOutNotification>> GetNotificationsAsync(int limit = 30)
    {
        try
        {
            var resp = await supabase.Rpc("get_notifications", new { p_limit = limit });
            if (!string.IsNullOrWhiteSpace(resp?.Content))
                return JsonConvert.DeserializeObject<List<NightOutNotification>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] RPC GetNotifications erreur : {ex.Message}");
        }

        // Secours si le script SQL n'a pas encore été exécuté.
        try
        {
            var me = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(me)) return [];
            var result = await supabase.From<NightOutNotification>()
                .Filter("user_id", Operator.Equals, me)
                .Order(n => n.CreatedAt, Ordering.Descending)
                .Limit(limit)
                .Get();
            return result?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] fallback GetNotifications erreur : {ex.Message}");
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var resp = await supabase.Rpc("get_unread_notifications_count", new { });
            if (!string.IsNullOrWhiteSpace(resp?.Content) && int.TryParse(resp.Content.Trim(), out var count))
                return count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] RPC unread erreur : {ex.Message}");
        }

        var notifs = await GetNotificationsAsync(500);
        return notifs.Count(n => !n.IsRead);
    }

    public async Task<int> GetUnreadCountByTypeAsync(params string[] types)
    {
        try
        {
            if (types == null || types.Length == 0)
                return 0;

            var wantedTypes = types
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (wantedTypes.Count == 0)
                return 0;

            // On lit les notifications en base, puis on filtre côté app.
            // C'est volontairement simple et fiable avec ta structure actuelle.
            var notifs = await GetNotificationsAsync(500);

            return notifs.Count(n =>
                !n.IsRead &&
                !string.IsNullOrWhiteSpace(n.Type) &&
                wantedTypes.Contains(n.Type));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] GetUnreadCountByType erreur : {ex.Message}");
            return 0;
        }
    }


    public async Task MarkReadByTypeAsync(params string[] types)
    {
        try
        {
            var me = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(me) || types == null || types.Length == 0)
                return;

            var wantedTypes = types
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (wantedTypes.Count == 0)
                return;

            // Méthode volontairement simple et compatible avec ta structure actuelle :
            // on récupère les notifs non lues, puis on marque seulement les types demandés.
            var notifs = await GetNotificationsAsync(500);
            var idsToMark = notifs
                .Where(n => !n.IsRead
                            && !string.IsNullOrWhiteSpace(n.Id)
                            && !string.IsNullOrWhiteSpace(n.Type)
                            && wantedTypes.Contains(n.Type))
                .Select(n => n.Id)
                .Distinct()
                .ToList();

            foreach (var id in idsToMark)
            {
                try
                {
                    await supabase.From<NightOutNotification>()
                        .Filter("id", Operator.Equals, id)
                        .Filter("user_id", Operator.Equals, me)
                        .Set(n => n.IsReadRaw, true)
                        .Set(n => n.ReadAt, DateTime.UtcNow)
                        .Update();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Notif] MarkReadByType item erreur : {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] MarkReadByType erreur : {ex.Message}");
        }
    }


    public async Task MarkAllReadAsync()
    {
        try
        {
            await supabase.Rpc("mark_notifications_read", new { });
            return;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] RPC MarkAllRead erreur : {ex.Message}");
        }

        try
        {
            var me = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(me)) return;
            await supabase.From<NightOutNotification>()
                .Filter("user_id", Operator.Equals, me)
                .Filter("is_read", Operator.Equals, false)
                .Set(n => n.IsReadRaw, true)
                .Set(n => n.ReadAt, DateTime.UtcNow)
                .Update();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] fallback MarkAllRead erreur : {ex.Message}");
        }
    }

    public async Task PushAsync(
    string userId,
    string type,
    string? actorId = null,
    string? entityId = null,
    string? entityType = null,
    string? body = null)
    {
        try
        {
            await supabase.Rpc("push_notification", new
            {
                p_user_id = userId,
                p_type = type,
                p_actor_id = actorId,
                p_entity_id = entityId,
                p_entity_type = entityType,
                p_body = body
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] Push erreur : {ex.Message}");
        }
    }

    public async void SubscribeToNotifications(Action<NightOutNotification> onReceived)
    {
        Unsubscribe();
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return;

        try
        {
            _channel = supabase.Realtime.Channel($"notifications:{me}");
            _channel.Register(new PostgresChangesOptions("public", "notifications", filter: $"user_id=eq.{me}"));
            _channel.AddPostgresChangeHandler(ListenType.Inserts, (_, _) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var latest = (await GetNotificationsAsync(1)).FirstOrDefault();
                        if (latest != null) onReceived(latest);
                    }
                    catch { }
                });
            });
            await _channel.Subscribe();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] Subscribe erreur : {ex.Message}");
        }
    }

    public void Unsubscribe()
    {
        try { _channel?.Unsubscribe(); } catch { }
        _channel = null;
    }
}

// ══════════════════════════════════════════════════════════════
// UserStatusService
// ══════════════════════════════════════════════════════════════
public class UserStatusService(SupabaseClient supabase, IAuthService auth) : IUserStatusService
{
    public async Task SetStatusAsync(string status, string? barId = null)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return;
        try
        {
            await supabase.From<UserStatus>()
                .Upsert(new UserStatus
                {
                    UserId    = me,
                    Status    = status,
                    BarId     = barId,
                    UpdatedAt = DateTime.UtcNow,
                    // NightOut : on garde la présence pendant 1 heure quand le téléphone se met en veille.
                    // Le vrai retrait reste immédiat uniquement si l'utilisateur passe volontairement offline.
                    ExpiresAt = status == "offline"
                        ? DateTime.UtcNow
                        : DateTime.UtcNow.AddHours(1)
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Status] SetStatus erreur : {ex}");
        }
    }

    public async Task GoOnlineAsync()           => await SetStatusAsync("online");
    public async Task GoOutAsync(string barId)  => await SetStatusAsync("out", barId);
    public async Task GoOfflineAsync()          => await SetStatusAsync("offline");

    /// <summary>
    /// Prolonge expires_at de 1 heure (RPC heartbeat_presence).
    /// Silencieux en cas d'erreur — le cron Supabase prend le relai si le heartbeat s'arrête.
    /// </summary>
    public async Task HeartbeatAsync()
    {
        if (auth.GetCurrentUserId() == null) return;
        try
        {
            await supabase.Rpc("heartbeat_presence", null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Status] Heartbeat erreur : {ex.Message}");
        }
    }

    public async Task<UserStatus?> GetStatusAsync(string userId)
    {
        try
        {
            return await supabase.From<UserStatus>()
                .Where(s => s.UserId == userId)
                .Single();
        }
        catch { return null; }
    }

    public async Task<List<UserStatus>> GetFriendsStatusesAsync(IEnumerable<string> userIds)
    {
        try
        {
            var ids = userIds.ToList();
            if (ids.Count == 0) return [];

            var result = await supabase.From<UserStatus>()
                .Filter("user_id", Operator.In, ids)
                .Get();
            return result?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Status] GetFriendsStatuses erreur : {ex}");
            return [];
        }
    }
}
