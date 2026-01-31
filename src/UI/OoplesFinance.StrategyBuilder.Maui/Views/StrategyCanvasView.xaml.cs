using System.Collections.ObjectModel;
using System.Windows.Input;
using OoplesFinance.StrategyBuilder.Maui.Canvas;
using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Views;

/// <summary>
/// Custom canvas view for displaying and interacting with strategy nodes and connections.
/// Supports drag-and-drop, pan, zoom, and node selection.
/// </summary>
public partial class StrategyCanvasView : ContentView
{
    #region Bindable Properties

    public static readonly BindableProperty NodesProperty =
        BindableProperty.Create(
            nameof(Nodes),
            typeof(ObservableCollection<NodeViewModel>),
            typeof(StrategyCanvasView),
            null,
            propertyChanged: OnNodesChanged);

    public static readonly BindableProperty ConnectionsProperty =
        BindableProperty.Create(
            nameof(Connections),
            typeof(ObservableCollection<ConnectionViewModel>),
            typeof(StrategyCanvasView),
            null,
            propertyChanged: OnConnectionsChanged);

    public static readonly BindableProperty SelectedNodeProperty =
        BindableProperty.Create(
            nameof(SelectedNode),
            typeof(NodeViewModel),
            typeof(StrategyCanvasView),
            null,
            BindingMode.TwoWay,
            propertyChanged: OnSelectedNodeChanged);

    public static readonly BindableProperty NodeAddedCommandProperty =
        BindableProperty.Create(
            nameof(NodeAddedCommand),
            typeof(ICommand),
            typeof(StrategyCanvasView));

    public static readonly BindableProperty NodeMovedCommandProperty =
        BindableProperty.Create(
            nameof(NodeMovedCommand),
            typeof(ICommand),
            typeof(StrategyCanvasView));

    public static readonly BindableProperty ConnectionCreatedCommandProperty =
        BindableProperty.Create(
            nameof(ConnectionCreatedCommand),
            typeof(ICommand),
            typeof(StrategyCanvasView));

    public static readonly BindableProperty ConnectionDeletedCommandProperty =
        BindableProperty.Create(
            nameof(ConnectionDeletedCommand),
            typeof(ICommand),
            typeof(StrategyCanvasView));

    public ObservableCollection<NodeViewModel> Nodes
    {
        get => (ObservableCollection<NodeViewModel>)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public ObservableCollection<ConnectionViewModel> Connections
    {
        get => (ObservableCollection<ConnectionViewModel>)GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    public NodeViewModel? SelectedNode
    {
        get => (NodeViewModel?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public ICommand NodeAddedCommand
    {
        get => (ICommand)GetValue(NodeAddedCommandProperty);
        set => SetValue(NodeAddedCommandProperty, value);
    }

    public ICommand NodeMovedCommand
    {
        get => (ICommand)GetValue(NodeMovedCommandProperty);
        set => SetValue(NodeMovedCommandProperty, value);
    }

    public ICommand ConnectionCreatedCommand
    {
        get => (ICommand)GetValue(ConnectionCreatedCommandProperty);
        set => SetValue(ConnectionCreatedCommandProperty, value);
    }

    public ICommand ConnectionDeletedCommand
    {
        get => (ICommand)GetValue(ConnectionDeletedCommandProperty);
        set => SetValue(ConnectionDeletedCommandProperty, value);
    }

    #endregion

    private double _panOffsetX;
    private double _panOffsetY;
    private double _zoomLevel = 1.0;
    private Point _lastPanPoint;
    private bool _isPanning;
    private NodeViewModel? _draggingNode;
    private Point _dragStartPoint;
    private PortViewModel? _connectionStartPort;
    private Point _connectionEndPoint;
    private bool _isDrawingConnection;

    private readonly Dictionary<NodeViewModel, NodeView> _nodeViews = new();

    public StrategyCanvasView()
    {
        InitializeComponent();
    }

    #region Property Changed Handlers

    private static void OnNodesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StrategyCanvasView canvas)
        {
            canvas.RebuildNodeViews();

            if (newValue is ObservableCollection<NodeViewModel> nodes)
            {
                nodes.CollectionChanged += (s, e) => canvas.RebuildNodeViews();
            }
        }
    }

    private static void OnConnectionsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StrategyCanvasView canvas)
        {
            canvas.UpdateConnections();

            if (newValue is ObservableCollection<ConnectionViewModel> connections)
            {
                connections.CollectionChanged += (s, e) => canvas.UpdateConnections();
            }
        }
    }

