namespace OoplesFinance.StockIndicators.Builder.VisualBuilder.Canvas;

/// <summary>
/// View model for a node on the canvas.
/// </summary>
public sealed class NodeViewModel
{
    /// <summary>Gets or sets the unique node ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the node type.</summary>
    public NodeType Type { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the position on the canvas.</summary>
    public CanvasPoint Position { get; set; }

    /// <summary>Gets or sets the node size.</summary>
    public CanvasSize Size { get; set; } = new CanvasSize(180, 100);

    /// <summary>Gets or sets the header color.</summary>
    public CanvasColor HeaderColor { get; set; } = CanvasColor.IndicatorColor;

    /// <summary>Gets or sets the input ports.</summary>
    public List<PortViewModel> InputPorts { get; set; } = new();

    /// <summary>Gets or sets the output ports.</summary>
    public List<PortViewModel> OutputPorts { get; set; } = new();

    /// <summary>Gets or sets the configuration parameters.</summary>
    public Dictionary<string, object?> Configuration { get; set; } = new();

    /// <summary>Gets or sets whether the node is collapsed.</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>Gets or sets whether the node is locked.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Gets or sets the validation state.</summary>
    public NodeValidationState ValidationState { get; set; } = NodeValidationState.Valid;

    /// <summary>Gets or sets validation messages.</summary>
    public List<string> ValidationMessages { get; set; } = new();

    /// <summary>Gets the bounds rectangle.</summary>
    public CanvasRect Bounds => new(Position, Size);

    /// <summary>
    /// Updates port positions based on node position and size.
    /// </summary>
    public void UpdatePortPositions()
    {
        var portSpacing = 24.0;
        var headerHeight = 32.0;
        var portStartY = Position.Y + headerHeight + 12;

        // Position input ports on the left
        for (var i = 0; i < InputPorts.Count; i++)
        {
            InputPorts[i].Position = new CanvasPoint(
                Position.X,
                portStartY + i * portSpacing);
        }

        // Position output ports on the right
        for (var i = 0; i < OutputPorts.Count; i++)
        {
            OutputPorts[i].Position = new CanvasPoint(
                Position.X + Size.Width,
                portStartY + i * portSpacing);
        }

        // Update node height based on port count
        var maxPorts = Math.Max(InputPorts.Count, OutputPorts.Count);
        var minHeight = headerHeight + maxPorts * portSpacing + 20;
        if (Size.Height < minHeight)
        {
            Size = new CanvasSize(Size.Width, minHeight);
        }
    }

    /// <summary>
    /// Creates a view model from a graph node.
    /// </summary>
    public static NodeViewModel FromGraphNode(GraphNode graphNode)
    {
        var viewModel = new NodeViewModel
        {
            Id = graphNode.Id,
            Type = graphNode.Type,
            Name = graphNode.Name,
            Description = graphNode.Description,
            Position = new CanvasPoint(graphNode.Position.X, graphNode.Position.Y),
            Configuration = new Dictionary<string, object?>(graphNode.Configuration),
            IsCollapsed = graphNode.IsCollapsed,
            HeaderColor = GetColorForNodeType(graphNode.Type)
        };

        foreach (var port in graphNode.InputPorts)
        {
            viewModel.InputPorts.Add(PortViewModel.FromNodePort(port));
        }

        foreach (var port in graphNode.OutputPorts)
        {
            viewModel.OutputPorts.Add(PortViewModel.FromNodePort(port));
        }

        viewModel.UpdatePortPositions();
        return viewModel;
    }

    /// <summary>
    /// Converts to a graph node.
    /// </summary>
    public GraphNode ToGraphNode()
    {
        var node = new GraphNode
        {
            Id = Id,
            Type = Type,
            Name = Name,
            Description = Description,
            Position = new NodePosition { X = (float)Position.X, Y = (float)Position.Y },
            IsCollapsed = IsCollapsed
        };

        foreach (var kvp in Configuration)
        {
            node.Configuration[kvp.Key] = kvp.Value;
        }

        node.InputPorts.Clear();
        foreach (var port in InputPorts)
        {
            node.InputPorts.Add(port.ToNodePort());
        }

        node.OutputPorts.Clear();
        foreach (var port in OutputPorts)
        {
            node.OutputPorts.Add(port.ToNodePort());
        }

        return node;
    }

    private static CanvasColor GetColorForNodeType(NodeType type)
    {
        return type switch
        {
            NodeType.DataSource => CanvasColor.DataSourceColor,
            NodeType.Parameter => CanvasColor.ParameterColor,
            NodeType.Indicator => CanvasColor.IndicatorColor,
            NodeType.Math => CanvasColor.MathColor,
            NodeType.Logic => CanvasColor.LogicColor,
            NodeType.Signal => CanvasColor.SignalColor,
            NodeType.Action => CanvasColor.ActionColor,
            NodeType.Aggregator => CanvasColor.AggregatorColor,
            NodeType.Filter => CanvasColor.FilterColor,
            NodeType.RiskControl => CanvasColor.RiskColor,
            NodeType.PositionSizing => CanvasColor.SizingColor,
            _ => CanvasColor.Gray
        };
    }
}

/// <summary>
/// View model for a port on a node.
/// </summary>
public sealed class PortViewModel
{
    /// <summary>Gets or sets the port ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type.</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Gets or sets the position on the canvas.</summary>
    public CanvasPoint Position { get; set; }

