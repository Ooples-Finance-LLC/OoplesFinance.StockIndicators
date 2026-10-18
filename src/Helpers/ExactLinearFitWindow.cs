using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact partial-window OLS. Each published reading is rounded independently.
internal sealed class ExactLinearFitWindow : IDisposable
{
    private readonly PooledRingBuffer<double>? _window;
    private readonly Queue<double>? _observed;
    private readonly int _length;
    private readonly bool _compact;
    private int _grid;
    private bool _hasGrid;
    private BigInteger _sum, _weighted, _index;

    internal ExactLinearFitWindow(int length, bool observedHistory = false, bool compact = false)
    {
        _length = Math.Max(1, length);
        _compact = compact;
        if (observedHistory) _observed = new(); else _window = new PooledRingBuffer<double>(_length);
    }

    internal Fit Next(double value, bool isFinal)
    {
        var grid = _grid;
        var hasGrid = _hasGrid;
        BigInteger current;
        if (_compact)
        {
            var parts = Parts(value);
            if (parts.Integer != 0 && (!hasGrid || parts.Grid < grid))
            { grid = parts.Grid; hasGrid = true; }
            current = parts.Integer == 0 ? BigInteger.Zero : new BigInteger(parts.Integer) << (parts.Grid - grid);
        }
        else current = ExactVarianceWindow.Units(value);
        // Preview can discover a finer grid, but must not commit it or rescale
        // the stored moments until isFinal is true.
        var shift = _hasGrid ? _grid - grid : 0;
        var priorSum = _sum << shift;
        var priorWeighted = _weighted << shift;
        var observed = _observed?.Count ?? _window!.Count;
        var full = observed == _length;
        var expired = BigInteger.Zero;
        if (full)
        {
            var old = _observed is null ? _window![0] : _observed.Peek();
            if (_compact)
            {
                var parts = Parts(old);
                expired = parts.Integer == 0 ? BigInteger.Zero : new BigInteger(parts.Integer) << (parts.Grid - grid);
            }
            else expired = ExactVarianceWindow.Units(old);
        }
        var count = full ? observed : observed + 1;
        var sum = priorSum + current - expired;
        var weighted = full ? priorWeighted - priorSum + expired + (count - 1) * current : priorWeighted + (count - 1) * current;
        var n = new BigInteger(count);
        var spread = n * n - 1;
        var covariance = 2 * weighted - (n - 1) * sum;
        var fit = new Fit(sum, covariance, n, spread, _index, grid);
        if (isFinal)
        {
            _window?.TryAdd(value, out _);
            if (_observed is not null) { if (full) _observed.Dequeue(); _observed.Enqueue(value); }
            _sum = sum; _weighted = weighted; _index++;
            _grid = grid; _hasGrid = hasGrid;
        }
        return fit;
    }

    internal readonly struct Fit
    {
        private readonly BigInteger _compactSum, _compactCovariance, _n, _spread, _index;
        private readonly int _grid;
        // Existing compound consumers explicitly request minimum-unit arithmetic.
        private BigInteger _sum => _compactSum << _grid;
        private BigInteger _covariance => _compactCovariance << _grid;
        internal Fit(BigInteger sum, BigInteger covariance, BigInteger n, BigInteger spread, BigInteger index, int grid = 0)
        { _compactSum = sum; _compactCovariance = covariance; _n = n; _spread = spread; _index = index; _grid = grid; }
        private double At(BigInteger twiceCenteredPosition) => _n.IsOne
            ? ExactMeanAccumulator.ScaledRatio(_compactSum, BigInteger.One, _grid - 1074)
            : ExactMeanAccumulator.ScaledRatio(_compactSum * _spread + 3 * _compactCovariance * twiceCenteredPosition, _n * _spread, _grid - 1074);
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
        internal BigInteger CenteredUnits(double meanPrice, double meanTime, double gain)
        {
            if (_n.IsOne) return ExactVarianceWindow.Units(meanPrice);
            var denominator = _n * _spread;
            var offset = (_index << 1074) - ExactVarianceWindow.Units(meanTime);
            var numerator = (ExactVarianceWindow.Units(meanPrice) * denominator << 2148) + 6 * _covariance * offset * ExactVarianceWindow.Units(gain);
            return RocBankValue.RoundUnits(numerator, denominator << 2148);
        }
        internal int Count => (int)_n;
        internal double Slope => _n.IsOne ? 0 : ExactMeanAccumulator.ScaledRatio(6 * _compactCovariance, _n * _spread, _grid - 1074);
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
        internal double WindowIntercept => At(1 - _n);
        internal double OneBasedWindowIntercept => At(-1 - _n);
        internal double OneBasedGlobalIntercept => At(_n - 3 - 2 * _index);
        internal double WindowPosition(int position) => At(2 * new BigInteger(position) - (_n - 1));
        internal double EndpointWeights(int period, bool averageDuringWarmup)
        {
            if (averageDuringWarmup && _n < period) return ExactMeanAccumulator.ScaledRatio(_compactSum, _n, _grid - 1074);
            var massFactor = 4 * new BigInteger(period) + 1 - 3 * _n;
            return ExactMeanAccumulator.ScaledRatio(_compactSum * massFactor + 3 * _compactCovariance, _n * massFactor, _grid - 1074);
        }
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

    private static (long Integer, int Grid) Parts(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        if (exponent == 2047) throw new ArgumentOutOfRangeException(nameof(value));
        var integer = bits & ((1L << 52) - 1);
        if (exponent != 0) integer |= 1L << 52;
        if (integer == 0) return (0, 0);
        var grid = exponent == 0 ? 0 : exponent - 1;
        var zeros = ExactMeanAccumulator.TrailingBinaryZeros(integer);
        integer >>= zeros; grid += zeros;
        return (bits < 0 ? -integer : integer, grid);
    }

    internal void Reset() { _window?.Clear(); _observed?.Clear(); _sum = default; _weighted = default; _index = default; _grid = 0; _hasGrid = false; }
    public void Dispose() { _window?.Dispose(); _observed?.Clear(); }
}
