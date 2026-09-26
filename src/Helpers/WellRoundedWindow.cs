namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WellRoundedWindow
{
    private readonly double _alpha, _gamma;
    private RocBankValue _a, _b, _y, _mean, _residualY, _residualMean;
    internal WellRoundedWindow(int length)
    {
        _alpha = 2d / (Math.Max(1, length) + 1d); _gamma = Math.Min(.99, Math.Max(.01, _alpha));
    }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    {
        var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient);
        negative.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(negative);
    }
    private static RocBankValue Increment(RocBankValue previous, RocBankValue residual, double gain)
    {
        var sum = new ExactMeanAccumulator(); previous.AddTo(ref sum); Product(ref sum, residual, gain); return RocBankValue.Round(sum);
    }
    private static RocBankValue Blend(RocBankValue value, RocBankValue previous, double gain)
    {
        var sum = new ExactMeanAccumulator(); previous.AddTo(ref sum); Product(ref sum, value, gain); Product(ref sum, previous, -gain); return RocBankValue.Round(sum);
    }
    private static RocBankValue Difference(double price, RocBankValue value)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(price); value.AddTo(ref sum, -1); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var a = Increment(_a, _residualY, _alpha); var b = Increment(_b, _residualMean, _alpha);
        var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum);
        var y = Blend(RocBankValue.Round(sum), _y, .99); var mean = Blend(y, _mean, _gamma);
        var residualY = Difference(price, y); var residualMean = Difference(price, mean);
        if (commit) { _a = a; _b = b; _y = y; _mean = mean; _residualY = residualY; _residualMean = residualMean; }
        return y.Publish();
    }
    internal void Reset() { _a = default; _b = default; _y = default; _mean = default; _residualY = default; _residualMean = default; }
}
