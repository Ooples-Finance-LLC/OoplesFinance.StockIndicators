namespace OoplesFinance.StockIndicators.Builder.VisualBuilder.Canvas;

using OoplesFinance.StockIndicators.Builder.VisualBuilder.NodeTypes;

/// <summary>
/// Controller for the visual strategy canvas.
/// Handles user commands and synchronization with the underlying strategy.
/// </summary>
public sealed class CanvasController
{
    private readonly CanvasState _state;
    private readonly IndicatorLibrary _indicatorLibrary;
    private readonly Stack<ICanvasCommand> _undoStack = new();
    private readonly Stack<ICanvasCommand> _redoStack = new();
    private StrategyDefinition? _strategy;

    /// <summary>
    /// Initializes a new instance of the CanvasController class.
    /// </summary>
    public CanvasController(CanvasState state, IndicatorLibrary indicatorLibrary)
    {
        _state = state;
        _indicatorLibrary = indicatorLibrary;
    }

    /// <summary>Gets the canvas state.</summary>
    public CanvasState State => _state;

    /// <summary>Gets the current strategy.</summary>
    public StrategyDefinition? Strategy => _strategy;

    /// <summary>Gets whether there are commands to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether there are commands to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Event raised when the strategy changes.</summary>
    public event EventHandler<StrategyDefinition>? StrategyChanged;

    /// <summary>Event raised when undo/redo state changes.</summary>
    public event EventHandler? UndoRedoStateChanged;

    /// <summary>
    /// Creates a new strategy.
    /// </summary>
    public void NewStrategy(string name = "New Strategy")
    {
        _strategy = new StrategyDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = string.Empty,
            Version = "1.0.0"
        };

        ClearCanvas();
        _undoStack.Clear();
        _redoStack.Clear();
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads a strategy onto the canvas.
    /// </summary>
    public void LoadStrategy(StrategyDefinition strategy)
    {
        _strategy = strategy;
        ClearCanvas();

        // Convert graph nodes to view models
        foreach (var graphNode in strategy.Graph.Nodes)
        {
            var viewModel = NodeViewModel.FromGraphNode(graphNode);
            _state.AddNode(viewModel);
        }

        // Convert connections
        foreach (var connection in strategy.Graph.Connections)
        {
            var viewModel = ConnectionViewModel.FromNodeConnection(connection);
            _state.AddConnection(viewModel);
        }

        // Update port connection states
        UpdatePortConnectionStates();

        _state.FitToView();
        _undoStack.Clear();
        _redoStack.Clear();
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves the current canvas to the strategy.
    /// </summary>
    public StrategyDefinition SaveStrategy()
    {
        if (_strategy is null)
        {
            _strategy = new StrategyDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Untitled Strategy",
                Version = "1.0.0"
            };
        }

        _strategy.Graph.Nodes.Clear();
        _strategy.Graph.Connections.Clear();

        foreach (var nodeVm in _state.Nodes)
        {
            _strategy.Graph.Nodes.Add(nodeVm.ToGraphNode());
        }

        foreach (var connVm in _state.Connections)
        {
            _strategy.Graph.Connections.Add(connVm.ToNodeConnection());
        }

        StrategyChanged?.Invoke(this, _strategy);
        return _strategy;
    }

    /// <summary>
    /// Adds a node at the specified position.
    /// </summary>
    public NodeViewModel AddNode(NodeType type, string name, CanvasPoint position)
    {
        var node = new NodeViewModel
        {
            Type = type,
            Name = name,
            Position = _state.Theme.SnapToGrid ? _state.SnapToGrid(position) : position
        };

        // Set up default ports based on node type
        ConfigureDefaultPorts(node);

        var command = new AddNodeCommand(_state, node);
        ExecuteCommand(command);

        return node;
    }

    /// <summary>
    /// Adds an indicator node.
    /// </summary>
    public NodeViewModel AddIndicatorNode(string indicatorName, CanvasPoint position)
    {
        var graphNode = _indicatorLibrary.CreateNode(indicatorName);
        var node = NodeViewModel.FromGraphNode(graphNode);
        node.Position = _state.Theme.SnapToGrid ? _state.SnapToGrid(position) : position;
        node.UpdatePortPositions();

        var command = new AddNodeCommand(_state, node);
        ExecuteCommand(command);

        return node;
    }

