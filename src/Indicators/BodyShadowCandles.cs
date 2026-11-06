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
    private readonly BigInteger _twicePeriod = new BigInteger(period) * 2;
    private readonly BigInteger _tenPeriod = new BigInteger(period) * 10;
    private readonly int _smallTwicePeriod = period <= int.MaxValue / 2 ? period * 2 : 0;
    private readonly int _smallTenPeriod = period <= int.MaxValue / 10 ? period * 10 : 0;
    private readonly bool _usesRange = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu or BodyShadowCandleKind.BeltHold;
    private readonly bool _usesShadow = kind is BodyShadowCandleKind.LongLine or BodyShadowCandleKind.ShortLine;
    private readonly bool _longBody = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu or BodyShadowCandleKind.BeltHold or BodyShadowCandleKind.LongLine;
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
        AddDifference(ref _bodySum, direction, top, bottom);
        // Only maintain the statistic read by this fixed pattern. In particular,
        // spinning-top/high-wave compare current shadows with the current body;
        // neither needs a rolling range or shadow sum.
        if (_usesRange)
        {
            AddDifference(ref _rangeSum, direction, bar.High, bar.Low);
        }
        else if (_usesShadow)
        {
            AddDifference(ref _shadowSum, direction, bar.High, top);
            AddDifference(ref _shadowSum, direction, bottom, bar.Low);
        }
    }

    private bool Matches(in Bar bar)
    {
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        var bodyComparison = Compare(_bodySum, period, top, bottom);
        if (_longBody ? bodyComparison >= 0 : bodyComparison <= 0) return false;
        return kind switch
        {
            BodyShadowCandleKind.Marubozu => CompareRange(bar.High, top) > 0 && CompareRange(bottom, bar.Low) > 0,
            BodyShadowCandleKind.ClosingMarubozu => bar.Close >= bar.Open
                ? CompareRange(bar.High, top) > 0 : CompareRange(bottom, bar.Low) > 0,
            BodyShadowCandleKind.BeltHold => bar.Close >= bar.Open
                ? CompareRange(bottom, bar.Low) > 0 : CompareRange(bar.High, top) > 0,
            BodyShadowCandleKind.SpinningTop => BothShadowsExceedBody(bar, top, bottom, 1),
            BodyShadowCandleKind.HighWave => BothShadowsExceedBody(bar, top, bottom, 2),
            _ => CompareShadow(bar.High, top) > 0 && CompareShadow(bottom, bar.Low) > 0
        };
    }

    private int CompareRange(double upper, double lower) => _smallTenPeriod != 0
        ? Compare(_rangeSum, _smallTenPeriod, upper, lower) : Compare(_rangeSum, _tenPeriod, upper, lower);

    private int CompareShadow(double upper, double lower) => _smallTwicePeriod != 0
        ? Compare(_shadowSum, _smallTwicePeriod, upper, lower) : Compare(_shadowSum, _twicePeriod, upper, lower);

    private static int Compare(ExactMeanAccumulator sum, int divisor, double upper, double lower)
    {
        AddDifference(ref sum, -divisor, upper, lower);
        return sum.Sign;
    }

    private static void AddDifference(ref ExactMeanAccumulator sum, int weight, double upper, double lower)
    {
#if !NETFRAMEWORK
        // Collapse the two exact terms only when subtraction loses no bits.
        if (Binary64ArithmeticCertificate.TryDifference(upper, lower, out double difference))
        {
            if (weight is 1 or -1)
                sum.AddFactor(new ExactMeanAccumulator.ProductFactor(weight == 1 ? difference : -difference));
            else if (Binary64ArithmeticCertificate.TryProduct(difference, weight, out double product))
                sum.AddFactor(new ExactMeanAccumulator.ProductFactor(product));
            else sum.Add(difference, weight);
            return;
        }
#endif
        sum.Add(upper, weight); sum.Add(lower, -weight);
    }

    private static int Compare(ExactMeanAccumulator sum, BigInteger divisor, double upper, double lower)
    {
        sum.Add(upper, -divisor);
        sum.Add(lower, divisor);
        return sum.Sign;
    }

    private static bool BothShadowsExceedBody(in Bar bar, double top, double bottom, int multiplier)
    {
#if !NETFRAMEWORK
        if ((multiplier is 1 or 2)
            && Binary64ArithmeticCertificate.TryDifference(top, bottom, out double body)
            && Binary64ArithmeticCertificate.TryDifference(bar.High, top, out double upperShadow)
            && Binary64ArithmeticCertificate.TryDifference(bottom, bar.Low, out double lowerShadow))
        {
            // Both supported multipliers are exact binary shifts, including
            // unpublished values beyond the finite binary64 range.
            var bodyFactor = new ExactMeanAccumulator.ProductFactor(-body, multiplier == 2 ? 1 : 0);
            var upperMargin = new ExactMeanAccumulator();
            upperMargin.AddFactor(new ExactMeanAccumulator.ProductFactor(upperShadow));
            upperMargin.AddFactor(bodyFactor);
            var lowerMargin = new ExactMeanAccumulator();
            lowerMargin.AddFactor(new ExactMeanAccumulator.ProductFactor(lowerShadow));
            lowerMargin.AddFactor(bodyFactor);
            return upperMargin.Sign > 0 && lowerMargin.Sign > 0;
        }
#endif
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
