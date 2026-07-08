using NightOut.Models;

namespace NightOut.Services;

public interface IBarCategoryService
{
    Task<List<BarCategory>> GetActiveCategoriesAsync();
    Task<BarCategory?> GetBySlugAsync(string? slug);
}
