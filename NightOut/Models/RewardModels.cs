using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_rewards")]
public class BarReward : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("points_cost")]
    public int PointsCost { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("max_per_user_per_day")]
    public int? MaxPerUserPerDay { get; set; }

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true)]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public string CostLabel => $"{PointsCost} point{(PointsCost > 1 ? "s" : string.Empty)}";

    [JsonIgnore]
    public string StatusLabel => IsActive ? "Active" : "Inactive";

    [JsonIgnore]
    public string DescriptionLabel => string.IsNullOrWhiteSpace(Description)
        ? "Aucune description"
        : Description!;
}

public class RewardRedemptionIntentResult
{
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public string? TechnicalMessage { get; set; }
    public string IntentId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PointsCost { get; set; }
    public int Balance { get; set; }

    [JsonIgnore]
    public string CostLabel => $"{PointsCost} point{(PointsCost > 1 ? "s" : string.Empty)}";

    [JsonIgnore]
    public string ErrorLabel => Error switch
    {
        "not_authenticated" => "Connecte-toi pour utiliser tes points.",
        "reward_not_available" => "Cette recompense n'est plus disponible.",
        "reward_system_error" => "Le système de récompenses n'est pas encore prêt côté serveur.",
        "invalid_reward_id" => "Cette récompense est mal configurée.",
        "insufficient_balance" => "Tu n'as pas assez de points pour cette recompense.",
        "daily_limit_reached" => "Tu as deja utilise cette recompense aujourd'hui.",
        _ => "Impossible de preparer la recompense."
    };
}

public class RewardRedemptionResult
{
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public string? TechnicalMessage { get; set; }
    public string RedemptionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PointsCost { get; set; }
    public int Balance { get; set; }

    [JsonIgnore]
    public string ErrorLabel => Error switch
    {
        "not_authenticated" => "Connexion requise.",
        "invalid_token" => "Code invalide ou expire.",
        "reward_not_available" => "Cette recompense n'est plus disponible.",
        "reward_system_error" => "Le système de récompenses n'est pas encore prêt côté serveur.",
        "not_authorized" => "Ce compte ne peut pas valider cette recompense.",
        "insufficient_balance" => "Solde de points insuffisant.",
        "already_redeemed" => "Cette recompense a deja ete validee.",
        _ => "Transaction impossible."
    };
}

public class BarRewardRedemptionHistoryItem
{
    [JsonProperty("redemption_id")]
    public string RedemptionId { get; set; } = string.Empty;

    [JsonProperty("reward_id")]
    public string RewardId { get; set; } = string.Empty;

    [JsonProperty("reward_title")]
    public string RewardTitle { get; set; } = string.Empty;

    [JsonProperty("points_cost")]
    public int PointsCost { get; set; }

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("user_display_name")]
    public string UserDisplayName { get; set; } = string.Empty;

    [JsonProperty("validated_by")]
    public string? ValidatedBy { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public string UserLabel => string.IsNullOrWhiteSpace(UserDisplayName)
        ? "Client Spotiz"
        : UserDisplayName;

    [JsonIgnore]
    public string PointsLabel => $"{PointsCost} point{(PointsCost > 1 ? "s" : string.Empty)}";

    [JsonIgnore]
    public string DateLabel => CreatedAt == default
        ? string.Empty
        : CreatedAt.ToLocalTime().ToString("dd/MM HH:mm");
}
