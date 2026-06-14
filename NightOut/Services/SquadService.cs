using Newtonsoft.Json;
using NightOut.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using static Supabase.Postgrest.Constants;
using SupabaseClient = Supabase.Client;

namespace NightOut.Services;

public class SquadService(SupabaseClient supabase, IAuthService auth) : ISquadService
{
    private RealtimeChannel? _channel;

    // ── Liste squads ─────────────────────────────────────────
    public async Task<List<SquadSummary>> GetMySquadsAsync()
    {
        try
        {
            var resp = await supabase.Rpc("get_user_squads", new { });
            if (string.IsNullOrEmpty(resp?.Content)) return [];
            return JsonConvert.DeserializeObject<List<SquadSummary>>(resp.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] GetMySquads erreur : {ex}");
            return [];
        }
    }

    // ── Création ─────────────────────────────────────────────
    public async Task<Squad?> CreateSquadAsync(string name, string icon = "🎉")
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;
        try
        {
            // 1) Créer le squad
            var created = await supabase.From<Squad>()
                .Insert(new Squad { Name = name, Icon = icon, CreatedBy = me });
            var squad = created?.Models?.FirstOrDefault();
            if (squad == null) return null;

            // 2) S'ajouter comme admin
            await supabase.From<SquadMember>()
                .Insert(new SquadMember { SquadId = squad.Id, UserId = me, Role = "admin" });

            return squad;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] Create erreur : {ex}");
            return null;
        }
    }

    // ── Membres ──────────────────────────────────────────────
    public async Task<bool> AddMemberAsync(string squadId, string userId)
    {
        try
        {
            await supabase.From<SquadMember>()
                .Insert(new SquadMember { SquadId = squadId, UserId = userId, Role = "member" });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] AddMember erreur : {ex}");
            return false;
        }
    }

    public async Task<bool> RemoveMemberAsync(string squadId, string userId)
    {
        try
        {
            await supabase.From<SquadMember>()
                .Where(m => m.SquadId == squadId && m.UserId == userId)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] RemoveMember erreur : {ex}");
            return false;
        }
    }

    public async Task<bool> LeaveSquadAsync(string squadId)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return false;
        return await RemoveMemberAsync(squadId, me);
    }

    public async Task<List<SquadMember>> GetMembersAsync(string squadId)
    {
        try
        {
            var result = await supabase.From<SquadMember>()
                .Where(m => m.SquadId == squadId)
                .Get();
            return result?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] GetMembers erreur : {ex}");
            return [];
        }
    }

    // ── Messages ─────────────────────────────────────────────
    public async Task<List<SquadMessage>> GetMessagesAsync(
        string squadId, int limit = 40, DateTime? before = null)
    {
        try
        {
            var resp = await supabase.Rpc("get_squad_messages", new
            {
                p_squad_id = squadId,
                p_limit    = limit,
                p_before   = (before ?? DateTime.UtcNow).ToString("O")
            });
            if (string.IsNullOrEmpty(resp?.Content)) return [];
            var msgs = JsonConvert.DeserializeObject<List<SquadMessage>>(resp.Content) ?? [];
            var me   = auth.GetCurrentUserId();
            foreach (var m in msgs) m.IsMine = m.SenderId == me;
            msgs.Reverse();
            return msgs;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] GetMessages erreur : {ex}");
            return [];
        }
    }

    public async Task<SquadMessage?> SendTextAsync(string squadId, string content)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;
        try
        {
            var result = await supabase.From<SquadMessage>()
                .Insert(new SquadMessage
                {
                    SquadId  = squadId,
                    SenderId = me,
                    Content  = content,
                    Type     = "text"
                });
            var msg = result?.Models?.FirstOrDefault();
            if (msg != null) msg.IsMine = true;
            return msg;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] SendText erreur : {ex}");
            return null;
        }
    }

    public async Task<SquadMessage?> SendMediaAsync(
        string squadId, string mediaUrl, string type)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;
        try
        {
            var result = await supabase.From<SquadMessage>()
                .Insert(new SquadMessage
                {
                    SquadId  = squadId,
                    SenderId = me,
                    MediaUrl = mediaUrl,
                    Type     = type
                });
            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] SendMedia erreur : {ex}");
            return null;
        }
    }

    // ── Plans ────────────────────────────────────────────────
    public async Task<SquadPlan?> CreatePlanAsync(
        string squadId, string title, string[] barIds, DateTime? meetingTime)
    {
        var me = auth.GetCurrentUserId();
        if (me == null) return null;
        try
        {
            var result = await supabase.From<SquadPlan>()
                .Insert(new SquadPlan
                {
                    SquadId     = squadId,
                    CreatedBy   = me,
                    Title       = title,
                    BarIds      = barIds,
                    MeetingTime = meetingTime
                });
            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] CreatePlan erreur : {ex}");
            return null;
        }
    }

    public async Task<SquadPlan?> GetActivePlanAsync(string squadId)
    {
        try
        {
            var result = await supabase.From<SquadPlan>()
                .Where(p => p.SquadId == squadId && p.Status == "active")
                .Order(p => p.CreatedAt, Ordering.Descending)
                .Limit(1)
                .Get();
            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] GetActivePlan erreur : {ex}");
            return null;
        }
    }

    // ── Realtime ─────────────────────────────────────────────
    public async void SubscribeToMessages(string squadId, Action<SquadMessage> onReceived)
    {
        UnsubscribeMessages();
        try
        {
            _channel = supabase.Realtime.Channel($"squad:{squadId}");
            _channel.Register(new PostgresChangesOptions("public", "squad_messages",
                filter: $"squad_id=eq.{squadId}"));
            _channel.AddPostgresChangeHandler(ListenType.Inserts, (_, change) =>
            {
                try
                {
                    var msg = change.Model<SquadMessage>();
                    if (msg == null) return;
                    msg.IsMine = msg.SenderId == auth.GetCurrentUserId();
                    MainThread.BeginInvokeOnMainThread(() => onReceived(msg));
                }
                catch { }
            });
            await _channel.Subscribe();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Squad] Subscribe erreur : {ex}");
        }
    }

    public void UnsubscribeMessages()
    {
        try { _channel?.Unsubscribe(); } catch { }
        _channel = null;
    }
}
