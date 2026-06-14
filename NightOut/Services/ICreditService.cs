using NightOut.Models;

namespace NightOut.Services;

public interface ICreditService
{
    Task<UserCredit?> GetMyCreditsAsync();
    Task<List<CreditTransaction>> GetMyTransactionsAsync(int limit = 30);
    Task<bool> AddCreditsAsync(string userId, int amount, string reason, string? entityId = null, string? entityType = null);
    Task<bool> AddMyCreditsAsync(int amount, string reason, string? entityId = null, string? entityType = null);
}
