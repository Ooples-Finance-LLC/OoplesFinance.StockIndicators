using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Mean of the most frequent distinct closes in a full rolling window.</summary>
/// <remarks>Before a full window, publishes the expanding arithmetic mean.
/// Equal-frequency modes each receive one vote regardless of their frequency.
/// Ties use exact binary64 equality, with signed zeros denoting the same value.
/// Means round once without intermediate overflow. Histories grow lazily.</remarks>
public sealed class WindowMode : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a mode with a positive full-window length.</summary>
    public WindowMode(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of closes required before replacing the expanding mean with the mode.</summary>
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
            var count = Math.Min(Period, i + 1);
            var values = bars.Skip(i - count + 1).Take(count).Select(b => b.Close).ToArray();
            if (count == Period)
            {
                var groups = values.GroupBy(v => v).ToArray();
                var frequency = groups.Max(group => group.Count());
                values = groups
                    .Where(group => group.Count() == frequency)
                    .Select(group => group.Key)
                    .ToArray();
            }
            var sum = values.Aggregate(
                new ReferenceFraction(0),
                (a, b) => a + ReferenceFraction.FromDouble(b)
            );
            result[i] = (sum / new ReferenceFraction(values.Length)).ToDouble();
        }
        return result;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private readonly Queue<double> _window = new();
        private readonly Dictionary<double, int> _frequencies = new();
        private ExactMeanAccumulator _sum;

        public void Reset()
        {
            _window.Clear();
            _frequencies.Clear();
            _sum = default;
        }

        public double Update(in Bar bar)
        {
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _sum.Add(old, -1);
                if (_frequencies[old] == 1)
                    _frequencies.Remove(old);
                else
                    _frequencies[old]--;
            }
            _window.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            _frequencies.TryGetValue(bar.Close, out var current);
            _frequencies[bar.Close] = current + 1;
            if (_window.Count < period)
                return _sum.Mean(_window.Count);
            var maximum = 0;
            var modes = new ExactMeanAccumulator();
            var count = 0;
            foreach (var entry in _frequencies)
            {
                if (entry.Value < maximum)
                    continue;
                if (entry.Value > maximum)
                {
                    maximum = entry.Value;
                    modes = default;
                    count = 0;
                }
                modes.Add(entry.Key);
                count++;
            }
            return modes.Mean(count);
        }
    }
}
