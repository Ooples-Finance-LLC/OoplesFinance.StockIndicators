using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Errors relative to the published, once-rounded mean. The signed relative
// errors remain rational until the final average, so opposite large terms cancel.
internal sealed class RoundedMeanErrorWindow(int period)
{
    internal readonly struct Sample(double value, BigInteger units)
    {
        internal double Value { get; } = value;
        internal BigInteger Units { get; } = units;
    }
    private readonly Queue<Sample> _values = new();
    private BigInteger _sum,
        _squares;
    internal int Count => _values.Count;
    internal double Mean => Count == 0 ? 0 : ExactMeanAccumulator.UnitRatio(_sum, Count);

    internal void Reset()
    {
        _values.Clear();
        _sum = _squares = 0;
    }

    internal void Add(double value)
    {
        var units = ExactVarianceWindow.Units(value);
        if (Count == period)
        {
            var old = _values.Dequeue().Units;
            _sum -= old;
            _squares -= old * old;
        }
        _values.Enqueue(new Sample(value, units));
        _sum += units;
        _squares += units * units;
    }

    internal double AbsoluteError(double mean)
    {
        var center = ExactVarianceWindow.Units(mean);
        var sum = BigInteger.Zero;
        foreach (var sample in _values)
            sum += BigInteger.Abs(sample.Units - center);
        return ExactMeanAccumulator.UnitRatio(sum, Count);
    }

    internal double SquaredError(double mean)
    {
        var center = ExactVarianceWindow.Units(mean);
        return ExactMeanAccumulator.UnitRatio(
            _squares - 2 * center * _sum + Count * center * center,
            new BigInteger(Count) << 1074
        );
    }

    internal (bool Defined, double Value) SignedRelativeError(double mean)
    {
#if !NETFRAMEWORK
        if (CertifiedMeanRelativeError.TryCompute(_values, mean, out var certified))
            return (true, certified);
#endif
        var center = ExactVarianceWindow.Units(mean);
        var centerZeros = center.IsZero ? int.MaxValue : ExactMeanAccumulator.TrailingBinaryZeros(center);
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        foreach (var sample in _values)
        {
            var value = sample.Units;
            if (value.IsZero)
                return (false, 0);
            // Cancel the shared binary scale before constructing/reducing the
            // fraction. Ordinary prices then use significand-sized operands;
            // mixed exponents retain exactly the same rational value.
            var shift = Math.Min(centerZeros, ExactMeanAccumulator.TrailingBinaryZeros(value));
            var scaledValue = value >> shift;
            var top = BigInteger.Abs(scaledValue - (center >> shift)) * value.Sign;
            var bottom = BigInteger.Abs(scaledValue);
            var reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(top), bottom);
            top /= reduce;
            bottom /= reduce;
            var common = BigInteger.GreatestCommonDivisor(denominator, bottom);
            numerator = numerator * (bottom / common) + top * (denominator / common);
            denominator = denominator / common * bottom;
            // Both addends were reduced. Any factor left in the sum must
            // divide the original denominator GCD, not the growing product.
            reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), common);
            numerator /= reduce;
            denominator /= reduce;
        }
        return (true, ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator * Count));
    }
}
