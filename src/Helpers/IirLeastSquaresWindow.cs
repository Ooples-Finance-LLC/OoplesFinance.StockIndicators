namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class IirLeastSquaresWindow
{
    private readonly double _gain, _emaAlpha;
    private RocBankValue _previous, _ema;
    private bool _hasPrevious;
    internal IirLeastSquaresWindow(int length)
    {
        length = Math.Max(1, length); _gain = 4d / (length + 2d);
        var half = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        _emaAlpha = Math.Max(.01, Math.Min(.99, 2d / (half + 1d)));
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var current = new RocBankValue(price); var previous = _hasPrevious ? _previous : current;
        var ema = Add(previous.Multiply(_emaAlpha), _ema.Multiply(1 - _emaAlpha));
        var value = Add(Add(current.Multiply(_gain), previous), ema.Multiply(_gain), -1);
        if (commit) { _ema = ema; _previous = value; _hasPrevious = true; }
        return value.Publish();
    }
    internal void Reset() { _previous = default; _ema = default; _hasPrevious = false; }
}
