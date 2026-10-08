using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class NarrowBandpassWindow : IDisposable
{
    private readonly double[] _weights;
    private readonly PooledRingBuffer<double> _prices;
    internal NarrowBandpassWindow(int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(2, length); _weights = new double[length];
        for (var j = 1; j < length - 1; j++)
        {
            if (2L * j == length) continue;
            var x = (double)j / (length - 1);
            var window = new ExactMeanAccumulator(); window.Add(.42);
            window.AddProduct(-.5, Math.Cos(2 * Math.PI * x));
            window.AddProduct(.08, Math.Cos(4 * Math.PI * x));
            var product = new ExactMeanAccumulator();
            product.AddProduct(Math.Sin(2 * Math.PI * j / length), window.Mean(1));
            _weights[j] = product.Mean(1);
        }
        _prices = new(Math.Min(length, Math.Max(1, capacityHint)));
    }
    internal double Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var j = 1; j < _weights.Length && j <= _prices.Count; j++)
            sum.AddProduct(_prices[_prices.Count - j], _weights[j]);
        var value = sum.Mean(1);
        if (commit) _prices.TryAdd(price, out _);
        return value;
    }
    internal void Reset() => _prices.Clear();
    public void Dispose() => _prices.Dispose();
}
