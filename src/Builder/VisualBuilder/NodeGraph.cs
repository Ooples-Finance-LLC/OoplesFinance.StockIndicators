namespace OoplesFinance.StockIndicators.Builder.VisualBuilder;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the visual node graph for a trading strategy.
/// Nodes are connected via ports to form a directed acyclic graph (DAG).
/// </summary>
public sealed class NodeGraph
{
    /// <summary>Gets or sets the nodes in the graph.</summary>
    public List<GraphNode> Nodes { get; set; } = new();

    /// <summary>Gets or sets the connections between nodes.</summary>
    public List<NodeConnection> Connections { get; set; } = new();

    /// <summary>Gets or sets the graph metadata.</summary>
    public GraphMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public GraphNode AddNode(NodeType type, double x = 0, double y = 0)
    {
        var node = new GraphNode
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Position = new NodePosition { X = x, Y = y }
        };
        Nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Removes a node and all its connections.
    /// </summary>
    public bool RemoveNode(string nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return false;

        // Remove all connections to/from this node
        Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
        Nodes.Remove(node);
        return true;
    }

    /// <summary>
    /// Connects two nodes via their ports.
    /// </summary>
    public NodeConnection? Connect(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
    {
        var sourceNode = Nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        var targetNode = Nodes.FirstOrDefault(n => n.Id == targetNodeId);

        if (sourceNode is null || targetNode is null) return null;

        // Validate ports exist
        var sourcePort = sourceNode.OutputPorts.FirstOrDefault(p => p.Id == sourcePortId);
        var targetPort = targetNode.InputPorts.FirstOrDefault(p => p.Id == targetPortId);

        if (sourcePort is null || targetPort is null) return null;

        // Check type compatibility
        if (!AreTypesCompatible(sourcePort.DataType, targetPort.DataType)) return null;

        // Check for cycles
        if (WouldCreateCycle(sourceNodeId, targetNodeId)) return null;

        var connection = new NodeConnection
        {
            Id = Guid.NewGuid().ToString(),
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };

        Connections.Add(connection);
        return connection;
    }

    /// <summary>
    /// Disconnects two nodes.
    /// </summary>
    public bool Disconnect(string connectionId)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection is null) return false;
        return Connections.Remove(connection);
    }

    /// <summary>
    /// Gets nodes in topological order for execution.
    /// </summary>
    public IReadOnlyList<GraphNode> GetExecutionOrder()
    {
        var result = new List<GraphNode>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var node in Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                TopologicalSort(node.Id, visited, visiting, result);
            }
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Validates the graph structure.
    /// </summary>
    public GraphValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for disconnected required ports
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts.Where(p => p.IsRequired))
            {
                var hasConnection = Connections.Any(c =>
                    c.TargetNodeId == node.Id && c.TargetPortId == port.Id);
                if (!hasConnection)
                {
                    errors.Add($"Required input port '{port.Name}' on node '{node.Name}' is not connected.");
                }
            }
        }

        // Check for entry points (nodes with no inputs)
        var entryNodes = Nodes.Where(n =>
            !Connections.Any(c => c.TargetNodeId == n.Id) &&
            n.Type is NodeType.DataSource or NodeType.Parameter).ToList();

        if (entryNodes.Count == 0)
        {
            errors.Add("Graph has no entry points (DataSource or Parameter nodes).");
        }

        // Check for exit points (signal or action nodes)
        var exitNodes = Nodes.Where(n =>
            n.Type is NodeType.Signal or NodeType.Action).ToList();

        if (exitNodes.Count == 0)
        {
            warnings.Add("Graph has no output nodes (Signal or Action nodes).");
        }

        // Check for isolated nodes
        var isolatedNodes = Nodes.Where(n =>
            !Connections.Any(c => c.SourceNodeId == n.Id || c.TargetNodeId == n.Id)).ToList();

        foreach (var node in isolatedNodes)
        {
            warnings.Add($"Node '{node.Name}' is isolated (not connected to any other node).");
        }

        return new GraphValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Creates a deep copy of the graph.
    /// </summary>
    public NodeGraph Clone()
    {
        var json = JsonSerializer.Serialize(this, GetJsonOptions());
        return JsonSerializer.Deserialize<NodeGraph>(json, GetJsonOptions()) ?? new NodeGraph();
    }

    private void TopologicalSort(string nodeId, HashSet<string> visited, HashSet<string> visiting, List<GraphNode> result)
    {
        if (visited.Contains(nodeId)) return;
        if (visiting.Contains(nodeId)) throw new InvalidOperationException("Cycle detected in graph");

        visiting.Add(nodeId);

        var outgoing = Connections.Where(c => c.SourceNodeId == nodeId);
        foreach (var connection in outgoing)
        {
            TopologicalSort(connection.TargetNodeId, visited, visiting, result);
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);

        var node = Nodes.First(n => n.Id == nodeId);
        result.Add(node);
    }

    private bool WouldCreateCycle(string sourceNodeId, string targetNodeId)
    {
        // Check if there's already a path from target to source
        var visited = new HashSet<string>();
        return HasPath(targetNodeId, sourceNodeId, visited);
    }

    private bool HasPath(string fromNodeId, string toNodeId, HashSet<string> visited)
    {
        if (fromNodeId == toNodeId) return true;
        if (visited.Contains(fromNodeId)) return false;

        visited.Add(fromNodeId);

        var outgoing = Connections.Where(c => c.SourceNodeId == fromNodeId);
        return outgoing.Any(c => HasPath(c.TargetNodeId, toNodeId, visited));
    }

    private static bool AreTypesCompatible(PortDataType source, PortDataType target)
    {
        if (source == target) return true;
        if (target == PortDataType.Any) return true;

        // Numeric type compatibility
        if (target == PortDataType.Decimal && source is PortDataType.Integer or PortDataType.Double)
            return true;
        if (target == PortDataType.Double && source is PortDataType.Integer or PortDataType.Decimal)
            return true;

        return false;
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Represents a single node in the graph.
/// </summary>
public sealed class GraphNode
{
    /// <summary>Gets or sets the unique node identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the node type.</summary>
    public NodeType Type { get; set; }

    /// <summary>Gets or sets the node's display name.</summary>
    public string Name { get; set; } = "Untitled Node";

    /// <summary>Gets or sets the node's description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the visual position.</summary>
    public NodePosition Position { get; set; } = new();

    /// <summary>Gets or sets input ports.</summary>
    public List<NodePort> InputPorts { get; set; } = new();

    /// <summary>Gets or sets output ports.</summary>
    public List<NodePort> OutputPorts { get; set; } = new();

    /// <summary>Gets or sets node-specific configuration.</summary>
    public Dictionary<string, object?> Configuration { get; set; } = new();

    /// <summary>Gets or sets the node's visual style.</summary>
    public NodeStyle Style { get; set; } = new();

    /// <summary>Gets or sets whether the node is collapsed.</summary>
    public bool IsCollapsed { get; set; } = false;

    /// <summary>Gets or sets whether the node is selected.</summary>
    [JsonIgnore]
    public bool IsSelected { get; set; } = false;
}

/// <summary>
/// Types of nodes in the strategy graph.
/// </summary>
public enum NodeType
{
    /// <summary>Data source node (price data, volume, etc.).</summary>
    DataSource,

    /// <summary>Technical indicator node.</summary>
    Indicator,

    /// <summary>Mathematical operation node.</summary>
    Math,

    /// <summary>Comparison/logic node.</summary>
    Logic,

    /// <summary>Signal generation node.</summary>
    Signal,

    /// <summary>Trade action node.</summary>
    Action,

    /// <summary>Parameter/constant node.</summary>
    Parameter,

    /// <summary>Filter/condition node.</summary>
    Filter,

    /// <summary>Aggregation node (combine multiple signals).</summary>
    Aggregator,

    /// <summary>Risk management node.</summary>
    RiskControl,

    /// <summary>Position sizing node.</summary>
    PositionSizing,

    /// <summary>Comment/annotation node.</summary>
    Comment
}

/// <summary>
/// Represents a port on a node for connections.
/// </summary>
public sealed class NodePort
{
    /// <summary>Gets or sets the port identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the port name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type.</summary>
    public PortDataType DataType { get; set; } = PortDataType.Decimal;

    /// <summary>Gets or sets whether this port is required.</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Gets or sets the default value if not connected.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Gets or sets whether multiple connections are allowed.</summary>
    public bool AllowMultiple { get; set; } = false;

    /// <summary>Gets or sets the port description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Data types for port connections.
/// </summary>
public enum PortDataType
{
    /// <summary>Any type (accepts all).</summary>
    Any,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>Integer value.</summary>
    Integer,

    /// <summary>Decimal value.</summary>
    Decimal,

    /// <summary>Double precision value.</summary>
    Double,

    /// <summary>String value.</summary>
    String,

    /// <summary>DateTime value.</summary>
    DateTime,

    /// <summary>Price series (OHLCV data).</summary>
    PriceSeries,

    /// <summary>Indicator series (calculated values).</summary>
    IndicatorSeries,

    /// <summary>Signal value (buy/sell/hold).</summary>
    Signal,

    /// <summary>Trade action.</summary>
    TradeAction,

    /// <summary>List of values.</summary>
    List,

    /// <summary>Custom object type.</summary>
    Object
}

/// <summary>
/// Visual position of a node.
/// </summary>
public sealed class NodePosition
{
    /// <summary>Gets or sets the X coordinate.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the Y coordinate.</summary>
    public double Y { get; set; }
}

/// <summary>
/// Visual style for a node.
/// </summary>
public sealed class NodeStyle
{
    /// <summary>Gets or sets the node width.</summary>
    public double Width { get; set; } = 200;

    /// <summary>Gets or sets the node height.</summary>
    public double Height { get; set; } = 100;

    /// <summary>Gets or sets the header color (hex).</summary>
    public string HeaderColor { get; set; } = "#4A90D9";

    /// <summary>Gets or sets the body color (hex).</summary>
    public string BodyColor { get; set; } = "#2D2D2D";

    /// <summary>Gets or sets the border color (hex).</summary>
    public string BorderColor { get; set; } = "#3D3D3D";

    /// <summary>Gets or sets the text color (hex).</summary>
    public string TextColor { get; set; } = "#FFFFFF";

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Represents a connection between two nodes.
/// </summary>
public sealed class NodeConnection
{
    /// <summary>Gets or sets the connection identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the source node ID.</summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the source port ID.</summary>
    public string SourcePortId { get; set; } = string.Empty;

    /// <summary>Gets or sets the target node ID.</summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>Gets or sets the target port ID.</summary>
    public string TargetPortId { get; set; } = string.Empty;

    /// <summary>Gets or sets the connection style.</summary>
    public ConnectionStyle Style { get; set; } = new();
}

/// <summary>
/// Visual style for a connection.
/// </summary>
public sealed class ConnectionStyle
{
    /// <summary>Gets or sets the line color (hex).</summary>
    public string Color { get; set; } = "#4A90D9";

    /// <summary>Gets or sets the line width.</summary>
    public double Width { get; set; } = 2;

    /// <summary>Gets or sets whether to use curved lines.</summary>
    public bool UseCurve { get; set; } = true;
}

/// <summary>
/// Metadata for the graph.
/// </summary>
public sealed class GraphMetadata
{
    /// <summary>Gets or sets the graph version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Gets or sets the canvas zoom level.</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>Gets or sets the canvas pan offset X.</summary>
    public double PanX { get; set; } = 0;

    /// <summary>Gets or sets the canvas pan offset Y.</summary>
    public double PanY { get; set; } = 0;

    /// <summary>Gets or sets the grid snap size.</summary>
    public double GridSize { get; set; } = 20;

    /// <summary>Gets or sets whether grid snap is enabled.</summary>
    public bool SnapToGrid { get; set; } = true;
}

/// <summary>
/// Result of graph validation.
/// </summary>
public sealed class GraphValidationResult
{
    /// <summary>Gets or sets whether the graph is valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets validation errors.</summary>
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets validation warnings.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
