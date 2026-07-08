#if IOS
using CoreLocation;
using Foundation;
using NightOut.Services;

namespace NightOut.Platforms.iOS;

public sealed class IosBeaconScanner : NSObject, IBeaconScanner
{
    private static readonly string[] RangedUuids =
    [
        "8F2A6B7C-9D31-4E42-AF8B-4D7C2E9F5A10",
        "FDA50693-A4E2-4FB1-AFCF-C6EB07647825"
    ];

    private readonly CLLocationManager _manager;
    private readonly BeaconLocationDelegate _delegate;
    private readonly object _sync = new();
    private readonly Dictionary<string, BeaconAdvertisement> _latest = [];
    private readonly List<CLBeaconRegion> _activeRegions = [];

    public IosBeaconScanner()
    {
        _manager = new CLLocationManager();
        _delegate = new BeaconLocationDelegate(this);
        _manager.Delegate = _delegate;
    }

    public bool IsSupported => CLLocationManager.IsRangingAvailable;

    public BeaconScannerStatus LastStatus { get; private set; } = BeaconScannerStatus.Unknown;

    public async Task<IReadOnlyList<BeaconAdvertisement>> ScanAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            LastStatus = BeaconScannerStatus.Unsupported;
            return [];
        }

        var authorization = CLLocationManager.Status;
        if (authorization is CLAuthorizationStatus.Denied or CLAuthorizationStatus.Restricted)
        {
            LastStatus = BeaconScannerStatus.MissingLocationPermission;
            return [];
        }

        if (authorization == CLAuthorizationStatus.NotDetermined)
        {
            LastStatus = BeaconScannerStatus.MissingLocationPermission;
            _manager.RequestWhenInUseAuthorization();
            await Task.Delay(600, cancellationToken);
        }

        lock (_sync)
        {
            _latest.Clear();
        }

        try
        {
            StartRanging();
            LastStatus = BeaconScannerStatus.Scanning;
            await Task.Delay(duration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastStatus = BeaconScannerStatus.ScanFailed;
            System.Diagnostics.Debug.WriteLine($"[IosBeaconScanner] Scan erreur : {ex.Message}");
        }
        finally
        {
            StopRanging();
        }

        lock (_sync)
        {
            var result = _latest.Values
                .OrderByDescending(item => item.Rssi)
                .ToList();

            LastStatus = result.Count > 0
                ? BeaconScannerStatus.Completed
                : BeaconScannerStatus.Completed;

            return result;
        }
    }

    private void StartRanging()
    {
        StopRanging();

        foreach (var uuid in RangedUuids)
        {
            if (!Guid.TryParse(uuid, out _))
                continue;

            var region = new CLBeaconRegion(new NSUuid(uuid), $"spotiz-{uuid}");
            _activeRegions.Add(region);
            _manager.StartRangingBeacons(region);
        }
    }

    private void StopRanging()
    {
        foreach (var region in _activeRegions)
        {
            try
            {
                _manager.StopRangingBeacons(region);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosBeaconScanner] Stop erreur : {ex.Message}");
            }
        }

        _activeRegions.Clear();
    }

    private void AddBeacons(CLBeacon[] beacons)
    {
        var now = DateTime.UtcNow;

        lock (_sync)
        {
            foreach (var beacon in beacons)
            {
                if (beacon.Rssi == 0)
                    continue;

                var uuid = beacon.ProximityUuid.AsString().ToUpperInvariant();
                var major = beacon.Major.Int32Value;
                var minor = beacon.Minor.Int32Value;
                var key = $"{uuid}:{major}:{minor}";

                _latest[key] = new BeaconAdvertisement(
                    uuid,
                    major,
                    minor,
                    (int)beacon.Rssi,
                    now);
            }
        }
    }

    private sealed class BeaconLocationDelegate(IosBeaconScanner owner) : CLLocationManagerDelegate
    {
        public override void RangedBeacons(
            CLLocationManager manager,
            CLBeacon[] beacons,
            CLBeaconRegion region)
        {
            owner.AddBeacons(beacons);
        }

        public override void RangingBeaconsDidFailForRegion(
            CLLocationManager manager,
            CLBeaconRegion region,
            NSError error)
        {
            owner.LastStatus = BeaconScannerStatus.ScanFailed;
            System.Diagnostics.Debug.WriteLine($"[IosBeaconScanner] Ranging failed : {error.LocalizedDescription}");
        }
    }
}
#endif
