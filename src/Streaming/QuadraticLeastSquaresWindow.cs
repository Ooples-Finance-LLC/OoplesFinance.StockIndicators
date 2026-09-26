using System.Numerics;
namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>A full-window quadratic fit with exact sliding orthogonal moments.</summary>
internal sealed class QuadraticLeastSquaresWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly BigInteger _edge, _edgeSquare, _linearNorm, _quadraticNorm, _meanSquare;
    private BigInteger _sum, _linear, _quadratic;
    internal QuadraticLeastSquaresWindow(int length)
    {
        _length = Math.Max(1, length); _values = new(_length);
        var n = new BigInteger(_length); _edge = n - 1; _meanSquare = n * n - 1;
        _edgeSquare = 3 * _edge * _edge - _meanSquare;
        _linearNorm = n * _meanSquare / 3;
        _quadraticNorm = 4 * n * _meanSquare * (n * n - 4) / 5;
    }
    internal (double Value, double Forecast) Next(double value, int horizon, bool isFinal)
    {
        var incoming = ExactVarianceWindow.Units(value);
        var outgoing = _values.Count == _length ? ExactVarianceWindow.Units(_values[0]) : BigInteger.Zero;
        var retained = _sum - outgoing;
        var retainedLinear = _linear + _edge * outgoing;
        var sum = retained + incoming;
        var linear = retainedLinear - 2 * retained + _edge * incoming;
        var quadratic = _quadratic - _edgeSquare * outgoing - 12 * retainedLinear + 12 * retained + _edgeSquare * incoming;
        double fitted = 0, forecast = 0;
        if (_values.Count + 1 >= _length)
        {
            if (_length < 3) fitted = forecast = ExactMeanAccumulator.UnitRatio(sum, new BigInteger(_length));
            else
            {
                double At(BigInteger x)
                {
                    var q = 3 * x * x - _meanSquare;
                    var numerator = sum * _linearNorm * _quadraticNorm + linear * x * _length * _quadraticNorm + quadratic * q * _length * _linearNorm;
                    return ExactMeanAccumulator.UnitRatio(numerator, _length * _linearNorm * _quadraticNorm);
                }
                fitted = At(_edge); forecast = At(_edge + 2L * horizon);
            }
        }
        if (isFinal) { _values.TryAdd(value, out _); _sum = sum; _linear = linear; _quadratic = quadratic; }
        return (fitted, forecast);
    }
    internal void Reset() { _values.Clear(); _sum = _linear = _quadratic = default; }
    public void Dispose() => _values.Dispose();
}
