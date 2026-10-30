#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class DeviationAverageValueState : IIndicatorState
    {
        private readonly int _period;
        private readonly double _scale;
        private readonly JurikDyadic _c1, _c2, _c3;
        private readonly Square[] _window;
        private JurikDyadic _average, _zero, _filter, _olderFilter;
        private UInt128 _sum;
        private BigInteger _wideSum;
        private bool _wide;
        private int _position, _seen, _grid;
        private bool _seeded;
        private readonly record struct Square(UInt128 Integer, int Exponent);

        internal DeviationAverageValueState(int period, double scale, int count)
        {
            _period = period; _scale = scale;
            _window = new Square[Math.Min(period, Math.Max(1, count - 1))];
            double a = Math.Exp(-1.414 * Math.PI / (.5 * period));
            double b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
            double c = -a * a;
            // Decode the unchanged binary64 coefficients once per state. The existing
            // exact multiply overload consumes these immutable compact operands.
            static JurikDyadic Coefficient(double value)
            {
                var (integer, exponent) = ExactMeanAccumulator.DecomposeFinite(value);
                return JurikDyadic.FromBinaryProduct(integer, exponent);
            }
            _c1 = Coefficient(1 - b - c); _c2 = Coefficient(b); _c3 = Coefficient(c);
        }

        public void Reset()
        {
            _average = _zero = _filter = _olderFilter = default;
            _sum = default; _wideSum = default; _wide = false;
            _position = _seen = _grid = 0; _seeded = false;
        }

        public double Update(in Bar bar)
        {
            var price = JurikDyadic.FromDouble(bar.Close);
            if (!_seeded)
            {
                _seeded = true; _average = price;
                return bar.Close; // Preserve the original seed's signed zero.
            }
            var zero = price; zero.Subtract(_average); zero = zero.Rounded();
            var filter = zero; filter.AddExact(_zero); filter.Multiply(_c1);
            filter.ScaleByPowerOfTwo(-1);
            var term = _filter; term.Multiply(_c2); filter.AddExact(term);
            term = _olderFilter; term.Multiply(_c3); filter.AddExact(term);
            filter = filter.Rounded(); // One rounding after all three terms.

            Square square = FilterSquare(in filter);
            if (_seen == _window.Length)
            {
                var expired = _window[_position];
                if (expired.Integer != 0) Remove(expired);
            }
            else _seen++;
            _window[_position] = square;
            if (++_position == _window.Length) _position = 0;
            if (square.Integer != 0) Add(square);
            double normalized = Normalize(square);
            double gain = Math.Max(.1, Math.Min(1, _scale * normalized * 5 / _period));
            if (JurikDyadic.TryBlend(_average, price, gain, out var blended)) _average = blended;
            else
            {
                price.Subtract(_average); price.Multiply(gain); price.AddExact(_average);
                _average = price.Rounded();
            }
            _zero = zero; _olderFilter = _filter; _filter = filter;
            return _average.Mean(1);
        }

        private void Remove(Square expired)
        {
            int shift = expired.Exponent - _grid;
            if (!_wide) { _sum -= expired.Integer << shift; return; }
            _wideSum -= (BigInteger)expired.Integer << shift;
            if (_wideSum <= (BigInteger)UInt128.MaxValue)
            {
                _sum = (UInt128)_wideSum; _wideSum = default; _wide = false;
            }
        }

        private void Add(Square square)
        {
            if (!_wide && _sum == 0) { _sum = square.Integer; _grid = square.Exponent; return; }
            int grid = Math.Min(_grid, square.Exponent);
            int sumShift = _grid - grid, termShift = square.Exponent - grid;
            if (!_wide && sumShift < 128 && termShift < 128
                && _sum <= UInt128.MaxValue >> sumShift
                && square.Integer <= UInt128.MaxValue >> termShift)
            {
                UInt128 sum = _sum << sumShift, term = square.Integer << termShift;
                if (sum <= UInt128.MaxValue - term) { _sum = sum + term; _grid = grid; return; }
            }
            _wideSum = ((_wide ? _wideSum : (BigInteger)_sum) << sumShift)
                + ((BigInteger)square.Integer << termShift);
            _wide = true; _grid = grid;
        }

        private double Normalize(Square square)
        {
            if (square.Integer == 0) return 0;
            int power = square.Exponent - _grid;
            if (!_wide && square.Integer <= UInt128.MaxValue / (uint)_period
                && TryDeviationRoot(square.Integer * (uint)_period, _sum, power, out double value)) return value;
            return ExactPopulationDeviation.ScaledRootRatio((BigInteger)square.Integer * _period,
                _wide ? _wideSum : (BigInteger)_sum, power);
        }

        private static Square FilterSquare(in JurikDyadic filter)
        {
            long integer;
            int exponent;
            if (filter.TryDouble(out double value))
            {
                (integer, exponent) = ExactMeanAccumulator.DecomposeFinite(value);
                if (integer == 0) return default;
            }
            else
            {
                // Rounded unpublished filters may exceed binary64 range. Keep
                // their 53-bit significand and extended exponent before squaring
                // and normalizing; never turn an intermediate overflow into Inf.
                BigInteger units = filter.RoundedUnits();
                if (units.IsZero) return default;
                int zeros = ExactMeanAccumulator.TrailingBinaryZeros(units);
                integer = (long)(units >> zeros); exponent = zeros - 1074;
            }
            ulong magnitude = (ulong)Math.Abs(integer); // A rounded significand has at most 53 bits.
            return new((UInt128)magnitude * magnitude, 2 * exponent);
        }
    }

    // Certify sqrt(numerator/denominator) at both exact binary64 midpoints,
    // then apply an even power of two only when the result remains normal.
    // Four 64-bit words hold denominator * midpoint^2 without allocation.
    internal static bool TryDeviationRoot(UInt128 numerator, UInt128 denominator, int power, out double result)
    {
        result = 0;
        if (numerator == 0 || denominator == 0 || (power & 1) != 0) return false;
        long bits = BitConverter.DoubleToInt64Bits(Math.Sqrt((double)numerator / (double)denominator));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int exponent = (int)(bits >> 52) - 1023;
            ulong significand = (ulong)(bits & 0xfffffffffffffL) | (1UL << 52);
            bool boundary = significand == 1UL << 52;
            ulong lower = boundary ? (significand << 2) - 1 : (significand << 1) - 1;
            if (!CompareDeviationMidpoint(numerator, denominator, lower, exponent - (boundary ? 54 : 53), out int comparison)) return false;
            if (comparison < 0 || comparison == 0 && (bits & 1) != 0) { bits--; continue; }
            if (!CompareDeviationMidpoint(numerator, denominator, (significand << 1) + 1, exponent - 53, out comparison)) return false;
            if (comparison > 0 || comparison == 0 && (bits & 1) != 0) { bits++; continue; }
            long scaledExponent = (long)exponent + power / 2;
            if (scaledExponent is < -1022 or > 1023) return false;
            result = BitConverter.Int64BitsToDouble(bits + ((long)(power / 2) << 52));
            return true;
        }
        return false;
    }

    private static bool CompareDeviationMidpoint(UInt128 numerator, UInt128 denominator, ulong midpoint,
        int power, out int comparison)
    {
        comparison = 0;
        int shift = -2 * power;
        if (shift is <= -128 or >= 256 || shift >= 128 && numerator > UInt128.MaxValue >> (shift - 128)) return false;
        UInt128 leftLow = shift >= 128 ? 0 : shift <= 0 ? numerator : numerator << shift;
        UInt128 leftHigh = shift >= 128 ? numerator << (shift - 128) : shift <= 0 ? 0 : numerator >> (128 - shift);
        UInt128 square = (UInt128)midpoint * midpoint;
        // Four 64x64 partial products. The middle carry needs at most 66 bits;
        // midpoint < 2^55 bounds the complete product below 2^238.
        ulong squareLow = unchecked((ulong)square), denominatorLow = unchecked((ulong)denominator);
        UInt128 p00 = (UInt128)squareLow * denominatorLow;
        UInt128 p01 = (UInt128)squareLow * (ulong)(denominator >> 64);
        UInt128 p10 = (square >> 64) * denominatorLow;
        UInt128 p11 = (square >> 64) * (ulong)(denominator >> 64);
        UInt128 middle = (p00 >> 64) + unchecked((ulong)p01) + (UInt128)unchecked((ulong)p10);
        UInt128 rightLow = unchecked((ulong)p00) | (middle << 64);
        UInt128 rightHigh = p11 + (p01 >> 64) + (p10 >> 64) + (middle >> 64);
        if (shift < 0)
        {
            int rightShift = -shift;
            if (rightHigh > UInt128.MaxValue >> rightShift) return false;
            rightHigh = (rightHigh << rightShift) | (rightLow >> (128 - rightShift));
            rightLow <<= rightShift;
        }
        comparison = leftHigh.CompareTo(rightHigh);
        if (comparison == 0) comparison = leftLow.CompareTo(rightLow);
        return true;
    }
}
#endif
