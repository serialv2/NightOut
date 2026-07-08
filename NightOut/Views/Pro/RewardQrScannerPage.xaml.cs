using NightOut.Services;
using CommunityToolkit.Mvvm.Messaging;
using ZXing.Net.Maui;

namespace NightOut.Views.Pro;

public partial class RewardQrScannerPage : ContentPage
{
    private bool _handled;

    public RewardQrScannerPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _handled = false;
        var status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert(
                "Caméra refusée",
                "Autorise la caméra pour scanner le QR code, ou utilise la saisie manuelle du code.",
                "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };

        BarcodeReader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        BarcodeReader.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_handled)
            return;

        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        _handled = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            BarcodeReader.IsDetecting = false;
            WeakReferenceMessenger.Default.Send(new RewardQrScannedMessage(value.Trim()));
            await Shell.Current.GoToAsync("..");
        });
    }

    private async void OnCloseTapped(object sender, TappedEventArgs e)
    {
        BarcodeReader.IsDetecting = false;
        await Shell.Current.GoToAsync("..");
    }
}
