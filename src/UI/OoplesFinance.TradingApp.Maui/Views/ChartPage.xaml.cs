using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

[QueryProperty(nameof(Symbol), "symbol")]
public partial class ChartPage : ContentPage
{
    private readonly ChartViewModel _viewModel;

    public string Symbol
    {
        set => _viewModel.Symbol = value;
    }

    public ChartPage(ChartViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }
}
