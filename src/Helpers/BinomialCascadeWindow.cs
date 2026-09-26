using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BinomialCascadeWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallbacks;
    private readonly int[] _weights;
    internal BinomialCascadeWindow(MovingAvgType kind, int length, bool pentuple)
    {
        _weights = pentuple ? new[] { 8, -28, 56, -70, 56, -28, 8, -1 } : new[] { 5, -10, 10, -5, 1 };
        if (StrengthWindow.Supports(kind)) _averages = _weights.Select(_ => new RocBankAverage(kind, length, int.MaxValue)).ToArray();
        else _fallbacks = _weights.Select(_ => MovingAverageSmootherFactory.Create(kind, Math.Max(1, length))).ToArray();
    }
    internal double Next(double price, bool commit)
    {
        var stage = new RocBankValue(price); var total = new ExactMeanAccumulator();
        for (var i = 0; i < _weights.Length; i++)
        {
            stage = _averages is null ? new RocBankValue(_fallbacks![i].Next(stage.Publish(), commit)) : _averages[i].Next(stage, commit);
            stage.AddTo(ref total, _weights[i]);
        }
        return total.Mean(1);
    }
    internal static double Combine(bool pentuple, params double[] stages)
    {
        var weights = pentuple ? new[] { 8, -28, 56, -70, 56, -28, 8, -1 } : new[] { 5, -10, 10, -5, 1 };
        var total = new ExactMeanAccumulator();
        for (var i = 0; i < weights.Length; i++) total.AddProduct(stages[i], weights[i]);
        return total.Mean(1);
    }
    internal void Reset()
    {
        if (_averages is not null) foreach (var average in _averages) average.Reset();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Reset();
    }
    public void Dispose()
    {
        if (_averages is not null) foreach (var average in _averages) average.Dispose();
        if (_fallbacks is not null) foreach (var average in _fallbacks) average.Dispose();
    }
}
