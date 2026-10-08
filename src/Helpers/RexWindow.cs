using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RexWindow : IDisposable
{
    private readonly RocBankAverage? _first, _signal;
    private readonly IMovingAverageSmoother? _firstFallback, _signalFallback;
    internal RexWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length); if (StrengthWindow.Supports(kind)) { _first = new(kind, length, capacityHint); _signal = new(kind, length, capacityHint); } else { _firstFallback = MovingAverageSmootherFactory.Create(kind, length); _signalFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal static RocBankValue TrueValue(double close, double open, double high, double low)
    { var sum = new ExactMeanAccumulator(); sum.Add(close, 3); sum.Add(open, -1); sum.Add(high, -1); sum.Add(low, -1); return RocBankValue.Round(sum); }
    internal (double Value, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var input = TrueValue(close, open, high, low); var first = _first is not null ? _first.Next(input, commit) : new RocBankValue(_firstFallback!.Next(input.Publish(), commit)); var signal = _signal is not null ? _signal.Next(first, commit) : new RocBankValue(_signalFallback!.Next(first.Publish(), commit)); return (first.Publish(), signal.Publish());
    }
    internal void Reset() { _first?.Reset(); _signal?.Reset(); _firstFallback?.Reset(); _signalFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _signal?.Dispose(); _firstFallback?.Dispose(); _signalFallback?.Dispose(); }
}
