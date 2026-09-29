using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BreakoutRsiWindow
{
    private readonly int _length, _volumeLength;
    private readonly Queue<BigInteger> _volumes = new();
    private readonly Queue<(BigInteger Positive, BigInteger Negative)> _powers = new();
    private BigInteger _volumeSum, _positiveSum, _negativeSum, _previousPower, _previousSlope;
    private double _previous;
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    internal BreakoutRsiWindow(int length, int volumeLength) { _length = Math.Max(1, length); _volumeLength = Math.Max(1, volumeLength); }
    internal (double Value, Signal Signal) Next(double price, double open, double high, double low, double close, double volume, bool commit)
    {
        var v = ExactVarianceWindow.Units(volume); var volumeSum = _volumeSum + v - (_volumes.Count == _volumeLength ? _volumes.Peek() : BigInteger.Zero);
        var range = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low); var body = ExactVarianceWindow.Units(close) - ExactVarianceWindow.Units(open);
        // Keep the ratio and both multipliers exact until the power stage is rounded.
        var numerator = ExactVarianceWindow.Units(price) * body * volumeSum;
        var power = range.IsZero ? BigInteger.Zero : RocBankValue.RoundUnits(numerator * range.Sign, BigInteger.Abs(range) * Unit);
        var positive = power > _previousPower ? BigInteger.Abs(power) : BigInteger.Zero;
        var negative = power < _previousPower ? BigInteger.Abs(power) : BigInteger.Zero;
        var full = _powers.Count == _length; var old = full ? _powers.Peek() : (Positive: BigInteger.Zero, Negative: BigInteger.Zero);
        var positiveSum = _positiveSum + positive - old.Positive; var negativeSum = _negativeSum + negative - old.Negative;
        var value = negativeSum.IsZero ? 100 : positiveSum.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(100 * positiveSum * Unit, positiveSum + negativeSum);
        var slope = ExactVarianceWindow.Units(value) - ExactVarianceWindow.Units(_previous);
        var signal = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 || _previous < 20 && value > 20 ? Signal.Buy : slope.Sign < 0 || _previous > 80 && value < 80 ? Signal.Sell : Signal.None;
        if (commit)
        {
            if (_volumes.Count == _volumeLength) _volumes.Dequeue(); _volumes.Enqueue(v); _volumeSum = volumeSum;
            if (full) _powers.Dequeue(); _powers.Enqueue((positive, negative)); _positiveSum = positiveSum; _negativeSum = negativeSum;
            _previousPower = power; _previousSlope = slope; _previous = value;
        }
        return (value, signal);
    }
    internal static double Price(double open, double high, double low, double close)
    { var sum = new ExactMeanAccumulator(); sum.Add(open); sum.Add(high); sum.Add(low); sum.Add(close); return sum.Mean(4); }
    internal static (List<double> Input, List<double> High, List<double> Low, List<double> Open, List<double> Close, List<double> Volume) Inputs(StockData data)
    {
        if (data.ChainedValues.Count > 0) return CalculationsHelper.GetInputValuesList(InputName.FullTypicalPrice, data);
        var prices = Enumerable.Range(0, data.Count).Select(i => Price(data.OpenPrices[i], data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i])).ToList();
        var (high, low) = CalculationsHelper.GetCustomRangeLists(prices, data.HighPrices, data.LowPrices);
        return (prices, high, low, data.OpenPrices, data.ClosePrices, data.Volumes);
    }
    internal void Reset() { _volumes.Clear(); _powers.Clear(); _volumeSum = _positiveSum = _negativeSum = _previousPower = _previousSlope = default; _previous = 0; }
}
