using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Difference of two EMAs of cumulative money flow, with optional flow details.</summary>
/// <remarks>Mean mode skips zero ranges and seeds each EMA from its complete
/// mean. First-value mode skips all nonpositive ranges and seeds both EMAs from
/// the first cumulative value. Period order is preserved. Oscillator startup is
/// max(periods)-1, plus optional suppression in first-value mode. The multiplier
/// and complete volume flow round independently once; rounded flows accumulate
/// exactly, then the cumulative value and each EMA stage round once with extended
/// upper exponents. Only selected published overflow is rejected, so hidden flow
/// or cumulative overflow need not invalidate a finite oscillator. State uses
/// constant space in the periods.</remarks>
public sealed class SmoothedAccumulationOscillator
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates positive-period cumulative-flow EMAs with explicit startup and detail selection.</summary>
    public SmoothedAccumulationOscillator(
        int fastPeriod = 3,
        int slowPeriod = 10,
        bool firstValue = false,
        int suppression = 0,
        bool includeDetails = false
    )
        : base(8)
    {
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (suppression < 0 || (!firstValue && suppression != 0))
            throw new ArgumentOutOfRangeException(nameof(suppression));
        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
        FirstValue = firstValue;
        Suppression = suppression;
        IncludeDetails = includeDetails;
    }

    /// <summary>First EMA period.</summary>
    public int FastPeriod { get; }

    /// <summary>Second EMA period.</summary>
    public int SlowPeriod { get; }

    /// <summary>First-value seeds and nonpositive-range skipping instead of mean seeds and zero-range skipping.</summary>
    public bool FirstValue { get; }

    /// <summary>Additional first-value startup suppression.</summary>
    public int Suppression { get; }

    /// <summary>Whether multiplier, flow and cumulative line are also published.</summary>
    public bool IncludeDetails { get; }

    /// <summary>First EMA minus second EMA or zero before startup.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];

    /// <summary>Money-flow multiplier or zero when disabled.</summary>
    public IIndicatorOutput Multiplier => Outputs[1];

    /// <summary>Volume flow or zero when disabled.</summary>
    public IIndicatorOutput Flow => Outputs[2];

    /// <summary>Cumulative line or zero when disabled.</summary>
    public IIndicatorOutput Line => Outputs[3];

    /// <summary>Oscillator presence.</summary>
    public IIndicatorOutput OscillatorIsDefined => Outputs[4];

    /// <summary>Multiplier presence.</summary>
    public IIndicatorOutput MultiplierIsDefined => Outputs[5];

    /// <summary>Flow presence.</summary>
    public IIndicatorOutput FlowIsDefined => Outputs[6];

    /// <summary>Cumulative line presence.</summary>
    public IIndicatorOutput LineIsDefined => Outputs[7];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
#if !NETFRAMEWORK
        new CompactState(this);
#else
        new State(this);
