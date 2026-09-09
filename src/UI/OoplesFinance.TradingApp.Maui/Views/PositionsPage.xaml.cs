using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class PositionsPage : ContentPage
{
    private PositionsViewModel? _viewModel;

    public PositionsPage()
    {
        App.LogError("PositionsPage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("PositionsPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("PositionsPage.Constructor", ex);
            throw;
        }
    }

    public PositionsPage(PositionsViewModel viewModel)
    {
        App.LogError("PositionsPage", "DI Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("PositionsPage", "DI Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("PositionsPage.DIConstructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("PositionsPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();

            if (BindingContext is null || _viewModel is null)
            {
                _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<PositionsViewModel>();
                if (_viewModel is not null)
                {
                    BindingContext = _viewModel;
                    App.LogError("PositionsPage.OnAppearing", "Got ViewModel from DI");
                }
            }

            if (_viewModel is not null)
            {
                await _viewModel.LoadPositionsAsync();
                App.LogError("PositionsPage.OnAppearing", "Completed successfully");
            }
        }
        catch (Exception ex)
        {
            App.LogException("PositionsPage.OnAppearing", ex);
        }
    }
}
