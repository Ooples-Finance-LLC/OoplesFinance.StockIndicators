using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

[QueryProperty(nameof(Symbol), "symbol")]
public partial class PositionDetailPage : ContentPage
{
    private readonly PositionDetailViewModel _viewModel;
    private string? _pendingSymbol;

    public string Symbol
    {
        set
        {
            App.LogError("PositionDetailPage.Symbol", $"Setting symbol to: {value}");
            _pendingSymbol = value;
            if (_viewModel is not null)
            {
                _viewModel.Symbol = value;
            }
        }
    }

    // Parameterless constructor for Shell navigation
    public PositionDetailPage() : this(CreateViewModel())
    {
        App.LogError("PositionDetailPage", "Parameterless constructor called");
    }

    private static PositionDetailViewModel CreateViewModel()
    {
        App.LogError("PositionDetailPage.CreateViewModel", "Creating ViewModel");
        try
        {
            var vm = App.Current?.Handler?.MauiContext?.Services.GetService<PositionDetailViewModel>();
            if (vm is not null)
            {
                App.LogError("PositionDetailPage.CreateViewModel", "Got ViewModel from DI");
                return vm;
            }

            App.LogError("PositionDetailPage.CreateViewModel", "Creating ViewModel manually");
            return new PositionDetailViewModel(
                new Services.PortfolioService(),
                new Services.OrderService());
        }
        catch (Exception ex)
        {
            App.LogException("PositionDetailPage.CreateViewModel", ex);
            throw;
        }
    }

    public PositionDetailPage(PositionDetailViewModel viewModel)
    {
        App.LogError("PositionDetailPage", "Main constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("PositionDetailPage", "InitializeComponent completed");

            _viewModel = viewModel;
            BindingContext = viewModel;

            if (!string.IsNullOrEmpty(_pendingSymbol))
            {
                _viewModel.Symbol = _pendingSymbol;
            }

            App.LogError("PositionDetailPage", "Constructor completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("PositionDetailPage.Constructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("PositionDetailPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            await _viewModel.LoadPositionAsync();
            App.LogError("PositionDetailPage.OnAppearing", "Completed");
        }
        catch (Exception ex)
        {
            App.LogException("PositionDetailPage.OnAppearing", ex);
        }
    }
}
