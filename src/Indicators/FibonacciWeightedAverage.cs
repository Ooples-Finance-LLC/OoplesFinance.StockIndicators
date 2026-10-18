using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Newest-first Fibonacci weighting with available-prefix normalization.</summary>
/// <remarks>Two exact running sums implement the Fibonacci recurrence without
/// rescanning each window. Integer weights preserve long-period coefficients;
/// only the final ratio is rounded. History grows with received observations.</remarks>
public sealed class FibonacciWeightedAverage : IndicatorBase, IMovingAverage, IIndicatorValidationContract
{
    /// <summary>Creates an average with a positive window length.</summary>
    public FibonacciWeightedAverage(int period = 14)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum number of retained closes.</summary>
    public int Period { get; }
    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, bars => FibonacciWeightedSnapshot.Calculate(bars, Period), 0, 0)];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _history = new();
        private ExactMeanAccumulator _weighted, _shifted, _mass;
        private BigInteger _newest, _previous, _nextWeight, _followingWeight;
        private bool _initialized;

        public void Reset()
        {
            _history.Clear();
            _weighted = _shifted = _mass = default;
            _nextWeight = _newest;
            _followingWeight = _previous;
        }

        public double Update(in Bar bar)
        {
            if (!_initialized)
            {
                _newest = BigInteger.One;
                for (var i = 1; i < period; i++)
                    (_newest, _previous) = (_newest + _previous, _newest);
                _nextWeight = _newest;
                _followingWeight = _previous;
                _initialized = true;
            }

            var next = _shifted;
            next.Add(bar.Close, _newest);
            // F(k-2) = F(k) - F(k-1). The expired term has F(-1)=1
            // in this difference and must be removed explicitly.
            var shifted = _weighted;
            shifted.Subtract(_shifted);
            if (_history.Count == period)
                shifted.Add(_history.Dequeue(), -1);
            else
            {
                _mass.Add(1, _nextWeight);
                (_nextWeight, _followingWeight) = (_followingWeight, _nextWeight - _followingWeight);
            }
            shifted.Add(bar.Close, _previous);
            _history.Enqueue(bar.Close);
            _weighted = next;
            _shifted = shifted;
            return _weighted.Ratio(_mass);
        }
    }
}
