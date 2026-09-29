using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendExtractionWindow : IDisposable
{
    private readonly long _period, _mass;
    private readonly MovingAvgType _kind;
    private readonly double _drive, _feedback, _decay;
    private readonly Queue<Scaled> _history = new();
    private readonly IMovingAverageSmoother? _fallback;
    private ExactMeanAccumulator _sum, _weighted;
    private Scaled _band1, _band2;
    private double _price1, _price2;
    private int _count;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
    internal static int ExternalPeriod(int length) => (int)Math.Min(int.MaxValue, 2L * Math.Max(1, length));
    internal TrendExtractionWindow(MovingAvgType kind, int length, double delta, bool external = false)
    {
        HighLowBandsWindow.ValidateShift(delta); length = Math.Max(1, length); _period = 2L * length; _mass = (_period / 2) * (_period + 1); _kind = kind;
        var beta = Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length)));
        var gamma = 1 / Math.Cos(Math.Max(.01, Math.Min(.99, 4 * Math.PI * delta / length)));
        _decay = Math.Max(.01, Math.Min(.99, gamma - Math.Sqrt(gamma * gamma - 1))); _drive = .5 * (1 - _decay); _feedback = beta * (1 + _decay);
        if (!Supports(kind) && !external) _fallback = MovingAverageSmootherFactory.Create(kind, ExternalPeriod(length));
    }
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
    private Scaled Band(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        if (_count >= 2) { sum.AddProduct(price, _drive); sum.AddProduct(_price2, -_drive); _band1.Add(ref sum, _feedback); _band2.Add(ref sum, -_decay); }
        var band = Scaled.Round(sum);
        if (commit) { _price2 = _price1; _price1 = price; _band2 = _band1; _band1 = band; if (_count < 2) _count++; }
        return band;
    }
    internal double Prepare(double price, bool commit) => Publish(Band(price, commit));
    internal (double Trend, double Band) Next(double price, bool commit)
    {
        var band = Band(price, commit); double trend;
        if (_fallback is not null) trend = _fallback.Next(Publish(band), commit);
        else
        {
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); band.Add(ref weighted, (double)_period);
            if (_history.Count == _period) _history.Peek().Add(ref sum, -1d); band.Add(ref sum);
            trend = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Mean(_mass) : _history.Count + 1L < _period ? 0 : sum.Mean(_period);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _period) _history.Dequeue(); _history.Enqueue(band); }
        }
        return (trend, Publish(band));
    }
    internal void Reset() { _history.Clear(); _sum = _weighted = default; _band1 = _band2 = default; _price1 = _price2 = 0; _count = 0; _fallback?.Reset(); }
    public void Dispose() => _fallback?.Dispose();
}
