using System.Collections;
using System.Windows.Input;

namespace NightOut.Views.Bar.Components;

public partial class BarInfosView : ContentView
{
    public static readonly BindableProperty BarAddressProperty = BindableProperty.Create(nameof(BarAddress), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty OpenStatusTextProperty = BindableProperty.Create(nameof(OpenStatusText), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty BarPhoneProperty = BindableProperty.Create(nameof(BarPhone), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty BarInstagramProperty = BindableProperty.Create(nameof(BarInstagram), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty BarWebsiteProperty = BindableProperty.Create(nameof(BarWebsite), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty BarDescriptionProperty = BindableProperty.Create(nameof(BarDescription), typeof(string), typeof(BarInfosView), string.Empty);
    public static readonly BindableProperty HasPhoneProperty = BindableProperty.Create(nameof(HasPhone), typeof(bool), typeof(BarInfosView), false);
    public static readonly BindableProperty HasInstagramProperty = BindableProperty.Create(nameof(HasInstagram), typeof(bool), typeof(BarInfosView), false);
    public static readonly BindableProperty HasWebsiteProperty = BindableProperty.Create(nameof(HasWebsite), typeof(bool), typeof(BarInfosView), false);
    public static readonly BindableProperty HasDescriptionProperty = BindableProperty.Create(nameof(HasDescription), typeof(bool), typeof(BarInfosView), false);
    public static readonly BindableProperty HasAnyOfficialEventsProperty = BindableProperty.Create(nameof(HasAnyOfficialEvents), typeof(bool), typeof(BarInfosView), false);
    public static readonly BindableProperty UpcomingOfficialEventsProperty = BindableProperty.Create(nameof(UpcomingOfficialEvents), typeof(IEnumerable), typeof(BarInfosView));
    public static readonly BindableProperty GoToMapsCommandProperty = BindableProperty.Create(nameof(GoToMapsCommand), typeof(ICommand), typeof(BarInfosView));
    public static readonly BindableProperty CallBarCommandProperty = BindableProperty.Create(nameof(CallBarCommand), typeof(ICommand), typeof(BarInfosView));
    public static readonly BindableProperty OpenInstagramCommandProperty = BindableProperty.Create(nameof(OpenInstagramCommand), typeof(ICommand), typeof(BarInfosView));
    public static readonly BindableProperty OpenWebsiteCommandProperty = BindableProperty.Create(nameof(OpenWebsiteCommand), typeof(ICommand), typeof(BarInfosView));
    public static readonly BindableProperty OpenOfficialEventCommandProperty = BindableProperty.Create(nameof(OpenOfficialEventCommand), typeof(ICommand), typeof(BarInfosView));

    public string BarAddress { get => (string)GetValue(BarAddressProperty); set => SetValue(BarAddressProperty, value); }
    public string OpenStatusText { get => (string)GetValue(OpenStatusTextProperty); set => SetValue(OpenStatusTextProperty, value); }
    public string BarPhone { get => (string)GetValue(BarPhoneProperty); set => SetValue(BarPhoneProperty, value); }
    public string BarInstagram { get => (string)GetValue(BarInstagramProperty); set => SetValue(BarInstagramProperty, value); }
    public string BarWebsite { get => (string)GetValue(BarWebsiteProperty); set => SetValue(BarWebsiteProperty, value); }
    public string BarDescription { get => (string)GetValue(BarDescriptionProperty); set => SetValue(BarDescriptionProperty, value); }
    public bool HasPhone { get => (bool)GetValue(HasPhoneProperty); set => SetValue(HasPhoneProperty, value); }
    public bool HasInstagram { get => (bool)GetValue(HasInstagramProperty); set => SetValue(HasInstagramProperty, value); }
    public bool HasWebsite { get => (bool)GetValue(HasWebsiteProperty); set => SetValue(HasWebsiteProperty, value); }
    public bool HasDescription { get => (bool)GetValue(HasDescriptionProperty); set => SetValue(HasDescriptionProperty, value); }
    public bool HasAnyOfficialEvents { get => (bool)GetValue(HasAnyOfficialEventsProperty); set => SetValue(HasAnyOfficialEventsProperty, value); }
    public IEnumerable? UpcomingOfficialEvents { get => (IEnumerable?)GetValue(UpcomingOfficialEventsProperty); set => SetValue(UpcomingOfficialEventsProperty, value); }
    public ICommand? GoToMapsCommand { get => (ICommand?)GetValue(GoToMapsCommandProperty); set => SetValue(GoToMapsCommandProperty, value); }
    public ICommand? CallBarCommand { get => (ICommand?)GetValue(CallBarCommandProperty); set => SetValue(CallBarCommandProperty, value); }
    public ICommand? OpenInstagramCommand { get => (ICommand?)GetValue(OpenInstagramCommandProperty); set => SetValue(OpenInstagramCommandProperty, value); }
    public ICommand? OpenWebsiteCommand { get => (ICommand?)GetValue(OpenWebsiteCommandProperty); set => SetValue(OpenWebsiteCommandProperty, value); }
    public ICommand? OpenOfficialEventCommand { get => (ICommand?)GetValue(OpenOfficialEventCommandProperty); set => SetValue(OpenOfficialEventCommandProperty, value); }

    public BarInfosView()
    {
        InitializeComponent();
    }
}
