using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>An exponential moving average seeded by the first input, using alpha = 2 / (period + 1).</summary>
/// <remarks>Every bar has a defined output, starting with its first close. This differs from the conventional SMA-seeded Ema.
/// Each subsequent convex combination is rounded once, preserving finite extreme and subnormal inputs.</remarks>
public sealed class FirstValueEma : IndicatorBase, IMovingAverage, IIndicatorValidationContract
{
    /// <summary>Creates an exponential average with a positive smoothing period.</summary>
    public FirstValueEma(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Smoothing period; the initial value is independent of the period.</summary>
    public int Period { get; }

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
            if (i == 0)
                result[i] = bars[i].Close;
            else
                result[i] = (
                    (
                        ReferenceFraction.FromDouble(bars[i].Close) * new ReferenceFraction(2)
                        + ReferenceFraction.FromDouble(result[i - 1])
                            * new ReferenceFraction(Period - 1)
                    ) / new ReferenceFraction((long)Period + 1)
                ).ToDouble();
        }
        return result;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private bool _initialized;
        private double _previous;

        public void Reset()
        {
            _initialized = false;
            _previous = 0;
        }

        public double Update(in Bar bar)
        {
            if (!_initialized || period == 1)
                _previous = bar.Close;
            else
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(bar.Close, 2);
                sum.Add(_previous, period - 1);
                _previous = sum.Mean((long)period + 1);
            }
            _initialized = true;
            return _previous;
        }
    }
}
