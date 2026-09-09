using OoplesFinance.StockIndicators.Builder.Catalogs;

namespace OoplesFinance.StockIndicators.Builder.Signals;

/// <summary>
/// Represents a series reference for signals.
/// </summary>
public readonly struct SignalSeries
{
    private SignalSeries(IndicatorKey? key, SeriesHandle? handle)
    {
        Key = key;
        Handle = handle;
    }

    /// <summary>
    /// Gets the indicator key (if using key-based reference).
    /// </summary>
    public IndicatorKey? Key { get; }

    /// <summary>
    /// Gets the series handle (if using handle-based reference).
    /// </summary>
    public SeriesHandle? Handle { get; }

    /// <summary>
    /// Creates a signal series from an indicator key.
    /// </summary>
    public static SignalSeries FromKey(IndicatorKey key)
    {
        return new SignalSeries(key, null);
    }

    /// <summary>
    /// Creates a signal series from a series handle.
    /// </summary>
    public static SignalSeries FromHandle(SeriesHandle handle)
    {
        return new SignalSeries(null, handle);
    }

    /// <summary>
    /// Tries to resolve values from a snapshot.
    /// </summary>
    public bool TryResolve(IndicatorSnapshot snapshot, out ReadOnlyMemory<double> values)
    {
        if (Key.HasValue)
        {
            return snapshot.TryGetSeries(Key.Value, out values);
        }

        if (Handle.HasValue)
        {
            return snapshot.TryGetSeries(Handle.Value, out values);
        }

        values = ReadOnlyMemory<double>.Empty;
        return false;
    }
}

