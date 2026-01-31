using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OoplesFinance.StrategyBuilder.Maui.Views;

namespace OoplesFinance.StrategyBuilder.Maui.ViewModels;

/// <summary>
/// Main ViewModel for the Strategy Canvas, managing nodes, connections, and user interactions.
/// </summary>
public partial class StrategyCanvasViewModel : ObservableObject
{
    private readonly IStrategyService _strategyService;
    private readonly IIndicatorLibraryService _indicatorLibraryService;
    private readonly INodeConnectionService _connectionService;
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly IBacktestService _backtestService;

    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    [ObservableProperty]
    private string _strategyName = "New Strategy";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _validationStatus = "Not validated";

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private ObservableCollection<NodeViewModel> _nodes = new();

    [ObservableProperty]
    private ObservableCollection<ConnectionViewModel> _connections = new();

    [ObservableProperty]
    private ObservableCollection<IndicatorCategoryViewModel> _indicatorCategories = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private Color _selectedNodeColor = Colors.Gray;

    public int NodeCount => Nodes.Count;
    public int ConnectionCount => Connections.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasSelectedNode => SelectedNode is not null;

    public StrategyCanvasViewModel(
        IStrategyService strategyService,
        IIndicatorLibraryService indicatorLibraryService,
        INodeConnectionService connectionService,
        ICodeGenerationService codeGenerationService,
        IBacktestService backtestService)
    {
        _strategyService = strategyService;
        _indicatorLibraryService = indicatorLibraryService;
        _connectionService = connectionService;
        _codeGenerationService = codeGenerationService;
        _backtestService = backtestService;
    }

    public async Task InitializeAsync()
    {
        StatusMessage = "Loading indicator library...";
        await LoadIndicatorLibraryAsync();
        StatusMessage = "Ready";
    }

    public void Cleanup()
    {
        // Cleanup resources
    }

    #region Indicator Library

    private async Task LoadIndicatorLibraryAsync()
    {
        var categories = await _indicatorLibraryService.GetCategoriesAsync();
        IndicatorCategories = new ObservableCollection<IndicatorCategoryViewModel>(categories);
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterIndicators(value);
    }

    private void FilterIndicators(string searchText)
    {
        foreach (var category in IndicatorCategories)
        {
            foreach (var indicator in category.Indicators)
            {
                indicator.IsVisible = string.IsNullOrEmpty(searchText) ||
                    indicator.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    indicator.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    #endregion

    #region Node Management

    [RelayCommand]
    private void AddNode(AddNodeEventArgs args)
    {
        var node = new NodeViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = args.IndicatorName,
            NodeType = args.NodeType,
            X = args.X,
            Y = args.Y,
            Width = 180,
            Height = 100
        };

        // Initialize ports based on indicator type
        InitializeNodePorts(node, args.IndicatorName);

        Nodes.Add(node);
        OnPropertyChanged(nameof(NodeCount));

        var action = new AddNodeAction(this, node);
        PushUndoAction(action);

        StatusMessage = $"Added node: {node.Name}";
    }

    private static void InitializeNodePorts(NodeViewModel node, string indicatorName)
    {
        // Common input: Price data
        node.InputPorts.Add(new PortViewModel
        {
            Name = "Price",
            DataType = "decimal[]",
            IsInput = true,
            ParentNode = node
        });

        // Output based on indicator type
        node.OutputPorts.Add(new PortViewModel
        {
            Name = "Value",
            DataType = "decimal[]",
            IsInput = false,
            ParentNode = node
        });

        // Add signal output for indicators that generate signals
        if (indicatorName.Contains("Signal") || indicatorName.Contains("Crossover"))
        {
            node.OutputPorts.Add(new PortViewModel
            {
                Name = "Signal",
                DataType = "signal",
                IsInput = false,
                ParentNode = node
            });
        }
    }

    [RelayCommand]
    private void MoveNode(NodeMovedEventArgs args)
    {
        var action = new MoveNodeAction(args.Node, args.OldPosition, args.NewPosition);
        PushUndoAction(action);
    }

    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (SelectedNode is null) return;

        // Remove all connections to/from this node
        var connectionsToRemove = Connections
            .Where(c => c.SourceNode == SelectedNode || c.TargetNode == SelectedNode)
            .ToList();

        foreach (var connection in connectionsToRemove)
        {
            Connections.Remove(connection);
        }

        var action = new DeleteNodeAction(this, SelectedNode, connectionsToRemove);
        PushUndoAction(action);

        Nodes.Remove(SelectedNode);
        SelectedNode = null;

        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(ConnectionCount));
        StatusMessage = "Node deleted";
    }

