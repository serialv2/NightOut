using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NightOut.Models;

namespace NightOut.Services
{
    public class GeocodingService : IGeocodingService
    {
        // Token public Mapbox (déjà exposé dans map.html). À centraliser dans une
        // classe de constantes si tu en as une, pour ne pas le dupliquer.
        private const string MapboxToken =
            "pk.eyJ1Ijoic2VyaWFsdiIsImEiOiJjbXBsOTB1NGswcHE4MnNyODY1NGpscmp0In0.pgg7IK2dsZnz6V0vN0wffw";

        private static readonly HttpClient _http = new();

        public async Task<List<GeocodeResult>> SearchAsync(string query, double? proximityLng = null, double? proximityLat = null)
        {
            var results = new List<GeocodeResult>();
            if (string.IsNullOrWhiteSpace(query))
                return results;

            var url =
                $"https://api.mapbox.com/geocoding/v5/mapbox.places/{Uri.EscapeDataString(query)}.json" +
                $"?access_token={MapboxToken}&autocomplete=true&country=fr&language=fr&limit=5";

            if (proximityLng.HasValue && proximityLat.HasValue)
                url += $"&proximity={proximityLng.Value.ToString(CultureInfo.InvariantCulture)},{proximityLat.Value.ToString(CultureInfo.InvariantCulture)}";

            try
            {
                var json = await _http.GetStringAsync(url);
                var root = JObject.Parse(json);

                if (root["features"] is not JArray features)
                    return results;

                foreach (var f in features)
                {
                    if (f["center"] is not JArray center || center.Count < 2)
                        continue;

                    results.Add(new GeocodeResult
                    {
                        PlaceName = f["place_name"]?.ToString(),
                        Longitude = center[0].Value<double>(),
                        Latitude = center[1].Value<double>()
                    });
                }
            }
            catch
            {
                // réseau / parsing : on renvoie une liste vide, le ViewModel affiche le message.
            }

            return results;
        }
    }
}