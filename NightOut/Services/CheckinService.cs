using Newtonsoft.Json;
using NightOut.Models;
using Supabase;

namespace NightOut.Services;

public class CheckinService(Client supabase, IAuthService auth, ICreditService credits) : ICheckinService
{
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
                await credits.AddMyCreditsAsync(50, "checkin", checkin.Id, "checkin");
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

            var result = await supabase.From<Checkin>()
                .Where(c => c.UserId == userId && c.IsActive)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch { return null; }
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
