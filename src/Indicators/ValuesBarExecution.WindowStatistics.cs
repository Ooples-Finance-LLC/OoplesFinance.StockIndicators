#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedWindowStatisticsState : IIndicatorState, IDisposable
    {
        private readonly int _length, _kind;
        private readonly ExactSkewnessWindow? _skew;
        private readonly PooledRingBuffer<double>? _window;
        private readonly double[]? _sorted;
        internal GeneratedWindowStatisticsState(int length, int count, int kind)
        {
            _length = Math.Max(1, length); _kind = kind;
            // Preserve the formula-specific warmup without allocating unreachable history.
            if (_length > count || (kind == 1 && _length == count)) return;
            if (kind == 0) _skew = new(_length);
            else
            {
                _window = new(_length);
                if (kind >= 2) _sorted = new double[_length];
            }
        }
        public double Update(in Bar bar)
        {
            if (_kind == 0) return _skew?.Next(bar.Close, true) ?? 0;
            if (_window is null) return _kind == 1 ? 0 : bar.Close;
            double result;
            if (_kind == 1)
            {
                var below = 0;
                if (_window.Count >= _length)
                    for (var i = 0; i < _length; i++) if (_window[i] < bar.Close) below++;
                result = 100d * below / _length;
            }
            else if (_window.Count + 1 < _length) result = bar.Close;
            else
            {
                var start = _window.Count - (_length - 1);
                var sorted = _sorted!;
                for (var i = 0; i < _length - 1; i++) sorted[i] = _window[start + i];
                sorted[_length - 1] = bar.Close;
                Array.Sort(sorted);
                result = _length % 2 == 0 ? PriceMean.Of(sorted[_length / 2 - 1], sorted[_length / 2]) : sorted[_length / 2];
            }
            _window.TryAdd(bar.Close, out _);
            return result;
        }
        public void Reset() { _skew?.Reset(); _window?.Clear(); }
        public void Dispose() { _skew?.Dispose(); _window?.Dispose(); }
    }
}
#endif
