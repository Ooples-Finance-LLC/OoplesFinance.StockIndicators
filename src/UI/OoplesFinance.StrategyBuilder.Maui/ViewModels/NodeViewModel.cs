using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OoplesFinance.StrategyBuilder.Maui.ViewModels;

/// <summary>
/// ViewModel representing a node on the strategy canvas.
/// </summary>
public partial class NodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _nodeType = "indicator";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 180;

    [ObservableProperty]
    private double _height = 100;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PortViewModel> _inputPorts = new();

    [ObservableProperty]
    private ObservableCollection<PortViewModel> _outputPorts = new();

    [ObservableProperty]
    private ObservableCollection<NodeParameterViewModel> _parameters = new();

    public Color NodeColor => NodeType.ToLowerInvariant() switch
    {
        "indicator" => Color.FromArgb("#264F78"),
        "signal" => Color.FromArgb("#4B2F4B"),
        "action" => Color.FromArgb("#2F4B3E"),
        "datasource" => Color.FromArgb("#4B3E2F"),
        _ => Color.FromArgb("#2D2D30")
    };

    public Color HeaderColor => NodeType.ToLowerInvariant() switch
    {
        "indicator" => Color.FromArgb("#1E3A5F"),
        "signal" => Color.FromArgb("#3A1E3A"),
        "action" => Color.FromArgb("#1E3A2A"),
        "datasource" => Color.FromArgb("#3A2A1E"),
        _ => Color.FromArgb("#1E1E1E")
    };

    public bool ShowPreview => !string.IsNullOrEmpty(PreviewText);
    public bool HasIcon => false; // TODO: Implement icons
    public string IconSource => string.Empty;
}

/// <summary>
/// ViewModel representing a port (input or output) on a node.
/// </summary>
public partial class PortViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _dataType = string.Empty;

    [ObservableProperty]
    private bool _isInput;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private NodeViewModel? _parentNode;

    public ICommand? TapCommand { get; set; }
    public ICommand? DragStartCommand { get; set; }
}

/// <summary>
/// ViewModel representing a parameter that can be configured on a node.
/// </summary>
public partial class NodeParameterViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _parameterType = "int";

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private object? _defaultValue;

    [ObservableProperty]
    private object? _minValue;

    [ObservableProperty]
    private object? _maxValue;

    [ObservableProperty]
    private ObservableCollection<string> _options = new();

    public bool IsNumeric => ParameterType is "int" or "double" or "decimal";
    public bool IsEnum => ParameterType == "enum" && Options.Count > 0;
    public bool IsBool => ParameterType == "bool";

    public bool BoolValue
    {
        get => Value is true;
        set => Value = value;
    }

    public Keyboard KeyboardType => ParameterType switch
    {
        "int" => Keyboard.Numeric,
        "double" or "decimal" => Keyboard.Numeric,
        _ => Keyboard.Default
    };
}

/// <summary>
/// ViewModel representing a connection between two ports.
/// </summary>
public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private NodeViewModel? _sourceNode;

    [ObservableProperty]
    private PortViewModel? _sourcePort;

    [ObservableProperty]
    private NodeViewModel? _targetNode;

    [ObservableProperty]
    private PortViewModel? _targetPort;

    [ObservableProperty]
    private string _dataType = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
