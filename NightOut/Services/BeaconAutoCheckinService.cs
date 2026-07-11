
using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.ApplicationModel;
using NightOut.Models;

namespace NightOut.Services;

public sealed class BeaconAutoCheckinService : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScanDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ObservationTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SameBeaconAttemptCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DiagnosticToastCooldown = TimeSpan.FromSeconds(45);

    private const int DefaultMinRssi = -78;

    private readonly IBeaconScanner _scanner;
    private readonly ICheckinService _checkins;
    private readonly IAuthService _auth;
    private readonly IUserStatusService _userStatus;
    private readonly Dictionary<BeaconKey, BeaconObservation> _observations = [];
    private readonly Dictionary<BeaconKey, DateTime> _lastCheckinAttemptUtc = [];

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _scanInProgress;
    private DateTime _lastDiagnosticToastUtc = DateTime.MinValue;

    public BeaconAutoCheckinService(
        IBeaconScanner scanner,
        ICheckinService checkins,
        IAuthService auth,
        IUserStatusService userStatus)
    {
        _scanner = scanner;
        _checkins = checkins;
        _auth = auth;
        _userStatus = userStatus;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        if (!_scanner.IsSupported)
        {
            _ = ShowDiagnosticToastAsync("Beacon: telephone non compatible.", DateTime.UtcNow, force: true);
            return;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(ScanInterval);

        _ = ShowDiagnosticToastAsync("Scan beacon actif.", DateTime.UtcNow, force: true);
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _timer?.Dispose();
        _timer = null;
        _cts = null;
        _observations.Clear();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await ScanOnceAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested && _timer is not null)
        {
            try
            {
                if (!await _timer.WaitForNextTickAsync(cancellationToken))
                    break;

                await ScanOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BeaconAutoCheckin] Loop erreur : {ex.Message}");
            }
        }
    }

    private async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        if (_scanInProgress || _auth.GetCurrentUserId() is null)
            return;

        _scanInProgress = true;

        try
        {
            var now = DateTime.UtcNow;
            var advertisements = await _scanner.ScanAsync(ScanDuration, cancellationToken);

            if (advertisements.Count == 0)
            {
                await ShowDiagnosticToastAsync(
                    GetScannerStatusMessage(_scanner.LastStatus),
                    now);

                if (IsTerminalScannerStatus(_scanner.LastStatus))
                {
                    System.Diagnostics.Debug.WriteLine($"[BeaconAutoCheckin] Scan arrete : {_scanner.LastStatus}");
                    Stop();
                    return;
                }
            }

            foreach (var advertisement in advertisements)
            {
                if (advertisement.Rssi < DefaultMinRssi)
                {
                    await ShowDiagnosticToastAsync(
                        $"Beacon trop faible ({advertisement.Rssi} dBm).",
                        now);
                    continue;
                }

                var key = BeaconKey.From(advertisement);
                if (!_observations.TryGetValue(key, out var observation))
                {
                    observation = new BeaconObservation
                    {
                        Key = key,
                        FirstSeenUtc = now,
                        LastSeenUtc = now,
                        BestRssi = advertisement.Rssi,
                        SeenCount = 0
                    };
                    _observations[key] = observation;
                }

                observation.LastSeenUtc = now;
                observation.BestRssi = Math.Max(observation.BestRssi, advertisement.Rssi);
                observation.SeenCount++;

                await ShowDiagnosticToastAsync(
                    $"Beacon detecte: {advertisement.Uuid} / {advertisement.Major} / {advertisement.Minor} ({advertisement.Rssi} dBm).",
                    now);
            }

            PruneOldObservations(now);
            await TryAutoCheckinAsync(now, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BeaconAutoCheckin] Scan erreur : {ex.Message}");
        }
        finally
        {
            _scanInProgress = false;
        }
    }

    private void PruneOldObservations(DateTime now)
    {
        var expired = _observations
            .Where(pair => now - pair.Value.LastSeenUtc > ObservationTtl)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expired)
            _observations.Remove(key);
    }

    private async Task TryAutoCheckinAsync(DateTime now, CancellationToken cancellationToken)
    {
        var candidate = _observations.Values
            .Where(o => o.SeenCount >= 1)
            .OrderByDescending(o => o.BestRssi)
            .FirstOrDefault();

        if (candidate is null)
            return;

        var activeCheckin = await _checkins.GetActiveCheckinAsync();
        if (activeCheckin is not null)
        {
            return;
        }

        if (_lastCheckinAttemptUtc.TryGetValue(candidate.Key, out var lastAttempt) &&
            now - lastAttempt < SameBeaconAttemptCooldown)
        {
            return;
        }

        _lastCheckinAttemptUtc[candidate.Key] = now;

        cancellationToken.ThrowIfCancellationRequested();

        await ShowDiagnosticToastAsync("Tentative check-in beacon...", now, force: true);

        var checkin = await _checkins.CheckInByBeaconAsync(
            candidate.Key.Uuid,
            candidate.Key.Major,
            candidate.Key.Minor,
            candidate.BestRssi);

        if (checkin is null || string.IsNullOrWhiteSpace(checkin.BarId))
        {
            var reason = _checkins.LastBeaconCheckinError;
            var message = string.IsNullOrWhiteSpace(reason)
                ? "Tag vu, mais check-in serveur refuse."
                : $"Check-in refuse: {reason}";

            await ShowDiagnosticToastAsync(message, now, force: true);
            return;
        }

        _observations.Remove(candidate.Key);

        await _userStatus.GoOutAsync(checkin.BarId);
        await ShowAutoCheckinToastAsync(checkin);
    }

    private static async Task ShowAutoCheckinToastAsync(Checkin checkin)
    {
        try
        {
            var toast = Toast.Make("Check-in automatique Spotiz valide.");
            await toast.Show();
        }
        catch
        {
        }
    }

    private async Task ShowDiagnosticToastAsync(string message, DateTime now, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!force && now - _lastDiagnosticToastUtc < DiagnosticToastCooldown)
            return;

        if (!force)
            _lastDiagnosticToastUtc = now;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var toast = Toast.Make(message);
                await toast.Show();
            });
        }
        catch
        {
        }
    }

    private static string GetScannerStatusMessage(BeaconScannerStatus status)
        => status switch
        {
            BeaconScannerStatus.MissingActivity => "Beacon: app pas encore prete.",
            BeaconScannerStatus.MissingBluetoothPermission => "Beacon: autorise Appareils a proximite.",
            BeaconScannerStatus.MissingLocationPermission => "Beacon: autorise la localisation.",
            BeaconScannerStatus.BluetoothOff => "Beacon: Bluetooth desactive.",
            BeaconScannerStatus.ScannerUnavailable => "Beacon: scanner Bluetooth indisponible.",
            BeaconScannerStatus.ScanFailed => "Beacon: scan Bluetooth en erreur.",
            BeaconScannerStatus.Unsupported => "Beacon: telephone non compatible.",
            BeaconScannerStatus.BleSeenNoIBeacon => "BLE vu, mais aucune trame iBeacon Spotiz lisible.",
            BeaconScannerStatus.AppleBeaconSeenButInvalid => "Beacon Apple vu, mais format iBeacon invalide.",
            BeaconScannerStatus.Completed => "Beacon: aucun tag vu.",
            _ => "Beacon: scan en cours."
        };

    private static bool IsTerminalScannerStatus(BeaconScannerStatus status)
        => status is BeaconScannerStatus.Unsupported
            or BeaconScannerStatus.ScannerUnavailable;

    public void Dispose()
    {
        Stop();
    }
}
