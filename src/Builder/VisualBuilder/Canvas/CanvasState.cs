namespace OoplesFinance.StockIndicators.Builder.VisualBuilder.Canvas;

/// <summary>
/// Manages the state of the visual strategy canvas.
/// Platform-agnostic state that can be used by any UI framework.
/// </summary>
public sealed class CanvasState
{
    private readonly List<NodeViewModel> _nodes = new();
    private readonly List<ConnectionViewModel> _connections = new();
    private readonly HashSet<string> _selectedNodeIds = new();
    private string? _hoveredNodeId;
    private string? _hoveredPortId;
    private ConnectionDragState? _connectionDrag;

    /// <summary>Gets or sets the current zoom level (1.0 = 100%).</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>Gets or sets the pan offset.</summary>
    public CanvasPoint PanOffset { get; set; } = CanvasPoint.Zero;

    /// <summary>Gets or sets the viewport size.</summary>
    public CanvasSize ViewportSize { get; set; } = new CanvasSize(800, 600);

    /// <summary>Gets or sets the theme.</summary>
    public CanvasTheme Theme { get; set; } = CanvasTheme.Dark;

    /// <summary>Gets or sets whether the canvas is read-only.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Gets or sets whether multi-select is enabled.</summary>
    public bool MultiSelectEnabled { get; set; } = true;

    /// <summary>Gets all nodes.</summary>
    public IReadOnlyList<NodeViewModel> Nodes => _nodes;

    /// <summary>Gets all connections.</summary>
    public IReadOnlyList<ConnectionViewModel> Connections => _connections;

    /// <summary>Gets the selected node IDs.</summary>
    public IEnumerable<string> SelectedNodeIds => _selectedNodeIds;

    /// <summary>Checks if a node is selected.</summary>
    public bool IsNodeSelected(string nodeId) => _selectedNodeIds.Contains(nodeId);

    /// <summary>Gets the count of selected nodes.</summary>
    public int SelectedNodeCount => _selectedNodeIds.Count;

    /// <summary>Gets or sets the hovered node ID.</summary>
    public string? HoveredNodeId
    {
        get => _hoveredNodeId;
        set
        {
            if (_hoveredNodeId != value)
            {
                _hoveredNodeId = value;
                OnStateChanged(CanvasChangeType.Hover);
            }
        }
    }

    /// <summary>Gets or sets the hovered port ID.</summary>
    public string? HoveredPortId
    {
        get => _hoveredPortId;
        set
        {
            if (_hoveredPortId != value)
            {
                _hoveredPortId = value;
                OnStateChanged(CanvasChangeType.Hover);
            }
        }
    }

    /// <summary>Gets the current connection drag state.</summary>
    public ConnectionDragState? ConnectionDrag => _connectionDrag;

    /// <summary>Gets or sets the selection rectangle (for multi-select).</summary>
    public CanvasRect? SelectionRect { get; set; }

    /// <summary>Event raised when state changes.</summary>
    public event EventHandler<CanvasStateChangedEventArgs>? StateChanged;

    /// <summary>Event raised when a node is added.</summary>
    public event EventHandler<NodeViewModel>? NodeAdded;

    /// <summary>Event raised when a node is removed.</summary>
    public event EventHandler<NodeViewModel>? NodeRemoved;

    /// <summary>Event raised when a connection is added.</summary>
    public event EventHandler<ConnectionViewModel>? ConnectionAdded;

    /// <summary>Event raised when a connection is removed.</summary>
    public event EventHandler<ConnectionViewModel>? ConnectionRemoved;

    /// <summary>Event raised when selection changes.</summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Adds a node to the canvas.
    /// </summary>
    public void AddNode(NodeViewModel node)
    {
        _nodes.Add(node);
        NodeAdded?.Invoke(this, node);
        OnStateChanged(CanvasChangeType.NodeAdded);
    }

    /// <summary>
    /// Removes a node from the canvas.
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        // Remove all connections to/from this node
        var connectionsToRemove = _connections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();

