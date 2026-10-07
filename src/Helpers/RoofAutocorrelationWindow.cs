using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RoofAutocorrelationWindow
{
    private readonly int _length;
    private readonly EhlersRoofingFilterV2Kernel _roof;
    private readonly List<BigInteger> _history = new();
    private double _previous, _older;
    internal RoofAutocorrelationWindow(int length, int smoothing) { _length = Math.Max(1, length); _roof = new(_length, Math.Max(1, smoothing)); }
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        _roof.Next(price, commit, true); var value = _roof.ExactOutput; var units = ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
        var count = Math.Min(_length, _history.Count + 1); BigInteger sx = 0, sy = 0, xx = 0, yy = 0, xy = 0;
        for (var lag = 0; lag < count; lag++)
        {
            var x = lag == 0 ? units : _history[_history.Count - lag]; var delayed = (long)lag + _length; var y = delayed <= _history.Count ? _history[_history.Count - (int)delayed] : BigInteger.Zero;
            sx += x; sy += y; xx += x * x; yy += y * y; xy += x * y;
        }
        var vx = count * xx - sx * sx; var vy = count * yy - sy * sy; var covariance = count * xy - sx * sy;
        var correlation = count < 2 || vx.IsZero || vy.IsZero ? 0 : .5 * (1 + covariance.Sign * ExactPopulationDeviation.RootRatio((covariance * covariance) << 2148, vx * vy));
        var change = correlation - _previous; var previousChange = _previous - _older; var signal = change > 0 ? change > previousChange ? Signal.StrongBuy : Signal.Buy : change < 0 ? change < previousChange ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { if (_history.Count >= 2L * _length - 1) _history.RemoveAt(0); _history.Add(units); _older = _previous; _previous = correlation; } return (correlation, signal);
    }
    internal void Reset() { _roof.Reset(); _history.Clear(); _previous = _older = 0; }
}