    /// <summary>
    /// Removes selected nodes.
    /// </summary>
    public void RemoveSelectedNodes()
    {
        if (_state.SelectedNodeCount == 0) return;

        var nodeIds = _state.SelectedNodeIds.ToList();
        var command = new CompositeCommand(
            nodeIds.Select(id => new RemoveNodeCommand(_state, id)).ToArray());

        ExecuteCommand(command);
    }

    /// <summary>
    /// Duplicates selected nodes.
    /// </summary>
    public void DuplicateSelectedNodes()
    {
        if (_state.SelectedNodeCount == 0) return;

        var offset = new CanvasPoint(40, 40);
        var nodeMapping = new Dictionary<string, string>(); // Old ID -> New ID
        var commands = new List<ICanvasCommand>();

        foreach (var nodeId in _state.SelectedNodeIds)
        {
            var original = _state.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (original is null) continue;

            var duplicate = new NodeViewModel
            {
                Type = original.Type,
                Name = original.Name + " (Copy)",
                Description = original.Description,
                Position = original.Position + offset,
                Size = original.Size,
                HeaderColor = original.HeaderColor,
                Configuration = new Dictionary<string, object?>(original.Configuration)
            };

            foreach (var port in original.InputPorts)
            {
                duplicate.InputPorts.Add(new PortViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = port.Name,
                    DataType = port.DataType,
                    IsRequired = port.IsRequired,
                    DefaultValue = port.DefaultValue,
                    Color = port.Color
                });
            }

            foreach (var port in original.OutputPorts)
            {
                duplicate.OutputPorts.Add(new PortViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = port.Name,
                    DataType = port.DataType,
                    Color = port.Color
                });
            }

