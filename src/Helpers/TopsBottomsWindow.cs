using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TopsBottomsWindow : IDisposable
{
    private readonly Average _mean;
    private readonly ConstantWindow _rises, _falls;
    private Number _previousMean;
    private bool _previousUp, _previousDown;
    internal TopsBottomsWindow(MovingAvgType kind, int length)
    { _mean = new(kind, length); _rises = new(length); _falls = new(length); }
    private double FromMean(Number mean, bool final)
    {
        var direction = (mean - _previousMean).Sign;
        // For nonzero a, a/(a+sigma) equals one iff sigma is exactly zero.
        // Population variance is zero iff every value in its window is equal.
        var risingConstant = _rises.Next(direction > 0 ? mean : default, final);
        var fallingConstant = _falls.Next(direction < 0 ? mean : default, final);
        var up = mean.Sign != 0 && risingConstant;
        var down = mean.Sign != 0 && fallingConstant;
        var value = _previousUp && !up ? 1d : _previousDown && !down ? -1d : 0;
        if (final) { _previousMean = mean; _previousUp = up; _previousDown = down; }
        return value;
    }
    internal double Next(double price, bool final)
    { StreamingInputValidation.Finite(price, nameof(price)); return FromMean(_mean.Next(Number.Of(price), final), final); }
    internal static double[] Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new TopsBottomsWindow(kind, length); var result = new double[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        { for (var i = 0; i < result.Length; i++) result[i] = window.Next(prices[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                var mean = ComponentAverage.Take(prices.ToArray(), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, prices).ToArray();
                for (var i = 0; i < result.Length; i++) result[i] = window.FromMean(Number.Of(mean[i]), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return result;
    }
    private sealed class ConstantWindow
    {
        private readonly int _length;
        private readonly LinkedList<Number> _history = new();
        private int _changes;
        internal ConstantWindow(int length) => _length = Math.Max(1, length);
        internal bool Next(Number value, bool final)
        {
            var changes = _changes;
            var full = _history.Count == _length;
            if (full && _history.First!.Next is { } second && (_history.First.Value - second.Value).Sign != 0) changes--;
            var retained = _history.Count - (full ? 1 : 0);
            if (retained > 0 && (_history.Last!.Value - value).Sign != 0) changes++;
            var constant = retained + 1 < _length || changes == 0;
            if (final) { if (full) _history.RemoveFirst(); _history.AddLast(value); _changes = changes; }
            return constant;
        }
        internal void Reset() { _history.Clear(); _changes = 0; }
    }
    internal void Reset() { _mean.Reset(); _rises.Reset(); _falls.Reset(); _previousMean = default; _previousUp = _previousDown = false; }
    public void Dispose() { Reset(); _mean.Dispose(); }
}
