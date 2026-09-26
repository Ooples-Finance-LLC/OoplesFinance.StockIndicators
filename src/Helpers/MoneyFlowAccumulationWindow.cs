using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MoneyFlowAccumulationWindow
{
    private ExactMeanAccumulator _total;
    internal RocBankValue Next(double high, double low, double close, double volume, bool commit)
    {
        var flow = Flow(high, low, close, volume);
        var total = _total; flow.AddTo(ref total);
        if (commit) _total = total;
        return RocBankValue.Round(total);
    }
    internal static RocBankValue Flow(double high, double low, double close, double volume)
    {
        RocBankValue flow = default;
        if (high != low)
        {
            var numerator = new ExactMeanAccumulator();
            numerator.AddProduct(close, volume, 2); numerator.AddProduct(high, volume, -1); numerator.AddProduct(low, volume, -1);
            for (var shift = 0; ; shift += 1024)
            {
                var denominator = new ExactMeanAccumulator(); var scale = BigInteger.One << shift;
                denominator.Add(high, scale); denominator.Add(low, -scale);
                var value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value)) { flow = new RocBankValue(value, shift); break; }
            }
        }
        return flow;
    }
    internal void Reset() => _total = default;
}

internal sealed class MoneyFlowAverageWindow : IDisposable
{
    private readonly MoneyFlowAccumulationWindow _line = new();
    private readonly RocBankAverage? _first, _second;
    private readonly IMovingAverageSmoother? _firstFallback, _secondFallback;
    private readonly bool _oscillator;
    internal MoneyFlowAverageWindow(MovingAvgType kind, int firstLength, int? secondLength = null)
    {
        _oscillator = secondLength.HasValue;
        if (StrengthWindow.Supports(kind))
        {
            _first = new RocBankAverage(kind, firstLength, int.MaxValue);
            if (secondLength.HasValue) _second = new RocBankAverage(kind, secondLength.Value, int.MaxValue);
        }
        else
        {
            _firstFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, firstLength));
            if (secondLength.HasValue) _secondFallback = MovingAverageSmootherFactory.Create(kind, Math.Max(1, secondLength.Value));
        }
    }
    internal (double Line, double Signal) Next(double high, double low, double close, double volume, bool commit,
        double? customerFirst = null, double? customerSecond = null)
    {
        var line = _line.Next(high, low, close, volume, commit);
        var first = customerFirst.HasValue ? new RocBankValue(customerFirst.Value) : _first is null
            ? new RocBankValue(_firstFallback!.Next(line.Publish(), commit)) : _first.Next(line, commit);
        var signal = new ExactMeanAccumulator(); first.AddTo(ref signal);
        if (_oscillator)
        {
            var second = customerSecond.HasValue ? new RocBankValue(customerSecond.Value) : _second is null
                ? new RocBankValue(_secondFallback!.Next(line.Publish(), commit)) : _second.Next(line, commit);
            second.AddTo(ref signal, -1);
        }
        return (line.Publish(), signal.Mean(1));
    }
    internal void Reset() { _line.Reset(); _first?.Reset(); _second?.Reset(); _firstFallback?.Reset(); _secondFallback?.Reset(); }
    public void Dispose() { _first?.Dispose(); _second?.Dispose(); _firstFallback?.Dispose(); _secondFallback?.Dispose(); }
}
