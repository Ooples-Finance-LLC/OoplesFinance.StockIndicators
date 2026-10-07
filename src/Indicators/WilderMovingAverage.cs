using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Wilder's smoothed moving average, with alpha = 1/period and an explicit seed convention.</summary>
/// <remarks>By default startup publishes the expanding arithmetic mean, then applies
/// ((period-1)*previous+close)/period after the first complete window. When
/// seedWithAverage is false, the first close seeds the recurrence immediately.
/// Each mean and subsequent convex combination is rounded once, without intermediate overflow.</remarks>
public sealed class WilderMovingAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period Wilder average with either a full-window or first-value seed.</summary>
    public WilderMovingAverage(int period = 14, bool seedWithAverage = true)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        SeedWithAverage = seedWithAverage;
    }

    /// <summary>Positive smoothing period.</summary>
    public int Period { get; }

    /// <summary>Whether to seed with the mean of the first period closes.</summary>
    public bool SeedWithAverage { get; }

    /// <inheritdoc/>
    public override int WarmupBars => SeedWithAverage ? Period - 1 : 0;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, SeedWithAverage);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, Reference, 0, 0)];

    internal IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        var initial = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var value = ReferenceFraction.FromDouble(bars[i].Close);
            if (SeedWithAverage && i < Period)
            {
                initial += value;
                result[i] = (initial / new ReferenceFraction(i + 1)).ToDouble();
            }
            else if (i == 0)
                result[i] = bars[i].Close;
            else
                result[i] = (
                    (
                        ReferenceFraction.FromDouble(result[i - 1])
                            * new ReferenceFraction(Period - 1)
                        + value
                    ) / new ReferenceFraction(Period)
                ).ToDouble();
        }
        return result;
    }

    private sealed class State(int period, bool seedWithAverage) : IIndicatorState
    {
        private int _seedCount;
        private ExactMeanAccumulator _seedSum;
        private double _previous;
        private bool _started;

        public void Reset()
        {
            _seedCount = 0;
            _seedSum = default;
            _previous = 0;
            _started = false;
        }

        public double Update(in Bar bar)
        {
            if (seedWithAverage && _seedCount < period)
            {
                _seedSum.Add(bar.Close);
                _previous = _seedSum.Mean(++_seedCount);
                if (_seedCount == period)
                    _seedSum = default;
            }
            else if (!_started)
                _previous = bar.Close;
            else
            {
                var numerator = new ExactMeanAccumulator();
                numerator.Add(_previous, period - 1);
                numerator.Add(bar.Close);
                _previous = numerator.Mean(period);
            }
            _started = true;
            return _previous;
        }
    }
}
