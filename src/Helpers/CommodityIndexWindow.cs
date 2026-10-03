using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// SMA uses the classical current-window mean absolute deviation. Other convex
// averages retain the published average-of-absolute-residuals variant.
internal sealed class CommodityIndexWindow : IDisposable
{
    private readonly int _length;
    private readonly double _constant;
    private readonly PooledRingBuffer<double>? _window;
    private readonly StrengthAverage? _price, _deviation;
    private BigInteger _sum;

    internal CommodityIndexWindow(MovingAvgType kind, int length, double constant, int capacityHint = int.MaxValue)
    {
        if (!StrengthWindow.Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ValidateConstant(constant);
        _length = Math.Max(1, length); _constant = constant;
        if (kind == MovingAvgType.SimpleMovingAverage) _window = new PooledRingBuffer<double>(Math.Min(_length, Math.Max(1, capacityHint)));
        else { _price = new StrengthAverage(kind, _length, capacityHint); _deviation = new StrengthAverage(kind, _length, capacityHint); }
    }

    internal static double TypicalPrice(double high, double low, double close)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(high); sum.Add(low); sum.Add(close);
        return sum.Mean(3);
    }
    internal static List<double> Prices(StockData data)
    {
        if (data.ChainedValues.Count > 0) return data.ChainedValues;
        var result = new List<double>(data.Count);
        for (var i = 0; i < data.Count; i++) result.Add(TypicalPrice(data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i]));
        return result;
    }

    internal static void ValidateConstant(double constant)
    {
        if (constant == 0 || double.IsNaN(constant) || double.IsInfinity(constant)) throw new ArgumentOutOfRangeException(nameof(constant));
    }

    internal double Next(double value, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator();
        if (_window is not null)
        {
            var current = ExactVarianceWindow.Units(value);
            var full = _window.Count == _length;
            var sum = _sum + current - (full ? ExactVarianceWindow.Units(_window[0]) : BigInteger.Zero);
            var result = 0d;
            if (_window.Count >= _length - 1)
            {
                var n = new BigInteger(_length);
                var residual = n * current - sum;
                var deviations = BigInteger.Abs(residual);
                for (var i = full ? 1 : 0; i < _window.Count; i++)
                    deviations += BigInteger.Abs(n * ExactVarianceWindow.Units(_window[i]) - sum);
                numerator.Add(1d, n * residual); denominator.Add(_constant, deviations);
                result = numerator.Ratio(denominator);
            }
            if (commit) { _sum = sum; _window.TryAdd(value, out _); }
            return result;
        }
        var mean = _price!.Next(new StrengthValue(value), commit).Mantissa;
        var difference = new ExactMeanAccumulator(); difference.Add(value); difference.Add(mean, -1);
        var residualValue = StrengthValue.Round(difference, 1);
        var deviation = _deviation!.Next(residualValue.Absolute, commit);
        residualValue.AddTo(ref numerator);
        denominator.AddProduct(deviation.Mantissa, _constant, deviation.Doubled ? 2 : 1);
        return numerator.Ratio(denominator);
    }

    internal void Reset() { _window?.Clear(); _sum = default; _price?.Reset(); _deviation?.Reset(); }
    public void Dispose() { _window?.Dispose(); _price?.Dispose(); _deviation?.Dispose(); }
}
