using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Zero-lag EMA with an expanding startup alpha and clamped available lag.</summary>
/// <remarks>Uses synthetic close 2*close-laggedClose and alpha
/// 2/(min(observations,period)+1). Lag is floor((period-1)/2), clamped to available
/// history. The first close seeds the result. Each complete update rounds once,
/// avoiding overflow of an unpublished synthetic close. History grows lazily;
/// final output overflow is rejected by the runtime.</remarks>
public sealed class ExpandingZeroLagEma
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period zero-lag recurrence.</summary>
    public ExpandingZeroLagEma(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum alpha period and basis of the half-period lag.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var lag = Math.Min(i, (Period - 1) / 2);
            var synthetic =
                new ReferenceFraction(2) * ReferenceFraction.FromDouble(bars[i].Close)
                - ReferenceFraction.FromDouble(bars[i - lag].Close);
            var effective = Math.Min((long)i + 1, Period);
            var previous =
                i == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble(values[i - 1]);
            values[i] = (
                (
                    new ReferenceFraction(2) * synthetic
                    + new ReferenceFraction(effective - 1) * previous
                ) / new ReferenceFraction(effective + 1)
            ).ToDouble();
            if (double.IsInfinity(values[i]))
                break;
        }
        return values;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _history = new();
        private int _count;
        private double _previous;

        public void Reset()
        {
            _history.Clear();
            _count = 0;
            _previous = 0;
        }

        public double Update(in Bar bar)
        {
            if (_history.Count == (period - 1) / 2 + 1)
                _history.Dequeue();
            _history.Enqueue(bar.Close);
            if (_count < period)
                _count++;
            var sum = new ExactMeanAccumulator();
            sum.Add(bar.Close, 4);
            sum.Add(_history.Peek(), -2);
            sum.Add(_previous, _count - 1);
            _previous = sum.Mean((long)_count + 1);
            return _previous;
        }
    }
}
