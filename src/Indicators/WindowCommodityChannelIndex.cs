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
        new State(Period, RollingDeviations, FlatZero);

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
}
