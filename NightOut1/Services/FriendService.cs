using NightOut.Models;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class FriendService(Client supabase, IAuthService auth, INotificationService notifications, ICreditService credits) : IFriendService
{
    public async Task<List<Profile>> GetVisibleFriendsOnMapAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return [];

            var friends = await GetFriendsAsync();
            if (friends.Count == 0) return [];

            var limitUtc = DateTime.UtcNow.AddMinutes(-15);

            return friends
                .Where(f => f.Id != userId)
                .Where(f => f.ShareLocationWithFriends)
                .Where(f => !f.SecretMode)
                .Where(f => f.LastLatitude.HasValue && f.LastLongitude.HasValue)
                .Where(f => f.LastLocationUpdate.HasValue && f.LastLocationUpdate.Value.ToUniversalTime() >= limitUtc)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetVisibleFriendsOnMap error: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> UpdateMyMapLocationAsync(double latitude, double longitude)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return false;

            // Sécurité : si le profil est en mode fantôme, on ne republie jamais la position.
            var me = await supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Single();

            if (me == null || me.SecretMode || !me.ShareLocationWithFriends)
            {
                System.Diagnostics.Debug.WriteLine("[FriendService] Position non publiée : mode fantôme ou partage désactivé.");
                return false;
            }

            await supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.LastLatitude, latitude)
                .Set(p => p.LastLongitude, longitude)
                .Set(p => p.LastLocationUpdate, DateTime.UtcNow)
                .Update();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] UpdateMyMapLocation error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetMyMapVisibilityAsync(bool visible)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var update = supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.ShareLocationWithFriends, visible)
                .Set(p => p.SecretMode, !visible);

            if (!visible)
            {
                // Très important : on efface aussi la dernière position publiée.
                // Comme ça, les autres utilisateurs ne gardent pas l'ancien marqueur ami.
                update = update
                    .Set(p => p.LastLatitude, (double?)null)
                    .Set(p => p.LastLongitude, (double?)null)
                    .Set(p => p.LastLocationUpdate, (DateTime?)null);
            }

            await update.Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] SetMyMapVisibility error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Profile>> GetFriendsAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return [];

            var fs1 = await supabase.From<Friendship>()
                .Filter("status", Operator.Equals, "accepted")
                .Filter("requester_id", Operator.Equals, userId)
                .Get();

            var fs2 = await supabase.From<Friendship>()
                .Filter("status", Operator.Equals, "accepted")
                .Filter("addressee_id", Operator.Equals, userId)
                .Get();

            var all = new List<Friendship>();
            if (fs1?.Models != null) all.AddRange(fs1.Models);
            if (fs2?.Models != null) all.AddRange(fs2.Models);

            var friendIds = all.Select(f => f.GetFriendId(userId)).Distinct().ToList();
            if (friendIds.Count == 0) return [];

            var profiles = await supabase.From<Profile>()
                .Filter("id", Operator.In, friendIds)
                .Get();

            return profiles?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetFriends error: {ex.Message}");
            return [];
        }
    }

    public async Task<List<Friendship>> GetPendingRequestsAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return [];

            var result = await supabase.From<Friendship>()
                .Filter("addressee_id", Operator.Equals, userId)
                .Filter("status", Operator.Equals, "pending")
                .Get();

            if (result?.Models == null || result.Models.Count == 0) return [];

            var requests = result.Models
                .Where(f => !string.IsNullOrWhiteSpace(f.RequesterId))
                .GroupBy(f => f.RequesterId)
                .Select(g => g.OrderByDescending(f => f.UpdatedAt).First())
                .ToList();

            // Charger les profils des demandeurs
            var requesterIds = requests.Select(f => f.RequesterId).Distinct().ToList();
            if (requesterIds.Count == 0) return [];

            var profiles = await supabase.From<Profile>()
                .Filter("id", Operator.In, requesterIds)
                .Get();

            var profileMap = profiles?.Models?.ToDictionary(p => p.Id) ?? [];

            foreach (var f in requests)
            {
                if (profileMap.TryGetValue(f.RequesterId, out var profile))
                    f.Requester = profile;
            }

            return requests;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetPending error: {ex.Message}");
            return [];
        }
    }
    public async Task<List<Friendship>> GetSentRequestsAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return [];

            var result = await supabase.From<Friendship>()
                .Filter("requester_id", Operator.Equals, userId)
                .Filter("status", Operator.Equals, "pending")
                .Get();

            if (result?.Models == null || result.Models.Count == 0) return [];

            var requests = result.Models
                .Where(f => !string.IsNullOrWhiteSpace(f.AddresseeId))
                .GroupBy(f => f.AddresseeId)
                .Select(g => g.OrderByDescending(f => f.UpdatedAt).First())
                .ToList();

            // Charger les profils des destinataires
            var addresseeIds = requests.Select(f => f.AddresseeId).Distinct().ToList();
            if (addresseeIds.Count == 0) return [];

            var profiles = await supabase.From<Profile>()
                .Filter("id", Operator.In, addresseeIds)
                .Get();

            var profileMap = profiles?.Models?.ToDictionary(p => p.Id) ?? [];

            foreach (var f in requests)
            {
                if (profileMap.TryGetValue(f.AddresseeId, out var profile))
                    f.Addressee = profile;
            }

            return requests;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetSentRequests error: {ex.Message}");
            return [];
        }
    }

    public async Task<List<Friendship>> GetAcceptedFriendshipsAsync(string userId)
    {
        try
        {
            var all = await GetAllFriendshipsAsync(userId);
            return all.Where(f => string.Equals(f.Status, "accepted", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetAcceptedFriendships error: {ex.Message}");
            return [];
        }
    }

    public async Task<List<Friendship>> GetAllFriendshipsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId)) return [];

            var fs1 = await supabase.From<Friendship>()
                .Filter("requester_id", Operator.Equals, userId)
                .Get();

            var fs2 = await supabase.From<Friendship>()
                .Filter("addressee_id", Operator.Equals, userId)
                .Get();

            var all = new List<Friendship>();
            if (fs1?.Models != null) all.AddRange(fs1.Models);
            if (fs2?.Models != null) all.AddRange(fs2.Models);

            return all
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetAllFriendships error: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> SendFriendRequestAsync(string addresseeId)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return false;
            if (string.IsNullOrWhiteSpace(addresseeId)) return false;
            if (userId == addresseeId) return true;

            var existing = (await GetAllFriendshipsAsync(userId))
                .FirstOrDefault(f =>
                    (f.RequesterId == userId && f.AddresseeId == addresseeId) ||
                    (f.RequesterId == addresseeId && f.AddresseeId == userId));

            if (existing != null)
            {
                var status = existing.Status?.ToLowerInvariant() ?? string.Empty;

                // Déjà ami ou demande déjà en attente : ce n'est pas une erreur côté UI.
                if (status is "accepted" or "pending")
                    return true;

                // Ancienne demande refusée/annulée envoyée par moi : on la réactive.
                if (existing.RequesterId == userId && (status is "declined" or "cancelled" or "canceled"))
                {
                    await supabase.From<Friendship>()
                        .Filter("id", Operator.Equals, existing.Id)
                        .Set(f => f.Status, "pending")
                        .Set(f => f.UpdatedAt, DateTime.UtcNow)
                        .Update();
                    return true;
                }

                // Ancienne demande refusée dans l'autre sens : on supprime l'ancienne ligne
                // puis on recrée une vraie demande dans le bon sens.
                if (existing.RequesterId == addresseeId && (status is "declined" or "cancelled" or "canceled"))
                {
                    await supabase.From<Friendship>()
                        .Filter("id", Operator.Equals, existing.Id)
                        .Delete();
                }
            }

            var now = DateTime.UtcNow;
            var insert = await supabase.From<Friendship>().Insert(new Friendship
            {
                RequesterId = userId,
                AddresseeId = addresseeId,
                Status = "pending",
                CreatedAt = now,
                UpdatedAt = now
            });

            var friendshipId = insert?.Models?.FirstOrDefault()?.Id;
            await notifications.PushAsync(addresseeId, "friend_request", userId, friendshipId, "friendship");
            await credits.AddMyCreditsByRuleAsync("friend_request_sent", friendshipId, "friendship", 10);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] SendRequest error: {ex}");
            return false;
        }
    }

    public async Task<bool> AcceptFriendRequestAsync(string friendshipId)
    {
        try
        {
            await supabase.From<Friendship>()
                .Filter("id", Operator.Equals, friendshipId)
                .Set(f => f.Status, "accepted")
                .Set(f => f.UpdatedAt, DateTime.UtcNow)
                .Update();

            var me = auth.GetCurrentUserId();
            var all = !string.IsNullOrWhiteSpace(me) ? await GetAllFriendshipsAsync(me) : new List<Friendship>();
            var accepted = all.FirstOrDefault(f => f.Id == friendshipId);
            if (accepted != null && !string.IsNullOrWhiteSpace(me))
            {
                var otherUserId = accepted.GetFriendId(me);
                await notifications.PushAsync(otherUserId, "friend_accepted", me, friendshipId, "friendship");
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Accept error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeclineFriendRequestAsync(string friendshipId)
    {
        try
        {
            await supabase.From<Friendship>()
                .Filter("id", Operator.Equals, friendshipId)
                .Set(f => f.Status, "declined")
                .Set(f => f.UpdatedAt, DateTime.UtcNow)
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Decline error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveFriendAsync(string friendshipId)
    {
        try
        {
            await supabase.From<Friendship>()
                .Filter("id", Operator.Equals, friendshipId)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Remove error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> BlockUserAsync(string userId)
    {
        try
        {
            var currentId = auth.GetCurrentUserId();
            if (currentId == null) return false;

            await supabase.From<Block>().Insert(new Block
            {
                BlockerId = currentId,
                BlockedId = userId
            });

            await supabase.From<Friendship>()
                .Filter("requester_id", Operator.Equals, currentId)
                .Filter("addressee_id", Operator.Equals, userId)
                .Delete();

            await supabase.From<Friendship>()
                .Filter("requester_id", Operator.Equals, userId)
                .Filter("addressee_id", Operator.Equals, currentId)
                .Delete();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Block error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnblockUserAsync(string userId)
    {
        try
        {
            var currentId = auth.GetCurrentUserId();
            if (currentId == null) return false;
            await supabase.From<Block>()
                .Filter("blocker_id", Operator.Equals, currentId)
                .Filter("blocked_id", Operator.Equals, userId)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Unblock error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Profile>> GetBlockedUsersAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return [];

            var blocks = await supabase.From<Block>()
                .Filter("blocker_id", Operator.Equals, userId)
                .Get();

            if (blocks?.Models == null || blocks.Models.Count == 0) return [];

            var blockedIds = blocks.Models.Select(b => b.BlockedId).ToList();
            var profiles   = await supabase.From<Profile>()
                .Filter("id", Operator.In, blockedIds)
                .Get();

            return profiles?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] GetBlocked error: {ex.Message}");
            return [];
        }
    }

    public async Task<List<Profile>> SearchUsersAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return [];

            var result = await supabase.From<Profile>()
                .Filter("username", Operator.ILike, $"%{query}%")
                .Limit(20)
                .Get();

            return result?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendService] Search error: {ex.Message}");
            return [];
        }
    }
}
