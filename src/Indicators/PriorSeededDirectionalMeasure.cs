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
        private readonly Queue<double?>? _history = measure == PriorDirectionalMeasure.Rating ? new() : null;
        private readonly bool _needsPositive = measure is not (PriorDirectionalMeasure.NegativeMovement or PriorDirectionalMeasure.NegativeIndicator);
        private readonly bool _needsNegative = measure is not (PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator);
        private readonly bool _needsRange = measure >= PriorDirectionalMeasure.PositiveIndicator;
        private readonly ExactMeanAccumulator.ProductCoefficient _priorCoefficient = new(period - 1);
        private readonly ExactMeanAccumulator.ProductCoefficient _currentCoefficient = new(period);
        private RocBankValue _positive, _negative, _range;
        private ExactMeanAccumulator _positiveSeed, _negativeSeed, _rangeSeed, _seedDx;
#if NETFRAMEWORK
        private ExactMeanAccumulator.ProductFactor _previousHigh, _previousLow, _previousClose;
#endif
        private double _previousClosePrice;
#if !NETFRAMEWORK
        private double _previousHighPrice, _previousLowPrice;
#endif
        private long _index = -1;
        private double _average, _lastDx;

        public void Reset()
        {
            _history?.Clear();
            _positive = _negative = _range = default;
            _positiveSeed = _negativeSeed = _rangeSeed = _seedDx = default;
#if NETFRAMEWORK
            _previousHigh = _previousLow = _previousClose = default;
#endif
            _previousClosePrice = 0;
#if !NETFRAMEWORK
            _previousHighPrice = _previousLowPrice = 0;
#endif
            _index = -1;
            _average = _lastDx = 0;
        }

        private static void Publish(double value, Span<double> output)
        {
            output[0] = value;
            output[1] = 1;
        }

        private static ExactMeanAccumulator Difference(ExactMeanAccumulator.ProductFactor left, ExactMeanAccumulator.ProductFactor right)
        {
            var result = new ExactMeanAccumulator(); result.AddFactor(left);
            result.AddFactor(right, -1);
            return result;
        }

        private static ExactMeanAccumulator Absolute(ExactMeanAccumulator value)
        {
            if (value.Sign >= 0) return value;
            var result = new ExactMeanAccumulator(); result.Subtract(value); return result;
        }

        private static ExactMeanAccumulator Maximum(ExactMeanAccumulator left, ExactMeanAccumulator right)
        {
            var difference = left; difference.Subtract(right);
            return difference.Sign >= 0 ? left : right;
        }

        private static RocBankValue TrueRange(ExactMeanAccumulator.ProductFactor high,
            ExactMeanAccumulator.ProductFactor low, ExactMeanAccumulator.ProductFactor close,
            double highPrice, double lowPrice, double closePrice)
        {
            // An ordered range is one endpoint difference, rounded once.
            // Overflow and inverted candles retain the original exact formula.
            if (highPrice >= lowPrice)
            {
#if !NETFRAMEWORK
                double ordinary = Math.Max(highPrice, closePrice) - Math.Min(lowPrice, closePrice);
                if (double.IsFinite(ordinary)) return new RocBankValue(ordinary);
#endif
                return RocBankValue.Round(Difference(closePrice > highPrice ? close : high,
                    closePrice < lowPrice ? close : low));
            }
            return RocBankValue.Round(Maximum(Difference(high, low),
                Maximum(Absolute(Difference(high, close)), Absolute(Difference(low, close)))));
        }

#if !NETFRAMEWORK
        private static RocBankValue TrueRange(double highPrice, double lowPrice, double closePrice)
        {
            if (highPrice >= lowPrice)
            {
                double ordinary = Math.Max(highPrice, closePrice) - Math.Min(lowPrice, closePrice);
                if (double.IsFinite(ordinary)) return new RocBankValue(ordinary);
            }
            return TrueRange(new(highPrice), new(lowPrice), new(closePrice), highPrice, lowPrice, closePrice);
        }
