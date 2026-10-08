namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class LaguerreFilterWindow
{
    private readonly double _alpha;
    private RocBankValue _l0, _l1, _l2, _l3;
    private bool _started;
    internal LaguerreFilterWindow(double alpha)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        _alpha = alpha;
    }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    {
        var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient);
        negative.ScaleByPowerOfTwo(value.UpperShift); sum.Subtract(negative);
    }
    private RocBankValue First(RocBankValue current, RocBankValue old)
    {
        var sum = new ExactMeanAccumulator(); old.AddTo(ref sum); Product(ref sum, current, _alpha); Product(ref sum, old, -_alpha);
        return RocBankValue.Round(sum);
    }
    private RocBankValue Stage(RocBankValue current, RocBankValue oldInput, RocBankValue oldOutput)
    {
        var sum = new ExactMeanAccumulator(); oldInput.AddTo(ref sum); oldOutput.AddTo(ref sum); current.AddTo(ref sum, -1);
        Product(ref sum, current, _alpha); Product(ref sum, oldOutput, -_alpha);
        return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var input = new RocBankValue(price);
        var p0 = _started ? _l0 : input; var p1 = _started ? _l1 : input;
        var p2 = _started ? _l2 : input; var p3 = _started ? _l3 : input;
        var l0 = First(input, p0); var l1 = Stage(l0, p0, p1); var l2 = Stage(l1, p1, p2); var l3 = Stage(l2, p2, p3);
        var sum = new ExactMeanAccumulator(); l0.AddTo(ref sum); l1.AddTo(ref sum, 2); l2.AddTo(ref sum, 2); l3.AddTo(ref sum);
        var value = RocBankValue.Round(sum, count: 6);
        if (commit) { _l0 = l0; _l1 = l1; _l2 = l2; _l3 = l3; _started = true; }
        return value.Publish();
    }
    internal void Reset() { _l0 = _l1 = _l2 = _l3 = default; _started = false; }
}
