namespace OoplesFinance.StockIndicators.Helpers;

// Causal trailing Spencer filters; unavailable history contributes zero and
// the full coefficient sum remains the divisor during startup.
internal sealed class SpencerWindow : IDisposable
{
    private static readonly int[] ShortWeights = { -3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3 };
    private static readonly int[] LongWeights = { -1, -3, -5, -5, -2, 6, 18, 33, 47, 57, 60, 57, 47, 33, 18, 6, -2, -5, -5, -3, -1 };
    private readonly int[] _weights;
    private readonly int _divisor;
    private readonly PooledRingBuffer<double> _history;
    internal SpencerWindow(bool longWindow)
    {
        _weights = longWindow ? LongWeights : ShortWeights; _divisor = longWindow ? 350 : 320;
        _history = new(_weights.Length - 1);
    }
    internal double Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(price, _weights[0]);
        for (var lag = 1; lag < _weights.Length && lag <= _history.Count; lag++)
            sum.Add(_history[_history.Count - lag], _weights[lag]);
        var value = sum.Mean(_divisor);
        if (commit) _history.TryAdd(price, out _);
        return value;
    }
    internal void Reset() => _history.Clear();
    public void Dispose() => _history.Dispose();
}
