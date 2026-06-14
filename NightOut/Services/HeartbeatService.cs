using NightOut.Services;

namespace NightOut.Services;

/// <summary>
/// Envoie un heartbeat Supabase toutes les 2 minutes pour maintenir la présence.
/// Si l'app crashe ou est killée, le heartbeat s'arrête → le cron Supabase
/// nettoie automatiquement les présences expirées (expires_at dépassé).
/// </summary>
public class HeartbeatService : IDisposable
{
    private readonly IUserStatusService _userStatus;
    private readonly IAuthService       _auth;
    private System.Timers.Timer?        _timer;
    private bool                        _disposed;

    public HeartbeatService(IUserStatusService userStatus, IAuthService auth)
    {
        _userStatus = userStatus;
        _auth       = auth;
    }

    /// <summary>Démarre le timer heartbeat. Appeler depuis App() après init.</summary>
    public void Start()
    {
        if (_disposed) return;
        _timer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds);
        _timer.Elapsed  += async (_, _) => await TickAsync();
        _timer.AutoReset = true;
        _timer.Start();
        System.Diagnostics.Debug.WriteLine("[Heartbeat] ✅ Démarré (intervalle 2 min)");
    }

    /// <summary>Arrête le timer (ex: au logout).</summary>
    public void Stop()
    {
        _timer?.Stop();
        System.Diagnostics.Debug.WriteLine("[Heartbeat] ⏹ Arrêté");
    }

    private async Task TickAsync()
    {
        if (_auth.GetCurrentUserId() == null) return;
        try
        {
            await _userStatus.HeartbeatAsync();
            System.Diagnostics.Debug.WriteLine("[Heartbeat] 💓 Ping envoyé");
        }
        catch (Exception ex)
        {
            // Silencieux — un heartbeat raté n'est pas critique
            System.Diagnostics.Debug.WriteLine($"[Heartbeat] ⚠ Ping échoué : {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
