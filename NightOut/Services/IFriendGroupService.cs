using NightOut.Models;

namespace NightOut.Services;

public interface IFriendGroupService
{
    Task<List<FriendGroup>> GetMyGroupsAsync();
    Task<FriendGroup?> CreateGroupAsync(string name, string emoji = "🍻");
    Task<bool> UpdateGroupPhotoAsync(string groupId, string photoUrl);

    Task<bool> AddMemberAsync(string groupId, string userId);
    Task<bool> RemoveMemberAsync(string groupId, string userId);
    Task<bool> DeleteGroupAsync(string groupId);
    Task<List<FriendGroupMember>> GetMembersAsync(string groupId);

    Task<List<FriendGroupMessage>> GetMessagesAsync(string groupId, int limit = 50);
    Task<FriendGroupMessage?> SendTextMessageAsync(string groupId, string text);
    Task<FriendGroupMessage?> SendPhotoMessageAsync(string groupId, bool fromCamera);
    Task<FriendGroupMessage?> SendVideoMessageAsync(string groupId, bool fromCamera);

    Task<List<FriendGroupOuting>> GetOutingsAsync(string groupId, int limit = 20);
    Task<FriendGroupOuting?> CreateOutingAsync(string groupId, string barId, string title, string? message, DateTime plannedAt);
    Task<bool> RespondToOutingAsync(string outingId, string status);
}
