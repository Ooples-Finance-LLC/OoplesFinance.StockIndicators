using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Lagged-estimate error weighting. Round each recursive estimate to binary64 and
// each recursive error to binary64 precision with an extended upper exponent.
// Differences and the affine quotient are exact before those state boundaries.
internal sealed class ParametricKalmanWindow
{
    private readonly int _length;
    private readonly Queue<double> _estimates = new();
    private BigInteger _error, _previousPrice, _previousEstimate, _margin;
    internal ParametricKalmanWindow(int length) => _length = Math.Max(1, length);
    internal (double Value, Signal Signal) Next(double price, bool final)
    {
        var current = ExactVarianceWindow.Units(price);
        var previousPrice = _estimates.Count == 0 ? current : _previousPrice;
        var previousEstimate = _estimates.Count == 0 ? current : _previousEstimate;
        var lagged = _estimates.Count == _length ? ExactVarianceWindow.Units(_estimates.Peek()) : previousPrice;
        var measurement = BigInteger.Abs(current - lagged);
        var total = measurement + _error;
        var estimate = total.IsZero ? price : ExactMeanAccumulator.UnitRatio(measurement * previousEstimate + _error * current, total);
        var error = total.IsZero ? BigInteger.Zero : RocBankValue.RoundUnits(measurement * BigInteger.Abs(current - previousPrice), total);
        var estimateUnits = ExactVarianceWindow.Units(estimate);
        var margin = current - estimateUnits;
        var signal = margin.Sign > 0 ? (margin > _margin ? Signal.StrongBuy : Signal.Buy)
            : margin.Sign < 0 ? (margin < _margin ? Signal.StrongSell : Signal.Sell) : Signal.None;
        if (final)
        {
            if (_estimates.Count == _length) _estimates.Dequeue();
            _estimates.Enqueue(estimate); _error = error; _previousPrice = current;
            _previousEstimate = estimateUnits; _margin = margin;
        }
        return (estimate, signal);
    }
    internal void Reset() { _estimates.Clear(); _error = _previousPrice = _previousEstimate = _margin = BigInteger.Zero; }
}
