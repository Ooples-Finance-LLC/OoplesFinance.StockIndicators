namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendflexWindow
{
    private readonly int _length;
    private readonly bool _reflex;
    private readonly double _c1, _c2, _c3;
    private readonly Queue<RocBankValue> _history = new();
    private RocBankValue _first, _second;
    private ExactMeanAccumulator _historySum;
    private double _previousPrice, _energy;
    private int _energyShift;
    internal TrendflexWindow(int length, bool reflex = false)
    {
        _reflex = reflex; _length = Math.Max(1, length); var angle = MathHelper.Sqrt2 * Math.PI / (.5 * _length); var radius = Math.Exp(-angle);
        _c2 = 2 * radius * Math.Cos(angle); _c3 = -radius * radius;
        // |1 - radius * exp(i * angle)|^2, without subtracting nearby ones.
        var gap = 2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2); var sine = ExactVarianceWindow.Units(Math.Sin(angle / 2));
        var gain = new ExactMeanAccumulator(); gain.AddProduct(gap, gap);
        var curved = new ExactMeanAccumulator(); curved.Add(-4 * radius, sine * sine); curved.ScaleByPowerOfTwo(-2148); gain.Subtract(curved);
        _c1 = gain.Mean(1);
    }
    private static void Product(ref ExactMeanAccumulator total, RocBankValue value, double coefficient)
    { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift); total.Subtract(negative); }
    internal double Next(double price, bool commit)
    {
        var mean = new ExactMeanAccumulator(); mean.Add(price); mean.Add(_previousPrice);
        var total = new ExactMeanAccumulator(); Product(ref total, RocBankValue.Round(mean, count: 2), _c1); Product(ref total, _first, _c2); Product(ref total, _second, _c3);
        var filter = RocBankValue.Round(total);
        var deviationSum = new ExactMeanAccumulator(); filter.AddTo(ref deviationSum, _reflex ? _length - 1L : _length);
        if (_reflex)
        {
            // Sum departures from the endpoint chord algebraically. At period
            // one this cancels exactly instead of normalizing rounding noise.
            var endpoint = _history.Count == _length ? _history.Peek() : default; endpoint.AddTo(ref deviationSum, _length + 1L);
            var secondHistory = _historySum; deviationSum.Subtract(secondHistory);
        }
        deviationSum.Subtract(_historySum);
        var deviation = RocBankValue.Round(deviationSum, count: _reflex ? 2L * _length : _length);
        // Keep energy at normal binary64 precision with a separate exponent in
        // either direction: squaring a subnormal must not erase normalization.
        var units = ExactVarianceWindow.Units(deviation.Mantissa); var energySum = new ExactMeanAccumulator();
        energySum.Add(.04, units * units); energySum.ScaleByPowerOfTwo(2 * deviation.UpperShift - 2148);
        var retained = new ExactMeanAccumulator(); retained.AddProduct(-.96, _energy); retained.ScaleByPowerOfTwo(_energyShift); energySum.Subtract(retained);
        var shift = 0; var energy = energySum.Mean(1);
        if (!energySum.IsExactlyZero)
        {
            var lower = Math.Pow(2, -512); var upper = Math.Pow(2, 512);
            while (energy < lower) { energySum.ScaleByPowerOfTwo(1024); shift -= 1024; energy = energySum.Mean(1); }
            while (double.IsInfinity(energy) || energy >= upper) { energySum.ScaleByPowerOfTwo(-1024); shift += 1024; energy = energySum.Mean(1); }
        }
        var numerator = new ExactMeanAccumulator(); deviation.AddTo(ref numerator);
        var denominator = new ExactMeanAccumulator(); denominator.Add(Math.Sqrt(energy)); denominator.ScaleByPowerOfTwo(shift / 2);
        var result = energy == 0 ? 0 : numerator.Ratio(denominator);
        if (commit)
        {
            if (_history.Count == _length) _history.Dequeue().AddTo(ref _historySum, -1);
            _history.Enqueue(filter); filter.AddTo(ref _historySum); _second = _first; _first = filter; _previousPrice = price; _energy = energy; _energyShift = shift;
        }
        return result;
    }
    internal void Reset() { _history.Clear(); _historySum = default; _first = _second = default; _previousPrice = _energy = 0; _energyShift = 0; }
}
