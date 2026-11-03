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

    internal static bool TrySmoothedStochasticRawRatio(double high, double low, double close, out double result)
    {
        result = 0;
        const double minimum = 3.4395525670743494E-136; // 2^-450
        const double maximum = 2.9073548971824276E+135; // 2^450
        if (!HilbertRoundedKernel.TryExactSum(close, -low, out double numerator)
            || !HilbertRoundedKernel.TryExactSum(high, -low, out double denominator)
            || !(denominator >= minimum && denominator <= maximum)) return false;
        double product = 100 * numerator;
        if (!(Math.Abs(product) >= minimum && Math.Abs(product) <= maximum)) return false;
        double candidate = product / denominator;
        if (!(Math.Abs(candidate) >= minimum && Math.Abs(candidate) <= maximum)) return false;

        // The input differences are exact. FMA residuals enclose the exact
        // 100*numerator - product and product - candidate*denominator terms.
        // One adjacent value on each side includes even an inexact residual;
        // directed addition then encloses the complete rational residual.
        double productError = Math.FusedMultiplyAdd(100, numerator, -product);
        double divisionError = Math.FusedMultiplyAdd(-candidate, denominator, product);
        double lower = Math.BitDecrement(Math.BitDecrement(productError) + Math.BitDecrement(divisionError));
        double upper = Math.BitIncrement(Math.BitIncrement(productError) + Math.BitIncrement(divisionError));
        // All guarded products, residuals and scaled cells remain finite, and
        // the positive cell limits stay normal. Round the limits downward and
        // accept only strict containment; midpoint ties use the exact fallback.
        double below = Math.BitDecrement((candidate - Math.BitDecrement(candidate)) * .5 * denominator);
        double above = Math.BitDecrement((Math.BitIncrement(candidate) - candidate) * .5 * denominator);
        if (!(lower > -below && upper < above)) return false;
        result = candidate;
        return true;
    }

    private sealed class CompactStochasticRawState(int period)
    {
        private readonly WindowExtremeDeque _high = new(period, true), _low = new(period, false);
        private int _count;
        internal bool IsReady => _count >= period;
        internal void Reset() { _high.Reset(); _low.Reset(); _count = 0; }

        internal RocBankValue Next(in Bar bar)
        {
            _high.Add(bar.High); _low.Add(bar.Low);
            if (_count < period) _count++;
            if (_count < period || _high.Value == _low.Value) // NOSONAR: exactly flat ranges retain the existing zero convention.
                return default;
            if (TrySmoothedStochasticRawRatio(_high.Value, _low.Value, bar.Close, out double ordinary))
                return new RocBankValue(ordinary);
            // Preserve the unchanged public raw state's extended ratio path.
            var top = new ExactMeanAccumulator(); top.Add(bar.Close, 100); top.Add(_low.Value, -100);
            var bottom = new ExactMeanAccumulator(); bottom.Add(_high.Value); bottom.Add(_low.Value, -1);
            for (int shift = 0; ; shift += 1024)
            {
                var scaled = bottom; scaled.ScaleByPowerOfTwo(shift);
                double value = top.Ratio(scaled);
                if (double.IsFinite(value)) return new RocBankValue(value, shift);
            }
        }
    }

    private sealed class SmoothedStochasticValueState : IMultiOutputState
    {
        private readonly CompactStochasticRawState _raw;
        private readonly CompactStochasticAverage _k, _d;
        private readonly double _kFactor, _dFactor;

        internal SmoothedStochasticValueState(WindowStochasticKdj owner, int count)
        {
            _raw = new(owner.Period);
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
