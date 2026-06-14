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
}
