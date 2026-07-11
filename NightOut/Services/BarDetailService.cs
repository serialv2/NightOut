using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NightOut.Models;
using Supabase;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class BarDetailService(Supabase.Client supabase, IAuthService auth) : IBarDetailService
{
    private RealtimeChannel? _presenceChannel;
    // ── Stats ────────────────────────────────────────────────────
    public async Task<(long PresentCount, long MediaCount)> GetBarStatsAsync(string barId)
    {
        try
        {
            var resp = await supabase.Rpc("get_bar_stats", new { p_bar_id = barId });
            if (string.IsNullOrEmpty(resp?.Content)) return (0, 0);

            var arr = JArray.Parse(resp.Content);
            if (arr.Count == 0) return (0, 0);
            var row = arr[0];
            return (row["present_count"]?.Value<long>() ?? 0,
                    row["media_count"]?.Value<long>()   ?? 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetBarStats erreur : {ex}");
            return (0, 0);
        }
    }

    // ── Fil d'activité ───────────────────────────────────────────
    public async Task<List<BarActivityItem>> GetActivityFeedAsync(string barId, int limit = 30)
    {
        try
        {
            var resp = await supabase.Rpc("get_bar_activity_feed",
                new { p_bar_id = barId, p_limit = limit });

            if (string.IsNullOrEmpty(resp?.Content)) return [];

            var items = (JsonConvert.DeserializeObject<List<BarActivityItem>>(resp.Content) ?? [])
                .Where(item => !IsPresenceActivity(item))
                .ToList();
            var currentUserId = auth.GetCurrentUserId();

            var commentCounts = await GetActivityCommentCountsAsync(items.Select(i => i.Id));

            foreach (var item in items)
            {
                item.IsMine = item.UserId == currentUserId;
                if (commentCounts.TryGetValue(item.Id, out var count))
                    item.CommentCount = count;
            }

            return items;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetActivityFeed erreur : {ex}");
            return [];
        }
    }

    // ── Amis présents ────────────────────────────────────────────
    private static bool IsPresenceActivity(BarActivityItem item)
    {
        var type = item.Type?.Trim();
        return !string.IsNullOrWhiteSpace(type) &&
               (type.Contains("checkin", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("check-in", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("checkout", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("check-out", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Profile>> GetFriendsAtBarAsync(string barId)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return [];

            // Récupère les profils qui ont un check-in actif sur ce bar
            // ET qui sont amis avec l'utilisateur courant.
            var resp = await supabase.Rpc("get_friends_at_bar",
                new { p_bar_id = barId, p_user_id = userId });

            if (string.IsNullOrEmpty(resp?.Content)) return [];
            return JsonConvert.DeserializeObject<List<Profile>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetFriendsAtBar erreur : {ex}");
            return [];
        }
    }

    // ── Membres présents (tous, hors ghost mode) ─────────────────
    public async Task<List<Profile>> GetPresentUsersAtBarAsync(string barId)
    {
        try
        {
            // Profils ayant un check-in actif sur ce bar, hors mode fantôme.
            var resp = await supabase.Rpc("get_present_users_at_bar",
                new { p_bar_id = barId });

            if (string.IsNullOrEmpty(resp?.Content)) return [];
            return JsonConvert.DeserializeObject<List<Profile>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetPresentUsersAtBar erreur : {ex}");
            return [];
        }
    }

    // ── Like ─────────────────────────────────────────────────────
    public async Task<bool> ToggleLikeAsync(string photoId)
    {
        try
        {
            var resp = await supabase.Rpc("toggle_bar_media_like", new { p_photo_id = photoId });
            if (string.IsNullOrEmpty(resp?.Content)) return false;
            // Le RPC retourne true (liké) ou false (déliké)
            return bool.TryParse(resp.Content.Trim(), out var result) && result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] ToggleLike erreur : {ex}");
            return false;
        }
    }
    // ── Commentaires fil bar ─────────────────────────────────────
    public async Task<Dictionary<string, int>> GetActivityCommentCountsAsync(IEnumerable<string> activityIds)
    {
        var ids = activityIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return [];

        try
        {
            var resp = await supabase.Rpc("get_bar_activity_comment_counts", new { p_activity_ids = ids });
            if (string.IsNullOrWhiteSpace(resp?.Content))
                return [];

            var rows = JArray.Parse(resp.Content);
            var result = new Dictionary<string, int>();

            foreach (var row in rows)
            {
                var id = row["activity_id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                result[id] = row["comment_count"]?.Value<int>() ?? 0;
            }

            return result;
        }
        catch (Exception ex)
        {
            // Tant que la migration SQL commentaires n'est pas passée, le feed reste fonctionnel.
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetActivityCommentCounts erreur : {ex.Message}");
            return [];
        }
    }

    public async Task<List<BarActivityComment>> GetActivityCommentsAsync(string activityId, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(activityId))
            return [];

        try
        {
            var resp = await supabase.Rpc("get_bar_activity_comments",
                new { p_activity_id = activityId, p_limit = limit });

            if (string.IsNullOrWhiteSpace(resp?.Content))
                return [];

            return JsonConvert.DeserializeObject<List<BarActivityComment>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetActivityComments erreur : {ex.Message}");
            return [];
        }
    }

    public async Task<BarActivityComment?> PostActivityCommentAsync(string activityId, string activityType, string content)
    {
        if (string.IsNullOrWhiteSpace(activityId) || string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            var resp = await supabase.Rpc("post_bar_activity_comment",
                new
                {
                    p_activity_id = activityId,
                    p_activity_type = string.IsNullOrWhiteSpace(activityType) ? "unknown" : activityType,
                    p_content = content.Trim()
                });

            if (string.IsNullOrWhiteSpace(resp?.Content))
                return null;

            var list = JsonConvert.DeserializeObject<List<BarActivityComment>>(resp.Content);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] PostActivityComment erreur : {ex.Message}");
            return null;
        }
    }

    // ── Message texte ────────────────────────────────────────
    public async Task<bool> PostMessageAsync(string barId, string content)
    {
        try
        {
            var resp = await supabase.Rpc("post_bar_message",
                new { p_bar_id = barId, p_content = content });
            return resp != null && !string.IsNullOrEmpty(resp.Content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] PostMessage erreur : {ex}");
            return false;
        }
    }

    // ── Historique des visites ────────────────────────────────
    public async Task<List<BarVisitHistory>> GetVisitHistoryAsync(int limit = 50)
    {
        try
        {
            var resp = await supabase.Rpc("get_user_bar_history", new { p_limit = limit });
            if (string.IsNullOrEmpty(resp?.Content)) return [];
            return JsonConvert.DeserializeObject<List<BarVisitHistory>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetVisitHistory erreur : {ex}");
            return [];
        }
    }



    // ── Horaires d'ouverture ────────────────────────────────────
    public async Task<List<BarOpeningHour>> GetOpeningHoursAsync(string barId)
    {
        try
        {
            var result = await supabase.From<BarOpeningHour>()
                .Where(x => x.BarId == barId)
                .Order(x => x.DayOfWeek, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            return result.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetOpeningHours erreur : {ex}");
            return [];
        }
    }

    // ── Realtime présence (mode fantôme + check-ins) ─────────
    public async Task TrackBarProfileViewAsync(string barId)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(barId))
                return;

            await supabase.From<BarProfileView>().Insert(new BarProfileView
            {
                BarId = barId,
                ViewerId = userId,
                ViewedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] TrackBarProfileView erreur : {ex.Message}");
        }
    }

    public async Task<BarProfileViewStats> GetBarProfileViewStatsAsync(string? barId = null)
    {
        try
        {
            var resp = await supabase.Rpc("get_bar_profile_view_stats", new { p_bar_id = barId });
            if (string.IsNullOrEmpty(resp?.Content))
                return new BarProfileViewStats(0, 0, 0, 0);

            var arr = JArray.Parse(resp.Content);
            if (arr.Count == 0)
                return new BarProfileViewStats(0, 0, 0, 0);

            var row = arr[0];
            return new BarProfileViewStats(
                row["view_total"]?.Value<int>() ?? 0,
                row["view_female"]?.Value<int>() ?? 0,
                row["view_male"]?.Value<int>() ?? 0,
                row["view_unknown"]?.Value<int>() ?? 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetBarProfileViewStats erreur : {ex.Message}");
            return new BarProfileViewStats(0, 0, 0, 0);
        }
    }

    public async Task<BarPresenceStats> GetBarPresenceStatsAsync(string barId, DateTime? from = null, DateTime? to = null)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return new BarPresenceStats();

        try
        {
            var resp = await supabase.Rpc("get_bar_presence_stats", new
            {
                p_bar_id = barId,
                p_from = (from ?? DateTime.UtcNow.AddDays(-30)).ToUniversalTime(),
                p_to = (to ?? DateTime.UtcNow).ToUniversalTime()
            });

            if (string.IsNullOrWhiteSpace(resp?.Content))
                return new BarPresenceStats();

            return JsonConvert.DeserializeObject<BarPresenceStats>(resp.Content) ?? new BarPresenceStats();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetBarPresenceStats erreur : {ex.Message}");
            return new BarPresenceStats();
        }
    }

    public void SubscribeToPresence(string barId, Action onChanged)
    {
        UnsubscribePresence();
        try
        {
            _presenceChannel = supabase.Realtime.Channel($"presence:{barId}");
            // user_statuses : changement de statut (ghost mode ON/OFF, check-in, départ)
            _presenceChannel.Register(new PostgresChangesOptions(
                "public", "user_statuses"));
            _presenceChannel.AddPostgresChangeHandler(ListenType.All, (_, _) =>
                MainThread.BeginInvokeOnMainThread(onChanged));
            // checkins : quelqu'un arrive ou part
            _presenceChannel.Register(new PostgresChangesOptions(
                "public", "checkins",
                filter: $"bar_id=eq.{barId}"));
            _presenceChannel.AddPostgresChangeHandler(ListenType.All, (_, _) =>
                MainThread.BeginInvokeOnMainThread(onChanged));
            _ = _presenceChannel.Subscribe();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BarDetailService] SubscribeToPresence erreur : {ex}");
        }
    }

    public void UnsubscribePresence()
    {
        try { _presenceChannel?.Unsubscribe(); } catch { }
        _presenceChannel = null;
    }


}
