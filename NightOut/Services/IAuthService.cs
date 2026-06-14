using NightOut.Models;

namespace NightOut.Services;

public interface IAuthService
{
    Task<bool> RestoreSessionAsync();
    Task<Profile?> SignInAsync(string email, string password);
    Task<Profile?> SignUpAsync(string email, string password, string username, string accountType = "user");
    Task<Profile?> SignInWithGoogleAsync(string accountType = "user");
    Task SignOutAsync();
    Task<bool> ResetPasswordAsync(string email);
    Task<Profile?> GetCurrentProfileAsync();
    string? GetCurrentUserId();
    bool IsLoggedIn { get; }
}
