namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SimpleCycleWindow
{
    private readonly int _length;
    private readonly double _alpha, _emaWeight;
    private readonly Queue<RocBankValue> _sources = new();
    private RocBankValue _previous, _ema;
    internal SimpleCycleWindow(int length)
    { _length = Math.Max(1, length); _alpha = 1d / _length; _emaWeight = Math.Max(.01, Math.Min(.99, 2d / (_length + 1d))); }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    internal double Next(double price, bool commit)
    {
        var source = Add(new RocBankValue(price), _previous);
        var ema = Add(_previous.Multiply(_emaWeight), _ema.Multiply(1 - _emaWeight));
        var residual = Add(_previous, ema, -1); var prior = _sources.Count == _length ? _sources.Peek() : default;
        var change = Add(source, prior, -1); var cycle = Add(change.Multiply(_alpha), residual.Multiply(1 - _alpha));
        if (commit) { if (_sources.Count == _length) _sources.Dequeue(); _sources.Enqueue(source); _previous = cycle; _ema = ema; }
        return cycle.Publish();
    }
    internal void Reset() { _sources.Clear(); _previous = _ema = default; }
}
