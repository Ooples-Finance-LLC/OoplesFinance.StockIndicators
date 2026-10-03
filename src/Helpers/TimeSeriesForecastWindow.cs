using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TimeSeriesForecastWindow : IDisposable
{
    private readonly ExactLinearFitWindow _fit;
    private BigInteger _errorSum, _count;
    internal TimeSeriesForecastWindow(int length) { _fit = new(Math.Max(1, length)); }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var center = _fit.Next(close, commit).RoundedLastUnits;
        var error = RocBankValue.RoundUnits(BigInteger.Abs(ExactVarianceWindow.Units(close) - center), BigInteger.One);
        var sum = _errorSum + error; var width = RocBankValue.RoundUnits(sum, _count + 1);
        if (commit) { _errorSum = sum; _count++; }
        return (ExactMeanAccumulator.UnitRatio(center + width, BigInteger.One), ExactMeanAccumulator.UnitRatio(center, BigInteger.One), ExactMeanAccumulator.UnitRatio(center - width, BigInteger.One));
    }
    internal void Reset() { _fit.Reset(); _errorSum = _count = default; }
    public void Dispose() => _fit.Dispose();
}
