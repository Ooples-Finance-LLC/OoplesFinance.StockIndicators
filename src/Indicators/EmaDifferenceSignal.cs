using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Published components of an EMA difference and signal.</summary>
[Flags]
public enum EmaDifferenceSelection
{
    Oscillator = 1,
    Signal = 2,
    Histogram = 4,
    FastAverage = 8,
    SlowAverage = 16,
    All = 31,
}

/// <summary>Absolute or percentage EMA difference, EMA signal, histogram and both input averages.</summary>
/// <remarks>Each EMA seeds from a complete mean or the first input. The signal
/// starts from the first available oscillator. Percentage mode divides the exact
/// fast-minus-slow difference by slow and multiplies by 100. Zero slow gives an
/// absent oscillator and histogram but contributes zero to the signal. Each
/// arithmetic stage rounds once with extended upper exponents. Only selected
/// published overflow is rejected; a histogram can remain finite when hidden
/// oscillator/signal values are unrepresentable. Periods need not be ordered.
/// All state uses constant storage, including maximum periods.</remarks>
public sealed class EmaDifferenceSignal : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive-period EMA differences with explicit seed, source and selected outputs.</summary>
    public EmaDifferenceSignal(
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        bool firstPrice = false,
        bool percentage = false,
        bool volume = false,
        EmaDifferenceSelection selection = EmaDifferenceSelection.All
    )
        : base(10)
    {
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        if (selection <= 0 || (selection & ~EmaDifferenceSelection.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(selection));
        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
        SignalPeriod = signalPeriod;
        FirstPrice = firstPrice;
        Percentage = percentage;
        Volume = volume;
        Selection = selection;
    }

    /// <summary>First input EMA period.</summary>
    public int FastPeriod { get; }

    /// <summary>Second input EMA period.</summary>
    public int SlowPeriod { get; }

    /// <summary>Signal EMA period.</summary>
    public int SignalPeriod { get; }

    /// <summary>Whether every EMA seeds from its first input rather than a mean.</summary>
    public bool FirstPrice { get; }

    /// <summary>Whether the oscillator is a percentage of the second EMA.</summary>
    public bool Percentage { get; }

    /// <summary>Whether to use volume rather than close.</summary>
    public bool Volume { get; }

    /// <summary>Published components; unselected outputs stay absent.</summary>
    public EmaDifferenceSelection Selection { get; }

    /// <summary>Oscillator or zero when absent.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];

    /// <summary>Signal or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Histogram or zero when absent.</summary>
    public IIndicatorOutput Histogram => Outputs[2];

    /// <summary>First EMA or zero when absent.</summary>
    public IIndicatorOutput FastAverage => Outputs[3];

    /// <summary>Second EMA or zero when absent.</summary>
    public IIndicatorOutput SlowAverage => Outputs[4];

    /// <summary>Oscillator presence.</summary>
    public IIndicatorOutput OscillatorIsDefined => Outputs[5];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput SignalIsDefined => Outputs[6];

    /// <summary>Histogram presence.</summary>
    public IIndicatorOutput HistogramIsDefined => Outputs[7];

    /// <summary>First EMA presence.</summary>
    public IIndicatorOutput FastAverageIsDefined => Outputs[8];

    /// <summary>Second EMA presence.</summary>
    public IIndicatorOutput SlowAverageIsDefined => Outputs[9];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 10)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        EmaDifferenceReference
                            .Values(bars, this)[slot % 5]
                            .Select(v =>
                                slot < 5 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    internal sealed class Average(int period, bool first)
    {
        private long _count;
        private BigInteger _sum,
            _value;

        public void Reset()
        {
            _count = 0;
            _sum = _value = 0;
        }

        public BigInteger? Add(BigInteger input)
        {
            if (_count < (first ? 1 : period))
            {
                _sum += input;
                _count++;
                if (_count < (first ? 1 : period))
                    return null;
                _value = RocBankValue.RoundUnits(_sum, first ? 1 : period);
                _sum = 0;
            }
            else
                _value = RocBankValue.RoundUnits(
                    _value * (period - 1) + 2 * input,
                    (long)period + 1
                );
            return _value;
        }
    }

    private sealed class State(EmaDifferenceSignal owner) : IMultiOutputState
    {
        private readonly Average _fast = new(owner.FastPeriod, owner.FirstPrice),
            _slow = new(owner.SlowPeriod, owner.FirstPrice),
            _signal = new(owner.SignalPeriod, owner.FirstPrice);

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
            _signal.Reset();
        }

        private void Publish(BigInteger? value, int slot, Span<double> output)
        {
            if (value.HasValue && ((int)owner.Selection & (1 << slot)) != 0)
            {
                output[slot] = ExactMeanAccumulator.UnitRatio(value.Value, 1);
                output[slot + 5] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var value = ExactVarianceWindow.Units(owner.Volume ? bar.Volume : bar.Close);
            var fast = _fast.Add(value);
            var slow = _slow.Add(value);
            Publish(fast, 3, output);
            Publish(slow, 4, output);
            if (!fast.HasValue || !slow.HasValue)
                return;
            BigInteger? oscillator = owner.Percentage
                ? (
                    slow.Value.IsZero
                        ? null
                        : RocBankValue.RoundUnits(
                            (100 * (fast.Value - slow.Value) * slow.Value.Sign) << 1074,
                            BigInteger.Abs(slow.Value)
                        )
                )
                : RocBankValue.RoundUnits(fast.Value - slow.Value, 1);
            Publish(oscillator, 0, output);
            var signal = _signal.Add(oscillator ?? BigInteger.Zero);
            Publish(signal, 1, output);
            if (oscillator.HasValue && signal.HasValue)
                Publish(RocBankValue.RoundUnits(oscillator.Value - signal.Value, 1), 2, output);
        }
    }
}
