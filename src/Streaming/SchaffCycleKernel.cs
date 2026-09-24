namespace OoplesFinance.StockIndicators.Streaming;

// Keep low parts until after each stochastic division. Rounding fastD near 100 before
// measuring its shrinking range turns sub-ULP errors into whole oscillator points.
internal sealed class SchaffCycleKernel : IDisposable
{
    private readonly SpreadAverage _fast, _slow;
    private readonly PooledRingBuffer<SpreadNumber> _macd, _middle;
    private readonly PooledRingBuffer<double> _scales;
    private readonly int _d1, _d2;
    private SpreadNumber _previousMiddle, _previousOutput;
    private SpreadNumber _previousFirst, _previousSecond;

    internal SchaffCycleKernel(MovingAvgType kind, int fast, int slow, int cycle, int d1, int d2)
    {
        _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow));
        _macd = new(Math.Max(1, cycle)); _middle = new(Math.Max(1, cycle)); _scales = new(Math.Max(1, cycle));
        _d1 = Math.Max(1, d1); _d2 = Math.Max(1, d2);
    }

    internal (double Stc, double Macd) Next(double value, bool final)
    {
        var fast = _fast.Next(new SpreadNumber(value), final);
        var slow = _slow.Next(new SpreadNumber(value), final);
        var macd = SpreadNumber.Subtract(fast, slow);
        var scale = Math.Abs(fast.Value) + Math.Abs(slow.Value);
        var scaleMaximum = scale;
        for (var i = _scales.Count == _scales.Capacity ? 1 : 0; i < _scales.Count; i++)
            scaleMaximum = Math.Max(scaleMaximum, _scales[i]);
        var first = Normalize(macd, _macd, scaleMaximum, _previousFirst);
        var middle = SpreadNumber.Add(_previousMiddle,
            SpreadNumber.Subtract(first, _previousMiddle).Times(2).DividedBy(_d1 + 1d));
        var second = Normalize(middle, _middle, null, _previousSecond);
        var output = SpreadNumber.Add(_previousOutput,
            SpreadNumber.Subtract(second, _previousOutput).Times(2).DividedBy(_d2 + 1d));
        if (final)
        {
            _macd.TryAdd(macd, out _); _middle.TryAdd(middle, out _); _scales.TryAdd(scale, out _);
            _previousFirst = first; _previousSecond = second; _previousMiddle = middle; _previousOutput = output;
        }
        return (Math.Max(0, Math.Min(100, output.Value)), macd.Value);
    }

    private static SpreadNumber Normalize(SpreadNumber value, PooledRingBuffer<SpreadNumber> history, double? scale, SpreadNumber previous)
    {
        var low = value; var high = value;
        for (var i = history.Count == history.Capacity ? 1 : 0; i < history.Count; i++)
        {
            if (SpreadNumber.Subtract(history[i], low).Value < 0) low = history[i];
            if (SpreadNumber.Subtract(history[i], high).Value > 0) high = history[i];
        }
        var range = SpreadNumber.Subtract(high, low);
        var magnitude = scale ?? Math.Max(Math.Abs(low.Value), Math.Abs(high.Value));
        if (range.Value <= 1.4210854715202004e-14 * magnitude) return previous;
        var normalized = SpreadNumber.Subtract(value, low).Times(100).DividedBy(range);
        return normalized.Value < 0 ? new SpreadNumber(0) : normalized.Value > 100 ? new SpreadNumber(100) : normalized;
    }

    internal void Reset()
    {
        _fast.Reset(); _slow.Reset(); _macd.Clear(); _middle.Clear(); _scales.Clear();
        _previousFirst = _previousSecond = _previousMiddle = _previousOutput = new SpreadNumber(0);
    }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); _macd.Dispose(); _middle.Dispose(); _scales.Dispose(); }
}
