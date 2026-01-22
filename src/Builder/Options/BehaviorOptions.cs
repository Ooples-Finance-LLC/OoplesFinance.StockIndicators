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

    /// <summary>
    /// Gets or sets whether to suppress warmup values specifically for streaming mode.
    /// When null, falls back to !EmitWarmup behavior.
    /// When true, streaming mode skips the first snapshot.
    /// When false, streaming mode emits all snapshots including warmup.
    /// Batch mode always emits since there's only one snapshot.
    /// </summary>
    public bool? SuppressStreamingWarmup { get; set; }
}
