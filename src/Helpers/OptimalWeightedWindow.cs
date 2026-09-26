using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class OptimalWeightedWindow : IDisposable
{
    private readonly PooledRingBuffer<(double Price, double Previous)> _pairs;
    private double _previous;
    internal OptimalWeightedWindow(int length) => _pairs = new(Math.Max(1, length));
    internal double Next(double price, bool commit)
    {
        var count = Math.Min(_pairs.Capacity, _pairs.Count + 1);
        BigInteger sx = 0, sy = 0, xx = 0, yy = 0, xy = 0;
        for (var j = 0; j < count; j++)
        {
            var pair = j == 0 ? (Price: price, Previous: _previous) : _pairs[_pairs.Count - j];
            var x = ExactVarianceWindow.Units(pair.Price); var y = ExactVarianceWindow.Units(pair.Previous);
            sx += x; sy += y; xx += x * x; yy += y * y; xy += x * y;
        }
        var vx = count * xx - sx * sx; var vy = count * yy - sy * sy; var cross = count * xy - sx * sy;
        var correlation = vx.IsZero || vy.IsZero ? 0 : cross.Sign * ExactPopulationDeviation.RootRatio((cross * cross) << 2148, vx * vy);
        var top = new ExactMeanAccumulator(); var bottom = new ExactMeanAccumulator();
        for (var j = 0; j < _pairs.Capacity; j++)
        {
            var weight = Math.Pow(_pairs.Capacity - j, correlation);
            bottom.Add(weight);
            if (j < count) top.AddProduct(j == 0 ? price : _pairs[_pairs.Count - j].Price, weight);
        }
        var result = top.Ratio(bottom);
        if (commit) { _pairs.TryAdd((price, _previous), out _); _previous = result; }
        return result;
    }
    internal void Reset() { _pairs.Clear(); _previous = 0; }
    public void Dispose() => _pairs.Dispose();
}
