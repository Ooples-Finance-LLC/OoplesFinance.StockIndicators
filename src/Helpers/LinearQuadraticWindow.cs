using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class LinearQuadraticWindow : IDisposable
{
    private readonly ExactLinearFitWindow _linear;
    private readonly QuadraticProjectionWindow _quadratic;
    private readonly RocBankAverage _signal;
    private BigInteger _previous;
    internal Signal LastSignal { get; private set; }
    internal LinearQuadraticWindow(MovingAvgType kind, int length, int signalLength)
    {
        _linear = new(length, observedHistory: true);
        _quadratic = new(kind, length, observedHistory: true);
        _signal = new(kind, signalLength, 1, observedHistory: true);
    }
    internal double Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        var linear = _linear.Next(price, final).RoundedLastUnits;
        _quadratic.Next(price, final);
        var difference = RocBankValue.RoundUnits(_quadratic.LastExtended - linear, BigInteger.One);
        var exact = new ExactMeanAccumulator(); exact.Add(double.Epsilon, difference);
        var average = _signal.Next(RocBankValue.Round(exact), final);
        var mean = ExactVarianceWindow.Units(average.Mantissa) << average.UpperShift;
        // Public histogram subtracts the signal twice, rounding each stage.
        var oscillator = RocBankValue.RoundUnits(difference - mean, BigInteger.One);
        var histogram = RocBankValue.RoundUnits(oscillator - mean, BigInteger.One);
        LastSignal = histogram.Sign > 0 ? histogram > _previous ? Signal.StrongBuy : Signal.Buy
            : histogram.Sign < 0 ? histogram < _previous ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final) _previous = histogram;
        return ExactMeanAccumulator.UnitRatio(histogram, BigInteger.One);
    }
    internal void Reset() { _linear.Reset(); _quadratic.Reset(); _signal.Reset(); _previous = default; LastSignal = Signal.None; }
    public void Dispose() { _linear.Dispose(); _quadratic.Dispose(); _signal.Dispose(); }
    internal static (List<double> Values, List<Signal> Signals) Calculate(StockData data, MovingAvgType kind, int length, int signalLength)
    {
        var (input, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var state = new LinearQuadraticWindow(kind, length, signalLength);
        var output = new List<double>(input.Count); var signals = new List<Signal>(input.Count);
        foreach (var value in input) { output.Add(state.Next(value, true)); signals.Add(state.LastSignal); }
        return (output, signals);
    }
}
