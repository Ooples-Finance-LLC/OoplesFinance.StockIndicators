namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ShinoharaWindow : IDisposable
{
    private readonly record struct Candle(double Open, double High, double Low, double PreviousClose);
    private readonly PooledRingBuffer<Candle> _window;
    private ExactMeanAccumulator _upA, _downA, _upB, _downB;
    private double _previous;
    internal ShinoharaWindow(int length) => _window = new(Math.Max(1, length));
    private static void Add(ref ExactMeanAccumulator upA, ref ExactMeanAccumulator downA,
        ref ExactMeanAccumulator upB, ref ExactMeanAccumulator downB, Candle candle, int weight)
    {
        upA.Add(candle.High, 100 * weight); upA.Add(candle.Open, -100 * weight);
        downA.Add(candle.Open, weight); downA.Add(candle.Low, -weight);
        upB.Add(candle.High, 100 * weight); upB.Add(candle.PreviousClose, -100 * weight);
        downB.Add(candle.PreviousClose, weight); downB.Add(candle.Low, -weight);
    }
    internal (double A, double B) Next(double open, double high, double low, double close, bool commit)
    {
        var current = new Candle(open, high, low, _previous);
        var upA = _upA; var downA = _downA; var upB = _upB; var downB = _downB;
        if (_window.Count == _window.Capacity) Add(ref upA, ref downA, ref upB, ref downB, _window[0], -1);
        Add(ref upA, ref downA, ref upB, ref downB, current, 1);
        var result = (upA.Ratio(downA), upB.Ratio(downB));
        if (commit)
        { _window.TryAdd(current, out _); _upA = upA; _downA = downA; _upB = upB; _downB = downB; _previous = close; }
        return result;
    }
    internal void Reset() { _window.Clear(); _upA = _downA = _upB = _downB = default; _previous = 0; }
    public void Dispose() => _window.Dispose();
}
