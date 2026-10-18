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
#if !NETFRAMEWORK
        new CompactState(Period, MinimumTotalOne, SuppressedBars);
#else
        new State(Period, MinimumTotalOne, SuppressedBars);
#endif

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

#if !NETFRAMEWORK
    internal sealed class CompactState(int period, bool minimumTotalOne, int suppressed, int count = 0) : IMultiOutputState
    {
        private readonly Queue<Flow> _flows = new(Math.Min(period, Math.Max(0, count - 1)));
        private readonly struct Flow
        {
            private readonly Int128 _product;
            private readonly int _exponent;
            internal readonly int Direction;

            internal Flow(double price, double volume, int direction)
            {
                Direction = direction;
                if (direction == 0) { _product = 0; _exponent = 0; return; }
                var a = ExactMeanAccumulator.DecomposeFinite(price);
                var b = ExactMeanAccumulator.DecomposeFinite(volume);
                // Each finite binary64 factor has at most 53 significant bits.
                // Keep the exact product even when hardware multiplication would
                // overflow or underflow. Nonzero decomposed factors are odd.
                _product = (Int128)a.Integer * b.Integer;
                _exponent = a.Exponent + b.Exponent;
            }

            internal JurikDyadic Value => JurikDyadic.FromBinaryProduct(_product, _exponent);
        }
        private JurikDyadic _positive, _negative;
        private double _previous;
        private bool _started;
        private long _changes;

        public void Reset()
        {
            _flows.Clear(); _positive = _negative = default;
            _previous = 0; _started = false; _changes = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            double typical = ExactTypicalPrice.Calculate(bar.High, bar.Low, bar.Close);
            if (!_started) { _previous = typical; _started = true; return; }
            int direction = typical.CompareTo(_previous);
            _previous = typical;
            var flow = new Flow(typical, bar.Volume, direction);
            if (_flows.Count == period)
            {
                var old = _flows.Dequeue();
                if (old.Direction > 0) _positive.Subtract(old.Value);
                else if (old.Direction < 0) _negative.Subtract(old.Value);
            }
            _flows.Enqueue(flow);
            if (direction > 0) _positive.AddExact(flow.Value);
            else if (direction < 0) _negative.AddExact(flow.Value);
            if (_changes < (long)period + suppressed) _changes++;
            if (_changes < (long)period + suppressed) return;
            var total = _positive;
            total.AddExact(_negative);
            output[1] = 1;
            if (minimumTotalOne)
            {
                var threshold = total;
                threshold.Add(-1);
                if (threshold.Sign < 0) return;
            }
            else if (_negative.Sign == 0) { output[0] = 100; return; }
            if (total.Sign == 0) { output[0] = double.NegativeInfinity; return; }
            // The exact-integer contract canonicalizes an exactly zero numerator,
            // including when signed volume makes the denominator negative.
            if (_positive.Sign == 0) return;
            var numerator = _positive;
            numerator.Multiply(100);
            output[0] = numerator.Ratio(total);
        }
    }
#else
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
#endif
}
