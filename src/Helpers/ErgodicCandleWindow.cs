using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ErgodicCandleWindow : IDisposable
{
    private readonly RocBankAverage[]? _averages;
    private readonly IMovingAverageSmoother[]? _fallback;
    internal ErgodicCandleWindow(MovingAvgType kind, int first, int second, int capacity = int.MaxValue)
    {
        var periods = new[] { first, second, first, second, second };
        if (StrengthWindow.Supports(kind)) _averages = periods.Select(n => new RocBankAverage(kind, Math.Max(1, n), capacity)).ToArray();
        else _fallback = periods.Select(n => MovingAverageSmootherFactory.Create(kind, Math.Max(1, n))).ToArray();
    }
    private RocBankValue Stage(int slot, RocBankValue value, bool commit) => _averages is not null ? _averages[slot].Next(value, commit) : new(_fallback![slot].Next(value.Publish(), commit));
    private static RocBankValue Difference(double a, double b)
    { var sum = new ExactMeanAccumulator(); sum.Add(a); sum.Add(b, -1); return RocBankValue.Round(sum); }
    internal (double Eco, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var body = Stage(1, Stage(0, Difference(close, open), commit), commit);
        var range = Stage(3, Stage(2, Difference(high, low), commit), commit);
        var ratio = default(RocBankValue);
        if (range.Mantissa != 0)
        {
            var numerator = (100 * ExactVarianceWindow.Units(body.Mantissa)) << body.UpperShift;
            var denominator = ExactVarianceWindow.Units(range.Mantissa) << range.UpperShift;
            var units = RocBankValue.RoundUnits(numerator << 1074, denominator);
            var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, units); ratio = RocBankValue.Round(sum);
        }
        var signal = Stage(4, ratio, commit);
        return (ratio.Publish(), signal.Publish());
    }
    internal void Reset() { if (_averages is not null) foreach (var v in _averages) v.Reset(); if (_fallback is not null) foreach (var v in _fallback) v.Reset(); }
    public void Dispose() { if (_averages is not null) foreach (var v in _averages) v.Dispose(); if (_fallback is not null) foreach (var v in _fallback) v.Dispose(); }
}
