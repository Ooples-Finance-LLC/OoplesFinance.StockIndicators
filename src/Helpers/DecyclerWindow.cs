using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DecyclerWindow
{
    private readonly BigInteger _alpha, _feedback;
    private double _price;
    private RocBankValue _previous;
    internal DecyclerWindow(int length)
    {
        _alpha = ExactVarianceWindow.Units(EhlersFirstOrderCoefficient.Alpha(Math.Max(1, length)));
        _feedback = 2 * ((BigInteger.One << 1074) - _alpha);
    }
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var sum = new ExactMeanAccumulator(); sum.Add(price, _alpha); sum.Add(_price, _alpha);
        var feedback = new ExactMeanAccumulator(); feedback.Add(_previous.Mantissa, _feedback); feedback.ScaleByPowerOfTwo(_previous.UpperShift);
        var negative = new ExactMeanAccumulator(); negative.Subtract(feedback); sum.Subtract(negative);
        sum.ScaleByPowerOfTwo(-1075); var result = RocBankValue.Round(sum);
        if (commit) { _price = price; _previous = result; }
        return result.Publish();
    }
    internal void Reset() { _price = 0; _previous = default; }
}
