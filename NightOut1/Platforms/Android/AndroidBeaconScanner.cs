#if ANDROID
using Android;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Microsoft.Maui.ApplicationModel;
using NightOut.Services;

namespace NightOut.Platforms.Android;

public sealed class AndroidBeaconScanner : IBeaconScanner
{
    private const int AppleManufacturerId = 0x004C;
    private const string LaMaisonBeaconAddress = "DC:0D:30:80:C3:05";
    private const string LaMaisonBeaconUuid = "8F2A6B7C-9D31-4E42-AF8B-4D7C2E9F5A10";
    private const int LaMaisonBeaconMajor = 59001;
    private const int LaMaisonBeaconMinor = 1;

    public bool IsSupported => OperatingSystem.IsAndroidVersionAtLeast(23);
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

        if (!HasScanPermission())
            return [];

        var adapter = BluetoothAdapter.DefaultAdapter;
        var scanner = adapter?.BluetoothLeScanner;

        if (adapter?.IsEnabled != true)
        {
            LastStatus = BeaconScannerStatus.BluetoothOff;
            return [];
        }

        if (scanner is null)
        {
            LastStatus = BeaconScannerStatus.ScannerUnavailable;
            return [];
        }

        var callback = new IBeaconScanCallback();
        var settings = new ScanSettings.Builder()
            .SetScanMode(global::Android.Bluetooth.LE.ScanMode.LowLatency)
            .Build();

        try
        {
            LastStatus = BeaconScannerStatus.Scanning;
            scanner.StartScan(null, settings, callback);
            await Task.Delay(duration, cancellationToken);
            LastStatus = BeaconScannerStatus.Completed;
        }
        catch (System.OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastStatus = BeaconScannerStatus.ScanFailed;
            System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] StartScan erreur : {ex.Message}");
        }
        finally
        {
            try
            {
                scanner.StopScan(callback);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] StopScan erreur : {ex.Message}");
            }
        }

        return callback.Advertisements;
    }

    private bool HasScanPermission()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            LastStatus = BeaconScannerStatus.MissingActivity;
            return false;
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            var hasBluetoothScan =
                activity.CheckSelfPermission(Manifest.Permission.BluetoothScan) == Permission.Granted;
            var hasBluetoothConnect =
                activity.CheckSelfPermission(Manifest.Permission.BluetoothConnect) == Permission.Granted;
            var hasLocation =
                activity.CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted;

            if (hasBluetoothScan && hasBluetoothConnect && hasLocation)
                return true;

            activity.RequestPermissions(
                [
                    Manifest.Permission.BluetoothScan,
                    Manifest.Permission.BluetoothConnect,
                    Manifest.Permission.AccessFineLocation
                ],
                5901);

            LastStatus = !hasBluetoothScan || !hasBluetoothConnect
                ? BeaconScannerStatus.MissingBluetoothPermission
                : BeaconScannerStatus.MissingLocationPermission;
            return false;
        }

        if (activity.CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted)
            return true;

        activity.RequestPermissions([Manifest.Permission.AccessFineLocation], 5902);
        LastStatus = BeaconScannerStatus.MissingLocationPermission;
        return false;
    }

    private sealed class IBeaconScanCallback : ScanCallback
    {
        private readonly Dictionary<string, BeaconAdvertisement> _latest = [];

        public IReadOnlyList<BeaconAdvertisement> Advertisements
            => _latest.Values.ToList();

        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult? result)
        {
            AddResult(result);
        }

        public override void OnBatchScanResults(IList<ScanResult>? results)
        {
            if (results is null)
                return;

            foreach (var result in results)
                AddResult(result);
        }

        public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] Scan failed : {errorCode}");
        }

        private void AddResult(ScanResult? result)
        {
            if (result is null)
                return;

            var address = result.Device?.Address;
            if (string.Equals(address, LaMaisonBeaconAddress, StringComparison.OrdinalIgnoreCase))
            {
                var fallbackKey = $"{LaMaisonBeaconUuid}:{LaMaisonBeaconMajor}:{LaMaisonBeaconMinor}";
                _latest[fallbackKey] = new BeaconAdvertisement(
                    LaMaisonBeaconUuid,
                    LaMaisonBeaconMajor,
                    LaMaisonBeaconMinor,
                    result.Rssi,
                    DateTime.UtcNow);

                return;
            }

            var data = result?.ScanRecord?.GetManufacturerSpecificData(AppleManufacturerId);
            if (data is null || data.Length < 23)
                return;

            var bytes = data.ToArray();
            if (bytes[0] != 0x02 || bytes[1] != 0x15)
                return;

            var uuid = FormatUuid(bytes, 2);
            var major = ReadUInt16(bytes, 18);
            var minor = ReadUInt16(bytes, 20);
            var rssi = result.Rssi;
            var key = $"{uuid}:{major}:{minor}";

            _latest[key] = new BeaconAdvertisement(
                uuid,
                major,
                minor,
                rssi,
                DateTime.UtcNow);
        }

        private static int ReadUInt16(byte[] bytes, int offset)
            => ((bytes[offset] & 0xFF) << 8) | (bytes[offset + 1] & 0xFF);

        private static string FormatUuid(byte[] bytes, int offset)
        {
            var hex = Convert.ToHexString(bytes, offset, 16);
            return string.Create(36, hex, (span, value) =>
            {
                value.AsSpan(0, 8).CopyTo(span[0..8]);
                span[8] = '-';
                value.AsSpan(8, 4).CopyTo(span[9..13]);
                span[13] = '-';
                value.AsSpan(12, 4).CopyTo(span[14..18]);
                span[18] = '-';
                value.AsSpan(16, 4).CopyTo(span[19..23]);
                span[23] = '-';
                value.AsSpan(20, 12).CopyTo(span[24..36]);
            });
        }
    }
}
#endif
