using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Median-adaptive smoothing with descending candidate windows and upper medians.</summary>
/// <remarks>The first three prices pass through. The four-price 1/2/2/1 mean then
/// fills a lazy history. Mature windows shrink by two; signed-median error comparisons
/// are exact and each complete smoothing update rounds once. Reusable buffers avoid
/// per-candidate histories and sorting. MedianAdaptiveSnapshot remains the batch reference.</remarks>
public sealed class MedianAdaptiveAverage : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive history length and finite signed threshold.</summary>
    public MedianAdaptiveAverage(int period = 39, double threshold = .002)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
        Period = period; Threshold = threshold;
    }
    /// <summary>Maximum median history.</summary>
    public int Period { get; }
    /// <summary>Signed error threshold.</summary>
    public double Threshold { get; }
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Threshold);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, bars => MedianAdaptiveSnapshot.Calculate(bars, Period, Threshold), 0, 0)];

    internal sealed class State(int period, double threshold) : IIndicatorState
    {
        private readonly List<double> _history = new();
        private double[] _sorted = [];
        private int _seen, _position;
        private double _a, _b, _c, _filter, _candidate;
        public void Reset()
        {
            _history.Clear(); _seen = _position = 0;
            _a = _b = _c = _filter = _candidate = 0;
        }
        public double Update(in Bar bar)
        {
            if (!FrameworkCompatibility.IsFinite(bar.Close)) throw new ArgumentOutOfRangeException(nameof(bar));
            double price = bar.Close, smooth = price;
            if (_seen == 3)
            {
                var total = JurikDyadic.FromDouble(_a);
                var term = JurikDyadic.FromDouble(_b); term.Multiply(2); total.AddExact(term);
                term = JurikDyadic.FromDouble(_c); term.Multiply(2); total.AddExact(term);
                total.Add(price); smooth = total.Mean(6);
            }
            _a = _b; _b = _c; _c = price;
            if (_seen < 3) { _seen++; return price; }
            if (_history.Count < period) _history.Add(smooth);
            else
            {
                _history[_position] = smooth;
                if (++_position == period) _position = 0;
            }
            if (_history.Count < period) return smooth;

            int length = period, removed = 0;
            double candidate = _candidate;
            bool above = .2 > threshold;
            if (above)
            {
                if (_sorted.Length < period) _sorted = new double[period];
                _history.CopyTo(_sorted);
                Array.Sort(_sorted, 0, period);
            }
            while (above && length > 0)
            {
                double median = _sorted[length / 2];
                candidate = Blend(smooth, _candidate, length);
                var m = JurikDyadic.FromDouble(median);
                if (m.Sign != 0)
                {
                    var difference = m; difference.Subtract(JurikDyadic.FromDouble(candidate));
                    if (difference.Sign < 0) difference.Multiply(-1);
                    var bound = m; bound.Multiply(threshold); difference.Subtract(bound);
                    above = m.Sign > 0 ? difference.Sign > 0 : difference.Sign < 0;
                }
                int nextLength = length - 2;
                if (above && nextLength > 0)
                {
                    // Delete the two oldest occurrences, retaining the sorted suffix
                    // multiset. Equal values are interchangeable, including signed zero.
                    for (int step = 0; step < 2; step++)
                    {
                        int index = (int)(((long)_position + removed++) % period);
                        int at = Array.BinarySearch(_sorted, 0, length, _history[index]);
                        Array.Copy(_sorted, at + 1, _sorted, at, length - at - 1);
                        length--;
                    }
                }
                length = nextLength;
            }
            _filter = Blend(smooth, _filter, Math.Max(3, length));
            _candidate = candidate;
            return _filter;
        }
        private static double Blend(double input, double previous, int length)
        {
            var total = JurikDyadic.FromDouble(input); total.Multiply(2);
            var old = JurikDyadic.FromDouble(previous); old.Multiply(length - 1); total.AddExact(old);
            return total.Ratio(JurikDyadic.FromDouble((long)length + 1));
        }
    }
}
