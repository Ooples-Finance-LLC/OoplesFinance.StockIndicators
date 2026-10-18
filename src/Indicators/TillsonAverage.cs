using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Startup convention for six-stage Tillson smoothing.</summary>
public enum TillsonSeed
{
    /// <summary>Seed all stages with the first price.</summary>
    FirstPrice,

    /// <summary>Publish the first price, then use expanding averages excluding that price until period bars arrive.</summary>
    FollowingPrefix,

    /// <summary>Seed each stage with a full mean of the preceding stage before starting the next.</summary>
    CascadedMeans,
}

/// <summary>Six-stage Tillson T3 with explicit startup and exact final polynomial evaluation.</summary>
/// <remarks>Each EMA stage uses the exact weight 2/(period+1), rounding once per
/// update. Prefix and seed means also round once. The final polynomial in the
/// finite volume factor is evaluated exactly over the rounded stages and rounds
/// once on publication, retaining cancellation of oversized coefficient terms.
/// CascadedMeans starts at index 6*(period-1); the other conventions start at zero.
/// Nonnegative suppression delays publication after that startup. Stage storage
/// is constant in the period and derived lookbacks use 64-bit arithmetic.</remarks>
public sealed class TillsonAverage : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a T3 average with a positive period and finite volume factor.</summary>
    public TillsonAverage(
        int period = 5,
        double volumeFactor = .7,
        TillsonSeed seed = TillsonSeed.FirstPrice,
        int suppression = 0
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (double.IsNaN(volumeFactor) || double.IsInfinity(volumeFactor))
            throw new ArgumentOutOfRangeException(nameof(volumeFactor));
        if (seed < TillsonSeed.FirstPrice || seed > TillsonSeed.CascadedMeans)
            throw new ArgumentOutOfRangeException(nameof(seed));
        if (suppression < 0)
            throw new ArgumentOutOfRangeException(nameof(suppression));
        Period = period;
        VolumeFactor = volumeFactor;
        Seed = seed;
        Suppression = suppression;
    }

    /// <summary>EMA period.</summary>
    public int Period { get; }

    /// <summary>Finite polynomial volume factor.</summary>
    public double VolumeFactor { get; }

    /// <summary>Startup convention.</summary>
    public TillsonSeed Seed { get; }

    /// <summary>Additional bars withheld after startup.</summary>
    public int Suppression { get; }

    /// <summary>T3 value or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the value is defined.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, VolumeFactor, Seed, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        TillsonReference
                            .Calculate(bars, Period, VolumeFactor, Seed, Suppression)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State : IMultiOutputState, IWideAverageState
    {
        private readonly int _period;
        private readonly TillsonSeed _seed;
        private readonly long _first;
        private readonly BigInteger[] _coefficients;
        private readonly BigInteger _denominator;
        private readonly BigInteger[] _stages = new BigInteger[6],
            _sums = new BigInteger[6];
        private long _index = -1;
        private BigInteger _lastNumerator;
        public BigInteger RoundedUnits => RocBankValue.RoundUnits(_lastNumerator, _denominator);

        internal State(int period, double factor, TillsonSeed seed, int suppression)
        {
            _period = period;
            _seed = seed;
            _first = (seed == TillsonSeed.CascadedMeans ? 6L * (period - 1) : 0) + suppression;
            var units = ExactVarianceWindow.Units(factor);
            var grid = BigInteger.One << 1074;
            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(units), grid);
            var n = units / divisor;
            var q = grid / divisor;
            var combined = q + n;
            // Exact coefficients of E3..E6 over the common denominator q^3.
            // Cache these fixed products instead of reconstructing the polynomial
            // on every bar. Oversized terms still cancel before final rounding.
            _coefficients =
            [
                combined * combined * combined,
                -3 * n * combined * combined,
                3 * n * n * combined,
                -n * n * n
            ];
            _denominator = q * q * q;
        }

        public void Reset()
        {
            Array.Clear(_stages, 0, 6);
            Array.Clear(_sums, 0, 6);
            _index = -1;
            _lastNumerator = 0;
        }

        public void Update(in Bar bar, Span<double> output) =>
            UpdateUnits(ExactVarianceWindow.Units(bar.Close), output);

        public void UpdateUnits(BigInteger value, Span<double> output)
        {
            output.Clear();
            _index++;
            for (var j = 0; j < 6; j++)
            {
                if (_seed == TillsonSeed.CascadedMeans)
                {
                    var start = (long)j * (_period - 1);
                    if (_index < start)
                        return;
                    if (_index < start + _period)
                    {
                        _sums[j] += value;
                        if (_index != start + _period - 1)
                            return;
                        _stages[j] = RocBankValue.RoundUnits(_sums[j], _period);
                        _sums[j] = 0;
                    }
                    else
                        _stages[j] = Smooth(_stages[j], value);
                }
                else if (_index == 0)
                    _stages[j] = value;
                else if (_seed == TillsonSeed.FollowingPrefix && _index < _period)
                {
                    _sums[j] += value;
                    _stages[j] = RocBankValue.RoundUnits(_sums[j], _index);
                }
                else
                    _stages[j] = Smooth(_stages[j], value);
                value = _stages[j];
            }
            if (_index < _first)
                return;
            _lastNumerator = _coefficients[0] * _stages[2]
                + _coefficients[1] * _stages[3]
                + _coefficients[2] * _stages[4]
                + _coefficients[3] * _stages[5];
            output[0] = ExactMeanAccumulator.UnitRatio(_lastNumerator, _denominator);
            output[1] = 1;
        }

        private BigInteger Smooth(BigInteger prior, BigInteger input) =>
            RocBankValue.RoundUnits(prior * (_period - 1) + 2 * input, (long)_period + 1);
    }
}
