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

        internal int Count => _history.Count;
        internal double Mean => Count == 0 ? 0 : ExactMeanAccumulator.ScaledRatio(_sum, Count, _grid);
        internal bool HasVariance => Count > 1 && Count * _squares > _sum * _sum;

        public void Reset()
        {
            _history.Clear();
            _sum = _squares = _current = 0;
            _grid = 0;
            _hasGrid = false;
        }

        public double Update(in Bar bar)
        {
            var parts = Decompose(bar.Close);
            if (parts.Integer != 0 && (!_hasGrid || parts.Exponent < _grid))
            {
                if (_hasGrid)
                {
                    var shift = _grid - parts.Exponent;
                    _sum <<= shift;
                    _squares <<= 2 * shift;
                }
                _grid = parts.Exponent;
                _hasGrid = true;
            }
#if !NETFRAMEWORK
            if (TrySmallUpdate(parts, bar.Close)) return Reading(output);
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
#endif
            var n = new BigInteger(_history.Count);
            var variance = n * _squares - _sum * _sum;
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
            var deviation = (n * _current - _sum) * _scale.Integer;
            return deviation.Sign
                * ExactPopulationDeviation.ScaledRootRatio(
                    deviation * deviation * denominator,
                    n * n * variance, 2 * _scale.Exponent
                );
        }

#if !NETFRAMEWORK
        private bool TrySmallUpdate((long Integer, int Exponent) parts, double close)
        {
            if (_sum.GetBitLength() > 31 || _squares.GetBitLength() > 31
                || !TrySmallGrid(parts, out long current)) return false;
            bool full = _history.Count == period;
            long expired = 0;
            if (full && !TrySmallGrid(Decompose(_history.Peek()), out expired)) return false;
            // Each price magnitude is at most 2^26 and retained moments fit
            // signed 32-bit values. All intermediate updates fit signed 64 bits.
            long sum = (long)_sum + current - expired;
            long squares = (long)_squares + current * current - expired * expired;
            if (full) _history.Dequeue();
            _history.Enqueue(close);
            _sum = sum;
            _squares = squares;
            _current = current;
            return true;
        }

        private bool TrySmallGrid((long Integer, int Exponent) parts, out long value)
        {
            value = 0;
            if (parts.Integer == 0) return true;
            int shift = parts.Exponent - _grid;
            const long maximum = 1L << 26;
            if (shift is < 0 or > 26 || Math.Abs(parts.Integer) > (maximum >> shift)) return false;
            value = parts.Integer << shift;
            return true;
        }

        private bool TrySmallReading(WindowDispersionOutput selected, out double result)
        {
            result = 0;
            // These bounds make n*squares, sum*sum and n*current-sum exact Int64
            // operations. A power-of-two multiplier changes only the binary grid.
            int n = _history.Count;
            if (n > 4096 || _scale.Integer is not (1 or -1)
                || _sum.GetBitLength() > 31 || _squares.GetBitLength() > 31) return false;
            long sum = (long)_sum;
            long variance = n * (long)_squares - sum * sum;
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
            if (_current.GetBitLength() > 31) return false;
            long deviation = n * (long)_current - sum;
            ulong magnitude = (ulong)Math.Abs(deviation);
            if (magnitude == 0) return true;
            if (magnitude > ulong.MaxValue / magnitude) return false;
            ulong top = magnitude * magnitude, bottom = (ulong)variance;
            // Cancel n^2 exactly for population Z; for sample Z retain (n-1)/n.
            if (sample)
            {
                if (top > ulong.MaxValue / (uint)(n - 1) || bottom > ulong.MaxValue / (uint)n) return false;
                top *= (uint)(n - 1); bottom *= (uint)n;
            }
            if (!TryCompactRoot(top, bottom, 2 * _scale.Exponent, out result)) return false;
            result *= Math.Sign(deviation) * _scale.Integer;
            return true;
        }

        private static bool TryCompactRoot(ulong numerator, ulong denominator, int power, out double result) =>
            numerator <= (1UL << 53) && denominator <= 4096
                ? ExactPopulationDeviation.TrySmallScaledRoot(numerator, (uint)denominator, power, out result)
                : ExactPopulationDeviation.TryWideScaledRoot(numerator, denominator, power, out result);
#endif

        private BigInteger OnGrid((long Integer, int Exponent) value) => value.Integer == 0
            ? BigInteger.Zero : new BigInteger(value.Integer) << (value.Exponent - _grid);

        private static (long Integer, int Exponent) Decompose(double value) => ExactMeanAccumulator.DecomposeFinite(value);
    }
}