            duplicate.UpdatePortPositions();
            nodeMapping[original.Id] = duplicate.Id;
            commands.Add(new AddNodeCommand(_state, duplicate));
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(new CompositeCommand(commands.ToArray()));
            _state.ClearSelection();
            foreach (var newId in nodeMapping.Values)
            {
                _state.SelectNode(newId, true);
            }
        }
    }

    /// <summary>
    /// Connects two nodes.
    /// </summary>
    public bool Connect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        var connection = new ConnectionViewModel
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };

        var command = new AddConnectionCommand(_state, connection);
        ExecuteCommand(command);
        UpdatePortConnectionStates();
        return true;
    }

    /// <summary>
    /// Removes a connection.
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        var command = new RemoveConnectionCommand(_state, connectionId);
        ExecuteCommand(command);
        UpdatePortConnectionStates();
    }

    /// <summary>
    /// Updates a node's configuration.
    /// </summary>
    public void UpdateNodeConfiguration(string nodeId, Dictionary<string, object?> configuration)
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var command = new UpdateNodeConfigCommand(_state, nodeId, node.Configuration, configuration);
        ExecuteCommand(command);
    }

    /// <summary>
    /// Aligns selected nodes.
    /// </summary>
    public void AlignSelectedNodes(AlignmentType alignment)
    {
        if (_state.SelectedNodeCount < 2) return;

        var selectedNodes = _state.Nodes.Where(n => _state.IsNodeSelected(n.Id)).ToList();
        if (selectedNodes.Count < 2) return;

        var commands = new List<ICanvasCommand>();

        switch (alignment)
        {
            case AlignmentType.Left:
                var leftX = selectedNodes.Min(n => n.Position.X);
                foreach (var node in selectedNodes)
                {
                    if (Math.Abs(node.Position.X - leftX) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(leftX, node.Position.Y)));
                    }
                }
                break;

            case AlignmentType.Right:
                var rightX = selectedNodes.Max(n => n.Position.X + n.Size.Width);
                foreach (var node in selectedNodes)
                {
                    var targetX = rightX - node.Size.Width;
                    if (Math.Abs(node.Position.X - targetX) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(targetX, node.Position.Y)));
                    }
                }
                break;

            case AlignmentType.Top:
                var topY = selectedNodes.Min(n => n.Position.Y);
                foreach (var node in selectedNodes)
                {
                    if (Math.Abs(node.Position.Y - topY) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(node.Position.X, topY)));
                    }
                }
                break;

            case AlignmentType.Bottom:
                var bottomY = selectedNodes.Max(n => n.Position.Y + n.Size.Height);
                foreach (var node in selectedNodes)
                {
                    var targetY = bottomY - node.Size.Height;
                    if (Math.Abs(node.Position.Y - targetY) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(node.Position.X, targetY)));
                    }
                }
                break;

            case AlignmentType.CenterHorizontal:
                var centerX = selectedNodes.Average(n => n.Position.X + n.Size.Width / 2);
                foreach (var node in selectedNodes)
                {
                    var targetX = centerX - node.Size.Width / 2;
                    if (Math.Abs(node.Position.X - targetX) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(targetX, node.Position.Y)));
                    }
                }
                break;

            case AlignmentType.CenterVertical:
                var centerY = selectedNodes.Average(n => n.Position.Y + n.Size.Height / 2);
                foreach (var node in selectedNodes)
                {
                    var targetY = centerY - node.Size.Height / 2;
                    if (Math.Abs(node.Position.Y - targetY) > 0.1)
                    {
                        commands.Add(new MoveNodeCommand(_state, node.Id,
                            node.Position, new CanvasPoint(node.Position.X, targetY)));
                    }
                }
                break;
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(new CompositeCommand(commands.ToArray()));
        }
    }

    /// <summary>
    /// Distributes selected nodes evenly.
    /// </summary>
    public void DistributeSelectedNodes(DistributionType distribution)
    {
        if (_state.SelectedNodeCount < 3) return;

        var selectedNodes = _state.Nodes
            .Where(n => _state.IsNodeSelected(n.Id))
            .OrderBy(n => distribution == DistributionType.Horizontal ? n.Position.X : n.Position.Y)
            .ToList();

        if (selectedNodes.Count < 3) return;

        var commands = new List<ICanvasCommand>();

        if (distribution == DistributionType.Horizontal)
        {
            var minX = selectedNodes.First().Position.X;
            var maxX = selectedNodes.Last().Position.X;
            var spacing = (maxX - minX) / (selectedNodes.Count - 1);

            for (var i = 1; i < selectedNodes.Count - 1; i++)
            {
                var node = selectedNodes[i];
                var targetX = minX + i * spacing;
                if (Math.Abs(node.Position.X - targetX) > 0.1)
                {
                    commands.Add(new MoveNodeCommand(_state, node.Id,
                        node.Position, new CanvasPoint(targetX, node.Position.Y)));
                }
            }
        }
        else
        {
            var minY = selectedNodes.First().Position.Y;
            var maxY = selectedNodes.Last().Position.Y;
            var spacing = (maxY - minY) / (selectedNodes.Count - 1);

            for (var i = 1; i < selectedNodes.Count - 1; i++)
            {
                var node = selectedNodes[i];
                var targetY = minY + i * spacing;
                if (Math.Abs(node.Position.Y - targetY) > 0.1)
                {
                    commands.Add(new MoveNodeCommand(_state, node.Id,
                        node.Position, new CanvasPoint(node.Position.X, targetY)));
                }
            }
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(new CompositeCommand(commands.ToArray()));
        }
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Redoes the last undone command.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the canvas.
    /// </summary>
    public void ClearCanvas()
    {
        foreach (var node in _state.Nodes.ToList())
        {
            _state.RemoveNode(node.Id);
        }
        _state.ClearSelection();
    }

    private void ExecuteCommand(ICanvasCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureDefaultPorts(NodeViewModel node)
    {
        switch (node.Type)
        {
            case NodeType.DataSource:
                node.OutputPorts.Add(new PortViewModel { Id = "open", Name = "Open", DataType = "PriceSeries" });
                node.OutputPorts.Add(new PortViewModel { Id = "high", Name = "High", DataType = "PriceSeries" });
                node.OutputPorts.Add(new PortViewModel { Id = "low", Name = "Low", DataType = "PriceSeries" });
                node.OutputPorts.Add(new PortViewModel { Id = "close", Name = "Close", DataType = "PriceSeries" });
                node.OutputPorts.Add(new PortViewModel { Id = "volume", Name = "Volume", DataType = "PriceSeries" });
                break;

            case NodeType.Parameter:
                node.OutputPorts.Add(new PortViewModel { Id = "value", Name = "Value", DataType = "Number" });
                break;

            case NodeType.Indicator:
                node.InputPorts.Add(new PortViewModel { Id = "source", Name = "Source", DataType = "PriceSeries", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "value", Name = "Value", DataType = "IndicatorSeries" });
                break;

            case NodeType.Math:
                node.InputPorts.Add(new PortViewModel { Id = "a", Name = "A", DataType = "Number", IsRequired = true });
                node.InputPorts.Add(new PortViewModel { Id = "b", Name = "B", DataType = "Number" });
                node.OutputPorts.Add(new PortViewModel { Id = "result", Name = "Result", DataType = "Number" });
                break;

            case NodeType.Logic:
                node.InputPorts.Add(new PortViewModel { Id = "a", Name = "A", DataType = "Number", IsRequired = true });
                node.InputPorts.Add(new PortViewModel { Id = "b", Name = "B", DataType = "Number", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "result", Name = "Result", DataType = "Boolean" });
                break;

            case NodeType.Signal:
                node.InputPorts.Add(new PortViewModel { Id = "condition", Name = "Condition", DataType = "Boolean", IsRequired = true });
                node.InputPorts.Add(new PortViewModel { Id = "strength", Name = "Strength", DataType = "Number" });
                node.OutputPorts.Add(new PortViewModel { Id = "signal", Name = "Signal", DataType = "Signal" });
                break;

            case NodeType.Action:
                node.InputPorts.Add(new PortViewModel { Id = "trigger", Name = "Trigger", DataType = "Signal", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "order", Name = "Order", DataType = "Signal" });
                break;

            case NodeType.Aggregator:
                node.InputPorts.Add(new PortViewModel { Id = "signal1", Name = "Signal 1", DataType = "Signal" });
                node.InputPorts.Add(new PortViewModel { Id = "signal2", Name = "Signal 2", DataType = "Signal" });
                node.InputPorts.Add(new PortViewModel { Id = "signal3", Name = "Signal 3", DataType = "Signal" });
                node.OutputPorts.Add(new PortViewModel { Id = "result", Name = "Result", DataType = "Signal" });
                break;

            case NodeType.Filter:
                node.InputPorts.Add(new PortViewModel { Id = "input", Name = "Input", DataType = "Signal", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "output", Name = "Output", DataType = "Signal" });
                break;

            case NodeType.RiskControl:
                node.InputPorts.Add(new PortViewModel { Id = "signal", Name = "Signal", DataType = "Signal", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "approved", Name = "Approved", DataType = "Signal" });
                break;

            case NodeType.PositionSizing:
                node.InputPorts.Add(new PortViewModel { Id = "signal", Name = "Signal", DataType = "Signal", IsRequired = true });
                node.OutputPorts.Add(new PortViewModel { Id = "sized", Name = "Sized Signal", DataType = "Signal" });
                break;
        }

        node.UpdatePortPositions();
    }

    private void UpdatePortConnectionStates()
    {
        foreach (var node in _state.Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                port.IsConnected = _state.Connections.Any(c =>
                    c.TargetNodeId == node.Id && c.TargetPortId == port.Id);
            }

            foreach (var port in node.OutputPorts)
            {
                port.IsConnected = _state.Connections.Any(c =>
                    c.SourceNodeId == node.Id && c.SourcePortId == port.Id);
            }
        }
    }
}

