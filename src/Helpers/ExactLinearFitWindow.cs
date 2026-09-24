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
        internal int Count => (int)_n;
        internal double Slope => _n.IsOne ? 0 : ExactMeanAccumulator.UnitRatio(6 * _covariance, _n * _spread);
        internal double Last => At(_n - 1);
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
