using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NightOut.Models;
using Supabase;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

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

            var items = JsonConvert.DeserializeObject<List<BarActivityItem>>(resp.Content) ?? [];
            var currentUserId = auth.GetCurrentUserId();
            foreach (var item in items)
                item.IsMine = item.UserId == currentUserId;
            return items;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailService] GetActivityFeed erreur : {ex}");
            return [];
        }
    }

    // ── Amis présents ────────────────────────────────────────────
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


    // ── Realtime présence (mode fantôme + check-ins) ─────────
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
