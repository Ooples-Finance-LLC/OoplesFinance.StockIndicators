using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a long candle followed by three strictly short opposite candles overlapping its range, then a long candle resuming its direction. Returns the first color times 100 or zero.</summary>
public sealed class RisingFallingThreeMethodsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus four.</summary>
    public RisingFallingThreeMethodsCandle(int longPeriod = 10, int shortPeriod = 10)
    {
        FiveCandleContinuationState.Validate(longPeriod, shortPeriod);
        LongPeriod = longPeriod;
        ShortPeriod = shortPeriod;
    }

    /// <summary>Prior bodies used for each long-body threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior bodies used for each short-body threshold.</summary>
    public int ShortPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 4;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new FiveCandleContinuationState(LongPeriod, ShortPeriod, false, 0);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    FiveCandleContinuationReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        false,
                        0
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a long white first candle, a gapped black second candle and two declining short reaction bodies within the permitted penetration, followed by a white close above all reaction highs. Returns 100 or zero.</summary>
/// <remarks>The final body need not be long. All body and penetration boundaries are strict.</remarks>
public sealed class MatHoldCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus four and finite nonnegative penetration.</summary>
    public MatHoldCandle(int longPeriod = 10, int shortPeriod = 10, double penetration = .5)
    {
        FiveCandleContinuationState.Validate(longPeriod, shortPeriod);
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        LongPeriod = longPeriod;
        ShortPeriod = shortPeriod;
        Penetration = penetration;
    }

    /// <summary>Prior bodies used for the first long-body threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior bodies used for each short-body threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Maximum permitted fraction of the first body penetrated by the third and fourth bodies.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 4;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new FiveCandleContinuationState(LongPeriod, ShortPeriod, true, Penetration);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    FiveCandleContinuationReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        true,
                        Penetration
                    ),
                0,
                0
            ),
        ];
}

internal sealed class FiveCandleContinuationState(
    int longPeriod,
    int shortPeriod,
    bool matHold,
    double penetration
) : IIndicatorState
{
    internal static void Validate(int longPeriod, int shortPeriod)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 4)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 4)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
    }

    private readonly Queue<Bar> _longs = new(),
        _shorts = new();
    private readonly int _warmup = Math.Max(longPeriod, shortPeriod) + 4;
    private ExactMeanAccumulator _long,
        _l1,
        _l2,
        _l3,
        _l4,
        _short,
        _s1,
        _s2,
        _s3;
    private Bar _a,
        _b,
        _c,
        _d;
    private int _seen;

    public void Reset()
    {
        _longs.Clear();
        _shorts.Clear();
        _long = _l1 = _l2 = _l3 = _l4 = _short = _s1 = _s2 = _s3 = default;
        _a = _b = _c = _d = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var value = _seen >= _warmup ? Signal(bar) : 0;
        _l4 = _l3;
        _l3 = _l2;
        _l2 = _l1;
        _l1 = _long;
        _s3 = _s2;
        _s2 = _s1;
        _s1 = _short;
        Accumulate(_longs, ref _long, bar, longPeriod);
        Accumulate(_shorts, ref _short, bar, shortPeriod);
        _a = _b;
        _b = _c;
        _c = _d;
        _d = bar;
        if (_seen < _warmup)
            _seen++;
        return value;
    }

    private static void Accumulate(
        Queue<Bar> history,
        ref ExactMeanAccumulator sum,
        in Bar b,
        int period
    )
    {
        if (history.Count == period)
            Add(ref sum, history.Dequeue(), -1);
        history.Enqueue(b);
        Add(ref sum, b, 1);
    }

    private static void Add(ref ExactMeanAccumulator sum, in Bar b, int sign)
    {
        sum.Add(Math.Max(b.Open, b.Close), sign);
        sum.Add(Math.Min(b.Open, b.Close), -sign);
    }

    private static int Margin(in Bar b, ExactMeanAccumulator sum, int period)
    {
        Add(ref sum, b, -period);
        return sum.Sign;
    }

    private bool PenetrationAllowed(in Bar b)
    {
        var bottom = Math.Min(b.Open, b.Close);
        if (bottom >= _a.Close)
            return false;
        var margin = new ExactMeanAccumulator();
        margin.Add(bottom);
        margin.Add(_a.Close, -1);
        margin.AddProduct(_a.Close, penetration);
        margin.AddProduct(_a.Open, penetration, -1);
        return margin.Sign > 0;
    }

    private bool Overlaps(in Bar b) =>
        Math.Min(b.Open, b.Close) < _a.High && Math.Max(b.Open, b.Close) > _a.Low;

    private double Signal(in Bar e)
    {
        if (
            Margin(_a, _l4, longPeriod) >= 0
            || Margin(_b, _s3, shortPeriod) <= 0
            || Margin(_c, _s2, shortPeriod) <= 0
            || Margin(_d, _s1, shortPeriod) <= 0
        )
            return 0;
        if (matHold)
            return
                _a.Close >= _a.Open
                && _b.Close < _b.Open
                && e.Close >= e.Open
                && _b.Close > _a.Close
                && PenetrationAllowed(_c)
                && PenetrationAllowed(_d)
                && Math.Max(_c.Open, _c.Close) < _b.Open
                && Math.Max(_d.Open, _d.Close) < Math.Max(_c.Open, _c.Close)
                && e.Open > _d.Close
                && e.Close > Math.Max(Math.Max(_b.High, _c.High), _d.High)
                ? 100
                : 0;
        var white = _a.Close >= _a.Open;
        return
            Margin(e, _long, longPeriod) < 0
            && white != (_b.Close >= _b.Open)
            && white != (_c.Close >= _c.Open)
            && white != (_d.Close >= _d.Open)
            && white == (e.Close >= e.Open)
            && Overlaps(_b)
            && Overlaps(_c)
            && Overlaps(_d)
            && (
                white
                    ? _c.Close < _b.Close
                        && _d.Close < _c.Close
                        && e.Open > _d.Close
                        && e.Close > _a.Close
                    : _c.Close > _b.Close
                        && _d.Close > _c.Close
                        && e.Open < _d.Close
                        && e.Close < _a.Close
            )
            ? white
                ? 100
                : -100
            : 0;
    }
}

