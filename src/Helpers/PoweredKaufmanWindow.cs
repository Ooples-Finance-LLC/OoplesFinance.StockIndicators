namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PoweredKaufmanWindow : IDisposable
{
    private readonly RoundedKaufmanWindow _efficiency;
    private readonly double _factor;
    private double _average, _a, _b, _upper, _lower;
    private bool _started, _side;
    internal PoweredKaufmanWindow(int length, double factor) { _efficiency = new(Math.Max(1, length)); _factor = factor; }
    private static double Blend(double previous, double current, double gain)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(previous); sum.AddProduct(current, gain); sum.AddProduct(previous, gain, -1);
        return sum.Mean(1);
    }
    internal (double Average, double Power, double Stop) Next(double price, bool commit)
    {
        var gain = MathHelper.Pow(_efficiency.Next(price, commit).Efficiency, _factor);
        var average = Blend(_started ? _average : price, price, gain);
        var previousA = _started ? _a : price; var previousB = _started ? _b : price;
        var a = Blend(Math.Max(price, previousA), Math.Min(price, previousA), gain);
        var b = Blend(Math.Min(price, previousB), Math.Max(price, previousB), gain);
        var upper = a > previousA || a < previousA && b < previousB ? a : _upper;
        var lower = b < previousB || b > previousB && a > previousA ? b : _lower;
        var side = upper > price ? true : lower > price ? false : _side;
        var stop = side ? lower : upper;
        if (commit) { _average = average; _a = a; _b = b; _upper = upper; _lower = lower; _side = side; _started = true; }
        return (average, gain, stop);
    }
    internal void Reset() { _efficiency.Reset(); _average = _a = _b = _upper = _lower = 0; _started = _side = false; }
    public void Dispose() => _efficiency.Dispose();
}
