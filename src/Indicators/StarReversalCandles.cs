using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a bullish morning reversal with a long first body, a strictly gapped short middle body, and a final opposite body exceeding its prior short-body mean and penetrating the first body. Returns 100 on a match, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the classified candle. Penetration is strict; the final close may extend beyond the entire first body.</remarks>
public sealed class MorningStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2 and finite nonnegative penetration.</summary>
    public MorningStarCandle(int longPeriod = 10, int shortPeriod = 10, double penetration = 0.3)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Nonnegative fraction of the first body that the final close must penetrate strictly.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StarReversalState(LongPeriod, ShortPeriod, 0, Penetration, true);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    StarReversalReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        0,
                        Penetration,
                        true
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a bearish evening reversal with a long first body, a strictly gapped short middle body, and a final opposite body exceeding its prior short-body mean and penetrating the first body. Returns -100 on a match, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the classified candle. Penetration is strict; the final close may extend beyond the entire first body.</remarks>
public sealed class EveningStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2 and finite nonnegative penetration.</summary>
    public EveningStarCandle(int longPeriod = 10, int shortPeriod = 10, double penetration = 0.3)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Nonnegative fraction of the first body that the final close must penetrate strictly.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StarReversalState(LongPeriod, ShortPeriod, 0, Penetration, false);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    StarReversalReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        0,
                        Penetration,
                        false
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a bullish morning reversal with a long first body, a strictly gapped doji middle body, and a final opposite body exceeding its prior short-body mean and penetrating the first body. Returns 100 on a match, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the classified candle. Penetration is strict; the final close may extend beyond the entire first body.</remarks>
public sealed class MorningDojiStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2 and finite nonnegative penetration.</summary>
    public MorningDojiStarCandle(
        int longPeriod = 10,
        int shortPeriod = 10,
        int dojiPeriod = 10,
        double penetration = 0.3
    )
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        if (dojiPeriod < 1 || dojiPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(dojiPeriod));
        DojiPeriod = dojiPeriod;
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Prior observations used for the doji threshold.</summary>
    public int DojiPeriod { get; }

    /// <summary>Nonnegative fraction of the first body that the final close must penetrate strictly.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Math.Max(LongPeriod, ShortPeriod), DojiPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StarReversalState(LongPeriod, ShortPeriod, DojiPeriod, Penetration, true);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    StarReversalReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        DojiPeriod,
                        Penetration,
                        true
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a bearish evening reversal with a long first body, a strictly gapped doji middle body, and a final opposite body exceeding its prior short-body mean and penetrating the first body. Returns -100 on a match, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the classified candle. Penetration is strict; the final close may extend beyond the entire first body.</remarks>
public sealed class EveningDojiStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2 and finite nonnegative penetration.</summary>
    public EveningDojiStarCandle(
        int longPeriod = 10,
        int shortPeriod = 10,
        int dojiPeriod = 10,
        double penetration = 0.3
    )
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        if (dojiPeriod < 1 || dojiPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(dojiPeriod));
        DojiPeriod = dojiPeriod;
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Prior observations used for the doji threshold.</summary>
    public int DojiPeriod { get; }

    /// <summary>Nonnegative fraction of the first body that the final close must penetrate strictly.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Math.Max(LongPeriod, ShortPeriod), DojiPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StarReversalState(LongPeriod, ShortPeriod, DojiPeriod, Penetration, false);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    StarReversalReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        DojiPeriod,
                        Penetration,
                        false
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a long first body and opposite final body separated by a doji with strict full-range gaps on both sides. The final body exceeds its prior mean and penetrates the first body. Returns the final color times 100, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the classified candle. Penetration is strict; the final close may extend beyond the entire first body.</remarks>
public sealed class AbandonedBabyCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2 and finite nonnegative penetration.</summary>
    public AbandonedBabyCandle(
        int longPeriod = 10,
        int shortPeriod = 10,
        int dojiPeriod = 10,
        double penetration = 0.3
    )
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        if (dojiPeriod < 1 || dojiPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(dojiPeriod));
        DojiPeriod = dojiPeriod;
        if (double.IsNaN(penetration) || double.IsInfinity(penetration) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Prior observations used for the doji threshold.</summary>
    public int DojiPeriod { get; }

    /// <summary>Nonnegative fraction of the first body that the final close must penetrate strictly.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Math.Max(LongPeriod, ShortPeriod), DojiPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StarReversalState(LongPeriod, ShortPeriod, DojiPeriod, Penetration, null, true);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    StarReversalReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        DojiPeriod,
                        Penetration,
                        null,
                        true
                    ),
                0,
                0
            ),
        ];
}

