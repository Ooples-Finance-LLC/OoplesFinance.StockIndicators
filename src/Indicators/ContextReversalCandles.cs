using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a small-bodied candle with a long lower shadow with the body near the preceding low; returns 100 or zero.</summary>
/// <remarks>Body and very-short-shadow thresholds exclude the current bar. The near threshold excludes both current and immediately preceding bars; its boundary is inclusive.
/// Candle color does not change the signal direction.</remarks>
public sealed class HammerCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer; periods must be positive and below Int32.MaxValue.</summary>
    public HammerCandle(int period = 10, int nearPeriod = 5)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        if (nearPeriod < 1 || nearPeriod == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(nearPeriod));
        NearPeriod = nearPeriod;
        Period = period;
    }
    /// <summary>Prior body and high-low range averaging period.</summary>
    public int Period { get; }
    /// <summary>Number of ranges preceding the prior candle used by its one-fifth near threshold.</summary>
    public int NearPeriod { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Period, NearPeriod) + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContextReversalState(Period, NearPeriod, WarmupBars, ContextReversalKind.Hammer);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContextReversalReference.Evaluate(bars, Period, NearPeriod, WarmupBars, ContextReversalKind.Hammer), 0, 0)
    ];
}

/// <summary>Recognizes a small-bodied candle with a long lower shadow with the body near the preceding high; returns -100 or zero.</summary>
/// <remarks>Body and very-short-shadow thresholds exclude the current bar. The near threshold excludes both current and immediately preceding bars; its boundary is inclusive.
/// Candle color does not change the signal direction.</remarks>
public sealed class HangingManCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer; periods must be positive and below Int32.MaxValue.</summary>
    public HangingManCandle(int period = 10, int nearPeriod = 5)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        if (nearPeriod < 1 || nearPeriod == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(nearPeriod));
        NearPeriod = nearPeriod;
        Period = period;
    }
    /// <summary>Prior body and high-low range averaging period.</summary>
    public int Period { get; }
    /// <summary>Number of ranges preceding the prior candle used by its one-fifth near threshold.</summary>
    public int NearPeriod { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Period, NearPeriod) + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContextReversalState(Period, NearPeriod, WarmupBars, ContextReversalKind.HangingMan);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContextReversalReference.Evaluate(bars, Period, NearPeriod, WarmupBars, ContextReversalKind.HangingMan), 0, 0)
    ];
}

/// <summary>Recognizes a small-bodied candle with a long upper shadow and a strict downward body gap; returns 100 or zero.</summary>
/// <remarks>Body and very-short-shadow thresholds exclude the current bar. The two real bodies must be strictly separated; touching endpoints do not form a gap.
/// Candle color does not change the signal direction.</remarks>
public sealed class InvertedHammerCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer; periods must be positive and below Int32.MaxValue.</summary>
    public InvertedHammerCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior body and high-low range averaging period.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContextReversalState(Period, 0, WarmupBars, ContextReversalKind.InvertedHammer);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContextReversalReference.Evaluate(bars, Period, 0, WarmupBars, ContextReversalKind.InvertedHammer), 0, 0)
    ];
}

/// <summary>Recognizes a small-bodied candle with a long upper shadow and a strict upward body gap; returns -100 or zero.</summary>
/// <remarks>Body and very-short-shadow thresholds exclude the current bar. The two real bodies must be strictly separated; touching endpoints do not form a gap.
/// Candle color does not change the signal direction.</remarks>
public sealed class ShootingStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer; periods must be positive and below Int32.MaxValue.</summary>
    public ShootingStarCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Prior body and high-low range averaging period.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContextReversalState(Period, 0, WarmupBars, ContextReversalKind.ShootingStar);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContextReversalReference.Evaluate(bars, Period, 0, WarmupBars, ContextReversalKind.ShootingStar), 0, 0)
    ];
}

internal enum ContextReversalKind { Hammer, HangingMan, InvertedHammer, ShootingStar }

