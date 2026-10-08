using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Shannon entropy of exact close frequencies, normalized by the number of distinct closes.</summary>
/// <remarks>Uses the available rolling window immediately. By convention a single distinct
/// value returns one, matching QuanTAlib's Entropy definition. Signed zeros form one group.
/// History grows lazily; close magnitudes do not enter the entropy arithmetic.</remarks>
public sealed class NormalizedWindowEntropy : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates an entropy indicator with a window of at least two bars.</summary>
    public NormalizedWindowEntropy(int period = 14)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum number of closes in the frequency distribution.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, Reference, 0, 4e-15)];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(Period, i + 1);
            var groups = bars.Skip(i - count + 1).Take(count).GroupBy(b => b.Close).ToArray();
            if (groups.Length == 1)
            {
                result[i] = 1;
                continue;
            }
            var sum = new ReferenceFraction(0);
            foreach (var group in groups)
            {
                var p = new ReferenceFraction(group.Count()) / new ReferenceFraction(count);
                sum -= p * ReferenceFraction.FromDouble(p.LogToDouble());
            }
            result[i] = sum.ToDouble() / new ReferenceFraction(groups.Length).LogToDouble();
        }
        return result;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _window = new();
        private readonly Dictionary<double, int> _counts = new();

        public void Reset()
        {
            _window.Clear();
            _counts.Clear();
        }

        public double Update(in Bar bar)
        {
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                if (--_counts[old] == 0)
                    _counts.Remove(old);
            }
            _window.Enqueue(bar.Close);
            _counts.TryGetValue(bar.Close, out var frequency);
            _counts[bar.Close] = frequency + 1;
            if (_counts.Count == 1)
                return 1;
            double sum = 0,
                correction = 0;
            foreach (var count in _counts.Values)
            {
                // Compensate rounding of 1+x for a nearly constant window.
                // Positive integer counts ensure x >= 1/(int.MaxValue-1).
                var x = (double)(_window.Count - count) / count;
                var rounded = 1 + x;
                var logarithm = Math.Log(rounded) * (x / (rounded - 1));
                var term = (double)count / _window.Count * logarithm;
                var adjusted = term - correction;
                var next = sum + adjusted;
                correction = (next - sum) - adjusted;
                sum = next;
            }
            return sum / Math.Log(_counts.Count);
        }
    }
}
