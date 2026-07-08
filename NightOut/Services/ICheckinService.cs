using NightOut.Models;

namespace NightOut.Services;

public interface ICheckinService
{
    string? LastCheckinError { get; }
    string? LastBeaconCheckinError { get; }
    event Action<Checkin?>? ActiveCheckinChanged;

    /// <summary>
    /// Check-in validé côté serveur via la RPC check_in.
    /// Lance InvalidOperationException("trop_loin") si l'utilisateur est hors rayon du bar.
    /// Retourne null en cas d'erreur réseau ou autre.
    /// </summary>
    Task<Checkin?> CheckInAsync(string barId, double lat, double lng, string? eventId = null);

    Task<Checkin?> CheckInByBeaconAsync(string uuid, int major, int minor, int rssi);

    Task<bool>          CheckOutAsync(string checkinId);

    /// <summary>
    /// Checkout du checkin actif sans connaître son ID.
    /// Appelé automatiquement depuis App.OnSleep (fermeture/mise en arrière-plan).
    /// </summary>
    Task<bool>          CheckOutActiveAsync();

    Task<Checkin?>      GetActiveCheckinAsync();
    Task<List<Checkin>> GetFriendsCheckinsAtBarAsync(string barId);
}
