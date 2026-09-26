using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EhlersZeroLagWindow : IDisposable
{
    private readonly int _lag;
    private readonly PooledRingBuffer<double> _prices;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal EhlersZeroLagWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length); _lag = (length - 1) / 2; _prices = new(Math.Max(1, _lag));
        if (StrengthWindow.Supports(kind)) _average = new(kind, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, length);
    }
    internal RocBankValue Correct(double price, bool commit)
    {
        var adjusted = new RocBankValue(price);
        if (_lag > 0 && _prices.Count >= _lag)
        {
            var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(_prices[0], -1);
            var sum = new ExactMeanAccumulator(); adjusted.AddTo(ref sum); RocBankValue.Round(difference).AddTo(ref sum);
            adjusted = RocBankValue.Round(sum);
        }
        if (commit) _prices.TryAdd(price, out _);
        return adjusted;
    }
    internal double Next(double price, bool commit)
    {
        var adjusted = Correct(price, commit);
        return _average is null ? _fallback!.Next(adjusted.Publish(), commit) : _average.Next(adjusted, commit).Publish();
    }
    internal void Reset() { _prices.Clear(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _prices.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
