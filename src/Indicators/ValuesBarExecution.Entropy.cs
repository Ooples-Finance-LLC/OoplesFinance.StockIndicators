#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Cache only pure count arithmetic. Dictionary insertion/removal and
    // enumeration order remain identical to the original scalar state: its
    // compensated sum cannot be reordered by replaying just the last window.
    private sealed class EntropyValueState : IIndicatorState
    {
        private readonly double[] _window, _terms, _logarithms;
        private readonly int[] _termCounts;
        private readonly Dictionary<double, int> _counts = new();
        private int _position, _seen;

        internal EntropyValueState(int period, int count)
        {
            int capacity = Math.Min(period, Math.Max(1, count));
            _window = new double[capacity];
            _terms = new double[capacity + 1];
            _termCounts = new int[capacity + 1];
            _logarithms = new double[capacity + 1];
        }

        public void Reset()
        {
            _position = _seen = 0;
            _counts.Clear();
            // Cached terms depend only on (frequency, observation count).
        }

        public double Update(in Bar bar)
        {
            if (_seen == _window.Length)
            {
                double old = _window[_position];
                if (--_counts[old] == 0) _counts.Remove(old);
            }
            else _seen++;
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            _counts.TryGetValue(bar.Close, out int frequency);
            _counts[bar.Close] = frequency + 1;
            if (_counts.Count == 1) return 1;
            double sum = 0, correction = 0;
            foreach (int count in _counts.Values)
            {
                if (_termCounts[count] != _seen)
                {
                    // Preserve each binary64 operation, including the original
                    // correction for rounded 1+x near a constant window.
                    double x = (double)(_seen - count) / count;
                    double rounded = 1 + x;
                    double logarithm = Math.Log(rounded) * (x / (rounded - 1));
                    _terms[count] = (double)count / _seen * logarithm;
                    _termCounts[count] = _seen;
                }
                double adjusted = _terms[count] - correction;
                double next = sum + adjusted;
                correction = (next - sum) - adjusted;
                sum = next;
            }
            int distinct = _counts.Count;
            if (_logarithms[distinct] == 0) _logarithms[distinct] = Math.Log(distinct);
            return sum / _logarithms[distinct];
        }
    }
}
#endif
