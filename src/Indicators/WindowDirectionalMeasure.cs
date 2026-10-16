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

    internal sealed class State(DirectionalWindowMeasure measure, int period, int lag)
        : IMultiOutputState
    {
        private readonly SeededAtrWindow? _atr = measure >= DirectionalWindowMeasure.PositiveIndicator ? new(period) : null;
        private readonly Queue<double?>? _history = measure == DirectionalWindowMeasure.EarlyRating && lag > 0 ? new() : null;
        private readonly bool _needsPositive = measure != DirectionalWindowMeasure.NegativeIndicator;
        private readonly bool _needsNegative = measure != DirectionalWindowMeasure.PositiveIndicator;
        private RocBankValue _positive, _negative;
        private ExactMeanAccumulator _seedPositive, _seedNegative;
        private double _previousHigh, _previousLow;
        private bool _started,
            _averageStarted;
        private int _count;
        private double? _average;

        public void Reset()
        {
            _atr?.Reset();
            _history?.Clear();
            _positive = _negative = default;
            _seedPositive = _seedNegative = default;
            _previousHigh = _previousLow = 0;
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

        private static ExactMeanAccumulator Difference(double left, double right)
        {
            var sum = new ExactMeanAccumulator(); sum.Add(left); sum.Add(right, -1); return sum;
        }

        private RocBankValue Smooth(RocBankValue previous, RocBankValue current)
        {
            var sum = Exact(previous);
            sum.Multiply(period - 1);
            sum.AddExact(Exact(current));
            return DivideRounded(sum, JurikDyadic.FromDouble(period));
        }

        private static JurikDyadic Exact(RocBankValue value)
        {
            var result = JurikDyadic.FromDouble(value.Mantissa);
            if (value.UpperShift != 0) result.ScaleByPowerOfTwo(value.UpperShift);
            return result;
        }

        private static RocBankValue DivideRounded(JurikDyadic numerator, JurikDyadic denominator)
        {
            for (var shift = 0; ; shift += 1024)
            {
                var value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value)) return new RocBankValue(value, shift);
                numerator.ScaleByPowerOfTwo(-1024);
            }
        }

        private RocBankValue Direction(RocBankValue movement)
        {
            var numerator = Exact(movement);
            numerator.Multiply(100);
            // DI is rounded before DX but remains unpublished and may exceed
            // binary64's upper exponent. Preserve that range until publication.
            return DivideRounded(numerator, Exact(_atr!.Average));
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var first = !_started;
            _started = true;
            var up = first || measure == DirectionalWindowMeasure.NegativeMovement
                ? default : Difference(bar.High, _previousHigh);
            var down = first || measure == DirectionalWindowMeasure.PositiveMovement
                ? default : Difference(_previousLow, bar.Low);
            _previousHigh = bar.High; _previousLow = bar.Low;
            if (measure <= DirectionalWindowMeasure.NegativeMovement)
            {
                if (!first)
                    Publish((measure == DirectionalWindowMeasure.PositiveMovement ? up : down).Mean(1), output);
                return;
            }
            _atr!.Add(bar);
            double? dx = 0;
            if (!first)
            {
                var dominance = up; dominance.Subtract(down);
                var p = _needsPositive && up.Sign > 0 && dominance.Sign > 0 ? RocBankValue.Round(up) : default;
                var m = _needsNegative && down.Sign > 0 && dominance.Sign < 0 ? RocBankValue.Round(down) : default;
                if (_count < period)
                {
                    if (_needsPositive) p.AddTo(ref _seedPositive);
                    if (_needsNegative) m.AddTo(ref _seedNegative);
                    if (++_count == period)
                    {
                        if (_needsPositive) _positive = RocBankValue.Round(_seedPositive, count: period);
                        if (_needsNegative) _negative = RocBankValue.Round(_seedNegative, count: period);
                        _seedPositive = _seedNegative = default;
                    }
                }
                else
                {
                    if (_needsPositive) _positive = Smooth(_positive, p);
                    if (_needsNegative) _negative = Smooth(_negative, m);
                }
            }
            if (_atr.HasAverage && _atr.Average.Mantissa != 0) // NOSONAR: Exact zero range defines missing DI.
            {
                var pdi = _needsPositive ? Direction(_positive) : default;
                var mdi = _needsNegative ? Direction(_negative) : default;
                if (
                    measure
                    is DirectionalWindowMeasure.PositiveIndicator
                        or DirectionalWindowMeasure.NegativeIndicator
                )
                {
                    Publish((measure == DirectionalWindowMeasure.PositiveIndicator ? pdi : mdi).Publish(), output);
                    return;
                }
                var total = Exact(pdi);
                total.AddExact(Exact(mdi));
                var difference = Exact(pdi);
                difference.Subtract(Exact(mdi));
                difference.Multiply(100);
                if (difference.Sign < 0)
                {
                    difference.Multiply(-1);
                }
                dx = total.Sign == 0 ? null : difference.Ratio(total);
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
                    var seed = new ExactMeanAccumulator();
                    if (dx.HasValue) seed.Add(dx.Value);
                    _average = dx.HasValue ? seed.Mean(period) : period > 1 ? 0 : null;
                    _averageStarted = true;
                }
                else if (_average.HasValue && dx.HasValue)
                {
                    var sum = JurikDyadic.FromDouble(_average.Value);
                    sum.Multiply(period - 1);
                    sum.Add(dx.Value);
                    _average = sum.Ratio(JurikDyadic.FromDouble(period));
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
                : _history!.Count == lag ? _history.Dequeue()
                : null;
            if (lag > 0)
                _history!.Enqueue(_average);
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
