using NightOut.Models;
using Microsoft.Maui.Storage;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class EphemeralEventService(
    Client supabase,
    IAuthService auth,
    IFriendService friends,
    INotificationService notifications,
    ICreditService credits) : IEphemeralEventService
{
    public async Task<List<EphemeralEvent>> GetPublicEphemeralEventsAsync(string? cityId = null)
    {
        try
        {
            var nowIso = DateTime.UtcNow.ToString("O");

            var query = supabase.From<EphemeralEvent>()
                .Filter("is_active", Operator.Equals, "true")
                .Filter("status", Operator.Equals, "published")
                .Filter("expires_at", Operator.GreaterThanOrEqual, nowIso)
                .Order(x => x.StartAt, Ordering.Ascending);

            if (!string.IsNullOrWhiteSpace(cityId))
                query = query.Filter("city_id", Operator.Equals, cityId);

            var result = await query.Get();
            var events = result?.Models?.ToList() ?? [];

            events = await FilterVisibleEventsAsync(events);
            await EnrichParticipantsAsync(events);
            await EnrichCreatorReputationsAsync(events);
            ApplyCurrentUserFlags(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetPublicEphemeralEvents erreur : {ex.Message}");
            return [];
        }
    }

    public async Task<List<EphemeralEvent>> GetBarEphemeralEventsAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return [];

        try
        {
            var minIso = DateTime.UtcNow.AddHours(-8).ToString("O");

            var result = await supabase.From<EphemeralEvent>()
                .Filter("bar_id", Operator.Equals, barId)
                .Filter("is_active", Operator.Equals, "true")
                .Filter("status", Operator.Equals, "published")
                .Filter("expires_at", Operator.GreaterThanOrEqual, minIso)
                .Order(x => x.StartAt, Ordering.Ascending)
                .Get();

            var events = result?.Models?.ToList() ?? [];
            events = await FilterVisibleEventsAsync(events);

            foreach (var item in events)
            {
                item.SourceType = "ephemeral";
                item.SourceId = item.Id;
            }

            await EnrichParticipantsAsync(events);
            await EnrichCreatorReputationsAsync(events);
            ApplyCurrentUserFlags(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetBarEphemeralEvents erreur : {ex.Message}");
            return [];
        }
    }

    public async Task<bool> JoinEphemeralEventAsync(string eventId)
        => await RespondToEphemeralEventAsync(eventId, "going");

    public async Task<bool> RespondToEphemeralEventAsync(string eventId, string status)
        => await RespondToEphemeralEventAsync(eventId, status, awardParticipationCredits: true);

    private async Task<bool> RespondToEphemeralEventAsync(string eventId, string status, bool awardParticipationCredits)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            status = status switch
            {
                "going" or "maybe" or "not_going" => status,
                _ => "going"
            };

            var existing = await supabase.From<EphemeralEventParticipant>()
                .Filter("ephemeral_event_id", Operator.Equals, eventId)
                .Filter("user_id", Operator.Equals, userId)
                .Limit(1)
                .Get();

            var participant = existing?.Models?.FirstOrDefault();
            var wasAlreadyGoing = participant?.Status == "going";

            if (participant is null)
            {
                participant = new EphemeralEventParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    EphemeralEventId = eventId,
                    UserId = userId,
                    Status = status,
                    JoinedAt = DateTime.UtcNow
                };

                await supabase.From<EphemeralEventParticipant>().Insert(participant);
            }
            else
            {
                participant.Status = status;
                participant.JoinedAt = DateTime.UtcNow;
                await supabase.From<EphemeralEventParticipant>().Update(participant);
            }

            if (awardParticipationCredits && status == "going" && !wasAlreadyGoing)
                await credits.AddMyCreditsByRuleAsync("join_ephemeral_event", eventId, "ephemeral_event", 10);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] RespondToEphemeralEvent erreur : {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CancelEphemeralEventAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var result = await supabase.From<EphemeralEvent>()
                .Filter("id", Operator.Equals, eventId)
                .Filter("creator_id", Operator.Equals, userId)
                .Limit(1)
                .Get();

            var item = result?.Models?.FirstOrDefault();
            if (item is null)
                return false;

            var participants = await GetParticipantsAsync(item.Id);

            await NotifyCancellationAsync(item, userId, participants);
            await credits.ReverseMyCreditsForEntityAsync("ephemeral_event_created", item.Id, "ephemeral_event", "ephemeral_event_cancelled");
            await ReverseParticipantCreditsAsync(item, participants);
            await CleanupGroupEventFeedMessageAsync(item);

            await supabase.From<EphemeralEvent>()
                .Filter("id", Operator.Equals, eventId)
                .Filter("creator_id", Operator.Equals, userId)
                .Set(e => e.Status, "cancelled")
                .Set(e => e.IsActive, false)
                .Set(e => e.UpdatedAt, DateTime.UtcNow)
                .Update();

            await CleanupParticipantsAsync(item.Id);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CancelEphemeralEvent erreur : {ex.Message}");
            return false;
        }
    }

    public async Task<EphemeralEvent?> CreateEphemeralEventAsync(EphemeralEvent item)
    {
        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
            item.CreatorId = userId;
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "published" : item.Status;
            item.Visibility = NormalizeVisibility(item.Visibility);

            if (item.Visibility == "public" && !await CurrentUserCanCreatePublicEventAsync())
                item.Visibility = "friends";

            item.GroupId = item.Visibility == "group" ? item.GroupId : null;
            item.IsActive = true;
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            if (item.Visibility == "group" && string.IsNullOrWhiteSpace(item.GroupId))
                return null;

            var result = await supabase.From<EphemeralEvent>().Insert(item);
            var created = result?.Models?.FirstOrDefault() ?? item;

            await RespondToEphemeralEventAsync(created.Id, "going", awardParticipationCredits: false);
            await NotifyTargetsAsync(created);
            await credits.AddMyCreditsByRuleAsync("ephemeral_event_created", created.Id, "ephemeral_event", 20);
            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CreateEphemeralEvent erreur : {ex.Message}");
            return null;
        }
    }

    public async Task<string?> UploadFlyerAsync(FileResult file)
    {
        if (file is null)
            return null;

        var userId = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
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
        var path = $"ephemeral_events/{userId}/flyer_{Guid.NewGuid():N}.jpg";

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

    public async Task<bool> RateCreatorAsync(string ephemeralEventId, int rating, bool wouldJoinAgain, bool wasWelcoming, bool descriptionMatched, bool goodAmbience)
    {
        if (string.IsNullOrWhiteSpace(ephemeralEventId))
            return false;

        try
        {
            var userId = auth.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var eventResult = await supabase.From<EphemeralEvent>()
                .Filter("id", Operator.Equals, ephemeralEventId)
                .Limit(1)
                .Get();

            var item = eventResult?.Models?.FirstOrDefault();
            if (item is null || string.IsNullOrWhiteSpace(item.CreatorId) || item.CreatorId == userId)
                return false;

            var existingResult = await supabase.From<EventCreatorReview>()
                .Filter("ephemeral_event_id", Operator.Equals, ephemeralEventId)
                .Filter("reviewer_id", Operator.Equals, userId)
                .Limit(1)
                .Get();

            var existing = existingResult?.Models?.FirstOrDefault();

            rating = Math.Clamp(rating, 1, 5);
            var review = existing ?? new EventCreatorReview
            {
                Id = Guid.NewGuid().ToString(),
                EphemeralEventId = ephemeralEventId,
                CreatorId = item.CreatorId,
                ReviewerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            review.Rating = rating;
            review.WouldJoinAgain = wouldJoinAgain;
            review.WasWelcoming = wasWelcoming;
            review.DescriptionMatched = descriptionMatched;
            review.GoodAmbience = goodAmbience;

            await supabase.From<EventCreatorReview>().Upsert(review);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] RateCreator erreur : {ex.Message}");
            return false;
        }
    }

    private async Task<List<EphemeralEvent>> FilterVisibleEventsAsync(List<EphemeralEvent> events)
    {
        if (events.Count == 0)
            return events;

        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me))
            return events.Where(e => NormalizeVisibility(e.Visibility) == "public").ToList();

        var friendIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var myFriends = await friends.GetFriendsAsync();
            foreach (var friend in myFriends.Where(f => !string.IsNullOrWhiteSpace(f.Id)))
                friendIds.Add(friend.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] Filter friends erreur : {ex.Message}");
        }

        var myGroupIds = await GetMyGroupIdsAsync(me);

        return events.Where(e =>
        {
            var visibility = NormalizeVisibility(e.Visibility);
            if (e.CreatorId == me)
                return true;

            return visibility switch
            {
                "group" => !string.IsNullOrWhiteSpace(e.GroupId) && myGroupIds.Contains(e.GroupId),
                "friends" => !string.IsNullOrWhiteSpace(e.CreatorId) && friendIds.Contains(e.CreatorId),
                _ => true
            };
        }).ToList();
    }

    private async Task<HashSet<string>> GetMyGroupIdsAsync(string me)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var owned = await supabase.From<FriendGroup>()
                .Filter("owner_id", Operator.Equals, me)
                .Get();

            foreach (var group in owned?.Models ?? [])
                if (!string.IsNullOrWhiteSpace(group.Id))
                    ids.Add(group.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] owned groups erreur : {ex.Message}");
        }

        try
        {
            var memberships = await supabase.From<FriendGroupMember>()
                .Filter("user_id", Operator.Equals, me)
                .Get();

            foreach (var member in memberships?.Models ?? [])
                if (!string.IsNullOrWhiteSpace(member.GroupId))
                    ids.Add(member.GroupId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] group memberships erreur : {ex.Message}");
        }

        return ids;
    }

    private void ApplyCurrentUserFlags(List<EphemeralEvent> events)
    {
        var me = auth.GetCurrentUserId();
        foreach (var item in events)
        {
            item.CanCancel = !item.IsOfficialEvent
                             && !string.IsNullOrWhiteSpace(me)
                             && string.Equals(item.CreatorId, me, StringComparison.OrdinalIgnoreCase)
                             && item.IsActive
                             && string.Equals(item.Status, "published", StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task CleanupGroupEventFeedMessageAsync(EphemeralEvent item)
    {
        if (NormalizeVisibility(item.Visibility) != "group" || string.IsNullOrWhiteSpace(item.GroupId))
            return;

        try
        {
            await supabase.Rpc("cleanup_group_ephemeral_event_message", new
            {
                p_group_id = item.GroupId,
                p_event_title = item.Title
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CleanupGroupEventFeedMessage erreur : {ex.Message}");
        }

        try
        {
            var safeTitle = EscapeLikePattern(item.Title);
            await supabase.From<FriendGroupMessage>()
                .Filter("group_id", Operator.Equals, item.GroupId)
                .Filter("message_type", Operator.Equals, "text")
                .Filter("message_text", Operator.ILike, $"%{safeTitle}%")
                .Delete();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CleanupGroupEventFeedMessage fallback erreur : {ex.Message}");
        }
    }

    private async Task NotifyCancellationAsync(
        EphemeralEvent item,
        string creatorId,
        List<EphemeralEventParticipant> participants)
    {
        try
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var participant in participants)
            {
                if (!string.IsNullOrWhiteSpace(participant.UserId) && participant.UserId != creatorId)
                    recipients.Add(participant.UserId);
            }

            if (item.Visibility == "group")
            {
                foreach (var memberId in await GetGroupMemberIdsAsync(item.GroupId, creatorId))
                    recipients.Add(memberId);
            }

            var place = string.IsNullOrWhiteSpace(item.PlaceName) ? "NightOut" : item.PlaceName.Trim();
            var body = $"La sortie {item.Title} à {place} a été annulée.";

            foreach (var userId in recipients)
            {
                await notifications.PushAsync(
                    userId,
                    "ephemeral_event_cancelled",
                    creatorId,
                    item.Id,
                    "ephemeral_event",
                    body);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] NotifyCancellation erreur : {ex.Message}");
        }
    }

    private async Task<List<EphemeralEventParticipant>> GetParticipantsAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return [];

        try
        {
            var result = await supabase.From<EphemeralEventParticipant>()
                .Filter("ephemeral_event_id", Operator.Equals, eventId)
                .Get();

            return result?.Models?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetParticipants erreur : {ex.Message}");
            return [];
        }
    }

    private async Task ReverseParticipantCreditsAsync(
        EphemeralEvent item,
        List<EphemeralEventParticipant> participants)
    {
        foreach (var participant in participants.Where(p => p.Status == "going"))
        {
            if (string.IsNullOrWhiteSpace(participant.UserId))
                continue;

            try
            {
                await credits.ReverseCreditsForEntityAsync(
                    participant.UserId,
                    "join_ephemeral_event",
                    item.Id,
                    "ephemeral_event",
                    "ephemeral_event_cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] ReverseParticipantCredits erreur : {ex.Message}");
            }
        }
    }

    private async Task CleanupParticipantsAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        try
        {
            await supabase.From<EphemeralEventParticipant>()
                .Filter("ephemeral_event_id", Operator.Equals, eventId)
                .Delete();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CleanupParticipants erreur : {ex.Message}");
        }
    }

    private async Task NotifyTargetsAsync(EphemeralEvent item)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(item.Id))
            return;

        try
        {
            var recipients = item.Visibility == "group"
                ? await GetGroupMemberIdsAsync(item.GroupId, me)
                : await GetFriendIdsAsync(me);

            if (recipients.Count == 0)
                return;

            var title = item.Visibility == "group"
                ? "Nouvelle sortie dans ton groupe"
                : "Nouvelle sortie d'un ami";

            var place = string.IsNullOrWhiteSpace(item.PlaceName) ? "un lieu NightOut" : item.PlaceName.Trim();
            var body = $"{item.Title} • {place} à {item.StartAt.ToLocalTime():HH:mm}";
            var type = item.Visibility == "group" ? "ephemeral_event_group" : "ephemeral_event_friend";

            foreach (var userId in recipients)
            {
                await notifications.PushAsync(
                    userId,
                    type,
                    me,
                    item.Id,
                    "ephemeral_event",
                    body);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] NotifyTargets erreur : {ex.Message}");
        }
    }

    private async Task<List<string>> GetFriendIdsAsync(string me)
    {
        try
        {
            var myFriends = await friends.GetFriendsAsync();
            return myFriends
                .Where(f => !string.IsNullOrWhiteSpace(f.Id) && f.Id != me)
                .Select(f => f.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetFriendIds erreur : {ex.Message}");
            return [];
        }
    }


    public async Task<List<EphemeralEvent>> GetGroupEphemeralEventsAsync(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return [];

        try
        {
            var nowIso = DateTime.UtcNow.ToString("O");

            var result = await supabase.From<EphemeralEvent>()
                .Filter("group_id", Operator.Equals, groupId)
                .Filter("visibility", Operator.Equals, "group")
                .Filter("is_active", Operator.Equals, "true")
                .Filter("status", Operator.Equals, "published")
                .Filter("expires_at", Operator.GreaterThanOrEqual, nowIso)
                .Order(x => x.StartAt, Ordering.Ascending)
                .Get();

            var events = result?.Models?.ToList() ?? [];
            foreach (var item in events)
            {
                item.SourceType = "ephemeral";
                item.SourceId = item.Id;
            }

            await EnrichParticipantsAsync(events);
            await EnrichCreatorReputationsAsync(events);
            ApplyCurrentUserFlags(events);
            return events;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetGroupEphemeralEvents erreur : {ex.Message}");
            return [];
        }
    }

    private async Task<List<string>> GetGroupMemberIdsAsync(string? groupId, string me)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return [];

        try
        {
            var result = await supabase.From<FriendGroupMember>()
                .Filter("group_id", Operator.Equals, groupId)
                .Get();

            return (result?.Models ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.UserId) && m.UserId != me)
                .Select(m => m.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] GetGroupMemberIds erreur : {ex.Message}");
            return [];
        }
    }

    private static string NormalizeVisibility(string? visibility) => visibility?.Trim().ToLowerInvariant() switch
    {
        "friends" => "friends",
        "group" => "group",
        _ => "public"
    };

    private async Task<bool> CurrentUserCanCreatePublicEventAsync()
    {
        try
        {
            var profile = await auth.GetCurrentProfileAsync();
            return profile?.IsPro == true
                   || string.Equals(profile?.AccountType, "pro", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(profile?.ProfessionalStatus, "approved", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(profile?.ProfessionalStatus, "partner", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] CurrentUserCanCreatePublicEvent erreur : {ex.Message}");
            return false;
        }
    }

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

    private async Task EnrichParticipantsAsync(List<EphemeralEvent> events)
    {
        if (events.Count == 0)
            return;

        try
        {
            foreach (var item in events)
            {
                var result = await supabase.From<EphemeralEventParticipant>()
                    .Filter("ephemeral_event_id", Operator.Equals, item.Id)
                    .Filter("status", Operator.Equals, "going")
                    .Get();

                var participants = result?.Models?.ToList() ?? [];
                item.ParticipantsCount = participants.Count;
                item.ParticipantInitials = participants
                    .Take(5)
                    .Select((_, index) => DemoInitials[index % DemoInitials.Length])
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] EnrichParticipants erreur : {ex.Message}");
        }
    }

    private async Task EnrichCreatorReputationsAsync(List<EphemeralEvent> events)
    {
        var creatorIds = events
            .Where(e => !e.IsOfficialEvent && !string.IsNullOrWhiteSpace(e.CreatorId))
            .Select(e => e.CreatorId!)
            .Distinct()
            .ToList();

        if (creatorIds.Count == 0)
            return;

        try
        {
            var reputations = new Dictionary<string, EventCreatorReputation>();

            foreach (var creatorId in creatorIds)
            {
                var result = await supabase.From<EventCreatorReputation>()
                    .Filter("creator_id", Operator.Equals, creatorId)
                    .Limit(1)
                    .Get();

                var reputation = result?.Models?.FirstOrDefault();
                if (reputation is not null)
                    reputations[creatorId] = reputation;
            }

            foreach (var item in events.Where(e => !e.IsOfficialEvent && !string.IsNullOrWhiteSpace(e.CreatorId)))
            {
                if (reputations.TryGetValue(item.CreatorId!, out var reputation))
                {
                    item.CreatorDisplayName = reputation.Name;
                    item.CreatorRatingLabel = reputation.RatingLabel;
                    item.CreatorBadgeLabel = reputation.BadgeLabel;
                    item.CreatorStatsLabel = reputation.StatsLabel;
                }
                else
                {
                    item.CreatorDisplayName = "Organisateur NightOut";
                    item.CreatorRatingLabel = "Nouveau";
                    item.CreatorBadgeLabel = "🏅 Nouveau créateur";
                    item.CreatorStatsLabel = "Première sortie";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EphemeralEventService] EnrichCreatorReputations erreur : {ex.Message}");
        }
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

    private static readonly string[] DemoInitials = ["B", "A", "L", "E", "M"];
}