/// <summary>
/// Represents a signal condition.
/// </summary>
public readonly struct SignalCondition
{
    /// <summary>
    /// Creates a new signal condition with a single threshold.
    /// </summary>
    public SignalCondition(SeriesHandle series, SignalTrigger trigger, double threshold)
        : this(series, trigger, threshold, null)
    {
    }

    /// <summary>
    /// Creates a new signal condition with optional second threshold (for Between/Outside).
    /// </summary>
    public SignalCondition(SeriesHandle series, SignalTrigger trigger, double threshold, double? thresholdHigh)
    {
        Series = series;
        Trigger = trigger;
        Threshold = threshold;
        ThresholdHigh = thresholdHigh;
    }

    /// <summary>
    /// Gets the series handle.
    /// </summary>
    public SeriesHandle Series { get; }

    /// <summary>
    /// Gets the trigger type.
    /// </summary>
    public SignalTrigger Trigger { get; }

    /// <summary>
    /// Gets the threshold value (low threshold for Between/Outside).
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets the high threshold value (for Between/Outside triggers).
    /// </summary>
    public double? ThresholdHigh { get; }

    /// <summary>
    /// Gets whether this is a cross trigger.
    /// </summary>
    public bool IsCross => Trigger == SignalTrigger.CrossesAbove || Trigger == SignalTrigger.CrossesBelow;

    /// <summary>
    /// Gets whether this is a range trigger (Between/Outside).
    /// </summary>
    public bool IsRange => Trigger == SignalTrigger.Between || Trigger == SignalTrigger.Outside;

    /// <summary>
    /// Creates an Above condition.
    /// </summary>
    public static SignalCondition Above(SeriesHandle series, double threshold)
    {
        return new SignalCondition(series, SignalTrigger.Above, threshold);
    }

    /// <summary>
    /// Creates a Below condition.
    /// </summary>
    public static SignalCondition Below(SeriesHandle series, double threshold)
    {
        return new SignalCondition(series, SignalTrigger.Below, threshold);
    }

    /// <summary>
    /// Creates a CrossesAbove condition.
    /// </summary>
    public static SignalCondition CrossesAbove(SeriesHandle series, double threshold)
    {
        return new SignalCondition(series, SignalTrigger.CrossesAbove, threshold);
    }

    /// <summary>
    /// Creates a CrossesBelow condition.
    /// </summary>
    public static SignalCondition CrossesBelow(SeriesHandle series, double threshold)
    {
        return new SignalCondition(series, SignalTrigger.CrossesBelow, threshold);
    }

    /// <summary>
    /// Creates a Between condition (low &lt; value &lt; high).
    /// </summary>
    public static SignalCondition Between(SeriesHandle series, double low, double high)
    {
        return new SignalCondition(series, SignalTrigger.Between, low, high);
    }

    /// <summary>
    /// Creates an Outside condition (value &lt; low OR value &gt; high).
    /// </summary>
    public static SignalCondition Outside(SeriesHandle series, double low, double high)
    {
        return new SignalCondition(series, SignalTrigger.Outside, low, high);
    }

    /// <summary>
    /// Checks if the condition is currently active.
    /// </summary>
    public bool IsActive(double value)
    {
        return Trigger switch
        {
            SignalTrigger.Above => value >= Threshold,
            SignalTrigger.Below => value <= Threshold,
            SignalTrigger.Between => ThresholdHigh.HasValue && value > Threshold && value < ThresholdHigh.Value,
            SignalTrigger.Outside => ThresholdHigh.HasValue && (value < Threshold || value > ThresholdHigh.Value),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a cross has been triggered.
    /// </summary>
    public bool IsCrossTriggered(double value, double? previous)
    {
        if (!previous.HasValue || double.IsNaN(previous.Value))
        {
            return false;
        }

        return Trigger switch
        {
            SignalTrigger.CrossesAbove => previous.Value < Threshold && value >= Threshold,
            SignalTrigger.CrossesBelow => previous.Value > Threshold && value <= Threshold,
            _ => false
        };
    }
}

/// <summary>
/// Represents a signal rule.
/// </summary>
public sealed class SignalRule
{
    /// <summary>
    /// Creates a new signal rule.
    /// </summary>
    public SignalRule(SignalHandle handle, string name, SignalSeries series, SignalTrigger trigger, double threshold)
    {
        Handle = handle;
        Name = name;
        Series = series;
        Trigger = trigger;
        Threshold = threshold;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Handle { get; }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the series reference.
    /// </summary>
    public SignalSeries Series { get; }

    /// <summary>
    /// Gets the trigger type.
    /// </summary>
    public SignalTrigger Trigger { get; }

    /// <summary>
    /// Gets the threshold value.
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets whether this is a cross trigger.
    /// </summary>
    public bool IsCross => Trigger == SignalTrigger.CrossesAbove || Trigger == SignalTrigger.CrossesBelow;

    /// <summary>
    /// Checks if the rule is currently active.
    /// </summary>
    public bool IsActive(double value)
    {
        return Trigger switch
        {
            SignalTrigger.Above => value >= Threshold,
            SignalTrigger.Below => value <= Threshold,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a cross has been triggered.
    /// </summary>
    public bool IsCrossTriggered(double value, double? previous)
    {
        if (!previous.HasValue || double.IsNaN(previous.Value))
        {
            return false;
        }

        return Trigger switch
        {
            SignalTrigger.CrossesAbove => previous.Value < Threshold && value >= Threshold,
            SignalTrigger.CrossesBelow => previous.Value > Threshold && value <= Threshold,
            _ => false
        };
    }
}

/// <summary>
/// Represents a group signal rule.
/// </summary>
public sealed class SignalGroupRule
{
    /// <summary>
    /// Creates a new group signal rule.
    /// </summary>
    public SignalGroupRule(SignalHandle handle, string name, SignalCondition[] conditions, SignalGroupMode mode,
        int? requiredCount, double? requiredPercent, SignalWindow window)
    {
        Handle = handle;
        Name = name;
        Conditions = conditions;
        Mode = mode;
        RequiredCount = requiredCount;
        RequiredPercent = requiredPercent;
        Window = window;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Handle { get; }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the conditions.
    /// </summary>
    public SignalCondition[] Conditions { get; }

    /// <summary>
    /// Gets the aggregation mode.
    /// </summary>
    public SignalGroupMode Mode { get; }

    /// <summary>
    /// Gets the required count (for AtLeast mode).
    /// </summary>
    public int? RequiredCount { get; }

    /// <summary>
    /// Gets the required percent (for Percent mode).
    /// </summary>
    public double? RequiredPercent { get; }

    /// <summary>
    /// Gets the window.
    /// </summary>
    public SignalWindow Window { get; }

    /// <summary>
    /// Checks if the group is active given the active count.
    /// </summary>
    public bool IsGroupActive(int activeCount)
    {
        var total = Conditions.Length;
        if (total == 0)
        {
            return false;
        }

        return Mode switch
        {
            SignalGroupMode.All => activeCount == total,
            SignalGroupMode.Any => activeCount > 0,
            SignalGroupMode.AtLeast => activeCount >= Math.Max(1, RequiredCount ?? total),
            SignalGroupMode.Percent => activeCount >= Math.Ceiling(total * Math.Min(100, Math.Max(0, RequiredPercent ?? 0)) / 100d),
            _ => false
        };
    }
}

/// <summary>
/// Builder for signal rules.
/// </summary>
public readonly struct SignalRuleBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SignalSeries _series;

    internal SignalRuleBuilder(SignalCatalog catalog, SignalSeries series)
    {
        _catalog = catalog;
        _series = series;
    }

    /// <summary>
    /// Creates an Above trigger.
    /// </summary>
    public SignalEmissionBuilder Above(double threshold)
    {
        return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.Above, threshold);
    }

    /// <summary>
    /// Creates a Below trigger.
    /// </summary>
    public SignalEmissionBuilder Below(double threshold)
    {
        return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.Below, threshold);
    }

    /// <summary>
    /// Creates a CrossesAbove trigger.
    /// </summary>
    public SignalEmissionBuilder CrossesAbove(double threshold)
    {
        return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.CrossesAbove, threshold);
    }

    /// <summary>
    /// Creates a CrossesBelow trigger.
    /// </summary>
    public SignalEmissionBuilder CrossesBelow(double threshold)
    {
        return new SignalEmissionBuilder(_catalog, _series, SignalTrigger.CrossesBelow, threshold);
    }

    /// <summary>
    /// Creates a Between trigger (low &lt; value &lt; high).
    /// </summary>
    public SignalRangeEmissionBuilder Between(double low, double high)
    {
        return new SignalRangeEmissionBuilder(_catalog, _series, SignalTrigger.Between, low, high);
    }

    /// <summary>
    /// Creates an Outside trigger (value &lt; low OR value &gt; high).
    /// </summary>
    public SignalRangeEmissionBuilder Outside(double low, double high)
    {
        return new SignalRangeEmissionBuilder(_catalog, _series, SignalTrigger.Outside, low, high);
    }
}

/// <summary>
/// Builder for signal emission.
/// </summary>
public readonly struct SignalEmissionBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SignalSeries _series;
    private readonly SignalTrigger _trigger;
    private readonly double _threshold;

    internal SignalEmissionBuilder(SignalCatalog catalog, SignalSeries series, SignalTrigger trigger, double threshold)
    {
        _catalog = catalog;
        _series = series;
        _trigger = trigger;
        _threshold = threshold;
    }

    /// <summary>
    /// Emits the signal with an optional name.
    /// </summary>
    public SignalHandle Emit(string? name = null)
    {
        var handle = _catalog.NextHandle();
        _catalog.AddRule(new SignalRule(handle, name ?? handle.ToString(), _series, _trigger, _threshold));
        return handle;
    }
}

/// <summary>
/// Builder for range signal emission (Between/Outside).
/// </summary>
public readonly struct SignalRangeEmissionBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SignalSeries _series;
    private readonly SignalTrigger _trigger;
    private readonly double _low;
    private readonly double _high;

    internal SignalRangeEmissionBuilder(SignalCatalog catalog, SignalSeries series, SignalTrigger trigger, double low, double high)
    {
        _catalog = catalog;
        _series = series;
        _trigger = trigger;
        _low = low;
        _high = high;
    }

    /// <summary>
    /// Emits the signal with an optional name.
    /// </summary>
    public SignalHandle Emit(string? name = null)
    {
        var handle = _catalog.NextHandle();
        _catalog.AddRangeRule(new SignalRangeRule(handle, name ?? handle.ToString(), _series, _trigger, _low, _high));
        return handle;
    }
}

