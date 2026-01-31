using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class AlertsPage : ContentPage
{
    private readonly AlertsViewModel _viewModel;

    public AlertsPage(AlertsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAlertsAsync();
    }
}
