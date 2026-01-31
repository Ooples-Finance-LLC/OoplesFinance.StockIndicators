using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class StrategyMonitorPage : ContentPage
{
    private readonly StrategyMonitorViewModel _viewModel;

    public StrategyMonitorPage(StrategyMonitorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadStrategiesAsync();
    }
}