/// <summary>
/// Represents a range signal rule (Between/Outside).
/// </summary>
public sealed class SignalRangeRule
{
    /// <summary>
    /// Creates a new range signal rule.
    /// </summary>
    public SignalRangeRule(SignalHandle handle, string name, SignalSeries series, SignalTrigger trigger, double low, double high)
    {
        Handle = handle;
        Name = name;
        Series = series;
        Trigger = trigger;
        Low = low;
        High = high;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Handle { get; }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the series reference.
    /// </summary>
    public SignalSeries Series { get; }

    /// <summary>
    /// Gets the trigger type.
    /// </summary>
    public SignalTrigger Trigger { get; }

    /// <summary>
    /// Gets the low threshold.
    /// </summary>
    public double Low { get; }

    /// <summary>
    /// Gets the high threshold.
    /// </summary>
    public double High { get; }

    /// <summary>
    /// Checks if the rule is currently active.
    /// </summary>
    public bool IsActive(double value)
    {
        return Trigger switch
        {
            SignalTrigger.Between => value > Low && value < High,
            SignalTrigger.Outside => value < Low || value > High,
            _ => false
        };
    }
}

/// <summary>
/// Builder for signal group rules.
/// </summary>
public readonly struct SignalGroupRuleBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SignalCondition[] _conditions;

