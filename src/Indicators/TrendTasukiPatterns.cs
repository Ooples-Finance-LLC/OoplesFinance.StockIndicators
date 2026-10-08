using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a trend-qualified upside Tasuki gap, returning one or zero.</summary>
/// <remarks>Requires a strictly bearish final candle opening inside the second bullish body and closing below its open; the final close may cross the entire gap. Trend counts strict high/low transitions ending at the second candle. This follows the Trady definition, which differs from TA-Lib.</remarks>
public sealed class UpsideTasukiGapPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive number of trend transitions.</summary>
    public UpsideTasukiGapPattern(int period = 3)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Required consecutive high/low transitions.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() => new TrendTasukiState(true, Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => TrendTasukiReference.Evaluate(bars, true, Period),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a trend-qualified downside Tasuki gap, returning one or zero.</summary>
/// <remarks>Requires a strictly bullish final candle closing strictly inside the full-range gap; its open need not lie inside the second bearish body. Trend counts strict high/low transitions ending at the second candle. This follows the Trady definition, which differs from TA-Lib.</remarks>
public sealed class DownsideTasukiGapPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive number of trend transitions.</summary>
    public DownsideTasukiGapPattern(int period = 3)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Required consecutive high/low transitions.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() => new TrendTasukiState(false, Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => TrendTasukiReference.Evaluate(bars, false, Period),
                0,
                0
            ),
        ];
}

internal sealed class TrendTasukiState(bool upside, int period) : IIndicatorState
{
    private Bar _first,
        _second;
    private int _count,
        _trend;

    public void Reset()
    {
        _first = _second = default;
        _count = _trend = 0;
    }

    public double Update(in Bar bar)
    {
        var match =
            _count == 2
            && _trend >= period
            && (
                upside
                    ? _first.Close > _first.Open
                        && _second.Close > _second.Open
                        && bar.Close < bar.Open
                        && _first.High < _second.Low
                        && bar.Open > _second.Open
                        && bar.Open < _second.Close
                        && bar.Close < _second.Open
                    : _first.Close < _first.Open
                        && _second.Close < _second.Open
                        && bar.Close > bar.Open
                        && _first.Low > _second.High
                        && bar.Close < _first.Low
                        && bar.Close > _second.High
            );
        if (_count > 0)
        {
            var continues = upside
                ? bar.High > _second.High && bar.Low > _second.Low
                : bar.High < _second.High && bar.Low < _second.Low;
            _trend = continues ? (_trend < period ? _trend + 1 : _trend) : 0;
        }
        _first = _second;
        _second = bar;
        if (_count < 2)
            _count++;
        return match ? 1 : 0;
    }
}

internal static class TrendTasukiReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, bool upside, int period)
    {
        var values = new double[bars.Count];
        for (var i = 2; i < bars.Count; i++)
        {
            if (i - 1 < period)
                continue;
            var trend = true;
            for (var j = i - period; j < i; j++)
            {
                if (
                    upside
                        ? bars[j].High <= bars[j - 1].High || bars[j].Low <= bars[j - 1].Low
                        : bars[j].High >= bars[j - 1].High || bars[j].Low >= bars[j - 1].Low
                )
                {
                    trend = false;
                    break;
                }
            }
            if (!trend)
                continue;
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var colors =
                a.Close.CompareTo(a.Open) == (upside ? 1 : -1)
                && b.Close.CompareTo(b.Open) == (upside ? 1 : -1)
                && c.Close.CompareTo(c.Open) == (upside ? -1 : 1);
            var gap = upside ? a.High < b.Low : b.High < a.Low;
            var endpoint = upside
                ? c.Open > Math.Min(b.Open, b.Close)
                    && c.Open < Math.Max(b.Open, b.Close)
                    && c.Close < b.Open
                : c.Close > b.High && c.Close < a.Low;
            values[i] = colors && gap && endpoint ? 1 : 0;
        }
        return values;
    }
}
