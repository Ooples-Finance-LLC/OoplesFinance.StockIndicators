using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CalmarWindow : IDisposable
{
    private readonly int _length, _peakLength;
    private readonly Queue<double> _prices = new();
    private readonly Queue<Drawdown> _drawdowns = new();
    private double _lastPrice, _previousResult;
    internal CalmarWindow(int length) { _length = Math.Max(1, length); _peakLength = Math.Max(2, _length); }
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var previous = _length == 1 && _prices.Count != 0 ? _lastPrice : _prices.Count >= _length ? _prices.Peek() : 0;
        var peak = price; var skip = _prices.Count == _peakLength;
        foreach (var retained in _prices) { if (skip) { skip = false; continue; } peak = Math.Max(peak, retained); }
        var high = ExactVarianceWindow.Units(peak); var current = new Drawdown(ExactVarianceWindow.Units(price) - high, high);
        var minimum = current; skip = _drawdowns.Count == _length;
        foreach (var retained in _drawdowns) { if (skip) { skip = false; continue; } if (retained.CompareTo(minimum) < 0) minimum = retained; }
        var value = Value(price, previous, minimum, _length);
        var signal = value > 2 && value > _previousResult ? Signal.StrongBuy : value < 2 && value < _previousResult ? Signal.StrongSell : value > 2 ? Signal.Buy : value < 2 ? Signal.Sell : Signal.None;
        if (commit) { if (_prices.Count == _peakLength) _prices.Dequeue(); _prices.Enqueue(price); if (_drawdowns.Count == _length) _drawdowns.Dequeue(); _drawdowns.Enqueue(current); _lastPrice = price; _previousResult = value; }
        return (value, signal);
    }
    private readonly struct Drawdown
    {
        internal readonly BigInteger Numerator, Denominator;
        internal Drawdown(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) { Numerator = BigInteger.Zero; Denominator = BigInteger.One; return; }
            var common = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), BigInteger.Abs(denominator));
            Numerator = numerator * denominator.Sign / common; Denominator = BigInteger.Abs(denominator) / common;
        }
        internal int CompareTo(Drawdown other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    }
    private static double Value(double current, double previous, Drawdown minimum, int length)
    {
        if (previous == 0 || minimum.Numerator.IsZero || current != 0 && Math.Sign(current) != Math.Sign(previous)) return 0;
        var depth = BigInteger.Abs(minimum.Numerator); var depthDenominator = minimum.Denominator;
        if (current == 0) return ExactMeanAccumulator.UnitRatio(-(depthDenominator << 1074), depth);
        var numerator = BigInteger.Abs(ExactVarianceWindow.Units(current)); var denominator = BigInteger.Abs(ExactVarianceWindow.Units(previous));
        var sign = Math.Sign(numerator.CompareTo(denominator)); if (sign == 0) return 0;
        var common = BigInteger.GreatestCommonDivisor(numerator, denominator); numerator /= common; denominator /= common;
        var divisor = (int)BigInteger.GreatestCommonDivisor(24, length); var degree = length / divisor;
        var poweredNumerator = BigInteger.Pow(numerator, 24 / divisor); var poweredDenominator = BigInteger.Pow(denominator, 24 / divisor);
        // Compare the final magnitude directly: root(ratio) ? 1 + sign * candidate * drawdown.
        // Neither an overflowing annual return nor a rounded-to-one power is published first.
        int Compare(BigInteger units, int unitDivisor = 1)
        {
            var baseDenominator = (depthDenominator * unitDivisor) << 1074;
            var baseNumerator = baseDenominator + sign * units * depth;
            if (baseNumerator.Sign < 0) return sign;
            var reduction = BigInteger.GreatestCommonDivisor(baseNumerator, baseDenominator); baseNumerator /= reduction; baseDenominator /= reduction;
            return sign * (poweredNumerator * BigInteger.Pow(baseDenominator, degree)).CompareTo(poweredDenominator * BigInteger.Pow(baseNumerator, degree));
        }
        const long maxBits = 0x7fefffffffffffff;
        static double Number(long bits) => BitConverter.Int64BitsToDouble(bits);
        int At(long bits) => Compare(ExactVarianceWindow.Units(Number(bits)));
        // A floating estimate only locates a small interval; integer comparisons certify every endpoint.
        var logarithm = StableLogRatio.OfSameSign(current, previous) * (24d / length); double estimate;
        if (logarithm > 1) estimate = Math.Exp(logarithm + Math.Log(1 - Math.Exp(-logarithm)) - (BigInteger.Log(depth) - BigInteger.Log(depthDenominator)));
        else
        {
            var annual = Math.Abs(logarithm) < 1e-5 ? logarithm * (1 + logarithm * (.5 + logarithm * (1d / 6 + logarithm * (1d / 24 + logarithm / 120)))) : Math.Exp(logarithm) - 1;
            estimate = ExactMeanAccumulator.UnitRatio(ExactVarianceWindow.Units(Math.Abs(annual)) * depthDenominator, depth);
        }
        var guess = double.IsNaN(estimate) ? 0 : double.IsInfinity(estimate) ? maxBits : BitConverter.DoubleToInt64Bits(estimate);
        var comparison = At(guess); if (comparison == 0) return sign * Number(guess);
        long low = guess, high = guess, step = 1;
        if (comparison > 0)
        {
            while (high < maxBits)
            {
                var next = step >= maxBits - high ? maxBits : high + step; var relation = At(next);
                if (relation == 0) return sign * Number(next);
                if (relation < 0) { high = next; break; }
                low = high = next; step = step > maxBits / 16 ? maxBits : step * 16;
            }
        }
        else
        {
            while (low > 0)
            {
                var next = step >= low ? 0 : low - step; var relation = At(next);
                if (relation == 0) return sign * Number(next);
                if (relation > 0) { low = next; break; }
                low = high = next; step = step > maxBits / 16 ? maxBits : step * 16;
            }
        }
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2; var relation = At(middle);
            if (relation == 0) return sign * Number(middle);
            if (relation > 0) low = middle; else high = middle;
        }
        var lower = ExactVarianceWindow.Units(Number(low)); var upper = low == maxBits ? BigInteger.One << 2098 : ExactVarianceWindow.Units(Number(low + 1));
        var midpoint = Compare(lower + upper, 2); var rounded = midpoint > 0 || midpoint == 0 && (low & 1) != 0 ? low + 1 : low;
        return sign * Number(rounded);
    }
    internal void Reset() { _prices.Clear(); _drawdowns.Clear(); _lastPrice = _previousResult = 0; }
    public void Dispose() => Reset();
}
