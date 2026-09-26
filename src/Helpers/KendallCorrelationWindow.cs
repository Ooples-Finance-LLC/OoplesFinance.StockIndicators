using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Kendall tau-a between prices and once-rounded partial-window OLS endpoints.
// Endpoints may exceed binary64's exponent range although tau remains bounded.
internal sealed class KendallCorrelationWindow : IDisposable
{
    private readonly ExactLinearFitWindow _fit;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<BigInteger> _fitted;
    internal KendallCorrelationWindow(int length)
    {
        length = Math.Max(1, length);
        _fit = new ExactLinearFitWindow(length);
        _prices = new PooledRingBuffer<double>(length); _fitted = new PooledRingBuffer<BigInteger>(length);
    }
    internal double Next(double price, bool commit)
    {
        var fitted = _fit.Next(price, commit).RoundedLastUnits;
        var count = Math.Min(_prices.Capacity, _prices.Count + 1);
        var padding = _prices.Capacity - count;
        long score = 0;
        for (var j = 0; j < count; j++)
        {
            var p = j == 0 ? price : _prices[_prices.Count - j];
            var f = j == 0 ? fitted : _fitted[_fitted.Count - j];
            score += (long)padding * Math.Sign(p) * f.Sign;
            for (var k = 0; k < j; k++)
            {
                var otherPrice = k == 0 ? price : _prices[_prices.Count - k];
                var otherFit = k == 0 ? fitted : _fitted[_fitted.Count - k];
                score += p.CompareTo(otherPrice) * f.CompareTo(otherFit);
            }
        }
        var pairs = (long)_prices.Capacity * (_prices.Capacity - 1) / 2;
        var result = pairs == 0 ? 0 : ExactMeanAccumulator.UnitRatio(new BigInteger(score) << 1074, new BigInteger(pairs));
        if (commit) { _prices.TryAdd(price, out _); _fitted.TryAdd(fitted, out _); }
        return result;
    }
    internal void Reset() { _fit.Reset(); _prices.Clear(); _fitted.Clear(); }
    public void Dispose() { _fit.Dispose(); _prices.Dispose(); _fitted.Dispose(); }
}
