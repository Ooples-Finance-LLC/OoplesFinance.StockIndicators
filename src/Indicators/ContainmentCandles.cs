using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a short body contained inclusively in the preceding long body. Returns the opposite of the preceding candle direction as +100 or -100, otherwise zero.</summary>
/// <remarks>The preceding candle must have a body strictly larger than its preceding mean body.
/// The current body may equal its threshold: the preceding mean body for Harami/Homing Pigeon,
/// or one tenth of the preceding mean range for Harami Cross/Doji Star. Both windows exclude the candle being evaluated.</remarks>
public sealed class HaramiCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period below Int32.MaxValue.</summary>
    public HaramiCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles in each threshold window.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContainmentCandleState(Period, ContainmentCandleKind.Harami);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContainmentCandleReference.Evaluate(bars, Period, ContainmentCandleKind.Harami), 0, 0)
    ];
}

/// <summary>Recognizes a doji strictly contained in the preceding long body. Returns the opposite of the preceding candle direction as +100 or -100, otherwise zero.</summary>
/// <remarks>The preceding candle must have a body strictly larger than its preceding mean body.
/// The current body may equal its threshold: the preceding mean body for Harami/Homing Pigeon,
/// or one tenth of the preceding mean range for Harami Cross/Doji Star. Both windows exclude the candle being evaluated.</remarks>
public sealed class HaramiCrossCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period below Int32.MaxValue.</summary>
    public HaramiCrossCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles in each threshold window.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContainmentCandleState(Period, ContainmentCandleKind.HaramiCross);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContainmentCandleReference.Evaluate(bars, Period, ContainmentCandleKind.HaramiCross), 0, 0)
    ];
}

/// <summary>Recognizes a short bearish body strictly contained in the preceding long bearish body. Returns +100 when recognized, otherwise zero.</summary>
/// <remarks>The preceding candle must have a body strictly larger than its preceding mean body.
/// The current body may equal its threshold: the preceding mean body for Harami/Homing Pigeon,
/// or one tenth of the preceding mean range for Harami Cross/Doji Star. Both windows exclude the candle being evaluated.</remarks>
public sealed class HomingPigeonCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period below Int32.MaxValue.</summary>
    public HomingPigeonCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles in each threshold window.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContainmentCandleState(Period, ContainmentCandleKind.HomingPigeon);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContainmentCandleReference.Evaluate(bars, Period, ContainmentCandleKind.HomingPigeon), 0, 0)
    ];
}

/// <summary>Recognizes a doji with a strict body gap beyond the preceding long candle in its direction. Returns the opposite of the preceding candle direction as +100 or -100, otherwise zero.</summary>
/// <remarks>The preceding candle must have a body strictly larger than its preceding mean body.
/// The current body may equal its threshold: the preceding mean body for Harami/Homing Pigeon,
/// or one tenth of the preceding mean range for Harami Cross/Doji Star. Both windows exclude the candle being evaluated.</remarks>
public sealed class DojiStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period below Int32.MaxValue.</summary>
    public DojiStarCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of prior candles in each threshold window.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new ContainmentCandleState(Period, ContainmentCandleKind.DojiStar);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, bars => ContainmentCandleReference.Evaluate(bars, Period, ContainmentCandleKind.DojiStar), 0, 0)
    ];
}

internal enum ContainmentCandleKind { Harami, HaramiCross, HomingPigeon, DojiStar }

