using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Wilder RSI or signed Chande balance, including the flat-window convention.</summary>
public enum WilderStrengthConvention
{
    RsiZeroFlat,
    RsiHundredFlat,
    ChandeZeroFlat,
}

/// <summary>SMA-seeded Wilder gain/loss strength with explicit startup presence.</summary>
/// <remarks>Each complete gain/loss mean rounds to 53 significant binary digits
/// with an extended exponent in both directions, preserving tiny unpublished means.
/// The final bounded percentage rounds once to binary64. Differences and seed sums
/// are exact. Unstable periods suppress additional initial outputs without changing
/// the recurrence. Storage is constant and maximum periods allocate no windows.</remarks>
public sealed class WilderStrengthOscillator
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period oscillator and optional nonnegative startup suppression.</summary>
    public WilderStrengthOscillator(
        int period = 14,
        WilderStrengthConvention convention = WilderStrengthConvention.RsiZeroFlat,
        int unstablePeriods = 0
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(convention.GetType(), convention))
            throw new ArgumentOutOfRangeException(nameof(convention));
        if (unstablePeriods < 0)
            throw new ArgumentOutOfRangeException(nameof(unstablePeriods));
        Period = period;
        Convention = convention;
        UnstablePeriods = unstablePeriods;
    }

    /// <summary>Number of changes in the SMA seed and subsequent Wilder period.</summary>
    public int Period { get; }

    /// <summary>Percentage formula and flat-window convention.</summary>
    public WilderStrengthConvention Convention { get; }

    /// <summary>Additional initial values withheld after the seed.</summary>
    public int UnstablePeriods { get; }

    /// <summary>Strength percentage, or zero before presence.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One after all seed and suppressed values; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Convention, UnstablePeriods);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        StrengthReference.Calculate(bars, Period, Convention, UnstablePeriods)[
                            slot
                        ],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, WilderStrengthConvention convention, int unstable)
        : IMultiOutputState
    {
        private bool _started;
        private double _previous;
        private int _count,
            _skipped;
        private BigInteger _gainSeed,
            _lossSeed;
        private PositiveStrengthValue _gain,
            _loss;

        public void Reset()
        {
            _started = false;
            _previous = 0;
            _count = 0;
            _skipped = 0;
            _gainSeed = 0;
            _lossSeed = 0;
            _gain = default;
            _loss = default;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started)
            {
                _started = true;
                _previous = bar.Close;
                return;
            }
            var difference =
                ExactVarianceWindow.Units(bar.Close) - ExactVarianceWindow.Units(_previous);
            _previous = bar.Close;
            var gain = BigInteger.Max(difference, 0);
            var loss = BigInteger.Max(-difference, 0);
            if (_count < period)
            {
                _gainSeed += gain;
                _lossSeed += loss;
                if (++_count < period)
                    return;
                _gain = PositiveStrengthValue.Seed(_gainSeed, period);
                _loss = PositiveStrengthValue.Seed(_lossSeed, period);
                _gainSeed = 0;
                _lossSeed = 0;
            }
            else
            {
                _gain = _gain.Update(gain, period);
                _loss = _loss.Update(loss, period);
            }
            if (_skipped < unstable)
            {
                _skipped++;
                return;
            }
            output[0] =
                _gain.IsZero
                && _loss.IsZero
                && convention == WilderStrengthConvention.RsiHundredFlat
                    ? 100
                    : PositiveStrengthValue.Percent(
                        _gain,
                        _loss,
                        convention == WilderStrengthConvention.ChandeZeroFlat
                    );
            output[1] = 1;
        }
    }
}
