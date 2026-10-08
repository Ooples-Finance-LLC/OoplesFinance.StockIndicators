using Average = OoplesFinance.StockIndicators.Helpers.UnroundedMovingAverage;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TStepLeastSquaresWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _priceMean, _stepMean;
    private readonly Queue<Number> _prices = new(), _moves = new();
    private readonly Queue<(Number Step, Number Price)> _pairs = new();
    private Number _previousPrice, _previousStep, _travel, _distanceTotal;
    private Number _sx, _sy, _sxx, _sxy, _previousComparison;
    private long _count; private bool _hasComparison;
    internal TStepLeastSquaresWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _priceMean = new(kind, _length); _stepMean = new(kind, _length); }
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    private (Number Step, Number Slope) Fit(Number price, bool final)
    {
        var move = _count == 0 ? default : Abs(price - _previousPrice);
        var travel = _travel + move - (_moves.Count == _length ? _moves.Peek() : default);
        var efficiency = _prices.Count == _length && travel.Sign > 0 ? Abs(price - _prices.Peek()).Divide(travel) : default;
        var previous = _count == 0 ? price : _previousStep; var distance = Abs(price - previous);
        var total = _distanceTotal + distance; var threshold = total.Divide(_count + 1) * (Number.Of(2) - efficiency);
        var step = (distance - threshold).Sign > 0 ? price : previous;
        var sx = _sx + step; var sy = _sy + price; var sxx = _sxx + step * step; var sxy = _sxy + step * price;
        var full = _pairs.Count == _length;
        if (full) { var old = _pairs.Peek(); sx -= old.Step; sy -= old.Price; sxx -= old.Step * old.Step; sxy -= old.Step * old.Price; }
        var size = full ? _length : _pairs.Count + 1; var variance = sxx.Times(size) - sx * sx;
        // Correlation * price deviation / step deviation cancels to covariance / step variance.
        var slope = size < _length || variance.Sign == 0 ? default : (sxy.Times(size) - sx * sy).Divide(variance);
        if (final)
        {
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_moves.Count == _length) _moves.Dequeue(); _moves.Enqueue(move);
            if (full) _pairs.Dequeue(); _pairs.Enqueue((step, price));
            _previousPrice = price; _previousStep = step; _travel = travel; _distanceTotal = total;
            _sx = sx; _sy = sy; _sxx = sxx; _sxy = sxy; _count++;
        }
        return (step, slope);
    }
    private (double Line, Signal Trade) Finish(Number price, Number step, Number slope, Number mean, Number stepMean, bool final)
    {
        var line = mean + slope * (step - stepMean); var comparison = price - line;
        var previous = _hasComparison ? _previousComparison : default(Number) - price; var acceleration = comparison - previous;
        var trade = comparison.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : comparison.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousComparison = comparison; _hasComparison = true; }
        return (line.Publish(), trade);
    }
    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price); var fit = Fit(value, final);
        return Finish(value, fit.Step, fit.Slope, _priceMean.Next(value, final), _stepMean.Next(fit.Step, final), final);
    }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length)
    {
        length = Math.Max(1, length); var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var source = prices.Select(Number.Of).ToArray();
        Number[] Mean(Number[] values)
        {
            var custom = ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length);
            if (custom is not null) return Enumerable.Range(0, values.Length).Select(i => i < custom.Count ? Number.Of(custom[i]) : default).ToArray();
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        var mean = Mean(source); using var window = new TStepLeastSquaresWindow(kind, length);
        var fits = source.Select(v => window.Fit(v, true)).ToArray(); var stepMean = Mean(fits.Select(v => v.Step).ToArray());
        var line = new double[source.Length]; var trades = new Signal[source.Length];
        for (var i = 0; i < source.Length; i++) (line[i], trades[i]) = window.Finish(source[i], fits[i].Step, fits[i].Slope, mean[i], stepMean[i], true);
        return (line, trades);
    }
    internal void Reset()
    {
        _prices.Clear(); _moves.Clear(); _pairs.Clear(); _priceMean.Reset(); _stepMean.Reset();
        _previousPrice = _previousStep = _travel = _distanceTotal = _sx = _sy = _sxx = _sxy = _previousComparison = default;
        _count = 0; _hasComparison = false;
    }
    public void Dispose() { Reset(); _priceMean.Dispose(); _stepMean.Dispose(); }
}
