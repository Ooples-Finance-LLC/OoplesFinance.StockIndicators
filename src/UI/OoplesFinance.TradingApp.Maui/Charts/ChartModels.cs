using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace OoplesFinance.TradingApp.Maui.Charts;

/// <summary>
/// Factory for creating chart series with consistent styling.
/// </summary>
public static class ChartFactory
{
    // Chart colors matching the app theme
    private static readonly SKColor PrimaryColor = SKColor.Parse("#6366F1");
    private static readonly SKColor SuccessColor = SKColor.Parse("#10B981");
    private static readonly SKColor ErrorColor = SKColor.Parse("#EF4444");
    private static readonly SKColor SurfaceColor = SKColor.Parse("#1E293B");
    private static readonly SKColor TextMutedColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor GridColor = SKColor.Parse("#334155");

    /// <summary>
    /// Creates a portfolio value line chart series.
    /// </summary>
    public static ISeries[] CreatePortfolioSeries(IEnumerable<decimal> values, bool isPositive = true)
    {
        var color = isPositive ? SuccessColor : ErrorColor;
        var points = values.Select((v, i) => new ObservablePoint(i, (double)v)).ToArray();

        return new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = points,
                Fill = new LiveChartsCore.SkiaSharpView.Painting.LinearGradientPaint(
                    new[] { color.WithAlpha(80), color.WithAlpha(0) },
                    new SKPoint(0.5f, 0),
                    new SKPoint(0.5f, 1)),
                Stroke = new SolidColorPaint(color, 2),
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        };
    }

    /// <summary>
    /// Creates candlestick chart data from OHLC bars.
    /// </summary>
    public static ISeries[] CreateCandlestickSeries(IEnumerable<OhlcBar> bars)
    {
        var values = bars.Select(b => new FinancialPoint(b.Timestamp, (double)b.High, (double)b.Open, (double)b.Close, (double)b.Low)).ToArray();

        return new ISeries[]
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Values = values,
                UpStroke = new SolidColorPaint(SuccessColor, 2),
                DownStroke = new SolidColorPaint(ErrorColor, 2),
                UpFill = new SolidColorPaint(SuccessColor),
                DownFill = new SolidColorPaint(ErrorColor)
            }
        };
    }

    /// <summary>
    /// Creates a mini sparkline for position cards.
    /// </summary>
    public static ISeries[] CreateSparkline(IEnumerable<decimal> values, bool isPositive = true)
    {
        var color = isPositive ? SuccessColor : ErrorColor;
        var points = values.Select((v, i) => new ObservablePoint(i, (double)v)).ToArray();

        return new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = points,
                Fill = null,
                Stroke = new SolidColorPaint(color, 1.5f),
                GeometrySize = 0,
                LineSmoothness = 0.2
            }
        };
    }

    /// <summary>
    /// Creates volume bar chart series.
    /// </summary>
    public static ISeries[] CreateVolumeSeries(IEnumerable<long> volumes, IEnumerable<bool> isUp)
    {
        var volumeList = volumes.ToList();
        var upList = isUp.ToList();
        var values = new ObservableCollection<ObservablePoint>();

        for (int i = 0; i < volumeList.Count; i++)
        {
            values.Add(new ObservablePoint(i, volumeList[i]));
        }

        return new ISeries[]
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = values,
                Fill = new SolidColorPaint(TextMutedColor.WithAlpha(100)),
                Stroke = null,
                MaxBarWidth = 8
            }
        };
    }

    /// <summary>
    /// Creates a donut chart for portfolio allocation.
    /// </summary>
    public static ISeries[] CreateAllocationDonut(IEnumerable<(string Name, decimal Value)> allocations)
    {
        var colors = new[] { PrimaryColor, SuccessColor, SKColor.Parse("#F59E0B"), ErrorColor, SKColor.Parse("#8B5CF6") };
        var values = allocations.Select((a, i) => new PieSeries<decimal>
        {
            Values = new[] { a.Value },
            Name = a.Name,
            Fill = new SolidColorPaint(colors[i % colors.Length]),
            InnerRadius = 50,
            MaxRadialColumnWidth = 30
        }).ToArray();

        return values;
    }

    /// <summary>
    /// Creates standard X axis for charts.
    /// </summary>
    public static ICartesianAxis[] CreateDateXAxis(bool showLabels = true)
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                LabelsPaint = showLabels ? new SolidColorPaint(TextMutedColor) : null,
                SeparatorsPaint = new SolidColorPaint(GridColor, 1),
                ShowSeparatorLines = true,
                TextSize = 10
            }
        };
    }

    /// <summary>
    /// Creates standard Y axis for price charts.
    /// </summary>
    public static ICartesianAxis[] CreatePriceYAxis(bool showLabels = true)
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                LabelsPaint = showLabels ? new SolidColorPaint(TextMutedColor) : null,
                SeparatorsPaint = new SolidColorPaint(GridColor, 1),
                ShowSeparatorLines = true,
                TextSize = 10,
                Labeler = value => value.ToString("C0"),
                Position = AxisPosition.End
            }
        };
    }

    /// <summary>
    /// Creates hidden axes for sparklines.
    /// </summary>
    public static ICartesianAxis[] CreateHiddenAxis()
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                LabelsPaint = null,
                SeparatorsPaint = null,
                ShowSeparatorLines = false
            }
        };
    }

    // Indicator colors
    private static readonly SKColor SmaColor = SKColor.Parse("#F59E0B");      // Orange/Yellow
    private static readonly SKColor EmaColor = SKColor.Parse("#8B5CF6");      // Purple
    private static readonly SKColor BbUpperColor = SKColor.Parse("#60A5FA");  // Light blue
    private static readonly SKColor BbLowerColor = SKColor.Parse("#60A5FA");  // Light blue
    private static readonly SKColor BbMiddleColor = SKColor.Parse("#3B82F6"); // Blue
    private static readonly SKColor RsiColor = SKColor.Parse("#EC4899");      // Pink

    /// <summary>
    /// Creates a line series for SMA indicator.
    /// </summary>
    public static LineSeries<ObservablePoint> CreateSmaSeries(IEnumerable<double> values)
    {
        var points = values.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        return new LineSeries<ObservablePoint>
        {
            Values = points,
            Fill = null,
            Stroke = new SolidColorPaint(SmaColor, 1.5f),
            GeometrySize = 0,
            LineSmoothness = 0
        };
    }

    /// <summary>
    /// Creates a line series for EMA indicator.
    /// </summary>
    public static LineSeries<ObservablePoint> CreateEmaSeries(IEnumerable<double> values)
    {
        var points = values.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        return new LineSeries<ObservablePoint>
        {
            Values = points,
            Fill = null,
            Stroke = new SolidColorPaint(EmaColor, 1.5f),
            GeometrySize = 0,
            LineSmoothness = 0
        };
    }

    /// <summary>
    /// Creates line series for Bollinger Bands (upper, middle, lower).
    /// </summary>
    public static (LineSeries<ObservablePoint> Upper, LineSeries<ObservablePoint> Middle, LineSeries<ObservablePoint> Lower)
        CreateBollingerBandsSeries(IEnumerable<double> upper, IEnumerable<double> middle, IEnumerable<double> lower)
    {
        var upperPoints = upper.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        var middlePoints = middle.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        var lowerPoints = lower.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();

        return (
            new LineSeries<ObservablePoint>
            {
                Values = upperPoints,
                Fill = null,
                Stroke = new SolidColorPaint(BbUpperColor, 1f),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = middlePoints,
                Fill = null,
                Stroke = new SolidColorPaint(BbMiddleColor, 1.5f),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = lowerPoints,
                Fill = null,
                Stroke = new SolidColorPaint(BbLowerColor, 1f),
                GeometrySize = 0,
                LineSmoothness = 0
            }
        );
    }

    /// <summary>
    /// Creates a line series for RSI indicator (0-100 scale).
    /// </summary>
    public static ISeries[] CreateRsiSeries(IEnumerable<double> values)
    {
        var points = values.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();

        // RSI overbought/oversold lines
        var overbought = Enumerable.Range(0, points.Length).Select(i => new ObservablePoint(i, 70)).ToArray();
        var oversold = Enumerable.Range(0, points.Length).Select(i => new ObservablePoint(i, 30)).ToArray();

        return new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = overbought,
                Fill = null,
                Stroke = new SolidColorPaint(ErrorColor.WithAlpha(100), 1f),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = oversold,
                Fill = null,
                Stroke = new SolidColorPaint(SuccessColor.WithAlpha(100), 1f),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = points,
                Fill = null,
                Stroke = new SolidColorPaint(RsiColor, 2f),
                GeometrySize = 0,
                LineSmoothness = 0
            }
        };
    }

    /// <summary>
    /// Creates MACD series (MACD line, Signal line, Histogram).
    /// </summary>
    public static ISeries[] CreateMacdSeries(IEnumerable<double> macdLine, IEnumerable<double> signalLine, IEnumerable<double> histogram)
    {
        var macdPoints = macdLine.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        var signalPoints = signalLine.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();
        var histPoints = histogram.Select((v, i) => new ObservablePoint(i, double.IsNaN(v) ? (double?)null : v)).ToArray();

        return new ISeries[]
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = histPoints,
                Fill = new SolidColorPaint(TextMutedColor.WithAlpha(150)),
                Stroke = null,
                MaxBarWidth = 6
            },
            new LineSeries<ObservablePoint>
            {
                Values = signalPoints,
                Fill = null,
                Stroke = new SolidColorPaint(ErrorColor, 1.5f),
                GeometrySize = 0,
                LineSmoothness = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = macdPoints,
                Fill = null,
                Stroke = new SolidColorPaint(PrimaryColor, 2f),
                GeometrySize = 0,
                LineSmoothness = 0
            }
        };
    }

    /// <summary>
    /// Creates Y axis for RSI panel (0-100 scale).
    /// </summary>
    public static ICartesianAxis[] CreateRsiYAxis()
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(TextMutedColor),
                SeparatorsPaint = new SolidColorPaint(GridColor, 1),
                ShowSeparatorLines = true,
                TextSize = 10,
                MinLimit = 0,
                MaxLimit = 100,
                Position = AxisPosition.End
            }
        };
    }

    /// <summary>
    /// Creates Y axis for MACD panel.
    /// </summary>
    public static ICartesianAxis[] CreateMacdYAxis()
    {
        return new ICartesianAxis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(TextMutedColor),
                SeparatorsPaint = new SolidColorPaint(GridColor, 1),
                ShowSeparatorLines = true,
                TextSize = 10,
                Position = AxisPosition.End,
                Labeler = value => value.ToString("F2")
            }
        };
    }
}

