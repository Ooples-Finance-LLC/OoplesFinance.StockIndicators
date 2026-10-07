using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes three dojis against the same range mean preceding the first candle. A strict middle-body gap followed by a retreat of the final body signals reversal. Returns 100, -100, or zero.</summary>
public sealed class TristarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period at most Int32.MaxValue minus 2.</summary>
    public TristarCandle(int period = 10)
    {
        if (period < 1 || period > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for the range threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new GapContinuationState(Period, GapContinuationKind.Tristar);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    GapContinuationReference.Evaluate(bars, Period, GapContinuationKind.Tristar),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a strict body gap followed by an opposite candle opening inside the second body and closing inside the gap. The last two body sizes differ strictly less than their near tolerance. Returns 100, -100, or zero.</summary>
public sealed class TasukiGapCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period at most Int32.MaxValue minus 2.</summary>
    public TasukiGapCandle(int period = 5)
    {
        if (period < 1 || period > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior observations used for the range threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new GapContinuationState(Period, GapContinuationKind.TasukiGap);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    GapContinuationReference.Evaluate(bars, Period, GapContinuationKind.TasukiGap),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes two same-color candles separated by a strict body gap followed by an opposite candle opening strictly inside the second body and closing strictly inside the first. Returns 100, -100, or zero.</summary>
public sealed class UpDownSideGapThreeMethodsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer.</summary>
    public UpDownSideGapThreeMethodsCandle() { }

    /// <inheritdoc/>
    public override int WarmupBars => 0 + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new GapContinuationState(0, GapContinuationKind.UpDownSideGapThreeMethods);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    GapContinuationReference.Evaluate(
                        bars,
                        0,
                        GapContinuationKind.UpDownSideGapThreeMethods
                    ),
                0,
                0
            ),
        ];
}

internal enum GapContinuationKind
{
    Tristar,
    TasukiGap,
    UpDownSideGapThreeMethods,
}

internal sealed class GapContinuationState(int period, GapContinuationKind kind) : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly BigInteger _den =
        new BigInteger(period) * (kind == GapContinuationKind.Tristar ? 10 : 5);
    private ExactMeanAccumulator _range,
        _lag1,
        _lag2;
    private Bar _first,
        _second;
    private int _seen;

    public void Reset()
    {
        _history.Clear();
        _range = _lag1 = _lag2 = default;
        _first = _second = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var value = _seen >= period + 2 ? Signal(bar) : 0;
        _lag2 = _lag1;
        _lag1 = _range;
        if (period > 0)
        {
            if (_history.Count == period)
                Add(_history.Dequeue(), -1);
            _history.Enqueue(bar);
            Add(bar, 1);
        }
        _first = _second;
        _second = bar;
        if (_seen < period + 2)
            _seen++;
        return value;
    }

    private void Add(in Bar b, int sign)
    {
        _range.Add(b.High, sign);
        _range.Add(b.Low, -sign);
    }

    private bool Doji(in Bar b)
    {
        var sum = _lag2;
        sum.Add(Math.Max(b.Open, b.Close), -_den);
        sum.Add(Math.Min(b.Open, b.Close), _den);
        return sum.Sign >= 0;
    }

    private double Signal(in Bar c)
    {
        var a = _first;
        var b = _second;
        var upGap = Math.Min(b.Open, b.Close) > Math.Max(a.Open, a.Close);
        var downGap = Math.Max(b.Open, b.Close) < Math.Min(a.Open, a.Close);
        if (kind == GapContinuationKind.Tristar)
        {
            if (!Doji(a) || !Doji(b) || !Doji(c))
                return 0;
            if (upGap && Math.Max(c.Open, c.Close) < Math.Max(b.Open, b.Close))
                return -100;
            return downGap && Math.Min(c.Open, c.Close) > Math.Min(b.Open, b.Close) ? 100 : 0;
        }
        var white = b.Close >= b.Open;
        if (
            white == (c.Close >= c.Open)
            || c.Open <= Math.Min(b.Open, b.Close)
            || c.Open >= Math.Max(b.Open, b.Close)
        )
            return 0;
        if (kind == GapContinuationKind.UpDownSideGapThreeMethods)
            return
                white == (a.Close >= a.Open)
                && (white ? upGap : downGap)
                && c.Close > Math.Min(a.Open, a.Close)
                && c.Close < Math.Max(a.Open, a.Close)
                ? white
                    ? 100
                    : -100
                : 0;
        if (
            white
                ? !upGap || c.Close >= b.Open || c.Close <= Math.Max(a.Open, a.Close)
                : !downGap || c.Close <= b.Open || c.Close >= Math.Min(a.Open, a.Close)
        )
            return 0;
        var difference = new ExactMeanAccumulator();
        difference.Add(Math.Max(b.Open, b.Close), _den);
        difference.Add(Math.Min(b.Open, b.Close), -_den);
        difference.Add(Math.Max(c.Open, c.Close), -_den);
        difference.Add(Math.Min(c.Open, c.Close), _den);
        var margin = _lag1;
        if (difference.Sign < 0)
            margin.AddExact(difference);
        else
            margin.Subtract(difference);
        return margin.Sign > 0
            ? white
                ? 100
                : -100
            : 0;
    }
}

internal static class GapContinuationReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int period,
        GapContinuationKind kind
    )
    {
        var result = new double[bars.Count];
        for (var i = period + 2; i < bars.Count; i++)
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var sum = new ReferenceFraction(0);
            var end = kind == GapContinuationKind.Tristar ? i - 2 : i - 1;
            for (var j = end - period; j < end; j++)
                sum += R(bars[j].High) - R(bars[j].Low);
            var up = Math.Min(b.Open, b.Close) > Math.Max(a.Open, a.Close);
            var down = Math.Max(b.Open, b.Close) < Math.Min(a.Open, a.Close);
            if (kind == GapContinuationKind.Tristar)
            {
                var threshold = sum / new ReferenceFraction(new BigInteger(period) * 10);
                if (
                    Body(a).CompareTo(threshold) > 0
                    || Body(b).CompareTo(threshold) > 0
                    || Body(c).CompareTo(threshold) > 0
                )
                    continue;
                if (up && Math.Max(c.Open, c.Close) < Math.Max(b.Open, b.Close))
                    result[i] = -100;
                else if (down && Math.Min(c.Open, c.Close) > Math.Min(b.Open, b.Close))
                    result[i] = 100;
                continue;
            }
            var white = b.Close >= b.Open;
            if (
                white == (c.Close >= c.Open)
                || c.Open <= Math.Min(b.Open, b.Close)
                || c.Open >= Math.Max(b.Open, b.Close)
            )
                continue;
            bool match;
            if (kind == GapContinuationKind.UpDownSideGapThreeMethods)
                match =
                    white == (a.Close >= a.Open)
                    && (white ? up : down)
                    && c.Close > Math.Min(a.Open, a.Close)
                    && c.Close < Math.Max(a.Open, a.Close);
            else
                match =
                    (
                        white
                            ? up && c.Close < b.Open && c.Close > Math.Max(a.Open, a.Close)
                            : down && c.Close > b.Open && c.Close < Math.Min(a.Open, a.Close)
                    )
                    && (Body(b) - Body(c))
                        .Abs()
                        .CompareTo(sum / new ReferenceFraction(new BigInteger(period) * 5)) < 0;
            if (match)
                result[i] = white ? 100 : -100;
        }
        return result;
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Close) - R(b.Open)).Abs();
}