        foreach (var connection in connectionsToRemove)
        {
            RemoveConnection(connection.Id);
        }

        _nodes.Remove(node);
        _selectedNodeIds.Remove(nodeId);
        NodeRemoved?.Invoke(this, node);
        OnStateChanged(CanvasChangeType.NodeRemoved);
    }

    /// <summary>
    /// Adds a connection between nodes.
    /// </summary>
    public bool AddConnection(ConnectionViewModel connection)
    {
        // Validate connection
        var sourceNode = _nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
        var targetNode = _nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);

        if (sourceNode is null || targetNode is null)
            return false;

        // Check if connection already exists
        if (_connections.Any(c =>
            c.SourceNodeId == connection.SourceNodeId &&
            c.SourcePortId == connection.SourcePortId &&
            c.TargetNodeId == connection.TargetNodeId &&
            c.TargetPortId == connection.TargetPortId))
            return false;

        // Check for cycles
        if (WouldCreateCycle(connection.SourceNodeId, connection.TargetNodeId))
            return false;

        // Remove any existing connection to the target port (only one input allowed)
        var existingConnection = _connections.FirstOrDefault(c =>
            c.TargetNodeId == connection.TargetNodeId &&
            c.TargetPortId == connection.TargetPortId);

        if (existingConnection is not null)
        {
            RemoveConnection(existingConnection.Id);
        }

        _connections.Add(connection);
        ConnectionAdded?.Invoke(this, connection);
        OnStateChanged(CanvasChangeType.ConnectionAdded);
        return true;
    }

    /// <summary>
    /// Removes a connection.
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection is null) return;

        _connections.Remove(connection);
        ConnectionRemoved?.Invoke(this, connection);
        OnStateChanged(CanvasChangeType.ConnectionRemoved);
    }

    /// <summary>
    /// Selects a node.
    /// </summary>
    public void SelectNode(string nodeId, bool addToSelection = false)
    {
        if (!addToSelection || !MultiSelectEnabled)
        {
            _selectedNodeIds.Clear();
        }

        _selectedNodeIds.Add(nodeId);
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(_selectedNodeIds.ToList()));
        OnStateChanged(CanvasChangeType.Selection);
    }

    /// <summary>
    /// Deselects a node.
    /// </summary>
    public void DeselectNode(string nodeId)
    {
        if (_selectedNodeIds.Remove(nodeId))
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(_selectedNodeIds.ToList()));
            OnStateChanged(CanvasChangeType.Selection);
        }
    }

    /// <summary>
    /// Clears all selection.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedNodeIds.Count > 0)
        {
            _selectedNodeIds.Clear();
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(new List<string>()));
            OnStateChanged(CanvasChangeType.Selection);
        }
    }

    /// <summary>
    /// Selects nodes within the given rectangle.
    /// </summary>
    public void SelectNodesInRect(CanvasRect rect, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            _selectedNodeIds.Clear();
        }

        foreach (var node in _nodes)
        {
            var nodeRect = new CanvasRect(node.Position, node.Size);
            if (rect.Intersects(nodeRect))
            {
                _selectedNodeIds.Add(node.Id);
            }
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(_selectedNodeIds.ToList()));
        OnStateChanged(CanvasChangeType.Selection);
    }

    /// <summary>
    /// Starts dragging a connection from a port.
    /// </summary>
    public void StartConnectionDrag(string nodeId, string portId, bool isOutput)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        var port = isOutput
            ? node.OutputPorts.FirstOrDefault(p => p.Id == portId)
            : node.InputPorts.FirstOrDefault(p => p.Id == portId);

        if (port is null) return;

        _connectionDrag = new ConnectionDragState
        {
            SourceNodeId = nodeId,
            SourcePortId = portId,
            IsFromOutput = isOutput,
            StartPosition = port.Position,
            CurrentPosition = port.Position
        };

        OnStateChanged(CanvasChangeType.ConnectionDrag);
    }

    /// <summary>
    /// Updates the connection drag position.
    /// </summary>
    public void UpdateConnectionDrag(CanvasPoint position)
    {
        if (_connectionDrag is null) return;

        _connectionDrag.CurrentPosition = position;
        OnStateChanged(CanvasChangeType.ConnectionDrag);
    }

    /// <summary>
    /// Ends the connection drag.
    /// </summary>
    public void EndConnectionDrag(string? targetNodeId = null, string? targetPortId = null)
    {
        if (_connectionDrag is not null && targetNodeId is not null && targetPortId is not null)
        {
            var connection = _connectionDrag.IsFromOutput
                ? new ConnectionViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    SourceNodeId = _connectionDrag.SourceNodeId,
                    SourcePortId = _connectionDrag.SourcePortId,
                    TargetNodeId = targetNodeId,
                    TargetPortId = targetPortId
                }
                : new ConnectionViewModel
                {
                    Id = Guid.NewGuid().ToString(),
                    SourceNodeId = targetNodeId,
                    SourcePortId = targetPortId,
                    TargetNodeId = _connectionDrag.SourceNodeId,
                    TargetPortId = _connectionDrag.SourcePortId
                };

            AddConnection(connection);
        }

        _connectionDrag = null;
        OnStateChanged(CanvasChangeType.ConnectionDrag);
    }

    /// <summary>
    /// Cancels the connection drag.
    /// </summary>
    public void CancelConnectionDrag()
    {
        _connectionDrag = null;
        OnStateChanged(CanvasChangeType.ConnectionDrag);
    }

    /// <summary>
    /// Moves selected nodes by the given delta.
    /// </summary>
    public void MoveSelectedNodes(CanvasPoint delta)
    {
        foreach (var nodeId in _selectedNodeIds)
        {
            var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.Position = new CanvasPoint(
                    node.Position.X + delta.X,
                    node.Position.Y + delta.Y);

                if (Theme.SnapToGrid)
                {
                    node.Position = SnapToGrid(node.Position);
                }
            }
        }

        OnStateChanged(CanvasChangeType.NodeMoved);
    }

    /// <summary>
    /// Zooms the canvas.
    /// </summary>
    public void SetZoom(double zoom, CanvasPoint? center = null)
    {
        zoom = Math.Max(0.1, Math.Min(zoom, 4.0));

        if (center.HasValue)
        {
            // Adjust pan to zoom towards center point
            var worldCenter = ScreenToWorld(center.Value);
            Zoom = zoom;
            var newScreenCenter = WorldToScreen(worldCenter);
            PanOffset = new CanvasPoint(
                PanOffset.X + (center.Value.X - newScreenCenter.X),
                PanOffset.Y + (center.Value.Y - newScreenCenter.Y));
        }
        else
        {
            Zoom = zoom;
        }

        OnStateChanged(CanvasChangeType.ZoomPan);
    }

    /// <summary>
    /// Pans the canvas.
    /// </summary>
    public void Pan(CanvasPoint delta)
    {
        PanOffset = new CanvasPoint(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
        OnStateChanged(CanvasChangeType.ZoomPan);
    }

    /// <summary>
    /// Fits all nodes in the viewport.
    /// </summary>
    public void FitToView()
    {
        if (_nodes.Count == 0)
        {
            Zoom = 1.0;
            PanOffset = CanvasPoint.Zero;
            return;
        }

        var minX = _nodes.Min(n => n.Position.X);
        var minY = _nodes.Min(n => n.Position.Y);
        var maxX = _nodes.Max(n => n.Position.X + n.Size.Width);
        var maxY = _nodes.Max(n => n.Position.Y + n.Size.Height);

        var contentWidth = maxX - minX + 100; // Add padding
        var contentHeight = maxY - minY + 100;

        var scaleX = ViewportSize.Width / contentWidth;
        var scaleY = ViewportSize.Height / contentHeight;
        Zoom = Math.Min(Math.Min(scaleX, scaleY), 1.0);

        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;

        PanOffset = new CanvasPoint(
            ViewportSize.Width / 2 - centerX * Zoom,
            ViewportSize.Height / 2 - centerY * Zoom);

        OnStateChanged(CanvasChangeType.ZoomPan);
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates.
    /// </summary>
    public CanvasPoint ScreenToWorld(CanvasPoint screen)
    {
        return new CanvasPoint(
            (screen.X - PanOffset.X) / Zoom,
            (screen.Y - PanOffset.Y) / Zoom);
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates.
    /// </summary>
    public CanvasPoint WorldToScreen(CanvasPoint world)
    {
        return new CanvasPoint(
            world.X * Zoom + PanOffset.X,
            world.Y * Zoom + PanOffset.Y);
    }

    /// <summary>
    /// Snaps a position to the grid.
    /// </summary>
    public CanvasPoint SnapToGrid(CanvasPoint position)
    {
        var spacing = Theme.GridSpacing;
        return new CanvasPoint(
            Math.Round(position.X / spacing) * spacing,
            Math.Round(position.Y / spacing) * spacing);
    }

    /// <summary>
    /// Finds the node at the given screen position.
    /// </summary>
    public NodeViewModel? GetNodeAtPosition(CanvasPoint screenPosition)
    {
        var worldPosition = ScreenToWorld(screenPosition);

        // Check in reverse order (top nodes first)
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            var node = _nodes[i];
            var nodeRect = new CanvasRect(node.Position, node.Size);
            if (nodeRect.Contains(worldPosition))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the port at the given screen position.
    /// </summary>
    public (NodeViewModel Node, PortViewModel Port, bool IsOutput)? GetPortAtPosition(CanvasPoint screenPosition)
    {
        var worldPosition = ScreenToWorld(screenPosition);
        var hitRadius = Theme.PortRadius * 2;

        foreach (var node in _nodes)
        {
            foreach (var port in node.OutputPorts)
            {
                if (worldPosition.DistanceTo(port.Position) <= hitRadius)
                {
                    return (node, port, true);
                }
            }

            foreach (var port in node.InputPorts)
            {
                if (worldPosition.DistanceTo(port.Position) <= hitRadius)
                {
                    return (node, port, false);
                }
            }
        }

        return null;
    }

    private bool WouldCreateCycle(string sourceId, string targetId)
    {
        // BFS to check if target can reach source
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(targetId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == sourceId) return true;
            if (!visited.Add(current)) continue;

            // Find all nodes that this node connects to
            foreach (var connection in _connections.Where(c => c.SourceNodeId == current))
            {
                queue.Enqueue(connection.TargetNodeId);
            }
        }

        return false;
    }

    private void OnStateChanged(CanvasChangeType changeType)
    {
        StateChanged?.Invoke(this, new CanvasStateChangedEventArgs(changeType));
    }
}

/// <summary>
/// Types of canvas state changes.
/// </summary>
public enum CanvasChangeType
{
    NodeAdded,
    NodeRemoved,
    NodeMoved,
    ConnectionAdded,
    ConnectionRemoved,
    ConnectionDrag,
    Selection,
    Hover,
    ZoomPan
}

/// <summary>
/// Event args for canvas state changes.
/// </summary>
public sealed class CanvasStateChangedEventArgs : EventArgs
{
    public CanvasChangeType ChangeType { get; }

    public CanvasStateChangedEventArgs(CanvasChangeType changeType)
    {
        ChangeType = changeType;
    }
}

/// <summary>
/// Event args for selection changes.
/// </summary>
public sealed class SelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<string> SelectedNodeIds { get; }

    public SelectionChangedEventArgs(IReadOnlyList<string> selectedNodeIds)
    {
        SelectedNodeIds = selectedNodeIds;
    }
}

/// <summary>
/// State for connection dragging.
/// </summary>
public sealed class ConnectionDragState
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public bool IsFromOutput { get; set; }
    public CanvasPoint StartPosition { get; set; }
    public CanvasPoint CurrentPosition { get; set; }
}