/// <summary>
/// OHLC bar data for candlestick charts.
/// </summary>
public class OhlcBar
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>
/// Generates realistic mock chart data for development.
/// </summary>
public static class MockChartData
{
    private static readonly Random Random = new(42); // Deterministic seed for consistent data

    /// <summary>
    /// Generates portfolio value history with realistic fluctuations.
    /// </summary>
    public static List<decimal> GeneratePortfolioHistory(decimal currentValue, int days = 30)
    {
        var values = new List<decimal>();
        var value = currentValue * 0.85m; // Start 15% lower
        var dailyReturn = Math.Pow((double)(currentValue / value), 1.0 / days) - 1;

        for (int i = 0; i < days; i++)
        {
            values.Add(value);
            var randomFactor = 1 + (Random.NextDouble() - 0.5) * 0.03; // +/- 1.5% daily noise
            value *= (decimal)(1 + dailyReturn) * (decimal)randomFactor;
        }

        values.Add(currentValue); // End at current value
        return values;
    }

    /// <summary>
    /// Generates OHLC bars for a stock.
    /// </summary>
    public static List<OhlcBar> GenerateOhlcBars(decimal startPrice, int count = 30, string timeframe = "1D")
    {
        var bars = new List<OhlcBar>();
        var price = startPrice;
        var now = DateTime.Now;

        for (int i = count - 1; i >= 0; i--)
        {
            var timestamp = timeframe switch
            {
                "1D" => now.AddMinutes(-i * 5),
                "1W" => now.AddHours(-i),
                "1M" => now.AddDays(-i),
                "3M" => now.AddDays(-i * 3),
                "1Y" => now.AddDays(-i * 12),
                "ALL" => now.AddDays(-i * 30),
                _ => now.AddDays(-i)
            };

            var volatility = (decimal)(Random.NextDouble() * 0.04 + 0.01);
            var trend = (decimal)((Random.NextDouble() - 0.48) * 0.02);

            var open = price;
            var close = price * (1 + trend + (decimal)(Random.NextDouble() - 0.5) * volatility);
            var high = Math.Max(open, close) * (1 + (decimal)Random.NextDouble() * volatility * 0.5m);
            var low = Math.Min(open, close) * (1 - (decimal)Random.NextDouble() * volatility * 0.5m);

            bars.Add(new OhlcBar
            {
                Timestamp = timestamp,
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = (long)(1000000 + Random.NextDouble() * 5000000)
            });

            price = close;
        }

        return bars;
    }

