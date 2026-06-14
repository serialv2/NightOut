using NightOut.Models;

namespace NightOut.Services;

public interface IGooglePlacesService
{
    string LastErrorMessage { get; }

    // Conservé pour compatibilité avec l'ancien ViewModel.
    // Le formulaire Pro utilise maintenant surtout GetAddressDetailsFromTextAsync.
    Task<List<GooglePlacePrediction>> SearchAsync(string query);

    // Conservé pour compatibilité avec l'ancien ViewModel.
    Task<GooglePlaceDetails?> GetPlaceDetailsAsync(string placeId);

    // Utilisé quand l'utilisateur remplit le formulaire manuel :
    // numéro + rue + code postal + ville + pays.
    // Google Geocoding vérifie l'adresse et renvoie les coordonnées GPS.
    Task<GooglePlaceDetails?> GetAddressDetailsFromTextAsync(string address);
}
