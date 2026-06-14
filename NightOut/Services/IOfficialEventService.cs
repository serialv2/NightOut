using NightOut.Models;

namespace NightOut.Services;

public interface IOfficialEventService
{
    Task<List<OfficialEvent>> GetMyOfficialEventsAsync();

    Task<List<ProEventDemographicStats>> GetMyEventDemographicStatsAsync();

    Task<List<OfficialEvent>> GetPublicOfficialEventsAsync(string? cityId = null);

    Task<List<OfficialEvent>> GetBarOfficialEventsAsync(string barId);

    Task<OfficialEvent?> GetOfficialEventByIdAsync(string eventId);

    Task<OfficialEvent?> CreateOfficialEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime? endAt,
        int? maxParticipants,
        string? flyerUrl);

    Task<string?> UploadFlyerAsync(string professionalAccountId, FileResult file);

    Task<string?> GetMyParticipationStatusAsync(string eventId);

    Task SetMyParticipationAsync(string eventId, string status);

    Task<bool> HasCheckedInAsync(string eventId);

    Task<bool> CheckInOfficialEventAsync(string eventId, double userLatitude, double userLongitude);

    Task<bool> IsFollowingBarAsync(string barId);

    Task<int> GetBarFollowersCountAsync(string barId);

    Task<bool> ToggleFollowBarAsync(string barId);
}
