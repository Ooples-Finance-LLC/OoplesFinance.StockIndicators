using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class GroverWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly bool _cycle, _carryFlat;
    private readonly BigInteger _mult;
    private readonly Average _atr, _oscillator, _gains, _losses;
    private bool _started, _rsiStarted;
    private BigInteger _previousPrice, _trail, _previousDiff, _previousSmooth, _previousLine, _previousSlope;
    internal GroverWindow(MovingAvgType kind, int length, int smoothLength, double mult, bool cycle)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); _cycle = cycle; _mult = ExactVarianceWindow.Units(mult);
        _carryFlat = smoothLength > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
        _atr = new(kind, length); _oscillator = new(kind, smoothLength); _gains = new(kind, smoothLength); _losses = new(kind, smoothLength);
    }
    private static BigInteger Round(BigInteger value) => RocBankValue.RoundUnits(value, BigInteger.One);
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    internal static BigInteger TrueRange(double high, double low, double previous)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous);
        return Round(BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p))));
    }
    internal (BigInteger Trail, BigInteger Oscillator, Signal Trade) StepTrail(double price, double high, double low, bool final, double? externalAtr = null)
    {
        var current = ExactVarianceWindow.Units(price);
        var range = TrueRange(high, low, _started ? Publish(_previousPrice) : price);
        var atr = externalAtr.HasValue ? ExactVarianceWindow.Units(externalAtr.Value) : _atr.Next(range, final);
        var previous = _started ? _trail : current;
        if (!_cycle && previous.IsZero) previous = _started ? _previousPrice : BigInteger.Zero;
        var diff = current - previous;
        var trail = diff.IsZero ? previous : RocBankValue.RoundUnits(previous * Unit - diff.Sign * atr * _mult, Unit);
        var oscillator = Round(current - trail);
        var trade = diff.Sign > 0 && diff > _previousDiff ? Signal.StrongBuy : diff.Sign < 0 && diff < _previousDiff ? Signal.StrongSell : diff.Sign > 0 ? Signal.Buy : diff.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _started = true; _previousPrice = current; _trail = trail; _previousDiff = diff; }
        return (trail, oscillator, trade);
    }
    internal BigInteger Smooth(BigInteger oscillator, bool final, double? external = null)
        => external.HasValue ? ExactVarianceWindow.Units(external.Value) : _oscillator.Next(oscillator, final);
    internal (double Line, Signal Trade) Rsi(BigInteger smooth, bool final, double? gainOverride = null, double? lossOverride = null)
    {
        var change = _rsiStarted ? Round(smooth - _previousSmooth) : BigInteger.Zero;
        var gain = gainOverride.HasValue ? ExactVarianceWindow.Units(gainOverride.Value) : _gains.Next(BigInteger.Max(change, BigInteger.Zero), final);
        var loss = lossOverride.HasValue ? ExactVarianceWindow.Units(lossOverride.Value) : _losses.Next(BigInteger.Max(-change, BigInteger.Zero), final);
        var total = gain + loss;
        var line = _carryFlat && _rsiStarted && smooth == _previousSmooth && !gainOverride.HasValue && !lossOverride.HasValue ? Publish(_previousLine)
            : total.IsZero ? 100 : ExactMeanAccumulator.UnitRatio(100 * gain * Unit, total);
        var units = ExactVarianceWindow.Units(line); var slope = units - _previousLine;
        var trade = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 || _previousLine < 20 * Unit && units > 20 * Unit ? Signal.Buy : slope.Sign < 0 || _previousLine > 80 * Unit && units < 80 * Unit ? Signal.Sell : Signal.None;
        if (final) { _rsiStarted = true; _previousSmooth = smooth; _previousLine = units; _previousSlope = slope; }
        return (line, trade);
    }
    internal (double Line, Signal Trade) Next(double price, double high, double low, bool final)
    {
        var point = StepTrail(price, high, low, final);
        return _cycle ? Rsi(Smooth(point.Oscillator, final), final) : (Publish(point.Trail), point.Trade);
    }
    internal static (double[] Line, Signal[] Trades) Calculate(List<double> prices, List<double> highs, List<double> lows, MovingAvgType kind, int length, int smoothLength, double mult, bool cycle, bool callbacks)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        var window = new GroverWindow(kind, length, smoothLength, mult, cycle); var line = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        { for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], highs[i], lows[i], true); line[i] = point.Line; trades[i] = point.Trade; } return (line, trades); }
        var ranges = prices.Select((price, i) => Publish(TrueRange(highs[i], lows[i], i == 0 ? price : prices[i - 1]))).ToArray();
        var atr = ComponentAverage.Take(ranges, length); var oscillators = new BigInteger[prices.Count];
        for (var i = 0; i < prices.Count; i++) { var point = window.StepTrail(prices[i], highs[i], lows[i], true, atr is null ? null : atr[i]); line[i] = Publish(point.Trail); trades[i] = point.Trade; oscillators[i] = point.Oscillator; }
        if (!cycle) return (line, trades);
        var externalSmooth = ComponentAverage.Take(oscillators.Select(Publish).ToArray(), smoothLength); var smooth = new BigInteger[prices.Count];
        for (var i = 0; i < prices.Count; i++) smooth[i] = window.Smooth(oscillators[i], true, externalSmooth is null ? null : externalSmooth[i]);
        var gains = new double[prices.Count]; var losses = new double[prices.Count];
        for (var i = 1; i < prices.Count; i++) { var change = Round(smooth[i] - smooth[i - 1]); gains[i] = Publish(BigInteger.Max(change, BigInteger.Zero)); losses[i] = Publish(BigInteger.Max(-change, BigInteger.Zero)); }
        var gain = ComponentAverage.Take(gains, smoothLength); var loss = ComponentAverage.Take(losses, smoothLength);
        for (var i = 0; i < prices.Count; i++) { var point = window.Rsi(smooth[i], true, gain is null ? null : gain[i], loss is null ? null : loss[i]); line[i] = point.Line; trades[i] = point.Trade; }
        return (line, trades);
    }
    internal void Reset()
    { _atr.Reset(); _oscillator.Reset(); _gains.Reset(); _losses.Reset(); _started = _rsiStarted = false; _previousPrice = _trail = _previousDiff = _previousSmooth = _previousLine = _previousSlope = default; }
    private sealed class Average
    {
        private readonly MovingAvgType _kind; private readonly int _length; private readonly Queue<BigInteger> _history = new();
        private BigInteger _sum, _weighted, _previous; private int _count;
        internal Average(MovingAvgType kind, int length) { _kind = kind; _length = length; }
        internal BigInteger Next(BigInteger value, bool final)
        {
            var sum = _sum; var weighted = _weighted; BigInteger result;
            var rolling = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (rolling)
            {
                weighted = weighted - sum + _length * value; if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.RoundUnits(weighted, new BigInteger((long)_length * (_length + 1L) / 2))
                    : _history.Count < _length - 1 ? BigInteger.Zero : RocBankValue.RoundUnits(sum, new BigInteger(_length));
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = RocBankValue.RoundUnits(sum, new BigInteger(_count + 1)); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = RocBankValue.RoundUnits((_length - 1L) * _previous + (ema ? 2 : 1) * value, new BigInteger(ema ? _length + 1L : _length)); }
            if (final) { _sum = sum; _weighted = weighted; _previous = result; if (_count < _length) _count++; if (rolling) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    }
}
