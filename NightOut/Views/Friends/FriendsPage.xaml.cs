using NightOut.ViewModels;

namespace NightOut.Views.Friends;

public partial class FriendsPage : ContentPage
{
    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FriendsViewModel vm)
        {
            await vm.OnAppearingAsync();
            vm.RebuildOutTonight();
        }
    }

    private void OnAmisClicked(object sender, EventArgs e) => ShowPanel(PanelAmis, BtnAmis);
    private void OnDemandesClicked(object sender, EventArgs e) => ShowPanel(PanelDemandes, BtnDemandes);
    private void OnAjouterClicked(object sender, EventArgs e) => ShowPanel(PanelAjouter, BtnAjouter);
    private void OnGroupesClicked(object sender, EventArgs e) => ShowPanel(PanelGroupes, BtnGroupes);

    private void ShowPanel(View activePanel, Button activeButton)
    {
        PanelAmis.IsVisible = activePanel == PanelAmis;
        PanelDemandes.IsVisible = activePanel == PanelDemandes;
        PanelAjouter.IsVisible = activePanel == PanelAjouter;
        PanelGroupes.IsVisible = activePanel == PanelGroupes;

        ResetButton(BtnAmis);
        ResetButton(BtnDemandes);
        ResetButton(BtnAjouter);
        ResetButton(BtnGroupes);

        activeButton.SetDynamicResource(Button.BackgroundColorProperty, "Accent");
        activeButton.SetDynamicResource(Button.TextColorProperty, "BgDeep");
    }

    private static void ResetButton(Button button)
    {
        button.SetDynamicResource(Button.BackgroundColorProperty, "BgElevated");
        button.SetDynamicResource(Button.TextColorProperty, "TextSecondary");
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is FriendsViewModel vm)
        {
            vm.SearchQuery = e.NewTextValue;
            _ = vm.SearchCommand.ExecuteAsync(null);
        }
    }
}
