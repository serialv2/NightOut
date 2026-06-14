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
    private readonly AppShell _shell;

    public App(
        IAuthService auth,
        IUserStatusService userStatus,
        ICheckinService checkin,
        HeartbeatService heartbeat,
        InviteDeepLinkService inviteDeepLinks,
        IPushNotificationService pushNotifications,
        AppShell shell,
        IServiceProvider services)
    {
        InitializeComponent();

        _auth = auth;
        _userStatus = userStatus;
        _checkin = checkin;
        _heartbeat = heartbeat;
        _services = services;
        _inviteDeepLinks = inviteDeepLinks;
        _pushNotifications = pushNotifications;
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

    private static Page CreateLoadingPage()
    {
        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#0A1018"),
            Content = new VerticalStackLayout
            {
                Spacing = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "NightOut",
                        FontFamily = "PlayfairDisplay-BoldItalic",
                        FontSize = 42,
                        TextColor = Color.FromArgb("#FFB627"),
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = Color.FromArgb("#FFB627"),
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
            BarBackgroundColor = Color.FromArgb("#0A1018"),
            BarTextColor = Color.FromArgb("#F2E8D5")
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

    protected override void OnSleep()
    {
        base.OnSleep();

        if (_auth.GetCurrentUserId() == null)
            return;

        _heartbeat.Stop();

        Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(
                    _userStatus.GoOfflineAsync(),
                    _checkin.CheckOutActiveAsync()
                );

                System.Diagnostics.Debug.WriteLine(
                    "[App] 😴 OnSleep → GoOffline + Checkout OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] OnSleep erreur : {ex.Message}");
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();

        if (_auth.GetCurrentUserId() == null)
            return;

        Task.Run(async () =>
        {
            try
            {
                await _userStatus.GoOnlineAsync();

                System.Diagnostics.Debug.WriteLine(
                    "[App] 🌅 OnResume → GoOnline OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] OnResume erreur : {ex.Message}");
            }
        });

        _heartbeat.Start();

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
