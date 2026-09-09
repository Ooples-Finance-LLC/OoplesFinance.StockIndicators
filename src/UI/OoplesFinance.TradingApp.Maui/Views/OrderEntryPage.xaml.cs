using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class OrderEntryPage : ContentPage
{
    private OrderEntryViewModel? _viewModel;

    public OrderEntryPage()
    {
        App.LogError("OrderEntryPage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("OrderEntryPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("OrderEntryPage.Constructor", ex);
            throw;
        }
    }

    public OrderEntryPage(OrderEntryViewModel viewModel)
    {
        App.LogError("OrderEntryPage", "DI Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("OrderEntryPage", "DI Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("OrderEntryPage.DIConstructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("OrderEntryPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();

            if (BindingContext is null || _viewModel is null)
            {
                _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<OrderEntryViewModel>();
                if (_viewModel is not null)
                {
                    BindingContext = _viewModel;
                    App.LogError("OrderEntryPage.OnAppearing", "Got ViewModel from DI");
                }
            }

            if (_viewModel is not null)
            {
                await _viewModel.InitializeAsync();
                App.LogError("OrderEntryPage.OnAppearing", "Completed successfully");
            }
        }
        catch (Exception ex)
        {
            App.LogException("OrderEntryPage.OnAppearing", ex);
        }
    }
}
