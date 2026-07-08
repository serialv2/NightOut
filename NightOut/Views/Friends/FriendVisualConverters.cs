using System.Globalization;

namespace NightOut.Views.Friends;

// Couleur d'avatar déterministe à partir d'un nom (pseudo).
public sealed class NameToColorConverter : IValueConverter
{
    private static readonly string[] Palette =
    {
        "#5B87A6", "#C85B40", "#537E3D", "#42637B",
        "#A2782F", "#C47663", "#39502D", "#CF8458"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value as string ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Color.FromArgb(Palette[0]);

        int h = 0;
        foreach (var c in name)
            h = (h * 31 + c) & 0x7fffffff;

        return Color.FromArgb(Palette[h % Palette.Length]);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Première lettre (majuscule) d'un pseudo, pour le fallback initiales.
public sealed class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value as string ?? string.Empty;
        return string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Statut de présence -> couleur du point (vert si en ligne / en sortie, gris sinon).
public sealed class StatusToPresenceColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string ?? "offline";
        return status is "out" or "online"
            ? FriendThemeColors.Get("Success", "#4A7A52")
            : FriendThemeColors.Get("TextMuted", "#D4C7B5");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Statut de présence -> couleur du sous-titre (or si en sortie, gris sinon).
public sealed class StatusToLocationColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string ?? "offline";
        return status == "out"
            ? FriendThemeColors.Get("Accent", "#C2754C")
            : FriendThemeColors.Get("TextMuted", "#D4C7B5");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Chaîne non vide -> bool (visibilité d'une image d'avatar).
public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal static class FriendThemeColors
{
    public static Color Get(string key, string fallbackHex)
    {
        var resources = Application.Current?.Resources;
        return resources != null &&
               resources.TryGetValue(key, out var value) &&
               value is Color color
            ? color
            : Color.FromArgb(fallbackHex);
    }
}
