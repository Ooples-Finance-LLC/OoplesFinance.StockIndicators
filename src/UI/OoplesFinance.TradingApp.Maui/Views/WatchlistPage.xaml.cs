using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class WatchlistPage : ContentPage
{
    private WatchlistViewModel? _viewModel;

    public WatchlistPage()
    {
        App.LogError("WatchlistPage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("WatchlistPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("WatchlistPage.Constructor", ex);
            throw;
        }
    }

    public WatchlistPage(WatchlistViewModel viewModel)
    {
        App.LogError("WatchlistPage", "DI Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("WatchlistPage", "DI Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("WatchlistPage.DIConstructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("WatchlistPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();

            if (BindingContext is null || _viewModel is null)
            {
                _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<WatchlistViewModel>();
                if (_viewModel is not null)
                {
                    BindingContext = _viewModel;
                    App.LogError("WatchlistPage.OnAppearing", "Got ViewModel from DI");
                }
            }

            if (_viewModel is not null)
            {
                await _viewModel.LoadDataAsync();
                App.LogError("WatchlistPage.OnAppearing", "Completed successfully");
            }
        }
        catch (Exception ex)
        {
            App.LogException("WatchlistPage.OnAppearing", ex);
        }
    }
}