    partial void OnSelectedNodeChanged(NodeViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedNode));
        if (value is not null)
        {
            SelectedNodeColor = GetNodeTypeColor(value.NodeType);
        }
    }

    private static Color GetNodeTypeColor(string nodeType)
    {
        return nodeType.ToLowerInvariant() switch
        {
            "indicator" => Color.FromArgb("#264F78"),
            "signal" => Color.FromArgb("#4B2F4B"),
            "action" => Color.FromArgb("#2F4B3E"),
            "datasource" => Color.FromArgb("#4B3E2F"),
            _ => Color.FromArgb("#2D2D30")
        };
    }

    #endregion

    #region Connection Management

    [RelayCommand]
    private void CreateConnection(ConnectionCreatedEventArgs args)
    {
        var connection = new ConnectionViewModel
        {
            Id = Guid.NewGuid().ToString(),
            SourceNode = args.SourcePort.ParentNode,
            SourcePort = args.SourcePort,
            TargetNode = args.TargetPort.ParentNode,
            TargetPort = args.TargetPort,
            DataType = args.SourcePort.DataType
        };

        Connections.Add(connection);
        OnPropertyChanged(nameof(ConnectionCount));

        var action = new AddConnectionAction(this, connection);
        PushUndoAction(action);

        StatusMessage = $"Connected {args.SourcePort.Name} to {args.TargetPort.Name}";
    }

    [RelayCommand]
    private void DeleteConnection(ConnectionViewModel connection)
    {
        var action = new DeleteConnectionAction(this, connection);
        PushUndoAction(action);

        Connections.Remove(connection);
        OnPropertyChanged(nameof(ConnectionCount));
        StatusMessage = "Connection deleted";
    }

    #endregion

    #region File Operations

    [RelayCommand]
    private async Task NewStrategyAsync()
    {
        if (Nodes.Count > 0)
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "New Strategy",
                "Are you sure you want to create a new strategy? Unsaved changes will be lost.",
                "Yes", "No");

            if (!confirm) return;
        }

        Nodes.Clear();
        Connections.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        StrategyName = "New Strategy";
        SelectedNode = null;

        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(ConnectionCount));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

        StatusMessage = "Created new strategy";
    }

    [RelayCommand]
    private async Task OpenStrategyAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".ostrat" } },
                { DevicePlatform.macOS, new[] { "ostrat" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.Android, new[] { "application/octet-stream" } }
            }),
            PickerTitle = "Open Strategy"
        });

        if (result is null) return;

        StatusMessage = "Loading strategy...";

        var strategy = await _strategyService.LoadStrategyAsync(result.FullPath);
        if (strategy is not null)
        {
            Nodes = new ObservableCollection<NodeViewModel>(strategy.Nodes);
            Connections = new ObservableCollection<ConnectionViewModel>(strategy.Connections);
            StrategyName = strategy.Name;

            OnPropertyChanged(nameof(NodeCount));
            OnPropertyChanged(nameof(ConnectionCount));
            StatusMessage = $"Loaded: {StrategyName}";
        }
        else
        {
            StatusMessage = "Failed to load strategy";
        }
    }

    [RelayCommand]
    private async Task SaveStrategyAsync()
    {
        var strategy = new StrategyDefinition
        {
            Name = StrategyName,
            Nodes = Nodes.ToList(),
            Connections = Connections.ToList()
        };

        var path = await _strategyService.SaveStrategyAsync(strategy);
        StatusMessage = path is not null ? $"Saved: {path}" : "Save cancelled";
    }

    #endregion

    #region Validation & Code Generation

    [RelayCommand]
    private async Task ValidateAsync()
    {
        StatusMessage = "Validating strategy...";

        var result = await _strategyService.ValidateStrategyAsync(
            Nodes.ToList(),
            Connections.ToList());

        if (result.IsValid)
        {
            ValidationStatus = "Valid";
            StatusMessage = "Strategy is valid";
        }
        else
        {
            ValidationStatus = $"Invalid ({result.Errors.Count} errors)";
            StatusMessage = result.Errors.FirstOrDefault() ?? "Validation failed";

            // Highlight problematic nodes
            foreach (var node in Nodes)
            {
                node.HasError = result.ErrorNodes.Contains(node.Id);
            }
        }
    }

    [RelayCommand]
    private async Task GenerateCodeAsync()
    {
        StatusMessage = "Generating code...";

        var code = await _codeGenerationService.GenerateAsync(
            Nodes.ToList(),
            Connections.ToList(),
            StrategyName);

        if (code is not null)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                "Generated Code",
                code,
                "OK");
            StatusMessage = "Code generated successfully";
        }
        else
        {
            StatusMessage = "Code generation failed";
        }
    }

    [RelayCommand]
    private async Task BacktestAsync()
    {
        StatusMessage = "Starting backtest...";

        var result = await _backtestService.RunBacktestAsync(
            Nodes.ToList(),
            Connections.ToList());

        if (result is not null)
        {
            await Shell.Current.GoToAsync(nameof(BacktestResultsPage), new Dictionary<string, object>
            {
                { "Result", result }
            });
            StatusMessage = "Backtest completed";
        }
        else
        {
            StatusMessage = "Backtest failed";
        }
    }

    #endregion

    #region Zoom

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.2, 4.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.2, 0.25);
    }

    [RelayCommand]
    private void FitToView()
    {
        // Handled by the view
    }

    #endregion

    #region Undo/Redo

    private void PushUndoAction(IUndoableAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(ConnectionCount));

        StatusMessage = $"Undid: {action.Description}";
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(NodeCount));
        OnPropertyChanged(nameof(ConnectionCount));

        StatusMessage = $"Redid: {action.Description}";
    }

    #endregion
}

