namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DetrendedLeadingWindow
{
    private readonly double _alpha, _half;
    private double _high, _low;
    private bool _seeded;
    private RocBankValue _fast, _slow, _signal;
    internal DetrendedLeadingWindow(int length)
    { length = Math.Max(1, length); _alpha = length > 2 ? 2d / (length + 1d) : 0.67; _half = _alpha / 2; }
    private static RocBankValue Add(RocBankValue a, RocBankValue b, int sign = 1)
    { var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Blend(RocBankValue value, RocBankValue previous, double alpha) => Add(value.Multiply(alpha), previous.Multiply(1 - alpha));
    internal (double Dsp, double Deli) Next(double high, double low, bool commit)
    {
        var price = new RocBankValue(PriceMean.Of(Math.Max(_high, high), Math.Min(_low, low)));
        var fast = Blend(price, _seeded ? _fast : price, _alpha); var slow = Blend(price, _seeded ? _slow : price, _half);
        var dsp = Add(fast, slow, -1); var signal = Blend(dsp, _signal, _alpha); var deli = Add(dsp, signal, -1);
        if (commit) { _high = high; _low = low; _fast = fast; _slow = slow; _signal = signal; _seeded = true; }
        return (dsp.Publish(), deli.Publish());
    }
    internal void Reset() { _high = _low = 0; _fast = _slow = _signal = default; _seeded = false; }
}
