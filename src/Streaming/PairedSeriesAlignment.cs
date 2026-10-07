namespace OoplesFinance.StockIndicators.Streaming;

// Exact timestamp pairing. Validate before any numerical state changes; commit the clock
// only after the corresponding numerical update succeeds. Corrections require reset/replay.
internal sealed class PairedSeriesAlignment
{
    private DateTime? _market, _primary;
    internal bool CanUpdate(MultiSeriesContext context, SeriesKey primary, SeriesKey market, SeriesKey series, OhlcvBar bar, bool final)
    {
        if (!series.Equals(primary) && !series.Equals(market)) return false;
        ValidateObservation(bar);
        var previous = series.Equals(market) ? _market : _primary;
        if (previous.HasValue && (bar.EndTime <= previous.Value || bar.EndTime.Kind != previous.Value.Kind))
            throw new ArgumentException("Duplicate or out-of-order observations require reset and replay.", nameof(bar));
        if (series.Equals(market)) return true;
        if (!_market.HasValue) return false;
        if (_market.Value.Kind != bar.EndTime.Kind)
            throw new ArgumentException("Paired observations must use the same timestamp kind.", nameof(bar));
        if (_market.Value > bar.EndTime)
            throw new ArgumentException("A future benchmark cannot be used for an earlier primary bar.", nameof(bar));
        if (_market.Value < bar.EndTime && context.AlignmentPolicy == SeriesAlignmentPolicy.Strict) return false;
        if (context.MaximumBenchmarkAge.HasValue && bar.EndTime - _market.Value > context.MaximumBenchmarkAge.Value) return false;
        return true;
    }
    internal void Commit(SeriesKey primary, SeriesKey market, SeriesKey series, OhlcvBar bar, bool final)
    {
        if (!final) return;
        if (series.Equals(market)) _market = bar.EndTime;
        else if (series.Equals(primary)) _primary = bar.EndTime;
    }
    internal void Reset() { _market = _primary = null; }
    internal static void ValidateObservation(OhlcvBar bar)
    {
        if (bar is null) throw new ArgumentNullException(nameof(bar));
        if (!Finite(bar.Open) || !Finite(bar.High) || !Finite(bar.Low) || !Finite(bar.Close) || !Finite(bar.Volume))
            throw new ArgumentOutOfRangeException(nameof(bar), "Paired observations must have finite OHLCV fields.");
        if (bar.EndTime < bar.StartTime) throw new ArgumentException("A bar cannot end before it starts.", nameof(bar));
        if (bar.EndTime.Kind != bar.StartTime.Kind) throw new ArgumentException("Bar timestamps must use the same kind.", nameof(bar));
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
