namespace NightOut.Services;

public class LocationService : ILocationService
{
    // Intervalle de rafraîchissement quand le tracking est actif (carte ouverte).
    // 8 s = bon compromis réactivité / batterie en foreground.
    private static readonly TimeSpan TrackingInterval = TimeSpan.FromSeconds(8);

    // Une ancienne position connue peut être fausse (ex : dernier test à Lille).
    // Au démarrage on accepte seulement un cache récent et pas trop imprécis.
    private static readonly TimeSpan MaxLastKnownAge = TimeSpan.FromMinutes(30);
    private const double MaxLastKnownAccuracyMeters = 1500;

    private CancellationTokenSource? _cts;
    private Action<double, double>?  _onUpdated;

    public bool IsTracking => _cts != null && !_cts.IsCancellationRequested;

    public async Task<(double Lat, double Lng)?> GetCurrentLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted) return null;

            // 1) Cache OS seulement s'il est récent. Sinon on l'ignore pour éviter
            // d'afficher une ancienne ville au lancement.
            var last = await Geolocation.GetLastKnownLocationAsync();
            if (IsUsableLocation(last, requireRecentCache: true))
                return (last!.Latitude, last.Longitude);

            // 2) Fix rapide : précision moyenne.
            var medium = await TryGetLocationAsync(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(7));
            if (IsUsableLocation(medium))
                return (medium!.Latitude, medium.Longitude);

            // 3) Fix précis : plus lent, mais meilleur pour le vrai démarrage sur l'utilisateur.
            var high = await TryGetLocationAsync(GeolocationAccuracy.High, TimeSpan.FromSeconds(18));
            if (IsUsableLocation(high))
                return (high!.Latitude, high.Longitude);

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task StartTrackingAsync(Action<double, double> onLocationUpdated)
    {
        if (IsTracking) return;

        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted) return;

        _onUpdated = onLocationUpdated;
        _cts       = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            // Seed instantané depuis le cache OS, mais uniquement si fiable/récent.
            try
            {
                var last = await Geolocation.GetLastKnownLocationAsync();
                if (IsUsableLocation(last, requireRecentCache: true))
                    _onUpdated?.Invoke(last!.Latitude, last.Longitude);
            }
            catch { }

            // Premier fix rapide, puis fix précis.
            try
            {
                var quick = await TryGetLocationAsync(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(7), _cts.Token);
                if (IsUsableLocation(quick))
                    _onUpdated?.Invoke(quick!.Latitude, quick.Longitude);
            }
            catch { }

            try
            {
                var precise = await TryGetLocationAsync(GeolocationAccuracy.High, TimeSpan.FromSeconds(18), _cts.Token);
                if (IsUsableLocation(precise))
                    _onUpdated?.Invoke(precise!.Latitude, precise.Longitude);
            }
            catch { }

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var location = await TryGetLocationAsync(GeolocationAccuracy.High, TimeSpan.FromSeconds(15), _cts.Token);
                    if (IsUsableLocation(location))
                        _onUpdated?.Invoke(location!.Latitude, location.Longitude);
                }
                catch (OperationCanceledException) { break; }
                catch { }

                try
                {
                    await Task.Delay(TrackingInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, _cts.Token);
    }

    public void StopTracking()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private static async Task<Location?> TryGetLocationAsync(
        GeolocationAccuracy accuracy,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await Geolocation.GetLocationAsync(new GeolocationRequest
        {
            DesiredAccuracy = accuracy,
            Timeout         = timeout
        }, cancellationToken);
    }

    private static bool IsUsableLocation(Location? location, bool requireRecentCache = false)
    {
        if (location == null) return false;
        if (double.IsNaN(location.Latitude) || double.IsNaN(location.Longitude)) return false;
        if (double.IsInfinity(location.Latitude) || double.IsInfinity(location.Longitude)) return false;
        if (Math.Abs(location.Latitude) < 0.0001 && Math.Abs(location.Longitude) < 0.0001) return false;
        if (location.Latitude < -90 || location.Latitude > 90) return false;
        if (location.Longitude < -180 || location.Longitude > 180) return false;

        if (requireRecentCache)
        {
            var age = DateTimeOffset.UtcNow - location.Timestamp.ToUniversalTime();
            if (age > MaxLastKnownAge) return false;

            if (location.Accuracy.HasValue && location.Accuracy.Value > MaxLastKnownAccuracyMeters)
                return false;
        }

        return true;
    }
}
