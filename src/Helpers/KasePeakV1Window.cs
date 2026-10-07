using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class KasePeakV1Window : IDisposable
{
    private readonly int _length;
    private readonly KaseConvergenceWindow _peak;
    private readonly Queue<BigInteger> _history = new();
    private BigInteger _sum, _squares, _previousPeak, _previousLevel;
    internal KasePeakV1Window(int length, int smoothLength)
    {
        _length = Math.Max(1, length);
        _peak = new(MovingAvgType.SimpleMovingAverage, _length, smoothLength, 1);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger numerator, BigInteger denominator) => RocBankValue.RoundUnits(numerator, denominator);
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    private static BigInteger Root(BigInteger numerator, BigInteger denominator)
    {
        for (var shift = 0; ; shift += 32)
        {
            var value = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift));
            if (!double.IsInfinity(value)) return U(value) << shift;
        }
    }
    internal (double Level, double Peak, Signal Trade) Next(double high, double low, double price, bool final)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low));
        StreamingInputValidation.Finite(price, nameof(price));
        var peak = _peak.NextPeak(high, low, price, final);
        var expired = _history.Count == _length ? _history.Peek() : BigInteger.Zero;
        var sum = _sum + peak - expired; var squares = _squares + peak * peak - expired * expired;
        var count = Math.Min(_length, _history.Count + 1L);
        var mean = count < _length ? BigInteger.Zero : Round(sum, _length);
        var deviation = Root(count * squares - sum * sum, new BigInteger(count) * count);
        var radius = Round(U(1.33) * deviation, BigInteger.One << 1074);
        var upper = BigInteger.Max(U(2.08), Round(mean + radius, BigInteger.One));
        var lower = BigInteger.Min(U(-1.92), Round(mean - radius, BigInteger.One));
        var level = _previousPeak.Sign >= 0 && peak.Sign > 0 ? upper
            : _previousPeak.Sign <= 0 && peak.Sign < 0 ? lower : BigInteger.Zero;
        var trade = level.Sign > 0 ? level > _previousLevel ? Signal.StrongBuy : Signal.Buy
            : level.Sign < 0 ? level < _previousLevel ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(peak);
            _sum = sum; _squares = squares; _previousPeak = peak; _previousLevel = level;
        }
        return (Publish(level), Publish(peak), trade);
    }
    internal void Reset() { _peak.Reset(); _history.Clear(); _sum = _squares = _previousPeak = _previousLevel = default; }
    public void Dispose() { _peak.Dispose(); _history.Clear(); }
    internal static (List<double> Levels, List<double> Peaks, List<Signal> Signals) Calculate(StockData data, int length, int smoothLength)
    {
        var (input, high, low, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var values in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        using var state = new KasePeakV1Window(length, smoothLength);
        var levels = new List<double>(input.Count); var peaks = new List<double>(input.Count); var signals = new List<Signal>(input.Count);
        for (var i = 0; i < input.Count; i++)
        { var point = state.Next(high[i], low[i], input[i], true); levels.Add(point.Level); peaks.Add(point.Peak); signals.Add(point.Trade); }
        return (levels, peaks, signals);
    }
}
