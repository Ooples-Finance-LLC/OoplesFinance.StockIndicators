using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Exact rolling quadratic slopes, centered on the selected moving averages.</summary>
internal sealed class QuadraticProjectionWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices;
    private readonly RocBankAverage? _xMean, _qMean, _yMean;
    private BigInteger _sum, _linear, _square;
    private long _index;
    internal QuadraticProjectionWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length); _prices = new(Math.Min(_length, Math.Max(1, capacityHint)));
        if (StrengthWindow.Supports(kind)) { _xMean = new(kind, _length, capacityHint); _qMean = new(kind, _length, capacityHint); _yMean = new(kind, _length, capacityHint); }
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    // Sums over [0,n]; missing prehistory has both index and price zero.
    private static BigInteger PowerSum(BigInteger n, int power) => n.Sign < 0 ? BigInteger.Zero : power == 1 ? n * (n + 1) / 2
        : power == 2 ? n * (n + 1) * (2 * n + 1) / 6
        : power == 3 ? BigInteger.Pow(n * (n + 1) / 2, 2)
        : n * (n + 1) * (2 * n + 1) * (3 * n * n + 3 * n - 1) / 30;
    internal double Next(double price, bool commit, double? indexMean = null, double? squareMean = null, double? priceMean = null)
    {
        var x = new BigInteger(_index); var q = x * x; var incoming = ExactVarianceWindow.Units(price);
        var expiredX = x - _length; var expired = _prices.Count == _length ? ExactVarianceWindow.Units(_prices[0]) : BigInteger.Zero;
        var sum = _sum + incoming - expired;
        var linear = _linear + x * incoming - expiredX * expired;
        var square = _square + q * incoming - expiredX * expiredX * expired;
        var xm = indexMean.HasValue ? new RocBankValue(indexMean.Value) : _xMean!.Next(new RocBankValue((double)x), commit);
        var qm = squareMean.HasValue ? new RocBankValue(squareMean.Value) : _qMean!.Next(new RocBankValue((double)q), commit);
        var ym = priceMean.HasValue ? new RocBankValue(priceMean.Value) : _yMean!.Next(new RocBankValue(price), commit);
        var result = ym.Publish();
        if (_length >= 3 && _index >= 2)
        {
            var n = new BigInteger(_length);
            var sx = PowerSum(x, 1) - PowerSum(expiredX, 1); var sq = PowerSum(x, 2) - PowerSum(expiredX, 2);
            var sc = PowerSum(x, 3) - PowerSum(expiredX, 3); var sf = PowerSum(x, 4) - PowerSum(expiredX, 4);
            var xx = n * sq - sx * sx; var xq = n * sc - sx * sq; var qq = n * sf - sq * sq;
            var xy = n * linear - sx * sum; var qy = n * square - sq * sum;
            var determinant = xx * qq - xq * xq;
            if (!determinant.IsZero)
            {
                var slope = xy * qq - qy * xq; var curvature = qy * xx - xy * xq;
                var numerator = (Units(ym) * determinant << 1074) + slope * ((x << 1074) - Units(xm)) + curvature * ((q << 1074) - Units(qm));
                result = ExactMeanAccumulator.UnitRatio(numerator, determinant << 1074);
            }
        }
        if (commit) { _prices.TryAdd(price, out _); _sum = sum; _linear = linear; _square = square; _index++; }
        return result;
    }
    internal void Reset() { _prices.Clear(); _xMean?.Reset(); _qMean?.Reset(); _yMean?.Reset(); _sum = _linear = _square = default; _index = 0; }
    public void Dispose() { _prices.Dispose(); _xMean?.Dispose(); _qMean?.Dispose(); _yMean?.Dispose(); }
}
