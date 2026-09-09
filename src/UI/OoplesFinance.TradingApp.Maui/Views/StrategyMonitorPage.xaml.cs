using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class StrategyMonitorPage : ContentPage
{
    private readonly StrategyMonitorViewModel _viewModel;

    // Parameterless constructor for Shell navigation
    public StrategyMonitorPage() : this(
        App.Current?.Handler?.MauiContext?.Services.GetService<StrategyMonitorViewModel>()
        ?? new StrategyMonitorViewModel(new Services.StrategyMonitorService()))
    {
        App.LogError("StrategyMonitorPage", "Parameterless constructor called");
    }

    public StrategyMonitorPage(StrategyMonitorViewModel viewModel)
    {
        App.LogError("StrategyMonitorPage", "Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("StrategyMonitorPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("StrategyMonitorPage.Constructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("StrategyMonitorPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            await _viewModel.LoadStrategiesAsync();
            App.LogError("StrategyMonitorPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("StrategyMonitorPage.OnAppearing", ex);
        }
    }
}
