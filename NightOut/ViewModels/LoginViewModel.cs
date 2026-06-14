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

    private bool CanLogin =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        Password.Length >= 6 &&
        !IsBusy;

    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();

        if (auth.IsLoggedIn)
        {
            await OpenAppAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        await RunAsync(async () =>
        {
            var profile = await auth.SignInAsync(Email.Trim(), Password);

            if (profile == null)
            {
                await ShowToastAsync("E-mail ou mot de passe incorrect");
                return;
            }

            await OpenAppAsync();

        }, "Connexion impossible, réessaie");
    }

    private async Task OpenAppAsync()
    {
        var shell = services.GetRequiredService<AppShell>();

        Application.Current!.Windows[0].Page = shell;

        heartbeat.Start();

        await pushNotifications.InitializeAsync();
        await inviteDeepLinks.ProcessPendingInviteAsync();
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        var registerPage = services.GetRequiredService<RegisterPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(registerPage);
    }

    [RelayCommand]
    private async Task GoToForgotPasswordAsync()
    {
        var forgotPage = services.GetRequiredService<ForgotPasswordPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(forgotPage);
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        await RunAsync(async () =>
        {
            var profile = await auth.SignInWithGoogleAsync();

            if (profile == null)
            {
                await ShowToastAsync("Connexion Google annulée ou impossible");
                return;
            }

            await OpenAppAsync();

        }, "Connexion Google impossible");
    }

    [RelayCommand]
    private void TogglePasswordVisibility() =>
        IsPasswordVisible = !IsPasswordVisible;
}