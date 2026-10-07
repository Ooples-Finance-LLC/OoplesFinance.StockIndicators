using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one for a bullish abandoned baby with prior high/low trend, percentile-long outer bodies, and an isolated doji; otherwise zero.</summary>
/// <remarks>Uses the Trady definition. The bullish body uses the configured percentile window; the bearish body uses the fixed 20-bar 0.75 percentile used by Trady. Both windows include their candidate candle. Full-range gaps and doji comparisons are strict.</remarks>
public sealed class BullishAbandonedBabyPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive trend/body periods and decimal fractions in [0, 1].</summary>
    public BullishAbandonedBabyPattern(
        int trendPeriod = 3,
        int longPeriod = 20,
        decimal longPercentile = .75m,
        decimal dojiFraction = .1m
    )
    {
        TrendBabyState.Validate(trendPeriod, longPeriod, longPercentile, dojiFraction);
        TrendPeriod = trendPeriod;
        LongPeriod = longPeriod;
        LongPercentile = longPercentile;
        DojiFraction = dojiFraction;
    }

    /// <summary>Required strict high/low transitions ending at the middle candle.</summary>
    public int TrendPeriod { get; }

    /// <summary>Window length for the bullish long body.</summary>
    public int LongPeriod { get; }

    /// <summary>Exact decimal percentile for the bullish long body.</summary>
    public decimal LongPercentile { get; }

    /// <summary>Strict body/range fraction for the middle candle.</summary>
    public decimal DojiFraction { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TrendBabyState(true, TrendPeriod, LongPeriod, LongPercentile, DojiFraction);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TrendBabyReference.Evaluate(
                        bars,
                        true,
                        TrendPeriod,
                        LongPeriod,
                        LongPercentile,
                        DojiFraction
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Returns one for a bearish abandoned baby with prior high/low trend, percentile-long outer bodies, and an isolated doji; otherwise zero.</summary>
/// <remarks>Uses the Trady definition. The bullish body uses the configured percentile window; the bearish body uses the fixed 20-bar 0.75 percentile used by Trady. Both windows include their candidate candle. Full-range gaps and doji comparisons are strict.</remarks>
public sealed class BearishAbandonedBabyPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive trend/body periods and decimal fractions in [0, 1].</summary>
    public BearishAbandonedBabyPattern(
        int trendPeriod = 3,
        int longPeriod = 20,
        decimal longPercentile = .75m,
        decimal dojiFraction = .1m
    )
    {
        TrendBabyState.Validate(trendPeriod, longPeriod, longPercentile, dojiFraction);
        TrendPeriod = trendPeriod;
        LongPeriod = longPeriod;
        LongPercentile = longPercentile;
        DojiFraction = dojiFraction;
    }

    /// <summary>Required strict high/low transitions ending at the middle candle.</summary>
    public int TrendPeriod { get; }

    /// <summary>Window length for the bullish long body.</summary>
    public int LongPeriod { get; }

    /// <summary>Exact decimal percentile for the bullish long body.</summary>
    public decimal LongPercentile { get; }

    /// <summary>Strict body/range fraction for the middle candle.</summary>
    public decimal DojiFraction { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TrendBabyState(false, TrendPeriod, LongPeriod, LongPercentile, DojiFraction);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TrendBabyReference.Evaluate(
                        bars,
                        false,
                        TrendPeriod,
                        LongPeriod,
                        LongPercentile,
                        DojiFraction
                    ),
                0,
                0
            ),
        ];
}

internal sealed class TrendBabyState : IIndicatorState
{
    private readonly bool _bullish;
    private readonly IIndicatorState _trend,
        _bullBody,
        _bearBody,
        _doji;
    private Bar _first,
        _middle;
    private bool _firstLong,
        _middleLong,
        _middleDoji;
    private int _count;

