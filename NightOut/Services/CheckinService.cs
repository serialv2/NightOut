using Newtonsoft.Json;
using NightOut.Models;
using Supabase;

namespace NightOut.Services;

public class CheckinService(Client supabase, IAuthService auth, ICreditService credits) : ICheckinService
{
    public string? LastBeaconCheckinError { get; private set; }
    public event Action<Checkin?>? ActiveCheckinChanged;

    /// <summary>
    /// Appelle la RPC check_in (atomique + validation de distance côté serveur).
    /// Lance InvalidOperationException("trop_loin") si l'utilisateur est hors rayon.
    /// Retourne null pour toute autre erreur (réseau, etc.).
    /// </summary>
    public async Task<Checkin?> CheckInAsync(string barId, double lat, double lng, string? eventId = null)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return null;

            var activeCheckins = await GetActiveCheckinsForUserAsync(userId);
            var activeAtSameBar = activeCheckins
                .OrderByDescending(c => c.CheckedInAt)
                .FirstOrDefault(c => c.BarId == barId);

            if (activeAtSameBar != null)
            {
                await CloseDuplicateActiveCheckinsAsync(userId, activeAtSameBar.Id);
                return activeAtSameBar;
            }

            await CloseDuplicateActiveCheckinsAsync(userId, keepCheckinId: null);

            var response = await supabase.Rpc("check_in", new
            {
                p_bar_id   = barId,
                p_user_id  = userId,
                p_lat      = lat,
                p_lng      = lng,
                p_event_id = eventId
            });

            if (string.IsNullOrEmpty(response?.Content))
                return null;

            var checkin = JsonConvert.DeserializeObject<Checkin>(response.Content);
            if (checkin != null)
            {
                await CloseDuplicateActiveCheckinsAsync(userId, checkin.Id);
                await credits.AddMyCreditsByRuleAsync("checkin", checkin.Id, "checkin", 50);
            }
            return checkin;
        }
        catch (Exception ex) when (ex.ToString().Contains("trop_loin"))
        {
            throw new InvalidOperationException("trop_loin", ex);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Checkin?> CheckInByBeaconAsync(string uuid, int major, int minor, int rssi)
    {
        try
        {
            LastBeaconCheckinError = null;

            var userId = auth.GetCurrentUserId();
            if (userId == null)
            {
                LastBeaconCheckinError = "utilisateur non connecte";
                return null;
            }

            var response = await supabase.Rpc("check_in_by_beacon", new
            {
                p_user_id = userId,
                p_uuid = uuid,
                p_major = major,
                p_minor = minor,
                p_rssi = rssi
            });

            if (string.IsNullOrEmpty(response?.Content))
            {
                LastBeaconCheckinError = "reponse serveur vide";
                return null;
            }

            var checkin = JsonConvert.DeserializeObject<Checkin>(response.Content);
            if (checkin is null)
            {
                LastBeaconCheckinError = "reponse serveur illisible";
                return null;
            }

            await CloseDuplicateActiveCheckinsAsync(userId, checkin.Id);

            try
            {
                await credits.AddMyCreditsByRuleAsync("checkin", checkin.Id, "checkin", 50);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Checkin] Credits beacon erreur : {ex.Message}");
            }

            NotifyActiveCheckinChanged(checkin);
            return checkin;
        }
        catch (Exception ex)
        {
            LastBeaconCheckinError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[Checkin] CheckInByBeacon erreur : {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CheckOutAsync(string checkinId)
    {
        try
        {
            await supabase.From<Checkin>()
                .Where(c => c.Id == checkinId)
                .Set(c => c.IsActive,     false)
                .Set(c => c.CheckedOutAt, DateTime.UtcNow)
                .Update();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Checkout du checkin actif de l'utilisateur courant sans connaître son ID.
    /// Appelé depuis App.OnSleep pour garantir le retrait de présence à la fermeture.
    /// </summary>
    public async Task<bool> CheckOutActiveAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return false;

            // Guard : vérifie que la session Supabase est active avant d'écrire
            if (supabase.Auth?.CurrentSession == null) return false;

            await supabase.From<Checkin>()
                .Where(c => c.UserId == userId && c.IsActive)
                .Set(c => c.IsActive, false)
                .Set(c => c.CheckedOutAt, DateTime.UtcNow)
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Checkin] CheckOutActive erreur : {ex.Message}");
            return false;
        }
    }
    public async Task<Checkin?> GetActiveCheckinAsync()
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (userId == null) return null;

            var active = await GetActiveCheckinsForUserAsync(userId);
            var latest = active.OrderByDescending(c => c.CheckedInAt).FirstOrDefault();

            if (latest != null)
                await CloseDuplicateActiveCheckinsAsync(userId, latest.Id);

            return latest;
        }
        catch { return null; }
    }

    private async Task<List<Checkin>> GetActiveCheckinsForUserAsync(string userId)
    {
        var result = await supabase.From<Checkin>()
            .Where(c => c.UserId == userId && c.IsActive)
            .Get();

        return result?.Models ?? [];
    }

    private void NotifyActiveCheckinChanged(Checkin? checkin)
    {
        try
        {
            ActiveCheckinChanged?.Invoke(checkin);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Checkin] Notification check-in actif erreur : {ex.Message}");
        }
    }

    private async Task CloseDuplicateActiveCheckinsAsync(string userId, string? keepCheckinId)
    {
        try
        {
            var active = await GetActiveCheckinsForUserAsync(userId);
            var toClose = active
                .Where(c => string.IsNullOrWhiteSpace(keepCheckinId) || c.Id != keepCheckinId)
                .ToList();

            foreach (var checkin in toClose)
            {
                await supabase.From<Checkin>()
                    .Where(c => c.Id == checkin.Id)
                    .Set(c => c.IsActive, false)
                    .Set(c => c.CheckedOutAt, DateTime.UtcNow)
                    .Update();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Checkin] Nettoyage check-ins actifs erreur : {ex.Message}");
        }
    }

    public async Task<List<Checkin>> GetFriendsCheckinsAtBarAsync(string barId)
    {
        try
        {
            var result = await supabase.From<Checkin>()
                .Where(c => c.BarId == barId && c.IsActive)
                .Get();

            return result?.Models ?? [];
        }
        catch { return []; }
    }
}
