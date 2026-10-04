using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// The binary64 sine basis depends on phase+lag, so exact integer convolution
// computes every dot product without a quadratic matrix. Correlation ordering
// cancels the common history variance and compares signed squared covariances.
internal sealed class AnticipateExactPhase
{
    private readonly int _length;
    private BigInteger[]? _waves, _prefix, _squares;
    internal AnticipateExactPhase(int length) => _length = Math.Max(1, length);
    private double Wave(long index) => -Math.Sin(2 * Math.PI * index / _length);
    private static int Bits(BigInteger value)
    {
        var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var top = bytes[last]; var bits = 0; while (top != 0) { bits++; top >>= 1; }
        return last * 8 + bits;
    }
    private static int Trailing(BigInteger value)
    {
        if (value.IsZero) return int.MaxValue;
        var bytes = BigInteger.Abs(value).ToByteArray(); var count = 0; var index = 0;
        while (bytes[index] == 0) { count += 8; index++; }
        var next = bytes[index]; while ((next & 1) == 0) { count++; next >>= 1; } return count;
    }
    private void Prepare()
    {
        if (_waves is not null) return;
        var waves = new BigInteger[2 * _length - 1]; var shift = int.MaxValue;
        for (var i = 0; i < waves.Length; i++) { waves[i] = ExactVarianceWindow.Units(Wave(i)); shift = Math.Min(shift, Trailing(waves[i])); }
        _prefix = new BigInteger[waves.Length + 1]; _squares = new BigInteger[waves.Length + 1];
        for (var i = 0; i < waves.Length; i++)
        {
            waves[i] >>= shift; _prefix[i + 1] = _prefix[i] + waves[i]; _squares[i + 1] = _squares[i] + waves[i] * waves[i];
        }
        _waves = waves;
    }
    private static BigInteger Pack(IReadOnlyList<BigInteger> values, BigInteger offset, int bytesPerValue, bool reverse)
    {
        var bytes = new byte[checked(values.Count * bytesPerValue + 1)];
        for (var i = 0; i < values.Count; i++)
        {
            var coefficient = (values[reverse ? values.Count - 1 - i : i] + offset).ToByteArray();
            Buffer.BlockCopy(coefficient, 0, bytes, i * bytesPerValue, coefficient.Length);
        }
        return new BigInteger(bytes);
    }
    private static bool Better(BigInteger covariance, BigInteger variance, BigInteger previousCovariance, BigInteger previousVariance)
    {
        if (covariance.Sign != previousCovariance.Sign) return covariance.Sign > previousCovariance.Sign;
        if (covariance.IsZero) return false;
        var order = (covariance * covariance * previousVariance).CompareTo(previousCovariance * previousCovariance * variance);
        return covariance.Sign * order > 0;
    }
    internal double Predict(IReadOnlyList<BigInteger> history)
    {
        if (_length <= 2) return 0;
        var count = Math.Min(_length, history.Count); BigInteger sum = 0, squares = 0;
        for (var i = 0; i < count; i++) { sum += history[i]; squares += history[i] * history[i]; }
        if (_length * squares == sum * sum) return 0;
        // This is an internal algorithm choice, not a public period restriction.
        // Larger periods retain a constant-space direct path; zero/flat histories
        // return above without generating any phase basis.
        if (_length > 16384) return Direct(history, count, sum);
        Prepare();
        var source = history.Take(count).ToArray(); var shift = source.Min(Trailing);
        for (var i = 0; i < count; i++) source[i] >>= shift;
        sum >>= shift;
        var sourceOffset = source.Max(BigInteger.Abs); var waveOffset = _waves!.Max(BigInteger.Abs);
        var width = Math.Max(1, (Bits(4 * count * sourceOffset * waveOffset) + 7) / 8);
        var waveCount = _length + count - 1;
        var product = (Pack(source, sourceOffset, width, true) * Pack(_waves!.Take(waveCount).ToArray(), waveOffset, width, false)).ToByteArray();
        var bestPhase = 0; BigInteger bestCovariance = 0, bestVariance = 1;
        for (var phase = 0; phase < _length; phase++)
        {
            var bytes = new byte[width + 1]; var start = checked((phase + count - 1) * width);
            if (start < product.Length) Buffer.BlockCopy(product, start, bytes, 0, Math.Min(width, product.Length - start));
            var dot = new BigInteger(bytes) - sourceOffset * (_prefix![phase + count] - _prefix[phase]) - waveOffset * sum - count * sourceOffset * waveOffset;
            var waveSum = _prefix![phase + _length] - _prefix[phase];
            var variance = _length * (_squares![phase + _length] - _squares[phase]) - waveSum * waveSum;
            var covariance = _length * dot - sum * waveSum;
            if (variance.IsZero) { covariance = 0; variance = 1; }
            if (phase == 0 || Better(covariance, variance, bestCovariance, bestVariance))
            { bestPhase = phase; bestCovariance = covariance; bestVariance = variance; }
        }
        return Wave(bestPhase);
    }
    private double Direct(IReadOnlyList<BigInteger> history, int count, BigInteger sum)
    {
        var bestPhase = 0; BigInteger bestCovariance = 0, bestVariance = 1;
        for (var phase = 0; phase < _length; phase++)
        {
            BigInteger dot = 0, waveSum = 0, waveSquares = 0;
            for (var lag = 0; lag < _length; lag++)
            {
                var wave = ExactVarianceWindow.Units(Wave((long)phase + lag)); waveSum += wave; waveSquares += wave * wave;
                if (lag < count) dot += history[lag] * wave;
            }
            var variance = _length * waveSquares - waveSum * waveSum; var covariance = _length * dot - sum * waveSum;
            if (variance.IsZero) { covariance = 0; variance = 1; }
            if (phase == 0 || Better(covariance, variance, bestCovariance, bestVariance))
            { bestPhase = phase; bestCovariance = covariance; bestVariance = variance; }
        }
        return Wave(bestPhase);
    }
}
