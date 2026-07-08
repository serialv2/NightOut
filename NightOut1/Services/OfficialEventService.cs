using NightOut.Models;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class OfficialEventService(
    Client supabase,
    IProfessionalService professionalService,
    IAuthService auth,
    INotificationService notifications) : IOfficialEventService
{
    public Task<List<OfficialEvent>> GetMyOfficialEventsAsync()
        => GetMyOfficialEventsAsync(null);

    public async Task<List<OfficialEvent>> GetMyOfficialEventsAsync(string? barId)
    {
        var account = await professionalService.GetCurrentProfessionalAccountAsync();

        if (account is null || string.IsNullOrWhiteSpace(account.Id))
            return [];

        try
        {
            var query = supabase.From<OfficialEvent>()
                .Filter("professional_account_id", Operator.Equals, account.Id)
                .Order(e => e.StartAt, Ordering.Ascending);

            if (!string.IsNullOrWhiteSpace(barId))
                query = query.Filter("bar_id", Operator.Equals, barId);

            var result = await query.Get();

            var events = result?.Models?.ToList() ?? [];
            await EnrichEventsAsync(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetMyOfficialEvents erreur : {ex}");
            return [];
        }
    }


    public Task<List<ProEventDemographicStats>> GetMyEventDemographicStatsAsync()
        => GetMyEventDemographicStatsAsync(null);

    public async Task<List<ProEventDemographicStats>> GetMyEventDemographicStatsAsync(string? barId)
    {
        var account = await professionalService.GetCurrentProfessionalAccountAsync();

        if (account is null || string.IsNullOrWhiteSpace(account.Id))
            return [];

        try
        {
            var query = supabase.From<ProEventDemographicStats>()
                .Filter("professional_account_id", Operator.Equals, account.Id)
                .Order(x => x.StartAt, Ordering.Descending);

            if (!string.IsNullOrWhiteSpace(barId))
                query = query.Filter("bar_id", Operator.Equals, barId);

            var result = await query.Get();

            return result?.Models?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetMyEventDemographicStats erreur : {ex}");
            return [];
        }
    }

    public async Task<List<OfficialEvent>> GetPublicOfficialEventsAsync(string? cityId = null)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[EVENTS] Recherche événements publics city_id={cityId ?? "TOUTES"}");

            var query = supabase.From<OfficialEvent>()
                .Filter("is_active", Operator.Equals, "true")
                .Filter("status", Operator.Equals, "published")
                .Order(e => e.StartAt, Ordering.Ascending);

            if (!string.IsNullOrWhiteSpace(cityId))
                query = query.Filter("city_id", Operator.Equals, cityId);

            var result = await query.Get();

            System.Diagnostics.Debug.WriteLine($"[EVENTS] Supabase a renvoyé {result?.Models?.Count ?? 0} événement(s)");

            var now = DateTime.UtcNow.AddHours(-6);
            var events = (result?.Models ?? [])
                .Where(e => e.StartAt == default || e.StartAt.ToUniversalTime() >= now)
                .ToList();

            await EnrichEventsAsync(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetPublicOfficialEvents erreur : {ex}");
            return [];
        }
    }



    public async Task<List<OfficialEvent>> GetBarOfficialEventsAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return [];

        try
        {
            var result = await supabase.From<OfficialEvent>()
                .Filter("bar_id", Operator.Equals, barId)
                .Filter("is_active", Operator.Equals, "true")
                .Filter("status", Operator.Equals, "published")
                .Order(e => e.StartAt, Ordering.Ascending)
                .Get();

            var now = DateTime.UtcNow;

            var events = (result?.Models ?? [])
                .Where(e => GetEffectiveEndUtc(e) >= now)
                .OrderBy(e => e.StartAt)
                .ToList();

            await EnrichEventsAsync(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetBarOfficialEvents erreur : {ex}");
            return [];
        }
    }

    public async Task<OfficialEvent?> GetOfficialEventByIdAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return null;

        try
        {
            var result = await supabase.From<OfficialEvent>()
                .Filter("id", Operator.Equals, eventId)
                .Limit(1)
                .Get();

            var officialEvent = result?.Models?.FirstOrDefault();
            if (officialEvent is null)
                return null;

            await EnrichEventsAsync([officialEvent]);
            return officialEvent;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetOfficialEventById erreur : {ex}");
            return null;
        }
    }

    public Task<OfficialEvent?> CreateOfficialEventAsync(
        string title,
        string? description,
        DateTime startAt,
        DateTime? endAt,
        int? maxParticipants,
        string? flyerUrl)
        => CreateOfficialEventAsync(null, title, description, startAt, endAt, maxParticipants, flyerUrl);

    public async Task<OfficialEvent?> CreateOfficialEventAsync(
        string? barId,
        string title,
        string? description,
        DateTime startAt,
        DateTime? endAt,
        int? maxParticipants,
        string? flyerUrl)
    {
        var account = await professionalService.GetCurrentProfessionalAccountAsync();

        if (account is null || string.IsNullOrWhiteSpace(account.Id))
            throw new InvalidOperationException("compte_pro_introuvable");

        if (account.Status is not ("approved" or "partner"))
            throw new InvalidOperationException("compte_pro_non_valide");

        var bar = !string.IsNullOrWhiteSpace(barId)
            ? await GetOwnedBarByIdAsync(account.Id, barId)
            : await GetLinkedBarAsync(account.Id);

        if (bar is null || string.IsNullOrWhiteSpace(bar.Id))
            throw new InvalidOperationException("bar_lie_introuvable");

        var officialEvent = new OfficialEvent
        {
            ProfessionalAccountId = account.Id,
            BarId = bar.Id,
            CityId = bar.CityId ?? account.CityId,
            Title = title.Trim(),
            Description = Clean(description),
            FlyerUrl = Clean(flyerUrl),
            StartAt = startAt,
            EndAt = endAt,
            MaxParticipants = maxParticipants,
            Latitude = bar.Latitude,
            Longitude = bar.Longitude,
            Status = "published",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            var result = await supabase.From<OfficialEvent>().Insert(officialEvent);
            var created = result?.Models?.FirstOrDefault();

            if (created is not null)
                await NotifyBarFollowersAsync(created, bar);

            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] CreateOfficialEvent erreur : {ex}");
            throw;
        }
    }

    public Task<string?> UploadFlyerAsync(string professionalAccountId, FileResult file)
        => UploadFlyerAsync(professionalAccountId, null, file);

    public async Task<string?> UploadFlyerAsync(string professionalAccountId, string? barId, FileResult file)
    {
        if (string.IsNullOrWhiteSpace(professionalAccountId) || file is null)
            return null;

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            throw new InvalidOperationException("format_image_invalide");

        byte[] original;

        using (var stream = await file.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms);
            original = ms.ToArray();
        }

        if (original.Length > 10L * 1024 * 1024)
            throw new InvalidOperationException("image_trop_lourde");

        var compressed = CompressImage(original, 1600);
        var safeBarPart = string.IsNullOrWhiteSpace(barId) ? "general" : barId;
        var path = $"official_events/{professionalAccountId}/{safeBarPart}/flyer_{Guid.NewGuid():N}.jpg";

        await supabase.Storage.From("event-flyers").Upload(
            compressed,
            path,
            new Supabase.Storage.FileOptions
            {
                ContentType = "image/jpeg",
                Upsert = false
            });

        return supabase.Storage.From("event-flyers").GetPublicUrl(path);
    }

    public async Task<string?> GetMyParticipationStatusAsync(string eventId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(eventId))
            return null;

        try
        {
            var result = await supabase.From<OfficialEventParticipant>()
                .Filter("official_event_id", Operator.Equals, eventId)
                .Filter("user_id", Operator.Equals, me)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault()?.Status;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetMyParticipationStatus erreur : {ex}");
            return null;
        }
    }

    public async Task SetMyParticipationAsync(string eventId, string status)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me))
            throw new InvalidOperationException("utilisateur_non_connecte");

        if (string.IsNullOrWhiteSpace(eventId))
            throw new InvalidOperationException("evenement_introuvable");

        status = status switch
        {
            "going" or "maybe" or "not_going" => status,
            _ => "going"
        };

        var existing = await supabase.From<OfficialEventParticipant>()
            .Filter("official_event_id", Operator.Equals, eventId)
            .Filter("user_id", Operator.Equals, me)
            .Limit(1)
            .Get();

        var participant = existing?.Models?.FirstOrDefault();

        if (participant is null)
        {
            participant = new OfficialEventParticipant
            {
                Id = Guid.NewGuid().ToString(),
                OfficialEventId = eventId,
                UserId = me,
                Status = status,
                Source = "event_detail",
                UpdatedAt = DateTime.UtcNow
            };

            await supabase.From<OfficialEventParticipant>().Insert(participant);
        }
        else
        {
            participant.Status = status;
            participant.Source = "event_detail";
            participant.UpdatedAt = DateTime.UtcNow;
            await supabase.From<OfficialEventParticipant>().Update(participant);
        }
    }



    public async Task<bool> HasCheckedInAsync(string eventId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(eventId))
            return false;

        try
        {
            var result = await supabase.From<OfficialEventParticipant>()
                .Filter("official_event_id", Operator.Equals, eventId)
                .Filter("user_id", Operator.Equals, me)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault()?.CheckedIn == true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] HasCheckedIn erreur : {ex}");
            return false;
        }
    }


    public async Task<bool> CheckInOfficialEventAsync(string eventId, double userLatitude, double userLongitude)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me))
            throw new InvalidOperationException("utilisateur_non_connecte");

        if (string.IsNullOrWhiteSpace(eventId))
            throw new InvalidOperationException("evenement_introuvable");

        var officialEvent = await GetOfficialEventByIdAsync(eventId);
        if (officialEvent is null)
            throw new InvalidOperationException("evenement_introuvable");

        if (officialEvent.Latitude is null || officialEvent.Longitude is null)
            throw new InvalidOperationException("coordonnees_evenement_introuvables");

        var now = DateTime.UtcNow;
        var startUtc = officialEvent.StartAt == default ? now.AddMinutes(-1) : officialEvent.StartAt.ToUniversalTime();
        var endUtc = (officialEvent.EndAt ?? officialEvent.StartAt.AddHours(8)).ToUniversalTime();

        // Tolérance volontaire : possibilité de confirmer 2 h avant et jusqu'à 6 h après la fin.
        // Ça évite de bloquer un utilisateur sur place si l'horaire est légèrement mal renseigné.
        if (now < startUtc.AddHours(-2))
            throw new InvalidOperationException("checkin_trop_tot");

        if (now > endUtc.AddHours(6))
            throw new InvalidOperationException("checkin_trop_tard");

        var distanceMeters = CalculateDistanceMeters(
            userLatitude,
            userLongitude,
            officialEvent.Latitude.Value,
            officialEvent.Longitude.Value);

        const double maxCheckInDistanceMeters = 150;
        if (distanceMeters > maxCheckInDistanceMeters)
            throw new InvalidOperationException($"checkin_trop_loin:{Math.Round(distanceMeters)}");

        var existing = await supabase.From<OfficialEventParticipant>()
            .Filter("official_event_id", Operator.Equals, eventId)
            .Filter("user_id", Operator.Equals, me)
            .Limit(1)
            .Get();

        var participant = existing?.Models?.FirstOrDefault();

        if (participant is null)
        {
            participant = new OfficialEventParticipant
            {
                Id = Guid.NewGuid().ToString(),
                OfficialEventId = eventId,
                UserId = me,
                Status = "going",
                Source = "checkin",
                CheckedIn = true,
                CheckedInAt = DateTime.UtcNow,
                CheckinLatitude = userLatitude,
                CheckinLongitude = userLongitude,
                UpdatedAt = DateTime.UtcNow
            };

            await supabase.From<OfficialEventParticipant>().Insert(participant);
            return true;
        }

        if (participant.CheckedIn)
            return true;

        participant.Status = "going";
        participant.Source = "checkin";
        participant.CheckedIn = true;
        participant.CheckedInAt = DateTime.UtcNow;
        participant.CheckinLatitude = userLatitude;
        participant.CheckinLongitude = userLongitude;
        participant.UpdatedAt = DateTime.UtcNow;

        await supabase.From<OfficialEventParticipant>().Update(participant);
        return true;
    }

    public async Task<bool> IsFollowingBarAsync(string barId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(barId))
            return false;

        try
        {
            var result = await supabase.From<BarFollower>()
                .Filter("bar_id", Operator.Equals, barId)
                .Filter("user_id", Operator.Equals, me)
                .Limit(1)
                .Get();

            return result?.Models?.Any() == true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] IsFollowingBar erreur : {ex}");
            return false;
        }
    }


    public async Task<int> GetBarFollowersCountAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return 0;

        try
        {
            var result = await supabase.From<BarFollower>()
                .Filter("bar_id", Operator.Equals, barId)
                .Get();

            return result?.Models?.Count ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] GetBarFollowersCount erreur : {ex}");
            return 0;
        }
    }

    public async Task<bool> ToggleFollowBarAsync(string barId)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me))
            throw new InvalidOperationException("utilisateur_non_connecte");

        if (string.IsNullOrWhiteSpace(barId))
            throw new InvalidOperationException("bar_introuvable");

        var existing = await supabase.From<BarFollower>()
            .Filter("bar_id", Operator.Equals, barId)
            .Filter("user_id", Operator.Equals, me)
            .Limit(1)
            .Get();

        var follower = existing?.Models?.FirstOrDefault();

        if (follower is not null)
        {
            await supabase.From<BarFollower>()
                .Filter("id", Operator.Equals, follower.Id)
                .Delete();

            return false;
        }

        await supabase.From<BarFollower>().Insert(new BarFollower
        {
            Id = Guid.NewGuid().ToString(),
            BarId = barId,
            UserId = me
        });

        return true;
    }

    private async Task EnrichEventsAsync(List<OfficialEvent> events)
    {
        if (events.Count == 0)
            return;

        foreach (var item in events)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(item.BarId))
                {
                    var bar = await GetBarByIdAsync(item.BarId);
                    if (bar is not null)
                    {
                        item.Bar = bar;
                        item.BarName = bar.Name;
                        item.BarAddress = bar.Address;
                    }
                }

                if (!string.IsNullOrWhiteSpace(item.CityId))
                {
                    var city = await GetCityByIdAsync(item.CityId);
                    if (city is not null)
                        item.CityName = city.Name;
                }

                var participants = await supabase.From<OfficialEventParticipant>()
                    .Filter("official_event_id", Operator.Equals, item.Id)
                    .Get();

                var models = participants?.Models ?? [];
                item.GoingCount = models.Count(p => p.Status == "going");
                item.MaybeCount = models.Count(p => p.Status == "maybe");
                item.CheckedInCount = models.Count(p => p.CheckedIn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OfficialEventService] EnrichEvents erreur : {ex.Message}");
            }
        }
    }

    private async Task NotifyBarFollowersAsync(OfficialEvent officialEvent, Bar bar)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bar.Id))
                return;

            var followers = await supabase.From<BarFollower>()
                .Filter("bar_id", Operator.Equals, bar.Id)
                .Get();

            foreach (var follower in followers?.Models ?? [])
            {
                if (string.IsNullOrWhiteSpace(follower.UserId))
                    continue;

                await notifications.PushAsync(
                    follower.UserId,
                    "official_event_created",
                    actorId: null,
                    entityId: officialEvent.Id,
                    entityType: "official_event",
                    body: $"🎉 Nouvel événement chez {bar.Name} : {officialEvent.Title}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventService] NotifyBarFollowers erreur : {ex.Message}");
        }
    }

    private async Task<Bar?> GetLinkedBarAsync(string professionalAccountId)
    {
        var result = await supabase.From<Bar>()
            .Filter("professional_account_id", Operator.Equals, professionalAccountId)
            .Limit(1)
            .Get();

        return result?.Models?.FirstOrDefault();
    }


    private async Task<Bar?> GetOwnedBarByIdAsync(string professionalAccountId, string barId)
    {
        var result = await supabase.From<Bar>()
            .Filter("id", Operator.Equals, barId)
            .Filter("professional_account_id", Operator.Equals, professionalAccountId)
            .Limit(1)
            .Get();

        return result?.Models?.FirstOrDefault();
    }

    private async Task<Bar?> GetBarByIdAsync(string barId)
    {
        var result = await supabase.From<Bar>()
            .Filter("id", Operator.Equals, barId)
            .Limit(1)
            .Get();

        return result?.Models?.FirstOrDefault();
    }

    private async Task<City?> GetCityByIdAsync(string cityId)
    {
        var result = await supabase.From<City>()
            .Filter("id", Operator.Equals, cityId)
            .Limit(1)
            .Get();

        return result?.Models?.FirstOrDefault();
    }

    private static byte[] CompressImage(byte[] input, int maxEdge)
    {
        using var bitmap = SKBitmap.Decode(input);

        if (bitmap is null)
            return input;

        var ratio = Math.Min(
            (double)maxEdge / bitmap.Width,
            (double)maxEdge / bitmap.Height);

        if (ratio > 1)
            ratio = 1;

        var width = Math.Max(1, (int)(bitmap.Width * ratio));
        var height = Math.Max(1, (int)(bitmap.Height * ratio));

        using var resized = bitmap.Resize(
            new SKImageInfo(width, height),
            SKFilterQuality.High);

        using var image = SKImage.FromBitmap(resized ?? bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        return data.ToArray();
    }




    private static DateTime GetEffectiveEndUtc(OfficialEvent officialEvent)
    {
        var baseEnd = officialEvent.EndAt ?? officialEvent.StartAt.AddHours(8);
        return baseEnd.Kind == DateTimeKind.Utc ? baseEnd : baseEnd.ToUniversalTime();
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) *
            Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static string? Clean(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
