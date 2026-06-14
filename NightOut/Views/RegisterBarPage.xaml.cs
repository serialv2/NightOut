using NightOut.Models;
using NightOut.ViewModels;

namespace NightOut.Views.Bar
{
    public partial class RegisterBarPage : ContentPage
    {
        private readonly RegisterBarViewModel _vm;

        // À renseigner avant le push pour le mode édition (null = création).
        public Models.Bar EditingBar { get; set; }

        public RegisterBarPage(RegisterBarViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
            _vm.Finished += OnFinished;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitAsync(EditingBar);
        }

        private async void OnFinished(object sender, bool saved)
        {
            await Navigation.PopModalAsync();
        }
    }
}