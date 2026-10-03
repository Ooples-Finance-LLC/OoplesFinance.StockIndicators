using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// FIR and candidate/filter means are convex combinations. Round each complete
// mean once; its finite inputs therefore cannot overflow before cancellation.
internal sealed class MedianAdaptiveWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length;
    private readonly double _threshold;
    private readonly BigInteger _thresholdUnits;
    private readonly List<double> _history = new();
    private int _start;
    private double _price, _olderPrice, _oldestPrice, _candidate, _filter;

    internal MedianAdaptiveWindow(int length, double threshold)
    {
        if (double.IsNaN(threshold) || double.IsInfinity(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold));
        _length = Math.Max(1, length);
        _threshold = threshold;
        _thresholdUnits = U(threshold);
    }

    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static double Mean(BigInteger numerator, long denominator)
        => ExactMeanAccumulator.UnitRatio(numerator, denominator);
    private static double Candidate(double smooth, double previous, int length)
        => Mean(2 * U(smooth) + (length - 1L) * U(previous), length + 1L);

    private bool Accepts(double median, double candidate)
        => median != 0 && BigInteger.Abs(U(median) - U(candidate)) * Unit <= _thresholdUnits * BigInteger.Abs(U(median));

    // Candidate is monotone as period falls. Locate the first entry into the
    // acceptable interval, then check its other endpoint (a step may skip it).
    private int FirstAcceptable(double smooth, double median, int available)
    {
        if (median == 0 || _threshold < 0) return 0;
        var center = U(median) * Unit;
        var radius = _thresholdUnits * BigInteger.Abs(U(median));
        var count = (_length - available) / 2 + 1;
        var left = 0; var right = count;
        var rising = smooth >= _candidate;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            var value = U(Candidate(smooth, _candidate, _length - 2 * middle)) * Unit;
            var entered = rising ? value >= center - radius : value <= center + radius;
            if (entered) right = middle; else left = middle + 1;
        }
        if (left == count) return 0;
        var period = _length - 2 * left;
        return Accepts(median, Candidate(smooth, _candidate, period)) ? period : 0;
    }

    internal (double Value, Signal Trade, int Period, double Candidate) Next(double price, bool final)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var smooth = Mean(U(price) + 2 * U(_price) + 2 * U(_olderPrice) + U(_oldestPrice), 6);
        var retained = _history.Count - _start;
        var available = (int)Math.Min(_length, retained + 1L);
        var first = _history.Count - (available - 1);
        var length = _length; var candidate = 0d;
        if (.2 > _threshold)
        {
            var tree = new OrderStatisticTree();
            for (var i = first; i < _history.Count; i++) tree.Insert(_history[i]);
            tree.Insert(smooth);
            double Median(int count)
            {
                var lower = tree.SelectByRank((count - 1) / 2 + 1);
                return (count & 1) != 0 ? lower : Mean(U(lower) + U(tree.SelectByRank(count / 2 + 1)), 2);
            }
            var median = Median(available);
            var accepted = FirstAcceptable(smooth, median, available);
            var evaluated = accepted != 0 ? accepted : _length - 2 * ((_length - available) / 2);
            candidate = Candidate(smooth, _candidate, evaluated);
            length = evaluated - 2;
            var consumed = 0;
            while (accepted == 0 && length > 0)
            {
                var count = Math.Min(length, available);
                while (available - consumed > count) tree.Remove(_history[first + consumed++]);
                median = Median(count);
                candidate = Candidate(smooth, _candidate, length);
                if (Accepts(median, candidate)) accepted = length;
                length -= 2;
            }
        }
        var period = Math.Max(3, length);
        var value = Candidate(smooth, _filter, period);
        var residual = U(price) - U(value); var previous = U(_price) - U(_filter);
        var trade = residual.Sign > 0 && residual > previous ? Signal.StrongBuy : residual.Sign < 0 && residual < previous ? Signal.StrongSell
            : residual.Sign > 0 ? Signal.Buy : residual.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            _oldestPrice = _olderPrice; _olderPrice = _price; _price = price;
            _candidate = candidate; _filter = value; _history.Add(smooth);
            if (_history.Count - _start > _length) _start++;
            if (_start >= 1024 && _start >= _history.Count / 2) { _history.RemoveRange(0, _start); _start = 0; }
        }
        return (value, trade, period, candidate);
    }

    internal void Reset()
    { _history.Clear(); _start = 0; _price = _olderPrice = _oldestPrice = _candidate = _filter = 0; }
}
