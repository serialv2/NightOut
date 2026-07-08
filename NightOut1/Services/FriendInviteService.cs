using NightOut.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class FriendInviteService(Client supabase, IAuthService auth, IFriendService friends, ICreditService credits, INotificationService notifications) : IFriendInviteService
{
    public async Task<FriendInvite?> CreateInviteAsync()
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return null;
        try
        {
            var invite = new FriendInvite
            {
                InviterId = me,
                InviteCode = GenerateCode(),
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            var result = await supabase.From<FriendInvite>().Insert(invite);
            var created = result?.Models?.FirstOrDefault() ?? invite;
            await credits.AddMyCreditsByRuleAsync("friend_invite_created", created.Id, "friend_invite", 50);
            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendInviteService] CreateInvite error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<FriendInvite>> GetMyPendingInvitesAsync()
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return [];
        try
        {
            var result = await supabase.From<FriendInvite>()
                .Filter("inviter_id", Operator.Equals, me)
                .Filter("status", Operator.Equals, "pending")
                .Order(i => i.CreatedAt, Ordering.Descending)
                .Limit(20)
                .Get();
            return result?.Models ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> UseInviteAsync(string inviteCode)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(inviteCode)) return false;
        try
        {
            var result = await supabase.From<FriendInvite>()
                .Filter("invite_code", Operator.Equals, inviteCode.Trim())
                .Filter("status", Operator.Equals, "pending")
                .Limit(1)
                .Get();

            var invite = result?.Models?.FirstOrDefault();
            if (invite == null || invite.InviterId == me) return false;
            if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value.ToUniversalTime() < DateTime.UtcNow) return false;

            var requestOk = await friends.SendFriendRequestAsync(invite.InviterId);
            if (!requestOk) return false;

            await supabase.From<FriendInvite>()
                .Filter("id", Operator.Equals, invite.Id)
                .Set(i => i.Status, "used")
                .Set(i => i.UsedByUserId, me)
                .Set(i => i.UsedAt, DateTime.UtcNow)
                .Update();

            await credits.AddCreditsByRuleAsync(invite.InviterId, "friend_invite_used_inviter", invite.Id, "friend_invite", 500);
            await credits.AddMyCreditsByRuleAsync("friend_invite_used_new_user", invite.Id, "friend_invite", 100);
            await notifications.PushAsync(invite.InviterId, "friend_invite_used", me, invite.Id, "friend_invite");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendInviteService] UseInvite error: {ex.Message}");
            return false;
        }
    }

    public async Task ShareInviteAsync(FriendInvite invite)
    {
        var url = invite.ShareUrl;

        var text = $"Rejoins-moi sur NightOut 🍻\n\nAjoute-moi en ami ici :";

        await Share.RequestAsync(new ShareTextRequest
        {
            Title = "Inviter un ami sur NightOut",
            Text = text,
            Uri = url
        });
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
