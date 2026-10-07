using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HilbertNoiseWindow : IDisposable
{
    private readonly HilbertPhaseWindow _hilbert;
    private readonly Average? _average;
    private Scaled _range, _energy;
    private ExactMeanAccumulator _distance;
    private double _snr;
    internal HilbertNoiseWindow(MovingAvgType kind, int length, bool externalAverage = false)
    { length = Math.Max(1, length); _hilbert = new(length, .635, .338, 1, false); if (!externalAverage) _average = new(kind, length); }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal void Add(ref ExactMeanAccumulator sum, BigInteger coefficient)
        { var negative = new ExactMeanAccumulator(); negative.Add(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum, long divisor = 1)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(divisor); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(divisor); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(divisor); }
            return new(value, shift);
        }
    }
    private static double Publish(Scaled value) { var sum = new ExactMeanAccumulator(); value.Add(ref sum); return sum.Mean(1); }
    private static void Square(ref ExactMeanAccumulator sum, Scaled value, double coefficient = 1)
    { var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, ExactVarianceWindow.Units(value.Mantissa) * ExactVarianceWindow.Units(coefficient)); term.ScaleByPowerOfTwo(2 * value.Shift - 2148); var opposite = new ExactMeanAccumulator(); opposite.Subtract(term); sum.Subtract(opposite); }
    private static double LogRatio(ExactMeanAccumulator numerator, ExactMeanAccumulator denominator)
    {
        if (numerator.Sign <= 0 || denominator.Sign <= 0) return 0;
        var shift = 0; var ratio = numerator.Ratio(denominator); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
        while (ratio < lower) { numerator.ScaleByPowerOfTwo(512); shift -= 512; ratio = numerator.Ratio(denominator); }
        while (double.IsInfinity(ratio) || ratio >= upper) { numerator.ScaleByPowerOfTwo(-512); shift += 512; ratio = numerator.Ratio(denominator); }
        return Math.Log10(ratio) + shift * Math.Log10(2);
    }
    internal (double Value, Signal Signal) Next(double price, double high, double low, bool commit, double? externalAverage = null)
    {
        _hilbert.Next(price, commit, true); var energyInput = Scaled.Round(_hilbert.NoiseEnergy); var energySum = new ExactMeanAccumulator(); energyInput.Add(ref energySum, .2); _energy.Add(ref energySum, .8); var energy = Scaled.Round(energySum);
        var rangeDifference = new ExactMeanAccumulator(); rangeDifference.Add(high); rangeDifference.Add(low, -1); var difference = Scaled.Round(rangeDifference); var rangeSum = new ExactMeanAccumulator(); difference.Add(ref rangeSum, .2); _range.Add(ref rangeSum, .8); var range = Scaled.Round(rangeSum);
        var numerator = new ExactMeanAccumulator(); energy.Add(ref numerator); var denominator = new ExactMeanAccumulator(); Square(ref denominator, range); var level = 10 * LogRatio(numerator, denominator) + 1.9; var smoothed = new ExactMeanAccumulator(); smoothed.AddProduct(level, .25); smoothed.AddProduct(_snr, .75); var snr = range.Mantissa != 0 ? smoothed.Mean(1) : 0;
        var mean = externalAverage.HasValue ? new RocBankValue(externalAverage.Value) : _average!.Next(new RocBankValue(price), commit); var distance = new ExactMeanAccumulator(); distance.Add(price); mean.AddTo(ref distance, -1); var acceleration = distance; acceleration.Subtract(_distance); var signal = snr < 1.9 ? Signal.None : distance.Sign > 0 ? acceleration.Sign > 0 ? Signal.StrongBuy : Signal.Buy : distance.Sign < 0 ? acceleration.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { _range = range; _energy = energy; _distance = distance; _snr = snr; } return (snr, signal);
    }
    internal void Reset() { _hilbert.Reset(); _average?.Reset(); _range = _energy = default; _distance = default; _snr = 0; }
    public void Dispose() { _average?.Dispose(); _hilbert.Reset(); }
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
