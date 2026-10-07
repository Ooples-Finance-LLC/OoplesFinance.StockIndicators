using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HurstCoefficientWindow
{
    private readonly int _length, _half;
    private readonly Extrema _full, _recent, _older;
    private readonly Queue<double> _lag = new();
    private readonly BigInteger _gain, _feedback, _decay;
    private long _index;
    private double _dimension, _hurst, _value, _previousValue;
    internal HurstCoefficientWindow(int length, int smoothLength)
    {
        _length = Math.Max(1, length); _half = _length / 2 + _length % 2;
        _full = new(_length); _recent = new(_half); _older = new(Math.Max(1, _length - _half));
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(1, smoothLength);
        var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(Math.Min(angle, .99)));
        _feedback = 2 * radius * cosine; _decay = -radius * radius;
        _gain = (BigInteger.One << 2148) - _feedback - _decay;
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    internal static double LogRatio(BigInteger numerator, BigInteger denominator)
    {
        // Range scaling cancels before the logarithm. Normalize the rational
        // argument so neither an overflow nor a subnormal range can erase it.
        var exponent = (numerator.ToByteArray().Length - denominator.ToByteArray().Length) * 8;
        if (exponent >= 0) denominator <<= exponent; else numerator <<= -exponent;
        while (numerator < denominator) { numerator <<= 1; exponent--; }
        while (numerator >= 2 * denominator) { denominator <<= 1; exponent++; }
        var mantissa = ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator);
        return exponent + Math.Log(mantissa) / Math.Log(2);
    }
    internal (double Value, double Hurst, double Dimension, Signal Trade) Next(double price, bool final)
    {
        var full = _full.Next(price, final); var recent = _recent.Next(price, final);
        (double High, double Low) older;
        if (_lag.Count == _half) older = _older.Next(_lag.Peek(), final);
        else older = (price, price);
        // Unavailable observations in the older half are explicitly zero;
        // its initial anchor is the current price until the half-lag exists.
        if (_length > _half && _index < _length - 1L) older = (Math.Max(0, older.High), Math.Min(0, older.Low));
        var fullRange = U(full.High) - U(full.Low); var halves = U(recent.High) - U(recent.Low) + U(older.High) - U(older.Low);
        var dimension = _dimension;
        if (fullRange.Sign > 0 && halves.Sign > 0)
        {
            var log = LogRatio(halves * _length, fullRange * _half);
            dimension = ExactMeanAccumulator.UnitRatio(U(log) + U(_dimension), new BigInteger(2));
        }
        var hurst = ExactMeanAccumulator.UnitRatio(U(2) - U(dimension), BigInteger.One);
        // Retain the exact two-pole coefficient polynomial of the rounded
        // radius/cosine. Its small DC gain must not cancel at large periods.
        var sum = _gain * (U(hurst) + U(_hurst)) + 2 * _feedback * U(_value) + 2 * _decay * U(_previousValue);
        var value = ExactMeanAccumulator.UnitRatio(sum, BigInteger.One << 2149);
        var slope = U(value) - U(_value); var previousSlope = U(_value) - U(_previousValue);
        var trade = slope.Sign > 0 && slope > previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < previousSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_lag.Count == _half) _lag.Dequeue(); _lag.Enqueue(price); _index++;
            _dimension = dimension; _hurst = hurst; _previousValue = _value; _value = value;
        }
        return (value, hurst, dimension, trade);
    }
    internal void Reset() { _full.Reset(); _recent.Reset(); _older.Reset(); _lag.Clear(); _index = 0; _dimension = _hurst = _value = _previousValue = 0; }
    private sealed class Extrema
    {
        private readonly int _length; private long _index;
        private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
        internal Extrema(int length) => _length = length;
        private double Extreme(LinkedList<(long Index, double Value)> queue, double value, bool maximum, bool final)
        {
            var expiry = _index - _length + 1L; var first = queue.First;
            while (first is not null && first.Value.Index < expiry) first = first.Next;
            var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
            if (final)
            {
                while (queue.First is { } old && old.Value.Index < expiry) queue.RemoveFirst();
                while (queue.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) queue.RemoveLast();
                queue.AddLast((_index, value));
            }
            return result;
        }
        internal (double High, double Low) Next(double value, bool final)
        { var result = (Extreme(_highs, value, true, final), Extreme(_lows, value, false, final)); if (final) _index++; return result; }
        internal void Reset() { _highs.Clear(); _lows.Clear(); _index = 0; }
    }
}
