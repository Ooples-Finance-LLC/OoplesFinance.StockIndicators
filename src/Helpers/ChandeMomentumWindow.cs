namespace OoplesFinance.StockIndicators.Helpers;

// Differences are represented by their finite endpoints, never by a potentially
// overflowing double subtraction. Only the bounded final ratio is rounded.
internal sealed class ChandeMomentumWindow : IDisposable
{
    private readonly PooledRingBuffer<(double From, double To)> _changes;
    private readonly double? _maximumChange;
    private readonly bool _zeroOrigin;
    private readonly int _numeratorScale;
    private ExactMeanAccumulator _numerator, _denominator;
    private double _previous;
    private bool _hasPrevious;

    internal ChandeMomentumWindow(int length, double? maximumChange = null, bool zeroOrigin = false, bool unitRatio = false)
    {
        if (maximumChange is double limit && (double.IsNaN(limit) || double.IsInfinity(limit) || limit < 0))
            throw new ArgumentOutOfRangeException(nameof(maximumChange));
        _maximumChange = maximumChange;
        _zeroOrigin = zeroOrigin;
        _numeratorScale = unitRatio ? 1 : 100;
        _changes = new PooledRingBuffer<(double, double)>(Math.Max(1, length));
    }

    private void Apply(ref ExactMeanAccumulator numerator, ref ExactMeanAccumulator denominator,
        double from, double to, int sign)
    {
        numerator.Add(to, _numeratorScale * sign);
        numerator.Add(from, -_numeratorScale * sign);
        var direction = to >= from ? sign : -sign;
        denominator.Add(to, direction);
        denominator.Add(from, -direction);
    }

    internal double Next(double value, bool commit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var from = _hasPrevious ? _previous : _zeroOrigin ? 0 : value;
        // Preserve the filter variant's rounded-difference threshold. Accepted
        // changes still contribute their exact endpoints to both totals.
        if (_maximumChange is double limit && Math.Abs(value - from) > limit) from = value;
        var numerator = _numerator;
        var denominator = _denominator;
        if (_changes.Count == _changes.Capacity)
        {
            var old = _changes[0];
            Apply(ref numerator, ref denominator, old.From, old.To, -1);
        }
        Apply(ref numerator, ref denominator, from, value, 1);
        var result = numerator.Ratio(denominator);
        if (commit)
        {
            _changes.TryAdd((from, value), out _);
            _numerator = numerator;
            _denominator = denominator;
            _previous = value;
            _hasPrevious = true;
        }
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var window = new ChandeMomentumWindow(Math.Min(Math.Max(1, length), input.Length));
        for (var i = 0; i < input.Length; i++) output[i] = window.Next(input[i], true);
    }

    internal void Reset()
    {
        _changes.Clear();
        _numerator = _denominator = default;
        _previous = 0;
        _hasPrevious = false;
    }
    public void Dispose() => _changes.Dispose();
}
