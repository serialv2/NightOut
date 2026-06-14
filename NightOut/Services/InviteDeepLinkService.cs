using System.Text.RegularExpressions;

namespace NightOut.Services;

public class InviteDeepLinkService
{
    private readonly IAuthService _auth;
    private readonly IFriendInviteService _invites;

    private const string PendingInviteKey = "pending_invite_code";

    public InviteDeepLinkService(
        IAuthService auth,
        IFriendInviteService invites)
    {
        _auth = auth;
        _invites = invites;
    }

    public string? ExtractInviteCode(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = Regex.Match(
            url,
            @"(?:nightout://invite/|https://nightout\.app/invite/)([A-Z0-9]{6,12})",
            RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups[1].Value.ToUpperInvariant()
            : null;
    }

    public async Task HandleUrlAsync(string? url)
    {
        var code = ExtractInviteCode(url);

        if (string.IsNullOrWhiteSpace(code))
            return;

        Microsoft.Maui.Storage.Preferences.Set(
            PendingInviteKey,
            code);

        if (_auth.IsLoggedIn)
            await ProcessPendingInviteAsync();
    }

    public async Task ProcessPendingInviteAsync()
    {
        var code = Microsoft.Maui.Storage.Preferences.Get(
            PendingInviteKey,
            string.Empty);

        if (string.IsNullOrWhiteSpace(code))
            return;

        if (!_auth.IsLoggedIn)
            return;

        var success = await _invites.UseInviteAsync(code);

        if (!success)
        {
            Microsoft.Maui.Storage.Preferences.Remove(
                PendingInviteKey);

            return;
        }

        Microsoft.Maui.Storage.Preferences.Remove(
            PendingInviteKey);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var page = Application.Current?
                    .Windows
                    .FirstOrDefault()?
                    .Page;

                if (page != null)
                {
                    await page.DisplayAlert(
                        "Invitation acceptée 🎉",
                        "L'ami a bien été ajouté.",
                        "OK");
                }
            }
            catch
            {
            }
        });
    }
}