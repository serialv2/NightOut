using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Services;
using NightOut.ViewModels.Base;
using NightOut.Views.Auth;

namespace NightOut.ViewModels;

public partial class SettingsViewModel(
    IProfileService profileService,
    IAuthService authService,
    IServiceProvider services) : BaseViewModel
{
    [ObservableProperty]
    private string _appVersion = AppInfo.Current.VersionString;

    [ObservableProperty]
    private string _buildNumber = AppInfo.Current.BuildString;

    public string VersionLabel => $"Spotiz {AppVersion} ({BuildNumber})";

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation.NavigationStack.Count > 1)
            await Application.Current.Windows[0].Page!.Navigation.PopAsync();
        else
            await Shell.Current.GoToAsync("//ProfilePage");
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync()
    {
        await Launcher.OpenAsync("https://spotiz.fr/confidentialite");
    }

    [RelayCommand]
    private async Task OpenTermsAsync()
    {
        await Launcher.OpenAsync("https://spotiz.fr/conditions");
    }

    [RelayCommand]
    private async Task ContactSupportAsync()
    {
        var subject = Uri.EscapeDataString("Support Spotiz");
        var body = Uri.EscapeDataString("Bonjour,\n\nJ'ai besoin d'aide concernant mon compte Spotiz.\n\n");
        await Launcher.OpenAsync($"mailto:support@spotiz.fr?subject={subject}&body={body}");
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
            return;

        var confirm = await page.DisplayAlert(
            "Déconnexion",
            "Tu veux vraiment te déconnecter de Spotiz ?",
            "Se déconnecter",
            "Annuler");

        if (!confirm)
            return;

        IsBusy = true;
        try
        {
            await profileService.SignOutAsync();
            await authService.SignOutAsync();

            var loginPage = services.GetRequiredService<LoginPage>();
            Application.Current!.Windows[0].Page = new NavigationPage(loginPage)
            {
                BarBackgroundColor = Color.FromArgb("#0A1018"),
                BarTextColor = Color.FromArgb("#F2E8D5")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsVM] SignOut erreur : {ex}");
            await ShowToastAsync("Erreur lors de la déconnexion.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