/// <summary>
/// Node alignment types.
/// </summary>
public enum AlignmentType
{
    Left,
    Right,
    Top,
    Bottom,
    CenterHorizontal,
    CenterVertical
}

/// <summary>
/// Node distribution types.
/// </summary>
public enum DistributionType
{
    Horizontal,
    Vertical
}

#region Commands

/// <summary>
/// Interface for undoable canvas commands.
/// </summary>
public interface ICanvasCommand
{
    void Execute();
    void Undo();
}

/// <summary>
/// Command to add a node.
/// </summary>
public sealed class AddNodeCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly NodeViewModel _node;

    public AddNodeCommand(CanvasState state, NodeViewModel node)
    {
        _state = state;
        _node = node;
    }

    public void Execute() => _state.AddNode(_node);
    public void Undo() => _state.RemoveNode(_node.Id);
}

/// <summary>
/// Command to remove a node.
/// </summary>
public sealed class RemoveNodeCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly string _nodeId;
    private NodeViewModel? _removedNode;
    private List<ConnectionViewModel>? _removedConnections;

    public RemoveNodeCommand(CanvasState state, string nodeId)
    {
        _state = state;
        _nodeId = nodeId;
    }

    public void Execute()
    {
        _removedNode = _state.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        _removedConnections = _state.Connections
            .Where(c => c.SourceNodeId == _nodeId || c.TargetNodeId == _nodeId)
            .ToList();
        _state.RemoveNode(_nodeId);
    }

    public void Undo()
    {
        if (_removedNode is not null)
        {
            _state.AddNode(_removedNode);
        }
        if (_removedConnections is not null)
        {
            foreach (var conn in _removedConnections)
            {
                _state.AddConnection(conn);
            }
        }
    }
}

