using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PeakValleyWindow : IDisposable
{
    private readonly int _length, _smooth;
    private readonly MovingAvgType _kind;
    private readonly MacZWindow.Average _average;
    private readonly Queue<Number> _residuals = new();
    private readonly LinkedList<(long Index, Number Value)> _highs = new();
    private Number _sum, _weighted, _previousRatio, _meanSum, _previousMean;
    private long _count;
    internal PeakValleyWindow(MovingAvgType kind, int length, int smooth)
    { _kind = kind; _length = Math.Max(1, length); _smooth = Math.Max(1, smooth); _average = new(kind, _length); }
    private Number Mean(Number value, bool final)
    {
        if (_kind is not (MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod)) return _average.Next(value, final);
        var sum = _meanSum; Number result;
        if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum += value; result = sum.Divide(_count + 1); }
        else result = (_previousMean.Times(_length - 1L) + value.Times(_kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1)).Divide(_kind == MovingAvgType.ExponentialMovingAverage ? _length + 1L : _length);
        if (final) { _meanSum = sum; _previousMean = result; }
        return result;
    }
    private Number Fit(Number residual, bool final)
    {
        var full = _residuals.Count == _smooth;
        var expired = full ? _residuals.Peek() : default;
        var n = full ? _residuals.Count : _residuals.Count + 1L;
        var sum = _sum + residual - expired;
        var weighted = full ? _weighted - _sum + expired + residual.Times(n - 1) : _weighted + residual.Times(n - 1);
        // The OLS endpoint simplifies to (6*sum(j*yj) + (4-2*n)*sum(yj))/(n*(n+1)).
        var result = (weighted.Times(6) + sum.Times(4 - 2 * n)).Divide(n * (n + 1));
        if (final) { if (full) _residuals.Dequeue(); _residuals.Enqueue(residual); _sum = sum; _weighted = weighted; }
        return result;
    }
    private Number Highest(Number fit, bool final)
    {
        var expiry = _count - Math.Max(2, _length) + 1L;
        var first = _highs.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null || (fit - first.Value.Value).Sign > 0 ? fit : first.Value.Value;
        if (final)
        {
            while (_highs.First is { } old && old.Value.Index < expiry) _highs.RemoveFirst();
            while (_highs.Last is { } last && (last.Value.Value - fit).Sign <= 0) _highs.RemoveLast();
            _highs.AddLast((_count, fit));
        }
        return result;
    }
    private (double Sign1, double Sign2, double Sign3) FromMean(Number value, Number mean, bool final)
    {
        var offset = value - mean;
        var fit = Fit(offset.Sign < 0 ? default(Number) - offset : offset, final);
        var high = Highest(fit, final);
        var ratio = high.Sign == 0 ? default : fit.Divide(high);
        var atPeak = (ratio - Number.Of(1)).Sign == 0;
        var wasPeak = (_previousRatio - Number.Of(1)).Sign == 0;
        var direction = -(double)offset.Sign;
        var sign1 = atPeak && !wasPeak ? direction : 0;
        var sign2 = (ratio.Times(5) - Number.Of(4)).Sign < 0 ? direction : 0;
        var sign3 = wasPeak && (ratio - _previousRatio).Sign < 0 ? direction : 0;
        if (final) { _previousRatio = ratio; _count++; }
        return (sign1, sign2, sign3);
    }
    internal (double Sign1, double Sign2, double Sign3) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        return FromMean(value, Mean(value, final), final);
    }
    internal static (double[] Sign1, double[] Sign2, double[] Sign3) Calculate(StockData data, List<double> prices, MovingAvgType kind, int length, int smooth, bool callbacks)
    {
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        length = Math.Max(1, length); var caller = data.CaptureInputSeries();
        try
        {
            var custom = callbacks ? ComponentAverage.Take(SpanCompat.AsReadOnlySpan(prices), length) : null;
            var means = custom ?? (!StrengthWindow.Supports(kind) ? CalculationsHelper.GetMovingAverageList(data, kind, length, prices) : null);
            using var window = new PeakValleyWindow(kind, length, smooth);
            var points = prices.Select((price, i) => means is null ? window.Next(price, true) : window.FromMean(Number.Of(price), Number.Of(means[i]), true)).ToArray();
            return (points.Select(p => p.Sign1).ToArray(), points.Select(p => p.Sign2).ToArray(), points.Select(p => p.Sign3).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _average.Reset(); _residuals.Clear(); _highs.Clear(); _sum = _weighted = _previousRatio = _meanSum = _previousMean = default; _count = 0; }
    public void Dispose() { _average.Dispose(); _residuals.Clear(); _highs.Clear(); }
}
