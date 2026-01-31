using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

public partial class BacktestResultsPage : ContentPage
{
    public BacktestResultsPage()
    {
        InitializeComponent();
    }

    public BacktestResultsPage(BacktestResultsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
