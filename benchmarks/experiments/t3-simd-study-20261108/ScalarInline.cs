namespace T3Study;
internal static class BaselineInline
{
    internal readonly struct WordSmoother
    {
        private readonly ulong _denominator, _reciprocal;

        internal bool IsSupported => _denominator != 0;

        internal WordSmoother(int period)
        {
            _denominator = 0;
            _reciprocal = 0;
            if (period is < 1 or > 2047) return;
            _denominator = (ulong)period + 1;
            _reciprocal = (ulong)(((UInt128)1 << 64) / _denominator);
        }

        internal bool TrySmooth(double prior, double input, out double result)
        {
            result = 0;
            if (_denominator == 0) return false;
            ulong priorBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(prior));
            ulong inputBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(input));
            ulong exponent = priorBits >> 52;
            if (exponent is < 1 or > 2046 || inputBits >> 52 != exponent) return false;
            result = SmoothSameBinade(priorBits, inputBits, exponent);
            return true;
        }

        // Caller establishes a supported period and positive normal operands
        // with this same exponent. Convex means preserve the binade.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal double SmoothSameBinade(ulong priorBits, ulong inputBits, ulong exponent)
        {
            // n <= 2^64-2048, q*d <= n, and the corrected remainder < d.
            // The rounded significand remains below 2^53. These proven bounds
            // make arithmetic overflow checks redundant inside this kernel.
            unchecked
            {
                const ulong fractionMask = (1UL << 52) - 1;
                ulong a = (priorBits & fractionMask) | (1UL << 52);
                ulong b = (inputBits & fractionMask) | (1UL << 52);
                // d <= 2048 bounds n by 2^64 - 2048, including period 2047.
                ulong numerator = a * (_denominator - 2) + 2 * b;
                ulong quotient = Math.BigMul(numerator, _reciprocal, out _);
                ulong remainder = numerator - quotient * _denominator;
                if (remainder >= _denominator) { quotient++; remainder -= _denominator; }
                ulong complement = _denominator - remainder;
                if (remainder > complement || remainder == complement && (quotient & 1) != 0) quotient++;
                // Convex same-binade inputs keep the rounded significand in range.
                return BitConverter.Int64BitsToDouble((long)((exponent << 52) | (quotient & fractionMask)));
            }
        }
    }
}
