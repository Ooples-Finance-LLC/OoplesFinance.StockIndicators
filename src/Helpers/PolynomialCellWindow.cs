namespace OoplesFinance.StockIndicators.Helpers;

// Zero-padded convolution of the quadratic-plus-three-harmonic cell integrals.
internal sealed class PolynomialCellWindow
{
    private readonly int _length;
    private readonly List<double> _weights = new(), _prices = new();
    private int _next;
    internal PolynomialCellWindow(int length) => _length = Math.Max(1, length);
    internal double Next(double price, bool commit)
    {
        var count = Math.Min(_length - 1, _prices.Count);
        while (_weights.Count <= count)
        {
            var j = _weights.Count + 1d; var upper = j / _length; var lower = (j - 1) / _length;
            double upperSine = 0, lowerSine = 0;
            for (var harmonic = 1; harmonic <= 3; harmonic++)
            {
                upperSine += Math.Sin(upper * harmonic * Math.PI) / harmonic;
                lowerSine += Math.Sin(lower * harmonic * Math.PI) / harmonic;
            }
            _weights.Add((upper * upper + upperSine) - (lower * lower + lowerSine));
        }
        var sum = new ExactMeanAccumulator();
        // These two cell integrals have exact rational closed forms.
        if (_length == 1) sum.Add(price);
        else if (_length == 2) sum.Add(price, 11);
        else sum.AddProduct(price, _weights[0]);
        for (var lag = 1; lag <= count; lag++)
        {
            var index = (_next - lag + _prices.Count) % _prices.Count;
            if (_length == 2) sum.Add(_prices[index]);
            else sum.AddProduct(_prices[index], _weights[lag]);
        }
        var result = sum.Mean(_length == 2 ? 12 : 1);
        if (commit)
        {
            if (_prices.Count < _length) { _prices.Add(price); _next = _prices.Count; }
            else { if (_next == _length) _next = 0; _prices[_next++] = price; }
        }
        return result;
    }
    internal void Reset() { _prices.Clear(); _next = 0; }
}
