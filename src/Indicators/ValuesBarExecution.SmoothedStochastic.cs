#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int SmoothedStochasticWindow(IIndicator indicator)
    {
        if (indicator is not WindowStochasticKdj { WilderSmoothing: false } owner) return 0;
        long window = (long)owner.Period + owner.KPeriod + owner.DPeriod - 2;
        return window <= int.MaxValue ? (int)window : 0;
    }

    private sealed class SmoothedStochasticValueState : IMultiOutputState
    {
        private readonly FiftySeedStochastic.RawState _raw;
        private readonly CompactStochasticAverage _k, _d;
        private readonly double _kFactor, _dFactor;

        internal SmoothedStochasticValueState(WindowStochasticKdj owner, int count)
        {
            _raw = new(owner.Period, 0);
            _k = new(owner.KPeriod, count); _d = new(owner.DPeriod, count);
            _kFactor = owner.KFactor; _dFactor = owner.DFactor;
        }

        public void Reset() { _raw.Reset(); _k.Reset(); _d.Reset(); }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var raw = _raw.Next(bar);
            if (!_raw.IsReady) return;
            var value = JurikDyadic.FromDouble(raw.Mantissa);
            if (raw.UpperShift != 0) value.ScaleByPowerOfTwo(raw.UpperShift);
            var k = _k.Next(value);
            if (!k.HasValue) return;
            outputs[0] = k.Value.Mean(1); outputs[3] = 1;
            var d = _d.Next(k.Value);
            if (!d.HasValue) return;
            outputs[1] = d.Value.Mean(1); outputs[4] = 1;
            if (k.Value.TryDouble(out double kp) && d.Value.TryDouble(out double dp)
                && HilbertRoundedKernel.TryProductSum(kp, _kFactor, -dp, _dFactor, out double j))
                outputs[2] = j;
            else
            {
                var combined = k.Value; combined.Multiply(_kFactor);
                var subtract = d.Value; subtract.Multiply(_dFactor);
                combined.Subtract(subtract);
                outputs[2] = combined.Mean(1);
            }
            outputs[5] = 1;
        }
    }

    private sealed class CompactStochasticAverage(int period, int count)
    {
        private readonly JurikDyadic[] _window = new JurikDyadic[Math.Min(period, Math.Max(1, count))];
        private JurikDyadic _sum;
        private int _seen, _position;

        internal void Reset() { Array.Clear(_window); _sum = default; _seen = _position = 0; }

        internal JurikDyadic? Next(JurikDyadic value)
        {
            // Retain each rounded sample's exact compact parts for insertion
            // and later eviction; wide values keep their existing fallback.
            if (period > 1 && value.TryBinaryParts(out var integer, out var exponent))
                value = JurikDyadic.FromBinaryProduct(integer, exponent);
            if (_seen == _window.Length) _sum.Subtract(_window[_position]);
            else _seen++;
            _window[_position] = value;
            if (++_position == _window.Length) _position = 0;
            _sum.AddExact(value);
            // The existing dyadic publisher retains every stage's original
            // subnormal rounding and unpublished extended upper exponent.
            return _seen == period ? _sum.Rounded(period) : null;
        }
    }

    private struct SmoothedStochasticKernel(SmoothedStochasticValueState state, BoundedOutput target) : IBoundedKernel<SmoothedStochasticKernel>
    {
        private int _index = target.Origin;
        public static SmoothedStochasticKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new((WindowStochasticKdj)indicator, count), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[6];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }
}
#endif
