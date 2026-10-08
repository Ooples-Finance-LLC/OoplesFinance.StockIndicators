using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Directional outputs with mean-seeded Wilder smoothing and Trady-compatible early ADX.</summary>
public enum DirectionalWindowMeasure
{
    /// <summary>Signed high minus previous high.</summary>
    PositiveMovement,

    /// <summary>Signed previous low minus low.</summary>
    NegativeMovement,

    /// <summary>Smoothed positive dominant movement divided by ATR, times 100.</summary>
    PositiveIndicator,

    /// <summary>Smoothed negative dominant movement divided by ATR, times 100.</summary>
    NegativeIndicator,

    /// <summary>DX, with zero when DI is unavailable and absence when both mature DI values are zero.</summary>
    Index,

    /// <summary>ADX seeded at index period from DX indices 1 through period, including startup zeros.</summary>
    EarlyAverage,

    /// <summary>Mean of current early ADX and the early ADX ratingLag bars earlier.</summary>
    EarlyRating,
}

/// <summary>Raw or mean-seeded directional measures with explicit early-ADX conventions.</summary>
/// <remarks>Raw movements stay signed. Smoothed directions select strictly dominant
/// positive changes using exact differences, then round each selected movement and
/// each Wilder mean once with extended upper exponents. DI ratios round once before
/// DX is formed. Missing DI produces zero DX, while two mature zero DI values make
/// DX absent. Early ADX seeds at index period, averaging available DX values from
/// indices 1 through period; subsequent missing DX permanently makes that recurrence
/// absent. Ratings require both endpoints. Rating history grows lazily.</remarks>
public sealed class WindowDirectionalMeasure
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates a selected directional output with positive smoothing period and nonnegative rating lag.</summary>
    public WindowDirectionalMeasure(
        DirectionalWindowMeasure measure,
        int period = 14,
        int ratingLag = 14
    )
        : base(2)
    {
        if (
            measure < DirectionalWindowMeasure.PositiveMovement
            || measure > DirectionalWindowMeasure.EarlyRating
        )
            throw new ArgumentOutOfRangeException(nameof(measure));
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (ratingLag < 0)
            throw new ArgumentOutOfRangeException(nameof(ratingLag));
        Measure = measure;
        Period = period;
        RatingLag = ratingLag;
    }

    /// <summary>Selected output.</summary>
    public DirectionalWindowMeasure Measure { get; }

    /// <summary>Wilder mean period; unused by the two raw movement outputs.</summary>
    public int Period { get; }

    /// <summary>Rating delay; used only by EarlyRating.</summary>
    public int RatingLag { get; }

    /// <summary>Selected measure, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the selected measure is defined.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Measure, Period, RatingLag);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        DirectionalMeanReference
                            .Calculate(bars, Period, RatingLag)[(int)Measure]
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(DirectionalWindowMeasure measure, int period, int lag)
        : IMultiOutputState
    {
        private readonly SeededAtrWindow _atr = new(period);
        private readonly Queue<double?> _history = new();
        private BigInteger _positive,
            _negative,
            _seedPositive,
            _seedNegative;
        private Bar _previous;
        private bool _started,
            _averageStarted;
        private int _count;
        private double? _average;

        public void Reset()
        {
            _atr.Reset();
            _history.Clear();
            _positive = _negative = _seedPositive = _seedNegative = 0;
            _previous = default;
            _started = _averageStarted = false;
            _count = 0;
            _average = null;
        }

        private static void Publish(double? value, Span<double> output)
        {
            if (value.HasValue)
            {
                output[0] = value.Value;
                output[1] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var first = !_started;
            _started = true;
            var up = first
                ? BigInteger.Zero
                : ExactVarianceWindow.Units(bar.High) - ExactVarianceWindow.Units(_previous.High);
            var down = first
                ? BigInteger.Zero
                : ExactVarianceWindow.Units(_previous.Low) - ExactVarianceWindow.Units(bar.Low);
            _previous = bar;
            if (measure <= DirectionalWindowMeasure.NegativeMovement)
            {
                if (!first)
                    Publish(
                        ExactMeanAccumulator.UnitRatio(
                            measure == DirectionalWindowMeasure.PositiveMovement ? up : down,
                            BigInteger.One
                        ),
                        output
                    );
                return;
            }
            _atr.Add(bar);
            double? dx = 0;
            if (!first)
            {
                var p = RocBankValue.RoundUnits(
                    up > 0 && up > down ? up : BigInteger.Zero,
                    BigInteger.One
                );
                var m = RocBankValue.RoundUnits(
                    down > 0 && down > up ? down : BigInteger.Zero,
                    BigInteger.One
                );
                if (_count < period)
                {
                    _seedPositive += p;
                    _seedNegative += m;
                    if (++_count == period)
                    {
                        _positive = RocBankValue.RoundUnits(_seedPositive, period);
                        _negative = RocBankValue.RoundUnits(_seedNegative, period);
                        _seedPositive = _seedNegative = 0;
                    }
                }
                else
                {
                    _positive = RocBankValue.RoundUnits(_positive * (period - 1) + p, period);
                    _negative = RocBankValue.RoundUnits(_negative * (period - 1) + m, period);
                }
            }
            if (_atr.HasAverage && _atr.Average.Mantissa != 0) // NOSONAR: Exact zero range defines missing DI.
            {
                var range =
                    ExactVarianceWindow.Units(_atr.Average.Mantissa) << _atr.Average.UpperShift;
                var pdi = RocBankValue.RoundUnits((100 * _positive) << 1074, range);
                var mdi = RocBankValue.RoundUnits((100 * _negative) << 1074, range);
                if (
                    measure
                    is DirectionalWindowMeasure.PositiveIndicator
                        or DirectionalWindowMeasure.NegativeIndicator
                )
                {
                    Publish(
                        ExactMeanAccumulator.UnitRatio(
                            measure == DirectionalWindowMeasure.PositiveIndicator ? pdi : mdi,
                            BigInteger.One
                        ),
                        output
                    );
                    return;
                }
                dx = (pdi + mdi).IsZero
                    ? null
                    : ExactMeanAccumulator.UnitRatio(
                        (100 * BigInteger.Abs(pdi - mdi)) << 1074,
                        pdi + mdi
                    );
            }
            else if (
                measure
                is DirectionalWindowMeasure.PositiveIndicator
                    or DirectionalWindowMeasure.NegativeIndicator
            )
                return;
            if (measure == DirectionalWindowMeasure.Index)
            {
                Publish(dx, output);
                return;
            }
            if (_count == period)
            {
                if (!_averageStarted)
                {
                    _average =
                        dx.HasValue
                            ? ExactMeanAccumulator.UnitRatio(
                                ExactVarianceWindow.Units(dx.Value),
                                period
                            )
                        : period > 1 ? 0
                        : null;
                    _averageStarted = true;
                }
                else if (_average.HasValue && dx.HasValue)
                {
                    var sum = new ExactMeanAccumulator();
                    sum.Add(_average.Value, period - 1);
                    sum.Add(dx.Value);
                    _average = sum.Mean(period);
                }
                else
                    _average = null;
            }
            if (measure == DirectionalWindowMeasure.EarlyAverage)
            {
                Publish(_average, output);
                return;
            }
            var prior =
                lag == 0 ? _average
                : _history.Count == lag ? _history.Dequeue()
                : null;
            if (lag > 0)
                _history.Enqueue(_average);
            if (prior.HasValue && _average.HasValue)
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(prior.Value);
                sum.Add(_average.Value);
                Publish(sum.Mean(2), output);
            }
        }
    }
}
