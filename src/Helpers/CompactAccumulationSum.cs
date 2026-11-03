namespace OoplesFinance.StockIndicators.Helpers;

// Rounded flows accumulate exactly. Ordinary exponent spans reuse the bounded
// dyadic coefficient; extended stages and wider spans retain exact wide units.
internal struct CompactAccumulationSum
{
#if NETFRAMEWORK
    private ExactMeanAccumulator _sum;
    internal void Add(double value) => _sum.Add(value);
    internal void Add(RocBankValue value) => value.AddTo(ref _sum);
    internal double Publish() => _sum.Mean(1);
    internal RocBankValue Rounded() => RocBankValue.Round(_sum);
#else
    private CompactMacdValue _sum;
    internal void Add(double value) => _sum += CompactMacdValue.Of(value);
    internal void Add(RocBankValue value)
    {
        if (value.UpperShift == 0) Add(value.Mantissa);
        else _sum += CompactMacdValue.FromUnits(CompactMacdValue.Of(value.Mantissa).Units << value.UpperShift);
    }
    internal double Publish() => _sum.Round(1L).Publish();
    internal RocBankValue Rounded()
    {
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
