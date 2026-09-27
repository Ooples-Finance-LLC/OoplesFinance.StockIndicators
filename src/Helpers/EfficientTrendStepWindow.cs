using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EfficientTrendStepWindow : IDisposable
{
    private readonly RoundedKaufmanWindow _efficiency;
    private readonly DoubledPopulationWindow _fast, _slow;
    private double _previous;
    private bool _hasPrevious;
    internal EfficientTrendStepWindow(int length, int fastLength, int slowLength)
    { _efficiency = new(Math.Max(1, length)); _fast = new(fastLength); _slow = new(slowLength); }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var efficiency = ExactVarianceWindow.Units(_efficiency.Next(close, commit).Efficiency); var fast = _fast.Next(close, commit); var slow = _slow.Next(close, commit);
        var deviation = RocBankValue.RoundUnits((slow << 1074) + efficiency * (fast - slow), BigInteger.One << 1074);
        var price = ExactVarianceWindow.Units(close); var previous = ExactVarianceWindow.Units(_hasPrevious ? _previous : close);
        var center = price > previous + deviation || price < previous - deviation ? price : previous;
        var middle = ExactMeanAccumulator.UnitRatio(center, BigInteger.One);
        if (commit) { _previous = middle; _hasPrevious = true; }
        return (ExactMeanAccumulator.UnitRatio(center + deviation, BigInteger.One), middle, ExactMeanAccumulator.UnitRatio(center - deviation, BigInteger.One));
    }
    internal void Reset() { _efficiency.Reset(); _fast.Reset(); _slow.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() { _efficiency.Dispose(); _fast.Dispose(); _slow.Dispose(); }
    // Population deviation of twice the price: scale exact moments before rounding
    // the root, so both overflowing doubled prices and subnormal widths survive.
    private sealed class DoubledPopulationWindow : IDisposable
    {
        private readonly PooledRingBuffer<double> _prices;
        private BigInteger _sum, _squares;
        internal DoubledPopulationWindow(int length) { _prices = new(Math.Max(1, length)); }
        internal BigInteger Next(double close, bool commit)
        {
            var value = ExactVarianceWindow.Units(close); var expired = _prices.Count == _prices.Capacity ? ExactVarianceWindow.Units(_prices[0]) : BigInteger.Zero; var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired; var count = new BigInteger(_prices.Capacity); var result = BigInteger.Zero;
            if (_prices.Count >= _prices.Capacity - 1)
            {
                var numerator = 4 * (count * squares - sum * sum); var denominator = count * count; var root = ExactPopulationDeviation.RootRatio(numerator, denominator);
                result = double.IsInfinity(root) ? ExactVarianceWindow.Units(ExactPopulationDeviation.RootRatio(numerator, denominator << 2048)) << 1024 : ExactVarianceWindow.Units(root);
            }
            if (commit) { _sum = sum; _squares = squares; _prices.TryAdd(close, out _); }
            return result;
        }
        internal void Reset() { _prices.Clear(); _sum = _squares = default; }
        public void Dispose() => _prices.Dispose();
    }
}
