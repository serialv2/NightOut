using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Firebase.Messaging;
using NightOut.Services;
using System;

namespace NightOut.Platforms.Android;

[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class NightOutFirebaseMessagingService : FirebaseMessagingService
{
    private const string DefaultChannelId = "nightout_default_channel";
    private const string DefaultChannelName = "NightOut";

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);

        System.Diagnostics.Debug.WriteLine($"[FCM] Nouveau token : {token}");
        PushNotificationTokenStore.SetToken(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        try
        {
            // Quand l'app est ouverte, les messages arrivent déjà via Supabase Realtime.
            // On évite donc d'afficher une notification Android système en doublon.
            if (AppForegroundState.IsForeground)
            {
                System.Diagnostics.Debug.WriteLine("[FCM] Push reçu mais app au premier plan : notification Android ignorée, badge interne utilisé");
                return;
            }

            var title = message.GetNotification()?.Title;
            var body = message.GetNotification()?.Body;

            if (string.IsNullOrWhiteSpace(title) && message.Data.TryGetValue("title", out var dataTitle))
                title = dataTitle;

            if (string.IsNullOrWhiteSpace(body) && message.Data.TryGetValue("body", out var dataBody))
                body = dataBody;

            title = string.IsNullOrWhiteSpace(title) ? "NightOut" : title;
            body = string.IsNullOrWhiteSpace(body) ? "Nouvelle notification" : body;

            ShowLocalNotification(title, body, message.Data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FCM] OnMessageReceived erreur : {ex}");
        }
    }

    private void ShowLocalNotification(string title, string body, IDictionary<string, string> data)
    {
        CreateNotificationChannelIfNeeded();

        var intent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty)
            ?? new Intent(this, typeof(MainActivity));

        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

        foreach (var item in data)
            intent.PutExtra(item.Key, item.Value);

        var pendingIntent = PendingIntent.GetActivity(
            this,
            Random.Shared.Next(1000, 999999),
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notification = new NotificationCompat.Builder(this, DefaultChannelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(this)
            .Notify(Random.Shared.Next(1000, 999999), notification);
    }

    private void CreateNotificationChannelIfNeeded()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);

        if (manager?.GetNotificationChannel(DefaultChannelId) != null)
            return;

        var channel = new NotificationChannel(
            DefaultChannelId,
            DefaultChannelName,
            NotificationImportance.High)
        {
            Description = "Notifications NightOut"
        };

        manager?.CreateNotificationChannel(channel);
    }
}
