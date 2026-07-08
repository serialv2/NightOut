using NightOut.Models;

namespace NightOut.Services;

public interface IFriendService
{
    Task<List<Profile>> GetFriendsAsync();
    Task<List<Profile>> GetVisibleFriendsOnMapAsync();
    Task<bool> UpdateMyMapLocationAsync(double latitude, double longitude);
    Task<bool> SetMyMapVisibilityAsync(bool visible);
    Task<List<Friendship>> GetPendingRequestsAsync();
    Task<List<Friendship>> GetSentRequestsAsync();
    Task<bool> SendFriendRequestAsync(string addresseeId);
    Task<bool> AcceptFriendRequestAsync(string friendshipId);
    Task<bool> DeclineFriendRequestAsync(string friendshipId);
    Task<bool> RemoveFriendAsync(string friendshipId);
    Task<bool> BlockUserAsync(string userId);
    Task<bool> UnblockUserAsync(string userId);
    Task<List<Profile>> GetBlockedUsersAsync();
    Task<List<Profile>> SearchUsersAsync(string query);
    Task<List<Friendship>> GetAcceptedFriendshipsAsync(string userId);
    Task<List<Friendship>> GetAllFriendshipsAsync(string userId);
}
