using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Zero-seeded mother and following adaptive averages, published after the phase-filter startup.</summary>
/// <remarks>Hilbert stages begin at index twelve, with zero filter/average state;
/// publication starts at 32+suppression. Zero discriminator inputs retain the
/// previous period. Delta phase at most one selects the fast limit directly;
/// larger deltas select max(fast/delta,slow), so reversed limits remain meaningful.
/// Exact rounded logical stages and Math.Atan controls follow SeededPhaseAdaptiveAverage.
/// History is bounded independently of suppression.</remarks>
public sealed class DelayedPhaseAdaptiveAverage
    : MultiOutputIndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates independently bounded limits in [0.01,0.99] and nonnegative suppression.</summary>
    public DelayedPhaseAdaptiveAverage(
        double fastLimit = .5,
        double slowLimit = .05,
        int suppression = 0
    )
        : base(4)
    {
        if (!double.IsFinite(fastLimit) || fastLimit < .01 || fastLimit > .99)
            throw new ArgumentOutOfRangeException(nameof(fastLimit));
        if (!double.IsFinite(slowLimit) || slowLimit < .01 || slowLimit > .99)
            throw new ArgumentOutOfRangeException(nameof(slowLimit));
        if (suppression < 0)
            throw new ArgumentOutOfRangeException(nameof(suppression));
        FastLimit = fastLimit;
        SlowLimit = slowLimit;
        Suppression = suppression;
    }

    /// <summary>Fast phase gain.</summary>
    public double FastLimit { get; }

    /// <summary>Slow phase gain; may exceed the fast gain.</summary>
    public double SlowLimit { get; }

    /// <summary>Additional startup bars.</summary>
    public int Suppression { get; }

    /// <summary>Publication index capped to the metadata interface's Int32 maximum.</summary>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 32L + Suppression);

    /// <summary>Mother average, or zero before publication.</summary>
    public IIndicatorOutput Mama => Outputs[0];

    /// <summary>Following average, or zero before publication.</summary>
    public IIndicatorOutput Fama => Outputs[1];

    /// <summary>Mother-average presence.</summary>
    public IIndicatorOutput MamaIsDefined => Outputs[2];

    /// <summary>Following-average presence.</summary>
    public IIndicatorOutput FamaIsDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        SeededPhaseAdaptiveAverage.CreateDelayedState(FastLimit, SlowLimit, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SeededPhaseReference
                            .Delayed(bars, FastLimit, SlowLimit, Suppression)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );
}
