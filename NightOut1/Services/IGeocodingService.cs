using System.Collections.Generic;
using System.Threading.Tasks;
using NightOut.Models;

namespace NightOut.Services
{
    public interface IGeocodingService
    {
        // proximity (optionnel) = on biaise les résultats autour de la ville choisie.
        Task<List<GeocodeResult>> SearchAsync(string query, double? proximityLng = null, double? proximityLat = null);
    }
}