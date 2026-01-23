using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for indicator computation.
/// </summary>
public sealed class IndicatorOptions
{
    /// <summary>
    /// Gets or sets the compute policy. Defaults to <see cref="IndicatorComputePolicy.Lazy"/>.
    /// </summary>
    public IndicatorComputePolicy? ComputePolicy { get; set; }

    /// <summary>
    /// Gets or sets the indicator selection. Defaults to all indicators.
    /// </summary>
    public IndicatorSelection? Selection { get; set; }
}

/// <summary>
/// Defines which indicators to include.
/// </summary>
public sealed class IndicatorSelection
{
    private IndicatorSelection(IndicatorPreset preset, IReadOnlyList<IndicatorName>? include)
    {
        Preset = preset;
        Include = include;
    }

    /// <summary>
    /// Gets the preset type.
    /// </summary>
    public IndicatorPreset Preset { get; }

    /// <summary>
    /// Gets the specific indicators to include (for Only preset).
    /// </summary>
    public IReadOnlyList<IndicatorName>? Include { get; }

    /// <summary>
    /// Creates a selection for all indicators.
    /// </summary>
    public static IndicatorSelection All()
    {
        return new IndicatorSelection(IndicatorPreset.All, null);
    }

    /// <summary>
    /// Creates a selection for core indicators only.
    /// </summary>
    public static IndicatorSelection Core()
    {
        return new IndicatorSelection(IndicatorPreset.Core, null);
    }

    /// <summary>
    /// Creates a selection for specific indicators only.
    /// </summary>
    public static IndicatorSelection Only(params IndicatorName[] names)
    {
        return new IndicatorSelection(IndicatorPreset.Only, names);
    }

    /// <summary>
    /// Creates an empty selection (no default indicators).
    /// This is the default - only indicators explicitly added via ConfigureIndicators are computed.
    /// </summary>
    public static IndicatorSelection None()
    {
        return new IndicatorSelection(IndicatorPreset.None, null);
    }
}
