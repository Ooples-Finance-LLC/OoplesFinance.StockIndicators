namespace OoplesFinance.StockIndicators.Helpers;

// Exact products and total weights, with a single rounding of their ratio.
// Struct copies support previews without changing either accumulator.
internal struct ExactVolumeMean
{
    private ExactMeanAccumulator _weighted, _mass;

    internal void Add(double price, double volume, int taper = 1)
    {
        _weighted.AddProduct(price, volume, taper);
        // Product decoding removes exact powers of two from integral volumes,
        // keeping both the mass and its ratio on the small-integer path.
        _mass.AddProduct(volume, 1, taper);
    }

    internal void AddTypical(double high, double low, double close, double volume)
    {
        var typical = new ExactMeanAccumulator();
        typical.Add(high); typical.Add(low); typical.Add(close);
        Add(typical.Mean(3), volume);
    }

    internal double Value() => _weighted.Ratio(_mass);
}
