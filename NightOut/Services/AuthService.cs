using Microsoft.Maui.Authentication;
using NightOut.Models;
using Supabase;

namespace NightOut.Services;

public class AuthService(Client supabase) : IAuthService
{
    private const string SavedEmailKey = "nightout_saved_email";
    private const string SavedPasswordKey = "nightout_saved_password";
    private const string SavedAccessTokenKey = "nightout_saved_access_token";
    private const string SavedRefreshTokenKey = "nightout_saved_refresh_token";

    private const string GoogleRedirectUri = "nightout://auth-callback";

    private Profile? _currentProfile;

    public bool IsLoggedIn =>
        supabase.Auth.CurrentUser != null;

    public string? GetCurrentUserId() =>
        supabase.Auth.CurrentUser?.Id;

    public async Task<bool> RestoreSessionAsync()
    {
        if (IsLoggedIn)
        {
            _currentProfile = await GetCurrentProfileAsync();
            return true;
        }

        // 1) Tentative de restauration via tokens Supabase OAuth / Google
        try
        {
            var accessToken = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(SavedAccessTokenKey);
            var refreshToken = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(SavedRefreshTokenKey);

            if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                var session = await supabase.Auth.SetSession(accessToken, refreshToken, true);
                await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

                _currentProfile = await GetCurrentProfileAsync();

                if (IsLoggedIn)
                    return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] RestoreSessionAsync tokens erreur : {ex.Message}");
            RemoveSavedTokens();
        }

