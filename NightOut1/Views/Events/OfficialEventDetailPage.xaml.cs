using NightOut.ViewModels;

namespace NightOut.Views.Events;

public partial class OfficialEventDetailPage : ContentPage, IQueryAttributable
{
    private readonly OfficialEventDetailViewModel _viewModel;

    public OfficialEventDetailPage(OfficialEventDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("eventId", out var value) && value is string eventId)
            await _viewModel.LoadAsync(eventId);
    }
}