internal sealed class ContextReversalState(int period, int nearPeriod, int warmup, ContextReversalKind kind) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly Queue<Bar> _nearHistory = new();
    private readonly BigInteger _period = new(period);
    private readonly BigInteger _tenPeriod = new BigInteger(period) * 10;
    private readonly BigInteger _fiveNearPeriod = new BigInteger(nearPeriod) * 5;
    private ExactMeanAccumulator _bodySum, _rangeSum, _nearSum;
    private Bar _previous;
    private int _seen;
    public void Reset()
    {
        _history.Clear(); _nearHistory.Clear(); _bodySum = _rangeSum = _nearSum = default;
        _previous = default; _seen = 0;
    }
    public double Update(in Bar bar)
    {
        var result = 0d;
        if (_seen >= warmup && Matches(bar))
            result = kind is ContextReversalKind.Hammer or ContextReversalKind.InvertedHammer ? 100 : -100;
        if (nearPeriod > 0 && _seen > 0)
        {
            _nearHistory.Enqueue(_previous);
            AddRange(ref _nearSum, _previous, 1);
            if (_nearHistory.Count > nearPeriod) AddRange(ref _nearSum, _nearHistory.Dequeue(), -1);
        }
        _history.Enqueue(bar); Accumulate(bar, 1);
        if (_history.Count > period) Accumulate(_history.Dequeue(), -1);
        _previous = bar;
        if (_seen < warmup) _seen++;
        return result;
    }
    private void Accumulate(in Bar bar, int sign)
    {
        _bodySum.Add(Math.Max(bar.Open, bar.Close), sign);
        _bodySum.Add(Math.Min(bar.Open, bar.Close), -sign);
        AddRange(ref _rangeSum, bar, sign);
    }
    private static void AddRange(ref ExactMeanAccumulator sum, in Bar bar, int sign)
    {
        sum.Add(bar.High, sign); sum.Add(bar.Low, -sign);
    }
    private bool Matches(in Bar bar)
    {
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        if (Margin(_bodySum, _period, top, bottom) <= 0) return false;
        var lowerPattern = kind is ContextReversalKind.Hammer or ContextReversalKind.HangingMan;
        var longShadow = new ExactMeanAccumulator();
        longShadow.Add(lowerPattern ? bottom : bar.High);
        longShadow.Add(lowerPattern ? bar.Low : top, -1);
        longShadow.Add(top, -1); longShadow.Add(bottom);
        if (longShadow.Sign <= 0) return false;
        if (Margin(_rangeSum, _tenPeriod, lowerPattern ? bar.High : bottom, lowerPattern ? top : bar.Low) <= 0) return false;
        return kind switch
        {
            ContextReversalKind.Hammer => Margin(_nearSum, _fiveNearPeriod, bottom, _previous.Low) >= 0,
            ContextReversalKind.HangingMan => Margin(_nearSum, _fiveNearPeriod, _previous.High, bottom) >= 0,
            ContextReversalKind.InvertedHammer => top < Math.Min(_previous.Open, _previous.Close),
            _ => bottom > Math.Max(_previous.Open, _previous.Close)
        };
    }
    private static int Margin(ExactMeanAccumulator sum, BigInteger denominator, double upper, double lower)
    {
        sum.Add(upper, -denominator); sum.Add(lower, denominator); return sum.Sign;
    }
}

internal static class ContextReversalReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period, int nearPeriod, int warmup, ContextReversalKind kind)
    {
        var result = new double[bars.Count];
        for (var i = warmup; i < bars.Count; i++)
        {
            var bodySum = new ReferenceFraction(0);
            var rangeSum = new ReferenceFraction(0);
            for (var j = i - period; j < i; j++)
            {
                bodySum += (R(bars[j].Close) - R(bars[j].Open)).Abs();
                rangeSum += R(bars[j].High) - R(bars[j].Low);
            }
            var candle = bars[i];
            var top = R(Math.Max(candle.Open, candle.Close));
            var bottom = R(Math.Min(candle.Open, candle.Close));
            var body = top - bottom;
            var upper = R(candle.High) - top;
            var lower = bottom - R(candle.Low);
            var tiny = rangeSum / (new ReferenceFraction(period) * new ReferenceFraction(10));
            var lowerPattern = kind is ContextReversalKind.Hammer or ContextReversalKind.HangingMan;
            if (body.CompareTo(bodySum / new ReferenceFraction(period)) >= 0 ||
                (lowerPattern ? lower : upper).CompareTo(body) <= 0 || (lowerPattern ? upper : lower).CompareTo(tiny) >= 0) continue;
            if (!ContextMatches(bars, i, nearPeriod, kind, top, bottom)) continue;
            result[i] = kind is ContextReversalKind.Hammer or ContextReversalKind.InvertedHammer ? 100 : -100;
        }
        return result;
    }
    private static bool ContextMatches(IReadOnlyList<Bar> bars, int index, int nearPeriod, ContextReversalKind kind,
        ReferenceFraction top, ReferenceFraction bottom)
    {
        var previous = bars[index - 1];
        if (kind == ContextReversalKind.InvertedHammer) return top.CompareTo(R(Math.Min(previous.Open, previous.Close))) < 0;
        if (kind == ContextReversalKind.ShootingStar) return bottom.CompareTo(R(Math.Max(previous.Open, previous.Close))) > 0;
        var range = new ReferenceFraction(0);
        for (var i = index - nearPeriod - 1; i < index - 1; i++) range += R(bars[i].High) - R(bars[i].Low);
        var tolerance = range / (new ReferenceFraction(nearPeriod) * new ReferenceFraction(5));
        return kind == ContextReversalKind.Hammer ? bottom.CompareTo(R(previous.Low) + tolerance) <= 0
            : bottom.CompareTo(R(previous.High) - tolerance) >= 0;
    }
    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
}
