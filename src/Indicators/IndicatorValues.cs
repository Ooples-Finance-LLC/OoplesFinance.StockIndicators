namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Completed indicator series without retained bar snapshots.</summary>
/// <remarks>The builder determines inputs, working storage and output layout. Results own
/// their published series; changing the source or building again cannot change them.</remarks>
public interface IIndicatorValues
{
    /// <summary>The configured indicator's primary series.</summary>
    ReadOnlySpan<double> this[IIndicator indicator] { get; }
    /// <summary>A configured indicator's named output series.</summary>
    ReadOnlySpan<double> this[IIndicatorOutput output] { get; }
    /// <summary>Number of published observations, excluding explicit warmup input.</summary>
    int BarCount { get; }
}

internal sealed class IndicatorValues(Dictionary<IIndicatorOutput, double[]> series, int count) : IIndicatorValues
{
    public int BarCount => count;
    public ReadOnlySpan<double> this[IIndicator indicator] => indicator is null
        ? throw new ArgumentNullException(nameof(indicator)) : this[IndicatorContract.PrimaryOutput(indicator)];
    public ReadOnlySpan<double> this[IIndicatorOutput output]
    {
        get
        {
            if (output is null) throw new ArgumentNullException(nameof(output));
            return series.TryGetValue(output, out var values) ? values
                : throw new KeyNotFoundException("That output was not configured on this result.");
        }
    }
}
