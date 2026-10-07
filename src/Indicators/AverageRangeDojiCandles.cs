using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns 100 for a doji with body no larger than one tenth of the prior mean range; otherwise zero.</summary>
/// <remarks>The body limit is one tenth of the preceding period's mean high-low range, excluding the current bar.
/// Body equality qualifies. Shadow comparisons are strict. The first period bars return zero.</remarks>
public sealed class AverageRangeDoji : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-range period.</summary>
    public AverageRangeDoji(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior candle ranges included in the arithmetic mean.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new AverageRangeDojiState(Period, WarmupBars, AverageRangeDojiKind.Body);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 100),
        IndicatorValidationRule.Reference(0, bars => AverageRangeDojiReference.Evaluate(bars, Period, WarmupBars, AverageRangeDojiKind.Body), 0, 0)
    ];
}

/// <summary>Returns 100 for a doji with small body, very short upper shadow, and longer lower shadow; otherwise zero.</summary>
/// <remarks>The body limit is one tenth of the preceding period's mean high-low range, excluding the current bar.
/// Body equality qualifies. Shadow comparisons are strict. One additional startup bar matches TA-Lib.NETCore Dragonfly lookback.</remarks>
public sealed class AverageRangeDragonflyDoji : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-range period below Int32.MaxValue.</summary>
    public AverageRangeDragonflyDoji(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior candle ranges included in the arithmetic mean.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new AverageRangeDojiState(Period, WarmupBars, AverageRangeDojiKind.Dragonfly);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 100),
        IndicatorValidationRule.Reference(0, bars => AverageRangeDojiReference.Evaluate(bars, Period, WarmupBars, AverageRangeDojiKind.Dragonfly), 0, 0)
    ];
}

/// <summary>Returns 100 for a doji with small body, very short lower shadow, and longer upper shadow; otherwise zero.</summary>
/// <remarks>The body limit is one tenth of the preceding period's mean high-low range, excluding the current bar.
/// Body equality qualifies. Shadow comparisons are strict. The first period bars return zero.</remarks>
public sealed class AverageRangeGravestoneDoji : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-range period.</summary>
    public AverageRangeGravestoneDoji(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior candle ranges included in the arithmetic mean.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new AverageRangeDojiState(Period, WarmupBars, AverageRangeDojiKind.Gravestone);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 100),
        IndicatorValidationRule.Reference(0, bars => AverageRangeDojiReference.Evaluate(bars, Period, WarmupBars, AverageRangeDojiKind.Gravestone), 0, 0)
    ];
}

/// <summary>Returns 100 for a doji with small body and at least one shadow strictly longer than its body; otherwise zero.</summary>
/// <remarks>The body limit is one tenth of the preceding period's mean high-low range, excluding the current bar.
/// Body equality qualifies. Shadow comparisons are strict. The first period bars return zero.</remarks>
public sealed class AverageRangeLongLeggedDoji : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-range period.</summary>
    public AverageRangeLongLeggedDoji(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior candle ranges included in the arithmetic mean.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new AverageRangeDojiState(Period, WarmupBars, AverageRangeDojiKind.LongLegged);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 100),
        IndicatorValidationRule.Reference(0, bars => AverageRangeDojiReference.Evaluate(bars, Period, WarmupBars, AverageRangeDojiKind.LongLegged), 0, 0)
    ];
}

/// <summary>Returns 100 for a doji with a small body, a very short upper shadow, and a lower shadow strictly longer than twice the body; otherwise zero.</summary>
/// <remarks>The body limit is one tenth of the preceding period's mean high-low range, excluding the current bar.
/// Body equality qualifies. Shadow comparisons are strict. The first period bars return zero.</remarks>
public sealed class TakuriLineCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-range period.</summary>
    public TakuriLineCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior candle ranges included in the arithmetic mean.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new AverageRangeDojiState(Period, WarmupBars, AverageRangeDojiKind.TakuriLine);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 100),
        IndicatorValidationRule.Reference(0, bars => AverageRangeDojiReference.Evaluate(bars, Period, WarmupBars, AverageRangeDojiKind.TakuriLine), 0, 0)
    ];
}

internal enum AverageRangeDojiKind { Body, Dragonfly, Gravestone, LongLegged, TakuriLine }

