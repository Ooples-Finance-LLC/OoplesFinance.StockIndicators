using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ShapeshiftingWindow : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _mass;
    private readonly PooledRingBuffer<double> _prices;
    internal ShapeshiftingWindow(int length, int capacityHint = int.MaxValue)
    {
        length = Math.Max(2, length); _weights = new double[length];
        var d = new BigInteger(length - 1); var fourth = BigInteger.Pow(d, 4);
        var mass = new ExactMeanAccumulator();
        for (var j = 0; j < length; j++)
        {
            var denominator = fourth + BigInteger.Pow(new BigInteger(j), 4);
            var numerator = denominator - 2 * j * BigInteger.Pow(d, 3);
            _weights[j] = ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator);
            mass.Add(_weights[j]);
        }
        _mass = mass; _prices = new(Math.Min(length, Math.Max(1, capacityHint)));
    }
    internal double Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        sum.AddProduct(price, _weights[0]);
        for (var j = 1; j < _weights.Length && j <= _prices.Count; j++)
            sum.AddProduct(_prices[_prices.Count - j], _weights[j]);
        var value = sum.Ratio(_mass);
        if (commit) _prices.TryAdd(price, out _);
        return value;
    }
    internal void Reset() => _prices.Clear();
    public void Dispose() => _prices.Dispose();
}
