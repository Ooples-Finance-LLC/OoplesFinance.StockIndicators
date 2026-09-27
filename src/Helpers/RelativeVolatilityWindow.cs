using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RelativeVolatilityWindow : IDisposable
{
    private readonly ExactPopulationWindow _deviation;
    private readonly RocBankAverage? _up, _down;
    private readonly IMovingAverageSmoother? _upFallback, _downFallback;
    private double _previous;
    internal RelativeVolatilityWindow(MovingAvgType kind, int length, int smoothLength, bool external = false, int capacityHint = int.MaxValue)
    {
        _deviation = new(Math.Max(1, length)); smoothLength = Math.Max(1, smoothLength);
        if (!external) { if (StrengthWindow.Supports(kind)) { _up = new(kind, smoothLength, capacityHint); _down = new(kind, smoothLength, capacityHint); } else { _upFallback = MovingAverageSmootherFactory.Create(kind, smoothLength); _downFallback = MovingAverageSmootherFactory.Create(kind, smoothLength); } }
    }
    internal (double Up, double Down) Generate(double value, bool commit)
    {
        var deviation = _deviation.Next(value, commit); var up = value > _previous ? deviation : 0; var down = value < _previous ? deviation : 0;
        if (commit) _previous = value; return (up, down);
    }
    internal static double Ratio(RocBankValue up, RocBankValue down)
    {
        if (down.Mantissa == 0) return 100; if (up.Mantissa == 0) return 0;
        var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100); var denominator = new ExactMeanAccumulator(); up.AddTo(ref denominator); down.AddTo(ref denominator);
        return denominator.IsExactlyZero ? 0 : Math.Max(0, Math.Min(100, numerator.Ratio(denominator)));
    }
    internal static double Mean(double high, double low) { var total = new ExactMeanAccumulator(); total.Add(high); total.Add(low); return total.Mean(2); }
    internal double Next(double value, bool commit)
    {
        var moves = Generate(value, commit); var up = _up is not null ? _up.Next(new RocBankValue(moves.Up), commit) : new RocBankValue(_upFallback!.Next(moves.Up, commit)); var down = _down is not null ? _down.Next(new RocBankValue(moves.Down), commit) : new RocBankValue(_downFallback!.Next(moves.Down, commit)); return Ratio(up, down);
    }
    internal void Reset() { _deviation.Reset(); _up?.Reset(); _down?.Reset(); _upFallback?.Reset(); _downFallback?.Reset(); _previous = 0; }
    public void Dispose() { _deviation.Dispose(); _up?.Dispose(); _down?.Dispose(); _upFallback?.Dispose(); _downFallback?.Dispose(); }
}
