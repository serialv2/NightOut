using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("event_creator_reputation")]
public class EventCreatorReputation : BaseModel
{
    [PrimaryKey("creator_id", false)]
    public string CreatorId { get; set; } = string.Empty;

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("events_created")]
    public int EventsCreated { get; set; }

    [Column("participants_total")]
    public int ParticipantsTotal { get; set; }

    [Column("reviews_count")]
    public int ReviewsCount { get; set; }

    [Column("average_rating")]
    public double? AverageRating { get; set; }

    [Column("join_again_percent")]
    public int JoinAgainPercent { get; set; }

    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? "Organisateur NightOut" : DisplayName!;

    public string RatingLabel => ReviewsCount <= 0 || AverageRating is null
        ? "Nouveau"
        : $"⭐ {AverageRating.Value:0.0}";

    public string StatsLabel
    {
        get
        {
            if (EventsCreated <= 0)
                return "Nouveau créateur";

            var events = $"{EventsCreated} sortie{(EventsCreated > 1 ? "s" : string.Empty)}";
            var participants = ParticipantsTotal > 0 ? $" · {ParticipantsTotal} participant{(ParticipantsTotal > 1 ? "s" : string.Empty)}" : string.Empty;
            var again = JoinAgainPercent > 0 ? $" · {JoinAgainPercent}% recommandent" : string.Empty;
            return events + participants + again;
        }
    }

    public string BadgeLabel
    {
        get
        {
            if (EventsCreated >= 25 && ReviewsCount >= 10 && (AverageRating ?? 0) >= 4.6)
                return "👑 Ambassadeur";
            if (EventsCreated >= 10 && ReviewsCount >= 5 && (AverageRating ?? 0) >= 4.3)
                return "🥇 Organisateur populaire";
            if (EventsCreated >= 3)
                return "🥈 Organisateur confirmé";
            return "🏅 Nouveau créateur";
        }
    }
}
