using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>MACD whose mean seeds end together at the slow period, with optional fixed coefficients.</summary>
/// <remarks>Periods are sorted. Mean mode seeds the fast average from the final
/// fastPeriod prices of the initial slowPeriod window. First-price mode runs both
/// averages from the first close. Suppression advances both averages before the
/// oscillator starts, then advances the signal before any output is published.
/// Fixed mode requires periods 12/26 and uses binary64 constants 0.15/0.075.
/// Each complete arithmetic stage rounds once with extended upper exponents.
/// Only selected published overflow is rejected. Signal period one is defined
/// mathematically, even though the native TA-Lib route fails for that request.
/// State storage is constant in all periods.</remarks>
public sealed class AlignedEmaMacd : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates aligned MACD with input periods at least two and a positive signal period.</summary>
    public AlignedEmaMacd(
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        bool firstPrice = false,
        int suppression = 0,
        bool fixedCoefficients = false,
        EmaDifferenceSelection selection =
            EmaDifferenceSelection.Oscillator
            | EmaDifferenceSelection.Signal
            | EmaDifferenceSelection.Histogram
    )
        : base(6)
    {
        if (fastPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        if (suppression < 0)
            throw new ArgumentOutOfRangeException(nameof(suppression));
        FastPeriod = Math.Min(fastPeriod, slowPeriod);
        SlowPeriod = Math.Max(fastPeriod, slowPeriod);
        if (fixedCoefficients && (FastPeriod != 12 || SlowPeriod != 26))
            throw new ArgumentException(
                "Fixed coefficients require periods 12 and 26.",
                nameof(fixedCoefficients)
            );
        if (selection <= 0 || ((int)selection & ~7) != 0)
            throw new ArgumentOutOfRangeException(nameof(selection));
        SignalPeriod = signalPeriod;
        FirstPrice = firstPrice;
        Suppression = suppression;
        FixedCoefficients = fixedCoefficients;
        Selection = selection;
    }

    /// <summary>Normalized fast period.</summary>
    public int FastPeriod { get; }

    /// <summary>Normalized slow period.</summary>
    public int SlowPeriod { get; }

    /// <summary>Signal period.</summary>
    public int SignalPeriod { get; }

    /// <summary>Whether each EMA seeds from its first input.</summary>
    public bool FirstPrice { get; }

    /// <summary>Additional advances before the oscillator and before signal publication.</summary>
    public int Suppression { get; }

    /// <summary>Whether to use the binary64 fixed 12/26 coefficients.</summary>
    public bool FixedCoefficients { get; }

    /// <summary>Selected oscillator, signal and histogram components.</summary>
    public EmaDifferenceSelection Selection { get; }

    /// <summary>Oscillator or zero when absent.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];

    /// <summary>Signal or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Histogram or zero when absent.</summary>
    public IIndicatorOutput Histogram => Outputs[2];

    /// <summary>Oscillator presence.</summary>
    public IIndicatorOutput OscillatorIsDefined => Outputs[3];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput SignalIsDefined => Outputs[4];

    /// <summary>Histogram presence.</summary>
    public IIndicatorOutput HistogramIsDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        AlignedMacdReference
                            .Values(bars, this)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class Average(int period, bool first, double? coefficient)
    {
        private long _count;
        private CompactMacdValue _sum;
        internal CompactMacdValue Value { get; private set; }
        private readonly (BigInteger Alpha, BigInteger Complement, BigInteger Denominator) _weights =
            FixedWeights(coefficient);

        private static (BigInteger, BigInteger, BigInteger) FixedWeights(double? coefficient)
        {
            if (!coefficient.HasValue) return default;
            // The private fixed coefficients are in (0, 1). Cancel their exact
            // binary scale once, preserving the complete recurrence fraction.
            var factor = new ExactMeanAccumulator.ProductFactor(coefficient.Value);
            var denominator = BigInteger.One << (1074 - factor.Scale);
            return (factor.Mantissa, denominator - factor.Mantissa, denominator);
        }

        public void Reset()
        {
            _count = 0;
            _sum = Value = default;
        }

        internal void Add(CompactMacdValue input)
        {
            if (_count < (first ? 1 : period))
            {
                _count++;
                _sum += input;
                if (_count == (first ? 1 : period))
                {
                    Value = _sum.Round(first ? 1 : period);
                    _sum = default;
                }
                return;
            }
            Value = coefficient.HasValue
                ? (Value.Times(_weights.Complement) + input.Times(_weights.Alpha)).Round(_weights.Denominator)
                : Value.AdvanceEma(input, period);
        }
    }

    internal sealed class State(AlignedEmaMacd owner) : IMultiOutputState
    {
        private long _index = -1;
        private readonly Average _fast =
                new(owner.FastPeriod, owner.FirstPrice, owner.FixedCoefficients ? .15 : null),
            _slow = new(owner.SlowPeriod, owner.FirstPrice, owner.FixedCoefficients ? .075 : null),
            _signal = new(owner.SignalPeriod, owner.FirstPrice, null);

        public void Reset()
        {
            _index = -1;
            _fast.Reset();
            _slow.Reset();
            _signal.Reset();
        }

        private void Publish(CompactMacdValue value, int slot, Span<double> output)
        {
            if (((int)owner.Selection & (1 << slot)) != 0)
            {
                output[slot] = value.Publish();
                output[slot + 3] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var input = CompactMacdValue.Of(bar.Close);
            _slow.Add(input);
            if (owner.FirstPrice || _index >= owner.SlowPeriod - owner.FastPeriod)
                _fast.Add(input);
            var start = (long)owner.SlowPeriod - 1 + owner.Suppression;
            if (_index < start)
                return;
            var difference = (_fast.Value - _slow.Value).Round(1);
            _signal.Add(difference);
            if (_index < start + owner.SignalPeriod - 1L + owner.Suppression)
                return;
            Publish(difference, 0, output);
            Publish(_signal.Value, 1, output);
            Publish((difference - _signal.Value).Round(1), 2, output);
        }
    }
}