    internal SignalGroupRuleBuilder(SignalCatalog catalog, SignalCondition[] conditions)
    {
        _catalog = catalog;
        _conditions = conditions;
    }

    /// <summary>
    /// All conditions must be met.
    /// </summary>
    public SignalGroupAggregationBuilder All()
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.All, null, null);
    }

    /// <summary>
    /// Any condition must be met.
    /// </summary>
    public SignalGroupAggregationBuilder Any()
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.Any, null, null);
    }

    /// <summary>
    /// At least N conditions must be met.
    /// </summary>
    public SignalGroupAggregationBuilder AtLeast(int count)
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.AtLeast, count, null);
    }

    /// <summary>
    /// A percentage of conditions must be met.
    /// </summary>
    public SignalGroupAggregationBuilder Percent(double percent)
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, SignalGroupMode.Percent, null, percent);
    }
}

/// <summary>
/// Builder for signal group aggregation.
/// </summary>
public readonly struct SignalGroupAggregationBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SignalCondition[] _conditions;
    private readonly SignalGroupMode _mode;
    private readonly int? _requiredCount;
    private readonly double? _requiredPercent;
    private readonly SignalWindow _window;

    internal SignalGroupAggregationBuilder(SignalCatalog catalog, SignalCondition[] conditions, SignalGroupMode mode,
        int? requiredCount, double? requiredPercent, SignalWindow? window = null)
    {
        _catalog = catalog;
        _conditions = conditions;
        _mode = mode;
        _requiredCount = requiredCount;
        _requiredPercent = requiredPercent;
        _window = window ?? SignalWindow.FromBars(1);
    }

    /// <summary>
    /// Sets the window to a number of bars.
    /// </summary>
    public SignalGroupAggregationBuilder ForBars(int bars)
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent,
            SignalWindow.FromBars(bars));
    }

    /// <summary>
    /// Sets the window to a time duration.
    /// </summary>
    public SignalGroupAggregationBuilder ForTime(TimeSpan duration)
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent,
            SignalWindow.FromDuration(duration));
    }

    /// <summary>
    /// Sets the validity window to a number of bars after trigger.
    /// </summary>
    public SignalGroupAggregationBuilder WithinBars(int bars)
    {
        var newWindow = _window.WithValidityBars(bars);
        return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent, newWindow);
    }

    /// <summary>
    /// Sets the validity window to a time duration after trigger.
    /// </summary>
    public SignalGroupAggregationBuilder WithinTime(TimeSpan duration)
    {
        var newWindow = _window.WithValidityDuration(duration);
        return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent, newWindow);
    }

    /// <summary>
    /// Sets the window.
    /// </summary>
    public SignalGroupAggregationBuilder For(SignalWindow window)
    {
        return new SignalGroupAggregationBuilder(_catalog, _conditions, _mode, _requiredCount, _requiredPercent, window);
    }

    /// <summary>
    /// Emits the signal with an optional name.
    /// </summary>
    public SignalHandle Emit(string? name = null)
    {
        var handle = _catalog.NextHandle();
        _catalog.AddGroupRule(new SignalGroupRule(handle, name ?? handle.ToString(), _conditions, _mode, _requiredCount,
            _requiredPercent, _window));
        return handle;
    }
}

