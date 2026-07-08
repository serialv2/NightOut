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

    private const string GoogleRedirectUri = "spotiz://auth-callback";

    private Profile? _currentProfile;

    public bool IsLoggedIn => supabase.Auth.CurrentUser != null;

    public string? GetCurrentUserId() => supabase.Auth.CurrentUser?.Id;

    public async Task<bool> RestoreSessionAsync()
    {
        if (IsLoggedIn)
        {
            _currentProfile = await GetCurrentProfileAsync();
            return true;
        }

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

    // ─────────────────────────────────────────────────────────────
    // Anciennes signatures conservées
    // ─────────────────────────────────────────────────────────────

    public async Task<Profile?> SignInAsync(string email, string password)
    {
        var result = await SignInWithResultAsync(email, password);
        return result.Profile;
    }

    public async Task<Profile?> SignUpAsync(string email, string password, string username, string accountType = "user")
    {
        var result = await SignUpWithResultAsync(email, password, username, accountType);
        return result.Profile;
    }

    public async Task<Profile?> SignInWithGoogleAsync(string accountType = "user")
    {
        var result = await SignInWithGoogleResultAsync(accountType);
        return result.Profile;
    }

    // ─────────────────────────────────────────────────────────────
    // Nouvelles méthodes avec gestion d'erreur pro
    // ─────────────────────────────────────────────────────────────

    public async Task<AuthOperationResult> SignInWithResultAsync(string email, string password)
    {
        var validation = AuthErrorManager.ValidateLogin(email, password);
        if (!string.IsNullOrWhiteSpace(validation))
            return AuthOperationResult.Fail("validation_error", validation);

        try
        {
            var normalizedEmail = AuthErrorManager.NormalizeEmail(email);
            var session = await supabase.Auth.SignIn(normalizedEmail, password);

            if (session?.User == null)
            {
                return AuthOperationResult.Fail(
                    "empty_session",
                    "Connexion impossible. Verifie tes identifiants puis reessaie.");
            }

            await SaveCredentialsAsync(normalizedEmail, password);
            await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

            _currentProfile = await GetCurrentProfileAsync();

            if (_currentProfile == null)
            {
                var repaired = await EnsureProfileExistsAsync(session.User.Id, normalizedEmail, null, "user");
                if (repaired == null)
                {
                    await SignOutAsync();
                    return AuthOperationResult.Fail(
                        "profile_missing",
                        "Ton compte existe, mais ton profil Spotiz est incomplet. Reessaie dans quelques secondes ou contacte le support.");
                }

                _currentProfile = repaired;
            }

            return AuthOperationResult.Success(_currentProfile);
        }
        catch (Exception ex)
        {
            return AuthErrorManager.FromException(ex, "signin");
        }
    }

    public async Task<AuthOperationResult> SignUpWithResultAsync(string email, string password, string username, string accountType = "user")
    {
        var validation = AuthErrorManager.ValidateRegister(username, email, password, password, true);
        if (!string.IsNullOrWhiteSpace(validation))
            return AuthOperationResult.Fail("validation_error", validation);

        try
        {
            var normalizedEmail = AuthErrorManager.NormalizeEmail(email);
            var cleanUsername = CleanUsername(username);
            var normalizedAccountType = NormalizeAccountType(accountType);

            var options = new Supabase.Gotrue.SignUpOptions
            {
                Data = new Dictionary<string, object?>
                {
                    { "username", cleanUsername },
                    { "account_type", normalizedAccountType },
                    { "professional_kind", NormalizeProfessionalKind(normalizedAccountType) }
                }
            };

            var session = await supabase.Auth.SignUp(normalizedEmail, password, options);

            if (session?.User != null &&
                (string.IsNullOrWhiteSpace(session.AccessToken) || string.IsNullOrWhiteSpace(session.RefreshToken)))
            {
                _currentProfile = null;
                RemoveSavedCredentials();
                RemoveSavedTokens();
                await TrySignOutAfterPendingConfirmationAsync();
                return AuthOperationResult.EmailConfirmationRequired(normalizedEmail);
            }

            if (session?.User == null)
            {
                _currentProfile = null;
                RemoveSavedCredentials();
                RemoveSavedTokens();
                await TrySignOutAfterPendingConfirmationAsync();
                return AuthOperationResult.Fail(
                    "empty_signup_session",
                    "Le compte n'a pas pu etre cree. Verifie les reglages email Supabase/Brevo puis reessaie.");
            }

            await SaveCredentialsAsync(normalizedEmail, password);
            await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

            // Le trigger handle_new_user peut prendre un peu de temps.
            _currentProfile = await WaitForProfileAsync(session.User.Id, TimeSpan.FromSeconds(4));

            // Sécurité : si le trigger Supabase n'a pas créé le profil, l'app le répare.
            _currentProfile ??= await EnsureProfileExistsAsync(session.User.Id, normalizedEmail, cleanUsername, normalizedAccountType);

            if (_currentProfile == null)
            {
                return AuthOperationResult.Fail(
                    "profile_creation_failed",
                    "Ton compte a ete cree, mais le profil Spotiz n'a pas pu etre finalise. Ferme puis relance l'application.");
            }

            ApplyProfessionalFields(_currentProfile, normalizedAccountType);
            await SaveProfileProfessionalFieldsAsync(_currentProfile);

            return AuthOperationResult.Success(_currentProfile);
        }
        catch (Exception ex)
        {
            return AuthErrorManager.FromException(ex, "signup");
        }
    }

    public async Task<AuthOperationResult> SignInWithGoogleResultAsync(string accountType = "user")
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
                return AuthOperationResult.Fail(
                    "google_missing_tokens",
                    "Connexion Google impossible. Aucun jeton de connexion recu.");
            }

            var session = await supabase.Auth.SetSession(accessToken, refreshToken, true);
            if (session?.User == null)
            {
                return AuthOperationResult.Fail(
                    "google_empty_session",
                    "Connexion Google impossible. Reessaie dans quelques instants.");
            }

            RemoveSavedCredentials();
            await SaveSessionTokensAsync(session.AccessToken, session.RefreshToken);

            var email = session.User.Email ?? string.Empty;
            var normalizedAccountType = NormalizeAccountType(accountType);

            _currentProfile = await WaitForProfileAsync(session.User.Id, TimeSpan.FromSeconds(4));
            _currentProfile ??= await EnsureProfileExistsAsync(session.User.Id, email, null, normalizedAccountType);

            if (_currentProfile == null)
            {
                return AuthOperationResult.Fail(
                    "google_profile_creation_failed",
                    "Connexion Google reussie, mais le profil Spotiz n'a pas pu etre finalise.");
            }

            ApplyProfessionalFields(_currentProfile, normalizedAccountType);
            await SaveProfileProfessionalFieldsAsync(_currentProfile);

            return AuthOperationResult.Success(_currentProfile);
        }
        catch (TaskCanceledException)
        {
            return AuthOperationResult.Cancelled("Connexion Google annulee.");
        }
        catch (Exception ex)
        {
            return AuthErrorManager.FromException(ex, "google_auth");
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
            if (!AuthErrorManager.IsValidEmail(email))
                return false;

            await supabase.Auth.ResetPasswordForEmail(AuthErrorManager.NormalizeEmail(email));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] ResetPasswordAsync erreur : {ex.Message}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] GetCurrentProfileAsync profil introuvable : {ex.Message}");
            return null;
        }
    }

    private async Task<Profile?> WaitForProfileAsync(string userId, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            try
            {
                var profile = await supabase
                    .From<Profile>()
                    .Where(p => p.Id == userId)
                    .Single();

                if (profile != null)
                    return profile;
            }
            catch
            {
                // Le trigger peut ne pas avoir terminé. On réessaie.
            }

            await Task.Delay(500);
        }

        return null;
    }

    private async Task<Profile?> EnsureProfileExistsAsync(string userId, string? email, string? username, string accountType)
    {
        try
        {
            var existing = await supabase
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Single();

            if (existing != null)
                return existing;
        }
        catch
        {
            // Profil absent : on tente une création minimale.
        }

        var finalUsername = string.IsNullOrWhiteSpace(username)
            ? BuildUsernameFromEmail(email ?? string.Empty)
            : CleanUsername(username);

        var profile = new Profile
        {
            Id = userId,
            Username = finalUsername,
            DisplayName = finalUsername,
            Language = "fr",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        ApplyProfessionalFields(profile, accountType);

        try
        {
            await supabase.From<Profile>().Insert(profile);
            return profile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] EnsureProfileExistsAsync création impossible : {ex.Message}");

            // Dernier essai : le trigger a peut-être créé le profil entre temps.
            try
            {
                return await supabase
                    .From<Profile>()
                    .Where(p => p.Id == userId)
                    .Single();
            }
            catch
            {
                return null;
            }
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

    private async Task TrySignOutAfterPendingConfirmationAsync()
    {
        try
        {
            await supabase.Auth.SignOut();
        }
        catch
        {
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

    private static string CleanUsername(string username)
    {
        var cleaned = new string(username.Trim()
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned)
            ? $"spotiz_{Random.Shared.Next(1000, 9999)}"
            : cleaned;
    }

    private static string BuildUsernameFromEmail(string email)
    {
        var baseName = string.IsNullOrWhiteSpace(email)
            ? "spotiz_user"
            : email.Split('@')[0];

        var cleaned = new string(baseName
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.')
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "spotiz_user";

        return $"{cleaned}_{Random.Shared.Next(1000, 9999)}";
    }
}
