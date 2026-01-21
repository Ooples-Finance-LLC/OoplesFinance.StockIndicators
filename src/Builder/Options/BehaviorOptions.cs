namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for runtime behavior.
/// </summary>
public sealed class BehaviorOptions
{
    /// <summary>
    /// Gets or sets whether to emit warmup values. Defaults to true.
    /// </summary>
    public bool EmitWarmup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to skip the first update. Defaults to false.
    /// </summary>
    public bool SkipFirstUpdate { get; set; } = false;
}
