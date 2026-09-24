using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Optional mathematical contracts automatically used when validating an indicator.</summary>
public interface IIndicatorValidationContract
{
    IEnumerable<IndicatorValidationRule> ValidationRules { get; }
}

/// <summary>Read-only input and every output of a single validation run.</summary>
public sealed class IndicatorValidationContext
{
    internal IndicatorValidationContext(string fixture, IReadOnlyList<Bar> bars,
        IReadOnlyList<IReadOnlyList<double>> outputs, int warmupBars)
    {
        Fixture = fixture;
        Bars = bars;
        Outputs = outputs;
        WarmupBars = warmupBars;
    }
    public string Fixture { get; }
    public IReadOnlyList<Bar> Bars { get; }
    public IReadOnlyList<IReadOnlyList<double>> Outputs { get; }
    public int WarmupBars { get; }
}

/// <summary>A named mathematical assertion; throwing an exception records a validation failure.</summary>
public sealed class IndicatorValidationRule
{
    public IndicatorValidationRule(string name, Action<IndicatorValidationContext> check)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A rule needs a name.", nameof(name));
        Name = name;
        Check = check ?? throw new ArgumentNullException(nameof(check));
    }
    public string Name { get; }
    public Action<IndicatorValidationContext> Check { get; }
    /// <summary>The output compared to an independent reference; null for property-only rules.</summary>
    public int? ReferenceOutputSlot { get; private set; }
    /// <summary>True when the rule reads earlier observed outputs. Such a residual
    /// is evidence about the recurrence, not an independent full trajectory.</summary>
    public bool UsesObservedHistory { get; private set; }
    /// <summary>Whether this reference compares the declared initialization values from bar zero.</summary>
    public bool IncludesStartup { get; private set; }

    internal Func<IReadOnlyList<Bar>, IReadOnlyList<double>>? OverflowReference { get; private set; }

    internal static IndicatorValidationRule ConstantAverage(int slot) => new("ConstantAverage", context =>
    {
        if (!context.Fixture.StartsWith("settled-flat-", StringComparison.Ordinal)) return;
        var output = context.Outputs[slot];
        var expected = context.Bars[0].Close;
        for (var i = output.Count - 1000; i < output.Count; i++)
            if (i < 0 || double.IsNaN(output[i]) || double.IsInfinity(output[i]) || Math.Abs(output[i] - expected) > 1e-6)
                throw new InvalidOperationException($"Output {slot}, bar {i}: a settled average must preserve the constant {expected}.");
    });

    /// <summary>Checks a declared output range after the indicator's declared warmup.</summary>
    public static IndicatorValidationRule Bounds(int outputSlot, double minimum, double maximum)
    {
        if (outputSlot < 0) throw new ArgumentOutOfRangeException(nameof(outputSlot));
        if (double.IsNaN(minimum) || double.IsNaN(maximum) || minimum > maximum)
            throw new ArgumentException("Bounds must be ordered and not NaN.");
        return new IndicatorValidationRule("Bounds[" + outputSlot + "]", context =>
        {
            var output = context.Outputs[outputSlot];
            for (var i = context.WarmupBars; i < output.Count; i++)
                if (double.IsNaN(output[i]) || output[i] < minimum || output[i] > maximum)
                    throw new InvalidOperationException($"Output {outputSlot}, bar {i}: {output[i]:R} is outside [{minimum}, {maximum}].");
        });
    }

    // An independent equation residual is appropriate for feedback maps whose trajectories amplify rounding.
    // The evaluator may read earlier observed outputs, but must derive each current value independently.
    internal static IndicatorValidationRule ReferenceRecurrence(int outputSlot,
        Func<IndicatorValidationContext, IReadOnlyList<double>> expected) => new("ReferenceRecurrence[" + outputSlot + "]",
            context => Reference(outputSlot, _ => expected(context), new IndicatorErrorBudget(1e-9, 1e-9)).Check(context))
        { ReferenceOutputSlot = outputSlot, UsesObservedHistory = true, IncludesStartup = true };

    /// <summary>Compares finite results and requires runtime rejection when an independent reference
    /// proves that the correctly rounded mathematical result overflows binary64. The reference uses
    /// signed infinity only for that proof, never for an overflowing intermediate calculation.
    /// Validation checks the finite prefix and repeats the rejected execution on fresh instances.
    /// This must be the sole reference for its output. Property rules may still supplement it.
    /// This does not establish recovery of a live state after rejection.</summary>
    public static IndicatorValidationRule ReferenceWithOverflowRejection(int outputSlot,
        Func<IReadOnlyList<Bar>, IReadOnlyList<double>> reference, IndicatorErrorBudget errorBudget)
    {
        var rule = Reference(outputSlot, reference, errorBudget);
        rule.OverflowReference = reference;
        return rule;
    }

    /// <summary>Compares against an independently supplied formula from the first bar, including startup.</summary>
    public static IndicatorValidationRule Reference(int outputSlot,
        Func<IReadOnlyList<Bar>, IReadOnlyList<double>> reference, double absoluteTolerance = 1e-9,
        double relativeTolerance = 1e-9)
        => Reference(outputSlot, reference, new IndicatorErrorBudget(absoluteTolerance, relativeTolerance), true);

    /// <summary>Compares with a declared output-specific budget, including startup by default.</summary>
    public static IndicatorValidationRule Reference(int outputSlot,
        Func<IReadOnlyList<Bar>, IReadOnlyList<double>> reference, IndicatorErrorBudget errorBudget,
        bool includeWarmup = true)
    {
        if (outputSlot < 0) throw new ArgumentOutOfRangeException(nameof(outputSlot));
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        if (errorBudget is null) throw new ArgumentNullException(nameof(errorBudget));
        return new IndicatorValidationRule("Reference[" + outputSlot + "]", context =>
        {
            var expected = reference(context.Bars);
            var actual = context.Outputs[outputSlot];
            if (expected.Count != actual.Count) throw new InvalidOperationException("Reference output length differs.");
            for (var i = includeWarmup ? 0 : context.WarmupBars; i < actual.Count; i++)
            {
                // Unavailable startup is an exact status comparison, never a numerical tolerance.
                // The shared validator independently enforces the indicator's declared NaN policy.
                if (i < context.WarmupBars && double.IsNaN(expected[i]) && double.IsNaN(actual[i])) continue;
                if (!errorBudget.Accepts(expected[i], actual[i]))
                    throw new InvalidOperationException($"Output {outputSlot}, bar {i}: expected {expected[i]:R}, got {actual[i]:R}.");
            }
        }) { ReferenceOutputSlot = outputSlot, IncludesStartup = includeWarmup };
    }

}