#region Event Args

public class AddNodeEventArgs
{
    public string IndicatorName { get; set; } = string.Empty;
    public string NodeType { get; set; } = "indicator";
    public double X { get; set; }
    public double Y { get; set; }
}

#endregion

#region Undo Actions

public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}

public class AddNodeAction : IUndoableAction
{
    private readonly StrategyCanvasViewModel _viewModel;
    private readonly NodeViewModel _node;

    public string Description => $"Add {_node.Name}";

    public AddNodeAction(StrategyCanvasViewModel viewModel, NodeViewModel node)
    {
        _viewModel = viewModel;
        _node = node;
    }

    public void Undo() => _viewModel.Nodes.Remove(_node);
    public void Redo() => _viewModel.Nodes.Add(_node);
}

public class DeleteNodeAction : IUndoableAction
{
    private readonly StrategyCanvasViewModel _viewModel;
    private readonly NodeViewModel _node;
    private readonly List<ConnectionViewModel> _connections;

    public string Description => $"Delete {_node.Name}";

    public DeleteNodeAction(StrategyCanvasViewModel viewModel, NodeViewModel node, List<ConnectionViewModel> connections)
    {
        _viewModel = viewModel;
        _node = node;
        _connections = connections;
    }

    public void Undo()
    {
        _viewModel.Nodes.Add(_node);
        foreach (var conn in _connections)
        {
            _viewModel.Connections.Add(conn);
        }
    }

    public void Redo()
    {
        foreach (var conn in _connections)
        {
            _viewModel.Connections.Remove(conn);
        }
        _viewModel.Nodes.Remove(_node);
    }
}

public class MoveNodeAction : IUndoableAction
{
    private readonly NodeViewModel _node;
    private readonly Point _oldPosition;
    private readonly Point _newPosition;

    public string Description => $"Move {_node.Name}";

    public MoveNodeAction(NodeViewModel node, Point oldPosition, Point newPosition)
    {
        _node = node;
        _oldPosition = oldPosition;
        _newPosition = newPosition;
    }

    public void Undo()
    {
        _node.X = _oldPosition.X;
        _node.Y = _oldPosition.Y;
    }

    public void Redo()
    {
        _node.X = _newPosition.X;
        _node.Y = _newPosition.Y;
    }
}

public class AddConnectionAction : IUndoableAction
{
    private readonly StrategyCanvasViewModel _viewModel;
    private readonly ConnectionViewModel _connection;

    public string Description => "Add connection";

    public AddConnectionAction(StrategyCanvasViewModel viewModel, ConnectionViewModel connection)
    {
        _viewModel = viewModel;
        _connection = connection;
    }

    public void Undo() => _viewModel.Connections.Remove(_connection);
    public void Redo() => _viewModel.Connections.Add(_connection);
}

public class DeleteConnectionAction : IUndoableAction
{
    private readonly StrategyCanvasViewModel _viewModel;
    private readonly ConnectionViewModel _connection;

    public string Description => "Delete connection";

    public DeleteConnectionAction(StrategyCanvasViewModel viewModel, ConnectionViewModel connection)
    {
        _viewModel = viewModel;
        _connection = connection;
    }

    public void Undo() => _viewModel.Connections.Add(_connection);
    public void Redo() => _viewModel.Connections.Remove(_connection);
}

#endregion
