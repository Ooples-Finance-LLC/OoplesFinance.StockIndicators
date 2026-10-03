using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Retain rounded regression endpoints beyond binary64's exponent range. Only
// the final bounded position is published; all histories grow with observations.
internal sealed class AdaptiveStochasticWindow
{
    private readonly int _length, _fitLength, _fastLength, _slowLength;
    private readonly Queue<BigInteger> _fit = new(), _prices = new(), _changes = new();
    private readonly LinkedList<(long Index, BigInteger Value)> _fastHigh = new(), _fastLow = new(), _slowHigh = new(), _slowLow = new();
    private BigInteger _sum, _weighted, _travel, _previousPrice;
    private long _count;
    private double _previous, _previousSlope;

    internal AdaptiveStochasticWindow(int length, int fastLength, int slowLength)
    {
        _length = Math.Max(1, length); _fastLength = Math.Max(1, fastLength); _slowLength = Math.Max(1, slowLength);
        _fitLength = Math.Max(1, Math.Abs(_slowLength - _fastLength));
    }

    private BigInteger Extreme(LinkedList<(long Index, BigInteger Value)> deque, BigInteger value, int length, bool maximum, bool commit)
    {
        var expiry = _count - length + 1L;
        var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? BigInteger.Max(value, first.Value.Value) : BigInteger.Min(value, first.Value.Value);
        if (commit)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }

    private static BigInteger Blend(BigInteger fast, BigInteger slow, double efficiency)
    {
        // Preserve the specified two products and sum as separately rounded stages.
        var first = RocBankValue.RoundUnits(fast * ExactVarianceWindow.Units(efficiency), BigInteger.One << 1074);
        var second = RocBankValue.RoundUnits(slow * ExactVarianceWindow.Units(1 - efficiency), BigInteger.One << 1074);
        return RocBankValue.RoundUnits(first + second, BigInteger.One);
    }

    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var units = ExactVarianceWindow.Units(price);
        var full = _fit.Count == _fitLength; var expired = full ? _fit.Peek() : BigInteger.Zero;
        var n = new BigInteger(full ? _fit.Count : _fit.Count + 1);
        var sum = _sum + units - expired;
        var weighted = full ? _weighted - _sum + expired + (n - 1) * units : _weighted + (n - 1) * units;
        var spread = n * n - 1;
        var endpoint = n.IsOne ? units : RocBankValue.RoundUnits(sum * spread + 3 * (2 * weighted - (n - 1) * sum) * (n - 1), n * spread);
        var change = _count == 0 ? BigInteger.Zero : BigInteger.Abs(units - _previousPrice);
        var travel = _travel + change - (_changes.Count == _length ? _changes.Peek() : BigInteger.Zero);
        var distance = _prices.Count == _length ? BigInteger.Abs(units - _prices.Peek()) : BigInteger.Zero;
        var efficiency = travel.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(distance << 1074, travel);
        var high = Blend(Extreme(_fastHigh, endpoint, _fastLength, true, commit), Extreme(_slowHigh, endpoint, _slowLength, true, commit), efficiency);
        var low = Blend(Extreme(_fastLow, endpoint, _fastLength, false, commit), Extreme(_slowLow, endpoint, _slowLength, false, commit), efficiency);
        var value = high == low ? 0 : Math.Max(0, Math.Min(1, ExactMeanAccumulator.UnitRatio((endpoint - low) << 1074, high - low)));
        var slope = value - _previous;
        var signal = SignalHelper.GetRsiSignal(slope, _previousSlope, value, _previous, .8, .2);
        if (commit)
        {
            if (full) _fit.Dequeue(); _fit.Enqueue(units); _sum = sum; _weighted = weighted;
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(units);
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change);
            _travel = travel; _previousPrice = units; _previous = value; _previousSlope = slope; _count++;
        }
        return (value, signal);
    }

    internal void Reset()
    {
        _fit.Clear(); _prices.Clear(); _changes.Clear(); _fastHigh.Clear(); _fastLow.Clear(); _slowHigh.Clear(); _slowLow.Clear();
        _sum = _weighted = _travel = _previousPrice = BigInteger.Zero; _count = 0; _previous = _previousSlope = 0;
    }
}
