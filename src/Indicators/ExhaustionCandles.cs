using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes three white candles with rising closes whose advance weakens through body shrinkage or upper shadows. Returns -100 or zero.</summary>
public sealed class AdvanceBlockCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus two.</summary>
    public AdvanceBlockCandle(
        int longPeriod = 10,
        int shadowPeriod = 10,
        int nearPeriod = 5,
        int farPeriod = 5
    )
    {
        LongPeriod = ExhaustionCandleState.RequirePeriod(longPeriod, nameof(longPeriod));
        ShadowPeriod = ExhaustionCandleState.RequirePeriod(shadowPeriod, nameof(shadowPeriod));
        NearPeriod = ExhaustionCandleState.RequirePeriod(nearPeriod, nameof(nearPeriod));
        FarPeriod = ExhaustionCandleState.RequirePeriod(farPeriod, nameof(farPeriod));
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <summary>Prior observations used for the near threshold.</summary>
    public int NearPeriod { get; }

    /// <summary>Prior observations used for the far threshold.</summary>
    public int FarPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars =>
        Math.Max(Math.Max(Math.Max(LongPeriod, ShadowPeriod), NearPeriod), FarPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExhaustionCandleState(
            ExhaustionCandleKind.AdvanceBlock,
            LongPeriod,
            0,
            ShadowPeriod,
            NearPeriod,
            FarPeriod
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExhaustionCandleReference.Evaluate(
                        bars,
                        ExhaustionCandleKind.AdvanceBlock,
                        LongPeriod,
                        0,
                        ShadowPeriod,
                        NearPeriod,
                        FarPeriod
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes two long white candles followed by a strictly short white body with rising closes, near opens, and a very short second upper shadow. Returns -100 or zero.</summary>
public sealed class StalledCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus two.</summary>
    public StalledCandle(
        int longPeriod = 10,
        int shortPeriod = 10,
        int shadowPeriod = 10,
        int nearPeriod = 5
    )
    {
        LongPeriod = ExhaustionCandleState.RequirePeriod(longPeriod, nameof(longPeriod));
        ShortPeriod = ExhaustionCandleState.RequirePeriod(shortPeriod, nameof(shortPeriod));
        ShadowPeriod = ExhaustionCandleState.RequirePeriod(shadowPeriod, nameof(shadowPeriod));
        NearPeriod = ExhaustionCandleState.RequirePeriod(nearPeriod, nameof(nearPeriod));
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <summary>Prior observations used for the near threshold.</summary>
    public int NearPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars =>
        Math.Max(Math.Max(Math.Max(LongPeriod, ShortPeriod), ShadowPeriod), NearPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExhaustionCandleState(
            ExhaustionCandleKind.Stalled,
            LongPeriod,
            ShortPeriod,
            ShadowPeriod,
            NearPeriod,
            0
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExhaustionCandleReference.Evaluate(
                        bars,
                        ExhaustionCandleKind.Stalled,
                        LongPeriod,
                        ShortPeriod,
                        ShadowPeriod,
                        NearPeriod,
                        0
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes three black candles: a long first body with an even longer lower shadow, a smaller second body trading within the first low, then a small marubozu strictly within the second range. Returns 100 or zero.</summary>
public sealed class ThreeStarsInSouthCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus two.</summary>
    public ThreeStarsInSouthCandle(int longPeriod = 10, int shortPeriod = 10, int shadowPeriod = 10)
    {
        LongPeriod = ExhaustionCandleState.RequirePeriod(longPeriod, nameof(longPeriod));
        ShortPeriod = ExhaustionCandleState.RequirePeriod(shortPeriod, nameof(shortPeriod));
        ShadowPeriod = ExhaustionCandleState.RequirePeriod(shadowPeriod, nameof(shadowPeriod));
    }

    /// <summary>Prior observations used for the long threshold.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior observations used for the short threshold.</summary>
    public int ShortPeriod { get; }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Math.Max(LongPeriod, ShortPeriod), ShadowPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new ExhaustionCandleState(
            ExhaustionCandleKind.ThreeStarsInSouth,
            LongPeriod,
            ShortPeriod,
            ShadowPeriod,
            0,
            0
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    ExhaustionCandleReference.Evaluate(
                        bars,
                        ExhaustionCandleKind.ThreeStarsInSouth,
                        LongPeriod,
                        ShortPeriod,
                        ShadowPeriod,
                        0,
                        0
                    ),
                0,
                0
            ),
        ];
}

internal enum ExhaustionCandleKind
{
    AdvanceBlock,
    Stalled,
    ThreeStarsInSouth,
}

internal enum ExhaustionMeasure
{
    Body,
    Range,
    Shadows,
}

internal sealed class ExhaustionWindow(int period, ExhaustionMeasure measure)
{
    private readonly Queue<Bar> _history = new();
    private ExactMeanAccumulator _sum,
        _lag1,
        _lag2;
    internal int Period => period;

    internal ExactMeanAccumulator At(int lag) =>
        lag == 0 ? _sum
        : lag == 1 ? _lag1
        : _lag2;

    internal void Reset()
    {
        _history.Clear();
        _sum = _lag1 = _lag2 = default;
    }

    internal void Update(in Bar b)
    {
        _lag2 = _lag1;
        _lag1 = _sum;
        if (_history.Count == period)
            Add(_history.Dequeue(), -1);
        _history.Enqueue(b);
        Add(b, 1);
    }

    private void Add(in Bar b, int sign)
    {
        if (measure != ExhaustionMeasure.Body)
        {
            _sum.Add(b.High, sign);
            _sum.Add(b.Low, -sign);
        }
        if (measure != ExhaustionMeasure.Range)
        {
            var factor = measure == ExhaustionMeasure.Body ? sign : -sign;
            _sum.Add(Math.Max(b.Open, b.Close), factor);
            _sum.Add(Math.Min(b.Open, b.Close), -factor);
        }
    }
}

internal sealed class ExhaustionCandleState(
    ExhaustionCandleKind kind,
    int longPeriod,
    int shortPeriod,
    int shadowPeriod,
    int nearPeriod,
    int farPeriod
) : IIndicatorState
{
    internal static int RequirePeriod(int value, string name)
    {
        if (value < 1 || value > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(name);
        return value;
    }

    private readonly ExhaustionWindow _long = new(longPeriod, ExhaustionMeasure.Body),
        _shadow =
            new(
                shadowPeriod,
                kind == ExhaustionCandleKind.AdvanceBlock
                    ? ExhaustionMeasure.Shadows
                    : ExhaustionMeasure.Range
            );
    private readonly ExhaustionWindow? _short = shortPeriod > 0
            ? new(shortPeriod, ExhaustionMeasure.Body)
            : null,
        _near = nearPeriod > 0 ? new(nearPeriod, ExhaustionMeasure.Range) : null,
        _far = farPeriod > 0 ? new(farPeriod, ExhaustionMeasure.Range) : null;
    private readonly int _warmup =
        Math.Max(
            Math.Max(longPeriod, shortPeriod),
            Math.Max(shadowPeriod, Math.Max(nearPeriod, farPeriod))
        ) + 2;
    private Bar _first,
        _second;
    private int _seen;

    public void Reset()
    {
        _long.Reset();
        _shadow.Reset();
        _short?.Reset();
        _near?.Reset();
        _far?.Reset();
        _first = _second = default;
        _seen = 0;
    }

    public double Update(in Bar b)
    {
        var value =
            _seen >= _warmup && Matches(b)
                ? kind == ExhaustionCandleKind.ThreeStarsInSouth
                    ? 100d
                    : -100d
                : 0;
        _long.Update(b);
        _shadow.Update(b);
        _short?.Update(b);
        _near?.Update(b);
        _far?.Update(b);
        _first = _second;
        _second = b;
        if (_seen < _warmup)
            _seen++;
        return value;
    }

    private static ExactMeanAccumulator Threshold(ExhaustionWindow w, int lag, int numerator)
    {
        var sum = w.At(lag);
        var term = sum;
        for (var n = 1; n < numerator; n++)
            sum.AddExact(term);
        return sum;
    }

    private static int Margin(
        ExhaustionWindow w,
        int lag,
        double high,
        double low,
        int denominator = 1,
        int numerator = 1
    )
    {
        var sum = Threshold(w, lag, numerator);
        var den = new BigInteger(w.Period) * denominator;
        sum.Add(high, -den);
        sum.Add(low, den);
        return sum.Sign;
    }

    private static int BodyMargin(ExhaustionWindow w, int lag, in Bar b) =>
        Margin(w, lag, Math.Max(b.Open, b.Close), Math.Min(b.Open, b.Close));

    private static void Body(ref ExactMeanAccumulator sum, in Bar b, BigInteger weight)
    {
        sum.Add(Math.Max(b.Open, b.Close), weight);
        sum.Add(Math.Min(b.Open, b.Close), -weight);
    }

    private static int BodyDifference(in Bar a, in Bar b)
    {
        var sum = new ExactMeanAccumulator();
        Body(ref sum, a, BigInteger.One);
        Body(ref sum, b, -BigInteger.One);
        return sum.Sign;
    }

    private static int DeltaMargin(
        ExhaustionWindow w,
        int lag,
        in Bar a,
        in Bar b,
        int denominator,
        int numerator = 1
    )
    {
        var sum = Threshold(w, lag, numerator);
        var den = new BigInteger(w.Period) * denominator;
        Body(ref sum, a, -den);
        Body(ref sum, b, den);
        return sum.Sign;
    }

    private static int LengthDifference(double high, double low, double otherHigh, double otherLow)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(high);
        sum.Add(low, -1);
        sum.Add(otherHigh, -1);
        sum.Add(otherLow);
        return sum.Sign;
    }

    private bool Matches(in Bar c)
    {
        var a = _first;
        var b = _second;
        if (kind == ExhaustionCandleKind.ThreeStarsInSouth)
            return a.Close < a.Open
                && b.Close < b.Open
                && c.Close < c.Open
                && BodyMargin(_long, 2, a) < 0
                && LengthDifference(a.Close, a.Low, a.Open, a.Close) > 0
                && BodyDifference(b, a) < 0
                && b.Open > a.Close
                && b.Open <= a.High
                && b.Low < a.Close
                && b.Low >= a.Low
                && Margin(_shadow, 1, b.Close, b.Low, 10) < 0
                && BodyMargin(_short!, 0, c) > 0
                && Margin(_shadow, 0, c.Close, c.Low, 10) > 0
                && Margin(_shadow, 0, c.High, c.Open, 10) > 0
                && c.Low > b.Low
                && c.High < b.High;
        if (
            a.Close < a.Open
            || b.Close < b.Open
            || c.Close < c.Open
            || c.Close <= b.Close
            || b.Close <= a.Close
            || BodyMargin(_long, 2, a) >= 0
            || b.Open <= a.Open
            || Margin(_near!, 2, b.Open, a.Close, 5) < 0
        )
            return false;
        if (kind == ExhaustionCandleKind.Stalled)
            // For a white final body, open + body equals close exactly.
            return BodyMargin(_long, 1, b) < 0
                && Margin(_shadow, 1, b.High, b.Close, 10) > 0
                && BodyMargin(_short!, 0, c) > 0
                && Margin(_near!, 1, b.Close, c.Close, 5) >= 0;
        if (
            c.Open <= b.Open
            || Margin(_near!, 1, c.Open, b.Close, 5) < 0
            || Margin(_shadow, 2, a.High, a.Close, 2) <= 0
        )
            return false;
        return DeltaMargin(_far!, 2, a, b, 5, 3) < 0 && DeltaMargin(_near!, 1, c, b, 5) > 0
            || DeltaMargin(_far!, 1, b, c, 5, 3) < 0
            || BodyDifference(c, b) < 0
                && BodyDifference(b, a) < 0
                && (
                    Margin(_shadow, 0, c.High, c.Close, 2) < 0
                    || Margin(_shadow, 1, b.High, b.Close, 2) < 0
                )
            || BodyDifference(c, b) < 0 && LengthDifference(c.High, c.Close, c.Close, c.Open) > 0;
    }
}

internal static class ExhaustionCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        ExhaustionCandleKind kind,
        int longPeriod,
        int shortPeriod,
        int shadowPeriod,
        int nearPeriod,
        int farPeriod
    )
    {
        var result = new double[bars.Count];
        var warm =
            Math.Max(
                Math.Max(longPeriod, shortPeriod),
                Math.Max(shadowPeriod, Math.Max(nearPeriod, farPeriod))
            ) + 2;
        for (var i = warm; i < bars.Count; i++)
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var ba = Body(a);
            var bb = Body(b);
            var bc = Body(c);
            bool match;
            if (kind == ExhaustionCandleKind.ThreeStarsInSouth)
                match =
                    a.Close < a.Open
                    && b.Close < b.Open
                    && c.Close < c.Open
                    && ba.CompareTo(Mean(i - 2, longPeriod, ExhaustionMeasure.Body)) > 0
                    && (R(a.Close) - R(a.Low)).CompareTo(ba) > 0
                    && bb.CompareTo(ba) < 0
                    && b.Open > a.Close
                    && b.Open <= a.High
                    && b.Low < a.Close
                    && b.Low >= a.Low
                    && (R(b.Close) - R(b.Low)).CompareTo(
                        Mean(i - 1, shadowPeriod, ExhaustionMeasure.Range)
                            / new ReferenceFraction(10)
                    ) > 0
                    && bc.CompareTo(Mean(i, shortPeriod, ExhaustionMeasure.Body)) < 0
                    && (R(c.Close) - R(c.Low)).CompareTo(
                        Mean(i, shadowPeriod, ExhaustionMeasure.Range) / new ReferenceFraction(10)
                    ) < 0
                    && (R(c.High) - R(c.Open)).CompareTo(
                        Mean(i, shadowPeriod, ExhaustionMeasure.Range) / new ReferenceFraction(10)
                    ) < 0
                    && c.Low > b.Low
                    && c.High < b.High;
            else
            {
                if (
                    a.Close < a.Open
                    || b.Close < b.Open
                    || c.Close < c.Open
                    || c.Close <= b.Close
                    || b.Close <= a.Close
                    || ba.CompareTo(Mean(i - 2, longPeriod, ExhaustionMeasure.Body)) <= 0
                    || b.Open <= a.Open
                    || R(b.Open).CompareTo(R(a.Close) + Near(i - 2)) > 0
                )
                    continue;
                if (kind == ExhaustionCandleKind.Stalled)
                    match =
                        bb.CompareTo(Mean(i - 1, longPeriod, ExhaustionMeasure.Body)) > 0
                        && (R(b.High) - R(b.Close)).CompareTo(
                            Mean(i - 1, shadowPeriod, ExhaustionMeasure.Range)
                                / new ReferenceFraction(10)
                        ) < 0
                        && bc.CompareTo(Mean(i, shortPeriod, ExhaustionMeasure.Body)) < 0
                        && R(c.Open).CompareTo(R(b.Close) - bc - Near(i - 1)) >= 0;
                else
                    match =
                        c.Open > b.Open
                        && R(c.Open).CompareTo(R(b.Close) + Near(i - 1)) <= 0
                        && (R(a.High) - R(a.Close)).CompareTo(ShortShadow(i - 2)) < 0
                        && (
                            bb.CompareTo(ba - Far(i - 2)) < 0 && bc.CompareTo(bb + Near(i - 1)) < 0
                            || bc.CompareTo(bb - Far(i - 1)) < 0
                            || bc.CompareTo(bb) < 0
                                && bb.CompareTo(ba) < 0
                                && (
                                    (R(c.High) - R(c.Close)).CompareTo(ShortShadow(i)) > 0
                                    || (R(b.High) - R(b.Close)).CompareTo(ShortShadow(i - 1)) > 0
                                )
                            || bc.CompareTo(bb) < 0 && (R(c.High) - R(c.Close)).CompareTo(bc) > 0
                        );
            }
            if (match)
                result[i] = kind == ExhaustionCandleKind.ThreeStarsInSouth ? 100 : -100;
        }
        return result;
        ReferenceFraction Mean(int end, int period, ExhaustionMeasure measure)
        {
            var sum = new ReferenceFraction(0);
            for (var j = end - period; j < end; j++)
            {
                var range = R(bars[j].High) - R(bars[j].Low);
                sum +=
                    measure == ExhaustionMeasure.Body ? Body(bars[j])
                    : measure == ExhaustionMeasure.Range ? range
                    : range - Body(bars[j]);
            }
            return sum / new ReferenceFraction(period);
        }
        ReferenceFraction Near(int end) =>
            Mean(end, nearPeriod, ExhaustionMeasure.Range) / new ReferenceFraction(5);
        ReferenceFraction Far(int end) =>
            Mean(end, farPeriod, ExhaustionMeasure.Range)
            * new ReferenceFraction(3)
            / new ReferenceFraction(5);
        ReferenceFraction ShortShadow(int end) =>
            Mean(end, shadowPeriod, ExhaustionMeasure.Shadows) / new ReferenceFraction(2);
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Close) - R(b.Open)).Abs();
}
