using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Linear weighted average retaining period-based weights during startup.</summary>
/// <remarks>The newest close has weight Period, the previous close Period-1,
/// and so on. Available weights are normalized even before a full window, so
/// startup differs from a WMA whose weights are count through one. Each result
/// rounds the complete weighted ratio once. History grows lazily; updates take
/// constant work independent of the period. Compose two instances with Of for
/// a double weighted moving average, rounding the intermediate series once.</remarks>
public sealed class FixedPeriodWma : IndicatorBase, IMovingAverage, IIndicatorValidationContract
{
    /// <summary>Creates a weighted average with a positive maximum window length.</summary>
    public FixedPeriodWma(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Full-window length and weight of the newest close.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, Reference, 0, 0)];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            long mass = 0;
            for (var lag = 0; lag < Math.Min(Period, i + 1); lag++)
            {
                var weight = Period - lag;
                sum +=
                    ReferenceFraction.FromDouble(bars[i - lag].Close)
                    * new ReferenceFraction(weight);
                mass += weight;
            }
            result[i] = (sum / new ReferenceFraction(mass)).ToDouble();
        }
        return result;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _window = new();
        private ExactMeanAccumulator _sum,
            _weighted;

        public void Reset()
        {
            _window.Clear();
            _sum = _weighted = default;
        }

        public double Update(in Bar bar)
        {
            _weighted.Subtract(_sum);
            if (_window.Count == period)
                _sum.Add(_window.Dequeue(), -1);
            _window.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            _weighted.Add(bar.Close, period);
            var count = _window.Count;
            var mass = (long)count * (2L * period - count + 1) / 2;
            return _weighted.Mean(mass);
        }
    }
}
