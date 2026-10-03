using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// Products of finite price and volume changes can lie beyond either binary64
// exponent bound while their liquid-strength share remains representable.
internal sealed class LiquidRsiWindow
{
    private readonly int _length;
    private double _price, _volume, _value;
    private bool _hasPrevious;
    private Number _numerator, _denominator;
    internal LiquidRsiWindow(int length) => _length = Math.Max(1, length);
    private readonly struct Number
    {
        private readonly double _high, _low;
        private readonly int _shift;
        private Number(double high, double low, int shift) { _high = high; _low = low; _shift = shift; }
        internal void AddTo(ref ExactMeanAccumulator sum, int weight = 1)
        {
            var opposite = new ExactMeanAccumulator(); opposite.Add(_high, -weight); opposite.Add(_low, -weight);
            opposite.ScaleByPowerOfTwo(_shift); sum.Subtract(opposite);
        }
        internal static Number Smooth(Number previous, ExactMeanAccumulator product, int length)
        {
            previous.AddTo(ref product, length - 1);
            if (product.IsExactlyZero) return default;
            var shift = 0; var high = product.Mean(length);
            var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(high) < lower) { product.ScaleByPowerOfTwo(512); shift = checked(shift - 512); high = product.Mean(length); }
            while (double.IsInfinity(high) || Math.Abs(high) >= upper) { product.ScaleByPowerOfTwo(-512); shift = checked(shift + 512); high = product.Mean(length); }
            product.Add(high, -length); return new(high, product.Mean(length), shift);
        }
    }
    internal double Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var product = new ExactMeanAccumulator();
        if (_hasPrevious)
        {
            product.AddProduct(price, volume); product.AddProduct(price, _volume, -1);
            product.AddProduct(_price, volume, -1); product.AddProduct(_price, _volume);
        }
        if (product.Sign < 0) { var absolute = new ExactMeanAccumulator(); absolute.Subtract(product); product = absolute; }
        var positive = _hasPrevious && price > _price && volume > _volume;
        var numerator = Number.Smooth(_numerator, positive ? product : default, _length);
        var denominator = Number.Smooth(_denominator, product, _length);
        var top = new ExactMeanAccumulator(); numerator.AddTo(ref top, 100);
        var bottom = new ExactMeanAccumulator(); denominator.AddTo(ref bottom);
        // Both masses undergo the same decay when either change is zero.
        // Preserve the exact constant share, including arbitrarily long flat runs.
        var value = _length > 1 && product.IsExactlyZero ? _value : Math.Max(0, Math.Min(100, top.Ratio(bottom)));
        if (final) { _price = price; _volume = volume; _value = value; _hasPrevious = true; _numerator = numerator; _denominator = denominator; }
        return value;
    }
    internal void Reset() { _price = _volume = _value = 0; _hasPrevious = false; _numerator = _denominator = default; }
}
