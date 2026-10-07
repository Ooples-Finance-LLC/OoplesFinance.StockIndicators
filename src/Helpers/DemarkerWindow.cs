using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DemarkerWindow : IDisposable
{
    private readonly StrengthAverage? _up, _down;
    private readonly IMovingAverageSmoother? _upFallback, _downFallback;
    private double _high, _low;
    private bool _hasPrevious;
    internal DemarkerWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(1, length);
        if (StrengthWindow.Supports(kind)) { _up = new(kind, length, capacityHint); _down = new(kind, length, capacityHint); }
        else { _upFallback = MovingAverageSmootherFactory.Create(kind, length); _downFallback = MovingAverageSmootherFactory.Create(kind, length); }
    }
    internal static StrengthValue PositiveDifference(double current, double previous)
    {
        var difference = new ExactMeanAccumulator(); if (current > previous) { difference.Add(current); difference.Add(previous, -1); } return StrengthValue.Round(difference, 1);
    }
    internal static double Ratio(StrengthValue up, StrengthValue down)
    {
        var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100); var denominator = new ExactMeanAccumulator(); up.AddTo(ref denominator); down.AddTo(ref denominator);
        return denominator.IsExactlyZero ? 0 : Math.Max(0, Math.Min(100, numerator.Ratio(denominator)));
    }
    internal double Next(double high, double low, bool commit)
    {
        var up = PositiveDifference(high, _hasPrevious ? _high : high); var down = PositiveDifference(_hasPrevious ? _low : low, low);
        var smoothUp = _up is not null ? _up.Next(up, commit) : new StrengthValue(_upFallback!.Next(up.Mantissa * (up.Doubled ? 2 : 1), commit));
        var smoothDown = _down is not null ? _down.Next(down, commit) : new StrengthValue(_downFallback!.Next(down.Mantissa * (down.Doubled ? 2 : 1), commit));
        if (commit) { _high = high; _low = low; _hasPrevious = true; } return Ratio(smoothUp, smoothDown);
    }
    internal void Reset() { _up?.Reset(); _down?.Reset(); _upFallback?.Reset(); _downFallback?.Reset(); _high = _low = 0; _hasPrevious = false; }
    public void Dispose() { _up?.Dispose(); _down?.Dispose(); _upFallback?.Dispose(); _downFallback?.Dispose(); }
}
