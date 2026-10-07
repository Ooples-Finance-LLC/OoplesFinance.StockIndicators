using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RobustWeightingWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<BigInteger> _history = new();
    private readonly RocBankAverage _priceMean, _timeMean, _residualMean;
    private BigInteger _sum, _weighted, _index, _previous, _previousSlope;
    internal RobustWeightingWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length);
        _priceMean = new(kind, _length, 1, true); _timeMean = new(kind, _length, 1, true);
        _residualMean = new(kind, _length, 1, true);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Average(RocBankAverage average, BigInteger value, bool final)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, value);
        var result = average.Next(RocBankValue.Round(sum), final);
        return U(result.Mantissa) << result.UpperShift;
    }
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var value = U(price); var full = _history.Count == _length;
        var expired = full ? _history.Peek() : BigInteger.Zero;
        var count = full ? _history.Count : _history.Count + 1;
        var sum = _sum + value - expired;
        var weighted = full ? _weighted - _sum + expired + (count - 1) * value : _weighted + (count - 1) * value;
        var mean = Average(_priceMean, value, final); var time = Average(_timeMean, _index << 1074, final);
        var n = new BigInteger(count); var denominator = n * (n * n - 1);
        // Standard deviation's existing warmup keeps the slope zero until a full window.
        var numerator = count < _length || count == 1 ? BigInteger.Zero : 6 * (2 * weighted - (n - 1) * sum);
        var line = numerator.IsZero ? mean : RocBankValue.RoundUnits((mean * denominator << 1074)
            + numerator * ((_index << 1074) - time), denominator << 1074);
        var residual = RocBankValue.RoundUnits(value - line, BigInteger.One);
        var output = Average(_residualMean, residual, final); var slope = output - _previous;
        var trade = slope.Sign > 0 ? slope > _previousSlope ? Signal.StrongBuy : Signal.Buy
            : slope.Sign < 0 ? slope < _previousSlope ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _history.Dequeue(); _history.Enqueue(value);
            _sum = sum; _weighted = weighted; _index++; _previous = output; _previousSlope = slope;
        }
        return (ExactMeanAccumulator.UnitRatio(output, BigInteger.One), trade);
    }
    internal void Reset()
    {
        _priceMean.Reset(); _timeMean.Reset(); _residualMean.Reset(); _history.Clear();
        _sum = _weighted = _index = _previous = _previousSlope = default;
    }
    public void Dispose() { _priceMean.Dispose(); _timeMean.Dispose(); _residualMean.Dispose(); _history.Clear(); }
    internal static (List<double> Values, List<Signal> Signals) Calculate(StockData data, MovingAvgType kind, int length)
    {
        var (input, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var state = new RobustWeightingWindow(kind, length); var result = new List<double>(input.Count); var signals = new List<Signal>(input.Count);
        foreach (var value in input) { var point = state.Next(value, true); result.Add(point.Value); signals.Add(point.Trade); }
        return (result, signals);
    }
}
