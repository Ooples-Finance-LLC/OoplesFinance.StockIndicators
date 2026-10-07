using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class KaufmanAdaptiveBandWindow
{
    private readonly int _length;
    private readonly KaufmanBandAlgebra _algebra;
    private readonly Queue<F> _prices = new(), _moves = new();
    private F _lastPrice, _travel, _previousDifference;
    private bool _started;
    private KaufmanBandAlgebra.Node _mean, _second;
    private double _previousPrice, _previousUpper, _previousLower;
    internal KaufmanAdaptiveBandWindow(int length, double exponent)
    {
        Builder.Specs.KaufmanAdaptiveBandsSpecOptions.ValidateExponent(exponent);
        _length = Math.Max(1, length); _algebra = new(exponent);
        _mean = _second = _algebra.Constant(0);
    }
    internal (double Upper, double Middle, double Lower, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var value = F.Of(price); var full = _prices.Count == _length;
        var movement = _started ? (value - _lastPrice).Abs() : (F)0;
        var travel = _travel + movement - (full ? _moves.Peek() : (F)0);
        var efficiency = full && travel.Sign > 0 ? (value - _prices.Peek()).Abs() / travel : (F)0;
        var gain = _algebra.Power(efficiency); var retained = _algebra.Constant(1) - gain;
        var mean = gain * _algebra.Constant(value) + retained * _mean;
        var second = gain * _algebra.Constant(value * value) + retained * _second;
        var variance = second - mean * mean;
        var middle = mean.Publish(); var upper = mean.Band(variance, 1); var lower = mean.Band(variance, -1);
        // Signals use the published bands, as on the existing public route.
        // Rational differences prevent overflow without changing that boundary.
        var difference = value - F.Of(middle); var sign = difference.Sign;
        var change = (difference - _previousDifference).Sign;
        var trade = sign > 0 && change > 0 ? Signal.StrongBuy : sign < 0 && change < 0 ? Signal.StrongSell
            : sign > 0 || _previousPrice < _previousLower && price > lower ? Signal.Buy
            : sign < 0 || _previousPrice > _previousUpper && price < upper ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) { _prices.Dequeue(); _moves.Dequeue(); }
            _prices.Enqueue(value); _moves.Enqueue(movement); _lastPrice = value; _travel = travel; _started = true;
            _mean = mean; _second = second; _previousDifference = difference;
            _previousPrice = price; _previousUpper = upper; _previousLower = lower;
        }
        return (upper, middle, lower, trade);
    }
    internal void Reset()
    {
        _prices.Clear(); _moves.Clear(); _lastPrice = _travel = _previousDifference = default; _started = false;
        _mean = _second = _algebra.Constant(0);
        _previousPrice = _previousUpper = _previousLower = 0;
    }
}