/// <summary>
/// State for tracking signal groups.
/// </summary>
public sealed class SignalGroupState
{
    /// <summary>
    /// Creates a new signal group state.
    /// </summary>
    public SignalGroupState(int conditionCount)
    {
        PreviousValues = new double?[conditionCount];
    }

    /// <summary>
    /// Gets the previous values for each condition.
    /// </summary>
    public double?[] PreviousValues { get; }

    /// <summary>
    /// Gets or sets the number of active bars.
    /// </summary>
    public int ActiveBars { get; set; }

    /// <summary>
    /// Gets or sets whether the group is currently active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents a compound signal rule (combining multiple conditions with And/Or).
/// </summary>
public sealed class CompoundSignalRule
{
    /// <summary>
    /// Creates a new compound signal rule.
    /// </summary>
    public CompoundSignalRule(SignalHandle handle, string name, List<SignalConditionSpec> conditions, CompoundOperator op)
    {
        Handle = handle;
        Name = name;
        Conditions = conditions;
        Operator = op;
    }

    /// <summary>
    /// Gets the signal handle.
    /// </summary>
    public SignalHandle Handle { get; }

    /// <summary>
    /// Gets the signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the conditions.
    /// </summary>
    public List<SignalConditionSpec> Conditions { get; }

    /// <summary>
    /// Gets the logical operator (And/Or).
    /// </summary>
    public CompoundOperator Operator { get; }
}

/// <summary>
/// Specifies a condition in a compound signal.
/// </summary>
public sealed class SignalConditionSpec
{
    /// <summary>
    /// Gets or sets the series handle.
    /// </summary>
    public SeriesHandle Series { get; set; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public SignalTrigger Trigger { get; set; }

    /// <summary>
    /// Gets or sets the threshold (or low threshold for range triggers).
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets the high threshold (for range triggers).
    /// </summary>
    public double? ThresholdHigh { get; set; }

    /// <summary>
    /// Gets or sets the comparison series (for series-to-series comparisons).
    /// </summary>
    public SeriesHandle? ComparisonSeries { get; set; }
}

/// <summary>
/// Logical operator for compound signals.
/// </summary>
public enum CompoundOperator
{
    /// <summary>
    /// All conditions must be true.
    /// </summary>
    And,

    /// <summary>
    /// Any condition must be true.
    /// </summary>
    Or
}

/// <summary>
/// Builder for compound signals with fluent .And() / .Or() syntax.
/// </summary>
public sealed class CompoundSignalBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly List<SignalConditionSpec> _conditions = new();
    private CompoundOperator _operator = CompoundOperator.And;

