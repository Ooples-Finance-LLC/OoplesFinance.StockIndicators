using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class PositionsPage : ContentPage
{
    private readonly PositionsViewModel _viewModel;

    public PositionsPage(PositionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPositionsAsync();
    }
}
