namespace NightOut.Services;

public interface IBeaconScanner
{
    bool IsSupported { get; }
    BeaconScannerStatus LastStatus { get; }

    Task<IReadOnlyList<BeaconAdvertisement>> ScanAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
