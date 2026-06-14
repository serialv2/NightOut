using NightOut.Models;

namespace NightOut.Services;

public interface IProfileService
{
    /// <summary>Charge le profil de l'utilisateur connecté.</summary>
    Task<Profile?> GetCurrentProfileAsync();

    /// <summary>Met à jour les champs éditables du profil.</summary>
    Task<bool> UpdateProfileAsync(Profile profile);

    /// <summary>
    /// Compresse et uploade une photo de profil vers le bucket 'avatars'.
    /// Retourne l'URL publique, ou null en cas d'erreur.
    /// </summary>
    Task<string?> UploadAvatarAsync(byte[] imageData, string userId);

    /// <summary>Charge le score de fiabilité NightOut de l'utilisateur connecté.</summary>
    Task<UserEventReliability?> GetMyEventReliabilityAsync();

    /// <summary>Déconnecte l'utilisateur (Supabase GoTrue).</summary>
    Task SignOutAsync();
}
