using NightOut.Models;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class BarCategoryService(Supabase.Client supabase) : IBarCategoryService
{
    public async Task<List<BarCategory>> GetActiveCategoriesAsync()
    {
        try
        {
            var result = await supabase
                .From<BarCategory>()
                .Order(c => c.SortOrder, Ordering.Ascending)
                .Get();

            var categories = (result?.Models ?? [])
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList();

            return categories.Count > 0
                ? categories
                : BarCategories.Fallback.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarCategoryService] GetActiveCategories erreur : {ex}");
            return BarCategories.Fallback.ToList();
        }
    }

    public async Task<BarCategory?> GetBySlugAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BarCategories.Default;

        var categories = await GetActiveCategoriesAsync();
        return BarCategories.Resolve(slug, categories);
    }
}
