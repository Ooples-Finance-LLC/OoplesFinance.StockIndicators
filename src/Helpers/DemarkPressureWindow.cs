using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Products and V2 range-weighted volume stay exact until the bounded percentage
// is published. The rational representation is confined to these pressure windows.
internal sealed class DemarkPressureWindow : IDisposable
{
    private readonly bool _second;
    private readonly PooledRingBuffer<(Weight Buy, Weight Sell)> _window;
    private Weight _buy, _sell;
    private double _previousClose;
    internal DemarkPressureWindow(int length, bool second)
    { _window = new PooledRingBuffer<(Weight, Weight)>(Math.Max(1, length)); _second = second; }

    private readonly struct Weight
    {
        internal readonly BigInteger Numerator;
        private readonly BigInteger _denominator;
        internal BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        internal Weight(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / divisor; _denominator = denominator / divisor;
        }
        internal Weight Abs() => new(BigInteger.Abs(Numerator), Denominator);
        public static Weight operator +(Weight a, Weight b)
        {
            var common = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
            var left = a.Denominator / common; var right = b.Denominator / common;
            return new Weight(a.Numerator * right + b.Numerator * left, a.Denominator * right);
        }
        public static Weight operator -(Weight a, Weight b) => a + new Weight(-b.Numerator, b.Denominator);
    }
    // A strict decimal 15% threshold, including negative-price denominators.
    private static bool Gap(BigInteger difference, BigInteger denominator) => !denominator.IsZero &&
        (20 * difference).CompareTo(3 * denominator) * denominator.Sign > 0;

    internal double Next(double open, double high, double low, double close, double volume, bool commit)
    {
        var o = ExactVarianceWindow.Units(open); var h = ExactVarianceWindow.Units(high);
        var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close);
        var v = ExactVarianceWindow.Units(volume); var previous = ExactVarianceWindow.Units(_previousClose);
        var delta = c - o; Weight buy = default, sell = default;
        if (_second)
        {
            var range = h - l;
            var pressure = range.IsZero ? default : new Weight(delta * v, range);
            if (delta.Sign > 0) buy = pressure;
            if (delta.Sign < 0) sell = pressure;
        }
        else
        {
            buy = new Weight((Gap(o - previous, previous) ? h - previous + c - l : BigInteger.Max(BigInteger.Zero, delta)) * v, BigInteger.One);
            sell = new Weight((Gap(previous - o, o) ? -(previous - l + h - c) : BigInteger.Min(BigInteger.Zero, delta)) * v, BigInteger.One);
        }
        var buyers = _buy + buy; var sellers = _sell + sell;
        if (_window.Count == _window.Capacity) { buyers -= _window[0].Buy; sellers -= _window[0].Sell; }
        var denominator = _second ? buyers + sellers.Abs() : buyers - sellers;
        double value;
        if (denominator.Numerator.IsZero) value = _second ? 50 : 0;
        else
        {
            var numerator = 100 * buyers.Numerator * denominator.Denominator;
            var divisor = buyers.Denominator * denominator.Numerator;
            if (divisor.Sign < 0) { numerator = -numerator; divisor = -divisor; }
            value = numerator.Sign <= 0 ? 0 : numerator >= 100 * divisor ? 100 : ExactMeanAccumulator.UnitRatio(numerator << 1074, divisor);
        }
        if (commit) { _window.TryAdd((buy, sell), out _); _buy = buyers; _sell = sellers; _previousClose = close; }
        return value;
    }
    internal void Reset() { _window.Clear(); _buy = _sell = default; _previousClose = 0; }
    public void Dispose() => _window.Dispose();
}
