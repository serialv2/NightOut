namespace NightOut.Services;

/// <summary>
/// Envoie un heartbeat Supabase toutes les 2 minutes pour maintenir la presence.
/// Si l'app crashe ou est kill, le cron Supabase nettoie les presences expirees.
/// </summary>
public class HeartbeatService : IDisposable
{
    private readonly IUserStatusService _userStatus;
    private readonly IAuthService _auth;
    private System.Timers.Timer? _timer;
    private bool _disposed;

    public HeartbeatService(IUserStatusService userStatus, IAuthService auth)
    {
        _userStatus = userStatus;
        _auth = auth;
    }

    public void Start()
    {
        if (_disposed)
            return;

        if (_timer != null)
        {
            _timer.Start();
            System.Diagnostics.Debug.WriteLine("[Heartbeat] Redemarre (intervalle 2 min)");
            return;
        }

        _timer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds);
        _timer.Elapsed += async (_, _) => await TickAsync();
        _timer.AutoReset = true;
        _timer.Start();
        System.Diagnostics.Debug.WriteLine("[Heartbeat] Demarre (intervalle 2 min)");
    }

    public void Stop()
    {
        _timer?.Stop();
        System.Diagnostics.Debug.WriteLine("[Heartbeat] Arrete");
    }

    private async Task TickAsync()
    {
        if (_auth.GetCurrentUserId() == null)
            return;

        try
        {
            await _userStatus.HeartbeatAsync();
            System.Diagnostics.Debug.WriteLine("[Heartbeat] Ping envoye");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Heartbeat] Ping echoue : {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
