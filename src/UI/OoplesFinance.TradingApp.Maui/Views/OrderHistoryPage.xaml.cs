using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class OrderHistoryPage : ContentPage
{
    private readonly OrderHistoryViewModel _viewModel;

    // Parameterless constructor for Shell navigation
    public OrderHistoryPage() : this(
        App.Current?.Handler?.MauiContext?.Services.GetService<OrderHistoryViewModel>()
        ?? new OrderHistoryViewModel(new Services.OrderService()))
    {
        App.LogError("OrderHistoryPage", "Parameterless constructor called");
    }

    public OrderHistoryPage(OrderHistoryViewModel viewModel)
    {
        App.LogError("OrderHistoryPage", "Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("OrderHistoryPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("OrderHistoryPage.Constructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("OrderHistoryPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            await _viewModel.LoadOrdersAsync();
            App.LogError("OrderHistoryPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("OrderHistoryPage.OnAppearing", ex);
        }
    }
}
