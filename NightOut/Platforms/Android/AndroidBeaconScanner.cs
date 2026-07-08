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

#pragma warning disable CA1416, CA1422
public sealed class AndroidBeaconScanner : IBeaconScanner
{
    private const int AppleManufacturerId = 0x004C;

    public bool IsSupported
    {
        get
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(23))
                return false;

            if (IsProbablyEmulator())
            {
                LastStatus = BeaconScannerStatus.Unsupported;
                return false;
            }

            try
            {
                var packageManager = Platform.AppContext?.PackageManager;
                var hasBle = packageManager?.HasSystemFeature(PackageManager.FeatureBluetoothLe) == true;
                return hasBle && BluetoothAdapter.DefaultAdapter is not null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] Support BLE indisponible : {ex.Message}");
                return false;
            }
        }
    }

    private static bool IsProbablyEmulator()
    {
        static bool ContainsEmulatorMarker(string? value)
            => !string.IsNullOrWhiteSpace(value) &&
               (value.Contains("generic", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("emulator", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("sdk_gphone", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("ranchu", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("goldfish", StringComparison.OrdinalIgnoreCase));

        return ContainsEmulatorMarker(Build.Fingerprint) ||
               ContainsEmulatorMarker(Build.Model) ||
               ContainsEmulatorMarker(Build.Manufacturer) ||
               ContainsEmulatorMarker(Build.Brand) ||
               ContainsEmulatorMarker(Build.Device) ||
               ContainsEmulatorMarker(Build.Hardware) ||
               ContainsEmulatorMarker(Build.Product);
    }

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

        try
        {
            if (!HasScanPermission())
                return [];
        }
        catch (Exception ex)
        {
            LastStatus = BeaconScannerStatus.MissingBluetoothPermission;
            System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] Permission scan erreur : {ex.Message}");
            return [];
        }

        BluetoothAdapter? adapter;
        BluetoothLeScanner? scanner;

        try
        {
            adapter = BluetoothAdapter.DefaultAdapter;
            scanner = adapter?.BluetoothLeScanner;
        }
        catch (Exception ex)
        {
            LastStatus = BeaconScannerStatus.ScannerUnavailable;
            System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] Scanner BLE indisponible : {ex.Message}");
            return [];
        }

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

        ScanSettings settings;
        try
        {
            var builtSettings = new ScanSettings.Builder()
                .SetScanMode(global::Android.Bluetooth.LE.ScanMode.LowLatency)
                .Build();

            if (builtSettings is null)
            {
                LastStatus = BeaconScannerStatus.ScannerUnavailable;
                return [];
            }

            settings = builtSettings;
        }
        catch (Exception ex)
        {
            LastStatus = BeaconScannerStatus.ScannerUnavailable;
            System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] ScanSettings erreur : {ex.Message}");
            return [];
        }

        var callback = new IBeaconScanCallback();
        var scanStarted = false;

        try
        {
            LastStatus = BeaconScannerStatus.Scanning;
            scanner.StartScan(null, settings, callback);
            scanStarted = true;
            await Task.Delay(duration, cancellationToken);
            LastStatus = callback.GetCompletionStatus();
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
                if (scanStarted)
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
        private bool _sawBleDevice;
        private bool _sawAppleManufacturerData;

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

        public BeaconScannerStatus GetCompletionStatus()
        {
            if (_latest.Count > 0)
                return BeaconScannerStatus.Completed;

            if (_sawAppleManufacturerData)
                return BeaconScannerStatus.AppleBeaconSeenButInvalid;

            if (_sawBleDevice)
                return BeaconScannerStatus.BleSeenNoIBeacon;

            return BeaconScannerStatus.Completed;
        }

        private void AddResult(ScanResult? result)
        {
            try
            {
                if (result is null)
                    return;

                _sawBleDevice = true;

                var record = result.ScanRecord;
                var data = record?.GetManufacturerSpecificData(AppleManufacturerId)
                    ?? ExtractAppleManufacturerData(record?.GetBytes());
                if (data is null || data.Length < 23)
                    return;

                _sawAppleManufacturerData = true;

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidBeaconScanner] Resultat BLE ignore : {ex.Message}");
            }
        }

        private static int ReadUInt16(byte[] bytes, int offset)
            => ((bytes[offset] & 0xFF) << 8) | (bytes[offset + 1] & 0xFF);

        private static byte[]? ExtractAppleManufacturerData(byte[]? recordBytes)
        {
            if (recordBytes is null || recordBytes.Length == 0)
                return null;

            var offset = 0;
            while (offset < recordBytes.Length)
            {
                var length = recordBytes[offset] & 0xFF;
                if (length == 0)
                    break;

                var nextOffset = offset + length + 1;
                if (nextOffset > recordBytes.Length || length < 3)
                    break;

                var type = recordBytes[offset + 1] & 0xFF;
                if (type == 0xFF)
                {
                    var manufacturerId = (recordBytes[offset + 2] & 0xFF)
                        | ((recordBytes[offset + 3] & 0xFF) << 8);

                    if (manufacturerId == AppleManufacturerId)
                    {
                        var dataLength = length - 3;
                        if (dataLength <= 0)
                            return null;

                        var manufacturerData = new byte[dataLength];
                        System.Buffer.BlockCopy(recordBytes, offset + 4, manufacturerData, 0, dataLength);
                        return manufacturerData;
                    }
                }

                offset = nextOffset;
            }

            return null;
        }

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
#pragma warning restore CA1416, CA1422
#endif