#endif

        private static void AddWeighted(ref ExactMeanAccumulator sum, RocBankValue value,
            in ExactMeanAccumulator.ProductCoefficient coefficient, int weight)
        {
            if (value.UpperShift == 0)
                sum.AddProduct(new ExactMeanAccumulator.ProductFactor(value.Mantissa), coefficient);
            else value.AddTo(ref sum, weight);
        }

        private RocBankValue Smooth(RocBankValue previous, RocBankValue current)
        {
#if !NETFRAMEWORK
            if (previous.UpperShift == 0 && current.UpperShift == 0
                && SumSeededDirectionalIndex.WeightedCertificate.TrySum(
                    previous.Mantissa, current.Mantissa, period, period, out var rounded))
                return new RocBankValue(rounded);
#endif
            var sum = new ExactMeanAccumulator();
            AddWeighted(ref sum, previous, _priorCoefficient, period - 1);
            AddWeighted(ref sum, current, _currentCoefficient, period);
            return RocBankValue.Round(sum, count: period);
        }

        private static double Ratio(RocBankValue value, RocBankValue divisor, int weight = 1)
        {
#if !NETFRAMEWORK
            if (value.UpperShift == 0 && divisor.UpperShift == 0)
            {
                if (value.Mantissa is 0 || divisor.Mantissa is 0) return 0;
                if (weight == 1) return value.Mantissa / divisor.Mantissa;
                if (weight == 100 && AwesomeWithDetails.Arithmetic.TryNormalize(
                    value.Mantissa, divisor.Mantissa, out var rounded)) return rounded;
            }
#endif
            var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator, weight);
            var denominator = new ExactMeanAccumulator(); divisor.AddTo(ref denominator);
            return numerator.Ratio(denominator);
        }

        private static RocBankValue Direction(RocBankValue movement, RocBankValue range)
        {
            if (range.Mantissa is 0) return default;
#if !NETFRAMEWORK
            if (movement.UpperShift == 0 && range.UpperShift == 0
                && AwesomeWithDetails.Arithmetic.TryNormalize(movement.Mantissa, range.Mantissa, out var rounded))
                return new RocBankValue(rounded);
#endif
            var numerator = new ExactMeanAccumulator(); movement.AddTo(ref numerator, 100);
            numerator.ScaleByPowerOfTwo(-range.UpperShift);
            // DI is an unpublished rounded stage for DX/ADX. Keep its upper
            // exponent extended so a later finite ratio can cancel large values.
            return RocBankValue.Round(numerator, range.Mantissa);
        }

#if !NETFRAMEWORK
        private static bool TryDx(RocBankValue positive, RocBankValue negative,
            out bool hasDirection, out double dx)
        {
            hasDirection = false;
            dx = 0;
            if (positive.UpperShift != 0 || negative.UpperShift != 0
                || !Binary64ArithmeticCertificate.TryDifference(positive.Mantissa, -negative.Mantissa, out var total)
                || !Binary64ArithmeticCertificate.TryDifference(positive.Mantissa, negative.Mantissa, out var difference))
                return false;
            if (total is 0) return true;
            hasDirection = true;
            // Both DI stages and their sum/difference are exact binary64 inputs.
            // Certify the complete 100*absolute(difference)/total rounding;
            // the intermediate multiplication itself need not be exact.
            return AwesomeWithDetails.Arithmetic.TryNormalize(Math.Abs(difference), total, out dx);
        }
#endif

#if !NETFRAMEWORK
        private bool TryOrdinaryMovements(double high, double low,
            out RocBankValue positive, out RocBankValue negative)
        {
            double up = high - _previousHighPrice, down = _previousLowPrice - low;
            positive = negative = default;
            // Correct rounding is monotone: unequal finite results retain exact
            // dominance. A positive rounded tie can hide unequal exact changes.
            if (!double.IsFinite(up) || !double.IsFinite(down)
                || up > 0 && up == down) // NOSONAR: S1244 - A rounded tie requires exact dominance fallback.
                return false;
            // Finite binary64 differences are multiples of the least subnormal,
            // so a nonzero exact difference cannot round to zero.
            if (_needsPositive && up > 0 && up > down) positive = new RocBankValue(up);
            if (_needsNegative && down > 0 && down > up) negative = new RocBankValue(down);
            return true;
        }
#endif

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
#if NETFRAMEWORK
            var high = new ExactMeanAccumulator.ProductFactor(bar.High);
            var low = new ExactMeanAccumulator.ProductFactor(bar.Low);
            var close = _needsRange ? new ExactMeanAccumulator.ProductFactor(bar.Close) : default;
#else
            // Preserve the original constructor's finite-input rejection, order,
            // and exception before any state mutation. Decode only on fallback.
            if (!double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                || _needsRange && !double.IsFinite(bar.Close))
            {
                _ = new ExactMeanAccumulator.ProductFactor(bar.High);
                _ = new ExactMeanAccumulator.ProductFactor(bar.Low);
                if (_needsRange) _ = new ExactMeanAccumulator.ProductFactor(bar.Close);
            }
#endif
            if (++_index == 0)
            {
#if NETFRAMEWORK
                _previousHigh = high; _previousLow = low; _previousClose = close;
#endif
                _previousClosePrice = bar.Close;
#if !NETFRAMEWORK
                _previousHighPrice = bar.High; _previousLowPrice = bar.Low;
#endif
                return;
            }
            RocBankValue positive, negative;
#if !NETFRAMEWORK
            if (!TryOrdinaryMovements(bar.High, bar.Low, out positive, out negative))
#endif
            {
#if !NETFRAMEWORK
                var up = Difference(new(bar.High), new(_previousHighPrice));
                var down = Difference(new(_previousLowPrice), new(bar.Low));
#else
                var up = Difference(high, _previousHigh);
                var down = Difference(_previousLow, low);
#endif
                var dominance = up; dominance.Subtract(down);
                positive = _needsPositive && up.Sign > 0 && dominance.Sign > 0 ? RocBankValue.Round(up) : default;
                negative = _needsNegative && down.Sign > 0 && dominance.Sign < 0 ? RocBankValue.Round(down) : default;
            }