    private static void OnSelectedNodeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StrategyCanvasView canvas)
        {
            canvas.UpdateNodeSelection(oldValue as NodeViewModel, newValue as NodeViewModel);
        }
    }

    #endregion

    #region Node Management

    private void RebuildNodeViews()
    {
        NodesContainer.Children.Clear();
        _nodeViews.Clear();

        if (Nodes is null) return;

        foreach (var nodeVm in Nodes)
        {
            AddNodeView(nodeVm);
        }

        UpdateConnections();
    }

    private void AddNodeView(NodeViewModel nodeVm)
    {
        var nodeView = new NodeView
        {
            BindingContext = nodeVm
        };

        nodeView.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => SelectNode(nodeVm))
        });

        nodeView.GestureRecognizers.Add(new PanGestureRecognizer
        {
            PanUpdated = (s, e) => OnNodePan(nodeVm, e)
        });

        // Position the node
        AbsoluteLayout.SetLayoutBounds(nodeView, new Rect(
            nodeVm.X * _zoomLevel + _panOffsetX,
            nodeVm.Y * _zoomLevel + _panOffsetY,
            nodeVm.Width * _zoomLevel,
            nodeVm.Height * _zoomLevel));
        AbsoluteLayout.SetLayoutFlags(nodeView, AbsoluteLayoutFlags.None);

        NodesContainer.Children.Add(nodeView);
        _nodeViews[nodeVm] = nodeView;
    }

    private void SelectNode(NodeViewModel node)
    {
        SelectedNode = node;
    }

    private void UpdateNodeSelection(NodeViewModel? oldNode, NodeViewModel? newNode)
    {
        if (oldNode is not null && _nodeViews.TryGetValue(oldNode, out var oldView))
        {
            oldView.IsSelected = false;
        }

        if (newNode is not null && _nodeViews.TryGetValue(newNode, out var newView))
        {
            newView.IsSelected = true;
        }
    }

    private void OnNodePan(NodeViewModel node, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _draggingNode = node;
                _dragStartPoint = new Point(node.X, node.Y);
                break;

            case GestureStatus.Running:
                if (_draggingNode == node)
                {
                    node.X = _dragStartPoint.X + e.TotalX / _zoomLevel;
                    node.Y = _dragStartPoint.Y + e.TotalY / _zoomLevel;
                    UpdateNodePosition(node);
                    UpdateConnections();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_draggingNode == node)
                {
                    NodeMovedCommand?.Execute(new NodeMovedEventArgs
                    {
                        Node = node,
                        OldPosition = _dragStartPoint,
                        NewPosition = new Point(node.X, node.Y)
                    });
                    _draggingNode = null;
                }
                break;
        }
    }

    private void UpdateNodePosition(NodeViewModel node)
    {
        if (_nodeViews.TryGetValue(node, out var nodeView))
        {
            AbsoluteLayout.SetLayoutBounds(nodeView, new Rect(
                node.X * _zoomLevel + _panOffsetX,
                node.Y * _zoomLevel + _panOffsetY,
                node.Width * _zoomLevel,
                node.Height * _zoomLevel));
        }
    }

    #endregion

    #region Connection Management

    private void UpdateConnections()
    {
        if (ConnectionsDrawable is not null)
        {
            ConnectionsDrawable.Connections = Connections;
            ConnectionsDrawable.NodeViews = _nodeViews;
            ConnectionsDrawable.ZoomLevel = _zoomLevel;
            ConnectionsDrawable.PanOffset = new Point(_panOffsetX, _panOffsetY);
            ConnectionsLayer.Invalidate();
        }

        if (MinimapDrawable is not null)
        {
            MinimapDrawable.Nodes = Nodes;
            MinimapDrawable.Connections = Connections;
            MinimapView.Invalidate();
        }
    }

    public void StartConnection(PortViewModel port, Point startPoint)
    {
        _connectionStartPort = port;
        _isDrawingConnection = true;
        _connectionEndPoint = startPoint;
    }

    public void UpdateConnectionDrag(Point currentPoint)
    {
        if (_isDrawingConnection)
        {
            _connectionEndPoint = currentPoint;
            ConnectionsLayer.Invalidate();
        }
    }

    public void EndConnection(PortViewModel? endPort)
    {
        if (_isDrawingConnection && _connectionStartPort is not null && endPort is not null)
        {
            // Validate connection (output to input, compatible types)
            if (CanConnect(_connectionStartPort, endPort))
            {
                ConnectionCreatedCommand?.Execute(new ConnectionCreatedEventArgs
                {
                    SourcePort = _connectionStartPort,
                    TargetPort = endPort
                });
            }
        }

        _isDrawingConnection = false;
        _connectionStartPort = null;
        ConnectionsLayer.Invalidate();
    }

    private static bool CanConnect(PortViewModel source, PortViewModel target)
    {
        // Must be output -> input
        if (source.IsInput == target.IsInput) return false;

        // Must be different nodes
        if (source.ParentNode == target.ParentNode) return false;

        // Must have compatible data types
        return AreTypesCompatible(source.DataType, target.DataType);
    }

    private static bool AreTypesCompatible(string sourceType, string targetType)
    {
        if (sourceType == targetType) return true;
        if (targetType == "any") return true;
        if (sourceType == "decimal" && targetType == "double") return true;
        if (sourceType == "double" && targetType == "decimal") return true;
        return false;
    }

    #endregion

    #region Canvas Interaction

    private void OnCanvasTapped(object? sender, TappedEventArgs e)
    {
        // Deselect node when clicking on empty canvas
        SelectedNode = null;
    }

    private void OnCanvasPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isPanning = true;
                _lastPanPoint = new Point(_panOffsetX, _panOffsetY);
                break;

            case GestureStatus.Running:
                if (_isPanning)
                {
                    _panOffsetX = _lastPanPoint.X + e.TotalX;
                    _panOffsetY = _lastPanPoint.Y + e.TotalY;
                    UpdateAllNodePositions();
                    UpdateConnections();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isPanning = false;
                break;
        }
    }

    private void UpdateAllNodePositions()
    {
        if (Nodes is null) return;

        foreach (var node in Nodes)
        {
            UpdateNodePosition(node);
        }
    }

    public void ZoomIn()
    {
        SetZoom(_zoomLevel * 1.2);
    }

    public void ZoomOut()
    {
        SetZoom(_zoomLevel / 1.2);
    }

    public void SetZoom(double zoom)
    {
        _zoomLevel = Math.Clamp(zoom, 0.25, 4.0);
        UpdateAllNodePositions();
        UpdateConnections();
    }

    public void FitToView()
    {
        if (Nodes is null || Nodes.Count == 0) return;

        var minX = Nodes.Min(n => n.X);
        var minY = Nodes.Min(n => n.Y);
        var maxX = Nodes.Max(n => n.X + n.Width);
        var maxY = Nodes.Max(n => n.Y + n.Height);

        var contentWidth = maxX - minX + 100;
        var contentHeight = maxY - minY + 100;

        var scaleX = Width / contentWidth;
        var scaleY = Height / contentHeight;

        _zoomLevel = Math.Min(scaleX, scaleY);
        _zoomLevel = Math.Clamp(_zoomLevel, 0.25, 4.0);

        _panOffsetX = (Width - contentWidth * _zoomLevel) / 2 - minX * _zoomLevel;
        _panOffsetY = (Height - contentHeight * _zoomLevel) / 2 - minY * _zoomLevel;

        UpdateAllNodePositions();
        UpdateConnections();
    }

    #endregion
}

#region Event Args

public class NodeMovedEventArgs
{
    public NodeViewModel Node { get; set; } = null!;
    public Point OldPosition { get; set; }
    public Point NewPosition { get; set; }
}

public class ConnectionCreatedEventArgs
{
    public PortViewModel SourcePort { get; set; } = null!;
    public PortViewModel TargetPort { get; set; } = null!;
}

#endregion
