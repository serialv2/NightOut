using NightOut.Services;
using NightOut.ViewModels;
using NightOut.Views.Auth;
using NightOut.Views.Profile;

namespace NightOut.Views.Pro;

public partial class ProDashboardPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly IServiceProvider _services;

    public ProDashboardPage(
        ProDashboardViewModel vm,
        IAuthService authService,
        IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _authService = authService;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ProDashboardViewModel vm)
            _ = vm.OnAppearingAsync();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnProMenuTapped(object sender, TappedEventArgs e)
    {
        var action = await DisplayActionSheet(
            "Espace Pro",
            "Annuler",
            null,
            "👤 Mon profil",
            "⚙️ Paramètres",
            "🚪 Se déconnecter");

        switch (action)
        {
            case "👤 Mon profil":
                await Shell.Current.GoToAsync("..");
                break;

            case "⚙️ Paramètres":
                await OpenSettingsAsync();
                break;

            case "🚪 Se déconnecter":
                await SignOutAsync();
                break;
        }
    }

    private async void OnSignOutTapped(object sender, TappedEventArgs e)
    {
        await SignOutAsync();
    }

    private async Task OpenSettingsAsync()
    {
        try
        {
            var settingsPage = _services.GetRequiredService<SettingsPage>();
            await Navigation.PushAsync(settingsPage);
        }
        catch
        {
            try
            {
                await Shell.Current.GoToAsync("SettingsPage");
            }
            catch
            {
                await DisplayAlert(
                    "Paramètres",
                    "La page Paramètres n'est pas encore enregistrée dans la navigation.",
                    "OK");
            }
        }
    }

    private async Task SignOutAsync()
    {
        var confirm = await DisplayAlert(
            "Déconnexion",
            "Voulez-vous vraiment vous déconnecter de Spotiz ?",
            "Se déconnecter",
            "Annuler");

        if (!confirm)
            return;

        try
        {
            await _authService.SignOutAsync();

            var loginPage = _services.GetRequiredService<LoginPage>();
            Application.Current!.Windows[0].Page = new NavigationPage(loginPage)
            {
                BarBackgroundColor = ThemeColor("BgDeep", "#F4EFE6"),
                BarTextColor = ThemeColor("TextPrimary", "#3D2817")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardPage] SignOut erreur : {ex}");
            await DisplayAlert(
                "Déconnexion impossible",
                "Une erreur est survenue pendant la déconnexion. Réessaie dans quelques instants.",
                "OK");
        }
    }

    private static Color ThemeColor(string key, string fallbackHex)
    {
        var resources = Application.Current?.Resources;
        return resources != null &&
               resources.TryGetValue(key, out var value) &&
               value is Color color
            ? color
            : Color.FromArgb(fallbackHex);
    }
}
