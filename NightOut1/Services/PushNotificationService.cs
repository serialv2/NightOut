using NightOut.Models;
using Supabase;
using static Supabase.Postgrest.Constants;

#if ANDROID
using Android;
using Firebase.Messaging;
#endif

namespace NightOut.Services;

public class PushNotificationService(Client supabase, IAuthService auth) : IPushNotificationService
{
    private bool _initialized;

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        if (!_initialized)
        {
            _initialized = true;

            PushNotificationTokenStore.TokenChanged += token =>
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await SaveTokenAsync(token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Push] TokenChanged SaveToken erreur : {ex.Message}");
                    }
                });
            };
        }

        await RegisterDeviceAsync();
    }

    public async System.Threading.Tasks.Task RegisterDeviceAsync()
    {
        try
        {
#if ANDROID
            await RequestAndroidNotificationPermissionAsync();
#endif
            var token = await GetCurrentTokenAsync();

            if (!string.IsNullOrWhiteSpace(token))
                await SaveTokenAsync(token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] RegisterDeviceAsync erreur : {ex}");
        }
    }

    public async System.Threading.Tasks.Task<string?> GetCurrentTokenAsync()
    {
#if ANDROID
        try
        {
            var token = await GetFirebaseTokenAsync();

            if (!string.IsNullOrWhiteSpace(token))
                PushNotificationTokenStore.SetToken(token);

            System.Diagnostics.Debug.WriteLine($"[Push] FCM TOKEN = {token}");

            return token;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] GetCurrentTokenAsync Android erreur : {ex.Message}");
            return PushNotificationTokenStore.CurrentToken;
        }
#else
        await System.Threading.Tasks.Task.CompletedTask;
        return null;
#endif
    }

    private async System.Threading.Tasks.Task SaveTokenAsync(string token)
    {
        var me = auth.GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(token))
            return;

        var platform = GetPlatform();
        var deviceName = DeviceInfo.Current.Name;
        var appVersion = AppInfo.Current.VersionString;
        var now = DateTime.UtcNow;

        try
        {
            // IMPORTANT : un même téléphone ne doit pas rester attaché à plusieurs comptes.
            // Sinon l'ancien compte continue à recevoir les notifications push.
            await supabase.From<DeviceToken>()
                .Filter("token", Operator.Equals, token)
                .Delete();

            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                await supabase.From<DeviceToken>()
                    .Filter("platform", Operator.Equals, platform)
                    .Filter("device_name", Operator.Equals, deviceName)
                    .Delete();
            }

            await supabase.From<DeviceToken>().Insert(new DeviceToken
            {
                Id = Guid.NewGuid().ToString(),
                UserId = me,
                Token = token,
                Platform = platform,
                DeviceName = deviceName,
                AppVersion = appVersion,
                CreatedAt = now,
                LastSeenAt = now
            });

            System.Diagnostics.Debug.WriteLine($"[Push] Token FCM nettoyé et enregistré pour {me} / {deviceName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] SaveTokenAsync erreur : {ex}");
        }
    }

    private static string GetPlatform()
    {
#if ANDROID
        return "android";
#elif IOS
        return "ios";
#else
        return DeviceInfo.Current.Platform.ToString().ToLowerInvariant();
#endif
    }

#if ANDROID
    private static System.Threading.Tasks.Task<string?> GetFirebaseTokenAsync()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string?>();

        try
        {
            FirebaseMessaging.Instance.GetToken()
                .AddOnCompleteListener(new TokenCompleteListener(tcs));
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    private sealed class TokenCompleteListener(System.Threading.Tasks.TaskCompletionSource<string?> tcs)
        : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        public void OnComplete(global::Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
            {
                tcs.TrySetResult(task.Result?.ToString());
                return;
            }

            var message = task.Exception?.Message ?? "Impossible de récupérer le token FCM.";
            tcs.TrySetException(new Exception(message));
        }
    }

    private static async System.Threading.Tasks.Task RequestAndroidNotificationPermissionAsync()
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                return;

            var status = await Permissions.CheckStatusAsync<NotificationPermission>();

            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<NotificationPermission>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Push] Permission notification erreur : {ex.Message}");
        }
    }

    private sealed class NotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [
            (Manifest.Permission.PostNotifications, true)
        ];
    }
#endif
}
