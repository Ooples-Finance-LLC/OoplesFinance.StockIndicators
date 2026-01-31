using System.Collections.ObjectModel;
using OoplesFinance.StrategyBuilder.Maui.ViewModels;

namespace OoplesFinance.StrategyBuilder.Maui.Canvas;

/// <summary>
/// Draws the minimap showing an overview of the entire strategy graph.
/// </summary>
public class MinimapDrawable : IDrawable
{
    public ObservableCollection<NodeViewModel>? Nodes { get; set; }
    public ObservableCollection<ConnectionViewModel>? Connections { get; set; }
    public RectF ViewportRect { get; set; }

    private readonly Color _nodeColor = Color.FromArgb("#569CD6");
    private readonly Color _connectionColor = Color.FromArgb("#444444");
    private readonly Color _viewportColor = Color.FromArgb("#007ACC");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Nodes is null || Nodes.Count == 0) return;

        // Calculate bounds of all nodes
        var minX = Nodes.Min(n => n.X);
        var minY = Nodes.Min(n => n.Y);
        var maxX = Nodes.Max(n => n.X + n.Width);
        var maxY = Nodes.Max(n => n.Y + n.Height);

        var contentWidth = maxX - minX + 50;
        var contentHeight = maxY - minY + 50;

        // Calculate scale to fit in minimap
        var scaleX = dirtyRect.Width / contentWidth;
        var scaleY = dirtyRect.Height / contentHeight;
        var scale = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave margin

        var offsetX = (dirtyRect.Width - contentWidth * scale) / 2 - minX * scale;
        var offsetY = (dirtyRect.Height - contentHeight * scale) / 2 - minY * scale;

        // Draw connections first (behind nodes)
        if (Connections is not null)
        {
            canvas.StrokeColor = _connectionColor;
            canvas.StrokeSize = 1f;

            foreach (var connection in Connections)
            {
                if (connection.SourceNode is null || connection.TargetNode is null) continue;

                var startX = (connection.SourceNode.X + connection.SourceNode.Width) * scale + offsetX;
                var startY = (connection.SourceNode.Y + connection.SourceNode.Height / 2) * scale + offsetY;
                var endX = connection.TargetNode.X * scale + offsetX;
                var endY = (connection.TargetNode.Y + connection.TargetNode.Height / 2) * scale + offsetY;

                canvas.DrawLine((float)startX, (float)startY, (float)endX, (float)endY);
            }
        }

        // Draw nodes as small rectangles
        canvas.FillColor = _nodeColor;

        foreach (var node in Nodes)
        {
            var x = node.X * scale + offsetX;
            var y = node.Y * scale + offsetY;
            var width = Math.Max(node.Width * scale, 4);
            var height = Math.Max(node.Height * scale, 3);

            canvas.FillRectangle((float)x, (float)y, (float)width, (float)height);
        }

        // Draw viewport rectangle
        canvas.StrokeColor = _viewportColor;
        canvas.StrokeSize = 2f;
        canvas.DrawRectangle(
            ViewportRect.X * (float)scale + (float)offsetX,
            ViewportRect.Y * (float)scale + (float)offsetY,
            ViewportRect.Width * (float)scale,
            ViewportRect.Height * (float)scale);
    }
}
