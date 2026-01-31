using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

/// <summary>
/// Main page for the Strategy Builder application.
/// Contains the three-panel layout: Indicator Library, Canvas, and Properties.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StrategyCanvasViewModel _viewModel;

    public MainPage(StrategyCanvasViewModel viewModel)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }
}
