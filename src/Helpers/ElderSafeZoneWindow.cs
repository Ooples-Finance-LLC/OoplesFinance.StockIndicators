using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ElderSafeZoneWindow : IDisposable
{
    private readonly int _noiseLength, _stopLength;
    private readonly double _factor;
    private readonly Average? _trend;
    private readonly Queue<(RocBankValue Minus, RocBankValue Plus)> _moves = new();
    private readonly Queue<(RocBankValue Floor, RocBankValue Ceiling)> _stops = new();
    private ExactMeanAccumulator _minusSum, _plusSum, _bullish, _bearish;
    private int _minusCount, _plusCount;
    private double _previousHigh, _previousLow;
    internal ElderSafeZoneWindow(MovingAvgType kind, int trendLength, int noiseLength, int stopLength, double factor, bool external = false)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _noiseLength = Math.Max(1, noiseLength); _stopLength = Math.Max(1, stopLength); _factor = factor; if (!external) _trend = new(kind, Math.Max(1, trendLength));
    }
    private static int Compare(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, -1); return sum.Sign; }
    private static RocBankValue Move(double first, double second)
    { var sum = new ExactMeanAccumulator(); sum.Add(first); sum.Add(second, -1); return sum.Sign > 0 ? RocBankValue.Round(sum) : default; }
    internal (double Value, Signal Signal) Next(double high, double low, double close, bool commit, double? externalTrend = null)
    {
        var minus = Move(_previousLow, low); var plus = Move(high, _previousHigh); var minusSum = _minusSum; var plusSum = _plusSum; var minusCount = _minusCount; var plusCount = _plusCount;
        if (_moves.Count == _noiseLength) { var old = _moves.Peek(); old.Minus.AddTo(ref minusSum, -1); old.Plus.AddTo(ref plusSum, -1); if (old.Minus.Mantissa > 0) minusCount--; if (old.Plus.Mantissa > 0) plusCount--; }
        minus.AddTo(ref minusSum); plus.AddTo(ref plusSum); if (minus.Mantissa > 0) minusCount++; if (plus.Mantissa > 0) plusCount++;
        var minusMean = minusCount == 0 ? default : RocBankValue.Round(minusSum, count: minusCount); var plusMean = plusCount == 0 ? default : RocBankValue.Round(plusSum, count: plusCount);
        RocBankValue Candidate(double previous, RocBankValue mean, int sign) { var sum = new ExactMeanAccumulator(); sum.AddProduct(mean.Mantissa, _factor, sign); sum.ScaleByPowerOfTwo(mean.UpperShift); sum.Add(previous); return RocBankValue.Round(sum); }
        var floor = Candidate(_previousLow, minusMean, -1); var ceiling = Candidate(_previousHigh, plusMean, 1); var highest = floor; var lowest = ceiling; var skip = _stops.Count == _stopLength;
        foreach (var retained in _stops) { if (skip) { skip = false; continue; } if (Compare(retained.Floor, highest) > 0) highest = retained.Floor; if (Compare(retained.Ceiling, lowest) < 0) lowest = retained.Ceiling; }
        var average = externalTrend.HasValue ? new RocBankValue(externalTrend.Value) : _trend!.Next(new(close), commit); var stop = Compare(new(close), average) >= 0 ? highest : lowest;
        var upper = Compare(average, stop) > 0 ? average : stop; var lower = Compare(average, stop) < 0 ? average : stop;
        var bullish = new ExactMeanAccumulator(); bullish.Add(close); upper.AddTo(ref bullish, -1); var bearish = new ExactMeanAccumulator(); bearish.Add(close); lower.AddTo(ref bearish, -1);
        var bullChange = bullish; bullChange.Subtract(_bullish); var bearChange = bearish; bearChange.Subtract(_bearish);
        var signal = bullish.Sign > 0 && bullChange.Sign > 0 ? Signal.StrongBuy : bearish.Sign < 0 && bearChange.Sign < 0 ? Signal.StrongSell : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_moves.Count == _noiseLength) _moves.Dequeue(); _moves.Enqueue((minus, plus)); if (_stops.Count == _stopLength) _stops.Dequeue(); _stops.Enqueue((floor, ceiling)); _minusSum = minusSum; _plusSum = plusSum; _minusCount = minusCount; _plusCount = plusCount; _previousHigh = high; _previousLow = low; _bullish = bullish; _bearish = bearish; }
        return (stop.Publish(), signal);
    }
    internal void Reset() { _trend?.Reset(); _moves.Clear(); _stops.Clear(); _minusSum = _plusSum = _bullish = _bearish = default; _minusCount = _plusCount = 0; _previousHigh = _previousLow = 0; }
    public void Dispose() => _trend?.Dispose();
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
