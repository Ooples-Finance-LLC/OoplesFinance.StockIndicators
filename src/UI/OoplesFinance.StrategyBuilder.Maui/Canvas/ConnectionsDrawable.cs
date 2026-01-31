using System.Collections.ObjectModel;
using OoplesFinance.StrategyBuilder.Maui.ViewModels;
using OoplesFinance.StrategyBuilder.Maui.Views;

namespace OoplesFinance.StrategyBuilder.Maui.Canvas;

/// <summary>
/// Draws the connection lines between nodes on the strategy canvas.
/// Uses bezier curves for smooth, professional-looking connections.
/// </summary>
public class ConnectionsDrawable : IDrawable
{
    public ObservableCollection<ConnectionViewModel>? Connections { get; set; }
    public Dictionary<NodeViewModel, NodeView>? NodeViews { get; set; }
    public double ZoomLevel { get; set; } = 1.0;
    public Point PanOffset { get; set; }

    // For drawing in-progress connection
    public PortViewModel? DraggingFromPort { get; set; }
    public Point DraggingEndPoint { get; set; }

    private readonly Color _normalColor = Color.FromArgb("#569CD6");
    private readonly Color _selectedColor = Color.FromArgb("#DCDCAA");
    private readonly Color _signalColor = Color.FromArgb("#C586C0");
    private readonly Color _dataColor = Color.FromArgb("#4EC9B0");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Connections is null || NodeViews is null) return;

        canvas.Antialias = true;

        foreach (var connection in Connections)
        {
            DrawConnection(canvas, connection);
        }

        // Draw in-progress connection if dragging
        if (DraggingFromPort is not null)
        {
            DrawDraggingConnection(canvas);
        }
    }

    private void DrawConnection(ICanvas canvas, ConnectionViewModel connection)
    {
        // Get port positions
        var startPoint = GetPortPosition(connection.SourceNode, connection.SourcePort, isOutput: true);
        var endPoint = GetPortPosition(connection.TargetNode, connection.TargetPort, isOutput: false);

        if (startPoint is null || endPoint is null) return;

        // Determine color based on data type
        var color = GetConnectionColor(connection.DataType, connection.IsSelected);

        canvas.StrokeColor = color;
        canvas.StrokeSize = connection.IsSelected ? 3f : 2f;
        canvas.StrokeLineCap = LineCap.Round;

        // Calculate bezier control points
        var controlPointDistance = Math.Abs(endPoint.Value.X - startPoint.Value.X) * 0.5;
        controlPointDistance = Math.Max(controlPointDistance, 50);

        var path = new PathF();
        path.MoveTo((float)startPoint.Value.X, (float)startPoint.Value.Y);
        path.CurveTo(
            (float)(startPoint.Value.X + controlPointDistance), (float)startPoint.Value.Y,
            (float)(endPoint.Value.X - controlPointDistance), (float)endPoint.Value.Y,
            (float)endPoint.Value.X, (float)endPoint.Value.Y);

        canvas.DrawPath(path);

        // Draw arrow at end
        DrawArrow(canvas, endPoint.Value, color);
    }

    private void DrawDraggingConnection(ICanvas canvas)
    {
        if (DraggingFromPort is null) return;

        var startPoint = GetPortPositionFromViewModel(DraggingFromPort);
        if (startPoint is null) return;

        canvas.StrokeColor = _normalColor;
        canvas.StrokeSize = 2f;
        canvas.StrokeDashPattern = new float[] { 5, 3 };
        canvas.StrokeLineCap = LineCap.Round;

        var controlPointDistance = Math.Abs(DraggingEndPoint.X - startPoint.Value.X) * 0.5;
        controlPointDistance = Math.Max(controlPointDistance, 50);

        var path = new PathF();
        path.MoveTo((float)startPoint.Value.X, (float)startPoint.Value.Y);
        path.CurveTo(
            (float)(startPoint.Value.X + controlPointDistance), (float)startPoint.Value.Y,
            (float)(DraggingEndPoint.X - controlPointDistance), (float)DraggingEndPoint.Y,
            (float)DraggingEndPoint.X, (float)DraggingEndPoint.Y);

        canvas.DrawPath(path);

        // Reset dash pattern
        canvas.StrokeDashPattern = null;
    }

    private Point? GetPortPosition(NodeViewModel? node, PortViewModel? port, bool isOutput)
    {
        if (node is null || port is null || NodeViews is null) return null;
        if (!NodeViews.TryGetValue(node, out var nodeView)) return null;

        // Get node screen position
        var nodeX = node.X * ZoomLevel + PanOffset.X;
        var nodeY = node.Y * ZoomLevel + PanOffset.Y;
        var nodeWidth = node.Width * ZoomLevel;
        var nodeHeight = node.Height * ZoomLevel;

        // Calculate port position (output on right, input on left)
        var portIndex = isOutput
            ? node.OutputPorts.IndexOf(port)
            : node.InputPorts.IndexOf(port);

        var portCount = isOutput ? node.OutputPorts.Count : node.InputPorts.Count;
        var portSpacing = nodeHeight / (portCount + 1);

        var x = isOutput ? nodeX + nodeWidth : nodeX;
        var y = nodeY + portSpacing * (portIndex + 1);

        return new Point(x, y);
    }

    private Point? GetPortPositionFromViewModel(PortViewModel port)
    {
        if (port.ParentNode is null || NodeViews is null) return null;
        if (!NodeViews.TryGetValue(port.ParentNode, out _)) return null;

        return GetPortPosition(port.ParentNode, port, !port.IsInput);
    }

    private Color GetConnectionColor(string dataType, bool isSelected)
    {
        if (isSelected) return _selectedColor;

        return dataType.ToLowerInvariant() switch
        {
            "signal" or "bool" => _signalColor,
            "decimal" or "double" or "float" or "int" => _dataColor,
            _ => _normalColor
        };
    }

    private static void DrawArrow(ICanvas canvas, Point tip, Color color)
    {
        const float arrowSize = 8f;
        const float arrowAngle = 25f * (float)(Math.PI / 180);

        canvas.FillColor = color;

        var path = new PathF();
        path.MoveTo((float)tip.X, (float)tip.Y);
        path.LineTo(
            (float)(tip.X - arrowSize * Math.Cos(arrowAngle)),
            (float)(tip.Y - arrowSize * Math.Sin(arrowAngle)));
        path.LineTo(
            (float)(tip.X - arrowSize * Math.Cos(-arrowAngle)),
            (float)(tip.Y - arrowSize * Math.Sin(-arrowAngle)));
        path.Close();

        canvas.FillPath(path);
    }
}
