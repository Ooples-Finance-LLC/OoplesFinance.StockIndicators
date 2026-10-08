using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BetterVolumeWindow
{
    private readonly int _length, _lookback; private long _count;
    private double _previousClose, _previousOpen; private BigInteger _previousUp, _previousDown;
    private readonly LinkedList<(long Index, BigInteger Value)> _highs = new(), _lows = new();
    private readonly LinkedList<(long Index, BigInteger Value)>[] _metrics = Enumerable.Range(0, 22).Select(_ => new LinkedList<(long Index, BigInteger Value)>()).ToArray();
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    internal BetterVolumeWindow(int length, int lookback) { _length = Math.Max(1, length); _lookback = Math.Max(1, lookback); }
    private static BigInteger R(BigInteger value) => RocBankValue.RoundUnits(value, BigInteger.One);
    private static BigInteger Ratio(BigInteger numerator, BigInteger denominator)
        => denominator.IsZero ? BigInteger.Zero : RocBankValue.RoundUnits(numerator * denominator.Sign, BigInteger.Abs(denominator));
    private BigInteger Extreme(LinkedList<(long Index, BigInteger Value)> deque, BigInteger value, int length, bool maximum, bool commit)
    {
        var expiry = _count - length + 1L; var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? BigInteger.Max(value, first.Value.Value) : BigInteger.Min(value, first.Value.Value);
        if (commit) { while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst(); while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast(); deque.AddLast((_count, value)); }
        return result;
    }
    internal (double Value, Signal Signal) Next(double open, double high, double low, double close, double volume, bool commit)
    {
        var o = ExactVarianceWindow.Units(open); var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var c = ExactVarianceWindow.Units(close); var v = ExactVarianceWindow.Units(volume);
        var previousClose = _count == 0 ? c : ExactVarianceWindow.Units(_previousClose); var previousOpen = _count == 0 ? BigInteger.Zero : ExactVarianceWindow.Units(_previousOpen);
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - previousClose), BigInteger.Abs(l - previousClose)));
        var span = Extreme(_highs, h, _lookback, true, commit) - Extreme(_lows, l, _lookback, false, commit);
        var body = c - o;
        // A zero allocation denominator carries no directional share; doji volume is split equally.
        var up = body.Sign > 0 ? Ratio(v * range, 2 * range - body) : body.Sign < 0 ? Ratio(v * (range + body), 2 * range + body) : Ratio(v, new BigInteger(2));
        var down = R(v - up); var pairUp = up + _previousUp; var pairDown = down + _previousDown;
        var metric = new BigInteger[22];
        metric[4] = Ratio(up * range, Unit); metric[5] = Ratio((up - down) * range, Unit);
        metric[6] = Ratio(down * range, Unit); metric[7] = Ratio((down - up) * range, Unit);
        metric[8] = Ratio(up * Unit, range); metric[9] = Ratio((up - down) * Unit, range);
        metric[10] = Ratio(down * Unit, range); metric[11] = Ratio((down - up) * Unit, range);
        metric[14] = Ratio(pairUp * span, Unit); metric[15] = Ratio((pairUp - pairDown) * span, Unit);
        metric[16] = Ratio(pairDown * span, Unit); metric[17] = Ratio((pairDown - pairUp) * span, Unit);
        metric[18] = Ratio(pairUp * Unit, span); metric[19] = Ratio((pairUp - pairDown) * Unit, span);
        metric[20] = Ratio(pairDown * Unit, span); metric[21] = Ratio((pairDown - pairUp) * Unit, span);
        var at = new bool[22];
        for (var index = 4; index < 22; index++)
        {
            if (index is 12 or 13) continue;
            var maximum = index is 4 or 5 or 6 or 7 or 14 or 15;
            at[index] = metric[index] == Extreme(_metrics[index], metric[index], _length, maximum, commit);
        }
        var rising = body.Sign > 0; var falling = body.Sign < 0; var previousRising = previousClose > previousOpen; var previousFalling = previousClose < previousOpen;
        var c2 = at[4] && rising; var c3 = at[5] && rising; var c4 = at[6] && falling; var c5 = at[7] && falling;
        var c6 = at[8] && falling; var c7 = at[9] && falling; var c8 = at[10] && rising; var c9 = at[11] && rising;
        var c12 = at[14] && rising && previousRising; var c13 = at[15] && rising && previousFalling;
        var c14 = at[16] && falling && previousFalling; var c15 = at[17] && falling && previousFalling; var c16 = at[18] && falling && previousFalling;
        var c17 = at[19] && rising && previousFalling; var c18 = at[20] && rising && previousRising; var c19 = at[21] && rising && previousRising;
        var buy = c2 || c3 || c8 || c9 || c12 || c13 || c18 || c19;
        var sell = c4 || c5 || c6 || c7 || c14 || c15 || c16 || c17;
        if (commit) { _count++; _previousClose = close; _previousOpen = open; _previousUp = up; _previousDown = down; }
        return (ExactMeanAccumulator.UnitRatio(up, BigInteger.One), buy ? Signal.Buy : sell ? Signal.Sell : Signal.None);
    }
    internal void Reset() { _count = 0; _previousClose = _previousOpen = 0; _previousUp = _previousDown = default; _highs.Clear(); _lows.Clear(); foreach (var metric in _metrics) metric.Clear(); }
}