    internal static void Validate(
        int trendPeriod,
        int longPeriod,
        decimal longPercentile,
        decimal dojiFraction
    )
    {
        if (trendPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(trendPeriod));
        if (longPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (longPercentile < 0 || longPercentile > 1)
            throw new ArgumentOutOfRangeException(nameof(longPercentile));
        if (dojiFraction < 0 || dojiFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(dojiFraction));
    }

    internal TrendBabyState(
        bool bullish,
        int trendPeriod,
        int longPeriod,
        decimal longPercentile,
        decimal dojiFraction
    )
    {
        _bullish = bullish;
        _trend = new CandleTrendState(
            bullish ? CandleTrendKind.DownTrend : CandleTrendKind.UpTrend,
            trendPeriod
        );
        _bullBody = new PercentileCandleState(
            longPeriod,
            longPercentile,
            CandleLengthKind.Body,
            true,
            1
        );
        _bearBody = new PercentileCandleState(20, .75m, CandleLengthKind.Body, true, -1);
        _doji = (IIndicatorState)new DojiCandle(dojiFraction).CreateState();
    }

    public void Reset()
    {
        _trend.Reset();
        _bullBody.Reset();
        _bearBody.Reset();
        _doji.Reset();
        _first = _middle = default;
        _firstLong = _middleLong = _middleDoji = _priorTrend = false;
        _count = 0;
    }

    private bool _priorTrend;

    public double Update(in Bar bar)
    {
        var bull = _bullBody.Update(bar) > 0;
        var bear = _bearBody.Update(bar) > 0;
        var gaps = _bullish
            ? _middle.High < _first.Low && _middle.High < bar.Low
            : _middle.Low > _first.High && _middle.Low > bar.High;
        var match =
            _count == 2
            && _priorTrend
            && _firstLong
            && _middleDoji
            && gaps
            && (_bullish ? bull : bear);
        _priorTrend = _trend.Update(bar) > 0;
        _firstLong = _middleLong;
        _middleLong = _bullish ? bear : bull;
        _middleDoji = _doji.Update(bar) > 0;
        _first = _middle;
        _middle = bar;
        if (_count < 2)
            _count++;
        return match ? 1 : 0;
    }
}

internal static class TrendBabyReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        bool bullish,
        int trendPeriod,
        int longPeriod,
        decimal percentile,
        decimal doji
    )
    {
        var bulls = PercentileCandleReference.Evaluate(
            bars,
            longPeriod,
            percentile,
            CandleLengthKind.Body,
            true,
            1
        );
        var bears = PercentileCandleReference.Evaluate(
            bars,
            20,
            .75m,
            CandleLengthKind.Body,
            true,
            -1
        );
        var s = doji.ToString(CultureInfo.InvariantCulture);
        var point = s.IndexOf('.');
        var fraction =
            new ReferenceFraction(
                BigInteger.Parse(s.Replace(".", ""), CultureInfo.InvariantCulture)
            ) / new ReferenceFraction(BigInteger.Pow(10, point < 0 ? 0 : s.Length - point - 1));
        var values = new double[bars.Count];
        for (var i = 2; i < bars.Count; i++)
        {
            if (i - 1 < trendPeriod)
                continue;
            var trend = true;
            for (var j = i - trendPeriod; j < i; j++)
                if (
                    bullish
                        ? bars[j].High >= bars[j - 1].High || bars[j].Low >= bars[j - 1].Low
                        : bars[j].High <= bars[j - 1].High || bars[j].Low <= bars[j - 1].Low
                )
                {
                    trend = false;
                    break;
                }
            var a = bars[i - 2];
            var b = bars[i - 1];
            var c = bars[i];
            var small =
                (R(b.Close) - R(b.Open)).Abs().CompareTo((R(b.High) - R(b.Low)) * fraction) < 0;
            var isolated = bullish
                ? b.High < Math.Min(a.Low, c.Low)
                : b.Low > Math.Max(a.High, c.High);
            var outer = bullish
                ? bears[i - 2] > 0 && bulls[i] > 0
                : bulls[i - 2] > 0 && bears[i] > 0;
            values[i] = trend && small && isolated && outer ? 1 : 0;
        }
        return values;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
}
