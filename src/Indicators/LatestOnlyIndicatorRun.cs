namespace OoplesFinance.StockIndicators.Indicators;

internal sealed class LatestOnlyIndicatorRun(Dictionary<IIndicatorOutput, double[]> series, int count, IBarSnapshot? latest) : IIndicatorRun
{
    public bool IsComplete => true;
    public IBarSnapshot Latest => latest ?? throw new InvalidOperationException("No bars have arrived yet.");
    public void Dispose() { }
    public IAsyncEnumerator<IBarSnapshot> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Finite snapshot replay requires ConfigureHistory(IndicatorHistoryMode.Full).");
    }
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
