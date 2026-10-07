using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one for a rising trend continuation using Trady's backward anchor search; otherwise zero.</summary>
/// <remarks>This is a variable-length pattern, not the fixed five-candle TA-Lib definition. The latest qualifying long candle or rejection barrier determines the result. Percentile windows include their candidate. Reaction open/close steps and breakout endpoints are strict.</remarks>
public sealed class RisingThreeMethodsPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods and percentile fractions in [0, 1].</summary>
    public RisingThreeMethodsPattern(
        int trendPeriod = 3,
        int period = 20,
        decimal shortPercentile = .25m,
        decimal longPercentile = .75m
    )
    {
        TrendMethodsState.Validate(trendPeriod, period, shortPercentile, longPercentile);
        TrendPeriod = trendPeriod;
        ShortPercentile = shortPercentile;
        Period = period;
        LongPercentile = longPercentile;
    }

    /// <summary>Required high/low trend transitions at the anchor candle.</summary>
    public int TrendPeriod { get; }

    /// <summary>Strict short-body percentile.</summary>
    public decimal ShortPercentile { get; }

    /// <summary>Window for both body percentiles.</summary>
    public int Period { get; }

    /// <summary>Inclusive long-body percentile.</summary>
    public decimal LongPercentile { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TrendMethodsState(true, TrendPeriod, Period, ShortPercentile, LongPercentile);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TrendMethodsReference.Evaluate(
                        bars,
                        true,
                        TrendPeriod,
                        Period,
                        ShortPercentile,
                        LongPercentile
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Returns one for a falling trend continuation using Trady's backward anchor search; otherwise zero.</summary>
/// <remarks>This is a variable-length pattern, not the fixed five-candle TA-Lib definition. The latest qualifying long candle or rejection barrier determines the result. Percentile windows include their candidate. Reaction open/close steps and breakout endpoints are strict.</remarks>
public sealed class FallingThreeMethodsPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with positive periods and percentile fractions in [0, 1].</summary>
    public FallingThreeMethodsPattern(
        int trendPeriod = 3,
        int shortPeriod = 20,
        decimal shortPercentile = .25m
    )
    {
        TrendMethodsState.Validate(trendPeriod, shortPeriod, shortPercentile, .75m);
        TrendPeriod = trendPeriod;
        ShortPercentile = shortPercentile;
        ShortPeriod = shortPeriod;
    }

    /// <summary>Required high/low trend transitions at the anchor candle.</summary>
    public int TrendPeriod { get; }

    /// <summary>Strict short-body percentile.</summary>
    public decimal ShortPercentile { get; }

    /// <summary>Window for the short-body percentile. Long bodies use 20 bars and percentile 0.75.</summary>
    public int ShortPeriod { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new TrendMethodsState(false, TrendPeriod, ShortPeriod, ShortPercentile, .75m);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    TrendMethodsReference.Evaluate(
                        bars,
                        false,
                        TrendPeriod,
                        ShortPeriod,
                        ShortPercentile,
                        .75m
                    ),
                0,
                0
            ),
        ];
}

internal sealed class TrendMethodsState : IIndicatorState
{
    private readonly bool _rising;
    private readonly IIndicatorState _longBody,
        _shortBody,
        _trend;
    private Bar _previous,
        _anchor,
        _after;
    private bool _hasPrevious,
        _previousLong,
        _previousShort,
        _hasAnchor,
        _hasAfter,
        _anchorTrend;

    internal static void Validate(
        int trendPeriod,
        int period,
        decimal shortPercentile,
        decimal longPercentile
    )
    {
        if (trendPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(trendPeriod));
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (shortPercentile < 0 || shortPercentile > 1)
            throw new ArgumentOutOfRangeException(nameof(shortPercentile));
        if (longPercentile < 0 || longPercentile > 1)
            throw new ArgumentOutOfRangeException(nameof(longPercentile));
    }