        // 2) Fallback : restauration email/mot de passe déjà mise en place
        try
        {
            var email = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(SavedEmailKey);
            var password = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(SavedPasswordKey);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            var session = await supabase.Auth.SignIn(email, password);

            if (session?.User == null)
                return false;

            await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

            _currentProfile = await GetCurrentProfileAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] RestoreSessionAsync credentials erreur : {ex.Message}");
            RemoveSavedCredentials();
            return false;
        }
    }

    public async Task<Profile?> SignInAsync(string email, string password)
    {
        var session = await supabase.Auth.SignIn(email, password);
        if (session?.User == null) return null;

        await SaveCredentialsAsync(email, password);
        await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

        _currentProfile = await GetCurrentProfileAsync();
        return _currentProfile;
    }

    public async Task<Profile?> SignUpAsync(string email, string password, string username, string accountType = "user")
    {
        var options = new Supabase.Gotrue.SignUpOptions
        {
            Data = new Dictionary<string, object>
            {
                { "username", username },
                { "account_type", NormalizeAccountType(accountType) },
                { "professional_kind", NormalizeProfessionalKind(accountType) }
            }
        };

        var session = await supabase.Auth.SignUp(email, password, options);
        if (session?.User == null) return null;

        await SaveCredentialsAsync(email, password);
        await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

        // Attendre que le trigger handle_new_user crée le profil
        await Task.Delay(1000);

        _currentProfile = await GetCurrentProfileAsync();

        // Si le profil n'est toujours pas dispo, retourner un profil minimal
        if (_currentProfile == null)
        {
            _currentProfile = new Profile
            {
                Id = session.User.Id,
                Username = username,
                DisplayName = username
            };

            ApplyProfessionalFields(_currentProfile, accountType);
        }

        if (_currentProfile != null)
        {
            ApplyProfessionalFields(_currentProfile, accountType);
            await SaveProfileProfessionalFieldsAsync(_currentProfile);
        }

        return _currentProfile;
    }

    public async Task<Profile?> SignInWithGoogleAsync(string accountType = "user")
    {
        try
        {
            var state = await supabase.Auth.SignIn(
                Supabase.Gotrue.Constants.Provider.Google,
                new Supabase.Gotrue.SignInOptions
                {
                    RedirectTo = GoogleRedirectUri,
                    Scopes = "openid email profile"
                });

            var result = await WebAuthenticator.Default.AuthenticateAsync(
                state.Uri,
                new Uri(GoogleRedirectUri));

            if (!TryGet(result, "access_token", out var accessToken) ||
                !TryGet(result, "refresh_token", out var refreshToken))
            {
                System.Diagnostics.Debug.WriteLine("[Auth] Google : access_token ou refresh_token manquant dans le callback.");
                return null;
            }

            var session = await supabase.Auth.SetSession(accessToken, refreshToken, true);
            if (session?.User == null) return null;

            RemoveSavedCredentials();
            await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

            // Le trigger Supabase doit créer le profil. On lui laisse un court délai.
            await Task.Delay(1000);

            _currentProfile = await GetCurrentProfileAsync();

            // Sécurité : si le trigger n'a pas encore répondu, on crée un profil minimal côté app.
            if (_currentProfile == null)
            {
                var email = session.User.Email ?? string.Empty;
                var username = BuildUsernameFromEmail(email);

                _currentProfile = new Profile
                {
                    Id = session.User.Id,
                    Username = username,
                    DisplayName = username,
                    Language = "fr",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                ApplyProfessionalFields(_currentProfile, accountType);

                try
                {
                    await supabase.From<Profile>().Insert(_currentProfile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Auth] Google : création profil minimale impossible : {ex.Message}");
                }
            }

            if (_currentProfile != null)
            {
                ApplyProfessionalFields(_currentProfile, accountType);
                await SaveProfileProfessionalFieldsAsync(_currentProfile);
            }

            return _currentProfile;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] SignInWithGoogleAsync erreur : {ex}");
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        await supabase.Auth.SignOut();
        _currentProfile = null;

        RemoveSavedCredentials();
        RemoveSavedTokens();
    }

    public async Task<bool> ResetPasswordAsync(string email)
    {
        try
        {
            await supabase.Auth.ResetPasswordForEmail(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Profile?> GetCurrentProfileAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return null;

        if (_currentProfile?.Id == userId) return _currentProfile;

        try
        {
            var result = await supabase
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Single();

            _currentProfile = result;
            return _currentProfile;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeAccountType(string? accountType) =>
        accountType switch
        {
            "establishment" => "establishment",
            "organizer" => "organizer",
            _ => "user"
        };

    private static string? NormalizeProfessionalKind(string? accountType) =>
        accountType switch
        {
            "establishment" => "establishment",
            "organizer" => "organizer",
            _ => null
        };

    private static void ApplyProfessionalFields(Profile profile, string? accountType)
    {
        var normalized = NormalizeAccountType(accountType);
        profile.AccountType = normalized;
        profile.ProfessionalKind = NormalizeProfessionalKind(normalized);

        if (normalized == "user")
        {
            profile.IsPro = false;
            profile.IsVerified = false;
            profile.ProfessionalStatus = "none";
        }
        else
        {
            profile.IsPro = true;
            profile.IsVerified = false;
            profile.ProfessionalStatus = "pending";
        }
    }

    private async Task SaveProfileProfessionalFieldsAsync(Profile profile)
    {
        try
        {
            await supabase.From<Profile>()
                .Where(p => p.Id == profile.Id)
                .Set(p => p.AccountType, profile.AccountType)
                .Set(p => p.ProfessionalKind, profile.ProfessionalKind)
                .Set(p => p.ProfessionalStatus, profile.ProfessionalStatus)
                .Set(p => p.IsPro, profile.IsPro)
                .Set(p => p.IsVerified, profile.IsVerified)
                .Update();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] SaveProfileProfessionalFieldsAsync erreur : {ex.Message}");
        }
    }

    private static async Task SaveCredentialsAsync(string email, string password)
    {
        try
        {
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(SavedEmailKey, email);
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(SavedPasswordKey, password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] SaveCredentialsAsync erreur : {ex.Message}");
        }
    }

    private static async Task SaveSessionTokensAsync(string? accessToken, string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            return;

        try
        {
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(SavedAccessTokenKey, accessToken);
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(SavedRefreshTokenKey, refreshToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] SaveSessionTokensAsync erreur : {ex.Message}");
        }
    }

    private static void RemoveSavedCredentials()
    {
        try
        {
            Microsoft.Maui.Storage.SecureStorage.Default.Remove(SavedEmailKey);
            Microsoft.Maui.Storage.SecureStorage.Default.Remove(SavedPasswordKey);
        }
        catch
        {
        }
    }

    private static void RemoveSavedTokens()
    {
        try
        {
            Microsoft.Maui.Storage.SecureStorage.Default.Remove(SavedAccessTokenKey);
            Microsoft.Maui.Storage.SecureStorage.Default.Remove(SavedRefreshTokenKey);
        }
        catch
        {
        }
    }

    private static bool TryGet(WebAuthenticatorResult result, string key, out string value)
    {
        value = string.Empty;

        if (result.Properties.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw;
            return true;
        }

        return false;
    }

    private static string BuildUsernameFromEmail(string email)
    {
        var baseName = string.IsNullOrWhiteSpace(email)
            ? "nightout_user"
            : email.Split('@')[0];

        var cleaned = new string(baseName
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.')
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "nightout_user";

        return $"{cleaned}_{Random.Shared.Next(1000, 9999)}";
    }
}
