using NightOut.Services;
using NightOut.Views.Auth;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace NightOut;

public partial class App : MauiApp
{
    private readonly IAuthService _auth;
    private readonly IUserStatusService _userStatus;
    private readonly ICheckinService _checkin;
    private readonly HeartbeatService _heartbeat;
    private readonly IServiceProvider _services;
    private readonly InviteDeepLinkService _inviteDeepLinks;
    private readonly IPushNotificationService _pushNotifications;
    private readonly BeaconAutoCheckinService _beaconAutoCheckin;
    private readonly AppShell _shell;
    private readonly IThemeService _theme;
    private static readonly TimeSpan BackgroundGracePeriod = TimeSpan.FromMinutes(10);
    private CancellationTokenSource? _backgroundGraceCts;

    public App(
        IAuthService auth,
        IUserStatusService userStatus,
        ICheckinService checkin,
        HeartbeatService heartbeat,
        InviteDeepLinkService inviteDeepLinks,
        IPushNotificationService pushNotifications,
        BeaconAutoCheckinService beaconAutoCheckin,
        AppShell shell,
        IThemeService theme,
        IServiceProvider services)
    {
        InitializeComponent();

        _theme = theme;
        _theme.Initialize(); // Ajoute ColorsLight ou ColorsDark selon la préférence sauvegardée — DOIT être fait avant tout rendu de page.

        _auth = auth;
        _userStatus = userStatus;
        _checkin = checkin;
        _heartbeat = heartbeat;
        _services = services;
        _inviteDeepLinks = inviteDeepLinks;
        _pushNotifications = pushNotifications;
        _beaconAutoCheckin = beaconAutoCheckin;
        _shell = shell;

        RegisterGlobalExceptionHandlers();

        MainPage = CreateLoadingPage();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await StartAsync();
        });
    }

    private void RegisterGlobalExceptionHandlers()
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine(
                $"[App] ⚠ UnobservedTaskException : {e.Exception}");
            e.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;

            System.Diagnostics.Debug.WriteLine(
                $"[App] 💥 UnhandledException (fatal={e.IsTerminating}) : {ex}");

            if (!e.IsTerminating)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var toast = CommunityToolkit.Maui.Alerts.Toast.Make(
                            "Une erreur inattendue s'est produite.");
                        await toast.Show();
                    }
                    catch
                    {
                    }
                });
            }
        };
    }

    /// <summary>Lit une couleur du thème actif (ColorsLight/ColorsDark) avec repli si non trouvée.</summary>
    private static Color ThemeColor(string key, string fallbackHex)
    {
        if (Current?.Resources.TryGetValue(key, out var value) == true && value is Color c)
            return c;
        return Color.FromArgb(fallbackHex);
    }

    private static Page CreateLoadingPage()
    {
        return new ContentPage
        {
            BackgroundColor = ThemeColor("BgDeep", "#F5F2EE"),
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Spotiz",
                        FontFamily = "PlayfairDisplay-BoldItalic",
                        FontSize = 42,
                        TextColor = ThemeColor("Accent", "#CEA358"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = ThemeColor("Accent", "#CEA358"),
                        WidthRequest = 42,
                        HeightRequest = 42
                    }
                }
            }
        };
    }

    private async Task StartAsync()
    {
        var restored = await _auth.RestoreSessionAsync();

        if (restored || _auth.IsLoggedIn)
        {
            SetRootPage(_shell);

            _heartbeat.Start();
            _beaconAutoCheckin.Start();

            try
            {
                await _userStatus.GoOnlineAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] StartAsync GoOnline erreur : {ex.Message}");
            }

            await _pushNotifications.InitializeAsync();
            await _inviteDeepLinks.ProcessPendingInviteAsync();
            await NotificationNavigationService.ProcessPendingAsync();
            return;
        }

        ShowLoginPage();
    }

    private void ShowLoginPage()
    {
        var loginPage = _services.GetRequiredService<LoginPage>();

        SetRootPage(new NavigationPage(loginPage)
        {
            BarBackgroundColor = ThemeColor("BgDeep", "#F5F2EE"),
            BarTextColor = ThemeColor("TextPrimary", "#37241B")
        });
    }

    private static void SetRootPage(Page page)
    {
        var app = Current;

        if (app?.Windows.Count > 0)
        {
            app.Windows[0].Page = page;
            return;
        }

        app!.MainPage = page;
    }

    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        try
        {
            System.Diagnostics.Debug.WriteLine($"[DeepLink] URL reçue : {uri}");
            await _inviteDeepLinks.HandleUrlAsync(uri.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeepLink] Erreur : {ex}");
        }
    }

    private void ScheduleBackgroundGraceStop()
    {
        CancelBackgroundGraceStop();

        _backgroundGraceCts = new CancellationTokenSource();
        var token = _backgroundGraceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(BackgroundGracePeriod, token);

                if (token.IsCancellationRequested)
                    return;

                _heartbeat.Stop();
                _beaconAutoCheckin.Stop();

                System.Diagnostics.Debug.WriteLine("[App] Background grace expiree : heartbeat et beacon arretes.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Background grace erreur : {ex.Message}");
            }
        }, token);

        System.Diagnostics.Debug.WriteLine("[App] Background grace activee pour 10 minutes.");
    }

    private void CancelBackgroundGraceStop()
    {
        try
        {
            _backgroundGraceCts?.Cancel();
            _backgroundGraceCts?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _backgroundGraceCts = null;
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();

        if (_auth.GetCurrentUserId() == null)
            return;

        // IMPORTANT Spotiz : écran verrouillé / application en arrière-plan ≠ départ réel.
        // Avant, on faisait GoOfflineAsync() + CheckOutActiveAsync() ici, donc l'utilisateur
        // disparaissait immédiatement de la carte et des présences dès que le téléphone se mettait
        // en veille. Maintenant on arrête seulement le heartbeat local : Supabase gardera la
        // présence jusqu'à expires_at, prolongé à 1 heure par heartbeat_presence().
        ScheduleBackgroundGraceStop();

        System.Diagnostics.Debug.WriteLine(
            "[App] 😴 OnSleep → heartbeat arrêté, présence conservée temporairement (expiration serveur)");
    }

    protected override void OnResume()
    {
        base.OnResume();

        CancelBackgroundGraceStop();

        Task.Run(async () =>
        {
            try
            {
                var restored = await _auth.RestoreSessionAsync();
                if (!restored || _auth.GetCurrentUserId() == null)
                {
                    System.Diagnostics.Debug.WriteLine("[App] OnResume session absente : retour connexion.");
                    MainThread.BeginInvokeOnMainThread(ShowLoginPage);
                    return;
                }

                await _userStatus.GoOnlineAsync();
                _heartbeat.Start();
                _beaconAutoCheckin.Start();

                System.Diagnostics.Debug.WriteLine(
                    "[App] 🌅 OnResume → GoOnline OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] OnResume erreur : {ex.Message}");
            }
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _pushNotifications.RegisterDeviceAsync();
                await NotificationNavigationService.ProcessPendingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] OnResume Push erreur : {ex.Message}");
            }
        });
    }
}
