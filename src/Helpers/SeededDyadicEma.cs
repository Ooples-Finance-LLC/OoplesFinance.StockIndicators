namespace OoplesFinance.StockIndicators.Helpers;

// Shared exact, SMA-seeded EMA arithmetic for competitor-convention states.
internal static class SeededDyadicEma
{
    // Preserve binary64 significands above its exponent range. Scaling only occurs
    // after overflow; the first finite quotient is normal, so restoration adds no rounding.
    internal static JurikDyadic RoundedRatio(JurikDyadic numerator, JurikDyadic denominator)
    {
        int shift = 0;
        while (true)
        {
            double value = numerator.Ratio(denominator);
            if (FrameworkCompatibility.IsFinite(value))
            {
                var result = JurikDyadic.FromDouble(value);
                result.ScaleByPowerOfTwo(shift); return result;
            }
            numerator.ScaleByPowerOfTwo(-32); shift += 32;
        }
    }

    internal sealed class State(int period)
    {
        private int _count;
        private JurikDyadic _sum, _value;
        internal void Reset() { _count = 0; _sum = _value = default; }
        internal bool Add(JurikDyadic input, out JurikDyadic value)
        {
            if (_count < period)
            {
                _sum.AddExact(input); _count++;
                if (_count < period) { value = default; return false; }
                _value = RoundedRatio(_sum, JurikDyadic.FromDouble(period));
                _sum = default;
            }
            else
            {
                var next = _value; next.Multiply(period - 1);
                input.Multiply(2); next.AddExact(input);
                _value = RoundedRatio(next, JurikDyadic.FromDouble((long)period + 1));
            }
            value = _value; return true;
        }
    }

}
