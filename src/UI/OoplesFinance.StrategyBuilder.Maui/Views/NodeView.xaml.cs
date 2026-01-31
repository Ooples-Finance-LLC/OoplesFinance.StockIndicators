using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

/// <summary>
/// Visual representation of a node on the strategy canvas.
/// </summary>
public partial class NodeView : ContentView
{
    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(
            nameof(IsSelected),
            typeof(bool),
            typeof(NodeView),
            false,
            propertyChanged: OnIsSelectedChanged);

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public NodeView()
    {
        InitializeComponent();
    }

    private static void OnIsSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is NodeView nodeView)
        {
            nodeView.UpdateSelectionVisual((bool)newValue);
        }
    }

    private void UpdateSelectionVisual(bool isSelected)
    {
        if (isSelected)
        {
            NodeBorder.Stroke = Application.Current?.Resources.TryGetValue("NodeSelectedBorderColor", out var color) == true
                ? (Color)color
                : Colors.Blue;
            NodeBorder.StrokeThickness = 3;
        }
        else
        {
            NodeBorder.Stroke = Application.Current?.Resources.TryGetValue("NodeBorderColor", out var color) == true
                ? (Color)color
                : Colors.Gray;
            NodeBorder.StrokeThickness = 2;
        }
    }
}
