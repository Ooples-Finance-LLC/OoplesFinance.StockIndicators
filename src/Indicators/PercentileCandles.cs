using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one for a candle whose body length is at or above its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class LongBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public LongBodyCandle(int period = 20, decimal percentile = 0.75m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, true, 0);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, true, 0), 0, 0)
    ];
}

/// <summary>Returns one for a candle whose body length is strictly below its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class ShortBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public ShortBodyCandle(int period = 20, decimal percentile = 0.25m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, false, 0);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, false, 0), 0, 0)
    ];
}

/// <summary>Returns one for a bullish candle whose body length is at or above its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class BullishLongBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public BullishLongBodyCandle(int period = 20, decimal percentile = 0.75m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, true, 1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, true, 1), 0, 0)
    ];
}

/// <summary>Returns one for a bearish candle whose body length is at or above its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class BearishLongBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public BearishLongBodyCandle(int period = 20, decimal percentile = 0.75m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, true, -1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, true, -1), 0, 0)
    ];
}

/// <summary>Returns one for a bullish candle whose body length is strictly below its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class BullishShortBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public BullishShortBodyCandle(int period = 20, decimal percentile = 0.25m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, false, 1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, false, 1), 0, 0)
    ];
}

/// <summary>Returns one for a bearish candle whose body length is strictly below its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class BearishShortBodyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public BearishShortBodyCandle(int period = 20, decimal percentile = 0.25m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Body, false, -1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Body, false, -1), 0, 0)
    ];
}

/// <summary>Returns one for a candle whose upper length is at or above its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class UpperShadowAtOrAbovePercentileCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public UpperShadowAtOrAbovePercentileCandle(int period = 20, decimal percentile = 0.75m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Upper, true, 0);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Upper, true, 0), 0, 0)
    ];
}

/// <summary>Returns one for a candle whose lower length is strictly below its rolling percentile; otherwise zero.</summary>
/// <remarks>The window includes the current candle. Percentiles use linear interpolation at fraction * (period - 1).
/// Prices and decimal percentile fractions retain exact comparisons. The first period - 1 bars return zero.</remarks>
public sealed class LowerShadowBelowPercentileCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period and a percentile fraction between zero and one, inclusive.</summary>
    public LowerShadowBelowPercentileCandle(int period = 20, decimal percentile = 0.25m)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period; Percentile = percentile;
    }
    /// <summary>Number of candle lengths in the window, including the current candle.</summary>
    public int Period { get; }
    /// <summary>Exact decimal quantile fraction.</summary>
    public decimal Percentile { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new PercentileCandleState(Period, Percentile, CandleLengthKind.Lower, false, 0);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => PercentileCandleReference.Evaluate(bars, Period, Percentile, CandleLengthKind.Lower, false, 0), 0, 0)
    ];
}

internal enum CandleLengthKind { Body, Upper, Lower }

internal sealed class PercentileCandleState : IIndicatorState
{
    private readonly int _period, _rank, _direction;
    private readonly CandleLengthKind _kind;
    private readonly bool _above;
    private readonly Queue<ExactMeanAccumulator> _window = new();
    internal PercentileCandleState(int period, decimal percentile, CandleLengthKind kind, bool above, int direction)
    {
        _period = period; _kind = kind; _above = above; _direction = direction;
        var bits = decimal.GetBits(percentile);
        var numerator = new BigInteger(unchecked((uint)bits[0])) + (new BigInteger(unchecked((uint)bits[1])) << 32) +
            (new BigInteger(unchecked((uint)bits[2])) << 64);
        var denominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
        _rank = (int)((numerator * (period - 1) + denominator - 1) / denominator);
    }
    public void Reset() => _window.Clear();
    public double Update(in Bar bar)
    {
        var current = new ExactMeanAccumulator();
        var top = Math.Max(bar.Open, bar.Close); var bottom = Math.Min(bar.Open, bar.Close);
        current.Add(_kind == CandleLengthKind.Upper ? bar.High : _kind == CandleLengthKind.Lower ? bottom : top);
        current.Add(_kind == CandleLengthKind.Upper ? top : _kind == CandleLengthKind.Lower ? bar.Low : bottom, -1);
        // Drop before enqueue so even the largest valid period cannot overflow Count.
        if (_window.Count == _period) _window.Dequeue();
        _window.Enqueue(current);
        if (_window.Count < _period || _direction > 0 && bar.Close <= bar.Open || _direction < 0 && bar.Close >= bar.Open) return 0;
        var atMost = 0;
        foreach (var previous in _window)
        {
            var difference = current; difference.Subtract(previous);
            if (difference.Sign >= 0) atMost++;
        }
        // Current is a member of the sorted window. It reaches an interpolated
        // quantile iff it reaches the upper interpolation endpoint; ties count fully.
        return (atMost > _rank) == _above ? 1 : 0;
    }
}

internal static class PercentileCandleReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period, decimal percentile,
        CandleLengthKind kind, bool above, int direction)
    {
        var text = percentile.ToString(CultureInfo.InvariantCulture);
        var point = text.IndexOf('.');
        var quantile = new ReferenceFraction(BigInteger.Parse(text.Replace(".", ""), CultureInfo.InvariantCulture)) /
            new ReferenceFraction(BigInteger.Pow(10, point < 0 ? 0 : text.Length - point - 1));
        var position = quantile * new ReferenceFraction(period - 1);
        var (numerator, denominator) = position.Components;
        var lowerIndex = (int)(numerator / denominator);
        var fraction = position - new ReferenceFraction(lowerIndex);
        var values = bars.Select(bar => kind switch
        {
            CandleLengthKind.Upper => R(bar.High) - R(Math.Max(bar.Open, bar.Close)),
            CandleLengthKind.Lower => R(Math.Min(bar.Open, bar.Close)) - R(bar.Low),
            _ => (R(bar.Close) - R(bar.Open)).Abs()
        }).ToArray();
        var result = new double[bars.Count];
        for (var i = period - 1; i < bars.Count; i++)
        {
            var sorted = values.Skip(i - period + 1).Take(period).OrderBy(v => v).ToArray();
            var threshold = sorted[lowerIndex];
            if (lowerIndex + 1 < period) threshold += (sorted[lowerIndex + 1] - threshold) * fraction;
            var bar = bars[i];
            var color = direction == 0 || direction > 0 && bar.Close > bar.Open || direction < 0 && bar.Close < bar.Open;
            result[i] = color && (values[i].CompareTo(threshold) >= 0) == above ? 1 : 0;
        }
        return result;
    }
    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
}
