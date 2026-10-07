using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Fractal adaptive close average using the ranges of two adjacent half windows.</summary>
/// <remarks>Incomplete windows publish their mean. Full windows use the oldest floor(period/2)
/// prices as the first half. Dimension is log((splitRange/half+epsilon)/(range/period+epsilon))/log(2).
/// The exact positive ratio rounds once before Math.Log; Math.Exp defines the gain, clamped
/// to [0.01,1]. The convex recurrence rounds once. History grows with available input.</remarks>
public sealed class RangeFractalAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a fractal average with a period of at least two.</summary>
    public RangeFractalAverage(int period = 14)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Full range window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => RangeAdaptiveReference.Fractal(bars, Period),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _prices = new();
        private BigInteger _sum,
            _average;
        private static readonly BigInteger Grid = BigInteger.One << 1074;

        public void Reset()
        {
            _prices.Clear();
            _sum = _average = 0;
        }

        public double Update(in Bar bar)
        {
            if (_prices.Count == period)
                _sum -= ExactVarianceWindow.Units(_prices.Dequeue());
            _prices.Enqueue(bar.Close);
            var price = ExactVarianceWindow.Units(bar.Close);
            _sum += price;
            if (_prices.Count < period)
                _average = RocBankValue.RoundUnits(_sum, _prices.Count);
            else
            {
                var window = _prices.ToArray();
                var half = period / 2;
                BigInteger Range(int start, int count)
                {
                    var high = window[start];
                    var low = high;
                    for (var i = start + 1; i < start + count; i++)
                    {
                        high = Math.Max(high, window[i]);
                        low = Math.Min(low, window[i]);
                    }
                    return ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
                }
                var whole = Range(0, period);
                var split = Range(0, half) + Range(half, period - half);
                // epsilon is exactly one grid unit; integer cross-products cannot overflow.
                var ratio = ExactMeanAccumulator.UnitRatio(
                    ((split + half) * period) << 1074,
                    (whole + period) * half
                );
                var dimension = Math.Log(ratio) / Math.Log(2);
                var gain = Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (dimension - 1))));
                var alpha = ExactVarianceWindow.Units(gain);
                _average = RocBankValue.RoundUnits(alpha * price + (Grid - alpha) * _average, Grid);
            }
            return ExactMeanAccumulator.UnitRatio(_average, 1);
        }
    }
}
