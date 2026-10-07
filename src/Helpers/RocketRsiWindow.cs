using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RocketRsiWindow : IDisposable
{
    private readonly int _length, _smoothLength;
    private readonly double _mult;
    private readonly bool _twoPole;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly Queue<double> _prices = new();
    private readonly Queue<Scaled> _averages = new(), _changes = new();
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private ExactMeanAccumulator _averageSum, _weighted, _signedSum, _absoluteSum;
    private Scaled _momentum, _drive, _filterDrive, _first, _second, _smoothed;
    private double _ratio;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 or MovingAvgType.WeightedMovingAverage;
    internal static void Validate(double obosLevel, double mult)
    {
        if (double.IsNaN(obosLevel) || double.IsInfinity(obosLevel)) throw new ArgumentOutOfRangeException(nameof(obosLevel));
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
    }
    internal RocketRsiWindow(MovingAvgType kind, int length, int smoothLength, double mult, bool external = false)
    {
        Validate(2, mult); _length = Math.Max(1, length); _smoothLength = Math.Max(1, smoothLength); _mult = mult; _twoPole = kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV2;
        if (!Supports(kind) && !external) _fallback = MovingAverageSmootherFactory.Create(kind, _smoothLength);
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(2, _smoothLength); var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle));
        _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
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
    private Scaled Argument(double price, bool commit)
    {
        var lag = _length - 1; var difference = new ExactMeanAccumulator();
        if (lag > 0 && _prices.Count == lag) { difference.Add(price); difference.Add(_prices.Peek(), -1); }
        var momentum = Scaled.Round(difference); var total = new ExactMeanAccumulator(); momentum.Add(ref total); _momentum.Add(ref total); var argument = Scaled.Round(total, 2);
        if (commit) { if (lag > 0) { if (_prices.Count == lag) _prices.Dequeue(); _prices.Enqueue(price); } _momentum = momentum; }
        return argument;
    }
    internal double Prepare(double price, bool commit) => Publish(Argument(price, commit));
    internal double Finish(double smoothed, bool commit) => Finish(new Scaled(smoothed, 0), false, commit);
    private static double LogOnePlus(double value)
    {
        var rounded = 1 + value; return rounded == 1 ? value : Math.Log(rounded) * (value / (rounded - 1)); // NOSONAR: S1244 - Exact equality detects when 1 + value rounds to 1; a tolerance would discard representable logarithmic corrections.
    }
    private double Finish(Scaled value, bool isChange, bool commit)
    {
        var difference = new ExactMeanAccumulator(); value.Add(ref difference); if (!isChange) _smoothed.Add(ref difference, -1d); var change = Scaled.Round(difference);
        var sum = _signedSum; var absolute = _absoluteSum; change.Add(ref sum); new Scaled(Math.Abs(change.Mantissa), change.Shift).Add(ref absolute);
        if (_changes.Count == _length) { var expired = _changes.Peek(); expired.Add(ref sum, -1d); new Scaled(Math.Abs(expired.Mantissa), expired.Shift).Add(ref absolute, -1d); }
        var ratio = absolute.IsExactlyZero ? _ratio : Math.Max(-.999, Math.Min(.999, sum.Ratio(absolute)));
        var fisher = .5 * (LogOnePlus(ratio) - LogOnePlus(-ratio)); var result = fisher * _mult;
        if (commit) { if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change); _signedSum = sum; _absoluteSum = absolute; _ratio = ratio; _smoothed = value; }
        return result;
    }
    internal double Next(double price, bool commit)
    {
        var argument = Argument(price, commit); Scaled filtered;
        if (_twoPole)
        {
            // Differentiate before this zero-seeded linear filter to retain settled changes.
            var delta = new ExactMeanAccumulator(); argument.Add(ref delta); _drive.Add(ref delta, -1d); var drive = Scaled.Round(delta);
            var filter = new ExactMeanAccumulator(); drive.Add(ref filter, _gain); _filterDrive.Add(ref filter, _gain); _first.Add(ref filter, _feedback1 << 1); _second.Add(ref filter, _feedback2 << 1); filter.ScaleByPowerOfTwo(-2149); filtered = Scaled.Round(filter);
            if (commit) { _drive = argument; _filterDrive = drive; _second = _first; _first = filtered; }
        }
        else if (_fallback is not null) filtered = new Scaled(_fallback.Next(Publish(argument), commit), 0);
        else
        {
            var sum = _averageSum; var weighted = _weighted; weighted.Subtract(sum); argument.Add(ref weighted, (double)_smoothLength); argument.Add(ref sum);
            if (_averages.Count == _smoothLength) _averages.Peek().Add(ref sum, -1d);
            filtered = Scaled.Round(weighted, (long)_smoothLength * (_smoothLength + 1L) / 2);
            if (commit) { if (_averages.Count == _smoothLength) _averages.Dequeue(); _averages.Enqueue(argument); _averageSum = sum; _weighted = weighted; }
        }
        return Finish(filtered, _twoPole, commit);
    }
    internal void Reset() { _prices.Clear(); _averages.Clear(); _changes.Clear(); _fallback?.Reset(); _averageSum = _weighted = _signedSum = _absoluteSum = default; _momentum = _drive = _filterDrive = _first = _second = _smoothed = default; _ratio = 0; }
    public void Dispose() => _fallback?.Dispose();
}
