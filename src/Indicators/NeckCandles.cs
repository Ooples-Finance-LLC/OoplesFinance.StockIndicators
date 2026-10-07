using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns -100 when a white candle gaps below the prior long black candle and closes near the prior low within an inclusive range-based tolerance; otherwise zero.</summary>
/// <remarks>Both historical windows exclude the prior pattern candle. Tolerance is one twentieth of the mean range. White includes an unchanged open/close.</remarks>
public sealed class OnNeckCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern with positive body and tolerance periods below Int32.MaxValue.</summary>
    public OnNeckCandle(int bodyPeriod = 10, int equalPeriod = 5)
    {
        if (bodyPeriod < 1 || bodyPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        if (equalPeriod < 1 || equalPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        BodyPeriod = bodyPeriod;
        EqualPeriod = equalPeriod;
    }

    /// <summary>Prior bodies defining the strictly long anchor body.</summary>
    public int BodyPeriod { get; }

    /// <summary>Prior ranges defining the close tolerance.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, EqualPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new NeckCandleState(BodyPeriod, EqualPeriod, NeckCandleKind.OnNeck);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 0),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    NeckCandleReference.Evaluate(
                        bars,
                        BodyPeriod,
                        EqualPeriod,
                        NeckCandleKind.OnNeck
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Returns -100 when a white candle gaps below the prior long black candle and closes between the prior close and that close plus an inclusive range-based tolerance; otherwise zero.</summary>
/// <remarks>Both historical windows exclude the prior pattern candle. Tolerance is one twentieth of the mean range. White includes an unchanged open/close.</remarks>
public sealed class InNeckCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern with positive body and tolerance periods below Int32.MaxValue.</summary>
    public InNeckCandle(int bodyPeriod = 10, int equalPeriod = 5)
    {
        if (bodyPeriod < 1 || bodyPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        if (equalPeriod < 1 || equalPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        BodyPeriod = bodyPeriod;
        EqualPeriod = equalPeriod;
    }

    /// <summary>Prior bodies defining the strictly long anchor body.</summary>
    public int BodyPeriod { get; }

    /// <summary>Prior ranges defining the close tolerance.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, EqualPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new NeckCandleState(BodyPeriod, EqualPeriod, NeckCandleKind.InNeck);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 0),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    NeckCandleReference.Evaluate(
                        bars,
                        BodyPeriod,
                        EqualPeriod,
                        NeckCandleKind.InNeck
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Returns -100 when a white candle gaps below the prior long black candle and closes strictly above the prior close plus a range-based tolerance and at or below the prior body midpoint; otherwise zero.</summary>
/// <remarks>Both historical windows exclude the prior pattern candle. Tolerance is one twentieth of the mean range. White includes an unchanged open/close.</remarks>
public sealed class ThrustingCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a pattern with positive body and tolerance periods below Int32.MaxValue.</summary>
    public ThrustingCandle(int bodyPeriod = 10, int equalPeriod = 5)
    {
        if (bodyPeriod < 1 || bodyPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bodyPeriod));
        if (equalPeriod < 1 || equalPeriod == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(equalPeriod));
        BodyPeriod = bodyPeriod;
        EqualPeriod = equalPeriod;
    }

    /// <summary>Prior bodies defining the strictly long anchor body.</summary>
    public int BodyPeriod { get; }

    /// <summary>Prior ranges defining the close tolerance.</summary>
    public int EqualPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(BodyPeriod, EqualPeriod) + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new NeckCandleState(BodyPeriod, EqualPeriod, NeckCandleKind.Thrusting);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 0),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    NeckCandleReference.Evaluate(
                        bars,
                        BodyPeriod,
                        EqualPeriod,
                        NeckCandleKind.Thrusting
                    ),
                0,
                0
            ),
        ];
}

internal enum NeckCandleKind
{
    OnNeck,
    InNeck,
    Thrusting,
}

