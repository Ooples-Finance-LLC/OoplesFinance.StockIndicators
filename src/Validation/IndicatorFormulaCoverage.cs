using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Declared per-output reference coverage, separate from whether validation passes.</summary>
public sealed class IndicatorFormulaCoverage
{
    internal IndicatorFormulaCoverage(IIndicator indicator, IEnumerable<IndicatorValidationRule> rules)
        : this(indicator.Outputs.Select(o => o.Slot).ToArray(), rules.Where(r => r.ReferenceOutputSlot.HasValue)
            .Select(r => r.ReferenceOutputSlot!.Value).ToArray(), rules.Where(r => r.ReferenceOutputSlot.HasValue && !r.UsesObservedHistory)
            .Select(r => r.ReferenceOutputSlot!.Value).ToArray(), rules.Where(r => r.ReferenceOutputSlot.HasValue && !r.UsesObservedHistory
                && (r.IncludesStartup || indicator.WarmupBars == 0)).Select(r => r.ReferenceOutputSlot!.Value).ToArray(),
            rules.Where(r => r.OverflowReference is not null).Select(r => r.ReferenceOutputSlot!.Value).ToArray()) { }

    internal IndicatorFormulaCoverage(int[] slots, int[] references, int[]? independentTrajectories = null,
        int[]? startupReferences = null, int[]? overflowReferences = null)
    {
        if (slots.Length == 0 || !slots.SequenceEqual(Enumerable.Range(0, slots.Length)))
            throw new InvalidOperationException("Outputs must declare contiguous slots starting at zero.");
        var covered = references.Distinct().OrderBy(s => s).ToArray();
        if (covered.Except(slots).Any())
            throw new InvalidOperationException("A formula reference names an undeclared output slot.");
        OutputOverflowReferenceSlots = Array.AsReadOnly((overflowReferences ?? Array.Empty<int>()).Distinct().OrderBy(s => s).ToArray());
        if (OutputOverflowReferenceSlots.Except(covered).Any())
            throw new InvalidOperationException("An overflow reference names an uncovered output slot.");
        ReferencedOutputSlots = Array.AsReadOnly(covered);
        IndependentTrajectoryOutputSlots = Array.AsReadOnly((independentTrajectories ?? covered).Distinct().OrderBy(s => s).ToArray());
        RecurrenceOnlyOutputSlots = Array.AsReadOnly(covered.Except(IndependentTrajectoryOutputSlots).ToArray());
        MissingOutputSlots = Array.AsReadOnly(slots.Except(covered).ToArray());
        MissingStartupOutputSlots = Array.AsReadOnly(slots.Except(startupReferences ?? covered).ToArray());
    }

    public IReadOnlyList<int> ReferencedOutputSlots { get; }
    public IReadOnlyList<int> OutputOverflowReferenceSlots { get; }
    public IReadOnlyList<int> MissingOutputSlots { get; }
    public IReadOnlyList<int> IndependentTrajectoryOutputSlots { get; }
    public IReadOnlyList<int> RecurrenceOnlyOutputSlots { get; }
    /// <summary>Outputs lacking an independent reference that checks initialization.</summary>
    public IReadOnlyList<int> MissingStartupOutputSlots { get; }
    public bool HasCompleteStartupReferences => IsComplete && MissingStartupOutputSlots.Count == 0;
    /// <summary>Complete reference registration can include recurrence-only evidence.</summary>
    public bool IsComplete => MissingOutputSlots.Count == 0;
    public bool HasCompleteIndependentTrajectories => HasCompleteStartupReferences && RecurrenceOnlyOutputSlots.Count == 0;

    /// <summary>Inspects contracts without executing fixtures. Construction errors propagate.</summary>
    public static IndicatorFormulaCoverage Inspect(IndicatorValidationCase testCase)
    {
        if (testCase is null) throw new ArgumentNullException(nameof(testCase));
        var indicator = testCase.Factory();
        if (indicator is null || indicator.GetType() != testCase.IndicatorType)
            throw new InvalidOperationException("The factory must return the declared indicator type.");
        return new IndicatorFormulaCoverage(indicator, Resolve(testCase, indicator));
    }

    internal static IndicatorValidationRule[] Resolve(IndicatorValidationCase testCase, IIndicator indicator)
    {
        var rules = testCase.Rules.Concat(indicator is IIndicatorValidationContract contract
                ? contract.ValidationRules : Array.Empty<IndicatorValidationRule>())
            .Concat(BuiltInFormulaReferences.For(indicator)).ToArray();
        if (rules.Any(r => r is null)) throw new InvalidOperationException("The validation contract contains a null rule.");
        if (rules.Where(rule => rule.ReferenceOutputSlot.HasValue).GroupBy(rule => rule.ReferenceOutputSlot)
            .Any(group => group.Any(rule => rule.OverflowReference is not null) && group.Count() != 1))
            throw new InvalidOperationException("An overflow-rejection output must have one unambiguous independent reference.");
        return rules;
    }
}
