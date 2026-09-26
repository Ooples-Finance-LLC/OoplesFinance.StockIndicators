using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AbsoluteStrengthMtfWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private double _price;
    internal AbsoluteStrengthMtfWindow(MovingAvgType kind, int length, int smoothLength, int capacityHint = int.MaxValue)
    {
        var periods = new[] { Math.Max(1, length), Math.Max(1, length), Math.Max(1, smoothLength), Math.Max(1, smoothLength) };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(p => new RocBankAverage(kind, p, capacityHint)).ToArray();
        else _fallbacks = periods.Select(p => MovingAverageSmootherFactory.Create(kind, p)).ToArray();
    }
    private RocBankValue Average(RocBankValue value, int stage, bool commit) => _averages is null
        ? new RocBankValue(_fallbacks![stage].Next(value.Publish(), commit)) : _averages[stage].Next(value, commit);
    internal static RocBankValue Difference(RocBankValue current, RocBankValue previous)
    { var sum = new ExactMeanAccumulator(); current.AddTo(ref sum); previous.AddTo(ref sum, -1); return RocBankValue.Round(sum); }
    internal (double Bulls, double Bears) Next(double price, bool commit)
    {
        var current = Average(new RocBankValue(price), 0, commit); var previous = Average(new RocBankValue(_price), 1, commit);
        var difference = Difference(current, previous);
        var bulls = Average(difference.Mantissa > 0 ? difference : default, 2, commit);
        var bears = Average(difference.Mantissa < 0 ? new RocBankValue(-difference.Mantissa, difference.UpperShift) : default, 3, commit);
        if (commit) _price = price;
        return (bulls.Publish(), bears.Publish());
    }
    internal void Reset()
    {
        _price = 0;
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
