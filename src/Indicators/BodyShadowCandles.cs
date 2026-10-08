using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a long body and two very short shadows; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class MarubozuCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public MarubozuCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.Marubozu);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.Marubozu), 0, 0)
    ];
}

/// <summary>Recognizes a long body and a very short shadow at the closing end; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class ClosingMarubozuCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public ClosingMarubozuCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.ClosingMarubozu);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.ClosingMarubozu), 0, 0)
    ];
}

/// <summary>Recognizes a short body and both shadows longer than its body; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class SpinningTopCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public SpinningTopCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.SpinningTop);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.SpinningTop), 0, 0)
    ];
}

/// <summary>Recognizes a short body and both shadows longer than twice its body; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class HighWaveCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public HighWaveCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.HighWave);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.HighWave), 0, 0)
    ];
}

/// <summary>Recognizes a long body and both shadows below their prior average; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class LongLineCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public LongLineCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.LongLine);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.LongLine), 0, 0)
    ];
}

/// <summary>Recognizes a short body and both shadows below their prior average; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class ShortLineCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public ShortLineCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.ShortLine);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.ShortLine), 0, 0)
    ];
}

/// <summary>Recognizes a long body and a very short shadow at the opening end; returns +100 for bullish candles, -100 for bearish, and zero otherwise.</summary>
/// <remarks>Long/short bodies compare strictly with the preceding mean body. Very short shadows use one tenth
/// of the prior mean range; short shadows use half the prior mean total shadows. The current candle is excluded.
/// The first period bars return zero. Equal open and close is bullish when the remaining conditions qualify.</remarks>
public sealed class BeltHoldCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the recognizer with a positive prior-history period.</summary>
    public BeltHoldCandle(int period = 10)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles used by the adaptive thresholds.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period;
    /// <inheritdoc/>
    protected internal override object CreateState() => new BodyShadowCandleState(Period, BodyShadowCandleKind.BeltHold);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => BodyShadowCandleReference.Evaluate(bars, Period, BodyShadowCandleKind.BeltHold), 0, 0)
    ];
}

internal enum BodyShadowCandleKind { Marubozu, ClosingMarubozu, BeltHold, SpinningTop, HighWave, LongLine, ShortLine }

internal sealed class BodyShadowCandleState(int period, BodyShadowCandleKind kind) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly BigInteger _period = new(period);
    private readonly BigInteger _twicePeriod = new BigInteger(period) * 2;
    private readonly BigInteger _tenPeriod = new BigInteger(period) * 10;
    private ExactMeanAccumulator _bodySum;
    private ExactMeanAccumulator _rangeSum;
    private ExactMeanAccumulator _shadowSum;
    public void Reset() { _history.Clear(); _bodySum = _rangeSum = _shadowSum = default; }

    public double Update(in Bar bar)
    {
        var result = 0d;
        if (_history.Count == period && Matches(bar)) result = bar.Close >= bar.Open ? 100 : -100;
        _history.Enqueue(bar);
        Accumulate(bar, 1);
        if (_history.Count > period) Accumulate(_history.Dequeue(), -1);
        return result;
    }

    private void Accumulate(in Bar bar, int direction)
    {
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        _bodySum.Add(top, direction);
        _bodySum.Add(bottom, -direction);
        _rangeSum.Add(bar.High, direction);
        _rangeSum.Add(bar.Low, -direction);
        _shadowSum.Add(bar.High, direction);
        _shadowSum.Add(top, -direction);
        _shadowSum.Add(bottom, direction);
        _shadowSum.Add(bar.Low, -direction);
    }

    private bool Matches(in Bar bar)
    {
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        var bodyComparison = Compare(_bodySum, _period, top, bottom);
        var longBody = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu or BodyShadowCandleKind.BeltHold or BodyShadowCandleKind.LongLine;
        if (longBody ? bodyComparison >= 0 : bodyComparison <= 0) return false;
        return kind switch
        {
            BodyShadowCandleKind.Marubozu => Compare(_rangeSum, _tenPeriod, bar.High, top) > 0 && Compare(_rangeSum, _tenPeriod, bottom, bar.Low) > 0,
            BodyShadowCandleKind.ClosingMarubozu => bar.Close >= bar.Open
                ? Compare(_rangeSum, _tenPeriod, bar.High, top) > 0 : Compare(_rangeSum, _tenPeriod, bottom, bar.Low) > 0,
            BodyShadowCandleKind.BeltHold => bar.Close >= bar.Open
                ? Compare(_rangeSum, _tenPeriod, bottom, bar.Low) > 0 : Compare(_rangeSum, _tenPeriod, bar.High, top) > 0,
            BodyShadowCandleKind.SpinningTop => BothShadowsExceedBody(bar, top, bottom, 1),
            BodyShadowCandleKind.HighWave => BothShadowsExceedBody(bar, top, bottom, 2),
            _ => Compare(_shadowSum, _twicePeriod, bar.High, top) > 0 && Compare(_shadowSum, _twicePeriod, bottom, bar.Low) > 0
        };
    }

    private static int Compare(ExactMeanAccumulator sum, BigInteger divisor, double upper, double lower)
    {
        sum.Add(upper, -divisor);
        sum.Add(lower, divisor);
        return sum.Sign;
    }

    private static bool BothShadowsExceedBody(in Bar bar, double top, double bottom, int multiplier)
    {
        var upper = new ExactMeanAccumulator();
        upper.Add(bar.High); upper.Add(top, -1 - multiplier); upper.Add(bottom, multiplier);
        var lower = new ExactMeanAccumulator();
        lower.Add(bottom, 1 + multiplier); lower.Add(bar.Low, -1); lower.Add(top, -multiplier);
        return upper.Sign > 0 && lower.Sign > 0;
    }
}

