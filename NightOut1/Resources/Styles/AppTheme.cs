namespace NightOut.Resources.Styles;

/// <summary>
/// Charte graphique NightOut — Neon Night
/// CE FICHIER EST LA SEULE SOURCE DE VÉRITÉ pour toutes les couleurs,
/// tailles et constantes visuelles de l'application.
/// Modifier ici = s'applique partout.
/// </summary>
public static class AppTheme
{
    // ══════════════════════════════════════════════
    // BACKGROUNDS
    // ══════════════════════════════════════════════

    /// Fond principal de l'app (le plus sombre)
    public const string BgDeep = "#090814";

    /// Fond des cartes / composants
    public const string BgCard = "#121124";

    /// Fond des panneaux (bottom sheets, modals)
    public const string BgPanel = "#191733";

    /// Fond surélevé (inputs, blocs internes)
    public const string BgElevated = "#211F42";

    /// Fond des champs de saisie
    public const string BgInput = "#29264F";

    // ══════════════════════════════════════════════
    // ACCENT PRINCIPAL — Violet
    // ══════════════════════════════════════════════

    /// Couleur accent principale
    public const string Accent = "#A855F7";

    /// Accent foncé (pressed states)
    public const string AccentDark = "#7E22CE";

    /// Accent transparent 10% (backgrounds subtils)
    public const string AccentDim = "#1AA855F7";

    /// Accent transparent 18% (hover states)
    public const string AccentSoft = "#2EA855F7";

    /// Accent transparent 30% (glows, borders actives)
    public const string AccentGlow = "#4DA855F7";

    // ══════════════════════════════════════════════
    // COULEUR SECONDAIRE — Or
    // ══════════════════════════════════════════════

    /// Couleur secondaire (promos, badges, gradients)
    public const string Second = "#FFB627";

    /// Secondaire transparent 12%
    public const string SecondDim = "#1FFFB627";

    // ══════════════════════════════════════════════
    // TEXTES
    // ══════════════════════════════════════════════

    /// Texte principal (titres, contenu important)
    public const string TextPrimary = "#F4EEFF";

    /// Texte secondaire (sous-titres, descriptions)
    public const string TextSecondary = "#9CA3C7";

    /// Texte atténué (placeholders, labels discrets)
    public const string TextMuted = "#555B7A";

    // ══════════════════════════════════════════════
    // BORDURES
    // ══════════════════════════════════════════════

    /// Bordure subtile (séparateurs, contours légers)
    public const string Border = "#1AA855F7";

    /// Bordure moyenne
    public const string BorderMid = "#2EA855F7";

    /// Bordure forte (éléments actifs, sélectionnés)
    public const string BorderStrong = "#4DA855F7";

    // ══════════════════════════════════════════════
    // JAUGES DE FRÉQUENTATION
    // ══════════════════════════════════════════════

    /// Jauge basse fréquentation — Tranquille
    public const string GaugeLow = "#34D399";

    /// Jauge moyenne fréquentation — Animé
    public const string GaugeMid = "#FFB627";

    /// Jauge haute fréquentation — Chaud
    public const string GaugeHot = "#FF6B35";

    /// Jauge très haute fréquentation — En feu
    public const string GaugeFire = "#FF2D6B";

    // ══════════════════════════════════════════════
    // ÉTATS SYSTÈME
    // ══════════════════════════════════════════════

    public const string Success = "#34D399";
    public const string Warning = "#FFB627";
    public const string Error = "#EF4444";
    public const string Info = "#60A5FA";

    // ══════════════════════════════════════════════
    // TYPOGRAPHIE — Tailles
    // ══════════════════════════════════════════════

    public const double FontXs = 9;
    public const double FontSm = 11;
    public const double FontMd = 13;
    public const double FontLg = 15;
    public const double FontXl = 17;
    public const double FontXxl = 21;
    public const double FontHero = 28;

    // ══════════════════════════════════════════════
    // TYPOGRAPHIE — Familles
    // ══════════════════════════════════════════════

    public const string FontDisplay = "PlayfairDisplay-BoldItalic";
    public const string FontBody = "DMSans-Regular";
    public const string FontBodyMd = "DMSans-Medium";
    public const string FontBodySb = "DMSans-SemiBold";
    public const string FontBodyBd = "DMSans-Bold";

    // ══════════════════════════════════════════════
    // RAYONS DE BORDURE
    // ══════════════════════════════════════════════

    public const double RadiusSm = 8;
    public const double RadiusMd = 14;
    public const double RadiusLg = 20;
    public const double RadiusXl = 26;
    public const double RadiusFull = 999;

    // ══════════════════════════════════════════════
    // ESPACEMENTS
    // ══════════════════════════════════════════════

    public const double SpaceXs = 4;
    public const double SpaceSm = 8;
    public const double SpaceMd = 12;
    public const double SpaceLg = 16;
    public const double SpaceXl = 24;
    public const double SpaceXxl = 32;

    // ══════════════════════════════════════════════
    // DIMENSIONS UI
    // ══════════════════════════════════════════════

    public const double TopBarHeight = 62;
    public const double BottomNavHeight = 62;
    public const double BottomSheetRadius = 26;

    public const double MarkerCardMinWidth = 136;
    public const double AvatarSm = 30;
    public const double AvatarMd = 36;
    public const double AvatarLg = 46;

    public const double FabSize = 40;
    public const double IconBtnSize = 36;

    // ══════════════════════════════════════════════
    // OMBRES (elevation)
    // ══════════════════════════════════════════════

    public const float ShadowLow = 4;
    public const float ShadowMid = 12;
    public const float ShadowHigh = 28;

    // ══════════════════════════════════════════════
    // HELPERS — Jauge couleur selon taux
    // ══════════════════════════════════════════════

    public static string GetGaugeColor(double fillRatio) => fillRatio switch
    {
        < 0.30 => GaugeLow,
        < 0.55 => GaugeMid,
        < 0.80 => GaugeHot,
        _ => GaugeFire
    };

    public static string GetGaugeLabel(double fillRatio) => fillRatio switch
    {
        < 0.30 => "Tranquille",
        < 0.55 => "Bonne ambiance",
        < 0.80 => "Très animé",
        _ => "En feu 🔥"
    };

    // ══════════════════════════════════════════════
    // MAPBOX — Style carte
    // ══════════════════════════════════════════════

    public const string MapboxStyleUrl = "mapbox://styles/mapbox/dark-v11";
    public const string MapBgColor = "#090814";
    public const string MapStreetColor = "#211F42";
    public const string MapStreetMinor = "#191733";
    public const string MapLabelColor = "#555B7A";
}