    /// <summary>
    /// Generates sparkline data for a stock.
    /// </summary>
    public static List<decimal> GenerateSparkline(decimal currentPrice, int points = 20)
    {
        var values = new List<decimal>();
        var price = currentPrice * (decimal)(0.95 + Random.NextDouble() * 0.1);

        for (int i = 0; i < points; i++)
        {
            values.Add(price);
            var change = (decimal)((Random.NextDouble() - 0.48) * 0.02);
            price *= 1 + change;
        }

        // Adjust last value to match current price
        values.Add(currentPrice);
        return values;
    }

    /// <summary>
    /// Generates intraday price data.
    /// </summary>
    public static List<(DateTime Time, decimal Price)> GenerateIntradayPrices(decimal currentPrice, int minutesBars = 78)
    {
        var prices = new List<(DateTime, decimal)>();
        var marketOpen = DateTime.Today.AddHours(9).AddMinutes(30);
        var price = currentPrice * (decimal)(0.98 + Random.NextDouble() * 0.04);

        for (int i = 0; i < minutesBars; i++)
        {
            var time = marketOpen.AddMinutes(i * 5);
            prices.Add((time, Math.Round(price, 2)));
            var change = (decimal)((Random.NextDouble() - 0.48) * 0.005);
            price *= 1 + change;
        }

        return prices;
    }
}
