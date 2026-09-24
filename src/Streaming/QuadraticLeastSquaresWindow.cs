namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>A full-window population quadratic fit in centered, orthogonal coordinates.</summary>
internal sealed class QuadraticLeastSquaresWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    internal QuadraticLeastSquaresWindow(int length)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
    }
    internal (double Value, double Forecast) Next(double value, int horizon, bool isFinal)
    {
        var kept = Math.Min(_values.Count, _length-1);
        double At(int j) => j == kept ? value : _values[_values.Count-kept+j];
        double fitted = 0, forecast = 0;
        if (kept+1 == _length)
        {
            var anchor = At(0);
            double mean = 0, linear = 0, quadratic = 0, linearNorm = 0, quadraticNorm = 0;
            var center = (_length-1)/2d;
            var meanSquare = (_length*(double)_length-1)/12;
            for (var j = 0; j < _length; j++)
            {
                var y = At(j)-anchor;
                var x = j-center;
                var q = x*x-meanSquare;
                mean += y;
                linear += x*y;
                quadratic += q*y;
                linearNorm += x*x;
                quadraticNorm += q*q;
            }
            mean = anchor+mean/_length;
            // With fewer than three points the quadratic system is rank deficient;
            // retain the existing mean-only fallback deterministically.
            if (_length < 3) fitted = forecast = mean;
            else
            {
                linear /= linearNorm;
                quadratic /= quadraticNorm;
                fitted = mean+linear*center+quadratic*(center*center-meanSquare);
                var future = center+horizon;
                forecast = mean+linear*future+quadratic*(future*future-meanSquare);
            }
        }
        if (isFinal) _values.TryAdd(value, out _);
        return (fitted, forecast);
    }
    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
