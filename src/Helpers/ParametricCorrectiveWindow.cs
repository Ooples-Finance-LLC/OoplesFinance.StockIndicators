namespace OoplesFinance.StockIndicators.Helpers;

// Signed, time-dependent weights on the period-lagged input. Queues grow only with observed history.
internal sealed class ParametricCorrectiveWindow
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly RocBankValue _offset;
    private readonly Queue<double> _lag = new();
    private readonly Queue<(RocBankValue Weight, ExactMeanAccumulator Product)> _terms = new();
    private ExactMeanAccumulator _mass, _sum;
    private long _index;
    internal ParametricCorrectiveWindow(int length, double alpha = 1, double per = 35, bool complement = false)
    {
        if (!(!double.IsNaN(alpha) && !double.IsInfinity(alpha))) throw new ArgumentOutOfRangeException(nameof(alpha));
        if (!(!double.IsNaN(per) && !double.IsInfinity(per))) throw new ArgumentOutOfRangeException(nameof(per));
        _length = Math.Max(1, length); _alpha = alpha;
        var percentage = new ExactMeanAccumulator();
        if (complement) { percentage.Add(100); percentage.Add(per, -1); }
        else percentage.Add(per);
        var roundedPercentage = RocBankValue.Round(percentage);
        var fraction = new ExactMeanAccumulator(); roundedPercentage.AddTo(ref fraction);
        _offset = RocBankValue.Round(fraction, count: 100).Multiply(_length);
    }
    internal double Next(double price, bool commit)
    {
        var displacement = new ExactMeanAccumulator(); displacement.Add(1, new System.Numerics.BigInteger(_index) + 1); _offset.AddTo(ref displacement, -1);
        var weight = RocBankValue.Round(displacement);
        if (weight.Mantissa < 0) weight = weight.Multiply(_alpha);
        var lagged = _lag.Count == _length ? _lag.Peek() : 0;
        var product = new ExactMeanAccumulator(); product.AddProduct(lagged, weight.Mantissa); product.ScaleByPowerOfTwo(weight.UpperShift);
        var mass = _mass; var sum = _sum;
        if (_terms.Count == _length) { var oldest = _terms.Peek(); oldest.Weight.AddTo(ref mass, -1); sum.Subtract(oldest.Product); }
        weight.AddTo(ref mass); sum.Subtract(Negate(product));
        var result = sum.Ratio(mass);
        if (commit)
        {
            if (_lag.Count == _length) _lag.Dequeue(); _lag.Enqueue(price);
            if (_terms.Count == _length) _terms.Dequeue(); _terms.Enqueue((weight, product));
            _mass = mass; _sum = sum; _index++;
        }
        return result;
    }
    private static ExactMeanAccumulator Negate(ExactMeanAccumulator value) { var zero = new ExactMeanAccumulator(); zero.Subtract(value); return zero; }
    internal void Reset() { _lag.Clear(); _terms.Clear(); _mass = default; _sum = default; _index = 0; }
}
