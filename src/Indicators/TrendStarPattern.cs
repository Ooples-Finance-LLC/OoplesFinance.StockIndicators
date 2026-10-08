using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Trend-qualified star definitions with a relative first-body midpoint target.</summary>
public enum TrendStarKind
{
    /// <summary>Downtrend followed by a short-body morning star.</summary>
    Morning,

    /// <summary>Uptrend followed by a short-body evening star.</summary>
    Evening,

    /// <summary>Downtrend followed by a doji morning star.</summary>
    MorningDoji,

    /// <summary>Uptrend followed by a doji evening star.</summary>
    EveningDoji,
}

/// <summary>Returns one for a trend-qualified star whose final close is strictly within a relative tolerance of the first body's midpoint; otherwise zero.</summary>
/// <remarks>Follows Trady's star structure with exact supplied prices and decimal thresholds. The bearish long body uses 20 bars and percentile 0.75; the bullish body uses the requested window. A zero first-body midpoint cannot satisfy the relative target and returns zero. Doji variants use DojiFraction; other variants use ShortPercentile. Native decimal rounding may differ at strict ratio boundaries.</remarks>
public sealed class TrendStarPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a star recognizer with positive periods, body fractions in [0, 1], and a nonnegative relative midpoint tolerance.</summary>
    public TrendStarPattern(
        TrendStarKind kind = TrendStarKind.Morning,
        int trendPeriod = 3,
        int period = 20,
        decimal shortPercentile = .25m,
        decimal longPercentile = .75m,
        decimal dojiFraction = .25m,
        decimal midpointTolerance = .1m
    )
    {
        if (!Enum.IsDefined(typeof(TrendStarKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        TrendBabyState.Validate(trendPeriod, period, longPercentile, dojiFraction);
        if (shortPercentile < 0 || shortPercentile > 1)
            throw new ArgumentOutOfRangeException(nameof(shortPercentile));
        if (midpointTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(midpointTolerance));
        Kind = kind;
        TrendPeriod = trendPeriod;
        Period = period;
        ShortPercentile = shortPercentile;
        LongPercentile = longPercentile;
        DojiFraction = dojiFraction;
        MidpointTolerance = midpointTolerance;
    }

    /// <summary>Star direction and middle-body definition.</summary>
    public TrendStarKind Kind { get; }

    /// <summary>Required high/low trend transitions ending at the middle candle.</summary>
    public int TrendPeriod { get; }

    /// <summary>Window for bullish long bodies and non-doji short middle bodies.</summary>
    public int Period { get; }

    /// <summary>Strict short-middle-body percentile for non-doji variants.</summary>
    public decimal ShortPercentile { get; }

    /// <summary>Inclusive bullish long-body percentile.</summary>
    public decimal LongPercentile { get; }

    /// <summary>Strict middle body/range fraction for doji variants.</summary>
    public decimal DojiFraction { get; }

    /// <summary>Strict relative distance from the first body's midpoint.</summary>
    public decimal MidpointTolerance { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() => new TrendStarState(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => TrendStarReference.Evaluate(bars, this),
                0,
                0
            ),
        ];
}

internal sealed class TrendStarState : IIndicatorState
{
    private readonly bool _morning,
        _dojiKind;
    private readonly IIndicatorState _trend,
        _bull,
        _bear,
        _small;
    private readonly BigInteger _toleranceNumerator,
        _toleranceDenominator;
    private Bar _first,
        _middle;
    private bool _firstLong,
        _middleLong,
        _middleSmall,
        _priorTrend;
    private int _count;

    internal TrendStarState(TrendStarPattern options)
    {
        _morning = options.Kind is TrendStarKind.Morning or TrendStarKind.MorningDoji;
        _dojiKind = options.Kind is TrendStarKind.MorningDoji or TrendStarKind.EveningDoji;
        _trend = new CandleTrendState(
            _morning ? CandleTrendKind.DownTrend : CandleTrendKind.UpTrend,
            options.TrendPeriod
        );
        _bull = new PercentileCandleState(
            options.Period,
            options.LongPercentile,
            CandleLengthKind.Body,
            true,
            1
        );
        _bear = new PercentileCandleState(20, .75m, CandleLengthKind.Body, true, -1);
        _small = _dojiKind
            ? (IIndicatorState)new DojiCandle(options.DojiFraction).CreateState()
            : new PercentileCandleState(
                options.Period,
                options.ShortPercentile,
                CandleLengthKind.Body,
                false,
                0
            );
        var bits = decimal.GetBits(options.MidpointTolerance);
        _toleranceNumerator =
            new BigInteger(unchecked((uint)bits[0]))
            + (new BigInteger(unchecked((uint)bits[1])) << 32)
            + (new BigInteger(unchecked((uint)bits[2])) << 64);
        _toleranceDenominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
    }

    public void Reset()
    {
        _trend.Reset();
        _bull.Reset();
        _bear.Reset();
        _small.Reset();
        _first = _middle = default;
        _firstLong = _middleLong = _middleSmall = _priorTrend = false;
        _count = 0;
    }

    public double Update(in Bar bar)
    {
        var bull = _bull.Update(bar) > 0;
        var bear = _bear.Update(bar) > 0;
        var match =
            _count == 2
            && _priorTrend
            && _firstLong
            && _middleSmall
            && (_morning ? bull : bear)
            && Structure(bar)
            && NearMidpoint(bar.Close);
        _priorTrend = _trend.Update(bar) > 0;
        _firstLong = _middleLong;
        _middleLong = _morning ? bear : bull;
        _middleSmall = _small.Update(bar) > 0;
        _first = _middle;
        _middle = bar;
        if (_count < 2)
            _count++;
        return match ? 1 : 0;
    }

    private bool Structure(in Bar bar)
    {
        var middlePosition = new ExactMeanAccumulator();
        middlePosition.Add(_middle.Close);
        if (_dojiKind)
        {
            middlePosition.Add(_middle.Open);
            middlePosition.Add(_first.Close, -2);
        }
        else
            middlePosition.Add(_first.Close, -1);
        return _morning
            ? middlePosition.Sign < 0 && bar.Open > Math.Max(_middle.Open, _middle.Close)
            : middlePosition.Sign > 0 && bar.Open < Math.Min(_middle.Open, _middle.Close);
    }

    private bool NearMidpoint(double close)
    {
        var distance = new ExactMeanAccumulator();
        distance.Add(close, 2 * _toleranceDenominator);
        distance.Add(_first.Open, -_toleranceDenominator);
        distance.Add(_first.Close, -_toleranceDenominator);
        var target = new ExactMeanAccumulator();
        target.Add(_first.Open, _toleranceNumerator);
        target.Add(_first.Close, _toleranceNumerator);
        var difference = new ExactMeanAccumulator();
        if (target.Sign < 0)
            difference.Subtract(target);
        else
            difference.AddExact(target);
        if (distance.Sign < 0)
            difference.AddExact(distance);
        else
            difference.Subtract(distance);
        return difference.Sign > 0;
    }
}

internal static class TrendStarReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        TrendStarPattern options
    )
    {
        var morning = options.Kind is TrendStarKind.Morning or TrendStarKind.MorningDoji;
        var doji = options.Kind is TrendStarKind.MorningDoji or TrendStarKind.EveningDoji;
        var bulls = PercentileCandleReference.Evaluate(
            bars,
            options.Period,
            options.LongPercentile,
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
        var shorts = doji
            ? null
            : PercentileCandleReference.Evaluate(
                bars,
                options.Period,
                options.ShortPercentile,
                CandleLengthKind.Body,
                false,
                0
            );
        var tolerance = D(options.MidpointTolerance);
        var fraction = D(options.DojiFraction);
        var values = new double[bars.Count];
        for (var i = 2; i < bars.Count; i++)
        {
            if (i - 1 < options.TrendPeriod)
                continue;
            var trend = true;
            for (var j = i - options.TrendPeriod; j < i; j++)
                if (
                    morning
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
            var small = doji
                ? (R(b.Close) - R(b.Open)).Abs().CompareTo((R(b.High) - R(b.Low)) * fraction) < 0
                : shorts![i - 1] > 0;
            var middle = doji ? (R(b.Open) + R(b.Close)) / new ReferenceFraction(2) : R(b.Close);
            var position = middle.CompareTo(R(a.Close));
            var structure = morning
                ? position < 0 && c.Open > Math.Max(b.Open, b.Close)
                : position > 0 && c.Open < Math.Min(b.Open, b.Close);
            var midpoint = (R(a.Open) + R(a.Close)) / new ReferenceFraction(2);
            var near = (R(c.Close) - midpoint).Abs().CompareTo(midpoint.Abs() * tolerance) < 0;
            var outer = morning
                ? bears[i - 2] > 0 && bulls[i] > 0
                : bulls[i - 2] > 0 && bears[i] > 0;
            values[i] = trend && small && structure && near && outer ? 1 : 0;
        }
        return values;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private static ReferenceFraction D(decimal value)
    {
        var s = value.ToString(CultureInfo.InvariantCulture);
        var point = s.IndexOf('.');
        return new ReferenceFraction(
                BigInteger.Parse(s.Replace(".", ""), CultureInfo.InvariantCulture)
            ) / new ReferenceFraction(BigInteger.Pow(10, point < 0 ? 0 : s.Length - point - 1));
    }
}
