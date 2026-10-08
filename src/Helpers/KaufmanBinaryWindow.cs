using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

// Exact rational state preserves discrete wave decisions across overflow and cancellation.
internal sealed class KaufmanBinaryWindow
{
    private readonly int _length;
    private readonly F _fast, _slow, _filter;
    private readonly Queue<F> _prices = new(), _moves = new(), _changes = new();
    private F _price, _travel, _average, _low, _high, _sum, _squares;
    private bool _started;
    internal KaufmanBinaryWindow(int length, double fast, double slow, double filter)
    { _length = Math.Max(1, length); _fast = F.Of(fast); _slow = F.Of(slow); _filter = F.Of(filter) / 100; }

    // Compare against a signed square root by squaring only after separating signs.
    // This keeps threshold ties exact, including a negative filter percentage.
    internal static bool Exceeds(F distance, F filter, F variance)
    {
        if (variance.Sign == 0 || filter.Sign == 0) return distance.Sign > 0;
        var comparison = (distance * distance).CompareTo(filter * filter * variance);
        if (filter.Sign > 0) return distance.Sign > 0 && comparison > 0;
        return distance.Sign >= 0 || comparison < 0;
    }
    internal double Next(double price, bool final)
    {
        var value = F.Of(price);
        var move = _started ? (value - _price).Abs() : (F)0;
        var full = _prices.Count == _length;
        var travel = _travel + move - (full ? _moves.Peek() : (F)0);
        var efficiency = full && travel.Sign != 0 ? (value - _prices.Peek()).Abs() / travel : (F)0;
        var coefficient = efficiency * _fast + _slow; var gain = coefficient * coefficient;
        var previous = _started ? _average : value;
        var average = previous + gain * (value - previous); var change = average - previous;
        var expired = full ? _changes.Peek() : (F)0;
        var sum = _sum + change - expired; var squares = _squares + change * change - expired * expired;
        var variance = _changes.Count < _length - 1 ? (F)0 : (squares * _length - sum * sum) / _length / _length;
        var low = change.Sign < 0 ? average : _low; var high = change.Sign > 0 ? average : _high;
        var wave = Exceeds(average - low, _filter, variance) ? 1d : Exceeds(high - average, _filter, variance) ? -1d : 0d;
        if (final)
        {
            if (full) { _prices.Dequeue(); _moves.Dequeue(); _changes.Dequeue(); }
            _prices.Enqueue(value); _moves.Enqueue(move); _changes.Enqueue(change);
            _price = value; _travel = travel; _average = average; _low = low; _high = high;
            _sum = sum; _squares = squares; _started = true;
        }
        return wave;
    }
    internal void Reset()
    { _prices.Clear(); _moves.Clear(); _changes.Clear(); _price = _travel = _average = _low = _high = _sum = _squares = default; _started = false; }
}
