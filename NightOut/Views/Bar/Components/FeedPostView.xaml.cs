using System.Windows.Input;

namespace NightOut.Views.Bar.Components;

public partial class FeedPostView : ContentView
{
    public static readonly BindableProperty LikeCommandProperty =
        BindableProperty.Create(nameof(LikeCommand), typeof(ICommand), typeof(FeedPostView));

    public static readonly BindableProperty AddFriendCommandProperty =
        BindableProperty.Create(nameof(AddFriendCommand), typeof(ICommand), typeof(FeedPostView));

    public static readonly BindableProperty OpenCommentsCommandProperty =
        BindableProperty.Create(nameof(OpenCommentsCommand), typeof(ICommand), typeof(FeedPostView));

    public ICommand? LikeCommand
    {
        get => (ICommand?)GetValue(LikeCommandProperty);
        set => SetValue(LikeCommandProperty, value);
    }

    public ICommand? AddFriendCommand
    {
        get => (ICommand?)GetValue(AddFriendCommandProperty);
        set => SetValue(AddFriendCommandProperty, value);
    }

    public ICommand? OpenCommentsCommand
    {
        get => (ICommand?)GetValue(OpenCommentsCommandProperty);
        set => SetValue(OpenCommentsCommandProperty, value);
    }

    public FeedPostView()
    {
        InitializeComponent();
    }
}
