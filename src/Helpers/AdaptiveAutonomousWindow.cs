using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveAutonomousWindow : IDisposable
{
    private readonly RoundedKaufmanWindow _efficiency;
    private readonly BigInteger _gamma;
    private BigInteger _first, _second, _sum, _upper, _lower;
    private long _count;
    private bool _rising;
    internal AdaptiveAutonomousWindow(int length, double gamma)
    { _efficiency = new(Math.Max(1, length)); _gamma = ExactVarianceWindow.Units(gamma); }
    private static BigInteger Round(BigInteger value) => RocBankValue.RoundUnits(value, BigInteger.One);
    private static BigInteger Blend(BigInteger previous, BigInteger current, BigInteger gain) =>
        RocBankValue.RoundUnits((previous << 1074) + gain * (current - previous), BigInteger.One << 1074);
    internal (double Average, double Deviation, double Stop) Next(double value, bool commit)
    {
        var efficiency = ExactVarianceWindow.Units(_efficiency.Next(value, commit).Efficiency);
        var price = ExactVarianceWindow.Units(value);
        var previous = _count == 0 ? price : _second; var firstPrevious = _count == 0 ? price : _first;
        var error = Round(BigInteger.Abs(price - previous)); var sum = Round(_sum + error);
        var deviation = _count == 0 ? BigInteger.Zero : RocBankValue.RoundUnits(RocBankValue.RoundUnits(sum, new BigInteger(_count)) * _gamma, BigInteger.One << 1074);
        var target = price > previous + deviation ? Round(price + deviation) : price < previous - deviation ? Round(price - deviation) : previous;
        var first = Blend(firstPrevious, target, efficiency); var second = Blend(previous, first, efficiency);
        var rising = price > _upper ? true : price < _lower ? false : _rising;
        var upper = Round(second + deviation); var lower = Round(second - deviation);
        var stop = rising ? lower : upper;
        if (commit) { _first = first; _second = second; _sum = sum; _upper = upper; _lower = lower; _rising = rising; _count++; }
        return (ExactMeanAccumulator.UnitRatio(second, BigInteger.One), ExactMeanAccumulator.UnitRatio(deviation, BigInteger.One), ExactMeanAccumulator.UnitRatio(stop, BigInteger.One));
    }
    internal void Reset() { _efficiency.Reset(); _first = _second = _sum = _upper = _lower = default; _count = 0; _rising = false; }
    public void Dispose() => _efficiency.Dispose();
}
