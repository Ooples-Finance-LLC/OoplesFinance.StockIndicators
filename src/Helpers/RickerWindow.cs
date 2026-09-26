namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RickerWindow : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _mass;
    private readonly PooledRingBuffer<double> _prices;
    internal RickerWindow(int length, double pctWidth)
    {
        if (double.IsNaN(pctWidth) || double.IsInfinity(pctWidth)) throw new ArgumentOutOfRangeException(nameof(pctWidth));
        length = Math.Max(1, length); _weights = new double[length];
        var mass = new ExactMeanAccumulator();
        for (var lag = 0; lag < length; lag++)
        {
            // Normalize before multiplication so finite width percentages cannot overflow.
            var radius = lag == 0 ? 0 : pctWidth == 0 ? double.PositiveInfinity : Math.Abs((lag / (double)length) * 100 / pctWidth);
            var square = radius * radius;
            var weight = radius > 40 ? 0 : radius > 37 ? -Math.Exp(Math.Log(square - 1) - square / 2) : (1 - square) * Math.Exp(-square / 2);
            _weights[lag] = weight; mass.Add(weight);
        }
        _mass = mass; _prices = new PooledRingBuffer<double>(length);
    }
    internal double Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator();
        for (var lag = 0; lag < _weights.Length && lag <= _prices.Count; lag++)
            sum.AddProduct(lag == 0 ? price : _prices[_prices.Count - lag], _weights[lag]);
        var result = _mass.IsExactlyZero ? price : sum.Ratio(_mass);
        if (commit) _prices.TryAdd(price, out _);
        return result;
    }
    internal void Reset() => _prices.Clear();
    public void Dispose() => _prices.Dispose();
}