    internal TrendMethodsState(
        bool rising,
        int trendPeriod,
        int period,
        decimal shortPercentile,
        decimal longPercentile
    )
    {
        _rising = rising;
        _longBody = new PercentileCandleState(
            rising ? period : 20,
            rising ? longPercentile : .75m,
            CandleLengthKind.Body,
            true,
            rising ? 1 : -1
        );
        _shortBody = new PercentileCandleState(
            period,
            shortPercentile,
            CandleLengthKind.Body,
            false,
            0
        );
        _trend = new CandleTrendState(
            rising ? CandleTrendKind.UpTrend : CandleTrendKind.DownTrend,
            trendPeriod
        );
    }

    public void Reset()
    {
        _longBody.Reset();
        _shortBody.Reset();
        _trend.Reset();
        _previous = _anchor = _after = default;
        _hasPrevious =
            _previousLong =
            _previousShort =
            _hasAnchor =
            _hasAfter =
            _anchorTrend =
                false;
    }

    public double Update(in Bar bar)
    {
        var longBody = _longBody.Update(bar) > 0;
        var shortBody = _shortBody.Update(bar) > 0;
        var trend = _trend.Update(bar) > 0;
        // A previous-bar anchor cannot satisfy both opposed breakout comparisons.
        var match =
            longBody
            && _previousShort
            && _hasAnchor
            && _hasAfter
            && _anchorTrend
            && (
                _rising
                    ? bar.High > _anchor.High
                        && _anchor.High > _after.High
                        && _anchor.Low < _previous.Low
                    : bar.Low < _anchor.Low
                        && _anchor.Low < _after.Low
                        && _anchor.High > _previous.High
            );
        var reactionStep =
            _hasPrevious
            && (
                _rising
                    ? bar.Open < _previous.Open && bar.Close < _previous.Close
                    : bar.Open > _previous.Open && bar.Close > _previous.Close
            );
        // The newest terminal event completely summarizes the backwards scan.
        // A rejection takes precedence when arbitrary percentiles overlap.
        if (shortBody && !_previousLong && !reactionStep)
        {
            _hasAnchor = false;
            _hasAfter = false;
        }
        else if (longBody)
        {
            _anchor = bar;
            _anchorTrend = trend;
            _hasAnchor = true;
            _hasAfter = false;
        }
        else if (_hasAnchor && !_hasAfter)
        {
            _after = bar;
            _hasAfter = true;
        }
        _previous = bar;
        _previousLong = longBody;
        _previousShort = shortBody;
        _hasPrevious = true;
        return match ? 1 : 0;
    }
}

internal static class TrendMethodsReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        bool rising,
        int trendPeriod,
        int period,
        decimal shortPercentile,
        decimal longPercentile
    )
    {
        var longs = PercentileCandleReference.Evaluate(
            bars,
            rising ? period : 20,
            rising ? longPercentile : .75m,
            CandleLengthKind.Body,
            true,
            rising ? 1 : -1
        );
        var shorts = PercentileCandleReference.Evaluate(
            bars,
            period,
            shortPercentile,
            CandleLengthKind.Body,
            false,
            0
        );
        var values = new double[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        {
            if (longs[i] <= 0 || shorts[i - 1] <= 0)
                continue;
            for (var j = i - 1; j >= trendPeriod; j--)
            {
                var step = rising
                    ? bars[j].Close < bars[j - 1].Close && bars[j].Open < bars[j - 1].Open
                    : bars[j].Close > bars[j - 1].Close && bars[j].Open > bars[j - 1].Open;
                if (shorts[j] > 0 && longs[j - 1] <= 0 && !step)
                    break;
                if (longs[j] <= 0)
                    continue;
                var trend = true;
                for (var k = j - trendPeriod + 1; k <= j; k++)
                    if (
                        rising
                            ? bars[k].High <= bars[k - 1].High || bars[k].Low <= bars[k - 1].Low
                            : bars[k].High >= bars[k - 1].High || bars[k].Low >= bars[k - 1].Low
                    )
                    {
                        trend = false;
                        break;
                    }
                var ends = rising
                    ? bars[i].High > bars[j].High
                        && bars[j].High > bars[j + 1].High
                        && bars[j].Low < bars[i - 1].Low
                    : bars[i].Low < bars[j].Low
                        && bars[j].Low < bars[j + 1].Low
                        && bars[j].High > bars[i - 1].High;
                values[i] = trend && ends ? 1 : 0;
                break;
            }
        }
        return values;
    }
}
