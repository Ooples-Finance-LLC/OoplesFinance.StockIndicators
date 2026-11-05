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

    internal sealed class CompactAverage(int period, bool first)
    {
        private readonly CompactMacdValue.EmaRecurrence _recurrence = new(period);
        private long _count;
        private CompactMacdValue _sum,
            _value;

        public void Reset()
        {
            _count = 0;
            _sum = _value = default;
        }

        public CompactMacdValue? Add(CompactMacdValue input)
        {
            if (_count < (first ? 1 : period))
            {
                _sum += input;
                _count++;
                if (_count < (first ? 1 : period))
                    return null;
                _value = _sum.Round(first ? 1 : period);
                _sum = default;
            }
            else
                _value = _recurrence.Advance(_value, input);
            return _value;
        }
    }

#if !NETFRAMEWORK
    // Separate from CompactAverage: Klinger, Schaff and signal recurrences keep
    // their existing arithmetic. Only finite raw-volume EMA inputs use this tier.
    internal sealed class VolumeAverage(int period, bool first)
    {
        private readonly CompactMacdValue.EmaRecurrence _recurrence = new(period);
        private long _count;
        private CompactMacdValue _sum, _value;
        private double _previous;

        public void Reset()
        {
            _count = 0;
            _sum = _value = default;
            _previous = 0;
        }

        public CompactMacdValue? Add(double input)
        {
            if (_count < (first ? 1 : period))
            {
                _sum += CompactMacdValue.Of(input);
                if (++_count < (first ? 1 : period)) return null;
                _value = _sum.Round(first ? 1 : period);
                _sum = default;
            }
            else if (TryBlend(_previous, input, period, out var rounded))
            {
                _previous = rounded;
                return _value = CompactMacdValue.Of(rounded);
            }
            else
                _value = _recurrence.Advance(_value, CompactMacdValue.Of(input));
            _previous = _value.Publish();
            return _value;
        }

        internal static bool TryBlend(double previous, double input, int period, out double rounded)
        {
            double a = period - 1, denominator = (long)period + 1;
            double p = previous * a, q = input * 2;
            rounded = 0;
            // Nonzero products in [2^-450,2^450] keep TwoSum, residuals,
            // adjacent rounding cells and all scaled bounds normal and finite.
            static bool Safe(double x, double y, double product) => product == 0
                ? x == 0 || y == 0
                : Math.Abs(product) >= 3.4395525670743494e-136
                    && Math.Abs(product) <= 2.9073548971824276e135;
            if (!Safe(previous, a, p) || !Safe(input, 2, q)) return false;
            double total = p + q, candidate = total / denominator;
            if (candidate == 0 || !double.IsFinite(candidate)) return false;
            double virtualTerm = total - p;
            double sumError = (p - (total - virtualTerm)) + (q - virtualTerm);
            double productError = Math.FusedMultiplyAdd(previous, a, -p);
            // Doubling is exact. Integer-weight product rounding leaves <=31
            // residual bits, so that FMA and TwoSum are exact under these guards.
            // 32 unit roundoffs of the absolute terms cover two tail additions,
            // the bound calculation and both endpoint additions, with margin.
            // The product guard also keeps this bound above underflow.
            // Relative error against total/divisor stays below four unit roundoffs
            // for the native quotient and one adjacent float. A <=85-bit product
            // cancels to <=35 residual bits, so each division FMA remains exact.
            // The guarded product grid keeps both adjacent cells and bounds normal.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                double residual = Math.FusedMultiplyAdd(-candidate, denominator, total);
                double error = (residual + sumError) + productError;
                double bound = (Math.Abs(residual) + Math.Abs(sumError) + Math.Abs(productError))
                    * 3.552713678800501e-15; // 2^-48 = 32 * binary64 unit roundoff.
                double low = error - bound, high = error + bound;
                // Power-of-two gaps times the integer divisor are exact here.
                double below = (candidate - Math.BitDecrement(candidate)) * .5 * denominator;
                double above = (Math.BitIncrement(candidate) - candidate) * .5 * denominator;
                // Both attempts require strict containment; ties remain uncertified.
                if (low > -below && high < above)
                {
                    rounded = candidate;
                    return true;
                }
                if (attempt == 0)
                    candidate = error > 0 ? Math.BitIncrement(candidate) : Math.BitDecrement(candidate);
            }
            return false;
        }
    }
#endif

    internal sealed class State(EmaDifferenceSignal owner) : IMultiOutputState
    {
        private readonly CompactAverage? _fast = OrdinaryAverage(owner.FastPeriod, owner),
            _slow = OrdinaryAverage(owner.SlowPeriod, owner);
        private readonly CompactAverage _signal = new(owner.SignalPeriod, owner.FirstPrice);

        private static CompactAverage? OrdinaryAverage(int period, EmaDifferenceSignal owner)
        {
#if !NETFRAMEWORK
            if (owner.Volume && owner.Percentage) return null;
#endif
            return new(period, owner.FirstPrice);
        }
#if !NETFRAMEWORK
        private readonly VolumeAverage? _volumeFast = owner.Volume && owner.Percentage
                ? new(owner.FastPeriod, owner.FirstPrice) : null,
            _volumeSlow = owner.Volume && owner.Percentage
                ? new(owner.SlowPeriod, owner.FirstPrice) : null;
#endif

        public void Reset()
        {
            _fast?.Reset();
            _slow?.Reset();
            _signal.Reset();
#if !NETFRAMEWORK
            _volumeFast?.Reset();
            _volumeSlow?.Reset();
#endif
        }

        private void Publish(CompactMacdValue? value, int slot, Span<double> output)
        {
            if (value.HasValue && ((int)owner.Selection & (1 << slot)) != 0)
            {
                output[slot] = value.Value.Publish();
                output[slot + 5] = 1;
            }
        }

#if !NETFRAMEWORK
        // Keep the PVO-only dispatch behind the existing volume-input branch.
        // Ordinary price MACD never needs to inspect these optional states.
        private void AddVolumeAverages(double volume, out CompactMacdValue? fast, out CompactMacdValue? slow)
        {
            if (_volumeFast is not null)
            {
                fast = _volumeFast.Add(volume);
                slow = _volumeSlow!.Add(volume);
            }
            else
            {
                var value = CompactMacdValue.Of(volume);
                fast = _fast!.Add(value);
                slow = _slow!.Add(value);
            }
        }
#endif

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            CompactMacdValue? fast, slow;
#if !NETFRAMEWORK
            if (owner.Volume)
                AddVolumeAverages(bar.Volume, out fast, out slow);
            else
#endif
            {
#if !NETFRAMEWORK
                var value = CompactMacdValue.Of(bar.Close);
#else
                var value = CompactMacdValue.Of(owner.Volume ? bar.Volume : bar.Close);
#endif
                fast = _fast!.Add(value);
                slow = _slow!.Add(value);
            }
            Publish(fast, 3, output);
            Publish(slow, 4, output);
            if (!fast.HasValue || !slow.HasValue)
                return;
            CompactMacdValue? oscillator = owner.Percentage
                ? (
                    slow.Value.IsZero
                        ? null
                        : (fast.Value - slow.Value).Percentage(slow.Value)
                )
                : (fast.Value - slow.Value).Round(1);
            Publish(oscillator, 0, output);
            var signal = _signal.Add(oscillator ?? default(CompactMacdValue));
            Publish(signal, 1, output);
            if (oscillator.HasValue && signal.HasValue)
                Publish((oscillator.Value - signal.Value).Round(1), 2, output);
        }
    }
}
