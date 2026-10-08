using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Seven-price, oldest-first trendline convention with a deterministic startup cycle.</summary>
/// <remarks>Matches the finite-arithmetic formula of QuanTAlib 1.0 Htit, whose
/// oldest-first two-element phasor buffers compare each current phasor with itself.
/// The imaginary discriminator is therefore zero and the clamped period tends to
/// six independently of price. This is a distinct convention, not a conventional
/// Hilbert estimator. The first ten results are input prices. Subsequent results
/// weight the oldest four intermediate means by 4,3,2,1. Means use the oldest
/// min(cycle,7) available prices and divide by the full cycle. Published arithmetic
/// rounds once per mean and final weighted mean, avoiding unused phasor overflow.</remarks>
public sealed class OldestFirstHilbertTrendline : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    public override int WarmupBars => 10;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                OldestFirstHilbertReference.Values,
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State : IIndicatorState
    {
        private readonly Queue<double> _prices = new(),
            _means = new();
        private long _count;
        private double _period,
            _olderSmooth,
            _newerSmooth;

        public void Reset()
        {
            _prices.Clear();
            _means.Clear();
            _count = 0;
            _period = _olderSmooth = _newerSmooth = 0;
        }

        public double Update(in Bar bar)
        {
            if (_prices.Count == 7)
                _prices.Dequeue();
            _prices.Enqueue(bar.Close);
            var mean = bar.Close;
            if (++_count > 5)
            {
                _period = 0.2 * 6 + 0.8 * _period;
                var smooth = 0.33 * _period + 0.67 * _olderSmooth;
                _olderSmooth = _newerSmooth;
                _newerSmooth = smooth;
                var cycle = (int)(smooth + 0.5);
                if (cycle > 0)
                {
                    var sum = new ExactMeanAccumulator();
                    foreach (var price in _prices.Take(cycle))
                        sum.Add(price);
                    mean = sum.Mean(cycle);
                }
            }
            if (_means.Count == 4)
                _means.Dequeue();
            _means.Enqueue(mean);
            if (_count < 11)
                return bar.Close;
            var weighted = new ExactMeanAccumulator();
            var weight = 4;
            foreach (var value in _means)
                weighted.Add(value, weight--);
            return weighted.Mean(10);
        }
    }
}

internal static class OldestFirstHilbertReference
{
    internal static IReadOnlyList<double> Values(IReadOnlyList<Bar> bars)
    {
        var periods = new double[bars.Count];
        var smooth = new double[bars.Count];
        var means = new double[bars.Count];
        var result = new double[bars.Count];
        double Product(double a, double b) =>
            (ReferenceFraction.FromDouble(a) * ReferenceFraction.FromDouble(b)).ToDouble();
        double Sum(double a, double b) =>
            (ReferenceFraction.FromDouble(a) + ReferenceFraction.FromDouble(b)).ToDouble();
        for (var i = 0; i < bars.Count; i++)
        {
            means[i] = bars[i].Close;
            if (i >= 5)
            {
                periods[i] = Sum(Product(.2, 6), Product(.8, periods[i - 1]));
                smooth[i] = Sum(Product(.33, periods[i]), Product(.67, smooth[i - 2]));
                var cycle = (int)Sum(smooth[i], .5);
                if (cycle > 0)
                {
                    var total = new ReferenceFraction(0);
                    var start = Math.Max(0, i - 6);
                    for (var j = start; j < Math.Min(i + 1, start + cycle); j++)
                        total += ReferenceFraction.FromDouble(bars[j].Close);
                    means[i] = (total / new ReferenceFraction(cycle)).ToDouble();
                }
            }
            result[i] = bars[i].Close;
            if (i >= 10)
            {
                var total = new ReferenceFraction(0);
                for (var j = 0; j < 4; j++)
                    total +=
                        new ReferenceFraction(4 - j)
                        * ReferenceFraction.FromDouble(means[i - 3 + j]);
                result[i] = (total / new ReferenceFraction(10)).ToDouble();
            }
        }
        return result;
    }
}
