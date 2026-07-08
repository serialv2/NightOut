using NightOut.Resources.Styles;

namespace NightOut.Services;

/// <summary>
/// Gère le basculement thème clair / sombre au runtime.
/// Fonctionne en échangeant le ResourceDictionary de couleurs actif
/// (ColorsLight / ColorsDark) dans Application.Current.Resources.MergedDictionaries.
/// Tout élément lié via {DynamicResource ...} se met à jour automatiquement.
/// </summary>
public class ThemeService : IThemeService
{
    private const string PrefKey = "app_theme_mode";

    public AppThemeMode CurrentTheme { get; private set; } = AppThemeMode.Light;

    public bool IsDarkMode => CurrentTheme == AppThemeMode.Dark;

    public event EventHandler<AppThemeMode>? ThemeChanged;

    public void Initialize()
    {
        var saved = Preferences.Default.Get(PrefKey, nameof(AppThemeMode.Light));
        var theme = saved == nameof(AppThemeMode.Dark) ? AppThemeMode.Dark : AppThemeMode.Light;
        ApplyTheme(theme, persist: false, raiseEvent: false);
    }

    public void ApplyTheme(AppThemeMode theme) => ApplyTheme(theme, persist: true, raiseEvent: true);

    public void ToggleTheme() => ApplyTheme(IsDarkMode ? AppThemeMode.Light : AppThemeMode.Dark);

    private void ApplyTheme(AppThemeMode theme, bool persist, bool raiseEvent)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app == null)
            return;

        var merged = app.Resources.MergedDictionaries;

        // On retire l'ancien dictionnaire de couleurs (clair ou sombre) s'il est présent.
        foreach (var existing in merged.Where(d => d is ColorsLight or ColorsDark).ToList())
            merged.Remove(existing);

        merged.Add(theme == AppThemeMode.Dark
            ? new ColorsDark()
            : new ColorsLight());

        app.UserAppTheme = theme == AppThemeMode.Dark
            ? Microsoft.Maui.ApplicationModel.AppTheme.Dark
            : Microsoft.Maui.ApplicationModel.AppTheme.Light;

        CurrentTheme = theme;

        if (persist)
            Preferences.Default.Set(PrefKey, theme.ToString());

        if (raiseEvent)
            ThemeChanged?.Invoke(this, theme);
    }
}
