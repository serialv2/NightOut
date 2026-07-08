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
    public const string BgDeep = "#F5F2EF";

    /// Fond des cartes / composants
    public const string BgCard = "#F4F0EB";

    /// Fond des panneaux (bottom sheets, modals)
    public const string BgPanel = "#F2EEE9";

    /// Fond surélevé (inputs, blocs internes)
    public const string BgElevated = "#F0ECE6";

    /// Fond des champs de saisie
    public const string BgInput = "#EFEAE3";

    // ══════════════════════════════════════════════
    // ACCENT PRINCIPAL — Violet
    // ══════════════════════════════════════════════

    /// Couleur accent principale
    public const string Accent = "#D79975";

    /// Accent foncé (pressed states)
    public const string AccentDark = "#BA6636";

    /// Accent transparent 10% (backgrounds subtils)
    public const string AccentDim = "#1AD79975";

    /// Accent transparent 18% (hover states)
    public const string AccentSoft = "#2ED79975";

    /// Accent transparent 30% (glows, borders actives)
    public const string AccentGlow = "#4DD79975";

    // ══════════════════════════════════════════════
    // COULEUR SECONDAIRE — Or
    // ══════════════════════════════════════════════

    /// Couleur secondaire (promos, badges, gradients)
    public const string Second = "#CEA358";

    /// Secondaire transparent 12%
    public const string SecondDim = "#1FCEA358";

    // ══════════════════════════════════════════════
    // TEXTES
    // ══════════════════════════════════════════════

    /// Texte principal (titres, contenu important)
    public const string TextPrimary = "#37241B";

    /// Texte secondaire (sous-titres, descriptions)
    public const string TextSecondary = "#604A39";

    /// Texte atténué (placeholders, labels discrets)
    public const string TextMuted = "#C9BAAB";

    // ══════════════════════════════════════════════
    // BORDURES
    // ══════════════════════════════════════════════

    /// Bordure subtile (séparateurs, contours légers)
    public const string Border = "#1AD79975";

    /// Bordure moyenne
    public const string BorderMid = "#2ED79975";

    /// Bordure forte (éléments actifs, sélectionnés)
    public const string BorderStrong = "#4DD79975";

    // ══════════════════════════════════════════════
    // JAUGES DE FRÉQUENTATION
    // ══════════════════════════════════════════════

    /// Jauge basse fréquentation — Tranquille
    public const string GaugeLow = "#659B4B";

    /// Jauge moyenne fréquentation — Animé
    public const string GaugeMid = "#CEA358";

    /// Jauge haute fréquentation — Chaud
    public const string GaugeHot = "#D27962";

    /// Jauge très haute fréquentation — En feu
    public const string GaugeFire = "#D0735C";

    // ══════════════════════════════════════════════
    // ÉTATS SYSTÈME
    // ══════════════════════════════════════════════

    public const string Success = "#659B4B";
    public const string Warning = "#CEA358";
    public const string Error = "#D17862";
    public const string Info = "#94B1C6";

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
    public const string MapBgColor = "#F5F2EF";
    public const string MapStreetColor = "#F0ECE6";
    public const string MapStreetMinor = "#F2EEE9";
    public const string MapLabelColor = "#C9BAAB";
}