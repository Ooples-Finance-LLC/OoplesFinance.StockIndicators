using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes two opposite long bodies closing within an inclusive prior-range tolerance. Returns the second candle direction as +100 or -100, otherwise zero.</summary>
/// <remarks>Equal tolerance is one twentieth of the preceding mean range. Near tolerance is one fifth; very-short shadows use one tenth. Each window excludes the candle it classifies.</remarks>
public sealed class CounterattackCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 1.</summary>
    public CounterattackCandle(int bodyPeriod = 10, int equalPeriod = 5)
    {
        if (bodyPeriod < 1 || bodyPeriod > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        BodyPeriod = bodyPeriod;
        if (equalPeriod < 1 || equalPeriod > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        EqualPeriod = equalPeriod;
    }

    /// <summary>Number of preceding observations for the body threshold.</summary>
    public int BodyPeriod { get; }

    /// <summary>Number of preceding observations for the equal threshold.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, EqualPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new MatchedLinesState(BodyPeriod, 0, EqualPeriod, MatchedLinesKind.Counterattack);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    MatchedLinesReference.Evaluate(
                        bars,
                        BodyPeriod,
                        0,
                        EqualPeriod,
                        MatchedLinesKind.Counterattack
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes opposite bodies opening near each other with a strictly long second body and a very short opening-end shadow. Returns the second candle direction as +100 or -100, otherwise zero.</summary>
/// <remarks>Equal tolerance is one twentieth of the preceding mean range. Near tolerance is one fifth; very-short shadows use one tenth. Each window excludes the candle it classifies.</remarks>
public sealed class SeparatingLinesCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 1.</summary>
    public SeparatingLinesCandle(int bodyPeriod = 10, int shadowPeriod = 10, int equalPeriod = 5)
    {
        if (bodyPeriod < 1 || bodyPeriod > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        BodyPeriod = bodyPeriod;
        if (shadowPeriod < 1 || shadowPeriod > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        ShadowPeriod = shadowPeriod;
        if (equalPeriod < 1 || equalPeriod > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        EqualPeriod = equalPeriod;
    }

    /// <summary>Number of preceding observations for the body threshold.</summary>
    public int BodyPeriod { get; }

    /// <summary>Number of preceding observations for the shadow threshold.</summary>
    public int ShadowPeriod { get; }

    /// <summary>Number of preceding observations for the equal threshold.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Math.Max(BodyPeriod, ShadowPeriod), EqualPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new MatchedLinesState(
            BodyPeriod,
            ShadowPeriod,
            EqualPeriod,
            MatchedLinesKind.SeparatingLines
        );

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    MatchedLinesReference.Evaluate(
                        bars,
                        BodyPeriod,
                        ShadowPeriod,
                        EqualPeriod,
                        MatchedLinesKind.SeparatingLines
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes two white bodies gapping on the same side of a first body, with near-equal sizes and opens. Returns the gap direction as +100 or -100, otherwise zero.</summary>
/// <remarks>Equal tolerance is one twentieth of the preceding mean range. Near tolerance is one fifth; very-short shadows use one tenth. Each window excludes the candle it classifies.</remarks>
public sealed class GapSideBySideWhiteLinesCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods at most Int32.MaxValue minus 2.</summary>
    public GapSideBySideWhiteLinesCandle(int nearPeriod = 5, int equalPeriod = 5)
    {
        if (nearPeriod < 1 || nearPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(nearPeriod));
        NearPeriod = nearPeriod;
        if (equalPeriod < 1 || equalPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        EqualPeriod = equalPeriod;
    }

    /// <summary>Number of preceding observations for the near threshold.</summary>
    public int NearPeriod { get; }

    /// <summary>Number of preceding observations for the equal threshold.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(NearPeriod, EqualPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new MatchedLinesState(0, NearPeriod, EqualPeriod, MatchedLinesKind.GapSideBySideWhiteLines);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    MatchedLinesReference.Evaluate(
                        bars,
                        0,
                        NearPeriod,
                        EqualPeriod,
                        MatchedLinesKind.GapSideBySideWhiteLines
                    ),
                0,
                0
            ),
        ];
}

internal enum MatchedLinesKind
{
    Counterattack,
    SeparatingLines,
    GapSideBySideWhiteLines,
}

internal sealed class MatchedLinesState(
    int bodyPeriod,
    int rangePeriod,
    int equalPeriod,
    MatchedLinesKind kind
) : IIndicatorState
{
    private readonly Queue<Bar> _bodies = new(),
        _ranges = new(),
        _equals = new();
    private readonly BigInteger _equalDen = new BigInteger(equalPeriod) * 20;
    private readonly BigInteger _rangeDen =
        new BigInteger(rangePeriod) * (kind == MatchedLinesKind.GapSideBySideWhiteLines ? 5 : 10);
    private readonly int _warmup =
        Math.Max(bodyPeriod, Math.Max(rangePeriod, equalPeriod))
        + (kind == MatchedLinesKind.GapSideBySideWhiteLines ? 2 : 1);
    private ExactMeanAccumulator _body,
        _range,
        _equal,
        _lagBody,
        _lagRange,
        _lagEqual;
    private Bar _first,
        _previous;
    private int _seen;

    public void Reset()
    {
        _bodies.Clear();
        _ranges.Clear();
        _equals.Clear();
        _body = _range = _equal = _lagBody = _lagRange = _lagEqual = default;
        _first = _previous = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var value = _seen >= _warmup ? Signal(bar) : 0;
        _lagBody = _body;
        _lagRange = _range;
        _lagEqual = _equal;
        if (bodyPeriod > 0)
        {
            if (_bodies.Count == bodyPeriod)
                Body(ref _body, _bodies.Dequeue(), -1);
            _bodies.Enqueue(bar);
            Body(ref _body, bar, 1);
        }
        if (rangePeriod > 0)
        {
            if (_ranges.Count == rangePeriod)
                Range(ref _range, _ranges.Dequeue(), -1);
            _ranges.Enqueue(bar);
            Range(ref _range, bar, 1);
        }
        if (_equals.Count == equalPeriod)
            Range(ref _equal, _equals.Dequeue(), -1);
        _equals.Enqueue(bar);
        Range(ref _equal, bar, 1);
        _first = _previous;
        _previous = bar;
        if (_seen < _warmup)
            _seen++;
        return value;
    }

    private static void Body(ref ExactMeanAccumulator sum, in Bar b, int sign)
    {
        sum.Add(Math.Max(b.Open, b.Close), sign);
        sum.Add(Math.Min(b.Open, b.Close), -sign);
    }

    private static void Range(ref ExactMeanAccumulator sum, in Bar b, int sign)
    {
        sum.Add(b.High, sign);
        sum.Add(b.Low, -sign);
    }

    private bool Equal(double a, double b)
    {
        var margin = _lagEqual;
        margin.Add(Math.Max(a, b), -_equalDen);
        margin.Add(Math.Min(a, b), _equalDen);
        return margin.Sign >= 0;
    }

    private bool Long(in Bar b, ExactMeanAccumulator sum)
    {
        Body(ref sum, b, -bodyPeriod);
        return sum.Sign < 0;
    }

    private double Signal(in Bar c)
    {
        var b = _previous;
        if (kind == MatchedLinesKind.GapSideBySideWhiteLines)
        {
            if (b.Close < b.Open || c.Close < c.Open || !Equal(b.Open, c.Open))
                return 0;
            var up =
                b.Open > Math.Max(_first.Open, _first.Close)
                && c.Open > Math.Max(_first.Open, _first.Close);
            var down =
                b.Close < Math.Min(_first.Open, _first.Close)
                && c.Close < Math.Min(_first.Open, _first.Close);
            if (!up && !down)
                return 0;
            var difference = new ExactMeanAccumulator();
            difference.Add(c.Close, _rangeDen);
            difference.Add(c.Open, -_rangeDen);
            difference.Add(b.Close, -_rangeDen);
            difference.Add(b.Open, _rangeDen);
            var margin = _lagRange;
            if (difference.Sign < 0)
                margin.AddExact(difference);
            else
                margin.Subtract(difference);
            return margin.Sign >= 0
                ? up
                    ? 100
                    : -100
                : 0;
        }
        if ((b.Close < b.Open) == (c.Close < c.Open) || !Long(c, _body))
            return 0;
        if (kind == MatchedLinesKind.Counterattack)
        {
            if (!Long(b, _lagBody) || !Equal(b.Close, c.Close))
                return 0;
        }
        else
        {
            if (!Equal(b.Open, c.Open))
                return 0;
            var shadow = _range;
            if (c.Close >= c.Open)
            {
                shadow.Add(c.Open, -_rangeDen);
                shadow.Add(c.Low, _rangeDen);
            }
            else
            {
                shadow.Add(c.High, -_rangeDen);
                shadow.Add(c.Open, _rangeDen);
            }
            if (shadow.Sign <= 0)
                return 0;
        }
        return c.Close >= c.Open ? 100 : -100;
    }
}

internal static class MatchedLinesReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int bodyPeriod,
        int rangePeriod,
        int equalPeriod,
        MatchedLinesKind kind
    )
    {
        var values = new double[bars.Count];
        var gap = kind == MatchedLinesKind.GapSideBySideWhiteLines;
        var warmup = Math.Max(bodyPeriod, Math.Max(rangePeriod, equalPeriod)) + (gap ? 2 : 1);
        for (var i = warmup; i < bars.Count; i++)
        {
            var b = bars[i - 1];
            var c = bars[i];
            var tolerance = Mean(i - 1, equalPeriod, false) / new ReferenceFraction(20);
            if (gap)
            {
                var a = bars[i - 2];
                if (b.Close < b.Open || c.Close < c.Open)
                    continue;
                var up =
                    Math.Min(b.Open, b.Close) > Math.Max(a.Open, a.Close)
                    && Math.Min(c.Open, c.Close) > Math.Max(a.Open, a.Close);
                var down =
                    Math.Max(b.Open, b.Close) < Math.Min(a.Open, a.Close)
                    && Math.Max(c.Open, c.Close) < Math.Min(a.Open, a.Close);
                if (
                    (up || down)
                    && (R(c.Open) - R(b.Open)).Abs().CompareTo(tolerance) <= 0
                    && (Body(c) - Body(b))
                        .Abs()
                        .CompareTo(Mean(i - 1, rangePeriod, false) / new ReferenceFraction(5)) <= 0
                )
                    values[i] = up ? 100 : -100;
            }
            else
            {
                if (
                    (b.Close < b.Open) == (c.Close < c.Open)
                    || Body(c).CompareTo(Mean(i, bodyPeriod, true)) <= 0
                )
                    continue;
                if (kind == MatchedLinesKind.Counterattack)
                {
                    if (
                        Body(b).CompareTo(Mean(i - 1, bodyPeriod, true)) <= 0
                        || (R(c.Close) - R(b.Close)).Abs().CompareTo(tolerance) > 0
                    )
                        continue;
                }
                else
                {
                    if ((R(c.Open) - R(b.Open)).Abs().CompareTo(tolerance) > 0)
                        continue;
                    var shadow = c.Close >= c.Open ? R(c.Open) - R(c.Low) : R(c.High) - R(c.Open);
                    if (
                        shadow.CompareTo(Mean(i, rangePeriod, false) / new ReferenceFraction(10))
                        >= 0
                    )
                        continue;
                }
                values[i] = c.Close >= c.Open ? 100 : -100;
            }
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
