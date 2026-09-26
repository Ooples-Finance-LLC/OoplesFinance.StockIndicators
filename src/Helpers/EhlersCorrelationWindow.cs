using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Zero-padded Pearson windows. Integer moments retain all finite binary64 inputs;
// normalization precedes rounding, so neither squares nor hidden differences overflow.
internal sealed class EhlersCorrelationWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _values;
    private readonly BigInteger[] _real, _imag;
    private readonly BigInteger _realSpread, _imagSpread;

    internal EhlersCorrelationWindow(int length, bool trend = false)
    {
        length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(length);
        _real = new BigInteger[length]; _imag = new BigInteger[length];
        for (var j = 0; j < length; j++)
        {
            var phase = 2 * Math.PI * ((double)j / length);
            _real[j] = ExactVarianceWindow.Units(trend ? -(double)j : Math.Cos(phase));
            _imag[j] = ExactVarianceWindow.Units(trend || length <= 2 ? 0 : -Math.Sin(phase));
        }
        _realSpread = Center(_real); _imagSpread = Center(_imag);
    }

    private static BigInteger Center(BigInteger[] values)
    {
        var sum = values.Aggregate(BigInteger.Zero, (a, b) => a + b);
        var squares = BigInteger.Zero;
        for (var j = 0; j < values.Length; j++)
        {
            values[j] = values.Length * values[j] - sum;
            squares += values[j] * values[j];
        }
        return squares;
    }

    internal (double Real, double Imag) Next(double value, bool commit)
    {
        var sum = BigInteger.Zero; var squares = BigInteger.Zero;
        var real = BigInteger.Zero; var imag = BigInteger.Zero;
        for (var j = 0; j < _values.Capacity; j++)
        {
            var x = ExactVarianceWindow.Units(j == 0 ? value : j <= _values.Count ? _values[_values.Count - j] : 0);
            sum += x; squares += x * x;
            real += x * _real[j]; imag += x * _imag[j];
        }
        var spread = _values.Capacity * squares - sum * sum;
        double Normalize(BigInteger cross, BigInteger axisSpread) => spread.IsZero || axisSpread.IsZero ? 0
            : cross.Sign * ExactPopulationDeviation.RootRatio((cross * cross * _values.Capacity) << 2148, spread * axisSpread);
        var result = (Normalize(real, _realSpread), Normalize(imag, _imagSpread));
        if (commit) _values.TryAdd(value, out _);
        return result;
    }

    internal void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}
