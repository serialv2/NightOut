namespace NightOut.Services;

public interface IPushNotificationService
{
    Task InitializeAsync();
    Task RegisterDeviceAsync();
    Task<string?> GetCurrentTokenAsync();
}
