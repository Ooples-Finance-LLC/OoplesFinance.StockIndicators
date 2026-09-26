namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RegularizedWindow
{
    private readonly double _alpha, _lambda;
    private RocBankValue _previous, _prior;
    internal RegularizedWindow(int length, double lambda)
    {
        if (double.IsNaN(lambda) || double.IsInfinity(lambda) || lambda == -1) throw new ArgumentOutOfRangeException(nameof(lambda));
        _alpha = 2d / (Math.Max(1, length) + 1d); _lambda = lambda;
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var change = Add(new RocBankValue(price), _previous, -1).Multiply(_alpha);
        var projection = Add(_previous.Multiply(2), _prior, -1).Multiply(_lambda);
        var numerator = Add(Add(_previous, change), projection);
        var sum = new ExactMeanAccumulator(); numerator.AddTo(ref sum); var value = RocBankValue.Round(sum, _lambda + 1);
        if (commit) { _prior = _previous; _previous = value; }
        return value.Publish();
    }
    internal void Reset() { _previous = default; _prior = default; }
}
