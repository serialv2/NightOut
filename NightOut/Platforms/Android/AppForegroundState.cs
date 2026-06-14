namespace NightOut.Platforms.Android;

/// <summary>
/// État simple de l'application côté Android.
/// Sert à éviter d'afficher une notification système quand NightOut est déjà ouvert.
/// </summary>
public static class AppForegroundState
{
    public static bool IsForeground { get; set; }
}
