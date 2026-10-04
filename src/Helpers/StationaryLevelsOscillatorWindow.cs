using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class StationaryLevelsOscillatorWindow : IDisposable
{
    private readonly int _length;
    private readonly long _rangeLength;
    private readonly RocBankAverage? _wideMean;
    private readonly IMovingAverageSmoother? _legacyMean;
    private readonly Queue<BigInteger> _first = new(), _second = new();
    private readonly LinkedList<(long Index, BigInteger Value)> _max = new(), _min = new();
    private long _index;
    private BigInteger _previous, _previousSlope;
    internal StationaryLevelsOscillatorWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length); _rangeLength = 2L * _length;
        if (StrengthWindow.Supports(kind)) _wideMean = new(kind, _length, 1, true);
        else _legacyMean = MovingAverageSmootherFactory.Create(kind, _length);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger numerator, int denominator = 1) => RocBankValue.RoundUnits(numerator, denominator);
    internal (double Value, Signal Trade) Next(double price, bool final, double? suppliedMean = null)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        BigInteger mean;
        if (suppliedMean is { } supplied) { StreamingInputValidation.Finite(supplied, nameof(suppliedMean)); mean = U(supplied); }
        else if (_wideMean is not null) { var value = _wideMean.Next(new RocBankValue(price), final); mean = U(value.Mantissa) << value.UpperShift; }
        else mean = U(_legacyMean!.Next(price, final));
        var residual = Round(U(price) - mean);
        var first = _first.Count == _length ? _first.Peek() : BigInteger.Zero;
        var second = _second.Count == _length ? _second.Peek() : BigInteger.Zero;
        var extrapolated = Round(Round(Round(2 * first) - second), 2);
        var high = Extreme(_max, extrapolated, true, final); var low = Extreme(_min, extrapolated, false, final);
        var range = high - low;
        var output = range.IsZero ? 0 : ExactMeanAccumulator.UnitRatio((100 * (extrapolated - low)) << 1074, range);
        var units = U(output); var slope = units - _previous;
        var trade = slope.Sign > 0 ? slope > _previousSlope ? Signal.StrongBuy : Signal.Buy
            : slope.Sign < 0 ? slope < _previousSlope ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            if (_first.Count == _length) _first.Dequeue(); _first.Enqueue(residual);
            if (_second.Count == _length) _second.Dequeue(); _second.Enqueue(first);
            _index++; _previous = units; _previousSlope = slope;
        }
        return (output, trade);
    }
    private BigInteger Extreme(LinkedList<(long Index, BigInteger Value)> window, BigInteger value, bool maximum, bool final)
    {
        var first = window.First;
        while (first is not null && first.Value.Index <= _index - _rangeLength) first = first.Next;
        var result = first is null ? value : maximum ? BigInteger.Max(value, first.Value.Value) : BigInteger.Min(value, first.Value.Value);
        if (final)
        {
            while (window.First is { } expired && expired.Value.Index <= _index - _rangeLength) window.RemoveFirst();
            while (window.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) window.RemoveLast();
            window.AddLast((_index, value));
        }
        return result;
    }
    internal void Reset()
    {
        _wideMean?.Reset(); _legacyMean?.Reset(); _first.Clear(); _second.Clear(); _max.Clear(); _min.Clear();
        _index = 0; _previous = _previousSlope = default;
    }
    public void Dispose() { _wideMean?.Dispose(); _legacyMean?.Dispose(); _first.Clear(); _second.Clear(); _max.Clear(); _min.Clear(); }
    internal static (List<double> Values, List<Signal> Signals) Calculate(StockData data, MovingAvgType kind, int length, IReadOnlyList<double>? means = null)
    {
        var (input, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var state = new StationaryLevelsOscillatorWindow(kind, length); var output = new List<double>(input.Count); var signals = new List<Signal>(input.Count);
        for (var i = 0; i < input.Count; i++) { var point = state.Next(input[i], true, means?[i]); output.Add(point.Value); signals.Add(point.Trade); }
        return (output, signals);
    }
}
