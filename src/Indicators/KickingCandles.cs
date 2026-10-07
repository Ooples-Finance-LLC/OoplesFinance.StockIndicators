using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes opposite long candles with very short shadows and a strict full-range gap. Returns the direction of the second candle as +100 or -100, otherwise zero.</summary>
public sealed class KickingCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive body and range periods below Int32.MaxValue.</summary>
    public KickingCandle(int bodyPeriod = 10, int shadowPeriod = 10)
    {
        if (bodyPeriod < 1 || bodyPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        if (shadowPeriod < 1 || shadowPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        BodyPeriod = bodyPeriod;
        ShadowPeriod = shadowPeriod;
    }

    /// <summary>Prior bodies defining strict longness.</summary>
    public int BodyPeriod { get; }

    /// <summary>Prior ranges whose mean divided by ten defines strict shadow limits.</summary>
    public int ShadowPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, ShadowPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new KickingCandleState(BodyPeriod, ShadowPeriod, false);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars => KickingCandleReference.Evaluate(bars, BodyPeriod, ShadowPeriod, false),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes opposite long candles with very short shadows and a strict full-range gap. Returns the direction of the longer body, choosing the first on an exact tie as +100 or -100, otherwise zero.</summary>
public sealed class KickingByLengthCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive body and range periods below Int32.MaxValue.</summary>
    public KickingByLengthCandle(int bodyPeriod = 10, int shadowPeriod = 10)
    {
        if (bodyPeriod < 1 || bodyPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        if (shadowPeriod < 1 || shadowPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(shadowPeriod));
        BodyPeriod = bodyPeriod;
        ShadowPeriod = shadowPeriod;
    }

    /// <summary>Prior bodies defining strict longness.</summary>
    public int BodyPeriod { get; }

    /// <summary>Prior ranges whose mean divided by ten defines strict shadow limits.</summary>
    public int ShadowPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, ShadowPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new KickingCandleState(BodyPeriod, ShadowPeriod, true);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars => KickingCandleReference.Evaluate(bars, BodyPeriod, ShadowPeriod, true),
                0,
                0
            ),
        ];
}

internal sealed class KickingCandleState(int bodyPeriod, int shadowPeriod, bool byLength)
    : IIndicatorState
{
    private readonly Queue<Bar> _bodies = new(),
        _ranges = new();
    private readonly BigInteger _shadowDenominator = new BigInteger(shadowPeriod) * 10;
    private readonly int _warmup = Math.Max(bodyPeriod, shadowPeriod) + 1;
    private ExactMeanAccumulator _body,
        _range,
        _priorBody,
        _priorRange;
    private Bar _previous;
    private int _seen;

    public void Reset()
    {
        _bodies.Clear();
        _ranges.Clear();
        _body = _range = _priorBody = _priorRange = default;
        _previous = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var result = 0d;
        if (
            _seen >= _warmup
            && (_previous.Close < _previous.Open) != (bar.Close < bar.Open)
            && (
                _previous.Close < _previous.Open
                    ? bar.Low > _previous.High
                    : bar.High < _previous.Low
            )
            && Qualified(_previous, _priorBody, _priorRange)
            && Qualified(bar, _body, _range)
        )
        {
            var selected = bar;
            if (byLength)
            {
                var difference = new ExactMeanAccumulator();
                AddBody(ref difference, bar, 1);
                AddBody(ref difference, _previous, -1);
                if (difference.Sign <= 0)
                    selected = _previous;
            }
            result = selected.Close >= selected.Open ? 100 : -100;
        }
        _priorBody = _body;
        _priorRange = _range;
        if (_bodies.Count == bodyPeriod)
            AddBody(ref _body, _bodies.Dequeue(), -1);
        _bodies.Enqueue(bar);
        AddBody(ref _body, bar, 1);
        if (_ranges.Count == shadowPeriod)
            AddRange(_ranges.Dequeue(), -1);
        _ranges.Enqueue(bar);
        AddRange(bar, 1);
        _previous = bar;
        if (_seen < _warmup)
            _seen++;
        return result;
    }

    private static void AddBody(ref ExactMeanAccumulator sum, in Bar bar, int sign)
    {
        sum.Add(Math.Max(bar.Open, bar.Close), sign);
        sum.Add(Math.Min(bar.Open, bar.Close), -sign);
    }

    private void AddRange(in Bar bar, int sign)
    {
        _range.Add(bar.High, sign);
        _range.Add(bar.Low, -sign);
    }

    private bool Qualified(in Bar bar, ExactMeanAccumulator body, ExactMeanAccumulator range)
    {
        AddBody(ref body, bar, -bodyPeriod);
        if (body.Sign >= 0)
            return false;
        var top = Math.Max(bar.Open, bar.Close);
        var bottom = Math.Min(bar.Open, bar.Close);
        var upper = range;
        upper.Add(bar.High, -_shadowDenominator);
        upper.Add(top, _shadowDenominator);
        var lower = range;
        lower.Add(bottom, -_shadowDenominator);
        lower.Add(bar.Low, _shadowDenominator);
        return upper.Sign > 0 && lower.Sign > 0;
    }
}

internal static class KickingCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int bodyPeriod,
        int shadowPeriod,
        bool byLength
    )
    {
        var values = new double[bars.Count];
        for (var i = Math.Max(bodyPeriod, shadowPeriod) + 1; i < bars.Count; i++)
        {
            var p = bars[i - 1];
            var c = bars[i];
            if (
                (p.Close < p.Open) == (c.Close < c.Open)
                || !(p.Close < p.Open ? c.Low > p.High : c.High < p.Low)
            )
                continue;
            if (!Qualified(i - 1) || !Qualified(i))
                continue;
            var selected = byLength && Body(c).CompareTo(Body(p)) <= 0 ? p : c;
            values[i] = selected.Close >= selected.Open ? 100 : -100;
        }
        return values;
        bool Qualified(int i)
        {
            var body = new ReferenceFraction(0);
            var range = new ReferenceFraction(0);
            for (var j = i - bodyPeriod; j < i; j++)
                body += Body(bars[j]);
            for (var j = i - shadowPeriod; j < i; j++)
                range += R(bars[j].High) - R(bars[j].Low);
            var b = bars[i];
            var threshold =
                range / (new ReferenceFraction(shadowPeriod) * new ReferenceFraction(10));
            return Body(b).CompareTo(body / new ReferenceFraction(bodyPeriod)) > 0
                && (R(b.High) - R(Math.Max(b.Open, b.Close))).CompareTo(threshold) < 0
                && (R(Math.Min(b.Open, b.Close)) - R(b.Low)).CompareTo(threshold) < 0;
        }
    }

    private static ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);

    private static ReferenceFraction Body(Bar b) => (R(b.Open) - R(b.Close)).Abs();
}
