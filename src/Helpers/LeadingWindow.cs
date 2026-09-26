namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class LeadingWindow
{
    private readonly double _alpha1, _alpha2;
    private double _price;
    private RocBankValue _lead, _output;
    internal LeadingWindow(double alpha1 = .25, double alpha2 = .33)
    {
        if (double.IsNaN(alpha1) || double.IsInfinity(alpha1)) throw new ArgumentOutOfRangeException(nameof(alpha1));
        if (double.IsNaN(alpha2) || double.IsInfinity(alpha2)) throw new ArgumentOutOfRangeException(nameof(alpha2));
        _alpha1 = alpha1; _alpha2 = alpha2;
    }
    private static void AddScaled(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    {
        var term = new ExactMeanAccumulator(); term.AddProduct(value.Mantissa, coefficient); term.ScaleByPowerOfTwo(value.UpperShift);
        var negative = new ExactMeanAccumulator(); negative.Subtract(term); sum.Subtract(negative);
    }
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var leadSum = new ExactMeanAccumulator(); leadSum.Add(price, 2); leadSum.AddProduct(_price, _alpha1); leadSum.Add(_price, -2);
        _lead.AddTo(ref leadSum, 1); AddScaled(ref leadSum, _lead, -_alpha1);
        var lead = RocBankValue.Round(leadSum);
        var outputSum = new ExactMeanAccumulator(); AddScaled(ref outputSum, lead, _alpha2); _output.AddTo(ref outputSum, 1); AddScaled(ref outputSum, _output, -_alpha2);
        var output = RocBankValue.Round(outputSum);
        if (commit) { _price = price; _lead = lead; _output = output; }
        return output.Publish();
    }
    internal void Reset() { _price = 0; _lead = _output = default; }
}
