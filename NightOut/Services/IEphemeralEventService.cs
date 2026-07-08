using NightOut.Models;
using Microsoft.Maui.Storage;

namespace NightOut.Services;

public interface IEphemeralEventService
{
    Task<List<EphemeralEvent>> GetPublicEphemeralEventsAsync(string? cityId = null);
    Task<List<EphemeralEvent>> GetBarEphemeralEventsAsync(string barId);
    Task<List<EphemeralEvent>> GetGroupEphemeralEventsAsync(string groupId);
    Task<bool> JoinEphemeralEventAsync(string eventId);
    Task<bool> RespondToEphemeralEventAsync(string eventId, string status);
    Task<bool> CancelEphemeralEventAsync(string eventId);
    Task<EphemeralEvent?> CreateEphemeralEventAsync(EphemeralEvent item);
    Task<string?> UploadFlyerAsync(FileResult file);
    Task<bool> RateCreatorAsync(string ephemeralEventId, int rating, bool wouldJoinAgain, bool wasWelcoming, bool descriptionMatched, bool goodAmbience);
}
