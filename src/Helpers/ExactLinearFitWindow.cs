using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact partial-window OLS. Each published reading is rounded independently.
internal sealed class ExactLinearFitWindow : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private BigInteger _sum, _weighted, _index;

    internal ExactLinearFitWindow(int length) => _window = new PooledRingBuffer<double>(Math.Max(1, length));

    internal Fit Next(double value, bool isFinal)
    {
        var current = ExactVarianceWindow.Units(value);
        var full = _window.Count == _window.Capacity;
        var expired = full ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero;
        var count = full ? _window.Count : _window.Count + 1;
        var sum = _sum + current - expired;
        var weighted = full ? _weighted - _sum + expired + (count - 1) * current : _weighted + (count - 1) * current;
        var n = new BigInteger(count);
        var spread = n * n - 1;
        var covariance = 2 * weighted - (n - 1) * sum;
        var fit = new Fit(sum, covariance, n, spread, _index);
        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _sum = sum; _weighted = weighted; _index++;
        }
        return fit;
    }

    internal readonly struct Fit
    {
        private readonly BigInteger _sum, _covariance, _n, _spread, _index;
        internal Fit(BigInteger sum, BigInteger covariance, BigInteger n, BigInteger spread, BigInteger index)
        { _sum = sum; _covariance = covariance; _n = n; _spread = spread; _index = index; }
        private double At(BigInteger twiceCenteredPosition) => _n.IsOne
            ? ExactMeanAccumulator.UnitRatio(_sum, BigInteger.One)
            : ExactMeanAccumulator.UnitRatio(_sum * _spread + 3 * _covariance * twiceCenteredPosition, _n * _spread);
        // Exact endpoint gap, retained as minimum-unit numerator / denominator for normalization.
        internal (BigInteger Numerator, BigInteger Denominator) Difference(Fit other)
        {
            var denominator = _n.IsOne ? BigInteger.One : _n * _spread;
            var numerator = _n.IsOne ? _sum : _sum * _spread + 3 * _covariance * (_n - 1);
            var otherDenominator = other._n.IsOne ? BigInteger.One : other._n * other._spread;
            var otherNumerator = other._n.IsOne ? other._sum : other._sum * other._spread + 3 * other._covariance * (other._n - 1);
            return (numerator * otherDenominator - otherNumerator * denominator, denominator * otherDenominator);
        }
        internal double Index => (double)_index;
        internal double PercentFitResidual(double value)
        {
            var denominator = _n.IsOne ? BigInteger.One : _n * _spread;
            var numerator = _n.IsOne ? _sum : _sum * _spread + 3 * _covariance * (_n - 1);
            if (numerator.IsZero) return 0;
            return ExactMeanAccumulator.UnitRatio((100 * (ExactVarianceWindow.Units(value) * denominator - numerator) * numerator.Sign) << 1074,
                BigInteger.Abs(numerator));
        }
        internal double CenteredLine(double meanPrice, double meanTime)
        {
            if (_n.IsOne) return meanPrice;
            if (double.IsNaN(meanPrice) || double.IsInfinity(meanPrice) || double.IsNaN(meanTime) || double.IsInfinity(meanTime))
                return meanPrice + Slope * (Index - meanTime);
            var denominator = _n * _spread;
            var offset = (_index << 1074) - ExactVarianceWindow.Units(meanTime);
            return ExactMeanAccumulator.UnitRatio((ExactVarianceWindow.Units(meanPrice) * denominator << 1074) + 6 * _covariance * offset,
                denominator << 1074);
        }
        internal int Count => (int)_n;
        internal double Slope => _n.IsOne ? 0 : ExactMeanAccumulator.UnitRatio(6 * _covariance, _n * _spread);
        internal double Last => At(_n - 1);
        // The endpoint's coefficient absolute sum is below two. Round with one
        // extra exponent bit only when publication as a double would overflow.
        internal BigInteger RoundedLastUnits
        {
            get
            {
                var denominator = _n.IsOne ? BigInteger.One : _n * _spread;
                var numerator = _n.IsOne ? _sum : _sum * _spread + 3 * _covariance * (_n - 1);
                var value = ExactMeanAccumulator.UnitRatio(numerator, denominator);
                return double.IsInfinity(value)
                    ? 2 * ExactVarianceWindow.Units(ExactMeanAccumulator.UnitRatio(numerator, 2 * denominator))
                    : ExactVarianceWindow.Units(value);
            }
        }
        internal double Next => At(_n + 1);
        internal double GlobalIntercept => At(_n - 1 - 2 * _index);
        // Normalize the exact residual before rounding, even when the hidden endpoint overflows.
        internal double PercentResidual(double value)
        {
            var price = ExactVarianceWindow.Units(value);
            if (price.IsZero) return 0;
            var denominator = _n.IsOne ? BigInteger.One : _n * _spread;
            var numerator = _n.IsOne ? _sum : _sum * _spread + 3 * _covariance * (_n - 1);
            return ExactMeanAccumulator.UnitRatio((100 * (price * denominator - numerator) * price.Sign) << 1074,
                BigInteger.Abs(price) * denominator);
        }

        // Offset the exact fitted endpoint before the final output rounding.
        internal double Offset(double value, double multiplier)
        {
            var denominator = _n.IsOne ? BigInteger.One : _n * _spread;
            var numerator = _n.IsOne ? _sum : _sum * _spread + 3 * _covariance * (_n - 1);
            var product = ExactVarianceWindow.Units(value) * ExactVarianceWindow.Units(multiplier);
            return ExactMeanAccumulator.UnitRatio((numerator << 1074) + product * denominator, denominator << 1074);
        }

    }

    internal void Reset() { _window.Clear(); _sum = default; _weighted = default; _index = default; }
    public void Dispose() => _window.Dispose();
}
