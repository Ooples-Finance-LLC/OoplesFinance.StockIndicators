using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KaseRatioWindow : IDisposable
{
    private readonly BigInteger _sqrt;
    private readonly Mean? _volume, _atr;
    private double _high, _low, _price;
    private BigInteger _up, _down;
    private bool _hasPrevious;
    internal KaseRatioWindow(MovingAvgType kind, int length, bool external = false)
    {
        length = Math.Max(1, length); _sqrt = U(Math.Sqrt(length));
        if (!external) { _volume = new(kind, length); _atr = new(kind, length); }
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger value, BigInteger divisor) => RocBankValue.RoundUnits(value, divisor);
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    private static BigInteger Range(double high, double low, double previous) => Round(BigInteger.Max(U(high) - U(low),
        BigInteger.Max(BigInteger.Abs(U(high) - U(previous)), BigInteger.Abs(U(low) - U(previous)))), BigInteger.One);
    private BigInteger Ratio(double numerator, double divisor, BigInteger volume)
    {
        var denominator = U(divisor) * volume * _sqrt;
        return Round((U(numerator) << 3222) * denominator.Sign, BigInteger.Abs(denominator));
    }
    internal (double Up, double Down, Signal Trade) Next(double high, double low, double price, double volume, bool final,
        double? externalVolume = null, double? externalAtr = null)
    {
        var meanVolume = externalVolume.HasValue ? U(externalVolume.Value) : _volume!.Next(U(volume), final);
        var atr = externalAtr.HasValue ? U(externalAtr.Value) : _atr!.Next(Range(high, low, _hasPrevious ? _price : price), final);
        var up = atr.Sign > 0 && !meanVolume.IsZero && low != 0 ? Ratio(_hasPrevious ? _high : 0, low, meanVolume) : _up;
        var down = atr.Sign > 0 && !meanVolume.IsZero && _hasPrevious && _low != 0 ? Ratio(high, _low, meanVolume) : _down;
        var spread = up - down; var previous = _up - _down;
        var trade = spread.Sign > 0 && spread > previous ? Signal.StrongBuy : spread.Sign < 0 && spread < previous ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _high = high; _low = low; _price = price; _up = up; _down = down; _hasPrevious = true; }
        return (Publish(up), Publish(down), trade);
    }
    internal static (double[] Up, double[] Down, Signal[] Trades) Compute(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var (input, high, low, _, volume) = CalculationsHelper.GetInputValuesList(data);
        var external = callbacks && ComponentAverage.HasOverrides || !StrengthWindow.Supports(kind);
        double[] Average(double[] source) => (callbacks ? ComponentAverage.Take(source, length)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, length, source.ToList()).ToArray();
        var meanVolume = external ? Average(volume.ToArray()) : null;
        var atr = external ? Average(Enumerable.Range(0, input.Count).Select(i => Publish(Range(high[i], low[i], input[Math.Max(0, i - 1)]))).ToArray()) : null;
        var ups = new double[input.Count]; var downs = new double[input.Count]; var trades = new Signal[input.Count];
        using var window = new KaseRatioWindow(kind, length, external);
        for (var i = 0; i < input.Count; i++) (ups[i], downs[i], trades[i]) = window.Next(high[i], low[i], input[i], volume[i], true, meanVolume?[i], atr?[i]);
        return (ups, downs, trades);
    }
    internal void Reset() { _volume?.Reset(); _atr?.Reset(); _high = _low = _price = 0; _up = _down = default; _hasPrevious = false; }
    public void Dispose() { _volume?.Dispose(); _atr?.Dispose(); }
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<BigInteger> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private BigInteger _sum, _weighted, _previous; private long _count;
        internal Mean(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal BigInteger Next(BigInteger value, bool final)
        {
            if (_fallback is not null) return U(_fallback.Next(Publish(value), final));
            var sum = _sum; var weighted = _weighted; BigInteger result;
            if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            {
                weighted = weighted - sum + value * _length;
                sum = sum + value - (_history.Count == _length ? _history.Peek() : BigInteger.Zero);
                result = _kind == MovingAvgType.WeightedMovingAverage ? Round(weighted, (long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? BigInteger.Zero : Round(sum, _length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = Round(sum, _count + 1); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = Round(_previous * (_length - 1L) + value * (ema ? 2 : 1), ema ? _length + 1L : _length);
            }
            if (final)
            {
                _sum = sum; _weighted = weighted; _previous = result; _count++;
                if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
                { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
}
