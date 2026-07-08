using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using NightOut.Platforms.Android;
using NightOut.Services;

namespace NightOut;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[]
    {
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
    },
    DataScheme = "https",
    DataHost = "spotiz.fr",
    DataPathPrefix = "/invite")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[]
    {
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
    },
    DataScheme = "spotiz",
    DataHost = "invite")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Contournement du crash MAUI :
        // "No view found for id ... for fragment ShellItemRenderer"
        savedInstanceState?.Remove("android:support:fragments");
        savedInstanceState?.Remove("android:fragments");

        base.OnCreate(savedInstanceState);

        CreateNotificationChannelIfNeeded();
        HandleIntent(Intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppForegroundState.IsForeground = true;
        System.Diagnostics.Debug.WriteLine("[FCM] App au premier plan");
    }

    protected override void OnPause()
    {
        AppForegroundState.IsForeground = false;
        System.Diagnostics.Debug.WriteLine("[FCM] App en arrière-plan");
        base.OnPause();
    }

    protected override void OnStop()
    {
        AppForegroundState.IsForeground = false;
        base.OnStop();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent == null)
            return;

        Intent = intent;
        HandleIntent(intent);
    }

    private void CreateNotificationChannelIfNeeded()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        const string channelId = "spotiz_default_channel";
        const string channelName = "Spotiz";

        var manager = (NotificationManager?)GetSystemService(NotificationService);

        if (manager?.GetNotificationChannel(channelId) != null)
            return;

        var channel = new NotificationChannel(
            channelId,
            channelName,
            NotificationImportance.High)
        {
            Description = "Notifications Spotiz"
        };

        manager?.CreateNotificationChannel(channel);
    }

    private static void HandleIntent(Intent? intent)
    {
        if (intent == null)
            return;

        CaptureNotificationExtras(intent);

        var url = intent.DataString;

        if (!string.IsNullOrWhiteSpace(url))
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(new Uri(url));
    }

    private static void CaptureNotificationExtras(Intent intent)
    {
        var extras = intent.Extras;
        if (extras == null || extras.IsEmpty)
            return;

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in extras.KeySet() ?? [])
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = extras.Get(key)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                data[key] = value;
        }

        NotificationNavigationService.SetFromDictionary(data);

        // Très important : on ne navigue pas ici.
        // Sur ouverture depuis push, l'app doit d'abord passer par RestoreSessionAsync().
        // La navigation sera exécutée ensuite dans App.StartAsync() ou App.OnResume().
    }
}
