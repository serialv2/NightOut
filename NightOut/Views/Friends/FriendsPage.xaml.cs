using NightOut.ViewModels;

namespace NightOut.Views.Friends;

public partial class FriendsPage : ContentPage
{
    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FriendsViewModel vm)
            _ = vm.OnAppearingAsync();
    }

    private void OnAmisClicked(object sender, EventArgs e) => ShowPanel(PanelAmis, BtnAmis);
    private void OnDemandesClicked(object sender, EventArgs e) => ShowPanel(PanelDemandes, BtnDemandes);
    private void OnRechercheClicked(object sender, EventArgs e) => ShowPanel(PanelRecherche, BtnRecherche);
    private void OnInviterClicked(object sender, EventArgs e) => ShowPanel(PanelInviter, BtnInviter);
    private void OnGroupesClicked(object sender, EventArgs e) => ShowPanel(PanelGroupes, BtnGroupes);

    private void ShowPanel(View activePanel, Button activeButton)
    {
        PanelAmis.IsVisible = activePanel == PanelAmis;
        PanelDemandes.IsVisible = activePanel == PanelDemandes;
        PanelRecherche.IsVisible = activePanel == PanelRecherche;
        PanelInviter.IsVisible = activePanel == PanelInviter;
        PanelGroupes.IsVisible = activePanel == PanelGroupes;

        ResetButton(BtnAmis);
        ResetButton(BtnDemandes);
        ResetButton(BtnRecherche);
        ResetButton(BtnInviter);
        ResetButton(BtnGroupes);

        activeButton.BackgroundColor = Color.FromArgb("#A855F7");
        activeButton.TextColor = Color.FromArgb("#F4EEFF");
    }

    private static void ResetButton(Button button)
    {
        button.BackgroundColor = Colors.Transparent;
        button.TextColor = Color.FromArgb("#555B7A");
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
