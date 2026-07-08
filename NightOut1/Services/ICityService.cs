using NightOut.Models;

namespace NightOut.Services;

public interface ICityService
{
    Task<List<City>> GetActiveCitiesAsync();
    Task<City?> GetCityByIdAsync(string id);
}
