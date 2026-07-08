using NightOut.Models;

namespace NightOut.Services;

public interface IFriendInviteService
{
    Task<FriendInvite?> CreateInviteAsync();
    Task<bool> UseInviteAsync(string inviteCode);
    Task<List<FriendInvite>> GetMyPendingInvitesAsync();
    Task ShareInviteAsync(FriendInvite invite);
}
