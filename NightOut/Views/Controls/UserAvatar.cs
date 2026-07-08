using Microsoft.Maui.Controls.Shapes;

namespace NightOut.Views.Controls;

/// <summary>
/// Avatar réutilisable NightOut :
/// - affiche la photo si AvatarUrl est renseignée ;
/// - sinon affiche les initiales calculées à partir de Name.
/// </summary>
public class UserAvatar : ContentView
{
    private readonly Border _border;
    private readonly Grid _root;
    private readonly Image _image;
    private readonly Label _initialsLabel;

    public static readonly BindableProperty AvatarUrlProperty = BindableProperty.Create(
        nameof(AvatarUrl),
        typeof(string),
        typeof(UserAvatar),
        default(string),
        propertyChanged: OnAvatarChanged);

    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name),
        typeof(string),
        typeof(UserAvatar),
        default(string),
        propertyChanged: OnAvatarChanged);

    public static readonly BindableProperty AvatarSizeProperty = BindableProperty.Create(
        nameof(AvatarSize),
        typeof(double),
        typeof(UserAvatar),
        46d,
        propertyChanged: OnAvatarChanged);

    public static readonly BindableProperty BackgroundAvatarColorProperty = BindableProperty.Create(
        nameof(BackgroundAvatarColor),
        typeof(Color),
        typeof(UserAvatar),
        Color.FromArgb("#EEE9E3"),
        propertyChanged: OnAvatarChanged);

    public static readonly BindableProperty InitialsColorProperty = BindableProperty.Create(
        nameof(InitialsColor),
        typeof(Color),
        typeof(UserAvatar),
        Color.FromArgb("#CEA358"),
        propertyChanged: OnAvatarChanged);

    public string? AvatarUrl
    {
        get => (string?)GetValue(AvatarUrlProperty);
        set => SetValue(AvatarUrlProperty, value);
    }

    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public double AvatarSize
    {
        get => (double)GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public Color BackgroundAvatarColor
    {
        get => (Color)GetValue(BackgroundAvatarColorProperty);
        set => SetValue(BackgroundAvatarColorProperty, value);
    }

    public Color InitialsColor
    {
        get => (Color)GetValue(InitialsColorProperty);
        set => SetValue(InitialsColorProperty, value);
    }

    public UserAvatar()
    {
        _image = new Image
        {
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _initialsLabel = new Label
        {
            TextColor = InitialsColor,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        _root = new Grid
        {
            Children = { _image, _initialsLabel }
        };

        _border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = BackgroundAvatarColor,
            Content = _root
        };

        Content = _border;

        SetDynamicResource(BackgroundAvatarColorProperty, "BgElevated");
        SetDynamicResource(InitialsColorProperty, "Accent");

        UpdateVisualState();
    }

    private static void OnAvatarChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is UserAvatar avatar)
            avatar.UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_border is null || _root is null || _image is null || _initialsLabel is null)
            return;

        var size = AvatarSize <= 0 ? 46d : AvatarSize;

        WidthRequest = size;
        HeightRequest = size;
        MinimumWidthRequest = size;
        MinimumHeightRequest = size;

        _border.WidthRequest = size;
        _border.HeightRequest = size;
        _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(size / 2d) };
        _border.BackgroundColor = BackgroundAvatarColor;

        _root.WidthRequest = size;
        _root.HeightRequest = size;

        _initialsLabel.Text = BuildInitials(Name);
        _initialsLabel.TextColor = InitialsColor;
        _initialsLabel.FontSize = Math.Max(12, size * 0.38);

        var hasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        _image.IsVisible = hasAvatar;
        _initialsLabel.IsVisible = !hasAvatar;

        if (hasAvatar)
            _image.Source = ImageSource.FromUri(new Uri(AvatarUrl!.Trim()));
        else
            _image.Source = null;
    }

    private static string BuildInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var clean = name.Trim();
        var parts = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return "?";

        if (parts.Length == 1)
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0][..1].ToUpperInvariant();

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
