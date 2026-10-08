using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one when close is strictly above open; otherwise zero.</summary>
public sealed class BullishCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.Bullish, 1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.Bullish, 1), 0, 0)
    ];
}

/// <summary>Returns one when close is strictly below open; otherwise zero.</summary>
public sealed class BearishCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.Bearish, 1);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.Bearish, 1), 0, 0)
    ];
}

/// <summary>Returns one after successive strictly higher highs and lows; otherwise zero.</summary>
/// <remarks>Period counts high/low transitions, requiring period + 1 candles. Equal endpoints break a trend. Incomplete history returns zero.</remarks>
public sealed class CandleUpTrend : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern using the specified number of trend transitions.</summary>
    public CandleUpTrend(int period = 3)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of consecutive high/low transitions required.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.UpTrend, Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.UpTrend, Period), 0, 0)
    ];
}

/// <summary>Returns one after successive strictly lower highs and lows; otherwise zero.</summary>
/// <remarks>Period counts high/low transitions, requiring period + 1 candles. Equal endpoints break a trend. Incomplete history returns zero.</remarks>
public sealed class CandleDownTrend : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern using the specified number of trend transitions.</summary>
    public CandleDownTrend(int period = 3)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of consecutive high/low transitions required.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.DownTrend, Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.DownTrend, Period), 0, 0)
    ];
}

/// <summary>Returns one for strict bullish body engulfing after a completed candle downtrend; otherwise zero.</summary>
/// <remarks>Period counts high/low transitions, requiring period + 1 candles. Equal endpoints break a trend. Incomplete history returns zero.</remarks>
public sealed class BullishEngulfingPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern using the specified number of trend transitions.</summary>
    public BullishEngulfingPattern(int period = 3)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of consecutive high/low transitions required.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.BullishEngulfing, Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.BullishEngulfing, Period), 0, 0)
    ];
}

/// <summary>Returns one for strict bearish body engulfing after a completed candle uptrend; otherwise zero.</summary>
/// <remarks>Period counts high/low transitions, requiring period + 1 candles. Equal endpoints break a trend. Incomplete history returns zero.</remarks>
public sealed class BearishEngulfingPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern using the specified number of trend transitions.</summary>
    public BearishEngulfingPattern(int period = 3)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of consecutive high/low transitions required.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new CandleTrendState(CandleTrendKind.BearishEngulfing, Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => CandleTrendReference.Evaluate(bars, CandleTrendKind.BearishEngulfing, Period), 0, 0)
    ];
}

internal enum CandleTrendKind { Bullish, Bearish, UpTrend, DownTrend, BullishEngulfing, BearishEngulfing }

internal sealed class CandleTrendState(CandleTrendKind kind, int period) : IIndicatorState
{
    private Bar _previous;
    private bool _hasPrevious;
    private int _up;
    private int _down;
    public void Reset() { _previous = default; _hasPrevious = false; _up = _down = 0; }
    public double Update(in Bar bar)
    {
        var result = kind switch
        {
            CandleTrendKind.Bullish => bar.Close > bar.Open,
            CandleTrendKind.Bearish => bar.Close < bar.Open,
            CandleTrendKind.BullishEngulfing => _hasPrevious && _down >= period &&
                _previous.Close < _previous.Open && bar.Close > bar.Open &&
                bar.Open < _previous.Close && bar.Close > _previous.Open,
            CandleTrendKind.BearishEngulfing => _hasPrevious && _up >= period &&
                _previous.Close > _previous.Open && bar.Close < bar.Open &&
                bar.Open > _previous.Close && bar.Close < _previous.Open,
            _ => false
        };
        if (_hasPrevious)
        {
            _up = bar.High > _previous.High && bar.Low > _previous.Low ? (_up < period ? _up + 1 : _up) : 0;
            _down = bar.High < _previous.High && bar.Low < _previous.Low ? (_down < period ? _down + 1 : _down) : 0;
        }
        if (kind == CandleTrendKind.UpTrend) result = _up >= period;
        if (kind == CandleTrendKind.DownTrend) result = _down >= period;
        _previous = bar;
        _hasPrevious = true;
        return result ? 1 : 0;
    }
}

internal static class CandleTrendReference
{
    // Re-scan the required window independently of the production run-length counters.
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, CandleTrendKind kind, int period)
    {
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = bars[i];
            if (kind == CandleTrendKind.Bullish) { output[i] = current.Open < current.Close ? 1 : 0; continue; }
            if (kind == CandleTrendKind.Bearish) { output[i] = current.Open > current.Close ? 1 : 0; continue; }
            var engulfing = kind is CandleTrendKind.BullishEngulfing or CandleTrendKind.BearishEngulfing;
            var end = engulfing ? i - 1 : i;
            if (end < period) continue;
            var rising = kind is CandleTrendKind.UpTrend or CandleTrendKind.BearishEngulfing;
            var matches = true;
            for (var j = end - period; j < end; j++)
            {
                if (rising ? bars[j].High >= bars[j + 1].High || bars[j].Low >= bars[j + 1].Low
                    : bars[j].High <= bars[j + 1].High || bars[j].Low <= bars[j + 1].Low)
                { matches = false; break; }
            }
            if (engulfing && matches)
            {
                var previous = bars[i - 1];
                var currentColor = current.Close.CompareTo(current.Open);
                var previousColor = previous.Close.CompareTo(previous.Open);
                matches = previousColor != 0 && currentColor != 0 &&
                    currentColor != previousColor && (currentColor > 0) == (kind == CandleTrendKind.BullishEngulfing) &&
                    Math.Min(current.Open, current.Close) < Math.Min(previous.Open, previous.Close) &&
                    Math.Max(current.Open, current.Close) > Math.Max(previous.Open, previous.Close);
            }
            output[i] = matches ? 1 : 0;
        }
        return output;
    }
}
