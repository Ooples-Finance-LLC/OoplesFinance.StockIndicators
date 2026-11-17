using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a short body strictly inside a long body followed by an opposite-color close beyond the first open; returns the reversal direction as +100 or -100, otherwise zero.</summary>
/// <remarks>Body thresholds exclude the candle being classified. White includes an unchanged open/close.</remarks>
public sealed class ThreeInsideCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive prior-body periods at most Int32.MaxValue minus two.</summary>
    public ThreeInsideCandle(int longPeriod = 10, int shortPeriod = 10)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        LongPeriod = longPeriod;
    }

    /// <summary>Prior bodies defining the strictly long first candle.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior bodies defining the short candle.</summary>
    public int ShortPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TripleBodyCandleState(LongPeriod, ShortPeriod, TripleBodyCandleKind.ThreeInside);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TripleBodyCandleReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        TripleBodyCandleKind.ThreeInside
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a long white body, a black body gapping above it, then a black candle opening inside the second body and closing inside the first; returns -100, otherwise zero.</summary>
/// <remarks>Body thresholds exclude the candle being classified. White includes an unchanged open/close.</remarks>
public sealed class TwoCrowsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive prior-body periods at most Int32.MaxValue minus two.</summary>
    public TwoCrowsCandle(int longPeriod = 10)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        LongPeriod = longPeriod;
    }

    /// <summary>Prior bodies defining the strictly long first candle.</summary>
    public int LongPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => LongPeriod + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TripleBodyCandleState(LongPeriod, LongPeriod, TripleBodyCandleKind.TwoCrows);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TripleBodyCandleReference.Evaluate(
                        bars,
                        LongPeriod,
                        LongPeriod,
                        TripleBodyCandleKind.TwoCrows
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a long white body, a short black body gapping above it, then a black body engulfing the second while remaining above the first close; returns -100, otherwise zero.</summary>
/// <remarks>Body thresholds exclude the candle being classified. White includes an unchanged open/close.</remarks>
public sealed class UpsideGapTwoCrowsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive prior-body periods at most Int32.MaxValue minus two.</summary>
    public UpsideGapTwoCrowsCandle(int longPeriod = 10, int shortPeriod = 10)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        LongPeriod = longPeriod;
    }

    /// <summary>Prior bodies defining the strictly long first candle.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior bodies defining the short candle.</summary>
    public int ShortPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TripleBodyCandleState(LongPeriod, ShortPeriod, TripleBodyCandleKind.UpsideGapTwoCrows);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TripleBodyCandleReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        TripleBodyCandleKind.UpsideGapTwoCrows
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a long black body, a contained black body making a lower low, then a short white candle opening above the second low; returns +100, otherwise zero.</summary>
/// <remarks>Body thresholds exclude the candle being classified. White includes an unchanged open/close.</remarks>
public sealed class UniqueThreeRiverCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive prior-body periods at most Int32.MaxValue minus two.</summary>
    public UniqueThreeRiverCandle(int longPeriod = 10, int shortPeriod = 10)
    {
        if (longPeriod < 1 || longPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (shortPeriod < 1 || shortPeriod > int.MaxValue - 2)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        ShortPeriod = shortPeriod;
        LongPeriod = longPeriod;
    }

    /// <summary>Prior bodies defining the strictly long first candle.</summary>
    public int LongPeriod { get; }

    /// <summary>Prior bodies defining the short candle.</summary>
    public int ShortPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(LongPeriod, ShortPeriod) + 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TripleBodyCandleState(LongPeriod, ShortPeriod, TripleBodyCandleKind.UniqueThreeRiver);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TripleBodyCandleReference.Evaluate(
                        bars,
                        LongPeriod,
                        ShortPeriod,
                        TripleBodyCandleKind.UniqueThreeRiver
                    ),
                0,
                0
            ),
        ];
}

internal enum TripleBodyCandleKind
{
    ThreeInside,
    TwoCrows,
    UpsideGapTwoCrows,
    UniqueThreeRiver,
}

