using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DerivativeWindow : IDisposable
{
    private readonly RsiState _rsi;
    private readonly StrengthAverage? _first, _second;
    private readonly IMovingAverageSmoother? _fallbackFirst, _fallbackSecond;
    private readonly ExactPartialMeanWindow _mean;
    internal DerivativeWindow(MovingAvgType kind, int rsi, int mean, int first, int second)
    {
        _rsi = new(kind, rsi); _mean = new(mean);
        if (StrengthWindow.Supports(kind)) { _first = new(kind, first); _second = new(kind, second); }
        else { _fallbackFirst = MovingAverageSmootherFactory.Create(kind, Math.Max(1, first)); _fallbackSecond = MovingAverageSmootherFactory.Create(kind, Math.Max(1, second)); }
    }
    internal double Next(double price, bool commit)
    {
        var rsi = _rsi.Next(price, commit);
        var first = _first is null ? _fallbackFirst!.Next(rsi, commit) : _first.Next(new StrengthValue(rsi), commit).Mantissa;
        var second = _second is null ? _fallbackSecond!.Next(first, commit) : _second.Next(new StrengthValue(first), commit).Mantissa;
        return second - _mean.Next(second, commit);
    }
    internal void Reset() { _rsi.Reset(); _first?.Reset(); _second?.Reset(); _fallbackFirst?.Reset(); _fallbackSecond?.Reset(); _mean.Reset(); }
    public void Dispose() { _rsi.Dispose(); _first?.Dispose(); _second?.Dispose(); _fallbackFirst?.Dispose(); _fallbackSecond?.Dispose(); _mean.Dispose(); }
}