/// <summary>
/// Command to add a connection.
/// </summary>
public sealed class AddConnectionCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly ConnectionViewModel _connection;

    public AddConnectionCommand(CanvasState state, ConnectionViewModel connection)
    {
        _state = state;
        _connection = connection;
    }

    public void Execute() => _state.AddConnection(_connection);
    public void Undo() => _state.RemoveConnection(_connection.Id);
}

/// <summary>
/// Command to remove a connection.
/// </summary>
public sealed class RemoveConnectionCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly string _connectionId;
    private ConnectionViewModel? _removedConnection;

    public RemoveConnectionCommand(CanvasState state, string connectionId)
    {
        _state = state;
        _connectionId = connectionId;
    }

    public void Execute()
    {
        _removedConnection = _state.Connections.FirstOrDefault(c => c.Id == _connectionId);
        _state.RemoveConnection(_connectionId);
    }

    public void Undo()
    {
        if (_removedConnection is not null)
        {
            _state.AddConnection(_removedConnection);
        }
    }
}

/// <summary>
/// Command to move a node.
/// </summary>
public sealed class MoveNodeCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly string _nodeId;
    private readonly CanvasPoint _fromPosition;
    private readonly CanvasPoint _toPosition;

    public MoveNodeCommand(CanvasState state, string nodeId, CanvasPoint fromPosition, CanvasPoint toPosition)
    {
        _state = state;
        _nodeId = nodeId;
        _fromPosition = fromPosition;
        _toPosition = toPosition;
    }

    public void Execute()
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null)
        {
            node.Position = _toPosition;
            node.UpdatePortPositions();
        }
    }

    public void Undo()
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null)
        {
            node.Position = _fromPosition;
            node.UpdatePortPositions();
        }
    }
}

/// <summary>
/// Command to update node configuration.
/// </summary>
public sealed class UpdateNodeConfigCommand : ICanvasCommand
{
    private readonly CanvasState _state;
    private readonly string _nodeId;
    private readonly Dictionary<string, object?> _oldConfig;
    private readonly Dictionary<string, object?> _newConfig;

    public UpdateNodeConfigCommand(CanvasState state, string nodeId,
        Dictionary<string, object?> oldConfig, Dictionary<string, object?> newConfig)
    {
        _state = state;
        _nodeId = nodeId;
        _oldConfig = new Dictionary<string, object?>(oldConfig);
        _newConfig = new Dictionary<string, object?>(newConfig);
    }

    public void Execute()
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null)
        {
            node.Configuration = new Dictionary<string, object?>(_newConfig);
        }
    }

    public void Undo()
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null)
        {
            node.Configuration = new Dictionary<string, object?>(_oldConfig);
        }
    }
}

/// <summary>
/// Composite command that groups multiple commands.
/// </summary>
public sealed class CompositeCommand : ICanvasCommand
{
    private readonly ICanvasCommand[] _commands;

    public CompositeCommand(params ICanvasCommand[] commands)
    {
        _commands = commands;
    }

    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        // Undo in reverse order
        for (var i = _commands.Length - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}

#endregion
