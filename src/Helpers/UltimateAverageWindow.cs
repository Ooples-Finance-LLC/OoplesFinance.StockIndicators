using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// UMA keeps exact typical prices and directional flow totals until the
// fractional exponent has been formed. Histories grow only as bars arrive.
internal sealed class UltimateAverageWindow : IDisposable
{
    private readonly int _maximum;
    private readonly Fraction _acceleration;
    private readonly VariableLengthWindow _length;
    private readonly List<(Fraction Price, Fraction Positive, Fraction Negative)> _history = new();
    private Fraction _previousTypical;
    private bool _started;

    internal UltimateAverageWindow(MovingAvgType kind, int minimum, int maximum, double acceleration)
    {
        _acceleration = Fraction.Of(acceleration);
        _maximum = Math.Max(Math.Max(1, minimum), maximum);
        _length = new(kind, minimum, maximum);
    }

    internal double Next(double price, double high, double low, double volume, bool final, int? suppliedLength = null)
        => NextPoint(price, high, low, volume, final, suppliedLength).Value;

    internal sealed class Point
    {
        private readonly IReadOnlyList<Fraction> _prices;
        private readonly int _period;
        private readonly Fraction _exponent;
        private int _cachedBits;
        private UltimatePowerWeights.Bounds _cachedBounds;
        internal double Value { get; }
        internal Point(IReadOnlyList<Fraction> prices, int period, Fraction exponent)
        {
            _prices = prices; _period = period; _exponent = exponent;
            Value = UltimatePowerWeights.PublishMean(Bounds);
        }
        internal UltimatePowerWeights.Bounds Bounds(int bits)
        {
            if (_cachedBits != bits) { _cachedBounds = UltimatePowerWeights.Mean(_prices, _period, _exponent, bits); _cachedBits = bits; }
            return _cachedBounds;
        }
    }

    internal Point NextPoint(double price, double high, double low, double volume, bool final, int? suppliedLength = null)
    {
        var value = Fraction.Of(price); var top = Fraction.Of(high); var bottom = Fraction.Of(low); var size = Fraction.Of(volume);
        var period = suppliedLength ?? _length.Next(price, final).Length;
        period = Math.Max(1, Math.Min(_maximum, period));
        var typical = (top + bottom + value) / 3;
        var flow = typical * size;
        Fraction positive = _started && typical > _previousTypical ? flow : 0;
        Fraction negative = _started && typical < _previousTypical ? flow : 0;
        var up = positive; var down = negative;
        var prices = new List<Fraction> { value };
        for (var i = _history.Count - 1; i >= 0 && prices.Count < period; i--)
        {
            var point = _history[i]; prices.Add(point.Price); up += point.Positive; down += point.Negative;
        }
        Fraction mfi;
        if (down.Sign == 0) mfi = 100;
        else if (up.Sign == 0 || (up + down).Sign == 0) mfi = 0;
        else
        {
            mfi = 100 * up / (up + down);
            if (mfi < 0) mfi = 0; else if (mfi > 100) mfi = 100;
        }
        var exponent = _acceleration + (2 * mfi - 100).Abs() / 25;
        var result = new Point(prices, period, exponent);
        if (final)
        {
            if (_history.Count == _maximum) _history.RemoveAt(0);
            _history.Add((value, positive, negative)); _previousTypical = typical; _started = true;
        }
        return result;
    }

    internal void Reset() { _history.Clear(); _previousTypical = default; _started = false; _length.Reset(); }
    public void Dispose() { Reset(); _length.Dispose(); }

    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int minimum, int maximum, double acceleration,
        Action<int, Point>? observe = null)
    {
        // Validate before the callback or any state is advanced.
        _ = Fraction.Of(acceleration);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        for (var i = 0; i < prices.Count; i++)
        { _ = Fraction.Of(prices[i]); _ = Fraction.Of(data.HighPrices[i]); _ = Fraction.Of(data.LowPrices[i]); _ = Fraction.Of(data.Volumes[i]); }
        var lengths = VariableLengthWindow.Calculate(data, kind, minimum, maximum).Lengths;
        using var window = new UltimateAverageWindow(kind, minimum, maximum, acceleration);
        var values = new double[prices.Count]; var signals = new Signal[prices.Count]; Fraction previous = 0;
        for (var i = 0; i < prices.Count; i++)
        {
            var point = window.NextPoint(prices[i], data.HighPrices[i], data.LowPrices[i], data.Volumes[i], true, (int)lengths[i]);
            values[i] = point.Value; observe?.Invoke(i, point);
            var difference = Fraction.Of(prices[i]) - Fraction.Of(values[i]);
            signals[i] = difference.Sign > 0 && difference > previous ? Signal.StrongBuy
                : difference.Sign < 0 && difference < previous ? Signal.StrongSell
                : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            previous = difference;
        }
        return (values, signals);
    }
}