internal static class BodyShadowCandleReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period, BodyShadowCandleKind kind)
    {
        var output = new double[bars.Count];
        for (var index = period; index < bars.Count; index++)
        {
            var bodyTotal = new ReferenceFraction(0);
            var rangeTotal = new ReferenceFraction(0);
            var shadowTotal = new ReferenceFraction(0);
            for (var i = index - period; i < index; i++)
            {
                var body = (ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i].Open)).Abs();
                var range = ReferenceFraction.FromDouble(bars[i].High) - ReferenceFraction.FromDouble(bars[i].Low);
                bodyTotal += body;
                rangeTotal += range;
                shadowTotal += range - body;
            }
            var candle = bars[index];
            var currentBody = (ReferenceFraction.FromDouble(candle.Close) - ReferenceFraction.FromDouble(candle.Open)).Abs();
            var upper = ReferenceFraction.FromDouble(candle.High) - ReferenceFraction.FromDouble(Math.Max(candle.Open, candle.Close));
            var lower = ReferenceFraction.FromDouble(Math.Min(candle.Open, candle.Close)) - ReferenceFraction.FromDouble(candle.Low);
            var meanBody = bodyTotal / new ReferenceFraction(period);
            var shortShadow = shadowTotal / (new ReferenceFraction(period) * new ReferenceFraction(2));
            var veryShortShadow = rangeTotal / (new ReferenceFraction(period) * new ReferenceFraction(10));
            var longBody = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu or BodyShadowCandleKind.BeltHold or BodyShadowCandleKind.LongLine;
            var bodyMatches = longBody ? currentBody.CompareTo(meanBody) > 0 : currentBody.CompareTo(meanBody) < 0;
            var shadowMatches = kind switch
            {
                BodyShadowCandleKind.Marubozu => upper.CompareTo(veryShortShadow) < 0 && lower.CompareTo(veryShortShadow) < 0,
                BodyShadowCandleKind.ClosingMarubozu => (candle.Close >= candle.Open ? upper : lower).CompareTo(veryShortShadow) < 0,
                BodyShadowCandleKind.BeltHold => (candle.Close >= candle.Open ? lower : upper).CompareTo(veryShortShadow) < 0,
                BodyShadowCandleKind.SpinningTop => upper.CompareTo(currentBody) > 0 && lower.CompareTo(currentBody) > 0,
                BodyShadowCandleKind.HighWave => upper.CompareTo(currentBody * new ReferenceFraction(2)) > 0 && lower.CompareTo(currentBody * new ReferenceFraction(2)) > 0,
                _ => upper.CompareTo(shortShadow) < 0 && lower.CompareTo(shortShadow) < 0
            };
            if (bodyMatches && shadowMatches) output[index] = candle.Close >= candle.Open ? 100 : -100;
        }
        return output;
    }
}
