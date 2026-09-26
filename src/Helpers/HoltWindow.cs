namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HoltWindow
{
    private readonly double _alpha, _gamma;
    private RocBankValue _level, _trend;
    private bool _hasPrevious;
    internal HoltWindow(int alphaLength, int gammaLength)
    {
        _alpha = 2d / (Math.Max(1, alphaLength) + 1d); _gamma = 2d / (Math.Max(1, gammaLength) + 1d);
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var priorTrend = _hasPrevious ? _trend : new RocBankValue(price);
        var level = Add(Add(_level, priorTrend).Multiply(1 - _alpha), new RocBankValue(price).Multiply(_alpha));
        var trend = Add(priorTrend.Multiply(1 - _gamma), Add(level, _level, -1).Multiply(_gamma));
        if (commit) { _level = level; _trend = trend; _hasPrevious = true; }
        return level.Publish();
    }
    internal void Reset() { _level = default; _trend = default; _hasPrevious = false; }
}
