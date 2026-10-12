#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeTriangularColumn(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail, CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int count = seed.Length + end - start;
        var rented = System.Buffers.ArrayPool<double>.Shared.Rent(count);
        try
        {
            var prices = rented.AsSpan(0, count);
            for (int i = 0; i < seed.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                var bar = seed[i];
                if (!AllFieldsFinite(in bar))
                    return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
                prices[i] = bar.Close;
            }
            int tailStart = end - tail.Length;
            for (int i = start; i < end; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                var bar = i >= tailStart ? tail[i - tailStart] : source[i];
                region.Last = bar;
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
                prices[seed.Length + i - start] = bar.Close;
                if (owned is not null) owned[i] = bar;
            }
            int firstLength = (period + 1) / 2, secondLength = period - firstLength + 1;
            int mass = firstLength * secondLength;
            Core.SmaCpuKernel.Summarize(prices, out var grid, out _, cancellation);
            bool certified = grid.Certifies(mass);
            if (!certified && grid.CanRefine)
            {
                var proof = new Core.SmaCpuKernel.Certificate(mass);
                certified = true;
                foreach (double value in prices)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    if (!proof.Include(value)) { certified = false; break; }
                }
            }
            if (!certified)
            {
                var state = new TriangularWindowAverage.State(period);
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                    double value = state.Update(in bar);
                    if (i >= seed.Length)
                    {
                        int row = start + i - seed.Length;
                        output[row] = value;
                        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
                        {
                            region.InvalidOutputIndex = row;
                            region.InvalidOutputValue = value;
                        }
                    }
                }
                return region;
            }
            // Two box sums form the triangle. Direct column indexing removes
            // ring storage; the common-grid proof makes every sum exact.
            double first = 0, expired = 0, total = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (i >= firstLength) first -= prices[i - firstLength];
                first += prices[i];
                if (i == firstLength - 1) expired = first;
                if (i >= firstLength - 1)
                {
                    // Subtract first so intermediate coefficient mass never
                    // exceeds the certified full triangular denominator.
                    if (i >= period) total -= expired;
                    total += first;
                    if (i >= period)
                    {
                        expired -= prices[i - period];
                        expired += prices[i - secondLength + 1];
                    }
                }
                if (i >= seed.Length)
                    output[start + i - seed.Length] = i < period - 1 || total == 0 ? 0 : total / mass;
            }
            return region;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return region; }
        finally { System.Buffers.ArrayPool<double>.Shared.Return(rented); }
    }

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
