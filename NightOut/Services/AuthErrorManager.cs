using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace NightOut.Services;

public static class AuthErrorManager
{
    public static AuthOperationResult FromException(Exception ex, string context)
    {
        var raw = Flatten(ex).ToLowerInvariant();

        if (ex is TaskCanceledException or OperationCanceledException)
        {
            return AuthOperationResult.Fail(
                "timeout",
                "La connexion prend trop de temps. Verifie ta connexion internet puis reessaie.",
                ex.ToString());
        }

        if (ex is HttpRequestException || raw.Contains("network") || raw.Contains("connection") || raw.Contains("connexion") || raw.Contains("name resolution") || raw.Contains("host"))
        {
            return AuthOperationResult.Fail(
                "network_error",
                "Connexion impossible. Verifie ton reseau internet puis reessaie.",
                ex.ToString());
        }

        if (raw.Contains("invalid login credentials") || raw.Contains("invalid_grant") || raw.Contains("email or password") || raw.Contains("login credentials"))
        {
            return AuthOperationResult.Fail(
                "invalid_credentials",
                "Adresse e-mail ou mot de passe incorrect.",
                ex.ToString());
        }

        if (raw.Contains("smtp") || raw.Contains("send email") || raw.Contains("sending confirmation") || raw.Contains("confirmation email") || raw.Contains("email provider"))
        {
            return AuthOperationResult.Fail(
                "email_delivery_error",
                "Le compte n'a pas pu etre cree car l'email de validation n'a pas pu etre envoye. Verifie la configuration SMTP Supabase/Brevo.",
                ex.ToString());
        }

        if (raw.Contains("email not confirmed") || raw.Contains("not confirmed") || raw.Contains("email_confirm"))
        {
            return AuthOperationResult.Fail(
                "email_not_confirmed",
                "Ton e-mail n'est pas encore confirme. Verifie ta boite mail puis reessaie.",
                ex.ToString(),
                needsEmailConfirmation: true);
        }

        if (raw.Contains("user already registered") || raw.Contains("already registered") || raw.Contains("already exists") || raw.Contains("email_exists") || raw.Contains("duplicate") && raw.Contains("users"))
        {
            return AuthOperationResult.Fail(
                "email_already_used",
                "Cette adresse e-mail est deja utilisee. Connecte-toi ou utilise 'mot de passe oublie'.",
                ex.ToString());
        }

        if (raw.Contains("password") && (raw.Contains("weak") || raw.Contains("short") || raw.Contains("length") || raw.Contains("6 characters")))
        {
            return AuthOperationResult.Fail(
                "weak_password",
                "Le mot de passe est trop faible. Utilise au moins 8 caracteres avec lettres et chiffres.",
                ex.ToString());
        }

        if (raw.Contains("rate limit") || raw.Contains("too many") || raw.Contains("429"))
        {
            return AuthOperationResult.Fail(
                "rate_limited",
                "Trop de tentatives. Patiente quelques minutes puis reessaie.",
                ex.ToString());
        }

        if (raw.Contains("username") && (raw.Contains("duplicate") || raw.Contains("unique")))
        {
            return AuthOperationResult.Fail(
                "username_already_used",
                "Ce pseudo est deja pris. Choisis-en un autre.",
                ex.ToString());
        }

        if (raw.Contains("permission denied") || raw.Contains("row-level security") || raw.Contains("violates row-level security"))
        {
            return AuthOperationResult.Fail(
                "rls_error",
                "Le compte a ete cree, mais le profil n'a pas pu etre finalise. Reessaie dans quelques secondes.",
                ex.ToString());
        }

        return AuthOperationResult.Fail(
            $"{context}_unknown_error",
            context == "signup"
                ? "Creation du compte impossible pour le moment. Reessaie dans quelques instants."
                : "Connexion impossible pour le moment. Reessaie dans quelques instants.",
            ex.ToString());
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    }

    public static bool IsValidUsername(string username)
    {
        var value = username.Trim();
        return value.Length is >= 3 and <= 24 && value.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-');
    }

    public static string ValidateLogin(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Entre ton adresse e-mail.";
        if (!IsValidEmail(email)) return "Adresse e-mail invalide.";
        if (string.IsNullOrWhiteSpace(password)) return "Entre ton mot de passe.";
        if (password.Length < 6) return "Le mot de passe doit contenir au moins 6 caracteres.";
        return string.Empty;
    }

    public static string ValidateRegister(string username, string email, string password, string passwordConfirm, bool acceptTerms)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Choisis un pseudo.";
        if (!IsValidUsername(username)) return "Le pseudo doit contenir 3 a 24 caracteres, sans espace.";
        if (string.IsNullOrWhiteSpace(email)) return "Entre ton adresse e-mail.";
        if (!IsValidEmail(email)) return "Adresse e-mail invalide.";
        if (string.IsNullOrWhiteSpace(password)) return "Choisis un mot de passe.";
        if (password.Length < 8) return "Le mot de passe doit contenir au moins 8 caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Utilise au moins une lettre et un chiffre dans ton mot de passe.";
        if (password != passwordConfirm) return "Les deux mots de passe ne correspondent pas.";
        if (!acceptTerms) return "Tu dois accepter les conditions d'utilisation pour creer ton compte.";
        return string.Empty;
    }

    private static string Flatten(Exception ex)
    {
        var messages = new List<string>();
        var current = ex;
        while (current != null)
        {
            messages.Add(current.Message);
            current = current.InnerException!;
        }
        return string.Join(" | ", messages);
    }
}
