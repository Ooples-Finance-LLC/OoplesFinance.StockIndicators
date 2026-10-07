using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UniversalTradingWindow : IDisposable
{
    private readonly bool _snake;
    private readonly int _length, _rmsLength;
    private readonly double _lag, _drive, _feedback, _decay;
    private readonly MovingAvgType _kind;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly Queue<double> _prices = new();
    private readonly Queue<Scaled> _history = new(), _energy = new();
    private ExactMeanAccumulator _sum, _weighted, _squares;
    private Scaled _band1, _band2;
    private double _price1, _price2;
    private int _count;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.EhlersHannMovingAverage or MovingAvgType.WeightedMovingAverage;
    internal UniversalTradingWindow(MovingAvgType kind, int length, int rmsLength, double parameter, bool snake, bool external = false)
    {
        HighLowBandsWindow.ValidateShift(parameter); if (!snake && parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
        _length = Math.Max(1, length); _rmsLength = Math.Max(1, rmsLength); _snake = snake; _kind = kind; _lag = Math.Ceiling(parameter * _length);
        var cosine = Math.Cos(Math.Max(.01, Math.Min(.99, parameter * Math.PI / _length))); _decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        _drive = .5 * (1 - _decay); _feedback = Math.Cos(Math.Max(.01, Math.Min(.99, Math.PI / _length))) * (1 + _decay);
        if (!Supports(kind) && !external) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
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
    private Scaled Raw(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        if (_snake)
        {
            if (_count >= 3) { sum.AddProduct(price, _drive); sum.AddProduct(_price2, -_drive); _band1.Add(ref sum, _feedback); _band2.Add(ref sum, -_decay); }
        }
        else if (_lag != 0) { sum.Add(price); if (_prices.Count >= _lag) sum.Add(_prices.Peek(), -1); }
        var raw = Scaled.Round(sum);
        if (commit)
        {
            _price2 = _price1; _price1 = price; _band2 = _band1; _band1 = raw; if (_count < 3) _count++;
            if (!_snake && _lag > 0) { if (_prices.Count >= _lag) _prices.Dequeue(); _prices.Enqueue(price); }
        }
        return raw;
    }
    private Scaled Smooth(Scaled raw, bool commit)
    {
        if (_fallback is not null) return new(_fallback.Next(Publish(raw), commit), 0);
        var sum = _sum; var weighted = _weighted; Scaled value;
        if (_kind == MovingAvgType.EhlersHannMovingAverage)
        {
            // The full Hann mass is length+1. Sine-squared weights avoid cancellation at long periods.
            double Weight(int lag) { var sine = Math.Sin(Math.PI * ((lag + 1d) / (_length + 1d))); return 2 * sine * sine; }
            var total = new ExactMeanAccumulator(); raw.Add(ref total, Weight(0)); var lag = _history.Count;
            foreach (var prior in _history) { if (lag < _length) prior.Add(ref total, Weight(lag)); lag--; }
            value = Scaled.Round(total, _length + 1L);
        }
        else
        {
            weighted.Subtract(sum); raw.Add(ref weighted, (double)_length); raw.Add(ref sum); if (_history.Count == _length) _history.Peek().Add(ref sum, -1d);
            value = Scaled.Round(weighted, (long)_length * (_length + 1L) / 2);
        }
        if (commit) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(raw); _sum = sum; _weighted = weighted; }
        return value;
    }
    private static void Square(ref ExactMeanAccumulator sum, Scaled value, int sign)
    { var term = new ExactMeanAccumulator(); term.AddProduct(value.Mantissa, value.Mantissa, -sign); term.ScaleByPowerOfTwo(2 * value.Shift); sum.Subtract(term); }
    private (double Line, double Upper, double Lower) Finish(Scaled value, bool commit)
    {
        var squares = _squares; Square(ref squares, value, 1); if (_energy.Count == _rmsLength) Square(ref squares, _energy.Peek(), -1);
        var count = Math.Min(_rmsLength, _energy.Count + 1L); var rms = squares.SqrtMean(count);
        if (commit) { if (_energy.Count == _rmsLength) _energy.Dequeue(); _energy.Enqueue(value); _squares = squares; }
        return (Publish(value), rms, -rms);
    }
    internal double Prepare(double price, bool commit) => Publish(Raw(price, commit));
    internal (double Line, double Upper, double Lower) Finish(double value, bool commit) => Finish(new Scaled(value, 0), commit);
    internal (double Line, double Upper, double Lower) Next(double price, bool commit) => Finish(Smooth(Raw(price, commit), commit), commit);
    internal void Reset() { _prices.Clear(); _history.Clear(); _energy.Clear(); _sum = _weighted = _squares = default; _band1 = _band2 = default; _price1 = _price2 = 0; _count = 0; _fallback?.Reset(); }
    public void Dispose() => _fallback?.Dispose();
}
