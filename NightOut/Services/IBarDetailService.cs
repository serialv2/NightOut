using NightOut.Models;

namespace NightOut.Services;

public interface IBarDetailService
{
    Task<(long PresentCount, long MediaCount)> GetBarStatsAsync(string barId);
    Task<List<BarActivityItem>> GetActivityFeedAsync(string barId, int limit = 30);
    Task<bool> ToggleLikeAsync(string photoId);
    Task<List<NightOut.Models.Profile>> GetFriendsAtBarAsync(string barId);
    Task<bool> PostMessageAsync(string barId, string content);
    Task<List<NightOut.Models.BarVisitHistory>> GetVisitHistoryAsync(int limit = 50);
    /// <summary>
    /// S'abonne aux changements de présence (user_statuses) du bar.
    /// Le callback est déclenché dès qu'un utilisateur change de statut
    /// (activation/désactivation du mode fantôme, check-in, départ).
    /// </summary>
    void SubscribeToPresence(string barId, Action onChanged);
    void UnsubscribePresence();
}
