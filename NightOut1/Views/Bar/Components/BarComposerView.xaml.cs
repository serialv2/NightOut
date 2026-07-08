using System.Collections;
using System.Windows.Input;

namespace NightOut.Views.Bar.Components;

public partial class BarComposerView : ContentView
{
    public static readonly BindableProperty FeedSubtitleProperty =
        BindableProperty.Create(nameof(FeedSubtitle), typeof(string), typeof(BarComposerView), string.Empty);

    public static readonly BindableProperty HasFriendsHereProperty =
        BindableProperty.Create(nameof(HasFriendsHere), typeof(bool), typeof(BarComposerView), false);

    public static readonly BindableProperty FriendsHereProperty =
        BindableProperty.Create(nameof(FriendsHere), typeof(IEnumerable), typeof(BarComposerView));

    public static readonly BindableProperty FriendsHerePlusLabelProperty =
        BindableProperty.Create(nameof(FriendsHerePlusLabel), typeof(string), typeof(BarComposerView), string.Empty);

    public static readonly BindableProperty MessageTextProperty =
        BindableProperty.Create(nameof(MessageText), typeof(string), typeof(BarComposerView), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty PostPhotoCommandProperty =
        BindableProperty.Create(nameof(PostPhotoCommand), typeof(ICommand), typeof(BarComposerView));

    public static readonly BindableProperty PostVideoCommandProperty =
        BindableProperty.Create(nameof(PostVideoCommand), typeof(ICommand), typeof(BarComposerView));

    public static readonly BindableProperty PostMessageCommandProperty =
        BindableProperty.Create(nameof(PostMessageCommand), typeof(ICommand), typeof(BarComposerView));

    public string FeedSubtitle
    {
        get => (string)GetValue(FeedSubtitleProperty);
        set => SetValue(FeedSubtitleProperty, value);
    }

    public bool HasFriendsHere
    {
        get => (bool)GetValue(HasFriendsHereProperty);
        set => SetValue(HasFriendsHereProperty, value);
    }

    public IEnumerable? FriendsHere
    {
        get => (IEnumerable?)GetValue(FriendsHereProperty);
        set => SetValue(FriendsHereProperty, value);
    }

    public string FriendsHerePlusLabel
    {
        get => (string)GetValue(FriendsHerePlusLabelProperty);
        set => SetValue(FriendsHerePlusLabelProperty, value);
    }

    public string MessageText
    {
        get => (string)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public ICommand? PostPhotoCommand
    {
        get => (ICommand?)GetValue(PostPhotoCommandProperty);
        set => SetValue(PostPhotoCommandProperty, value);
    }

    public ICommand? PostVideoCommand
    {
        get => (ICommand?)GetValue(PostVideoCommandProperty);
        set => SetValue(PostVideoCommandProperty, value);
    }

    public ICommand? PostMessageCommand
    {
        get => (ICommand?)GetValue(PostMessageCommandProperty);
        set => SetValue(PostMessageCommandProperty, value);
    }

    public BarComposerView()
    {
        InitializeComponent();
    }
}
