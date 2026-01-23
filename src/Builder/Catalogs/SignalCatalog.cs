using OoplesFinance.StockIndicators.Builder.Signals;

namespace OoplesFinance.StockIndicators.Builder.Catalogs;

/// <summary>
/// Catalog of signals for the builder.
/// </summary>
public sealed class SignalCatalog
{
    private readonly List<SignalRule> _rules = new();
    private readonly List<SignalGroupRule> _groupRules = new();
    private readonly List<SignalRangeRule> _rangeRules = new();
    private int _nextId;

    /// <summary>
    /// Creates a signal rule builder for a series handle.
    /// </summary>
    public SignalRuleBuilder When(SeriesHandle handle)
    {
        return new SignalRuleBuilder(this, SignalSeries.FromHandle(handle));
    }

    /// <summary>
    /// Creates a signal rule builder for an indicator key.
    /// </summary>
    public SignalRuleBuilder When(IndicatorKey key)
    {
        return new SignalRuleBuilder(this, SignalSeries.FromKey(key));
    }

    /// <summary>
    /// Creates a signal group rule builder.
    /// </summary>
    public SignalGroupRuleBuilder Group(params SignalCondition[] conditions)
    {
        return new SignalGroupRuleBuilder(this, conditions ?? Array.Empty<SignalCondition>());
    }

    internal SignalHandle AddRule(SignalRule rule)
    {
        _rules.Add(rule);
        return rule.Handle;
    }

    internal SignalHandle AddGroupRule(SignalGroupRule rule)
    {
        _groupRules.Add(rule);
        return rule.Handle;
    }

    internal SignalHandle AddRangeRule(SignalRangeRule rule)
    {
        _rangeRules.Add(rule);
        return rule.Handle;
    }

    internal SignalHandle NextHandle()
    {
        _nextId++;
        return new SignalHandle(_nextId);
    }

    internal IReadOnlyList<SignalRule> Build()
    {
        // Return singleton empty array when no signals configured to avoid allocation
        return _rules.Count == 0 ? Array.Empty<SignalRule>() : new List<SignalRule>(_rules);
    }

    internal IReadOnlyList<SignalGroupRule> BuildGroups()
    {
        return _groupRules.Count == 0 ? Array.Empty<SignalGroupRule>() : new List<SignalGroupRule>(_groupRules);
    }

    internal IReadOnlyList<SignalRangeRule> BuildRangeRules()
    {
        return _rangeRules.Count == 0 ? Array.Empty<SignalRangeRule>() : new List<SignalRangeRule>(_rangeRules);
    }
}
