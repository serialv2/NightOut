using NightOut.ViewModels;

namespace NightOut.Views.Friends;

public partial class GroupDetailPage : ContentPage, IQueryAttributable
{
    private string? _groupId;

    public GroupDetailPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("groupId", out var value))
        {
            _groupId = Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
            _ = LoadGroupAsync();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadGroupAsync();
    }

    private async Task LoadGroupAsync()
    {
        if (BindingContext is not FriendsViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(_groupId))
        {
            await vm.OnAppearingAsync();
            return;
        }

        await vm.LoadGroupForDetailAsync(_groupId);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnGroupDiscussionClicked(object sender, EventArgs e) => ShowGroupSection(GroupSectionDiscussion, BtnGroupDiscussion);
    private void OnGroupSortiesClicked(object sender, EventArgs e) => ShowGroupSection(GroupSectionSorties, BtnGroupSorties);

    private async void OnShowAllOutingsClicked(object sender, EventArgs e)
    {
        ShowGroupSection(GroupSectionSorties, BtnGroupSorties);

        await Task.Delay(100);
        await GroupScrollView.ScrollToAsync(GroupSectionSorties, ScrollToPosition.Start, true);
    }
    private void OnGroupMediasClicked(object sender, EventArgs e) => ShowGroupSection(GroupSectionMedias, BtnGroupMedias);
    private void OnGroupMembresClicked(object sender, EventArgs e) => ShowGroupSection(GroupSectionMembres, BtnGroupMembres);
    private void OnGroupParametresClicked(object sender, EventArgs e) => ShowGroupSection(GroupSectionParametres, BtnGroupParametres);

    private void ShowGroupSection(View activeSection, Button activeButton)
    {
        GroupSectionDiscussion.IsVisible = activeSection == GroupSectionDiscussion;
        GroupSectionSorties.IsVisible = activeSection == GroupSectionSorties;
        GroupSectionMedias.IsVisible = activeSection == GroupSectionMedias;
        GroupSectionMembres.IsVisible = activeSection == GroupSectionMembres;
        GroupSectionParametres.IsVisible = activeSection == GroupSectionParametres;

        ResetGroupButton(BtnGroupDiscussion);
        ResetGroupButton(BtnGroupSorties);
        ResetGroupButton(BtnGroupMedias);
        ResetGroupButton(BtnGroupMembres);
        ResetGroupButton(BtnGroupParametres);

        activeButton.TextColor = Color.FromArgb("#C084FC");
    }

    private static void ResetGroupButton(Button button)
    {
        button.BackgroundColor = Colors.Transparent;
        button.TextColor = Color.FromArgb("#9CA3AF");
    }
}
