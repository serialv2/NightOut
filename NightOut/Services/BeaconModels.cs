namespace NightOut.Services;

public sealed record BeaconAdvertisement(
    string Uuid,
    int Major,
    int Minor,
    int Rssi,
    DateTime SeenAtUtc);

public enum BeaconScannerStatus
{
    Unknown,
    Unsupported,
    MissingActivity,
    MissingBluetoothPermission,
    MissingLocationPermission,
    BluetoothOff,
    ScannerUnavailable,
    Scanning,
    ScanFailed,
    Completed,
    BleSeenNoIBeacon,
    AppleBeaconSeenButInvalid
}

public sealed record BeaconKey(string Uuid, int Major, int Minor)
{
    public static BeaconKey From(BeaconAdvertisement advertisement)
        => new(NormalizeUuid(advertisement.Uuid), advertisement.Major, advertisement.Minor);

    public static string NormalizeUuid(string uuid)
        => uuid.Trim().ToUpperInvariant();
}

public sealed class BeaconObservation
{
    public BeaconKey Key { get; init; } = new(string.Empty, 0, 0);
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public int BestRssi { get; set; } = int.MinValue;
    public int SeenCount { get; set; }
}
