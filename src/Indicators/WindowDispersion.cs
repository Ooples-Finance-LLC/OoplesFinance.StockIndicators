using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Selected statistic of a rolling close distribution.</summary>
public enum WindowDispersionOutput
{
    /// <summary>Mean squared distance from the current window's mean.</summary>
    Variance,

    /// <summary>Square root of the current window's variance.</summary>
    StandardDeviation,

    /// <summary>Latest close's distance from the current window's mean, divided by its deviation.</summary>
    ZScore,
}

/// <summary>Sample or population rolling dispersion with exact moments and available-history startup.</summary>
/// <remarks>Sample statistics divide squared deviations by count-1; population statistics
/// divide by count. One observation produces zero. A constant window has zero Z-score.
/// The optional multiplier scales the selected mathematical result before final rounding,
/// preserving finite results across unrepresentable intermediate variance or deviation.
/// Every window uses its own mean. History grows lazily up to the requested period.</remarks>
public sealed class WindowDispersion : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a rolling dispersion calculation.</summary>
    /// <param name="period">Positive maximum window size.</param>
    /// <param name="output">Variance, deviation, or latest close's Z-score.</param>
    /// <param name="sample">Whether to use count-1 instead of count in the variance denominator.</param>
    /// <param name="multiplier">Finite signed scale applied before final rounding.</param>
    public WindowDispersion(
        int period = 20,
        WindowDispersionOutput output = WindowDispersionOutput.StandardDeviation,
        bool sample = false,
        double multiplier = 1
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(output.GetType(), output))
            throw new ArgumentOutOfRangeException(nameof(output));
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        Period = period;
        Output = output;
        Sample = sample;
        Multiplier = multiplier;
    }

    /// <summary>Maximum observations in the current window.</summary>
    public int Period { get; }

    /// <summary>Selected statistic.</summary>
    public WindowDispersionOutput Output { get; }

    /// <summary>Whether squared deviations use the sample denominator count-1.</summary>
    public bool Sample { get; }

    /// <summary>Signed scale applied to the mathematical statistic before final rounding.</summary>
    public double Multiplier { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Output, Sample, Multiplier);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        var scale = ReferenceFraction.FromDouble(Multiplier);
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(Period, i + 1);
            if (count < 2)
                continue;
            var values = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var mean =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(count);
            var variance =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + (b - mean) * (b - mean))
                / new ReferenceFraction(Sample ? count - 1 : count);
            if (Output == WindowDispersionOutput.Variance)
                result[i] = (variance * scale).ToDouble();
            else if (Output == WindowDispersionOutput.StandardDeviation)
                result[i] = scale.Sign * (variance * scale * scale).SqrtToDouble();
            else if (variance.Sign != 0)
            {
                var deviation = (values[values.Length - 1] - mean) * scale;
                result[i] = deviation.Sign * (deviation * deviation / variance).SqrtToDouble();
            }
        }
        return result;
    }

    internal sealed class State(
        int period,
        WindowDispersionOutput output,
        bool sample,
        double multiplier
    ) : IIndicatorState
    {
        private readonly Queue<double> _history = new();
        private readonly (long Integer, int Exponent) _scale = Decompose(multiplier);
        // Exact integer moments on the finest grid observed since reset. The
        // exponent is separate: 12.5 needs integer 25, not 1,078 integer bits.
        private int _grid;
        private bool _hasGrid;
        private BigInteger _sum,
            _squares,
            _current;
#if !NETFRAMEWORK
        private Int128 _compactSum, _compactSquares, _compactCurrent;
        private bool _wide;
        private BigInteger Sum => _wide ? _sum : (BigInteger)_compactSum;
        private BigInteger Squares => _wide ? _squares : (BigInteger)_compactSquares;
        private BigInteger Current => _wide ? _current : (BigInteger)_compactCurrent;
#else
        private BigInteger Sum => _sum;
        private BigInteger Squares => _squares;
        private BigInteger Current => _current;
#endif

        internal int Count => _history.Count;
        internal double Mean
        {
            get
            {
                if (Count == 0) return 0;
#if !NETFRAMEWORK
                if (!_wide && CertifiedCompactRatio.TryDivide(Magnitude(_compactSum),
                    (UInt128)Count, _grid, out double value)) return _compactSum < 0 ? -value : value;
#endif
                return ExactMeanAccumulator.ScaledRatio(Sum, Count, _grid);
            }
        }
        internal bool HasVariance
        {
            get
            {
                if (Count < 2) return false;
#if !NETFRAMEWORK
                if (!_wide)
                {
                    try { return checked(Count * _compactSquares - _compactSum * _compactSum) > 0; }
                    catch (OverflowException) { }
                }
#endif
                return Count * Squares > Sum * Sum;
            }
        }

        public void Reset()
        {
            _history.Clear();
            _sum = _squares = _current = 0;
            _grid = 0;
            _hasGrid = false;
#if !NETFRAMEWORK
            _compactSum = _compactSquares = _compactCurrent = 0;
            _wide = false;
#endif
        }

        public double Update(in Bar bar)
        {
            var parts = Decompose(bar.Close);
            if (parts.Integer != 0 && (!_hasGrid || parts.Exponent < _grid))
            {
                if (_hasGrid)
                {
                    var shift = _grid - parts.Exponent;
#if !NETFRAMEWORK
                    if (_wide || !TryRescale(shift))
                    {
                        Promote();
                        _sum <<= shift;
                        _squares <<= 2 * shift;
                    }
#else
                    _sum <<= shift;
                    _squares <<= 2 * shift;
#endif
                }
                _grid = parts.Exponent;
                _hasGrid = true;
            }
#if !NETFRAMEWORK
            if (!_wide && TryCompactUpdate(parts, bar.Close)) return Reading(output);
            Promote();
#endif
            var value = OnGrid(parts);
            if (_history.Count == period)
            {
                var expired = OnGrid(Decompose(_history.Dequeue()));
                _sum -= expired;
                _squares -= expired * expired;
            }
            _history.Enqueue(bar.Close);
            _sum += value;
            _squares += value * value;
            _current = value;
            return Reading(output);
        }

        internal double Reading(WindowDispersionOutput selected)
        {
            if (_history.Count < 2 || _scale.Integer == 0)
                return 0;
#if !NETFRAMEWORK
            if (TrySmallReading(selected, out var compact)) return compact;
            if (TryCompactReading(selected, out compact)) return compact;
#endif
            var n = new BigInteger(_history.Count);
            var sum = Sum;
            var variance = n * Squares - sum * sum;
            var denominator = n * (sample ? n - 1 : n);
            if (selected == WindowDispersionOutput.Variance)
                return ExactMeanAccumulator.ScaledRatio(variance * _scale.Integer, denominator,
                    2 * _grid + _scale.Exponent);
            if (selected == WindowDispersionOutput.StandardDeviation)
                return Math.Sign(_scale.Integer)
                    * ExactPopulationDeviation.ScaledRootRatio(
                        variance * _scale.Integer * _scale.Integer,
                        denominator, 2 * (_grid + _scale.Exponent)
                    );
            if (variance.IsZero)
                return 0;
            var deviation = (n * Current - sum) * _scale.Integer;
            return deviation.Sign
                * ExactPopulationDeviation.ScaledRootRatio(
                    deviation * deviation * denominator,
                    n * n * variance, 2 * _scale.Exponent
                );
        }

#if !NETFRAMEWORK
        private void Promote()
        {
            if (_wide) return;
            _sum = (BigInteger)_compactSum;
            _squares = (BigInteger)_compactSquares;
            _current = (BigInteger)_compactCurrent;
            _wide = true;
        }

        private static UInt128 Magnitude(Int128 value) => value < 0
            ? (UInt128)(-(value + 1)) + 1 : (UInt128)value;

        private static bool TryShift(Int128 value, int shift, out Int128 shifted)
        {
            shifted = 0;
            if (value == 0) return true;
            if (shift is < 0 or >= 128 || value < (Int128.MinValue >> shift)
                || value > (Int128.MaxValue >> shift)) return false;
            shifted = value << shift;
            return true;
        }

        private bool TryRescale(int shift)
        {
            if (!TryShift(_compactSum, shift, out var sum)
                || !TryShift(_compactSquares, 2 * shift, out var squares)
                || !TryShift(_compactCurrent, shift, out var current)) return false;
            _compactSum = sum; _compactSquares = squares; _compactCurrent = current;
            return true;
        }

        private bool TryCompactGrid((long Integer, int Exponent) parts, out Int128 value) =>
            TryShift(parts.Integer, parts.Integer == 0 ? 0 : parts.Exponent - _grid, out value);

        private bool TryCompactUpdate((long Integer, int Exponent) parts, double close)
        {
            if (!TryCompactGrid(parts, out var current)) return false;
            bool full = _history.Count == period;
            Int128 expired = 0;
            if (full && !TryCompactGrid(Decompose(_history.Peek()), out expired)) return false;
            try
            {
                // Do not mutate history or moments until every operation fits.
                Int128 sum = checked(_compactSum - expired + current);
                Int128 squares = checked(_compactSquares - expired * expired + current * current);
                if (full) _history.Dequeue();
                _history.Enqueue(close);
                _compactSum = sum; _compactSquares = squares; _compactCurrent = current;
                return true;
            }
            catch (OverflowException) { return false; }
        }

        private bool TryCompactReading(WindowDispersionOutput selected, out double result)
        {
            result = 0;
            if (_wide) return false;
            try
            {
                int n = Count;
                Int128 variance = checked(n * _compactSquares - _compactSum * _compactSum);
                ulong denominator = (ulong)n * (uint)(sample ? n - 1 : n);
                if (selected == WindowDispersionOutput.Variance)
                {
                    Int128 numerator = checked(variance * _scale.Integer);
                    if (!CertifiedCompactRatio.TryDivide(Magnitude(numerator), denominator,
                        2 * _grid + _scale.Exponent, out result)) return false;
                    if (numerator < 0) result = -result;
                    return true;
                }
                if (selected == WindowDispersionOutput.StandardDeviation)
                {
                    UInt128 numerator = checked((UInt128)variance * Magnitude(_scale.Integer) * Magnitude(_scale.Integer));
                    if (!ExactPopulationDeviation.TryCompactScaledRoot(numerator, denominator,
                        2 * (_grid + _scale.Exponent), out result)) return false;
                    result *= Math.Sign(_scale.Integer);
                    return true;
                }
                if (variance == 0) return true;
                Int128 deviation = checked(n * _compactCurrent - _compactSum);
                UInt128 magnitude = Magnitude(deviation);
                UInt128 top = checked(magnitude * magnitude);
                UInt128 bottom = (UInt128)variance;
                if (sample) { top = checked(top * (uint)(n - 1)); bottom = checked(bottom * (uint)n); }
                UInt128 scale = Magnitude(_scale.Integer);
                top = checked(top * scale * scale);
                if (bottom > ulong.MaxValue || !ExactPopulationDeviation.TryCompactScaledRoot(top,
                    (ulong)bottom, 2 * _scale.Exponent, out result)) return false;
                result *= deviation.CompareTo(0) * Math.Sign(_scale.Integer);
                return true;
            }
            catch (OverflowException) { return false; }
        }

        private bool TrySmallReading(WindowDispersionOutput selected, out double result)
        {
            result = 0;
            // These bounds make n*squares, sum*sum and n*current-sum exact Int64
            // operations. A power-of-two multiplier changes only the binary grid.
            int n = _history.Count;
            if (_wide || n > 4096 || _scale.Integer is not (1 or -1)
                || _compactSum < int.MinValue || _compactSum > int.MaxValue || _compactSquares > int.MaxValue) return false;
            long sum = (long)_compactSum;
            long variance = n * (long)_compactSquares - sum * sum;
            long denominator = (long)n * (sample ? n - 1 : n);
            if (selected == WindowDispersionOutput.Variance)
            {
                long numerator = variance * _scale.Integer;
                int power = 2 * _grid + _scale.Exponent;
                // Exact binary64 integer operands plus a normal final exponent
                // allow one hardware division, then an exact exponent adjustment.
                if (variance <= (1L << 53))
                {
                    if (variance == 0) return true;
                    long bits = BitConverter.DoubleToInt64Bits((double)numerator / denominator);
                    long exponent = ((bits >> 52) & 2047) + power;
                    if (exponent is > 0 and < 2047)
                    {
                        result = BitConverter.Int64BitsToDouble(bits + ((long)power << 52));
                        return true;
                    }
                }
                result = ExactMeanAccumulator.ScaledRatio(numerator, denominator, power);
                return true;
            }
            if (selected == WindowDispersionOutput.StandardDeviation)
            {
                if (variance == 0) { result = _scale.Integer * 0d; return true; }
                if (!TryCompactRoot((ulong)variance, (ulong)denominator,
                    2 * (_grid + _scale.Exponent), out result)) return false;
                result *= _scale.Integer;
                return true;
            }
            if (variance == 0) return true;
            if (_compactCurrent < int.MinValue || _compactCurrent > int.MaxValue) return false;
            long deviation = n * (long)_compactCurrent - sum;
            ulong magnitude = (ulong)Math.Abs(deviation);
            if (magnitude == 0) return true;
            if (magnitude > ulong.MaxValue / magnitude) return false;
            ulong top = magnitude * magnitude, bottom = (ulong)variance;
            // Cancel n^2 exactly for population Z; for sample Z retain (n-1)/n.
            if (sample)
            {
                if (top > ulong.MaxValue / (uint)(n - 1) || bottom > ulong.MaxValue / (uint)n) return false;
#pragma warning disable S3949 // n is 2..4096; the preceding MaxValue/factor guards prove both unsigned products fit.
                top = checked(top * (uint)(n - 1)); bottom = checked(bottom * (uint)n);
#pragma warning restore S3949
            }
            if (!TryCompactRoot(top, bottom, 2 * _scale.Exponent, out result)) return false;
            result *= Math.Sign(deviation) * _scale.Integer;
            return true;
        }

        private static bool TryCompactRoot(ulong numerator, ulong denominator, int power, out double result) =>
            numerator <= (1UL << 53) && denominator <= ExactPopulationDeviation.SmallRootMaximumDenominator
                ? ExactPopulationDeviation.TrySmallScaledRoot(numerator, (uint)denominator, power, out result)
                : ExactPopulationDeviation.TryWideScaledRoot(numerator, denominator, power, out result);
#endif

        private BigInteger OnGrid((long Integer, int Exponent) value) => value.Integer == 0
            ? BigInteger.Zero : new BigInteger(value.Integer) << (value.Exponent - _grid);

        private static (long Integer, int Exponent) Decompose(double value) => ExactMeanAccumulator.DecomposeFinite(value);
    }
}
