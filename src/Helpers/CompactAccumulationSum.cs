namespace OoplesFinance.StockIndicators.Helpers;

// Keep the original small-word path first. When it needs a wide integer,
// promote the exact sum to bounded dyadic arithmetic with its wide fallback.
internal struct CompactAccumulationSum
{
#if NETFRAMEWORK
    private ExactMeanAccumulator _sum;
    internal void Add(double value) => _sum.Add(value);
    internal void Add(RocBankValue value) => value.AddTo(ref _sum);
    internal double Publish() => _sum.Mean(1);
    internal RocBankValue Rounded() => RocBankValue.Round(_sum);
#else
    private ExactMeanAccumulator _ordinary;
    private CompactMacdValue _sum;
    private bool _compact;
    internal void Add(double value)
    {
        if (_compact) _sum += CompactMacdValue.Of(value);
        else { _ordinary.Add(value); PromoteIfWide(); }
    }
    internal void Add(RocBankValue value)
    {
        if (!_compact)
        {
            value.AddTo(ref _ordinary);
            PromoteIfWide();
            return;
        }
        if (value.UpperShift == 0) Add(value.Mantissa);
        else _sum += CompactMacdValue.FromUnits(CompactMacdValue.Of(value.Mantissa).Units << value.UpperShift);
    }
    private void PromoteIfWide()
    {
        if (!_ordinary.UsesWideInteger) return;
        var parts = _ordinary.ExactParts;
        if (parts.Scale < 0) throw new InvalidOperationException("Rounded flows must stay on the binary64 unit grid.");
        _sum = CompactMacdValue.FromUnits(parts.Coefficient << parts.Scale);
        _ordinary = default;
        _compact = true;
    }
    internal double Publish() => _compact ? _sum.Round(1L).Publish() : _ordinary.Mean(1);
    internal RocBankValue Rounded()
    {
        if (!_compact) return RocBankValue.Round(_ordinary);
        double value = Publish();
        if (double.IsFinite(value)) return new RocBankValue(value);
        // An unpublished cumulative overflow may later cancel. Recover its
        // extended rounded stage from the exact total, never from infinity.
        var exact = new ExactMeanAccumulator();
        exact.Add(1, _sum.Units);
        exact.ScaleByPowerOfTwo(-1074);
        return RocBankValue.Round(exact);
    }
#endif
}
