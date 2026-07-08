using System.Text.Json;
using NightOut.Models;

namespace NightOut.Services;

public class GooglePlacesService : IGooglePlacesService
{
    private readonly HttpClient _httpClient;

    // Clé Google utilisée par le projet NightOut.
    // Pour cette version, il faut surtout activer : Geocoding API.
    private const string ApiKey = "AIzaSyBNl6_0IHYGTy9zMaaDPPtMUAuspRZz6z0" +
        "";

    public string LastErrorMessage { get; private set; } = string.Empty;

    public GooglePlacesService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<GooglePlacePrediction>> SearchAsync(string query)
    {
        // L'ancien système Places Autocomplete / FindPlaceFromText utilisait des API legacy.
        // Pour éviter l'erreur REQUEST_DENIED Legacy API, on ne l'utilise plus ici.
        LastErrorMessage = "La recherche automatique Places est désactivée. Utilise la vérification par formulaire.";
        return Task.FromResult(new List<GooglePlacePrediction>());
    }

    public Task<GooglePlaceDetails?> GetPlaceDetailsAsync(string placeId)
    {
        // Conservé uniquement pour que les anciens bindings/commandes compilent.
        LastErrorMessage = "La récupération par place_id est désactivée. Utilise la vérification par formulaire.";
        return Task.FromResult<GooglePlaceDetails?>(null);
    }

    public async Task<GooglePlaceDetails?> GetAddressDetailsFromTextAsync(string address)
    {
        LastErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(address))
        {
            LastErrorMessage = "Adresse vide.";
            return null;
        }

        var query = NormalizeAddressQuery(address);

        // 1) Essai principal : adresse complète + région France.
        var details = await TryGeocodeAsync(query, withCountryComponent: false);
        if (details is not null)
            return details;

        var firstError = LastErrorMessage;

        // 2) Essai plus strict : restriction pays France.
        details = await TryGeocodeAsync(query, withCountryComponent: true);
        if (details is not null)
            return details;

        if (string.IsNullOrWhiteSpace(LastErrorMessage))
            LastErrorMessage = firstError;

        if (string.IsNullOrWhiteSpace(LastErrorMessage))
            LastErrorMessage = "Google n'a renvoyé aucun résultat pour cette adresse.";

        return null;
    }

    private async Task<GooglePlaceDetails?> TryGeocodeAsync(string query, bool withCountryComponent)
    {
        var url =
            $"https://maps.googleapis.com/maps/api/geocode/json" +
            $"?address={Uri.EscapeDataString(query)}" +
            (withCountryComponent ? "&components=country:FR" : string.Empty) +
            "&region=fr" +
            "&language=fr" +
            $"&key={ApiKey}";

        using var doc = await GetJsonDocumentAsync(
            url,
            withCountryComponent ? "Geocoding avec pays France" : "Geocoding");

        if (doc is null)
            return null;

        var status = GetStatus(doc.RootElement);

        if (status != "OK")
            return null;

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            LastErrorMessage = "Google Geocoding a répondu OK mais sans résultat.";
            return null;
        }

        return ParseDetailsFromResult(results[0]);
    }

    private async Task<JsonDocument?> GetJsonDocumentAsync(string url, string source)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                LastErrorMessage = $"Google {source} HTTP {(int)response.StatusCode} : {response.ReasonPhrase}";
                System.Diagnostics.Debug.WriteLine($"[GooglePlacesService] {source} http={(int)response.StatusCode} json={json}");
                return null;
            }

            var doc = JsonDocument.Parse(json);
            var status = GetStatus(doc.RootElement);
            var errorMessage = GetGoogleErrorMessage(doc.RootElement);

            if (status == "OK")
            {
                System.Diagnostics.Debug.WriteLine($"[GooglePlacesService] {source} status=OK");
                return doc;
            }

            if (status == "ZERO_RESULTS")
            {
                LastErrorMessage = $"Google {source} : ZERO_RESULTS. Adresse envoyée : {BuildSafeUrlForDebug(url)}";
                System.Diagnostics.Debug.WriteLine($"[GooglePlacesService] {source} status=ZERO_RESULTS");
                return doc;
            }

            LastErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? $"Google {source} : {status}"
                : $"Google {source} : {status} - {errorMessage}";

            System.Diagnostics.Debug.WriteLine($"[GooglePlacesService] {source} status={status} error={errorMessage}");
            return doc;
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Erreur appel Google {source} : {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[GooglePlacesService] {source} exception={ex}");
            return null;
        }
    }

    private static string GetStatus(JsonElement root)
    {
        return root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetGoogleErrorMessage(JsonElement root)
    {
        return root.TryGetProperty("error_message", out var errorElement)
            ? errorElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeAddressQuery(string value)
    {
        var query = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        while (query.Contains("  ", StringComparison.Ordinal))
            query = query.Replace("  ", " ");

        if (!query.Contains("france", StringComparison.OrdinalIgnoreCase))
            query += ", France";

        return query;
    }

    private static string BuildSafeUrlForDebug(string url)
    {
        var keyIndex = url.IndexOf("&key=", StringComparison.OrdinalIgnoreCase);

        if (keyIndex < 0)
            return url;

        return url[..keyIndex] + "&key=***";
    }

    private GooglePlaceDetails? ParseDetailsFromResult(JsonElement result)
    {
        if (!result.TryGetProperty("address_components", out var components))
        {
            LastErrorMessage = "Google a trouvé un résultat mais sans address_components.";
            return null;
        }

        var details = new GooglePlaceDetails();

        foreach (var component in components.EnumerateArray())
        {
            if (!component.TryGetProperty("types", out var types))
                continue;

            var longName = component.TryGetProperty("long_name", out var longNameElement)
                ? longNameElement.GetString() ?? string.Empty
                : string.Empty;

            foreach (var type in types.EnumerateArray())
            {
                switch (type.GetString())
                {
                    case "street_number":
                        details.StreetNumber = longName;
                        break;

                    case "route":
                        details.StreetName = longName;
                        break;

                    case "postal_code":
                        details.PostalCode = longName;
                        break;

                    case "locality":
                    case "postal_town":
                        details.City = longName;
                        break;

                    case "administrative_area_level_3":
                    case "administrative_area_level_2":
                        if (string.IsNullOrWhiteSpace(details.City))
                            details.City = longName;
                        break;

                    case "country":
                        details.Country = longName;
                        break;
                }
            }
        }

        if (!result.TryGetProperty("geometry", out var geometry) ||
            !geometry.TryGetProperty("location", out var location))
        {
            LastErrorMessage = "Google a trouvé l'adresse mais sans coordonnées GPS.";
            return null;
        }

        details.Latitude = location.TryGetProperty("lat", out var lat)
            ? lat.GetDouble()
            : 0;

        details.Longitude = location.TryGetProperty("lng", out var lng)
            ? lng.GetDouble()
            : 0;

        if (details.Latitude == 0 && details.Longitude == 0)
        {
            LastErrorMessage = "Google a renvoyé des coordonnées GPS invalides.";
            return null;
        }

        details.Country = string.IsNullOrWhiteSpace(details.Country) ? "France" : details.Country;

        return details;
    }
}
