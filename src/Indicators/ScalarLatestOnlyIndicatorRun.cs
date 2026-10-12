namespace OoplesFinance.StockIndicators.Indicators;

// A completed scalar run owns one series and one bar. It needs neither a lookup
// table nor a separate snapshot object; both interfaces share this immutable owner.
internal sealed class ScalarLatestOnlyIndicatorRun(IIndicatorOutput output, double[] values,
    Bar bar, int warmup) : IIndicatorRun, IBarSnapshot
{
    public bool IsComplete => true;
    public int BarCount => values.Length;
    public IBarSnapshot Latest => this;
    public Bar Bar => bar;
    public int Index => values.Length - 1;
    public bool IsWarmedUp => values.Length >= warmup && !double.IsNaN(values[values.Length - 1]);
    public void Dispose() { }

    public IAsyncEnumerator<IBarSnapshot> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Finite snapshot replay requires ConfigureHistory(IndicatorHistoryMode.Full).");
    }

    public ReadOnlySpan<double> this[IIndicator indicator] => indicator is null
        ? throw new ArgumentNullException(nameof(indicator)) : this[IndicatorContract.PrimaryOutput(indicator)];
    public ReadOnlySpan<double> this[IIndicatorOutput requested]
    {
        get
        {
            if (requested is null) throw new ArgumentNullException("output");
            if (!EqualityComparer<IIndicatorOutput>.Default.Equals(output, requested))
                throw new KeyNotFoundException("That output was not configured on this result.");
            return values;
        }
    }

    double IBarSnapshot.this[IIndicator indicator] => indicator is null
        ? throw new ArgumentNullException(nameof(indicator)) : ((IBarSnapshot)this)[IndicatorContract.PrimaryOutput(indicator)];
    double IBarSnapshot.this[IIndicatorOutput requested]
    {
        get
        {
            if (requested is null) throw new ArgumentNullException("output");
            if (!EqualityComparer<IIndicatorOutput>.Default.Equals(output, requested))
                throw new KeyNotFoundException("That series was not configured on this run. Pass the indicator to ConfigureIndicators before reading it.");
            return values[values.Length - 1];
        }
    }
}
