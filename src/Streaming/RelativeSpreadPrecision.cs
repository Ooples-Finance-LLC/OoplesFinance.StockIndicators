namespace OoplesFinance.StockIndicators.Streaming;

// Keep the low part through the fast/slow subtraction and through its first difference.
// Rounding either intermediate to double would erase the changes subsequently amplified by RSI.
internal readonly struct SpreadNumber
{
    internal readonly double High, Low;
    internal SpreadNumber(double high, double low = 0) { High = high; Low = low; }
    internal double Value => High + Low;
    internal static SpreadNumber Add(SpreadNumber a, SpreadNumber b)
    {
        var pair = Helpers.CompensatedSum.Add(a.High, a.Low, b.High);
        pair = Helpers.CompensatedSum.Add(pair.High, pair.Low, b.Low);
        return new(pair.High, pair.Low);
    }
    internal static SpreadNumber Subtract(SpreadNumber a, SpreadNumber b) => Add(a, new(-b.High, -b.Low));
    internal SpreadNumber Times(double b)
    {
        var product = High * b;
        // Bit splitting avoids the overflow of Dekker's large splitter multiplication.
        var ah = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(High) & ~((1L << 27) - 1));
        var bh = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(b) & ~((1L << 27) - 1));
        var al = High - ah; var bl = b - bh;
        var error = ((ah * bh - product) + ah * bl + al * bh) + al * bl + Low * b;
        return Add(new(product), new(error));
    }
    internal SpreadNumber DividedBy(double b)
    {
        var quotient = High / b;
        var remainder = Subtract(this, new SpreadNumber(quotient).Times(b));
        return Add(new(quotient), new(remainder.Value / b));
    }
    internal SpreadNumber Times(SpreadNumber b) => Add(Times(b.High), Times(b.Low));
    internal SpreadNumber DividedBy(SpreadNumber b)
    {
        var quotient = High / b.High;
        var remainder = Subtract(this, b.Times(quotient));
        return Add(new(quotient), new(remainder.Value / b.Value));
    }
}

internal sealed class SpreadAverage : IDisposable
{
    private readonly MovingAvgType _kind;
    private readonly int _length;
    private readonly SpreadNumber[] _window;
    private readonly SpreadAverage? _first, _second, _third;
    private readonly IMovingAverageSmoother? _fallback;
    private SpreadNumber _sum, _weighted, _previous;
    private int _count, _next;

    internal SpreadAverage(MovingAvgType kind, int length)
    {
        _kind = kind; _length = Math.Max(1, length);
        _window = kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage ? new SpreadNumber[_length] : Array.Empty<SpreadNumber>();
        if (kind is MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage)
        {
            _first = new(MovingAvgType.ExponentialMovingAverage, _length);
            _second = new(MovingAvgType.ExponentialMovingAverage, _length);
            if (kind == MovingAvgType.TripleExponentialMovingAverage) _third = new(MovingAvgType.ExponentialMovingAverage, _length);
        }
        else if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod))
            _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }

    internal static List<double> Calculate(IReadOnlyList<double> values, MovingAvgType kind, int length)
    {
        using var average = new SpreadAverage(kind, length);
        var result = new List<double>(values.Count);
        for (var i = 0; i < values.Count; i++) result.Add(average.Next(new(values[i]), true).Value);
        return result;
    }

    internal SpreadNumber Next(SpreadNumber value, bool final)
    {
        if (_fallback is not null) return new(_fallback.Next(value.Value, final));
        if (_first is not null)
        {
            var first = _first.Next(value, final);
            var second = _second!.Next(first, final);
            return _third is null ? SpreadNumber.Subtract(first.Times(2), second)
                : SpreadNumber.Add(SpreadNumber.Subtract(first, second).Times(3), _third.Next(second, final));
        }
        var sum = _sum; var weighted = _weighted;
        SpreadNumber result;
        if (_window.Length > 0)
        {
            weighted = SpreadNumber.Add(SpreadNumber.Subtract(weighted, sum), value.Times(_length));
            sum = SpreadNumber.Add(SpreadNumber.Subtract(sum, _window[_next]), value);
            result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.DividedBy(_length * (_length + 1d) / 2)
                : _count + 1 < _length ? new(0) : sum.DividedBy(_length);
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        { sum = SpreadNumber.Add(sum, value); result = sum.DividedBy(_count + 1); }
        else
        {
            var change = SpreadNumber.Subtract(value, _previous);
            result = SpreadNumber.Add(_previous, _kind == MovingAvgType.WildersSmoothingMethod
                ? change.DividedBy(_length) : change.Times(2).DividedBy(_length + 1d));
        }
        if (final)
        {
            if (_window.Length > 0) { _window[_next] = value; _next = (_next + 1) % _length; }
            _sum = sum; _weighted = weighted; _previous = result; _count++;
        }
        return result;
    }
    internal void Reset()
    {
        Array.Clear(_window, 0, _window.Length); _sum = _weighted = _previous = new(0); _count = _next = 0;
        _first?.Reset(); _second?.Reset(); _third?.Reset(); _fallback?.Reset();
    }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _third?.Dispose(); _fallback?.Dispose(); }
}

internal sealed class RelativeSpreadKernel : IDisposable
{
    private readonly SpreadAverage _fast, _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly int _period;
    private SpreadNumber _spread;
    private double _gain, _loss, _rsi;
    private bool _hasPrevious;
    internal RelativeSpreadKernel(MovingAvgType kind, int fast, int slow, int period, int smooth)
    {
        _fast = new(kind, fast); _slow = new(kind, slow); _period = Math.Max(1, period);
        _signal = MovingAverageSmootherFactory.Create(kind, Math.Max(1, smooth));
    }
    internal double Next(double price, bool final)
    {
        var spread = SpreadNumber.Subtract(_fast.Next(new(price), final), _slow.Next(new(price), final));
        var change = _hasPrevious ? SpreadNumber.Subtract(spread, _spread).Value : 0;
        var gain = _gain + (Math.Max(0, change) - _gain) / _period;
        var loss = _loss + (Math.Max(0, -change) - _loss) / _period;
        var rsi = change == 0 && _hasPrevious && _period > 1 ? _rsi : loss == 0 ? 100 : 100 * gain / (gain + loss);
        var result = _signal.Next(rsi, final);
        if (final) { _spread = spread; _gain = gain; _loss = loss; _rsi = rsi; _hasPrevious = true; }
        return result;
    }
    internal void Reset()
    { _fast.Reset(); _slow.Reset(); _signal.Reset(); _spread = new(0); _gain = _loss = _rsi = 0; _hasPrevious = false; }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }
}
