namespace NightOut.Services;

public static class AndroidNotificationCleaner
{
    public static void ClearAll()
    {
        try
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var manager = context.GetSystemService(Android.Content.Context.NotificationService)
                as Android.App.NotificationManager;

            manager?.CancelAll();
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notifications] ClearAll Android erreur : {ex.Message}");
        }
    }
}
