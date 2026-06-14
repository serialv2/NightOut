using NightOut.Models;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class CityService(Client supabase) : ICityService
{
    private List<City>? _cachedCities;

    public async Task<List<City>> GetActiveCitiesAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[CityService] Chargement villes Supabase...");

            var result = await supabase
                .From<City>()
                .Order(c => c.Name, Ordering.Ascending)
                .Get();

            var allCities = result?.Models ?? [];

            System.Diagnostics.Debug.WriteLine($"[CityService] Total reçu Supabase : {allCities.Count}");

            foreach (var city in allCities)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CityService] Ville : {city.Name} | Active={city.IsActive} | Lat={city.Latitude} | Lng={city.Longitude}");
            }

            _cachedCities = allCities
                .Where(c => c.IsActive)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[CityService] Villes actives : {_cachedCities.Count}");

            return _cachedCities;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CityService] ERREUR GetActiveCitiesAsync : {ex}");
            return [];
        }
    }

    public async Task<City?> GetCityByIdAsync(string id)
    {
        var cities = await GetActiveCitiesAsync();
        return cities.FirstOrDefault(c => c.Id == id);
    }
}