using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HawkeyeWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length;
    private readonly BigInteger _divisor;
    private readonly Queue<(BigInteger Range, BigInteger Volume)> _history = new();
    private BigInteger _rangeSum, _volumeSum, _previousHigh, _previousLow, _previousMidpoint;
    internal HawkeyeWindow(int length, double divisor)
    {
        if (double.IsNaN(divisor) || double.IsInfinity(divisor)) throw new ArgumentOutOfRangeException(nameof(divisor));
        _length = Math.Max(1, length); _divisor = ExactVarianceWindow.Units(divisor);
    }
    internal (double Up, double Down, Signal Trade) Next(double midpoint, double high, double low, double close, double volume, bool final)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close); var v = ExactVarianceWindow.Units(volume);
        var range = h - l; var ranges = _rangeSum + range; var volumes = _volumeSum + v;
        var count = _history.Count < _length ? _history.Count + 1 : _length;
        if (_history.Count == _length) { var old = _history.Peek(); ranges -= old.Range; volumes -= old.Volume; }
        // Add the midpoint before narrowing the quotient: an overflowing offset
        // can still yield a representable level through cancellation.
        var denominator = _divisor.IsZero ? BigInteger.One : BigInteger.Abs(_divisor);
        var offset = _divisor.IsZero ? BigInteger.Zero : (_previousHigh - _previousLow) * Unit * _divisor.Sign;
        var up = _previousMidpoint * denominator + offset;
        var down = _previousMidpoint * denominator - offset;
        var below = c * denominator < down; var above = c * denominator > up;
        var largeRange = range * count > ranges; var smallRange = 3 * range * count < 2 * ranges;
        var largeVolume = v * count > volumes; var smallVolume = v * count < volumes;
        var red = largeRange && below && largeVolume || c < _previousMidpoint;
        var green = c > _previousMidpoint || largeRange && above && largeVolume
            || h > _previousHigh && smallRange && smallVolume || l < _previousLow && smallRange && largeVolume;
        var trade = green ? Signal.Buy : red ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((range, v));
            _rangeSum = ranges; _volumeSum = volumes;
            _previousHigh = h; _previousLow = l; _previousMidpoint = ExactVarianceWindow.Units(midpoint);
        }
        return (ExactMeanAccumulator.UnitRatio(up, denominator), ExactMeanAccumulator.UnitRatio(down, denominator), trade);
    }
    internal void Reset() { _history.Clear(); _rangeSum = _volumeSum = _previousHigh = _previousLow = _previousMidpoint = BigInteger.Zero; }
}
