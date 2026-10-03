using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// Extremum ties select the newest observation. Each direction carries its own
// age-dependent range average, even when a new extremum resets its walk to zero.
internal sealed class DrunkardWalkWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length;
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private long _index; private double _previousPrice; private BigInteger _upAverage, _downAverage, _previousSpread;
    internal DrunkardWalkWindow(int length) => _length = Math.Max(1, length);
    private (double Value, long Age) Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _index - _length + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var best = first is null || (maximum ? value >= first.Value.Value : value <= first.Value.Value) ? (_index, value) : first.Value;
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return (best.Item2, _index - best.Item1);
    }
    private static BigInteger Blend(BigInteger range, BigInteger previous, long age)
    {
        var gain = age == 0 ? 0 : 1d / age;
        var current = RocBankValue.RoundUnits(range * ExactVarianceWindow.Units(gain), Unit);
        var retained = RocBankValue.RoundUnits(previous * ExactVarianceWindow.Units(1 - gain), Unit);
        return RocBankValue.RoundUnits(current + retained, BigInteger.One);
    }
    private static BigInteger Walk(BigInteger distance, BigInteger average, long age)
    {
        if (age == 0) return BigInteger.Zero;
        var denominator = RocBankValue.RoundUnits(ExactVarianceWindow.Units(Math.Sqrt(age)) * (average.Sign > 0 ? average : Unit), Unit);
        return RocBankValue.RoundUnits(distance * Unit, denominator);
    }
    internal (double Up, double Down, Signal Signal) Next(double high, double low, double close, bool final)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close);
        var previous = _index == 0 ? c : ExactVarianceWindow.Units(_previousPrice);
        var range = RocBankValue.RoundUnits(BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previous), BigInteger.Abs(l - previous))), BigInteger.One);
        var highest = Extreme(_highs, high, true, final); var lowest = Extreme(_lows, low, false, final);
        var upAverage = Blend(range, _upAverage, lowest.Age); var downAverage = Blend(range, _downAverage, highest.Age);
        var upDistance = RocBankValue.RoundUnits(h - ExactVarianceWindow.Units(lowest.Value), BigInteger.One);
        var downDistance = RocBankValue.RoundUnits(ExactVarianceWindow.Units(highest.Value) - l, BigInteger.One);
        var up = Walk(upDistance, upAverage, lowest.Age); var down = Walk(downDistance, downAverage, highest.Age);
        var spread = up - down;
        var signal = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _index++; _previousPrice = close; _upAverage = upAverage; _downAverage = downAverage; _previousSpread = spread; }
        return (ExactMeanAccumulator.UnitRatio(up, BigInteger.One), ExactMeanAccumulator.UnitRatio(down, BigInteger.One), signal);
    }
    internal void Reset() { _highs.Clear(); _lows.Clear(); _index = 0; _previousPrice = 0; _upAverage = _downAverage = _previousSpread = default; }
}
