using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Errors relative to the published, once-rounded mean. The signed relative
// errors remain rational until the final average, so opposite large terms cancel.
internal sealed class RoundedMeanErrorWindow(int period)
{
    private readonly Queue<BigInteger> _values = new();
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
            var old = _values.Dequeue();
            _sum -= old;
            _squares -= old * old;
        }
        _values.Enqueue(units);
        _sum += units;
        _squares += units * units;
    }

    internal double AbsoluteError(double mean)
    {
        var center = ExactVarianceWindow.Units(mean);
        var sum = BigInteger.Zero;
        foreach (var value in _values)
            sum += BigInteger.Abs(value - center);
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
        var center = ExactVarianceWindow.Units(mean);
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        foreach (var value in _values)
        {
            if (value.IsZero)
                return (false, 0);
            var top = BigInteger.Abs(value - center) * value.Sign;
            var bottom = BigInteger.Abs(value);
            var reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(top), bottom);
            top /= reduce;
            bottom /= reduce;
            var common = BigInteger.GreatestCommonDivisor(denominator, bottom);
            numerator = numerator * (bottom / common) + top * (denominator / common);
            denominator = denominator / common * bottom;
            reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            numerator /= reduce;
            denominator /= reduce;
        }
        return (true, ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator * Count));
    }
}