internal static class FiveCandleContinuationReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int longPeriod,
        int shortPeriod,
        bool matHold,
        double penetration
    )
    {
        var values = new double[bars.Count];
        for (var i = Math.Max(longPeriod, shortPeriod) + 4; i < bars.Count; i++)
        {
            var a = bars[i - 4];
            var b = bars[i - 3];
            var c = bars[i - 2];
            var d = bars[i - 1];
            var e = bars[i];
            if (
                Body(a).CompareTo(Mean(i - 4, longPeriod)) <= 0
                || Body(b).CompareTo(Mean(i - 3, shortPeriod)) >= 0
                || Body(c).CompareTo(Mean(i - 2, shortPeriod)) >= 0
                || Body(d).CompareTo(Mean(i - 1, shortPeriod)) >= 0
            )
                continue;
            bool match;
            if (matHold)
            {
                var threshold = R(a.Close) - Body(a) * R(penetration);
                match =
                    a.Close >= a.Open
                    && b.Close < b.Open
                    && e.Close >= e.Open
                    && b.Close > a.Close
                    && Math.Min(c.Open, c.Close) < a.Close
                    && Math.Min(d.Open, d.Close) < a.Close
                    && R(Math.Min(c.Open, c.Close)).CompareTo(threshold) > 0
                    && R(Math.Min(d.Open, d.Close)).CompareTo(threshold) > 0
                    && Math.Max(c.Open, c.Close) < b.Open
                    && Math.Max(d.Open, d.Close) < Math.Max(c.Open, c.Close)
                    && e.Open > d.Close
                    && e.Close > Math.Max(Math.Max(b.High, c.High), d.High);
                if (match)
                    values[i] = 100;
            }
            else
            {
                var white = a.Close >= a.Open;
                bool Overlaps(Bar middle) =>
                    Math.Min(middle.Open, middle.Close) < a.High
                    && Math.Max(middle.Open, middle.Close) > a.Low;
                match =
                    Body(e).CompareTo(Mean(i, longPeriod)) > 0
                    && white != (b.Close >= b.Open)
                    && white != (c.Close >= c.Open)
                    && white != (d.Close >= d.Open)
                    && white == (e.Close >= e.Open)
                    && Overlaps(b)
                    && Overlaps(c)
                    && Overlaps(d)
                    && (
                        white
                            ? c.Close < b.Close
                                && d.Close < c.Close
                                && e.Open > d.Close
                                && e.Close > a.Close
                            : c.Close > b.Close
                                && d.Close > c.Close
                                && e.Open < d.Close
                                && e.Close < a.Close
                    );
                if (match)
                    values[i] = white ? 100 : -100;
            }
        }
        return values;
        ReferenceFraction Mean(int end, int period)
        {
            var sum = new ReferenceFraction(0);
            for (var j = end - period; j < end; j++)
                sum += Body(bars[j]);
            return sum / new ReferenceFraction(period);
        }
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Close) - R(b.Open)).Abs();
}
