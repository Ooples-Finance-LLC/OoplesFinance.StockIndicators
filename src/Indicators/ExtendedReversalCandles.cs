using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a long first body, a body gap and continued high/low movement, then an opposite fifth candle closing strictly inside the original gap. Returns the final color times 100 or zero.</summary>
public sealed class BreakawayCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period at most Int32.MaxValue minus 4.</summary>
    public BreakawayCandle(int period = 10)
    {
        if (period < 1 || period > int.MaxValue - 4)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for each body or shadow threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 4;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExtendedReversalState(Period, ExtendedReversalKind.Breakaway);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExtendedReversalReference.Evaluate(
                        bars,
                        Period,
                        ExtendedReversalKind.Breakaway
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes four black candles: two with very short shadows, a gapped third body whose upper shadow enters the prior body, then strict full-range engulfment. Returns 100 or zero.</summary>
public sealed class ConcealingBabySwallowCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period at most Int32.MaxValue minus 3.</summary>
    public ConcealingBabySwallowCandle(int period = 10)
    {
        if (period < 1 || period > int.MaxValue - 3)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for each body or shadow threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 3;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExtendedReversalState(Period, ExtendedReversalKind.ConcealingBabySwallow);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExtendedReversalReference.Evaluate(
                        bars,
                        Period,
                        ExtendedReversalKind.ConcealingBabySwallow
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes three black candles with declining opens and closes, a fourth black candle with an upper shadow, and a white fifth candle opening above its body and closing above its high. Returns 100 or zero.</summary>
public sealed class LadderBottomCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period at most Int32.MaxValue minus 4.</summary>
    public LadderBottomCandle(int period = 10)
    {
        if (period < 1 || period > int.MaxValue - 4)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for each body or shadow threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 4;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExtendedReversalState(Period, ExtendedReversalKind.LadderBottom);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExtendedReversalReference.Evaluate(
                        bars,
                        Period,
                        ExtendedReversalKind.LadderBottom
                    ),
                0,
                0
            ),
        ];
}

internal enum ExtendedReversalKind
{
    Breakaway,
    ConcealingBabySwallow,
    LadderBottom,
}

internal sealed class ExtendedReversalState(int period, ExtendedReversalKind kind) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly int _warmup =
        period + (kind == ExtendedReversalKind.ConcealingBabySwallow ? 3 : 4);
    private readonly BigInteger _den =
        new BigInteger(period) * (kind == ExtendedReversalKind.Breakaway ? 1 : 10);
    private ExactMeanAccumulator _sum,
        _s1,
        _s2,
        _s3,
        _s4;
    private Bar _one,
        _two,
        _three,
        _four;
    private int _seen;

    public void Reset()
    {
        _history.Clear();
        _sum = _s1 = _s2 = _s3 = _s4 = default;
        _one = _two = _three = _four = default;
        _seen = 0;
    }

    public double Update(in Bar b)
    {
        var value = _seen >= _warmup ? Signal(b) : 0;
        _s4 = _s3;
        _s3 = _s2;
        _s2 = _s1;
        _s1 = _sum;
        if (_history.Count == period)
            Add(_history.Dequeue(), -1);
        _history.Enqueue(b);
        Add(b, 1);
        _one = _two;
        _two = _three;
        _three = _four;
        _four = b;
        if (_seen < _warmup)
            _seen++;
        return value;
    }

    private void Add(in Bar b, int sign)
    {
        var body = kind == ExtendedReversalKind.Breakaway;
        _sum.Add(body ? Math.Max(b.Open, b.Close) : b.High, sign);
        _sum.Add(body ? Math.Min(b.Open, b.Close) : b.Low, -sign);
    }

    private int Margin(double high, double low, ExactMeanAccumulator sum)
    {
        sum.Add(high, -_den);
        sum.Add(low, _den);
        return sum.Sign;
    }

    private double Signal(in Bar last)
    {
        var a = _one;
        var b = _two;
        var c = _three;
        var d = _four;
        if (kind == ExtendedReversalKind.ConcealingBabySwallow)
        {
            if (
                b.Close >= b.Open
                || c.Close >= c.Open
                || d.Close >= d.Open
                || last.Close >= last.Open
            )
                return 0;
            return
                Margin(b.Close, b.Low, _s3) > 0
                && Margin(b.High, b.Open, _s3) > 0
                && Margin(c.Close, c.Low, _s2) > 0
                && Margin(c.High, c.Open, _s2) > 0
                && d.Open < c.Close
                && Margin(d.High, d.Open, _s1) < 0
                && d.High > c.Close
                && last.High > d.High
                && last.Low < d.Low
                ? 100
                : 0;
        }
        if (kind == ExtendedReversalKind.LadderBottom)
            return
                a.Close < a.Open
                && b.Close < b.Open
                && c.Close < c.Open
                && d.Close < d.Open
                && a.Open > b.Open
                && b.Open > c.Open
                && a.Close > b.Close
                && b.Close > c.Close
                && Margin(d.High, d.Open, _s1) < 0
                && last.Close >= last.Open
                && last.Open > d.Open
                && last.Close > d.High
                ? 100
                : 0;
        var white = a.Close >= a.Open;
        if (
            Margin(Math.Max(a.Open, a.Close), Math.Min(a.Open, a.Close), _s4) >= 0
            || white != (b.Close >= b.Open)
            || white != (d.Close >= d.Open)
            || white == (last.Close >= last.Open)
        )
            return 0;
        var match = white
            ? b.Open > a.Close
                && c.High > b.High
                && c.Low > b.Low
                && d.High > c.High
                && d.Low > c.Low
                && last.Close < b.Open
                && last.Close > a.Close
            : b.Open < a.Close
                && c.High < b.High
                && c.Low < b.Low
                && d.High < c.High
                && d.Low < c.Low
                && last.Close > b.Open
                && last.Close < a.Close;
        return match
            ? white
                ? -100
                : 100
            : 0;
    }
}

internal static class ExtendedReversalReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int period,
        ExtendedReversalKind kind
    )
    {
        var result = new double[bars.Count];
        var offset = kind == ExtendedReversalKind.ConcealingBabySwallow ? 3 : 4;
        for (var i = period + offset; i < bars.Count; i++)
        {
            var d = bars[i - 1];
            var c = bars[i - 2];
            var b = bars[i - 3];
            var last = bars[i];
            bool match;
            if (kind == ExtendedReversalKind.ConcealingBabySwallow)
                match =
                    b.Close < b.Open
                    && c.Close < c.Open
                    && d.Close < d.Open
                    && last.Close < last.Open
                    && (R(b.Close) - R(b.Low)).CompareTo(Threshold(i - 3, false)) < 0
                    && (R(b.High) - R(b.Open)).CompareTo(Threshold(i - 3, false)) < 0
                    && (R(c.Close) - R(c.Low)).CompareTo(Threshold(i - 2, false)) < 0
                    && (R(c.High) - R(c.Open)).CompareTo(Threshold(i - 2, false)) < 0
                    && d.Open < c.Close
                    && (R(d.High) - R(d.Open)).CompareTo(Threshold(i - 1, false)) > 0
                    && d.High > c.Close
                    && last.High > d.High
                    && last.Low < d.Low;
            else
            {
                var a = bars[i - 4];
                if (kind == ExtendedReversalKind.LadderBottom)
                    match =
                        a.Close < a.Open
                        && b.Close < b.Open
                        && c.Close < c.Open
                        && d.Close < d.Open
                        && a.Open > b.Open
                        && b.Open > c.Open
                        && a.Close > b.Close
                        && b.Close > c.Close
                        && (R(d.High) - R(d.Open)).CompareTo(Threshold(i - 1, false)) > 0
                        && last.Close >= last.Open
                        && last.Open > d.Open
                        && last.Close > d.High;
                else
                {
                    var white = a.Close >= a.Open;
                    match =
                        (R(a.Close) - R(a.Open)).Abs().CompareTo(Threshold(i - 4, true)) > 0
                        && white == (b.Close >= b.Open)
                        && white == (d.Close >= d.Open)
                        && white != (last.Close >= last.Open)
                        && (
                            white
                                ? b.Open > a.Close
                                    && c.High > b.High
                                    && c.Low > b.Low
                                    && d.High > c.High
                                    && d.Low > c.Low
                                    && last.Close < b.Open
                                    && last.Close > a.Close
                                : b.Open < a.Close
                                    && c.High < b.High
                                    && c.Low < b.Low
                                    && d.High < c.High
                                    && d.Low < c.Low
                                    && last.Close > b.Open
                                    && last.Close < a.Close
                        );
                    if (match)
                        result[i] = white ? -100 : 100;
                    continue;
                }
            }
            if (match)
                result[i] = 100;
        }
        return result;
        ReferenceFraction Threshold(int end, bool body)
        {
            var sum = new ReferenceFraction(0);
            for (var j = end - period; j < end; j++)
                sum += body
                    ? (R(bars[j].Close) - R(bars[j].Open)).Abs()
                    : R(bars[j].High) - R(bars[j].Low);
            return sum / new ReferenceFraction(new BigInteger(period) * (body ? 1 : 10));
        }
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
}
