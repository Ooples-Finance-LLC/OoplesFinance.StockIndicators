using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes three black candles with declining closes and very short lower shadows following a white candle; later opens lie strictly inside the preceding bodies. Returns -100 when matched, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the candle they classify. White includes unchanged open/close.</remarks>
public sealed class ThreeBlackCrowsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 3.</summary>
    public ThreeBlackCrowsCandle(int shadowPeriod = 10)
    {
        if (shadowPeriod < 1 || shadowPeriod > int.MaxValue - 3)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        ShadowPeriod = shadowPeriod;
    }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => ShadowPeriod + 3;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new CrowSoldierState(ShadowPeriod, 0, 0, 0, CrowSoldierKind.Black);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    CrowSoldierReference.Evaluate(
                        bars,
                        ShadowPeriod,
                        0,
                        0,
                        0,
                        CrowSoldierKind.Black
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes three black candles with declining closes, very short lower shadows, and later opens within inclusive tolerance of the preceding closes. Returns -100 when matched, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the candle they classify. White includes unchanged open/close.</remarks>
public sealed class IdenticalThreeCrowsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2.</summary>
    public IdenticalThreeCrowsCandle(int shadowPeriod = 10, int equalPeriod = 5)
    {
        if (shadowPeriod < 1 || shadowPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        ShadowPeriod = shadowPeriod;
        if (equalPeriod < 1 || equalPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        EqualPeriod = equalPeriod;
    }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <summary>Prior observations used for the equal threshold.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(ShadowPeriod, EqualPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new CrowSoldierState(ShadowPeriod, EqualPeriod, 0, 0, CrowSoldierKind.Identical);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    CrowSoldierReference.Evaluate(
                        bars,
                        ShadowPeriod,
                        EqualPeriod,
                        0,
                        0,
                        CrowSoldierKind.Identical
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes three white candles with increasing closes, very short upper shadows, near prior-body opens, limited body shrinkage, and a final body above its prior mean. Returns 100 when matched, otherwise zero.</summary>
/// <remarks>Threshold windows exclude the candle they classify. White includes unchanged open/close.</remarks>
public sealed class ThreeWhiteSoldiersCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2.</summary>
    public ThreeWhiteSoldiersCandle(
        int shadowPeriod = 10,
        int nearPeriod = 5,
        int farPeriod = 5,
        int bodyPeriod = 10
    )
    {
        if (shadowPeriod < 1 || shadowPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        ShadowPeriod = shadowPeriod;
        if (nearPeriod < 1 || nearPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(nearPeriod));
        NearPeriod = nearPeriod;
        if (farPeriod < 1 || farPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(farPeriod));
        FarPeriod = farPeriod;
        if (bodyPeriod < 1 || bodyPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        BodyPeriod = bodyPeriod;
    }

    /// <summary>Prior observations used for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <summary>Prior observations used for the near threshold.</summary>
    public int NearPeriod { get; }

    /// <summary>Prior observations used for the far threshold.</summary>
    public int FarPeriod { get; }

    /// <summary>Prior observations used for the body threshold.</summary>
    public int BodyPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars =>
        Math.Max(Math.Max(ShadowPeriod, NearPeriod), Math.Max(FarPeriod, BodyPeriod)) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new CrowSoldierState(
            ShadowPeriod,
            NearPeriod,
            FarPeriod,
            BodyPeriod,
            CrowSoldierKind.White
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    CrowSoldierReference.Evaluate(
                        bars,
                        ShadowPeriod,
                        NearPeriod,
                        FarPeriod,
                        BodyPeriod,
                        CrowSoldierKind.White
                    ),
                0,
                0
            ),
        ];
}

internal enum CrowSoldierKind
{
    Black,
    Identical,
    White,
}

internal sealed class CrowSoldierState(
    int shadowPeriod,
    int nearPeriod,
    int farPeriod,
    int bodyPeriod,
    CrowSoldierKind kind
) : IIndicatorState
{
    private readonly Queue<Bar> _shadows = new(),
        _nears = new(),
        _fars = new(),
        _bodies = new();
    private readonly BigInteger _shadowDen = new BigInteger(shadowPeriod) * 10,
        _nearDen = new BigInteger(nearPeriod) * (kind == CrowSoldierKind.Identical ? 20 : 5),
        _farDen = new BigInteger(farPeriod) * 5;
    private readonly int _warmup =
        Math.Max(Math.Max(shadowPeriod, nearPeriod), Math.Max(farPeriod, bodyPeriod))
        + (kind == CrowSoldierKind.Black ? 3 : 2);
    private ExactMeanAccumulator _s,
        _s1,
        _s2,
        _n,
        _n1,
        _n2,
        _f,
        _f1,
        _f2,
        _body;
    private Bar _before,
        _first,
        _second;
    private int _seen;

    public void Reset()
    {
        _shadows.Clear();
        _nears.Clear();
        _fars.Clear();
        _bodies.Clear();
        _s = _s1 = _s2 = _n = _n1 = _n2 = _f = _f1 = _f2 = _body = default;
        _before = _first = _second = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var result =
            _seen >= _warmup && Matches(bar)
                ? kind == CrowSoldierKind.White
                    ? 100d
                    : -100
                : 0;
        _s2 = _s1;
        _s1 = _s;
        _n2 = _n1;
        _n1 = _n;
        _f2 = _f1;
        _f1 = _f;
        Accumulate(_shadows, ref _s, bar, shadowPeriod, false);
        if (nearPeriod > 0)
            Accumulate(_nears, ref _n, bar, nearPeriod, false);
        if (farPeriod > 0)
            Accumulate(_fars, ref _f, bar, farPeriod, false);
        if (bodyPeriod > 0)
            Accumulate(_bodies, ref _body, bar, bodyPeriod, true);
        _before = _first;
        _first = _second;
        _second = bar;
        if (_seen < _warmup)
            _seen++;
        return result;
    }

    private static void Accumulate(
        Queue<Bar> history,
        ref ExactMeanAccumulator sum,
        in Bar bar,
        int period,
        bool body
    )
    {
        if (history.Count == period)
            Add(ref sum, history.Dequeue(), body, -1);
        history.Enqueue(bar);
        Add(ref sum, bar, body, 1);
    }

    private static void Add(ref ExactMeanAccumulator sum, in Bar bar, bool body, int sign)
    {
        sum.Add(body ? Math.Max(bar.Open, bar.Close) : bar.High, sign);
        sum.Add(body ? Math.Min(bar.Open, bar.Close) : bar.Low, -sign);
    }

    private bool Shadow(in Bar b, ExactMeanAccumulator sum)
    {
        if (kind == CrowSoldierKind.White)
        {
            sum.Add(b.High, -_shadowDen);
            sum.Add(b.Close, _shadowDen);
        }
        else
        {
            sum.Add(b.Close, -_shadowDen);
            sum.Add(b.Low, _shadowDen);
        }
        return sum.Sign > 0;
    }

    private bool Equal(double open, double close, ExactMeanAccumulator sum)
    {
        sum.Add(Math.Max(open, close), -_nearDen);
        sum.Add(Math.Min(open, close), _nearDen);
        return sum.Sign >= 0;
    }

    private bool Near(double open, double close, ExactMeanAccumulator sum)
    {
        sum.Add(close, _nearDen);
        sum.Add(open, -_nearDen);
        return sum.Sign >= 0;
    }

    private bool NotFarShorter(in Bar current, in Bar prior, ExactMeanAccumulator sum)
    {
        var margin = sum;
        margin.AddExact(sum);
        margin.AddExact(sum);
        margin.Add(current.Close, _farDen);
        margin.Add(current.Open, -_farDen);
        margin.Add(prior.Close, -_farDen);
        margin.Add(prior.Open, _farDen);
        return margin.Sign > 0;
    }

    private bool Matches(in Bar c)
    {
        var a = _first;
        var b = _second;
        var white = kind == CrowSoldierKind.White;
        if (
            white
                ? a.Close < a.Open
                    || b.Close < b.Open
                    || c.Close < c.Open
                    || b.Close <= a.Close
                    || c.Close <= b.Close
                : a.Close >= a.Open
                    || b.Close >= b.Open
                    || c.Close >= c.Open
                    || b.Close >= a.Close
                    || c.Close >= b.Close
        )
            return false;
        if (!Shadow(a, _s2) || !Shadow(b, _s1) || !Shadow(c, _s))
            return false;
        if (kind == CrowSoldierKind.Black)
            return _before.Close >= _before.Open
                && _before.High > a.Close
                && b.Open < a.Open
                && b.Open > a.Close
                && c.Open < b.Open
                && c.Open > b.Close;
        if (kind == CrowSoldierKind.Identical)
            return Equal(b.Open, a.Close, _n2) && Equal(c.Open, b.Close, _n1);
        var shortMargin = _body;
        Add(ref shortMargin, c, true, -bodyPeriod);
        return b.Open > a.Open
            && c.Open > b.Open
            && Near(b.Open, a.Close, _n2)
            && Near(c.Open, b.Close, _n1)
            && NotFarShorter(b, a, _f2)
            && NotFarShorter(c, b, _f1)
            && shortMargin.Sign < 0;
    }
}

internal static class CrowSoldierReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int shadowPeriod,
        int nearPeriod,
        int farPeriod,
        int bodyPeriod,
        CrowSoldierKind kind
    )
    {
        var values = new double[bars.Count];
        var white = kind == CrowSoldierKind.White;
        var warmup =
            Math.Max(Math.Max(shadowPeriod, nearPeriod), Math.Max(farPeriod, bodyPeriod))
            + (kind == CrowSoldierKind.Black ? 3 : 2);
        for (var i = warmup; i < bars.Count; i++)
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            if (
                white
                    ? a.Close < a.Open
                        || b.Close < b.Open
                        || c.Close < c.Open
                        || b.Close <= a.Close
                        || c.Close <= b.Close
                    : a.Close >= a.Open
                        || b.Close >= b.Open
                        || c.Close >= c.Open
                        || b.Close >= a.Close
                        || c.Close >= b.Close
            )
                continue;
            var shadows = true;
            for (var j = i - 2; j <= i; j++)
            {
                var shadow = white
                    ? R(bars[j].High) - R(bars[j].Close)
                    : R(bars[j].Close) - R(bars[j].Low);
                shadows &=
                    shadow.CompareTo(Mean(j, shadowPeriod, false) / new ReferenceFraction(10)) < 0;
            }
            if (!shadows)
                continue;
            bool match;
            if (kind == CrowSoldierKind.Black)
            {
                var p = bars[i - 3];
                match =
                    p.Close >= p.Open
                    && p.High > a.Close
                    && b.Open > a.Close
                    && b.Open < a.Open
                    && c.Open > b.Close
                    && c.Open < b.Open;
            }
            else if (kind == CrowSoldierKind.Identical)
                match =
                    (R(b.Open) - R(a.Close))
                        .Abs()
                        .CompareTo(Mean(i - 2, nearPeriod, false) / new ReferenceFraction(20)) <= 0
                    && (R(c.Open) - R(b.Close))
                        .Abs()
                        .CompareTo(Mean(i - 1, nearPeriod, false) / new ReferenceFraction(20)) <= 0;
            else
                match =
                    b.Open > a.Open
                    && c.Open > b.Open
                    && R(b.Open)
                        .CompareTo(
                            R(a.Close) + Mean(i - 2, nearPeriod, false) / new ReferenceFraction(5)
                        ) <= 0
                    && R(c.Open)
                        .CompareTo(
                            R(b.Close) + Mean(i - 1, nearPeriod, false) / new ReferenceFraction(5)
                        ) <= 0
                    && Body(b)
                        .CompareTo(
                            Body(a)
                                - Mean(i - 2, farPeriod, false)
                                    * new ReferenceFraction(3)
                                    / new ReferenceFraction(5)
                        ) > 0
                    && Body(c)
                        .CompareTo(
                            Body(b)
                                - Mean(i - 1, farPeriod, false)
                                    * new ReferenceFraction(3)
                                    / new ReferenceFraction(5)
                        ) > 0
                    && Body(c).CompareTo(Mean(i, bodyPeriod, true)) > 0;
            if (match)
                values[i] = white ? 100 : -100;
        }
        return values;
        ReferenceFraction Mean(int end, int period, bool body)
        {
            var sum = new ReferenceFraction(0);
            for (var j = end - period; j < end; j++)
                sum += body ? Body(bars[j]) : R(bars[j].High) - R(bars[j].Low);
            return sum / new ReferenceFraction(period);
        }
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Close) - R(b.Open)).Abs();
}
