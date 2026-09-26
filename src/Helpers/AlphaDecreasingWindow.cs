namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AlphaDecreasingWindow
{
    private RocBankValue _previous;
    private long _count;
    private static RocBankValue Product(RocBankValue value, double factor)
    {
        if (factor == 0 || value.Mantissa == 0) return default;
        // Here the factors are 2/(bar number) and its binary64 complement, so
        // their binary exponents keep this exact power-of-two divisor finite.
        var bits = BitConverter.DoubleToInt64Bits(factor);
        var exponent = (int)((bits >> 52) & 2047);
        var significand = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
        if (bits < 0) significand = -significand;
        var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator, significand);
        return RocBankValue.Round(numerator, Math.Pow(2, 1074 - Math.Max(0, exponent - 1)));
    }
    internal double Next(double price, bool commit)
    {
        var alpha = 2d / (_count + 1d);
        var current = Product(new RocBankValue(price), alpha);
        var previous = Product(_previous, 1 - alpha);
        var sum = new ExactMeanAccumulator(); current.AddTo(ref sum); previous.AddTo(ref sum);
        var value = RocBankValue.Round(sum);
        if (commit) { _previous = value; _count++; }
        return value.Publish();
    }
    internal void Reset() { _previous = default; _count = 0; }
}
