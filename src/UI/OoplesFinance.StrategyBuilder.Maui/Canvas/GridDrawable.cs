namespace OoplesFinance.StrategyBuilder.Maui.Canvas;

/// <summary>
/// Draws the background grid pattern for the strategy canvas.
/// </summary>
public class GridDrawable : IDrawable
{
    public double GridSize { get; set; } = 20;
    public Color LineColor { get; set; } = Colors.Gray;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = 0.5f;
        canvas.Alpha = 0.3f;

        // Draw vertical lines
        for (float x = 0; x < dirtyRect.Width; x += (float)GridSize)
        {
            canvas.DrawLine(x, 0, x, dirtyRect.Height);
        }

        // Draw horizontal lines
        for (float y = 0; y < dirtyRect.Height; y += (float)GridSize)
        {
            canvas.DrawLine(0, y, dirtyRect.Width, y);
        }

        // Draw major grid lines (every 5 cells)
        canvas.Alpha = 0.5f;
        canvas.StrokeSize = 1f;

        var majorGridSize = (float)GridSize * 5;
        for (float x = 0; x < dirtyRect.Width; x += majorGridSize)
        {
            canvas.DrawLine(x, 0, x, dirtyRect.Height);
        }

        for (float y = 0; y < dirtyRect.Height; y += majorGridSize)
        {
            canvas.DrawLine(0, y, dirtyRect.Width, y);
        }
    }
}