internal sealed class StarReversalState(
    int longPeriod,
    int shortPeriod,
    int dojiPeriod,
    double penetration,
    bool? morning,
    bool fullGaps = false
) : IIndicatorState
{
    private readonly Queue<Bar> _longs = new(),
        _shorts = new(),
        _dojis = new();
    private readonly int _warmup = Math.Max(Math.Max(longPeriod, shortPeriod), dojiPeriod) + 2;
    private readonly BigInteger _dojiDen = new BigInteger(dojiPeriod) * 10;
    private ExactMeanAccumulator _long,
        _long1,
        _long2,
        _short,
        _short1,
        _doji,
        _doji1;
    private Bar _first,
        _second;
    private int _seen;

    public void Reset()
    {
        _longs.Clear();
        _shorts.Clear();
        _dojis.Clear();
        _long = _long1 = _long2 = _short = _short1 = _doji = _doji1 = default;
        _first = _second = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var value =
            _seen >= _warmup && Matches(bar)
                ? IsMorning
                    ? 100d
                    : -100d
                : 0;
        _long2 = _long1;
        _long1 = _long;
        _short1 = _short;
        _doji1 = _doji;
        Accumulate(_longs, ref _long, bar, longPeriod, false);
        Accumulate(_shorts, ref _short, bar, shortPeriod, false);
        if (dojiPeriod > 0)
            Accumulate(_dojis, ref _doji, bar, dojiPeriod, true);
        _first = _second;
        _second = bar;
        if (_seen < _warmup)
            _seen++;
        return value;
    }

    private static void Accumulate(
        Queue<Bar> history,
        ref ExactMeanAccumulator sum,
        in Bar bar,
        int period,
        bool range
    )
    {
        if (history.Count == period)
            Add(ref sum, history.Dequeue(), range, -1);
        history.Enqueue(bar);
        Add(ref sum, bar, range, 1);
    }

    private static void Add(ref ExactMeanAccumulator sum, in Bar bar, bool range, int sign)
    {
        sum.Add(range ? bar.High : Math.Max(bar.Open, bar.Close), sign);
        sum.Add(range ? bar.Low : Math.Min(bar.Open, bar.Close), -sign);
    }

    private static int BodyMargin(in Bar bar, ExactMeanAccumulator sum, BigInteger divisor)
    {
        sum.Add(Math.Max(bar.Open, bar.Close), -divisor);
        sum.Add(Math.Min(bar.Open, bar.Close), divisor);
        return sum.Sign;
    }

    private bool IsMorning => morning ?? _first.Close < _first.Open;

    private bool Matches(in Bar c)
    {
        var a = _first;
        var b = _second;
        if (
            IsMorning
                ? a.Close >= a.Open || c.Close < c.Open || Math.Max(b.Open, b.Close) >= a.Close
                : a.Close < a.Open || c.Close >= c.Open || Math.Min(b.Open, b.Close) <= a.Close
        )
            return false;
        if (
            fullGaps
            && (IsMorning ? b.High >= a.Low || b.High >= c.Low : b.Low <= a.High || b.Low <= c.High)
        )
            return false;
        if (BodyMargin(a, _long2, longPeriod) >= 0 || BodyMargin(c, _short, shortPeriod) >= 0)
            return false;
        if (
            BodyMargin(
                b,
                dojiPeriod > 0 ? _doji1 : _short1,
                dojiPeriod > 0 ? _dojiDen : new BigInteger(shortPeriod)
            ) < 0
        )
            return false;
        var margin = new ExactMeanAccumulator();
        var direction = IsMorning ? 1 : -1;
        margin.Add(c.Close, direction);
        margin.Add(a.Close, -direction);
        margin.AddProduct(Math.Max(a.Open, a.Close), penetration, -1);
        margin.AddProduct(Math.Min(a.Open, a.Close), penetration);
        return margin.Sign > 0;
    }
}

internal static class StarReversalReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int longPeriod,
        int shortPeriod,
        int dojiPeriod,
        double penetration,
        bool? morning,
        bool fullGaps = false
    )
    {
        var result = new double[bars.Count];
        for (
            var i = Math.Max(Math.Max(longPeriod, shortPeriod), dojiPeriod) + 2;
            i < bars.Count;
            i++
        )
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var up = morning ?? a.Close < a.Open;
            if (
                up
                    ? a.Close >= a.Open || c.Close < c.Open || Math.Max(b.Open, b.Close) >= a.Close
                    : a.Close < a.Open || c.Close >= c.Open || Math.Min(b.Open, b.Close) <= a.Close
            )
                continue;
            if (
                Body(a).CompareTo(Mean(i - 2, longPeriod, false)) <= 0
                || Body(c).CompareTo(Mean(i, shortPeriod, false)) <= 0
            )
                continue;
            if (
                fullGaps
                && (up ? b.High >= a.Low || b.High >= c.Low : b.Low <= a.High || b.Low <= c.High)
            )
                continue;
            var middle =
                dojiPeriod > 0
                    ? Mean(i - 1, dojiPeriod, true) / new ReferenceFraction(10)
                    : Mean(i - 1, shortPeriod, false);
            if (Body(b).CompareTo(middle) > 0)
                continue;
            var distance = up ? R(c.Close) - R(a.Close) : R(a.Close) - R(c.Close);
            if (distance.CompareTo(Body(a) * R(penetration)) > 0)
                result[i] = up ? 100 : -100;
        }
        return result;
        ReferenceFraction Mean(int end, int period, bool range)
        {
            var sum = new ReferenceFraction(0);
            for (var j = end - period; j < end; j++)
                sum += range ? R(bars[j].High) - R(bars[j].Low) : Body(bars[j]);
            return sum / new ReferenceFraction(period);
        }
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Close) - R(b.Open)).Abs();
}
