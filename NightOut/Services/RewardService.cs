using NightOut.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class RewardService(Client supabase, IAuthService auth) : IRewardService
{
    public async Task<List<BarReward>> GetActiveRewardsForBarAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return [];

        try
        {
            var now = DateTime.UtcNow;
            var result = await supabase.From<BarReward>()
                .Filter("bar_id", Operator.Equals, barId)
                .Filter("is_active", Operator.Equals, "true")
                .Order(r => r.PointsCost, Ordering.Ascending)
                .Get();

            return (result?.Models ?? [])
                .Where(r => (!r.StartsAt.HasValue || ToUtc(r.StartsAt.Value) <= now) &&
                            (!r.EndsAt.HasValue || ToUtc(r.EndsAt.Value) >= now))
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] GetActiveRewardsForBar erreur : {ex}");
            return [];
        }
    }

    public async Task<List<BarReward>> GetRewardsForBarAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return [];

        try
        {
            var result = await supabase.From<BarReward>()
                .Filter("bar_id", Operator.Equals, barId)
                .Order(r => r.PointsCost, Ordering.Ascending)
                .Get();

            return result?.Models?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] GetRewardsForBar erreur : {ex}");
            return [];
        }
    }

    public async Task<List<BarRewardRedemptionHistoryItem>> GetRedemptionsForBarAsync(string barId, int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return [];

        try
        {
            var response = await supabase.Rpc("get_bar_reward_redemptions", new
            {
                p_bar_id = barId,
                p_limit = Math.Clamp(limit, 1, 100)
            });

            if (string.IsNullOrWhiteSpace(response?.Content))
                return [];

            return JsonConvert.DeserializeObject<List<BarRewardRedemptionHistoryItem>>(response.Content) ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] GetRedemptionsForBar erreur : {ex}");
            return [];
        }
    }

    public async Task<BarReward?> SaveRewardAsync(BarReward reward)
    {
        if (reward is null || string.IsNullOrWhiteSpace(reward.BarId) || string.IsNullOrWhiteSpace(reward.Title) || reward.PointsCost <= 0)
            return null;

        try
        {
            reward.Title = reward.Title.Trim();
            reward.Description = string.IsNullOrWhiteSpace(reward.Description) ? null : reward.Description.Trim();
            reward.UpdatedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(reward.Id))
            {
                reward.Id = Guid.NewGuid().ToString();
                reward.CreatedBy = auth.GetCurrentUserId();
                var inserted = await supabase.From<BarReward>().Insert(reward);
                return inserted?.Models?.FirstOrDefault() ?? reward;
            }

            await supabase.From<BarReward>()
                .Filter("id", Operator.Equals, reward.Id)
                .Set(r => r.Title, reward.Title)
                .Set(r => r.Description, reward.Description)
                .Set(r => r.PointsCost, reward.PointsCost)
                .Set(r => r.MaxPerUserPerDay, reward.MaxPerUserPerDay)
                .Set(r => r.IsActive, reward.IsActive)
                .Set(r => r.UpdatedAt, DateTime.UtcNow)
                .Update();

            return reward;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] SaveReward erreur : {ex}");
            return null;
        }
    }

    public async Task<bool> SetRewardActiveAsync(string rewardId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
            return false;

        try
        {
            await supabase.From<BarReward>()
                .Filter("id", Operator.Equals, rewardId)
                .Set(r => r.IsActive, isActive)
                .Set(r => r.UpdatedAt, DateTime.UtcNow)
                .Update();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] SetRewardActive erreur : {ex}");
            return false;
        }
    }

    public async Task<RewardRedemptionIntentResult> CreateRedemptionIntentAsync(string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId))
            return new RewardRedemptionIntentResult { IsSuccess = false, Error = "invalid_reward_id" };

        if (!Guid.TryParse(rewardId, out _))
            return new RewardRedemptionIntentResult { IsSuccess = false, Error = "invalid_reward_id" };

        try
        {
            var existingReward = await supabase.From<BarReward>()
                .Filter("id", Operator.Equals, rewardId)
                .Limit(1)
                .Get();

            var reward = existingReward?.Models?.FirstOrDefault();
            if (reward is null || !reward.IsActive)
                return new RewardRedemptionIntentResult { IsSuccess = false, Error = "reward_not_available" };

            var response = await supabase.Rpc("create_reward_redemption_intent", new
            {
                p_reward_id = rewardId
            });

            var parsed = ParseIntent(response?.Content);
            if (!parsed.IsSuccess && parsed.Error == "reward_not_available")
            {
                parsed.Title = reward.Title;
                parsed.PointsCost = reward.PointsCost;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] CreateRedemptionIntent erreur : {ex}");
            return new RewardRedemptionIntentResult
            {
                IsSuccess = false,
                Error = "reward_system_error",
                TechnicalMessage = ex.Message
            };
        }
    }

    public async Task<RewardRedemptionResult> RedeemRewardAsync(string tokenOrShortCode, string? barId = null)
    {
        if (string.IsNullOrWhiteSpace(tokenOrShortCode))
            return new RewardRedemptionResult { IsSuccess = false, Error = "invalid_token" };

        try
        {
            var code = tokenOrShortCode.Trim();
            var isShortCode = code.Length <= 8;
            var response = await supabase.Rpc("redeem_reward_token", new
            {
                p_token = isShortCode ? null : code,
                p_short_code = isShortCode ? code.ToUpperInvariant() : null,
                p_bar_id = barId
            });

            return ParseRedemption(response?.Content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] RedeemReward erreur : {ex}");
            return new RewardRedemptionResult
            {
                IsSuccess = false,
                Error = "reward_system_error",
                TechnicalMessage = ex.Message
            };
        }
    }

    private static RewardRedemptionIntentResult ParseIntent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RewardRedemptionIntentResult { IsSuccess = false, Error = "reward_system_error" };

        var obj = TryReadObject(json);
        if (obj is null)
            return new RewardRedemptionIntentResult { IsSuccess = false, Error = "reward_system_error" };

        return new RewardRedemptionIntentResult
        {
            IsSuccess = obj.Value<bool?>("ok") == true,
            Error = obj.Value<string>("error"),
            IntentId = obj.Value<string>("intent_id") ?? string.Empty,
            Token = obj.Value<string>("token") ?? string.Empty,
            ShortCode = obj.Value<string>("short_code") ?? string.Empty,
            ExpiresAt = obj.Value<DateTime?>("expires_at"),
            Title = obj.Value<string>("title") ?? string.Empty,
            PointsCost = obj.Value<int?>("points_cost") ?? 0,
            Balance = obj.Value<int?>("balance") ?? 0
        };
    }

    private static RewardRedemptionResult ParseRedemption(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RewardRedemptionResult { IsSuccess = false, Error = "reward_system_error" };

        var obj = TryReadObject(json);
        if (obj is null)
            return new RewardRedemptionResult { IsSuccess = false, Error = "reward_system_error" };

        return new RewardRedemptionResult
        {
            IsSuccess = obj.Value<bool?>("ok") == true,
            Error = obj.Value<string>("error"),
            RedemptionId = obj.Value<string>("redemption_id") ?? string.Empty,
            Title = obj.Value<string>("title") ?? string.Empty,
            PointsCost = obj.Value<int?>("points_cost") ?? 0,
            Balance = obj.Value<int?>("balance") ?? 0
        };
    }

    private static JObject? TryReadObject(string json)
    {
        try
        {
            var token = JToken.Parse(json);

            if (token is JObject obj)
                return obj;

            if (token is JArray arr && arr.FirstOrDefault() is JObject firstObj)
                return firstObj;

            if (token is JValue { Type: JTokenType.String } value &&
                value.Value<string>() is { } nested &&
                nested.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return JObject.Parse(nested);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RewardService] Parse RPC json erreur : {ex.Message} / {json}");
        }

        return null;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
