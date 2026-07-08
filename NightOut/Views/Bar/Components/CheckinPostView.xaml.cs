using System.Windows.Input;

namespace NightOut.Views.Bar.Components;

public partial class CheckinPostView : ContentView
{
    public static readonly BindableProperty LikeCommandProperty =
        BindableProperty.Create(nameof(LikeCommand), typeof(ICommand), typeof(CheckinPostView));

    public static readonly BindableProperty AddFriendCommandProperty =
        BindableProperty.Create(nameof(AddFriendCommand), typeof(ICommand), typeof(CheckinPostView));

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

    public CheckinPostView()
    {
        InitializeComponent();
    }
}
