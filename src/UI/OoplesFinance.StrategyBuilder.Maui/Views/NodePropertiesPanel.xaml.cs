using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

public partial class NodePropertiesPanel : ContentView
{
    public NodePropertiesPanel()
    {
        InitializeComponent();
    }

    public NodePropertiesPanel(NodePropertiesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