#endif

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SmoothedAccumulationReference
                            .Values(bars, this)[slot % 4]
                            .Select(v =>
                                slot < 4 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

#if !NETFRAMEWORK
    private sealed class CompactMean(int period, bool first)
    {
        private readonly JurikDyadic _divisor = JurikDyadic.FromDouble((long)period + 1);
        private int _count;
        private JurikDyadic _sum;
        internal JurikDyadic Value { get; private set; }

        internal void Reset() { _count = 0; _sum = Value = default; }

        internal void Add(JurikDyadic value)
        {
            int seedLength = first ? 1 : period;
            if (_count < seedLength)
            {
                _sum.AddExact(value);
                if (++_count == seedLength)
                {
                    Value = SeededDyadicEma.RoundedRatio(_sum, JurikDyadic.FromDouble(seedLength));
                    _sum = default;
                }
            }
            else
            {
                var next = Value;
                next.Multiply(period - 1);
                value.Multiply(2);
                next.AddExact(value);
                Value = SeededDyadicEma.RoundedRatio(next, _divisor);
            }
        }
    }

    private sealed class CompactState(SmoothedAccumulationOscillator owner) : IMultiOutputState
    {
        private readonly CompactMean _fast = new(owner.FastPeriod, owner.FirstValue),
            _slow = new(owner.SlowPeriod, owner.FirstValue);
        private JurikDyadic _total;
        private long _index = -1;

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
            _total = default;
            _index = -1;
        }

        private static void Publish(JurikDyadic value, int slot, Span<double> output)
        {
            // Each stage was already rounded. The original integer-unit state
            // canonicalizes both exact and underflowed zero before publication.
            output[slot] = value.Sign == 0 ? 0 : value.Mean(1);
            output[slot + 4] = 1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var range = JurikDyadic.FromDouble(bar.High);
            range.Add(-bar.Low);
            JurikDyadic multiplier = default, flow = default;
            if (owner.FirstValue ? range.Sign > 0 : range.Sign != 0)
            {
                var numerator = JurikDyadic.FromDouble(bar.Close);
                numerator.Multiply(2);
                numerator.Add(-bar.High);
                numerator.Add(-bar.Low);
                if (owner.IncludeDetails)
                    multiplier = SeededDyadicEma.RoundedRatio(numerator, range);
                // Round the complete flow independently of the multiplier.
                numerator.Multiply(bar.Volume);
                flow = SeededDyadicEma.RoundedRatio(numerator, range);
            }
            _total.AddExact(flow);
            var line = _total.Rounded();
            _fast.Add(line);
            _slow.Add(line);
            if (owner.IncludeDetails)
            {
                Publish(multiplier, 1, output);
                Publish(flow, 2, output);
                Publish(line, 3, output);
            }
            if (_index >= (long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression)
            {
                var oscillator = _fast.Value;
                oscillator.Subtract(_slow.Value);
                Publish(oscillator.Rounded(), 0, output);
            }
        }
    }
#else
    private sealed class Mean(int period, bool first)
    {
        private long _count;
        private BigInteger _sum;
        internal BigInteger Value { get; private set; }

        internal void Reset()
        {
            _count = 0;
            _sum = Value = 0;
        }

        internal void Add(BigInteger value)
        {
            if (_count < (first ? 1 : period))
            {
                _sum += value;
                _count++;
                if (_count == (first ? 1 : period))
                {
                    Value = RocBankValue.RoundUnits(_sum, first ? 1 : period);
                    _sum = 0;
                }
            }
            else
                Value = RocBankValue.RoundUnits(Value * (period - 1) + 2 * value, (long)period + 1);
        }
    }

    private sealed class State(SmoothedAccumulationOscillator owner) : IMultiOutputState
    {
        private readonly Mean _fast = new(owner.FastPeriod, owner.FirstValue),
            _slow = new(owner.SlowPeriod, owner.FirstValue);
        private BigInteger _total;
        private long _index = -1;

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
            _total = 0;
            _index = -1;
        }

        private static void Publish(BigInteger value, int slot, Span<double> output)
        {
            output[slot] = ExactMeanAccumulator.UnitRatio(value, 1);
            output[slot + 4] = 1;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var high = ExactVarianceWindow.Units(bar.High);
            var low = ExactVarianceWindow.Units(bar.Low);
            var close = ExactVarianceWindow.Units(bar.Close);
            var range = high - low;
            BigInteger multiplier = 0,
                flow = 0;
            if (owner.FirstValue ? range.Sign > 0 : !range.IsZero)
            {
                var numerator = (2 * close - high - low) * range.Sign;
                var denominator = BigInteger.Abs(range);
                multiplier = RocBankValue.RoundUnits(numerator << 1074, denominator);
                flow = RocBankValue.RoundUnits(
                    numerator * ExactVarianceWindow.Units(bar.Volume),
                    denominator
                );
            }
            _total += flow;
            var line = RocBankValue.RoundUnits(_total, 1);
            _fast.Add(line);
            _slow.Add(line);
            if (owner.IncludeDetails)
            {
                Publish(multiplier, 1, output);
                Publish(flow, 2, output);
                Publish(line, 3, output);
            }
            if (
                _index
                >= (long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression
            )
                Publish(RocBankValue.RoundUnits(_fast.Value - _slow.Value, 1), 0, output);
        }
    }
#endif
}