internal sealed class TripleBodyCandleState(
    int longPeriod,
    int shortPeriod,
    TripleBodyCandleKind kind
) : IIndicatorState
{
    private readonly Queue<BodyFactors> _longHistory = new(),
        _shortHistory = new();
    private readonly int _warmup = Math.Max(longPeriod, shortPeriod) + 2;
    private ExactMeanAccumulator _longSum,
        _shortSum,
        _lagLong,
        _lagLong2,
        _lagShort;
    private Bar _first,
        _second;
    private BodyFactors _firstBody, _secondBody;
    private int _seen;

    private readonly struct BodyFactors
    {
        private readonly ExactMeanAccumulator.ProductFactor _top, _bottom;

        internal BodyFactors(in Bar bar)
        {
            var top = Math.Max(bar.Open, bar.Close);
            var bottom = Math.Min(bar.Open, bar.Close);
            if (double.IsNaN(top) || double.IsInfinity(top) || double.IsNaN(bottom) || double.IsInfinity(bottom))
                throw new ArithmeticException("An exact finite mean requires finite inputs.");
            _top = new(top);
            _bottom = new(bottom);
        }

        internal void AddTo(ref ExactMeanAccumulator sum, int weight)
        {
            sum.AddFactor(_top, weight);
            sum.AddFactor(_bottom, -weight);
        }
    }

    public void Reset()
    {
        _longHistory.Clear();
        _shortHistory.Clear();
        _longSum = _shortSum = _lagLong = _lagLong2 = _lagShort = default;
        _first = _second = default;
        _firstBody = _secondBody = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var signal =
            _seen >= _warmup && Matches(bar)
                ? kind == TripleBodyCandleKind.UniqueThreeRiver
                || kind == TripleBodyCandleKind.ThreeInside && _first.Close < _first.Open
                    ? 100d
                    : -100
                : 0;
        _lagLong2 = _lagLong;
        _lagLong = _longSum;
        _lagShort = _shortSum;
        if (_longHistory.Count == longPeriod)
            _longHistory.Dequeue().AddTo(ref _longSum, -1);
        // Construct only at the original first body-add point, after matching.
        var body = new BodyFactors(bar);
        _longHistory.Enqueue(body);
        body.AddTo(ref _longSum, 1);
        if (kind != TripleBodyCandleKind.TwoCrows)
        {
            if (shortPeriod == longPeriod)
                _shortSum = _longSum;
            else
            {
                if (_shortHistory.Count == shortPeriod)
                    _shortHistory.Dequeue().AddTo(ref _shortSum, -1);
                _shortHistory.Enqueue(body);
                body.AddTo(ref _shortSum, 1);
            }
        }
        _first = _second;
        _second = bar;
        _firstBody = _secondBody;
        _secondBody = body;
        if (_seen < _warmup)
            _seen++;
        return signal;
    }

    private static void Add(ref ExactMeanAccumulator sum, in Bar bar, int weight)
    {
        sum.Add(Math.Max(bar.Open, bar.Close), weight);
        sum.Add(Math.Min(bar.Open, bar.Close), -weight);
    }

    private bool Matches(in Bar c)
    {
        var longMargin = _lagLong2;
        _firstBody.AddTo(ref longMargin, -longPeriod);
        if (longMargin.Sign >= 0)
            return false;
        if (kind != TripleBodyCandleKind.TwoCrows)
        {
            var river = kind == TripleBodyCandleKind.UniqueThreeRiver;
            var shortMargin = river ? _shortSum : _lagShort;
            // The current river body is not retained yet; preserve its original
            // arithmetic/exception point instead of eagerly decoding it.
            if (river) Add(ref shortMargin, c, -shortPeriod);
            else _secondBody.AddTo(ref shortMargin, -shortPeriod);
            if (river ? shortMargin.Sign <= 0 : shortMargin.Sign < 0)
                return false;
        }
        var a = _first;
        var b = _second;
        return kind switch
        {
            TripleBodyCandleKind.ThreeInside => Math.Max(b.Open, b.Close)
                < Math.Max(a.Open, a.Close)
                && Math.Min(b.Open, b.Close) > Math.Min(a.Open, a.Close)
                && (
                    a.Close >= a.Open
                        ? c.Close < c.Open && c.Close < a.Open
                        : c.Close >= c.Open && c.Close > a.Open
                ),
            TripleBodyCandleKind.TwoCrows => a.Close >= a.Open
                && b.Close < b.Open
                && b.Close > a.Close
                && c.Close < c.Open
                && c.Open < b.Open
                && c.Open > b.Close
                && c.Close > a.Open
                && c.Close < a.Close,
            TripleBodyCandleKind.UpsideGapTwoCrows => a.Close >= a.Open
                && b.Close < b.Open
                && b.Close > a.Close
                && c.Close < c.Open
                && c.Open > b.Open
                && c.Close < b.Close
                && c.Close > a.Close,
            _ => a.Close < a.Open
                && b.Close < b.Open
                && b.Close > a.Close
                && b.Open <= a.Open
                && b.Low < a.Low
                && c.Close >= c.Open
                && c.Open > b.Low,
        };
    }
}

