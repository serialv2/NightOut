namespace NightOut.Services;

public sealed class UnsupportedBeaconScanner : IBeaconScanner
{
    public bool IsSupported => false;
    public BeaconScannerStatus LastStatus => BeaconScannerStatus.Unsupported;

    public Task<IReadOnlyList<BeaconAdvertisement>> ScanAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BeaconAdvertisement>>([]);
    }
}
