using Microsoft.Extensions.DependencyInjection;
using NightOut.ViewModels;
using System.Text.Json;

namespace NightOut.Views.Map;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;
    private bool _mapReady;

    public MapPage(MapViewModel viewModel)
    {
        InitializeComponent();

        _vm = viewModel;
        BindingContext = _vm;

        _vm.InvokeMapScript = InvokeMapScript;
        _vm.PropertyChanged -= OnViewModelPropertyChanged; // évite l'empilement si la page est recréée
        _vm.PropertyChanged += OnViewModelPropertyChanged;

#if ANDROID
        MapWebView.HandlerChanged += OnWebViewHandlerChanged;
#endif

        // Important : la WebView existe aussi sur iOS.
        // Le fichier map.html doit donc être chargé sur Android ET sur iPhone.
        MapWebView.Navigating += OnMapWebViewNavigating;
        _ = LoadMapAsync();
    }

    // Intercepte les URLs custom émises par le HTML (ex: nightout://navigate?tab=profile).
    private void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Url) || !e.Url.StartsWith("nightout://", StringComparison.OrdinalIgnoreCase)) return;
        e.Cancel = true;

        // Parse manuel : "nightout://navigate?tab=profile" → tab = "profile"
        var query = e.Url.Contains('?') ? e.Url.Split('?')[1] : string.Empty;
        var tab   = query.Split('&')
                         .Select(p => p.Split('='))
                         .FirstOrDefault(kv => kv.Length == 2 && kv[0] == "tab")?[1];

        if (tab == "profile")
        {
            MainThread.BeginInvokeOnMainThread(() =>
                _ = Shell.Current.GoToAsync("//ProfilePage"));
        }
    }

    // Charge la vidéo dans la WebView du visionneur quand le média affiché change.
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MapViewModel.ViewerMedia)) return;

        // TODO: MediaWebView pas encore dans le XAML — à activer quand le visionneur sera intégré
        /*
        var media = _vm.ViewerMedia;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (media is { IsVideo: true } && !string.IsNullOrEmpty(media.PhotoUrl))
            {
                var html = ...
                MediaWebView.Source = new HtmlWebViewSource { Html = html };
            }
            else
            {
                MediaWebView.Source = new HtmlWebViewSource { Html = "<html><body style='background:transparent'></body></html>" };
            }
        });
        */
    }
    private async Task LoadMapAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("map.html");
            using var reader = new StreamReader(stream);

            var html = await reader.ReadToEndAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapWebView.Source = new HtmlWebViewSource
                {
                    Html = html
                };
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] LoadMapAsync Error : {ex}");
        }
    }

#if ANDROID
    private void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
        if (MapWebView.Handler?.PlatformView is not Android.Webkit.WebView webView)
            return;

        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        webView.Settings.AllowFileAccess = true;
        webView.Settings.AllowUniversalAccessFromFileURLs = true;

        webView.AddJavascriptInterface(new AndroidJsBridge(this), "AndroidBridge");
    }