    internal CompoundSignalBuilder(SignalCatalog catalog, SeriesHandle series, SignalTrigger trigger, double threshold, double? thresholdHigh = null)
    {
        _catalog = catalog;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = trigger,
            Threshold = threshold,
            ThresholdHigh = thresholdHigh
        });
    }

    internal CompoundSignalBuilder(SignalCatalog catalog, SeriesHandle series, SignalTrigger trigger, SeriesHandle comparisonSeries)
    {
        _catalog = catalog;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = trigger,
            ComparisonSeries = comparisonSeries
        });
    }

    /// <summary>
    /// Adds an AND condition requiring this series to be above a threshold.
    /// </summary>
    public CompoundSignalBuilder AndAbove(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.Above,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition requiring this series to be below a threshold.
    /// </summary>
    public CompoundSignalBuilder AndBelow(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.Below,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition requiring this series to cross above a threshold.
    /// </summary>
    public CompoundSignalBuilder AndCrossesAbove(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesAbove,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition requiring this series to cross below a threshold.
    /// </summary>
    public CompoundSignalBuilder AndCrossesBelow(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesBelow,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition requiring this series to cross above another series.
    /// </summary>
    public CompoundSignalBuilder AndCrossesAbove(SeriesHandle series, SeriesHandle other)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesAbove,
            ComparisonSeries = other
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition requiring this series to cross below another series.
    /// </summary>
    public CompoundSignalBuilder AndCrossesBelow(SeriesHandle series, SeriesHandle other)
    {
        _operator = CompoundOperator.And;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesBelow,
            ComparisonSeries = other
        });
        return this;
    }

    /// <summary>
    /// Adds an AND condition for a fluent sub-builder on a series.
    /// </summary>
    public CompoundConditionBuilder And(SeriesHandle series)
    {
        _operator = CompoundOperator.And;
        return new CompoundConditionBuilder(this, series);
    }

    /// <summary>
    /// Adds an OR condition requiring this series to be above a threshold.
    /// </summary>
    public CompoundSignalBuilder OrAbove(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.Or;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.Above,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an OR condition requiring this series to be below a threshold.
    /// </summary>
    public CompoundSignalBuilder OrBelow(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.Or;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.Below,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an OR condition requiring this series to cross above a threshold.
    /// </summary>
    public CompoundSignalBuilder OrCrossesAbove(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.Or;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesAbove,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an OR condition requiring this series to cross below a threshold.
    /// </summary>
    public CompoundSignalBuilder OrCrossesBelow(SeriesHandle series, double threshold)
    {
        _operator = CompoundOperator.Or;
        _conditions.Add(new SignalConditionSpec
        {
            Series = series,
            Trigger = SignalTrigger.CrossesBelow,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds an OR condition for a fluent sub-builder on a series.
    /// </summary>
    public CompoundConditionBuilder Or(SeriesHandle series)
    {
        _operator = CompoundOperator.Or;
        return new CompoundConditionBuilder(this, series);
    }

    /// <summary>
    /// Names the compound signal and registers it.
    /// </summary>
    public SignalHandle Named(string name)
    {
        var handle = _catalog.NextHandle();
        _catalog.AddCompoundRule(new CompoundSignalRule(handle, name, _conditions, _operator));
        return handle;
    }

    /// <summary>
    /// Emits the compound signal with an optional name.
    /// </summary>
    public SignalHandle Emit(string? name = null)
    {
        var handle = _catalog.NextHandle();
        _catalog.AddCompoundRule(new CompoundSignalRule(handle, name ?? handle.ToString(), _conditions, _operator));
        return handle;
    }

    internal void AddCondition(SignalConditionSpec spec)
    {
        _conditions.Add(spec);
    }
}

/// <summary>
/// Builder for a single condition in a compound signal.
/// </summary>
public sealed class CompoundConditionBuilder
{
    private readonly CompoundSignalBuilder _parent;
    private readonly SeriesHandle _series;

    internal CompoundConditionBuilder(CompoundSignalBuilder parent, SeriesHandle series)
    {
        _parent = parent;
        _series = series;
    }

    /// <summary>
    /// Condition: series is above threshold.
    /// </summary>
    public CompoundSignalBuilder Above(double threshold)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.Above,
            Threshold = threshold
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series is below threshold.
    /// </summary>
    public CompoundSignalBuilder Below(double threshold)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.Below,
            Threshold = threshold
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series crosses above threshold.
    /// </summary>
    public CompoundSignalBuilder CrossesAbove(double threshold)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.CrossesAbove,
            Threshold = threshold
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series crosses below threshold.
    /// </summary>
    public CompoundSignalBuilder CrossesBelow(double threshold)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.CrossesBelow,
            Threshold = threshold
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series crosses above another series.
    /// </summary>
    public CompoundSignalBuilder CrossesAbove(SeriesHandle other)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.CrossesAbove,
            ComparisonSeries = other
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series crosses below another series.
    /// </summary>
    public CompoundSignalBuilder CrossesBelow(SeriesHandle other)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.CrossesBelow,
            ComparisonSeries = other
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series is between two values.
    /// </summary>
    public CompoundSignalBuilder Between(double low, double high)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.Between,
            Threshold = low,
            ThresholdHigh = high
        });
        return _parent;
    }

    /// <summary>
    /// Condition: series is outside a range.
    /// </summary>
    public CompoundSignalBuilder Outside(double low, double high)
    {
        _parent.AddCondition(new SignalConditionSpec
        {
            Series = _series,
            Trigger = SignalTrigger.Outside,
            Threshold = low,
            ThresholdHigh = high
        });
        return _parent;
    }
}

/// <summary>
/// Extended signal rule builder that supports compound signals.
/// </summary>
public readonly struct ExtendedSignalRuleBuilder
{
    private readonly SignalCatalog _catalog;
    private readonly SeriesHandle _series;

    internal ExtendedSignalRuleBuilder(SignalCatalog catalog, SeriesHandle series)
    {
        _catalog = catalog;
        _series = series;
    }

    /// <summary>
    /// Condition: series is above threshold. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder IsAbove(double threshold)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Above, threshold);
    }

    /// <summary>
    /// Condition: series is below threshold. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder IsBelow(double threshold)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Below, threshold);
    }

    /// <summary>
    /// Condition: series crosses above threshold. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder CrossesAbove(double threshold)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.CrossesAbove, threshold);
    }

    /// <summary>
    /// Condition: series crosses below threshold. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder CrossesBelow(double threshold)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.CrossesBelow, threshold);
    }

    /// <summary>
    /// Condition: series crosses above another series. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder CrossesAbove(SeriesHandle other)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.CrossesAbove, other);
    }

    /// <summary>
    /// Condition: series crosses below another series. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder CrossesBelow(SeriesHandle other)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.CrossesBelow, other);
    }

    /// <summary>
    /// Condition: series is between two values. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder IsBetween(double low, double high)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Between, low, high);
    }

    /// <summary>
    /// Condition: series is outside a range. Returns a compound builder for chaining.
    /// </summary>
    public CompoundSignalBuilder IsOutside(double low, double high)
    {
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Outside, low, high);
    }

    /// <summary>
    /// Condition: series is rising (current value &gt; previous value).
    /// </summary>
    public CompoundSignalBuilder IsRising()
    {
        // IsRising is implemented as crossing above 0 for the first derivative
        // For simplicity, we use a special trigger that the runtime will handle
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Rising, 0);
    }

    /// <summary>
    /// Condition: series is falling (current value &lt; previous value).
    /// </summary>
    public CompoundSignalBuilder IsFalling()
    {
        // IsFalling is implemented as crossing below 0 for the first derivative
        return new CompoundSignalBuilder(_catalog, _series, SignalTrigger.Falling, 0);
    }
}
