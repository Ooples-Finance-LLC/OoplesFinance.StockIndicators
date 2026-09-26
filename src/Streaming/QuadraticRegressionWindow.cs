namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Quadratic slopes in coordinates relative to the current bar.</summary>
/// <remarks>Keeping coordinates within the window avoids subtracting global fourth powers.
/// The prehistory consists of zero-valued observations at index zero, as in the original fit.</remarks>
internal sealed class QuadraticRegressionWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private int _index;

    internal QuadraticRegressionWindow(int length)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
    }

    internal (double Linear, double Quadratic) Next(double value, bool isFinal)
    {
        var kept = Math.Min(_values.Count, _length - 1);
        var count = kept + 1;
        double linear = 0, quadratic = 0;
        if (_length >= 3 && _index >= 2)
        {
            double sx = 0, sq = 0, sc = 0, sf = 0, sy = 0, sxy = 0, sqy = 0;
            for (var j = 0; j < _length; j++)
            {
                var missing = j >= count;
                double x = missing ? -_index : j - kept;
                // Translate prices too: constant offsets have zero covariance.
                var y = missing ? -value : j == kept ? 0 : _values[_values.Count - kept + j] - value;
                var q = x * x;
                sx += x; sq += q; sc += x * q; sf += q * q;
                sy += y; sxy += x * y; sqy += q * y;
            }
            var xx = sq - sx * sx / _length;
            var xq = sc - sx * sq / _length;
            var qq = sf - sq * sq / _length;
            var xy = sxy - sx * sy / _length;
            var qy = sqy - sq * sy / _length;
            var determinant = xx * qq - xq * xq;
            if (determinant != 0)
            {
                linear = (xy * qq - qy * xq) / determinant;
                quadratic = (qy * xx - xy * xq) / determinant;
            }
        }
        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }
        return (linear, quadratic);
    }

    internal static double Evaluate((double Linear, double Quadratic) slopes, int index,
        double xAverage, double squareAverage, double priceAverage)
    {
        var distance = index - xAverage;
        var squareDistance = index * (double)index - squareAverage - 2 * index * distance;
        return priceAverage + slopes.Linear * distance + slopes.Quadratic * squareDistance;
    }

    internal void Reset() { _values.Clear(); _index = 0; }
    public void Dispose() => _values.Dispose();
}
