using NightOut.Models;

namespace NightOut.Services;

public interface IRewardService
{
    Task<List<BarReward>> GetActiveRewardsForBarAsync(string barId);

    Task<List<BarReward>> GetRewardsForBarAsync(string barId);

    Task<List<BarRewardRedemptionHistoryItem>> GetRedemptionsForBarAsync(string barId, int limit = 30);

    Task<BarReward?> SaveRewardAsync(BarReward reward);

    Task<bool> SetRewardActiveAsync(string rewardId, bool isActive);

    Task<RewardRedemptionIntentResult> CreateRedemptionIntentAsync(string rewardId);

    Task<RewardRedemptionResult> RedeemRewardAsync(string tokenOrShortCode, string? barId = null);
}
