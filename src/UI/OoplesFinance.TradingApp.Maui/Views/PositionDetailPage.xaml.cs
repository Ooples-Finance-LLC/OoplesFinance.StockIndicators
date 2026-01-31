using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

[QueryProperty(nameof(Symbol), "symbol")]
public partial class PositionDetailPage : ContentPage
{
    private readonly PositionDetailViewModel _viewModel;

    public string Symbol
    {
        set => _viewModel.Symbol = value;
    }

    public PositionDetailPage(PositionDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPositionAsync();
    }
}
