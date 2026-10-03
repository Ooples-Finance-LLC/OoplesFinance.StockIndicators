using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class StochasticMomentumWindow : IDisposable
{
    private readonly RollingWindowMax _high;
    private readonly RollingWindowMin _low;
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    internal StochasticMomentumWindow(MovingAvgType kind, int rangeLength, int firstLength, int secondLength, int signalLength)
    {
        _high = new(Math.Max(1, rangeLength)); _low = new(Math.Max(1, rangeLength));
        var lengths = new[] { firstLength, firstLength, secondLength, secondLength, signalLength };
        if (StrengthWindow.Supports(kind)) _averages = lengths.Select(n => new RocBankAverage(kind, n, int.MaxValue)).ToArray();
        else _fallbacks = lengths.Select(n => MovingAverageSmootherFactory.Create(kind, Math.Max(1, n))).ToArray();
    }
    internal static (RocBankValue Distance, RocBankValue Range) Components(double price, double high, double low)
    {
        var middle = new ExactMeanAccumulator(); middle.Add(high); middle.Add(low);
        var distance = new ExactMeanAccumulator(); distance.Add(price); distance.Add(middle.Mean(2), -1);
        var range = new ExactMeanAccumulator(); range.Add(high); range.Add(low, -1);
        return (RocBankValue.Round(distance), RocBankValue.Round(range));
    }
    internal static double Ratio(RocBankValue distance, RocBankValue range)
    {
        var numerator = new ExactMeanAccumulator(); distance.AddTo(ref numerator, 200);
        var denominator = new ExactMeanAccumulator(); range.AddTo(ref denominator);
        return MathHelper.MinOrMax(numerator.Ratio(denominator), 100, -100);
    }
    private RocBankValue Smooth(int stage, RocBankValue value, bool commit) => _averages is not null
        ? _averages[stage].Next(value, commit) : new(_fallbacks![stage].Next(value.Publish(), commit));
    internal (double Line, double Signal) Next(double price, double high, double low, bool commit)
    {
        var highest = commit ? _high.Add(high, out _) : _high.Preview(high, out _);
        var lowest = commit ? _low.Add(low, out _) : _low.Preview(low, out _);
        var components = Components(price, highest, lowest);
        var distance = Smooth(2, Smooth(0, components.Distance, commit), commit);
        var range = Smooth(3, Smooth(1, components.Range, commit), commit);
        var line = Ratio(distance, range);
        return (line, Smooth(4, new(line), commit).Publish());
    }
    internal void Reset()
    {
        _high.Reset(); _low.Reset();
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        _high.Dispose(); _low.Dispose();
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
