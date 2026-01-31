namespace OoplesFinance.StockIndicators.Builder.VisualBuilder.Canvas;

/// <summary>
/// Represents a 2D point on the canvas.
/// </summary>
public readonly struct CanvasPoint
{
    /// <summary>Gets the X coordinate.</summary>
    public double X { get; }

    /// <summary>Gets the Y coordinate.</summary>
    public double Y { get; }

    public CanvasPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static CanvasPoint operator +(CanvasPoint a, CanvasPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static CanvasPoint operator -(CanvasPoint a, CanvasPoint b) => new(a.X - b.X, a.Y - b.Y);
    public static CanvasPoint operator *(CanvasPoint p, double scale) => new(p.X * scale, p.Y * scale);
    public static CanvasPoint operator /(CanvasPoint p, double scale) => new(p.X / scale, p.Y / scale);

    public static readonly CanvasPoint Zero = new(0, 0);

    public double DistanceTo(CanvasPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => $"({X:F2}, {Y:F2})";
}

/// <summary>
/// Represents a 2D size on the canvas.
/// </summary>
public readonly struct CanvasSize
{
    /// <summary>Gets the width.</summary>
    public double Width { get; }

    /// <summary>Gets the height.</summary>
    public double Height { get; }

    public CanvasSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public static readonly CanvasSize Zero = new(0, 0);

    public override string ToString() => $"{Width:F2} x {Height:F2}";
}

/// <summary>
/// Represents a rectangle on the canvas.
/// </summary>
public readonly struct CanvasRect
{
    /// <summary>Gets the top-left position.</summary>
    public CanvasPoint Position { get; }

    /// <summary>Gets the size.</summary>
    public CanvasSize Size { get; }

    /// <summary>Gets the left edge X coordinate.</summary>
    public double Left => Position.X;

    /// <summary>Gets the top edge Y coordinate.</summary>
    public double Top => Position.Y;

    /// <summary>Gets the right edge X coordinate.</summary>
    public double Right => Position.X + Size.Width;

    /// <summary>Gets the bottom edge Y coordinate.</summary>
    public double Bottom => Position.Y + Size.Height;

    /// <summary>Gets the center point.</summary>
    public CanvasPoint Center => new(Position.X + Size.Width / 2, Position.Y + Size.Height / 2);

    public CanvasRect(CanvasPoint position, CanvasSize size)
    {
        Position = position;
        Size = size;
    }

    public CanvasRect(double x, double y, double width, double height)
        : this(new CanvasPoint(x, y), new CanvasSize(width, height))
    {
    }

    public bool Contains(CanvasPoint point)
    {
        return point.X >= Left && point.X <= Right &&
               point.Y >= Top && point.Y <= Bottom;
    }

    public bool Intersects(CanvasRect other)
    {
        return Left < other.Right && Right > other.Left &&
               Top < other.Bottom && Bottom > other.Top;
    }

    public CanvasRect Inflate(double amount)
    {
        return new CanvasRect(
            Position.X - amount,
            Position.Y - amount,
            Size.Width + amount * 2,
            Size.Height + amount * 2);
    }

    public override string ToString() => $"[{Position}, {Size}]";
}

/// <summary>
/// Represents a color on the canvas.
/// </summary>
public readonly struct CanvasColor
{
    /// <summary>Gets the red component (0-255).</summary>
    public byte R { get; }

    /// <summary>Gets the green component (0-255).</summary>
    public byte G { get; }

    /// <summary>Gets the blue component (0-255).</summary>
    public byte B { get; }

    /// <summary>Gets the alpha component (0-255).</summary>
    public byte A { get; }

    public CanvasColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static CanvasColor FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            return new CanvasColor(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }
        if (hex.Length == 8)
        {
            return new CanvasColor(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                Convert.ToByte(hex.Substring(6, 2), 16));
        }
        return Black;
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
    public string ToHexWithAlpha() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    public CanvasColor WithAlpha(byte alpha) => new(R, G, B, alpha);

    // Common colors
    public static readonly CanvasColor Transparent = new(0, 0, 0, 0);
    public static readonly CanvasColor Black = new(0, 0, 0);
    public static readonly CanvasColor White = new(255, 255, 255);
    public static readonly CanvasColor Red = new(255, 0, 0);
    public static readonly CanvasColor Green = new(0, 255, 0);
    public static readonly CanvasColor Blue = new(0, 0, 255);
    public static readonly CanvasColor Yellow = new(255, 255, 0);
    public static readonly CanvasColor Orange = new(255, 165, 0);
    public static readonly CanvasColor Purple = new(128, 0, 128);
    public static readonly CanvasColor Gray = new(128, 128, 128);
    public static readonly CanvasColor LightGray = new(211, 211, 211);
    public static readonly CanvasColor DarkGray = new(64, 64, 64);

    // Node type colors
    public static readonly CanvasColor DataSourceColor = new(76, 175, 80);     // Green
    public static readonly CanvasColor ParameterColor = new(156, 39, 176);     // Purple
    public static readonly CanvasColor IndicatorColor = new(33, 150, 243);     // Blue
    public static readonly CanvasColor MathColor = new(255, 152, 0);           // Orange

    // Port colors
    public static readonly CanvasColor InputPortColor = new(76, 175, 80);      // Green
    public static readonly CanvasColor OutputPortColor = new(33, 150, 243);    // Blue
    public static readonly CanvasColor LogicColor = new(233, 30, 99);          // Pink
    public static readonly CanvasColor SignalColor = new(0, 188, 212);         // Cyan
    public static readonly CanvasColor ActionColor = new(244, 67, 54);         // Red
    public static readonly CanvasColor AggregatorColor = new(103, 58, 183);    // Deep Purple
    public static readonly CanvasColor FilterColor = new(255, 193, 7);         // Amber
    public static readonly CanvasColor RiskColor = new(121, 85, 72);           // Brown
    public static readonly CanvasColor SizingColor = new(96, 125, 139);        // Blue Gray

    public override string ToString() => ToHex();
}

/// <summary>
/// Canvas theme for styling the visual builder.
/// </summary>
public sealed class CanvasTheme
{
    /// <summary>Gets or sets the background color.</summary>
    public CanvasColor BackgroundColor { get; set; } = CanvasColor.FromHex("#1E1E1E");

    /// <summary>Gets or sets the grid color.</summary>
    public CanvasColor GridColor { get; set; } = CanvasColor.FromHex("#2D2D2D");

    /// <summary>Gets or sets the node background color.</summary>
    public CanvasColor NodeBackgroundColor { get; set; } = CanvasColor.FromHex("#2D2D2D");

    /// <summary>Gets or sets the node border color.</summary>
    public CanvasColor NodeBorderColor { get; set; } = CanvasColor.FromHex("#3C3C3C");

    /// <summary>Gets or sets the selected node border color.</summary>
    public CanvasColor SelectionColor { get; set; } = CanvasColor.FromHex("#0078D7");

    /// <summary>Gets or sets the connection line color.</summary>
    public CanvasColor ConnectionColor { get; set; } = CanvasColor.FromHex("#888888");

    /// <summary>Gets or sets the active connection line color.</summary>
    public CanvasColor ActiveConnectionColor { get; set; } = CanvasColor.FromHex("#00D1FF");

    /// <summary>Gets or sets the text color.</summary>
    public CanvasColor TextColor { get; set; } = CanvasColor.FromHex("#CCCCCC");

    /// <summary>Gets or sets the secondary text color.</summary>
    public CanvasColor SecondaryTextColor { get; set; } = CanvasColor.FromHex("#888888");

    /// <summary>Gets or sets the input port color.</summary>
    public CanvasColor InputPortColor { get; set; } = CanvasColor.FromHex("#4CAF50");

    /// <summary>Gets or sets the output port color.</summary>
    public CanvasColor OutputPortColor { get; set; } = CanvasColor.FromHex("#2196F3");

    /// <summary>Gets or sets the error color.</summary>
    public CanvasColor ErrorColor { get; set; } = CanvasColor.FromHex("#F44336");

    /// <summary>Gets or sets the warning color.</summary>
    public CanvasColor WarningColor { get; set; } = CanvasColor.FromHex("#FF9800");

    /// <summary>Gets or sets the success color.</summary>
    public CanvasColor SuccessColor { get; set; } = CanvasColor.FromHex("#4CAF50");

    /// <summary>Gets or sets the grid spacing.</summary>
    public double GridSpacing { get; set; } = 20;

    /// <summary>Gets or sets whether to snap to grid.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>Gets or sets the node width.</summary>
    public double NodeWidth { get; set; } = 180;

    /// <summary>Gets or sets the port radius.</summary>
    public double PortRadius { get; set; } = 6;

    /// <summary>Gets or sets the connection line width.</summary>
    public double ConnectionLineWidth { get; set; } = 2;

    /// <summary>Gets or sets the node corner radius.</summary>
    public double NodeCornerRadius { get; set; } = 4;

    /// <summary>Gets or sets the font family.</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Gets or sets the font size.</summary>
    public double FontSize { get; set; } = 12;

    /// <summary>Gets or sets the header font size.</summary>
    public double HeaderFontSize { get; set; } = 14;

    /// <summary>Creates a light theme.</summary>
    public static CanvasTheme Light => new()
    {
        BackgroundColor = CanvasColor.FromHex("#F5F5F5"),
        GridColor = CanvasColor.FromHex("#E0E0E0"),
        NodeBackgroundColor = CanvasColor.White,
        NodeBorderColor = CanvasColor.FromHex("#BDBDBD"),
        TextColor = CanvasColor.FromHex("#212121"),
        SecondaryTextColor = CanvasColor.FromHex("#757575"),
        ConnectionColor = CanvasColor.FromHex("#9E9E9E")
    };

    /// <summary>Creates a dark theme (default).</summary>
    public static CanvasTheme Dark => new();
}
