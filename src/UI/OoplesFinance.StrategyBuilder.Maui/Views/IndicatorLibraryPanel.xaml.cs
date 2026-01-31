using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

public partial class IndicatorLibraryPanel : ContentView
{
    public IndicatorLibraryPanel()
    {
        InitializeComponent();
    }

    public IndicatorLibraryPanel(IndicatorLibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
