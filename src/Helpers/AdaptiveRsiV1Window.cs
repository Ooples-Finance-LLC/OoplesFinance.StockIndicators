using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveRsiV1Window
{
    private readonly MamaWindow _mama = new(.5, .05);
    private readonly double _fraction;
    private readonly int _capacity;
    private readonly bool _fisher;
    private readonly List<double> _prices = new();
    private double _ema, _ema2, _fish, _fish2;
    private static int Length(double value) => value <= 0 ? 0 : value >= int.MaxValue - 1d ? int.MaxValue - 1 : (int)Math.Ceiling(value);
    internal AdaptiveRsiV1Window(double fraction = .5, bool fisher = false)
    {
        if (double.IsNaN(fraction) || double.IsInfinity(fraction)) throw new ArgumentOutOfRangeException(nameof(fraction));
        _fraction = fraction; _fisher = fisher; _capacity = Math.Max(1, Length(50 * fraction) + 1);
    }
    internal (double Value, double Average, Signal Signal) Next(double price, bool commit)
    {
        var period = _mama.Next(price, commit).Values.SmoothPeriod; var length = Length(_fraction * period); var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator();
        double At(int lag) => lag == 0 ? price : lag <= _prices.Count ? _prices[_prices.Count - lag] : 0;
        for (var lag = 0; lag < length && lag <= _prices.Count; lag++)
        {
            var current = At(lag); var previous = At(lag + 1);
            if (current > previous) { numerator.Add(current, 100); numerator.Add(previous, -100); denominator.Add(current); denominator.Add(previous, -1); }
            else if (current < previous) { denominator.Add(previous); denominator.Add(current, -1); }
        }
        var rsi = denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator);
        var alpha = Math.Max(.01, Math.Min(.99, 2d / (Math.Ceiling(period) + 1))); var sum = new ExactMeanAccumulator(); sum.AddProduct(rsi, alpha); sum.AddProduct(_ema, 1 - alpha); var average = sum.Mean(1);
        var argument = Math.Max(-.999, Math.Min(.999, 1.5 * (2 * (rsi / 100 - .5)))); var fish = .5 * Math.Log((1 + argument) / (1 - argument));
        var signal = _fisher ? SignalHelper.GetCompareSignal(fish - _fish, _fish - _fish2) : SignalHelper.GetRsiSignal(average - _ema, _ema - _ema2, average, _ema, 70, 30);
        if (commit) { if (_prices.Count == _capacity) _prices.RemoveAt(0); _prices.Add(price); _ema2 = _ema; _ema = average; _fish2 = _fish; _fish = fish; }
        return (_fisher ? fish : rsi, average, signal);
    }
    internal void Reset() { _mama.Reset(); _prices.Clear(); _ema = _ema2 = _fish = _fish2 = 0; }
}
