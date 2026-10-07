namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DoubleSmoothingWindow
{
    private readonly double _alpha, _gamma;
    private RocBankValue _previous, _prior;
    internal DoubleSmoothingWindow(double alpha, double gamma)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        if (double.IsNaN(gamma) || double.IsInfinity(gamma)) throw new ArgumentOutOfRangeException(nameof(gamma));
        _alpha = alpha; _gamma = gamma;
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var total = new ExactMeanAccumulator(); first.AddTo(ref total); second.AddTo(ref total, sign);
        return RocBankValue.Round(total);
    }
    internal double Next(double price, bool commit)
    {
        var change = Add(_previous, _prior, -1);
        var trend = Add(change, change.Multiply(1 - _gamma)).Multiply(_gamma);
        var retained = Add(_previous, trend).Multiply(1 - _alpha);
        var value = Add(new RocBankValue(price).Multiply(_alpha), retained);
        if (commit) { _prior = _previous; _previous = value; }
        return value.Publish();
    }
    internal void Reset() { _previous = default; _prior = default; }
}
