using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class OrderEntryPage : ContentPage
{
    private readonly OrderEntryViewModel _viewModel;

    public OrderEntryPage(OrderEntryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
