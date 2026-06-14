namespace NightOut.Services;

public interface ILocationService
{
    Task<(double Lat, double Lng)?> GetCurrentLocationAsync();
    Task StartTrackingAsync(Action<double, double> onLocationUpdated);
    void StopTracking();
    bool IsTracking { get; }
}
