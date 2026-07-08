using System.Collections.Generic;
using System.Threading.Tasks;
using NightOut.Models;

namespace NightOut.Services
{
    public interface IBarService
    {
        Task<List<Bar>> GetBarsByCityAsync(string cityId);
        Task<List<Category>> GetActiveCategoriesAsync();
        Task SyncBarCategoriesAsync(string barId, IEnumerable<string> categoryIds);
        Task<List<Bar>> GetBarsNearbyAsync(double latitude, double longitude, double radiusKm = 5);
        Task<List<Bar>> GetAllVisibleBarsAsync();
        Task<Dictionary<string, int>> GetActiveCheckinCountsByBarAsync(IEnumerable<string> barIds);
        Task<List<Bar>> SearchBarsAsync(string query, double? latitude = null, double? longitude = null, int limit = 20);
        Task<Bar>       GetBarByIdAsync(string id);
        Task<Bar>       CreateBarAsync(Bar bar);
        Task<Bar>       UpdateBarAsync(Bar bar);
        Task<bool>      DeleteBarAsync(string id);

        // --- Modération ---
        Task<List<Bar>> GetPendingBarsAsync();
        Task<Bar>       ApproveBarAsync(string id);
        Task<Bar>       RejectBarAsync(string id);
    }
}
