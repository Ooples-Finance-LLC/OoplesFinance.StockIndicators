#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal static bool TryAccelerationInputs(double high, double low, out double upper, out double lower)
    {
        upper = high; lower = low;
        if (!double.IsFinite(high) || !double.IsFinite(low)) return false;
        if (high == -low) return true; // NOSONAR: S1244 - Only this exact singular identity selects the defined zero-denominator case.
        const double minimum = 6.223015277861142E-61, maximum = 1.6069380442589903E60; // 2^-200, 2^200.
        if (!(Math.Abs(high) >= minimum && Math.Abs(high) <= maximum
            && Math.Abs(low) >= minimum && Math.Abs(low) <= maximum)) return false;
        var (h, he) = ExactMeanAccumulator.DecomposeFinite(high);
        var (l, le) = ExactMeanAccumulator.DecomposeFinite(low);
        int grid = Math.Min(he, le), hs = he - grid, ls = le - grid;
        const ulong limit = (1UL << 60) - 1;
        if (hs >= 64 || ls >= 64 || (ulong)Math.Abs(h) > limit >> hs
            || (ulong)Math.Abs(l) > limit >> ls) return false;
        Int128 hi = (Int128)h << hs, lo = (Int128)l << ls;
        Int128 denominator = hi + lo;
        Int128 top = hi * (5 * hi - 3 * lo), bottom = lo * (5 * lo - 3 * hi);
        if (denominator < 0) { denominator = -denominator; top = -top; bottom = -bottom; }
        // Aligned magnitudes below 2^60 keep products below 2^123. The
        // input bounds keep these rounded ratios and their rescaling normal,
        // so exact power-of-two scaling introduces no second rounding.
        upper = Math.ScaleB(JurikDyadic.IntegerRatio(top, denominator), grid);
        lower = Math.ScaleB(JurikDyadic.IntegerRatio(bottom, denominator), grid);
        return true;
    }

    private sealed class AccelerationValueState(int period, int count) : IMultiOutputState
    {
        private readonly Row[] _window = new Row[Math.Min(period, Math.Max(1, count))];
        private JurikDyadic _upper, _middle, _lower;
        private int _seen, _position;
        private readonly record struct Row(JurikDyadic Upper, JurikDyadic Middle, JurikDyadic Lower);

        public void Reset()
        {
            Array.Clear(_window);
            _upper = _middle = _lower = default;
            _seen = _position = 0;
        }

        private static Row Transform(in Bar bar)
        {
            var middle = JurikDyadic.FromDouble(bar.Close);
            if (TryAccelerationInputs(bar.High, bar.Low, out double upper, out double lower))
                return new(JurikDyadic.FromDouble(upper), middle, JurikDyadic.FromDouble(lower));

            // Retain the public state's exact extended-exponent transform for
            // unsupported scales, cancellation and wide aligned operands.
            var h = ExactVarianceWindow.Units(bar.High);
            var l = ExactVarianceWindow.Units(bar.Low);
            var sum = h + l;
            if (sum.IsZero) return new(JurikDyadic.FromDouble(bar.High), middle, JurikDyadic.FromDouble(bar.Low));
            int shift = (int)BigInteger.TrailingZeroCount(h | l);
            h >>= shift; l >>= shift;
            var common = BigInteger.GreatestCommonDivisor(h, l);
            sum = (h + l) / common;
            var spread = 4 * ((h - l) / common);
            var divisor = BigInteger.Abs(sum);
            return new(JurikDyadic.FromUnits(RocBankValue.RoundScaledUnits(h * (sum + spread) * sum.Sign, divisor, shift)),
                middle, JurikDyadic.FromUnits(RocBankValue.RoundScaledUnits(l * (sum - spread) * sum.Sign, divisor, shift)));
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            Row row = Transform(in bar);
            if (_seen == _window.Length)
            {
                Row expired = _window[_position];
                _upper.Subtract(expired.Upper); _middle.Subtract(expired.Middle); _lower.Subtract(expired.Lower);
            }
            else _seen++;
            _window[_position] = row;
            if (++_position == _window.Length) _position = 0;
            _upper.AddExact(row.Upper); _middle.AddExact(row.Middle); _lower.AddExact(row.Lower);
            if (_seen != period) return;
            output[0] = _upper.Mean(period); output[1] = _middle.Mean(period); output[2] = _lower.Mean(period);
            output[3] = output[4] = output[5] = 1;
        }
    }

    private struct AccelerationKernel(AccelerationValueState state, BoundedOutput target) : IBoundedKernel<AccelerationKernel>
    {
        private int _index = target.Origin;
        public static AccelerationKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
            => new(new(period, count), target);

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