internal sealed class ContainmentCandleState(int period, ContainmentCandleKind kind) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly BigInteger _period = new(period);
    private readonly BigInteger _tenPeriod = new BigInteger(period) * 10;
    private ExactMeanAccumulator _bodySum, _rangeSum, _lagBodySum;
    private Bar _previous;
    private int _seen;
    public void Reset()
    {
        _history.Clear(); _bodySum = _rangeSum = _lagBodySum = default;
        _previous = default; _seen = 0;
    }
    public double Update(in Bar bar)
    {
        var result = _seen >= period + 1 && Matches(bar)
            ? kind == ContainmentCandleKind.HomingPigeon || _previous.Close < _previous.Open ? 100d : -100d : 0d;
        // This snapshot excludes the candle about to enter the current window.
        // On the next call it is the window preceding that previous candle.
        _lagBodySum = _bodySum;
        if (_history.Count == period) Accumulate(_history.Dequeue(), -1);
        _history.Enqueue(bar); Accumulate(bar, 1);
        _previous = bar;
        if (_seen < period + 1) _seen++;
        return result;
    }
    private void Accumulate(in Bar bar, int sign)
    {
        _bodySum.Add(Math.Max(bar.Open, bar.Close), sign); _bodySum.Add(Math.Min(bar.Open, bar.Close), -sign);
        _rangeSum.Add(bar.High, sign); _rangeSum.Add(bar.Low, -sign);
    }
    private bool Matches(in Bar bar)
    {
        var previousTop = Math.Max(_previous.Open, _previous.Close);
        var previousBottom = Math.Min(_previous.Open, _previous.Close);
        var longMargin = _lagBodySum;
        longMargin.Add(previousTop, -_period); longMargin.Add(previousBottom, _period);
        if (longMargin.Sign >= 0) return false;
        var top = Math.Max(bar.Open, bar.Close); var bottom = Math.Min(bar.Open, bar.Close);
        var doji = kind is ContainmentCandleKind.HaramiCross or ContainmentCandleKind.DojiStar;
        var shortMargin = doji ? _rangeSum : _bodySum;
        var denominator = doji ? _tenPeriod : _period;
        shortMargin.Add(top, -denominator); shortMargin.Add(bottom, denominator);
        if (shortMargin.Sign < 0) return false;
        return kind switch
        {
            ContainmentCandleKind.Harami => top <= previousTop && bottom >= previousBottom,
            ContainmentCandleKind.HaramiCross => top < previousTop && bottom > previousBottom,
            ContainmentCandleKind.HomingPigeon => _previous.Close < _previous.Open && bar.Close < bar.Open && top < previousTop && bottom > previousBottom,
            _ => _previous.Close >= _previous.Open ? bottom > previousTop : top < previousBottom
        };
    }
}

internal static class ContainmentCandleReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, int period, ContainmentCandleKind kind)
    {
        var result = new double[bars.Count];
        for (var i = period + 1; i < bars.Count; i++)
        {
            var longSum = new ReferenceFraction(0); var shortSum = new ReferenceFraction(0);
            var doji = kind is ContainmentCandleKind.HaramiCross or ContainmentCandleKind.DojiStar;
            for (var j = i - period - 1; j < i - 1; j++) longSum += Body(bars[j]);
            for (var j = i - period; j < i; j++) shortSum += doji ? R(bars[j].High) - R(bars[j].Low) : Body(bars[j]);
            if (Body(bars[i-1]).CompareTo(longSum / new ReferenceFraction(period)) <= 0 ||
                Body(bars[i]).CompareTo(shortSum / (new ReferenceFraction(period) * new ReferenceFraction(doji ? 10 : 1))) > 0) continue;
            var previous = bars[i-1]; var current = bars[i];
            var priorTop = Math.Max(previous.Open, previous.Close); var priorBottom = Math.Min(previous.Open, previous.Close);
            var top = Math.Max(current.Open, current.Close); var bottom = Math.Min(current.Open, current.Close);
            var matches = kind switch
            {
                ContainmentCandleKind.Harami => top <= priorTop && bottom >= priorBottom,
                ContainmentCandleKind.HaramiCross => top < priorTop && bottom > priorBottom,
                ContainmentCandleKind.HomingPigeon => previous.Open > previous.Close && current.Open > current.Close && top < priorTop && bottom > priorBottom,
                _ => previous.Close >= previous.Open ? bottom > priorTop : top < priorBottom
            };
            if (matches) result[i] = kind == ContainmentCandleKind.HomingPigeon || previous.Close < previous.Open ? 100 : -100;
        }
        return result;
    }
    private static ReferenceFraction Body(Bar bar) => (R(bar.Close) - R(bar.Open)).Abs();
    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
}
