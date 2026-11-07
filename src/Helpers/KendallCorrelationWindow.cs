using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Kendall tau-a between prices and once-rounded partial-window OLS endpoints.
// Endpoints may exceed binary64's exponent range although tau remains bounded.
internal sealed class KendallCorrelationWindow : IDisposable
{
    private readonly ExactLinearFitWindow _fit;
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<BigInteger> _fitted;
    internal KendallCorrelationWindow(int length) : this(length, int.MaxValue) { }
    internal KendallCorrelationWindow(int length, int maximumObservations)
    {
        _length = Math.Max(1, length);
        var capacity = Math.Min(_length, Math.Max(1, maximumObservations));
        // The bounded form is for a complete batch of at most maximumObservations bars.
        // OLS uses observed history; Kendall retains the original zero-padding period.
        _fit = new ExactLinearFitWindow(capacity);
        _prices = new PooledRingBuffer<double>(capacity); _fitted = new PooledRingBuffer<BigInteger>(capacity);
    }
    internal double Next(double price, bool commit)
    {
        if (_prices.Capacity < _length && _prices.Count == _prices.Capacity)
            throw new InvalidOperationException("The bounded Kendall history has reached its observation limit.");
        var fitted = _fit.Next(price, commit).RoundedLastUnits;
        var count = Math.Min(_prices.Capacity, _prices.Count + 1);
        var padding = _length - count;
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
        var pairs = (long)_length * (_length - 1) / 2;
        var result = pairs == 0 ? 0 : ExactMeanAccumulator.UnitRatio(new BigInteger(score) << 1074, new BigInteger(pairs));
        if (commit) { _prices.TryAdd(price, out _); _fitted.TryAdd(fitted, out _); }
        return result;
    }
    internal void Reset() { _fit.Reset(); _prices.Clear(); _fitted.Clear(); }
    public void Dispose() { _fit.Dispose(); _prices.Dispose(); _fitted.Dispose(); }
}
