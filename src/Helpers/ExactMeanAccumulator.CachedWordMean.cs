#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal partial struct ExactMeanAccumulator
{
    internal readonly struct WordMeanDivisor
    {
        internal int Count { get; }
        internal int Bits { get; }
        private readonly ulong _reciprocal;

        internal WordMeanDivisor(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
            Bits = BitLength((ulong)count);
            _reciprocal = count == 1 ? 0 : (ulong)(((UInt128)1 << 64) / (ulong)count);
        }

        internal ulong Divide(ulong numerator)
        {
            if (Count <= 1) return Count == 1 ? numerator : throw new DivideByZeroException();
            ulong denominator = (ulong)Count;
            ulong quotient = Math.BigMul(numerator, _reciprocal, out _);
            // floor(2^64/d) estimates floor(n/d) at most one low. Thus
            // q*d <= n, and one correction recovers the exact quotient.
            ulong remainder = numerator - quotient * denominator;
            return quotient + (remainder >= denominator ? 1UL : 0UL);
        }
    }

    // Only the private captured A/D mean opts in. The original Mean method
    // and every existing consumer retain their original implementation.
    internal double MeanCachedWord(in WordMeanDivisor divisor)
    {
        int count = divisor.Count;
        if (count <= 0 || _wide || IsZero) return Mean(count);
        ulong magnitude = _small < 0 ? unchecked((ulong)(~_small)) + 1 : (ulong)_small;
        int bits = BitLength(magnitude);
        // Keep hardware-double division, fractional grids and extended scales
        // on the original path, including its signed-zero conventions.
        if (bits <= 53 || _scale < 0 || _scale > 2098) return Mean(count);
        ulong denominator = (ulong)count;
        // bits >= 54 and an Int32 count has <=31 bits, so exponent >=23.
        int exponent = bits - divisor.Bits;
        if (magnitude < (denominator << exponent)) exponent--;
        int grid = Math.Max(0, exponent + _scale - 52);
        int shift = grid - _scale;
        ulong numerator = magnitude;
        if (shift < 0)
        {
            if (bits - shift > 64) return Mean(count);
            numerator <<= -shift;
        }
        else
        {
            if (divisor.Bits + shift > 64) return Mean(count);
            denominator <<= shift;
        }
        ulong quotient = divisor.Divide(numerator);
        // floor(floor(n/d)/2^s) = floor(n/(d*2^s)). Recover the remainder
        // against the aligned denominator; q*denominator <= numerator.
        if (shift > 0) quotient >>= shift;
        ulong remainder = numerator - quotient * denominator;
        ulong complement = denominator - remainder;
        if (remainder > complement || remainder == complement && (quotient & 1) != 0) quotient++;
        return Encode(quotient, grid, _small < 0);
    }
}
#endif
