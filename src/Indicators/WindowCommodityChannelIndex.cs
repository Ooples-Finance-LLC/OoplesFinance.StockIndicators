using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Typical-price deviation normalized by mean absolute deviation and 0.015.</summary>
/// <remarks>Typical price and each trailing mean round once. By default every
/// deviation uses the current window mean. Rolling-deviation mode instead averages
/// period consecutive distances from each price's own trailing mean, matching
/// Trady's distinct definition. Absolute distances remain exact until the final
/// 200*distance/(3*meanDeviation) ratio rounds once. Standard startup needs period
/// prices; rolling-deviation startup needs 2*period-1. Flat denominators are absent
/// unless flatZero is selected. Histories grow lazily, without summed-period overflow.</remarks>
public sealed class WindowCommodityChannelIndex
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates CCI with a positive period and explicit deviation/flat-window conventions.</summary>
    public WindowCommodityChannelIndex(
        int period = 20,
        bool rollingDeviations = false,
        bool flatZero = false
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        RollingDeviations = rollingDeviations;
        FlatZero = flatZero;
    }

    /// <summary>Typical-price and deviation window length.</summary>
    public int Period { get; }

    /// <summary>Whether each historical deviation uses its own trailing mean.</summary>
    public bool RollingDeviations { get; }

    /// <summary>Whether zero replaces an undefined flat-window result.</summary>
    public bool FlatZero { get; }

    /// <summary>CCI, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when CCI is defined.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
#if !NETFRAMEWORK
        new CompactState(Period, RollingDeviations, FlatZero);
#else
        new State(Period, RollingDeviations, FlatZero);
#endif

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        CommodityChannelReference
                            .Calculate(bars, Period, RollingDeviations, FlatZero)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

#if !NETFRAMEWORK
    internal sealed class CompactState(int period, bool rollingDeviations, bool flatZero, int count = 0) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new(Math.Min(period, count));
        private readonly Queue<JurikDyadic> _deviations = new(rollingDeviations ? Math.Min(period, Math.Max(0, count - period + 1)) : 0);
        private JurikDyadic _sum, _deviationSum;

        public void Reset()
        {
            _prices.Clear(); _deviations.Clear();
            _sum = _deviationSum = default;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var typical = new ExactMeanAccumulator();
            typical.Add(bar.High); typical.Add(bar.Low); typical.Add(bar.Close);
            double value = typical.Mean(3);
            if (_prices.Count == period) _sum.Subtract(JurikDyadic.FromDouble(_prices.Dequeue()));
            _prices.Enqueue(value);
            _sum.Add(value);
            if (_prices.Count < period) return;
            double meanValue = _sum.Mean(period);
            var mean = JurikDyadic.FromDouble(meanValue);
            var distance = JurikDyadic.FromDouble(value);
            distance.Subtract(mean);
            JurikDyadic deviation;
            if (rollingDeviations)
            {
                if (_deviations.Count == period) _deviationSum.Subtract(_deviations.Dequeue());
                var absolute = distance;
                if (absolute.Sign < 0) absolute.Multiply(-1);
                _deviations.Enqueue(absolute);
                _deviationSum.AddExact(absolute);
                if (_deviations.Count < period) return;
                deviation = _deviationSum;
            }
            else
            {
                var below = new JurikDyadic();
                int belowCount = 0;
                foreach (double price in _prices)
                {
                    if (price >= meanValue) continue;
                    below.Add(price);
                    belowCount++;
                }
                // Sum |price-mean| = sum(price) - 2*sum(price<mean)
                //                       + (2*count(price<mean)-P)*mean.
                // Keep the rounded mean from the contract, but evaluate this
                // identity exactly instead of forming every dyadic distance.
                below.Multiply(2);
                deviation = _sum;
                deviation.Subtract(below);
                var center = mean;
                center.Multiply(2L * belowCount - period);
                deviation.AddExact(center);
            }
            if (deviation.Sign == 0) { output[1] = flatZero ? 1 : 0; return; }
            // The integer coefficient is exactly representable as a double for
            // every supported Int32 period; all products remain exact dyadics.
            distance.Multiply(200L * period);
            deviation.Multiply(3);
            output[0] = distance.Ratio(deviation);
            output[1] = 1;
        }
    }
#else
    private sealed class State(int period, bool rollingDeviations, bool flatZero)
        : IMultiOutputState
    {
        private readonly Queue<BigInteger> _prices = new(),
            _deviations = new();
        private BigInteger _sum,
            _deviationSum;

        public void Reset()
        {
            _prices.Clear();
            _deviations.Clear();
            _sum = _deviationSum = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var typical = new ExactMeanAccumulator();
            typical.Add(bar.High);
            typical.Add(bar.Low);
            typical.Add(bar.Close);
            var value = ExactVarianceWindow.Units(typical.Mean(3));
            if (_prices.Count == period)
                _sum -= _prices.Dequeue();
            _prices.Enqueue(value);
            _sum += value;
            if (_prices.Count < period)
                return;
            var mean = ExactVarianceWindow.Units(ExactMeanAccumulator.UnitRatio(_sum, period));
            var distance = value - mean;
            BigInteger deviation;
            if (rollingDeviations)
            {
                if (_deviations.Count == period)
                    _deviationSum -= _deviations.Dequeue();
                var absolute = BigInteger.Abs(distance);
                _deviations.Enqueue(absolute);
                _deviationSum += absolute;
                if (_deviations.Count < period)
                    return;
                deviation = _deviationSum;
            }
            else
            {
                deviation = 0;
                foreach (var price in _prices)
                    deviation += BigInteger.Abs(price - mean);
            }
            if (deviation.IsZero)
            {
                output[1] = flatZero ? 1 : 0;
                return;
            }
            output[0] = ExactMeanAccumulator.UnitRatio(
                (200 * new BigInteger(period) * distance) << 1074,
                3 * deviation
            );
            output[1] = 1;
        }
    }
#endif
}
