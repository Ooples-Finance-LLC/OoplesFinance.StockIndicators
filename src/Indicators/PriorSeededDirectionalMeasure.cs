using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Directional outputs using a period-minus-one sum seed.</summary>
public enum PriorDirectionalMeasure
{
    /// <summary>Positive dominant movement, raw for period one and Wilder-smoothed otherwise.</summary>
    PositiveMovement,

    /// <summary>Negative dominant movement, raw for period one and Wilder-smoothed otherwise.</summary>
    NegativeMovement,

    /// <summary>Positive movement divided by true range; percent for periods greater than one.</summary>
    PositiveIndicator,

    /// <summary>Negative movement divided by true range; percent for periods greater than one.</summary>
    NegativeIndicator,

    /// <summary>DX with prior-value retention at zero denominators.</summary>
    Index,

    /// <summary>ADX with prior-value retention when direction is unavailable.</summary>
    Average,

    /// <summary>Mean of ADX endpoints separated by period minus one bars.</summary>
    Rating,
}

/// <summary>Selected TA-style directional measure with explicit unstable startup suppression.</summary>
/// <remarks>Exact differences select strictly dominant positive movement. Selected
/// movement, range, seed sums and Wilder stages round once with extended upper
/// exponents. Period one returns raw movement or a DI ratio without multiplying
/// by 100, ignoring unstable suppression. Longer periods seed with period minus
/// one changes. Zero range yields zero DI; DX retains its prior published value
/// after its first result, and ADX retains its prior average when direction is
/// absent. DX repeats the range update after its first published result, matching
/// the native recurrence. ADXR lags period minus one. History grows lazily and
/// lookbacks use 64-bit arithmetic.</remarks>
public sealed class PriorSeededDirectionalMeasure
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates a selected output, smoothing period and nonnegative startup suppression.</summary>
    public PriorSeededDirectionalMeasure(
        PriorDirectionalMeasure measure,
        int period = 14,
        int unstablePeriod = 0
    )
        : base(2)
    {
        if (
            measure < PriorDirectionalMeasure.PositiveMovement
            || measure > PriorDirectionalMeasure.Rating
        )
            throw new ArgumentOutOfRangeException(nameof(measure));
        if (period < (measure >= PriorDirectionalMeasure.Index ? 2 : 1))
            throw new ArgumentOutOfRangeException(nameof(period));
        if (unstablePeriod < 0)
            throw new ArgumentOutOfRangeException(nameof(unstablePeriod));
        Measure = measure;
        Period = period;
        UnstablePeriod = unstablePeriod;
    }

    /// <summary>Selected directional measure.</summary>
    public PriorDirectionalMeasure Measure { get; }

    /// <summary>Smoothing period.</summary>
    public int Period { get; }

    /// <summary>Additional startup bars; ignored for period one.</summary>
    public int UnstablePeriod { get; }

    /// <summary>Selected value, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the selected output is available.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Measure, Period, UnstablePeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        PriorDirectionalReference
                            .Calculate(bars, Measure, Period, UnstablePeriod)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(PriorDirectionalMeasure measure, int period, int unstable)
        : IMultiOutputState
    {
        private readonly Queue<double?> _history = new();
        private BigInteger _positive,
            _negative,
            _range,
            _seedDx;
        private Bar _previous;
        private long _index = -1;
        private double _average,
            _lastDx;

        private static BigInteger U(double x) => ExactVarianceWindow.Units(x);

        private static BigInteger R(BigInteger n, BigInteger d) => RocBankValue.RoundUnits(n, d);

        private static double Ratio(BigInteger n, BigInteger d) =>
            ExactMeanAccumulator.UnitRatio(n << 1074, d);

        public void Reset()
        {
            _history.Clear();
            _positive = _negative = _range = _seedDx = 0;
            _previous = default;
            _index = -1;
            _average = _lastDx = 0;
        }

        private static void Publish(double value, Span<double> output)
        {
            output[0] = value;
            output[1] = 1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (++_index == 0)
            {
                _previous = bar;
                return;
            }
            var up = U(bar.High) - U(_previous.High);
            var down = U(_previous.Low) - U(bar.Low);
            var positive = R(up > 0 && up > down ? up : BigInteger.Zero, 1);
            var negative = R(down > 0 && down > up ? down : BigInteger.Zero, 1);
            var range = R(
                BigInteger.Max(
                    U(bar.High) - U(bar.Low),
                    BigInteger.Max(
                        BigInteger.Abs(U(bar.High) - U(_previous.Close)),
                        BigInteger.Abs(U(bar.Low) - U(_previous.Close))
                    )
                ),
                1
            );
            _previous = bar;
            if (period == 1)
            {
                var movement = measure
                    is PriorDirectionalMeasure.PositiveMovement
                        or PriorDirectionalMeasure.PositiveIndicator
                    ? positive
                    : negative;
                Publish(
                    measure <= PriorDirectionalMeasure.NegativeMovement
                            ? ExactMeanAccumulator.UnitRatio(movement, 1)
                        : range.IsZero ? 0
                        : Ratio(movement, range),
                    output
                );
                return;
            }
            if (_index < period)
            {
                _positive += positive;
                _negative += negative;
                _range += range;
                if (_index == period - 1)
                {
                    _positive = R(_positive, 1);
                    _negative = R(_negative, 1);
                    _range = R(_range, 1);
                }
            }
            else
            {
                _positive = R(_positive * (period - 1) + positive * period, period);
                _negative = R(_negative * (period - 1) + negative * period, period);
                _range = R(_range * (period - 1) + range * period, period);
            }
            if (measure <= PriorDirectionalMeasure.NegativeMovement)
            {
                if (_index >= (long)period - 1 + unstable)
                    Publish(
                        ExactMeanAccumulator.UnitRatio(
                            measure == PriorDirectionalMeasure.PositiveMovement
                                ? _positive
                                : _negative,
                            1
                        ),
                        output
                    );
                return;
            }
            if (_index < period)
                return;
            var firstDx = (long)period + unstable;
            if (measure == PriorDirectionalMeasure.Index && _index > firstDx)
            {
                var currentRange = R(
                    BigInteger.Max(
                        U(bar.High) - U(bar.Low),
                        BigInteger.Max(
                            BigInteger.Abs(U(bar.High) - U(bar.Close)),
                            BigInteger.Abs(U(bar.Low) - U(bar.Close))
                        )
                    ),
                    1
                );
                _range = R(_range * (period - 1) + currentRange * period, period);
            }
            if (
                measure
                is PriorDirectionalMeasure.PositiveIndicator
                    or PriorDirectionalMeasure.NegativeIndicator
            )
            {
                if (_index >= firstDx)
                    Publish(
                        _range.IsZero
                            ? 0
                            : Ratio(
                                100
                                    * (
                                        measure == PriorDirectionalMeasure.PositiveIndicator
                                            ? _positive
                                            : _negative
                                    ),
                                _range
                            ),
                        output
                    );
                return;
            }
            var pdi = _range.IsZero ? BigInteger.Zero : R((100 * _positive) << 1074, _range);
            var mdi = _range.IsZero ? BigInteger.Zero : R((100 * _negative) << 1074, _range);
            var hasDirection = !(pdi + mdi).IsZero;
            var dx = hasDirection ? Ratio(100 * BigInteger.Abs(pdi - mdi), pdi + mdi) : 0;
            if (measure == PriorDirectionalMeasure.Index)
            {
                if (_index < firstDx)
                    return;
                if (_index == firstDx || hasDirection)
                    _lastDx = dx;
                Publish(_lastDx, output);
                return;
            }
            var seedIndex = 2L * period - 1;
            if (_index <= seedIndex)
            {
                _seedDx += U(dx);
                if (_index == seedIndex)
                    _average = ExactMeanAccumulator.UnitRatio(_seedDx, period);
            }
            else if (hasDirection)
                _average = ExactMeanAccumulator.UnitRatio(
                    U(_average) * (period - 1) + U(dx),
                    period
                );
            var available = _index >= seedIndex + unstable;
            if (measure == PriorDirectionalMeasure.Average)
            {
                if (available)
                    Publish(_average, output);
                return;
            }
            var prior = _history.Count == period - 1 ? _history.Dequeue() : null;
            _history.Enqueue(available ? _average : null);
            if (available && prior.HasValue)
                Publish(ExactMeanAccumulator.UnitRatio(U(_average) + U(prior.Value), 2), output);
        }
    }
}