#endif

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _vm.OnDisappearingAsync();
    }

    private async void OnMapNavigated(object sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine("[MapPage] WebView naviguée OK");

            _mapReady = true;

            await MapWebView.EvaluateJavaScriptAsync(@"
                console.log('NightOutMap exists:', !!window.NightOutMap);
                console.log('loadCities exists:', !!window.NightOutMap?.loadCities);
            ");

            await _vm.OnMapReadyAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] OnMapNavigated Error : {ex}");
        }
    }

    private async void OnModerationClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("ModerationPage");

    private async void OnAddBarClicked(object sender, EventArgs e)
    {
        var page = Application.Current?.Handler?.MauiContext?.Services?
            .GetService<NightOut.Views.Bar.RegisterBarPage>();

        if (page is null)
            return;

        // Pour éditer un bar existant : page.EditingBar = barExistant;

        // Rafraîchit la carte si un établissement a bien été enregistré.
        if (page.BindingContext is RegisterBarViewModel rvm)
        {
            rvm.Finished += async (_, saved) =>
            {
                if (saved)
                {
                    await _vm.RefreshBarsAsync();
                }
            };
        }

        await Navigation.PushModalAsync(page);
    }

    // Ferme la fiche du bar (croix ✕). IsBottomSheetVisible pilote l'affichage du bottom sheet.
    private void OnCloseBottomSheetClicked(object sender, EventArgs e)
    {
        _vm.IsBottomSheetVisible = false;
    }

    private void InvokeMapScript(string function, string args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (!_mapReady)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Carte pas prête, JS ignoré : {function}");
                    return;
                }

                string js = function switch
                {
                    "loadBars" =>
                        $"NightOutMap.loadBars('{EscapeJs(args)}');",

                    "loadEvents" =>
                        $"NightOutMap.loadEvents('{EscapeJs(args)}');",

                    "loadFriends" =>
                        $"NightOutMap.loadFriends('{EscapeJs(args)}');",

                    "loadCities" =>
                        $"NightOutMap.loadCities('{EscapeJs(args)}');",

                    "loadCategoryFilters" =>
                        $"NightOutMap.loadCategoryFilters('{EscapeJs(args)}');",

                    "updateGauge" =>
                        BuildUpdateGaugeJs(args),

                    "updateUserPosition" =>
                        BuildUserPositionJs(args),

                    "centerOn" =>
                        BuildCenterJs(args),

                    "selectBar" =>
                        $"NightOutMap.selectBar('{EscapeJs(args)}');",

                    "updateLiveCount" =>
                        $"NightOutMap.updateLiveCount({args});",

                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(js))
                    return;

                System.Diagnostics.Debug.WriteLine($"[MapPage] JS exécuté : {function}");
                await MapWebView.EvaluateJavaScriptAsync(js);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] JS Error : {ex}");
            }
        });
    }
    private void OnNavMapTapped(object? sender, TappedEventArgs e)
    {
        // déjà sur la carte
    }

    private async void OnNavEventsTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//MapPage");

    private async void OnNavAmisTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//FriendsPage");

    private async void OnNavMessagesTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//MessagesPage");

    private async void OnNavProfilTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//ProfilePage");
    public async Task HandleJsMessageAsync(string payload)
    {
        try
        {
            var doc = JsonDocument.Parse(payload);
            var action = doc.RootElement.GetProperty("action").GetString();

            switch (action)
            {
                case "mapReady":
                    System.Diagnostics.Debug.WriteLine("[MapPage] mapReady reçu depuis JS");
                    break;

                case "barSelected":
                    var barId = doc.RootElement.GetProperty("barId").GetString();
                    if (!string.IsNullOrEmpty(barId))
                        await _vm.OnBarSelectedAsync(barId);
                    break;

                case "eventSelected":
                    var eventId = doc.RootElement.GetProperty("eventId").GetString();
                    if (!string.IsNullOrWhiteSpace(eventId))
                        await Shell.Current.GoToAsync($"OfficialEventDetailPage?eventId={Uri.EscapeDataString(eventId)}");
                    break;

                case "cityChanged":
                    var cityId = doc.RootElement.GetProperty("cityId").GetString();
                    if (!string.IsNullOrEmpty(cityId))
                        await _vm.ChangeCityFromHtmlAsync(cityId);
                    break;

                case "toggleSecretMode":
                    var active = doc.RootElement.GetProperty("active").GetBoolean();
                    await _vm.SetSecretModeAsync(active);
                    break;

                case "filterChanged":
                    var filter = doc.RootElement.GetProperty("filter").GetString();
                    if (!string.IsNullOrEmpty(filter))
                        _vm.ApplyFilter(filter);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] JS Message Error : {ex}");
        }
    }

    private static string EscapeJs(string json)
    {
        return json
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    private static string BuildUpdateGaugeJs(string args)
    {
        var parts = args.Split(',');

        if (parts.Length < 2)
            return string.Empty;

        return $"NightOutMap.updateGauge('{parts[0]}', {parts[1]});";
    }

    private static string BuildUserPositionJs(string args)
    {
        var parts = args.Split(',');

        if (parts.Length < 2)
            return string.Empty;

        return $"NightOutMap.updateUserPosition({parts[0]}, {parts[1]});";
    }

    private static string BuildCenterJs(string args)
    {
        var parts = args.Split(',');

        if (parts.Length < 2)
            return string.Empty;

        var zoom = parts.Length > 2 ? parts[2] : "13";

        return $"NightOutMap.centerOn({parts[0]}, {parts[1]}, {zoom});";
    }
}

#if ANDROID
public class AndroidJsBridge : Java.Lang.Object
{
    private readonly MapPage _page;

    public AndroidJsBridge(MapPage page)
    {
        _page = page;
    }

    [Android.Webkit.JavascriptInterface]
    [Java.Interop.Export("postMessage")]
    public void PostMessage(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _page.HandleJsMessageAsync(message);
        });
    }
}
#endif