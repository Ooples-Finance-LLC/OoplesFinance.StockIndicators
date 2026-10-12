#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal struct TriangularValueState(int period, int count) : IIndicatorState
    {
        private readonly int _firstLength = (int)((period + 1L) / 2);
        private readonly int _secondLength = period - (int)((period + 1L) / 2) + 1;
        private readonly long _mass = ((period + 1L) / 2) * (period - (period + 1L) / 2 + 1);
        private Core.SmaCpuKernel.Certificate _proof = new((int)Math.Min(int.MaxValue,
            ((period + 1L) / 2) * (period - (period + 1L) / 2 + 1)));
        private double[]? _prices, _sums;
        private int _position, _sumPosition, _seen, _sumCount;
        private double _first, _second;
        private TriangularWindowAverage.State? _fallback;

        public void Reset()
        {
            _position = _sumPosition = _seen = _sumCount = 0;
            _first = _second = 0;
            _fallback = null;
            _proof = new((int)Math.Min(int.MaxValue, _mass));
        }

        public double Update(in Bar bar)
        {
            // No completed window can be published by this worker.
            if (period > count) return 0;
            if (period == 1) return bar.Close == 0 ? 0 : bar.Close;
            if (_fallback is not null) return _fallback.Update(in bar);
            if (_mass > int.MaxValue || !_proof.Include(bar.Close))
            {
                _fallback = new(period);
                // Retain raw prices so rejection never seeds from a rounded mean.
                for (int j = 0; j < _seen; j++)
                {
                    int slot = _seen < period ? j : (_position + j) % period;
                    var prior = new Bar(default, 0, 0, 0, _prices![slot], 0);
                    _fallback.Update(in prior);
                }
                return _fallback.Update(in bar);
            }
            _prices ??= new double[period];
            _sums ??= new double[_secondLength];
            // Evict before adding: the certificate bounds every intermediate
            // by the convolution's coefficient mass, including startup.
            if (_seen >= _firstLength)
            {
                int expired = _position - _firstLength;
                if (expired < 0) expired += period;
                _first -= _prices[expired];
            }
            _first += bar.Close;
            _prices[_position] = bar.Close;
            if (++_position == period) _position = 0;
            if (_seen < period) _seen++;
            if (_seen < _firstLength) return 0;
            if (_sumCount == _secondLength) _second -= _sums[_sumPosition];
            else _sumCount++;
            _sums[_sumPosition] = _first;
            if (++_sumPosition == _secondLength) _sumPosition = 0;
            _second += _first;
            return _sumCount < _secondLength || _second == 0 ? 0 : _second / _mass;
        }
    }
}
#endif
