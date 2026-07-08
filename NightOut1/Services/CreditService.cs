using NightOut.Models;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class CreditService(Client supabase, IAuthService auth) : ICreditService
{
    public async Task<UserCredit?> GetMyCreditsAsync()
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return null;
        try
        {
            var result = await supabase.From<UserCredit>()
                .Filter("user_id", Operator.Equals, me)
                .Get();
            return result?.Models?.FirstOrDefault() ?? new UserCredit { UserId = me, Balance = 0, LifetimeEarned = 0 };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreditService] GetMyCredits error: {ex.Message}");
            return new UserCredit { UserId = me, Balance = 0, LifetimeEarned = 0 };
        }
    }

    public async Task<List<CreditTransaction>> GetMyTransactionsAsync(int limit = 30)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return [];
        try
        {
            var result = await supabase.From<CreditTransaction>()
                .Filter("user_id", Operator.Equals, me)
                .Order(t => t.CreatedAt, Ordering.Descending)
                .Limit(limit)
                .Get();
            return result?.Models ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreditService] GetTransactions error: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> AddMyCreditsAsync(int amount, string reason, string? entityId = null, string? entityType = null)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return false;
        return await AddCreditsAsync(me, amount, reason, entityId, entityType);
    }

    public async Task<bool> AddCreditsAsync(string userId, int amount, string reason, string? entityId = null, string? entityType = null)
    {
        if (string.IsNullOrWhiteSpace(userId) || amount == 0) return false;
        try
        {
            await supabase.Rpc("add_user_credits", new
            {
                p_user_id = userId,
                p_amount = amount,
                p_reason = reason,
                p_entity_id = entityId,
                p_entity_type = entityType
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreditService] RPC add_user_credits error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddMyCreditsByRuleAsync(string ruleKey, string? entityId = null, string? entityType = null, int fallbackAmount = 0)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return false;
        return await AddCreditsByRuleAsync(me, ruleKey, entityId, entityType, fallbackAmount);
    }

    public async Task<bool> AddCreditsByRuleAsync(string userId, string ruleKey, string? entityId = null, string? entityType = null, int fallbackAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(ruleKey)) return false;
        try
        {
            await supabase.Rpc("apply_point_rule", new
            {
                p_user_id = userId,
                p_rule_key = ruleKey,
                p_entity_id = entityId,
                p_entity_type = entityType
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreditService] RPC apply_point_rule error: {ex.Message}");
            if (fallbackAmount != 0)
                return await AddCreditsAsync(userId, fallbackAmount, ruleKey, entityId, entityType);
            return false;
        }
    }

    public async Task<bool> ReverseMyCreditsForEntityAsync(string originalRuleKey, string entityId, string entityType, string reversalRuleKey)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me)) return false;
        return await ReverseCreditsForEntityAsync(me, originalRuleKey, entityId, entityType, reversalRuleKey);
    }

    public async Task<bool> ReverseCreditsForEntityAsync(string userId, string originalRuleKey, string entityId, string entityType, string reversalRuleKey)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(originalRuleKey) || string.IsNullOrWhiteSpace(entityId))
            return false;

        try
        {
            await supabase.Rpc("reverse_credit_transaction_for_entity", new
            {
                p_user_id = userId,
                p_original_rule_key = originalRuleKey,
                p_entity_id = entityId,
                p_entity_type = entityType,
                p_reversal_rule_key = reversalRuleKey
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreditService] RPC reverse_credit_transaction_for_entity error: {ex.Message}");
            return false;
        }
    }

}
