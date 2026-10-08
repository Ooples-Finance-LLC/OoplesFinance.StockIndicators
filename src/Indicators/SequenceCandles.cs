using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Two bearish candles closing within one twentieth of the preceding mean range; returns 100 or zero.</summary>
/// <remarks>Range windows exclude the candle defining each tolerance. Boundaries are inclusive.
/// Matching-close tolerances use one twentieth of the mean range; near-body tolerances use one fifth.</remarks>
public sealed class MatchingLowCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period whose warmup fits Int32.</summary>
    public MatchingLowCandle(int period = 5)
    {
        if (period < 1 || period > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior ranges in each tolerance window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new SequenceCandleState(Period, WarmupBars, SequenceCandleKind.MatchingLow);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    SequenceCandleReference.Evaluate(
                        bars,
                        Period,
                        WarmupBars,
                        SequenceCandleKind.MatchingLow
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Bearish, bullish, bearish candles with the middle low above the first close and the last close near the first; returns 100 or zero.</summary>
/// <remarks>Range windows exclude the candle defining each tolerance. Boundaries are inclusive.
/// Matching-close tolerances use one twentieth of the mean range; near-body tolerances use one fifth.</remarks>
public sealed class StickSandwichCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period whose warmup fits Int32.</summary>
    public StickSandwichCandle(int period = 5)
    {
        if (period < 1 || period > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior ranges in each tolerance window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new SequenceCandleState(Period, WarmupBars, SequenceCandleKind.StickSandwich);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    SequenceCandleReference.Evaluate(
                        bars,
                        Period,
                        WarmupBars,
                        SequenceCandleKind.StickSandwich
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Three same-direction candles with advancing closes followed by an opposite candle crossing the first open; returns the first direction as 100 or -100, otherwise zero.</summary>
/// <remarks>Range windows exclude the candle defining each tolerance. Boundaries are inclusive.
/// Matching-close tolerances use one twentieth of the mean range; near-body tolerances use one fifth.</remarks>
public sealed class ThreeLineStrikeCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive period whose warmup fits Int32.</summary>
    public ThreeLineStrikeCandle(int period = 5)
    {
        if (period < 1 || period > int.MaxValue - 3)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior ranges in each tolerance window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 3;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new SequenceCandleState(Period, WarmupBars, SequenceCandleKind.ThreeLineStrike);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    SequenceCandleReference.Evaluate(
                        bars,
                        Period,
                        WarmupBars,
                        SequenceCandleKind.ThreeLineStrike
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes strict engulfing followed by a close beyond the engulfing close; returns that engulfing direction as 100 or -100, otherwise zero.</summary>
/// <remarks>The third candle's color is unrestricted. The first three bars return zero, matching the pinned TA-Lib lookback.</remarks>
public sealed class ThreeOutsideCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    public override int WarmupBars => 3;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new SequenceCandleState(0, WarmupBars, SequenceCandleKind.ThreeOutside);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    SequenceCandleReference.Evaluate(
                        bars,
                        0,
                        WarmupBars,
                        SequenceCandleKind.ThreeOutside
                    ),
                0,
                0
            ),
        ];
}

internal enum SequenceCandleKind
{
    MatchingLow,
    StickSandwich,
    ThreeLineStrike,
    ThreeOutside,
}

internal sealed class SequenceCandleState(int period, int warmup, SequenceCandleKind kind)
    : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private readonly Bar[] _previous = new Bar[3];
    private readonly ExactMeanAccumulator[] _lagRanges = new ExactMeanAccumulator[3];
    private readonly BigInteger _denominator =
        new BigInteger(period) * (kind == SequenceCandleKind.ThreeLineStrike ? 5 : 20);
    private ExactMeanAccumulator _range;
    private int _seen;

    public void Reset()
    {
        _history.Clear();
        Array.Clear(_previous, 0, 3);
        Array.Clear(_lagRanges, 0, 3);
        _range = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var result = _seen >= warmup ? Signal(bar) : 0;
        for (var i = 2; i > 0; i--)
        {
            _previous[i] = _previous[i - 1];
            _lagRanges[i] = _lagRanges[i - 1];
        }
        _previous[0] = bar;
        _lagRanges[0] = _range;
        if (period > 0)
        {
            if (_history.Count == period)
                Add(_history.Dequeue(), -1);
            _history.Enqueue(bar);
            Add(bar, 1);
        }
        if (_seen < warmup)
            _seen++;
        return result;
    }

    private void Add(in Bar bar, int sign)
    {
        _range.Add(bar.High, sign);
        _range.Add(bar.Low, -sign);
    }

    private static bool White(Bar bar) => bar.Close >= bar.Open;

    private double Signal(in Bar bar)
    {
        var last = _previous[0];
        var middle = _previous[1];
        var first = _previous[2];
        if (kind == SequenceCandleKind.MatchingLow)
            return !White(last) && !White(bar) && Near(bar.Close, last.Close, 0) ? 100 : 0;
        if (kind == SequenceCandleKind.StickSandwich)
            return
                !White(middle)
                && White(last)
                && !White(bar)
                && last.Low > middle.Close
                && Near(bar.Close, middle.Close, 1)
                ? 100
                : 0;
        if (kind == SequenceCandleKind.ThreeOutside)
        {
            var up =
                White(last)
                && !White(middle)
                && last.Close > middle.Open
                && last.Open < middle.Close
                && bar.Close > last.Close;
            var down =
                !White(last)
                && White(middle)
                && last.Open > middle.Close
                && last.Close < middle.Open
                && bar.Close < last.Close;
            return up ? 100
                : down ? -100
                : 0;
        }
        if (
            White(first) != White(middle)
            || White(middle) != White(last)
            || White(bar) == White(last)
            || !Within(middle.Open, first, 2)
            || !Within(last.Open, middle, 1)
        )
            return 0;
        return White(last)
            ? last.Close > middle.Close
            && middle.Close > first.Close
            && bar.Open > last.Close
            && bar.Close < first.Open
                ? 100
                : 0
            : last.Close < middle.Close
            && middle.Close < first.Close
            && bar.Open < last.Close
            && bar.Close > first.Open
                ? -100
                : 0;
    }

    private bool Near(double a, double b, int lag) =>
        Margin(Math.Max(a, b), Math.Min(a, b), lag) >= 0;

    private bool Within(double price, Bar body, int lag) =>
        Margin(price, Math.Max(body.Open, body.Close), lag) >= 0
        && Margin(Math.Min(body.Open, body.Close), price, lag) >= 0;

    private int Margin(double upper, double lower, int lag)
    {
        var margin = _lagRanges[lag];
        margin.Add(upper, -_denominator);
        margin.Add(lower, _denominator);
        return margin.Sign;
    }
}

internal static class SequenceCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int period,
        int warmup,
        SequenceCandleKind kind
    )
    {
        var result = new double[bars.Count];
        for (var i = warmup; i < bars.Count; i++)
        {
            var current = bars[i];
            var last = bars[i - 1];
            bool MatchClose(int prior) =>
                (R(current.Close) - R(bars[prior].Close)).Abs().CompareTo(Tolerance(prior, 20))
                <= 0;
            ReferenceFraction Tolerance(int end, int divisor)
            {
                var sum = new ReferenceFraction(0);
                for (var j = end - period; j < end; j++)
                    sum += R(bars[j].High) - R(bars[j].Low);
                return sum / (new ReferenceFraction(period) * new ReferenceFraction(divisor));
            }
            bool Inside(double open, int prior)
            {
                var tolerance = Tolerance(prior, 5);
                var bar = bars[prior];
                return R(open).CompareTo(R(Math.Min(bar.Open, bar.Close)) - tolerance) >= 0
                    && R(open).CompareTo(R(Math.Max(bar.Open, bar.Close)) + tolerance) <= 0;
            }
            if (kind == SequenceCandleKind.MatchingLow)
                result[i] =
                    last.Open > last.Close && current.Open > current.Close && MatchClose(i - 1)
                        ? 100
                        : 0;
            else if (kind == SequenceCandleKind.StickSandwich)
                result[i] =
                    bars[i - 2].Open > bars[i - 2].Close
                    && last.Close >= last.Open
                    && current.Open > current.Close
                    && last.Low > bars[i - 2].Close
                    && MatchClose(i - 2)
                        ? 100
                        : 0;
            else if (kind == SequenceCandleKind.ThreeOutside)
            {
                var first = bars[i - 2];
                if (
                    first.Close < first.Open
                    && last.Close >= last.Open
                    && last.Open < first.Close
                    && last.Close > first.Open
                    && current.Close > last.Close
                )
                    result[i] = 100;
                if (
                    first.Close >= first.Open
                    && last.Close < last.Open
                    && last.Open > first.Close
                    && last.Close < first.Open
                    && current.Close < last.Close
                )
                    result[i] = -100;
            }
            else
            {
                var first = bars[i - 3];
                var second = bars[i - 2];
                var white = first.Close >= first.Open;
                if (
                    (second.Close >= second.Open) != white
                    || (last.Close >= last.Open) != white
                    || (current.Close >= current.Open) == white
                    || !Inside(second.Open, i - 3)
                    || !Inside(last.Open, i - 2)
                )
                    continue;
                if (
                    white
                    && first.Close < second.Close
                    && second.Close < last.Close
                    && current.Open > last.Close
                    && current.Close < first.Open
                )
                    result[i] = 100;
                if (
                    !white
                    && first.Close > second.Close
                    && second.Close > last.Close
                    && current.Open < last.Close
                    && current.Close > first.Open
                )
                    result[i] = -100;
            }
        }
        return result;
    }

    private static ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
}
