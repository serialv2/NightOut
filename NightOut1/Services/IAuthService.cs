using NightOut.Models;

namespace NightOut.Services;

public interface IAuthService
{
    Task<bool> RestoreSessionAsync();

    // Anciennes méthodes conservées pour compatibilité avec le reste du projet.
    Task<Profile?> SignInAsync(string email, string password);
    Task<Profile?> SignUpAsync(string email, string password, string username, string accountType = "user");
    Task<Profile?> SignInWithGoogleAsync(string accountType = "user");

    // Nouvelles méthodes professionnelles : elles retournent une erreur claire et exploitable par l'UI.
    Task<AuthOperationResult> SignInWithResultAsync(string email, string password);
    Task<AuthOperationResult> SignUpWithResultAsync(string email, string password, string username, string accountType = "user");
    Task<AuthOperationResult> SignInWithGoogleResultAsync(string accountType = "user");

    Task SignOutAsync();
    Task<bool> ResetPasswordAsync(string email);
    Task<Profile?> GetCurrentProfileAsync();
    string? GetCurrentUserId();
    bool IsLoggedIn { get; }
}
