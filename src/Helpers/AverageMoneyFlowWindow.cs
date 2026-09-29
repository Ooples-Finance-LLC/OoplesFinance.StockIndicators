using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AverageMoneyFlowWindow : IDisposable
{
    private readonly int _length; private readonly Average? _volumeMean, _changeMean, _smooth;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _count; private double _previousPrice; private RocBankValue _previous;
    internal AverageMoneyFlowWindow(MovingAvgType kind, int length, int smoothLength, bool external = false)
    { _length = Math.Max(1, length); if (!external) { _volumeMean = new(kind, _length); _changeMean = new(kind, _length); _smooth = new(kind, Math.Max(1, smoothLength)); } }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool commit)
    {
        var expiry = _count - _length + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (commit) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast(); deque.AddLast((_count, value)); } return result;
    }
    internal static double LogFlow(RocBankValue volume, RocBankValue movement)
    {
        if (volume.Mantissa == 0 || movement.Mantissa == 0) return 0;
        var volumeUnits = ExactVarianceWindow.Units(volume.Mantissa) << volume.UpperShift;
        var movementUnits = ExactVarianceWindow.Units(movement.Mantissa) << movement.UpperShift;
        return ProductLog.Of(BigInteger.Abs(volumeUnits * movementUnits)) * movementUnits.Sign;
    }
    internal (double Value, Signal Signal, double Scaled) Next(double price, double volume, bool commit, double? externalVolume = null, double? externalChange = null, double? externalSmooth = null)
    {
        var difference = new ExactMeanAccumulator(); if (_count > 0) { difference.Add(price); difference.Add(_previousPrice, -1); }
        var averageVolume = externalVolume.HasValue ? new RocBankValue(externalVolume.Value) : _volumeMean!.Next(new(volume), commit);
        var averageChange = externalChange.HasValue ? new RocBankValue(externalChange.Value) : _changeMean!.Next(RocBankValue.Round(difference), commit);
        var flow = LogFlow(averageVolume, averageChange); var high = Extreme(_highs, flow, true, commit); var low = Extreme(_lows, flow, false, commit);
        var position = high == low ? 0 : ClampedRangePosition.Percent(flow, low, high); var scaled = position * 2 - 100;
        var output = externalSmooth.HasValue ? new RocBankValue(externalSmooth.Value) : _smooth!.Next(new(scaled), commit);
        var current = new ExactMeanAccumulator(); output.AddTo(ref current); var change = current; _previous.AddTo(ref change, -1);
        var signal = current.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : current.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : current.Sign > 0 ? Signal.Buy : current.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _count++; _previousPrice = price; _previous = output; } return (output.Publish(), signal, scaled);
    }
    internal static double[][] Components(StockData data, List<double> input, List<double> volumes, MovingAvgType kind, int length, int smoothLength)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); var caller = data.CaptureInputSeries();
        double[] Mean(List<double> values, int period) { var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), period)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values).ToArray(); data.RestoreInputSeries(caller); return result; }
        var averageVolume = Mean(volumes, length); var changes = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) { var difference = new ExactMeanAccumulator(); if (i > 0) { difference.Add(input[i]); difference.Add(input[i - 1], -1); } changes.Add(RocBankValue.Round(difference).Publish()); }
        var averageChange = Mean(changes, length); using var window = new AverageMoneyFlowWindow(kind, length, smoothLength, true); var scaled = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++) scaled.Add(window.Next(input[i], volumes[i], true, averageVolume[i], averageChange[i], 0).Scaled);
        return new[] { averageVolume, averageChange, Mean(scaled, smoothLength) };
    }
    internal void Reset() { _volumeMean?.Reset(); _changeMean?.Reset(); _smooth?.Reset(); _highs.Clear(); _lows.Clear(); _count = 0; _previousPrice = 0; _previous = default; }
    public void Dispose() { _volumeMean?.Dispose(); _changeMean?.Dispose(); _smooth?.Dispose(); }

    // The product is an exact positive integer in units of 2^-2148. Reduce to
    // [1,2), then enclose log with directed fixed-point atanh-series bounds.
    // For 0 <= z <= 1/3 the uncomputed tail is below 3*z^(2N+1)/(2N+1).
    // Increase precision until both interval ends round to the same binary64.
    private static class ProductLog
    {
        private static readonly (BigInteger Low, BigInteger High) LogTwo128 = Bounds(BigInteger.One, new BigInteger(3), 128);
        private static BigInteger Ceiling(BigInteger numerator, BigInteger denominator) => (numerator + denominator - 1) / denominator;
        private static (BigInteger Low, BigInteger High) Bounds(BigInteger numerator, BigInteger denominator, int precision)
        {
            if (numerator.IsZero) return (BigInteger.Zero, BigInteger.Zero);
            var unit = BigInteger.One << precision; var low = numerator * unit / denominator; var high = Ceiling(numerator * unit, denominator);
            var squareLow = low * low / unit; var squareHigh = Ceiling(high * high, unit); var powerLow = low; var powerHigh = high; var sumLow = BigInteger.Zero; var sumHigh = BigInteger.Zero;
            for (var term = 0; term < precision; term++) { var odd = 2 * term + 1; sumLow += powerLow / odd; sumHigh += Ceiling(powerHigh, odd); powerLow = powerLow * squareLow / unit; powerHigh = Ceiling(powerHigh * squareHigh, unit); }
            return (2 * sumLow, 2 * sumHigh + Ceiling(3 * powerHigh, 2 * precision + 1));
        }
        internal static double Of(BigInteger product)
        {
            var bytes = product.ToByteArray(); var last = bytes.Length - 1; while (last > 0 && bytes[last] == 0) last--;
            var bits = last * 8; for (var head = bytes[last]; head != 0; head >>= 1) bits++;
            var power = BigInteger.One << (bits - 1); var exponent = bits - 1 - 2148;
            for (var precision = 128; ; precision *= 2)
            {
                var mantissa = Bounds(product - power, product + power, precision); var logTwo = precision == 128 ? LogTwo128 : Bounds(BigInteger.One, new BigInteger(3), precision);
                var low = mantissa.Low + exponent * (exponent < 0 ? logTwo.High : logTwo.Low); var high = mantissa.High + exponent * (exponent < 0 ? logTwo.Low : logTwo.High);
                var lower = ExactMeanAccumulator.UnitRatio(low << 1074, BigInteger.One << precision); var upper = ExactMeanAccumulator.UnitRatio(high << 1074, BigInteger.One << precision);
                if (lower == upper) return lower;
            }
        }
    }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
