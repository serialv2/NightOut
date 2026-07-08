using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Services;
using NightOut.ViewModels.Base;
using NightOut.Views.Auth;

namespace NightOut.ViewModels;

public partial class LoginViewModel(
    IAuthService auth,
    IServiceProvider services,
    InviteDeepLinkService inviteDeepLinks,
    HeartbeatService heartbeat,
    BeaconAutoCheckinService beaconAutoCheckin,
    IPushNotificationService pushNotifications) : BaseViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private string _welcomeText = "Connecte-toi pour retrouver tes amis ce soir";

    protected override TimeSpan NetworkTimeout => TimeSpan.FromSeconds(25);

    private bool CanLogin =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        Password.Length >= 6 &&
        !IsBusy;

    partial void OnEmailChanged(string value) => ClearAuthError();
    partial void OnPasswordChanged(string value) => ClearAuthError();

    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();

        if (auth.IsLoggedIn)
            await OpenAppAsync();
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        var validation = AuthErrorManager.ValidateLogin(Email, Password);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            SetAuthError(validation);
            return;
        }

        await RunAsync(async () =>
        {
            var result = await auth.SignInWithResultAsync(Email, Password);

            if (!result.IsSuccess || result.Profile == null)
            {
                SetAuthError(result.UserMessage);
                return;
            }

            if (result.Profile.IsBanned)
            {
                await auth.SignOutAsync();
                SetAuthError(string.IsNullOrWhiteSpace(result.Profile.BanReason)
                    ? "Ce compte a été banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await OpenAppAsync();
        }, "Connexion impossible. Vérifie ta connexion puis réessaie.");
    }

    private async Task OpenAppAsync()
    {
        var profile = await auth.GetCurrentProfileAsync();
        if (profile?.IsBanned == true)
        {
            await auth.SignOutAsync();
            SetAuthError(string.IsNullOrWhiteSpace(profile.BanReason)
                ? "Ce compte a été banni de Spotiz."
                : profile.BanReason);
            return;
        }

        var shell = services.GetRequiredService<AppShell>();
        Application.Current!.Windows[0].Page = shell;

        heartbeat.Start();
        beaconAutoCheckin.Start();

        await pushNotifications.InitializeAsync();
        await inviteDeepLinks.ProcessPendingInviteAsync();
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        ClearAuthError();
        var registerPage = services.GetRequiredService<RegisterPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(registerPage);
    }

    [RelayCommand]
    private async Task GoToForgotPasswordAsync()
    {
        ClearAuthError();
        var forgotPage = services.GetRequiredService<ForgotPasswordPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(forgotPage);
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        await RunAsync(async () =>
        {
            var result = await auth.SignInWithGoogleResultAsync();

            if (result.IsCancelled)
                return;

            if (!result.IsSuccess || result.Profile == null)
            {
                SetAuthError(result.UserMessage);
                return;
            }

            if (result.Profile.IsBanned)
            {
                await auth.SignOutAsync();
                SetAuthError(string.IsNullOrWhiteSpace(result.Profile.BanReason)
                    ? "Ce compte a été banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await OpenAppAsync();
        }, "Connexion Google impossible. Vérifie ta connexion puis réessaie.");
    }

    [RelayCommand]
    private void TogglePasswordVisibility() =>
        IsPasswordVisible = !IsPasswordVisible;

    private void SetAuthError(string? message)
    {
        HasError = true;
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Une erreur est survenue. Réessaie dans quelques instants."
            : message;
    }

    private void ClearAuthError()
    {
        if (!HasError) return;
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
