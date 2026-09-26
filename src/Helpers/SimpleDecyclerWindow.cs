namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SimpleDecyclerWindow
{
    private readonly HighPassWindow _highPass;
    private readonly double _upper, _lower;
    internal SimpleDecyclerWindow(int length, double upperPercent = .5, double lowerPercent = .5)
    {
        if (double.IsNaN(upperPercent) || double.IsInfinity(upperPercent)) throw new ArgumentOutOfRangeException(nameof(upperPercent));
        if (double.IsNaN(lowerPercent) || double.IsInfinity(lowerPercent)) throw new ArgumentOutOfRangeException(nameof(lowerPercent));
        _highPass = new(length); _upper = 1 + upperPercent / 100; _lower = 1 - lowerPercent / 100;
    }
    internal (double Middle, double Upper, double Lower) Next(double price, bool commit)
    {
        var highPass = _highPass.NextValue(price, commit);
        var total = new ExactMeanAccumulator(); total.Add(price); highPass.AddTo(ref total, -1);
        var middle = RocBankValue.Round(total);
        return (middle.Publish(), middle.Multiply(_upper).Publish(), middle.Multiply(_lower).Publish());
    }
    internal void Reset() => _highPass.Reset();
}