internal sealed class AverageRangeDojiState(int period, int warmup, AverageRangeDojiKind kind) : IIndicatorState
{
    private readonly Queue<(double High, double Low)> _ranges = new();
    private readonly BigInteger _comparisonWeight = new BigInteger(period) * 10;
    private ExactMeanAccumulator _rangeSum;
    private int _seen;
    public void Reset() { _ranges.Clear(); _rangeSum = default; _seen = 0; }

    public double Update(in Bar bar)
    {
        var result = _seen >= warmup && Matches(bar) ? 100d : 0d;
        _ranges.Enqueue((bar.High, bar.Low));
        _rangeSum.Add(bar.High);
        _rangeSum.Add(bar.Low, -1);
        if (_ranges.Count > period)
        {
            var expired = _ranges.Dequeue();
            _rangeSum.Add(expired.High, -1);
            _rangeSum.Add(expired.Low);
        }
        if (_seen < warmup) _seen++;
        return result;
    }

    private bool Matches(in Bar bar)
    {
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        if (CompareWithLimit(top, bottom) < 0) return false;
        return kind switch
        {
            AverageRangeDojiKind.Dragonfly => CompareWithLimit(bar.High, top) > 0 && CompareWithLimit(bottom, bar.Low) < 0,
            AverageRangeDojiKind.Gravestone => CompareWithLimit(bottom, bar.Low) > 0 && CompareWithLimit(bar.High, top) < 0,
            AverageRangeDojiKind.TakuriLine => CompareWithLimit(bar.High, top) > 0 && ShadowLongerThanBody(bottom, bar.Low, top, bottom, 2),
            AverageRangeDojiKind.LongLegged => ShadowLongerThanBody(bar.High, top, top, bottom) || ShadowLongerThanBody(bottom, bar.Low, top, bottom),
            _ => true
        };
    }

    // Positive means the exact prior-range threshold exceeds this exact difference.
    private int CompareWithLimit(double upper, double lower)
    {
        var comparison = _rangeSum;
        comparison.Add(upper, -_comparisonWeight);
        comparison.Add(lower, _comparisonWeight);
        return comparison.Sign;
    }

    private static bool ShadowLongerThanBody(double upper, double lower, double top, double bottom, int multiplier = 1)
    {
        var comparison = new ExactMeanAccumulator();
        comparison.Add(upper);
        comparison.Add(lower, -1);
        comparison.Add(top, -multiplier);
        comparison.Add(bottom, multiplier);
        return comparison.Sign > 0;
    }
}

internal static class AverageRangeDojiReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period, int warmup, AverageRangeDojiKind kind)
    {
        var output = new double[bars.Count];
        for (var index = warmup; index < bars.Count; index++)
        {
            var sum = new ReferenceFraction(0);
            for (var previous = index - period; previous < index; previous++)
                sum += ReferenceFraction.FromDouble(bars[previous].High) - ReferenceFraction.FromDouble(bars[previous].Low);
            var limit = sum / (new ReferenceFraction(period) * new ReferenceFraction(10));
            var bar = bars[index];
            var open = ReferenceFraction.FromDouble(bar.Open);
            var close = ReferenceFraction.FromDouble(bar.Close);
            var body = (close - open).Abs();
            var top = open.CompareTo(close) > 0 ? open : close;
            var bottom = open.CompareTo(close) < 0 ? open : close;
            var upper = ReferenceFraction.FromDouble(bar.High) - top;
            var lower = bottom - ReferenceFraction.FromDouble(bar.Low);
            var shadows = kind switch
            {
                AverageRangeDojiKind.Dragonfly => upper.CompareTo(limit) < 0 && lower.CompareTo(limit) > 0,
                AverageRangeDojiKind.Gravestone => lower.CompareTo(limit) < 0 && upper.CompareTo(limit) > 0,
                AverageRangeDojiKind.TakuriLine => upper.CompareTo(limit) < 0 && lower.CompareTo(body * new ReferenceFraction(2)) > 0,
                AverageRangeDojiKind.LongLegged => upper.CompareTo(body) > 0 || lower.CompareTo(body) > 0,
                _ => true
            };
            output[index] = body.CompareTo(limit) <= 0 && shadows ? 100 : 0;
        }
        return output;
    }
}
