namespace OoplesFinance.StockIndicators.Attributes;

/// <summary>
/// Marks an indicator as validated against authoritative reference sources.
/// Applied after property tests, reference tests, and edge case tests all pass.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ValidatedAttribute : Attribute
{
    /// <summary>
    /// The reference source used for validation (e.g., "TA-Lib", "Wilder 1978", "TradingView").
    /// </summary>
    public string ReferenceSource { get; }

    /// <summary>
    /// The date validation was completed.
    /// </summary>
    public string ValidationDate { get; }

    /// <summary>
    /// Additional notes about the validation.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    public ValidatedAttribute(string referenceSource, string validationDate)
    {
        ReferenceSource = referenceSource;
        ValidationDate = validationDate;
    }
}

/// <summary>
/// Marks an indicator that needs review or has potential issues.
/// Used when property tests pass but reference verification is pending.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NeedsReviewAttribute : Attribute
{
    /// <summary>
    /// The reason this indicator needs review.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Priority level for review (High, Medium, Low).
    /// </summary>
    public string Priority { get; set; } = "Medium";

    public NeedsReviewAttribute(string reason)
    {
        Reason = reason;
    }
}

/// <summary>
/// Marks an indicator that has multiple valid formula variants.
/// Documents which variant is implemented and what alternatives exist.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HasVariantsAttribute : Attribute
{
    /// <summary>
    /// The variant currently implemented (e.g., "Wilder", "Cutler", "Standard").
    /// </summary>
    public string ImplementedVariant { get; }

    /// <summary>
    /// Other known variants that exist (comma-separated).
    /// </summary>
    public string OtherVariants { get; set; } = string.Empty;

    /// <summary>
    /// Reference for the different variants.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    public HasVariantsAttribute(string implementedVariant)
    {
        ImplementedVariant = implementedVariant;
    }
}

/// <summary>
/// Marks an indicator with known bounds for validation.
/// Used by property-based tests to verify output ranges.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IndicatorBoundsAttribute : Attribute
{
    /// <summary>
    /// The minimum valid value (inclusive). Use double.NegativeInfinity for unbounded.
    /// </summary>
    public double MinValue { get; }

    /// <summary>
    /// The maximum valid value (inclusive). Use double.PositiveInfinity for unbounded.
    /// </summary>
    public double MaxValue { get; }

    /// <summary>
    /// Whether the indicator can produce negative values.
    /// </summary>
    public bool CanBeNegative { get; set; } = true;

    public IndicatorBoundsAttribute(double minValue, double maxValue)
    {
        MinValue = minValue;
        MaxValue = maxValue;
    }
}

/// <summary>
/// Specifies the category of indicator for organizational purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IndicatorCategoryAttribute : Attribute
{
    /// <summary>
    /// The indicator category (e.g., "Oscillator", "Trend", "Volatility", "Volume").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Sub-category for more specific classification.
    /// </summary>
    public string SubCategory { get; set; } = string.Empty;

    public IndicatorCategoryAttribute(string category)
    {
        Category = category;
    }
}
