namespace NightOut.Services;

public enum AppThemeMode
{
    Light,
    Dark
}

public interface IThemeService
{
    /// <summary>Thème actuellement appliqué.</summary>
    AppThemeMode CurrentTheme { get; }

    /// <summary>Vrai si le thème sombre est actif.</summary>
    bool IsDarkMode { get; }

    /// <summary>Déclenché après un changement de thème (permet aux pages/VMs de réagir, ex: style de carte Mapbox).</summary>
    event EventHandler<AppThemeMode>? ThemeChanged;

    /// <summary>À appeler une fois au démarrage de l'app (dans App.xaml.cs, après InitializeComponent).</summary>
    void Initialize();

    /// <summary>Applique et persiste un thème précis.</summary>
    void ApplyTheme(AppThemeMode theme);

    /// <summary>Bascule entre clair et sombre.</summary>
    void ToggleTheme();
}
