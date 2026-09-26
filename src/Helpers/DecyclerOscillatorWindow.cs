namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DecyclerOscillatorWindow
{
    private readonly HighPassWindow _first, _second;
    private readonly RocBankValue _factor;
    internal DecyclerOscillatorWindow(int length, double multiplier = 1.2, double periodScale = 1)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        _first = new(length, periodScale); _second = new(length, periodScale / 2);
        var factor = new ExactMeanAccumulator(); factor.Add(multiplier, 100); _factor = RocBankValue.Round(factor);
    }
    internal double Next(double price, bool commit)
    {
        var highPass = _first.NextValue(price, commit);
        var difference = new ExactMeanAccumulator(); difference.Add(price); highPass.AddTo(ref difference, -1);
        var middle = RocBankValue.Round(difference);
        var filtered = _second.NextValue(middle, commit);
        if (price == 0) return 0;
        var numerator = new ExactMeanAccumulator(); numerator.AddProduct(filtered.Mantissa, _factor.Mantissa);
        numerator.ScaleByPowerOfTwo(filtered.UpperShift + _factor.UpperShift);
        return RocBankValue.Round(numerator, price).Publish();
    }
    internal void Reset() { _first.Reset(); _second.Reset(); }
}