    /// <summary>Gets or sets whether this is required.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets the default value.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Gets or sets the color.</summary>
    public CanvasColor Color { get; set; } = CanvasColor.InputPortColor;

    /// <summary>Gets or sets whether connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Creates a view model from a node port.
    /// </summary>
    public static PortViewModel FromNodePort(NodePort nodePort)
    {
        return new PortViewModel
        {
            Id = nodePort.Id,
            Name = nodePort.Name,
            DataType = nodePort.DataType.ToString(),
            IsRequired = nodePort.IsRequired,
            DefaultValue = nodePort.DefaultValue
        };
    }

    /// <summary>
    /// Converts to a node port.
    /// </summary>
    public NodePort ToNodePort()
    {
        return new NodePort
        {
            Id = Id,
            Name = Name,
            DataType = Enum.TryParse<PortDataType>(DataType, out var dataType) ? dataType : PortDataType.Any,
            IsRequired = IsRequired,
            DefaultValue = DefaultValue
        };
    }
}

/// <summary>
/// View model for a connection between nodes.
/// </summary>
public sealed class ConnectionViewModel
{
    /// <summary>Gets or sets the connection ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the source node ID.</summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the source port ID.</summary>
    public string SourcePortId { get; set; } = string.Empty;

    /// <summary>Gets or sets the target node ID.</summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the target port ID.</summary>
    public string TargetPortId { get; set; } = string.Empty;

    /// <summary>Gets or sets the line color.</summary>
    public CanvasColor Color { get; set; } = CanvasColor.FromHex("#888888");

    /// <summary>Gets or sets whether the connection is active/highlighted.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Creates a view model from a node connection.
    /// </summary>
    public static ConnectionViewModel FromNodeConnection(NodeConnection connection)
    {
        return new ConnectionViewModel
        {
            Id = connection.Id,
            SourceNodeId = connection.SourceNodeId,
            SourcePortId = connection.SourcePortId,
            TargetNodeId = connection.TargetNodeId,
            TargetPortId = connection.TargetPortId
        };
    }

    /// <summary>
    /// Converts to a node connection.
    /// </summary>
    public NodeConnection ToNodeConnection()
    {
        return new NodeConnection
        {
            Id = Id,
            SourceNodeId = SourceNodeId,
            SourcePortId = SourcePortId,
            TargetNodeId = TargetNodeId,
            TargetPortId = TargetPortId
        };
    }
}

/// <summary>
/// Node validation state.
/// </summary>
public enum NodeValidationState
{
    Valid,
    Warning,
    Error
}
