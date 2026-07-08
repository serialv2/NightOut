using NightOut.Models;

namespace NightOut.Services;

public interface ICreditService
{
    Task<UserCredit?> GetMyCreditsAsync();
    Task<List<CreditTransaction>> GetMyTransactionsAsync(int limit = 30);
    Task<bool> AddCreditsAsync(string userId, int amount, string reason, string? entityId = null, string? entityType = null);
    Task<bool> AddMyCreditsAsync(int amount, string reason, string? entityId = null, string? entityType = null);
    Task<bool> AddCreditsByRuleAsync(string userId, string ruleKey, string? entityId = null, string? entityType = null, int fallbackAmount = 0);
    Task<bool> AddMyCreditsByRuleAsync(string ruleKey, string? entityId = null, string? entityType = null, int fallbackAmount = 0);
    Task<bool> ReverseCreditsForEntityAsync(string userId, string originalRuleKey, string entityId, string entityType, string reversalRuleKey);
    Task<bool> ReverseMyCreditsForEntityAsync(string originalRuleKey, string entityId, string entityType, string reversalRuleKey);
}
