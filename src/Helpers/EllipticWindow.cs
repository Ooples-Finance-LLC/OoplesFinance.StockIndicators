namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EllipticWindow
{
    private readonly bool _modified;
    private double _input1, _input2, _input3;
    private RocBankValue _output1, _output2;
    private int _count;
    internal EllipticWindow(bool modified) => _modified = modified;
    private static RocBankValue Lead(double current, double previous)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(current, 2); sum.Add(previous, -1); return RocBankValue.Round(sum);
    }
    private static void Product(ref ExactMeanAccumulator sum, RocBankValue value, double coefficient)
    {
        // Subtracting a negated accumulator combines exact products without publishing them.
        var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, -coefficient); negative.ScaleByPowerOfTwo(value.UpperShift);
        sum.Subtract(negative);
    }
    internal double Next(double price, bool commit)
    {
        var first = _modified && _count < 1 ? price : _input1;
        var second = _modified && _count < 2 ? first : _input2;
        var third = _modified && _count < 3 ? second : _input3;
        var previous = _modified && _count < 1 ? new RocBankValue(price) : _output1;
        var older = _modified && _count < 2 ? previous : _output2;
        var sum = new ExactMeanAccumulator();
        Product(ref sum, _modified ? Lead(price, first) : new RocBankValue(price), .13785);
        Product(ref sum, _modified ? Lead(first, second) : new RocBankValue(first), .0007);
        Product(ref sum, _modified ? Lead(second, third) : new RocBankValue(second), .13785);
        Product(ref sum, previous, 1.2103); Product(ref sum, older, -.4867);
        var result = RocBankValue.Round(sum);
        if (commit)
        {
            _input3 = second; _input2 = first; _input1 = price;
            _output2 = previous; _output1 = result; if (_count < 3) _count++;
        }
        return result.Publish();
    }
    internal void Reset() { _input1 = _input2 = _input3 = 0; _output1 = _output2 = default; _count = 0; }
}
