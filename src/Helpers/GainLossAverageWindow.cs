using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GainLossAverageWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private double _price;
    private bool _started;
    internal GainLossAverageWindow(MovingAvgType kind, int length, int signalLength, int capacityHint = int.MaxValue)
    {
        var periods = new[] { Math.Max(1, length), Math.Max(1, signalLength) };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(p => new RocBankAverage(kind, p, capacityHint)).ToArray();
        else _fallbacks = periods.Select(p => MovingAverageSmootherFactory.Create(kind, p)).ToArray();
    }
    internal static double Change(double price, double previous, bool started)
    {
        var current = ExactVarianceWindow.Units(price); var prior = ExactVarianceWindow.Units(previous);
        var denominator = current + prior;
        if (!started || denominator.IsZero) return 0;
        return ExactMeanAccumulator.UnitRatio((200 * (current - prior) * denominator.Sign) << 1074, BigInteger.Abs(denominator));
    }
    private RocBankValue Average(RocBankValue value, int stage, bool commit) => _averages is null
        ? new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit)) : _averages[stage].Next(value, commit);
    internal (double Value, double Signal) Next(double price, bool commit)
    {
        var change = Change(price, _price, _started);
        var average = Average(new RocBankValue(change), 0, commit); var signal = Average(average, 1, commit);
        if (commit) { _price = price; _started = true; }
        return (average.Publish(), signal.Publish());
    }
    internal void Reset()
    {
        _price = 0; _started = false;
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