internal static class TripleBodyCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int longPeriod,
        int shortPeriod,
        TripleBodyCandleKind kind
    )
    {
        var result = new double[bars.Count];
        for (var i = Math.Max(longPeriod, shortPeriod) + 2; i < bars.Count; i++)
        {
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            if (Body(a).CompareTo(Mean(bars, i - 2, longPeriod)) <= 0)
                continue;
            if (kind != TripleBodyCandleKind.TwoCrows)
            {
                var index = kind == TripleBodyCandleKind.UniqueThreeRiver ? i : i - 1;
                var comparison = Body(bars[index]).CompareTo(Mean(bars, index, shortPeriod));
                if (
                    kind == TripleBodyCandleKind.UniqueThreeRiver ? comparison >= 0 : comparison > 0
                )
                    continue;
            }
            bool match;
            if (kind == TripleBodyCandleKind.ThreeInside)
            {
                match =
                    R(b.Open).CompareTo(R(Math.Min(a.Open, a.Close))) > 0
                    && R(b.Close).CompareTo(R(Math.Min(a.Open, a.Close))) > 0
                    && R(b.Open).CompareTo(R(Math.Max(a.Open, a.Close))) < 0
                    && R(b.Close).CompareTo(R(Math.Max(a.Open, a.Close))) < 0
                    && (
                        a.Close >= a.Open
                            ? c.Close < c.Open && c.Close < a.Open
                            : c.Close >= c.Open && c.Close > a.Open
                    );
            }
            else if (kind == TripleBodyCandleKind.UniqueThreeRiver)
                match =
                    a.Close < a.Open
                    && b.Close < b.Open
                    && b.Close > a.Close
                    && b.Open <= a.Open
                    && b.Low < a.Low
                    && c.Close >= c.Open
                    && c.Open > b.Low;
            else
            {
                match =
                    a.Close >= a.Open
                    && b.Close < b.Open
                    && c.Close < c.Open
                    && Math.Min(b.Open, b.Close) > Math.Max(a.Open, a.Close);
                match &=
                    kind == TripleBodyCandleKind.TwoCrows
                        ? c.Open > b.Close
                            && c.Open < b.Open
                            && c.Close > a.Open
                            && c.Close < a.Close
                        : c.Open > b.Open && c.Close < b.Close && c.Close > a.Close;
            }
            if (match)
                result[i] =
                    kind == TripleBodyCandleKind.UniqueThreeRiver
                    || kind == TripleBodyCandleKind.ThreeInside && a.Close < a.Open
                        ? 100
                        : -100;
        }
        return result;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private static ReferenceFraction Body(Bar bar) => (R(bar.Close) - R(bar.Open)).Abs();

    private static ReferenceFraction Mean(IReadOnlyList<Bar> bars, int end, int period)
    {
        var sum = new ReferenceFraction(0);
        for (var j = end - period; j < end; j++)
            sum += Body(bars[j]);
        return sum / new ReferenceFraction(period);
    }
}
