using OoplesFinance.TradingApp.Maui.Services;
using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

[QueryProperty(nameof(Symbol), "symbol")]
public partial class ChartPage : ContentPage
{
    private readonly ChartViewModel _viewModel;
    private string? _pendingSymbol;

    public string Symbol
    {
        set
        {
            App.LogError("ChartPage.Symbol", $"Setting symbol to: {value}");
            _pendingSymbol = value;
            if (_viewModel is not null)
            {
                _viewModel.Symbol = value;
            }
        }
    }

    // Parameterless constructor for Shell navigation
    public ChartPage() : this(
        App.Current?.Handler?.MauiContext?.Services.GetService<ChartViewModel>()
        ?? CreateFallbackViewModel())
    {
        App.LogError("ChartPage", "Parameterless constructor called");
    }

    private static ChartViewModel CreateFallbackViewModel()
    {
        var marketDataService = new MarketDataService();
        var indicatorService = new IndicatorService();
        var aiService = new AIAnalysisService(indicatorService);
        return new ChartViewModel(marketDataService, indicatorService, aiService);
    }

    public ChartPage(ChartViewModel viewModel)
    {
        App.LogError("ChartPage", "Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;

            if (!string.IsNullOrEmpty(_pendingSymbol))
            {
                _viewModel.Symbol = _pendingSymbol;
            }
            App.LogError("ChartPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("ChartPage.Constructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("ChartPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            await _viewModel.LoadDataAsync();
            App.LogError("ChartPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("ChartPage.OnAppearing", ex);
        }
    }
}
