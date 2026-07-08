using System.Globalization;

namespace NightOut.Views.Friends;

// Couleur d'avatar déterministe à partir d'un nom (pseudo).
public sealed class NameToColorConverter : IValueConverter
{
    private static readonly string[] Palette =
    {
        "#534AB7", "#D85A30", "#1D9E75", "#185FA5",
        "#BA7517", "#D4537E", "#0F6E56", "#7C3AED"
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
            ? Color.FromArgb("#3DB87A")
            : Color.FromArgb("#3D5068");
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
            ? Color.FromArgb("#FFB627")
            : Color.FromArgb("#66768A");
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