#if !NETFRAMEWORK
            var range = _needsRange ? TrueRange(bar.High, bar.Low, _previousClosePrice) : default;
#else
            var range = _needsRange ? TrueRange(high, low, _previousClose,
                bar.High, bar.Low, _previousClosePrice) : default;
            _previousHigh = high; _previousLow = low; _previousClose = close;
#endif
            _previousClosePrice = bar.Close;
#if !NETFRAMEWORK
            _previousHighPrice = bar.High; _previousLowPrice = bar.Low;
#endif
            if (period == 1)
            {
                var movement = measure is PriorDirectionalMeasure.PositiveMovement or PriorDirectionalMeasure.PositiveIndicator
                    ? positive : negative;
                Publish(measure <= PriorDirectionalMeasure.NegativeMovement ? movement.Publish() : Ratio(movement, range), output);
                return;
            }
            if (_index < period)
            {
                if (_needsPositive) positive.AddTo(ref _positiveSeed);
                if (_needsNegative) negative.AddTo(ref _negativeSeed);
                if (_needsRange) range.AddTo(ref _rangeSeed);
                if (_index == period - 1)
                {
                    if (_needsPositive) _positive = RocBankValue.Round(_positiveSeed);
                    if (_needsNegative) _negative = RocBankValue.Round(_negativeSeed);
                    if (_needsRange) _range = RocBankValue.Round(_rangeSeed);
                    _positiveSeed = _negativeSeed = _rangeSeed = default;
                }
            }
            else
            {
                if (_needsPositive) _positive = Smooth(_positive, positive);
                if (_needsNegative) _negative = Smooth(_negative, negative);
                if (_needsRange) _range = Smooth(_range, range);
            }
            if (measure <= PriorDirectionalMeasure.NegativeMovement)
            {
                if (_index >= (long)period - 1 + unstable)
                    Publish((measure == PriorDirectionalMeasure.PositiveMovement ? _positive : _negative).Publish(), output);
                return;
            }
            if (_index < period) return;
            var firstDx = (long)period + unstable;
            if (measure == PriorDirectionalMeasure.Index && _index > firstDx)
#if !NETFRAMEWORK
                _range = Smooth(_range, TrueRange(bar.High, bar.Low, bar.Close));
#else
                _range = Smooth(_range, TrueRange(high, low, close, bar.High, bar.Low, bar.Close));
#endif
            if (measure is PriorDirectionalMeasure.PositiveIndicator or PriorDirectionalMeasure.NegativeIndicator)
            {
                if (_index >= firstDx)
                    Publish(Ratio(measure == PriorDirectionalMeasure.PositiveIndicator ? _positive : _negative, _range, 100), output);
                return;
            }
            var pdi = Direction(_positive, _range);
            var mdi = Direction(_negative, _range);
            bool hasDirection;
            double dx;
#if !NETFRAMEWORK
            if (!TryDx(pdi, mdi, out hasDirection, out dx))
#endif
            {
                var total = new ExactMeanAccumulator(); pdi.AddTo(ref total); mdi.AddTo(ref total);
                var difference = new ExactMeanAccumulator(); pdi.AddTo(ref difference, 100); mdi.AddTo(ref difference, -100);
                hasDirection = !total.IsExactlyZero;
                dx = hasDirection ? Absolute(difference).Ratio(total) : 0;
            }
            if (measure == PriorDirectionalMeasure.Index)
            {
                if (_index < firstDx) return;
                if (_index == firstDx || hasDirection) _lastDx = dx;
                Publish(_lastDx, output);
                return;
            }
            var seedIndex = 2L * period - 1;
            if (_index <= seedIndex)
            {
                _seedDx.Add(dx);
                if (_index == seedIndex) _average = _seedDx.Mean(period);
            }
            else if (hasDirection)
            {
#if !NETFRAMEWORK
                if (SumSeededDirectionalIndex.WeightedCertificate.TrySum(_average, dx, period, 1, out var rounded))
                    _average = rounded;
                else
#endif
                {
                    var average = new ExactMeanAccumulator();
                    average.AddProduct(new ExactMeanAccumulator.ProductFactor(_average), _priorCoefficient);
                    average.Add(dx);
                    _average = average.Mean(period);
                }
            }
            var available = _index >= seedIndex + unstable;
            if (measure == PriorDirectionalMeasure.Average)
            {
                if (available) Publish(_average, output);
                return;
            }
            var prior = _history!.Count == period - 1 ? _history.Dequeue() : null;
            _history.Enqueue(available ? _average : null);
            if (available && prior.HasValue)
            {
                var average = new ExactMeanAccumulator(); average.Add(_average); average.Add(prior.Value);
                Publish(average.Mean(2), output);
            }
        }
    }
}
