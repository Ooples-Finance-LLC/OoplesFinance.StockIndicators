using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window positive money flow as a percentage of total directional money flow.</summary>
/// <remarks>Typical price rounds once from (high+low+close)/3. Its exact ordering
/// selects positive or negative flow; equal prices contribute neither. Products
/// with volume and rolling sums stay exact until the final ratio rounds once.
/// By default zero negative flow returns 100. With minimumTotalOne, total flow
/// below one returns zero instead, matching TA-Lib. Output starts after period
/// price changes plus optional suppressed bars. Histories grow lazily.</remarks>
public sealed class WindowMoneyFlowIndex
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates MFI with period at least two and nonnegative additional startup suppression.</summary>
    public WindowMoneyFlowIndex(
        int period = 14,
        bool minimumTotalOne = false,
        int suppressedBars = 0
    )
        : base(2)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (suppressedBars < 0)
            throw new ArgumentOutOfRangeException(nameof(suppressedBars));
        Period = period;
        MinimumTotalOne = minimumTotalOne;
        SuppressedBars = suppressedBars;
    }

    /// <summary>Number of directional flows.</summary>
    public int Period { get; }

    /// <summary>Whether total flow below one produces zero instead of the zero-negative-flow convention.</summary>
    public bool MinimumTotalOne { get; }

    /// <summary>Additional complete-window results suppressed at startup.</summary>
    public int SuppressedBars { get; }

    /// <summary>Money Flow Index, or zero before startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One after startup.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, MinimumTotalOne, SuppressedBars);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        WindowMoneyFlowReference
                            .Calculate(bars, Period, MinimumTotalOne, SuppressedBars)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, bool minimumTotalOne, int suppressed) : IMultiOutputState
    {
        private readonly Queue<(BigInteger Positive, BigInteger Negative)> _flows = new();
        private BigInteger _positive,
            _negative,
            _previous;
        private bool _started;
        private long _changes;

        public void Reset()
        {
            _flows.Clear();
            _positive = _negative = _previous = 0;
            _started = false;
            _changes = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var mean = new ExactMeanAccumulator();
            mean.Add(bar.High);
            mean.Add(bar.Low);
            mean.Add(bar.Close);
            var typical = ExactVarianceWindow.Units(mean.Mean(3));
            if (!_started)
            {
                _previous = typical;
                _started = true;
                return;
            }
            var flow = typical * ExactVarianceWindow.Units(bar.Volume);
            var positive = typical > _previous ? flow : BigInteger.Zero;
            var negative = typical < _previous ? flow : BigInteger.Zero;
            _previous = typical;
            if (_flows.Count == period)
            {
                var old = _flows.Dequeue();
                _positive -= old.Positive;
                _negative -= old.Negative;
            }
            _flows.Enqueue((positive, negative));
            _positive += positive;
            _negative += negative;
            if (_changes < (long)period + suppressed)
                _changes++;
            if (_changes < (long)period + suppressed)
                return;
            var total = _positive + _negative;
            output[1] = 1;
            if (minimumTotalOne && total < (BigInteger.One << 2148))
                return;
            if (!minimumTotalOne && _negative.IsZero)
            {
                output[0] = 100;
                return;
            }
            output[0] = total.IsZero
                ? double.NegativeInfinity
                : ExactMeanAccumulator.UnitRatio(
                    (100 * _positive * total.Sign) << 1074,
                    BigInteger.Abs(total)
                );
        }
    }
}
