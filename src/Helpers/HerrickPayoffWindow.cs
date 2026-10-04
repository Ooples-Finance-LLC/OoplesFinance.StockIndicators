using OoplesFinance.StockIndicators.Streaming;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HerrickPayoffWindow
{
    private readonly F _pointValue;
    private F _previousPrice, _previousOpen, _previousClose;
    private double _previousResult;
    private bool _started;
    internal HerrickPayoffWindow(double pointValue)
    {
        StreamingInputValidation.Finite(pointValue, nameof(pointValue));
        _pointValue = F.Of(pointValue);
    }
    internal (double Value, Signal Trade) Next(double price, double open, double close, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        StreamingInputValidation.Finite(open, nameof(open));
        StreamingInputValidation.Finite(close, nameof(close));
        StreamingInputValidation.Finite(volume, nameof(volume));
        var p = F.Of(price); var o = F.Of(open); var c = F.Of(close);
        F result = 0;
        if (_started)
        {
            var change = p - _previousPrice;
            var opening = o < _previousOpen ? o : _previousOpen;
            var adjustment = opening.Sign == 0 ? (F)0 : (c - _previousClose).Abs() / (2 * opening);
            result = change * _pointValue * F.Of(volume) * (1 + (change.Sign < 0 ? -adjustment : adjustment));
        }
        var published = result.Publish();
        var trade = SignalHelper.GetCompareSignal(published, _previousResult);
        if (final)
        {
            _previousPrice = p; _previousOpen = o; _previousClose = c;
            _previousResult = published; _started = true;
        }
        return (published, trade);
    }
    internal void Reset()
    {
        _previousPrice = _previousOpen = _previousClose = default;
        _previousResult = 0;
        _started = false;
    }
}