internal sealed class NeckCandleState(int bodyPeriod, int equalPeriod, NeckCandleKind kind)
    : IIndicatorState
{
    private readonly Queue<Bar> _bodies = new(),
        _ranges = new();
    private readonly BigInteger _denominator = new BigInteger(equalPeriod) * 20;
    private readonly int _warmup = Math.Max(bodyPeriod, equalPeriod) + 1;
    private ExactMeanAccumulator _body,
        _range,
        _lagBody,
        _lagRange;
    private Bar _previous;
    private int _seen;

    public void Reset()
    {
        _bodies.Clear();
        _ranges.Clear();
        _body = _range = _lagBody = _lagRange = default;
        _previous = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var signal = _seen >= _warmup && Matches(bar) ? -100d : 0;
        _lagBody = _body;
        _lagRange = _range;
        if (_bodies.Count == bodyPeriod)
            AddBody(_bodies.Dequeue(), -1);
        _bodies.Enqueue(bar);
        AddBody(bar, 1);
        if (_ranges.Count == equalPeriod)
            AddRange(_ranges.Dequeue(), -1);
        _ranges.Enqueue(bar);
        AddRange(bar, 1);
        _previous = bar;
        if (_seen < _warmup)
            _seen++;
        return signal;
    }

    private void AddBody(in Bar bar, int sign)
    {
        _body.Add(Math.Max(bar.Open, bar.Close), sign);
        _body.Add(Math.Min(bar.Open, bar.Close), -sign);
    }

    private void AddRange(in Bar bar, int sign)
    {
        _range.Add(bar.High, sign);
        _range.Add(bar.Low, -sign);
    }

    private bool Matches(in Bar bar)
    {
        if (_previous.Close >= _previous.Open || bar.Close < bar.Open || bar.Open >= _previous.Low)
            return false;
        var longMargin = _lagBody;
        longMargin.Add(_previous.Open, -bodyPeriod);
        longMargin.Add(_previous.Close, bodyPeriod);
        if (longMargin.Sign >= 0)
            return false;
        var center = kind == NeckCandleKind.OnNeck ? _previous.Low : _previous.Close;
        if (kind != NeckCandleKind.OnNeck && bar.Close < center)
            return false;
        var equalMargin = _lagRange;
        equalMargin.Add(Math.Max(bar.Close, center), -_denominator);
        equalMargin.Add(Math.Min(bar.Close, center), _denominator);
        if (kind != NeckCandleKind.Thrusting)
            return equalMargin.Sign >= 0;
        if (equalMargin.Sign >= 0)
            return false;
        var midpoint = new ExactMeanAccumulator();
        midpoint.Add(_previous.Open);
        midpoint.Add(_previous.Close);
        midpoint.Add(bar.Close, -2);
        return midpoint.Sign >= 0;
    }
}

internal static class NeckCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int bodyPeriod,
        int equalPeriod,
        NeckCandleKind kind
    )
    {
        var result = new double[bars.Count];
        for (var i = Math.Max(bodyPeriod, equalPeriod) + 1; i < bars.Count; i++)
        {
            var p = bars[i - 1];
            var c = bars[i];
            if (p.Close >= p.Open || c.Close < c.Open || c.Open >= p.Low)
                continue;
            var body = new ReferenceFraction(0);
            var range = new ReferenceFraction(0);
            for (var j = i - bodyPeriod - 1; j < i - 1; j++)
                body += (R(bars[j].Close) - R(bars[j].Open)).Abs();
            if ((R(p.Open) - R(p.Close)).CompareTo(body / new ReferenceFraction(bodyPeriod)) <= 0)
                continue;
            for (var j = i - equalPeriod - 1; j < i - 1; j++)
                range += R(bars[j].High) - R(bars[j].Low);
            var tolerance =
                range / (new ReferenceFraction(equalPeriod) * new ReferenceFraction(20));
            var match = kind switch
            {
                NeckCandleKind.OnNeck => (R(c.Close) - R(p.Low)).Abs().CompareTo(tolerance) <= 0,
                NeckCandleKind.InNeck => c.Close >= p.Close
                    && R(c.Close).CompareTo(R(p.Close) + tolerance) <= 0,
                _ => R(c.Close).CompareTo(R(p.Close) + tolerance) > 0
                    && R(c.Close).CompareTo((R(p.Open) + R(p.Close)) / new ReferenceFraction(2))
                        <= 0,
            };
            if (match)
                result[i] = -100;
        }
        return result;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
}
