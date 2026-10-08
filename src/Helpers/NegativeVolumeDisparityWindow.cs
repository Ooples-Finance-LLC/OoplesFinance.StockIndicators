using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class NegativeVolumeDisparityWindow : IDisposable
{
    private readonly VolumeIndexTotal _index = new();
    private readonly MacZWindow.Average _priceMean, _indexMean, _signalMean;
    private readonly Population _priceVariance, _indexVariance;
    private readonly Number _top, _bottom;
    private Number _previousLine;
    private int _previousState;
    internal NegativeVolumeDisparityWindow(MovingAvgType kind, int length, int signalLength, double top, double bottom)
    {
        StreamingInputValidation.Finite(top, nameof(top)); StreamingInputValidation.Finite(bottom, nameof(bottom));
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        _top = Number.Of(top); _bottom = Number.Of(bottom);
        _priceMean = new(kind, length); _indexMean = new(kind, length); _signalMean = new(kind, signalLength);
        _priceVariance = new(length); _indexVariance = new(length);
    }
    private static Number Extended(RocBankValue value)
        => Number.Of(value.Mantissa) * Number.Integer(BigInteger.One << value.UpperShift);
    // 1 + envelope position. Rationalize a subtractive numerator so exact
    // zeros and near-zero values survive; every remaining root sum is positive.
    private static Number Position(Number value, Number mean, Number variance)
    {
        if (variance.Sign == 0) return Number.Of(1);
        var delta = value - mean;
        if (delta.Sign >= 0) return Number.Of(1.5) + delta.OverRoot(variance).Divide(4);
        var normalizedSquare = (delta * delta).Divide(variance);
        var remainder = Number.Of(36) - normalizedSquare;
        if (remainder.Sign == 0) return default;
        // Work with the dimensionless root ratio, so proportional price/NVI
        // trajectories produce identical coordinates even at irrational roots.
        var rootRatio = (default(Number) - delta).OverRoot(variance);
        return remainder.Divide((Number.Of(6) + rootRatio).Times(4));
    }
    internal (Number Line, Number SignalLine, Signal Trade) Next(double price, double volume, bool final,
        double? externalPriceMean = null, double? externalIndexMean = null, double? externalSignalMean = null)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume));
        var current = Number.Of(price); var index = Extended(_index.Next(price, volume, final));
        var priceMean = externalPriceMean.HasValue ? Number.Of(externalPriceMean.Value) : _priceMean.Next(current, final);
        var indexMean = externalIndexMean.HasValue ? Number.Of(externalIndexMean.Value) : _indexMean.Next(index, final);
        var line = Standardize(current, index, priceMean, indexMean, final);
        var signal = externalSignalMean.HasValue ? Number.Of(externalSignalMean.Value) : _signalMean.Next(line, final);
        return Finish(line, signal, final);
    }
    private Number Standardize(Number current, Number index, Number priceMean, Number indexMean, bool final)
    {
        var pricePosition = Position(current, priceMean, _priceVariance.Next(current, final));
        var indexPosition = Position(index, indexMean, _indexVariance.Next(index, final));
        return indexPosition.Sign == 0 ? default : pricePosition.Divide(indexPosition);
    }
    private (Number Line, Number SignalLine, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var state = ((_previousLine - _bottom).Sign < 0 && (line - _bottom).Sign > 0) || (line - signal).Sign > 0 ? 1
            : ((_previousLine - _top).Sign > 0 && (line - _top).Sign < 0) || (line - _bottom).Sign < 0 ? -1 : _previousState;
        var trade = state > 0 && state > _previousState ? Signal.StrongBuy : state < 0 && state < _previousState ? Signal.StrongSell
            : state > 0 ? Signal.Buy : state < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousLine = line; _previousState = state; }
        return (line, signal, trade);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, List<double> prices,
        MovingAvgType kind, int length, int signalLength, double top, double bottom, bool callbacks, bool includeSignal = true)
    {
        StreamingInputValidation.Finite(top, nameof(top)); StreamingInputValidation.Finite(bottom, nameof(bottom));
        if (data.Volumes.Count < prices.Count) throw new ArgumentException("Volume input is shorter than selected prices.", nameof(data));
        for (var i = 0; i < prices.Count; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(data.Volumes[i], nameof(data.Volumes)); }
        var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] source, int period)
        {
            period = Math.Max(1, period);
            var published = callbacks || !StrengthWindow.Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, period) : null;
            if (custom is not null) return custom.Select(Number.Of).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, period, published!.ToList()).Select(Number.Of).ToArray();
            using var average = new MacZWindow.Average(kind, period); return source.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var source = prices.Select(Number.Of).ToArray(); var total = new VolumeIndexTotal();
            var indices = prices.Select((p, i) => Extended(total.Next(p, data.Volumes[i], true))).ToArray();
            // Preserve the existing fast-route component order: price, NVI,
            // then (only for the Signal output) the disparity itself.
            var priceMeans = Mean(source, length); var indexMeans = Mean(indices, length);
            using var window = new NegativeVolumeDisparityWindow(kind, length, signalLength, top, bottom);
            var line = source.Select((v, i) => window.Standardize(v, indices[i], priceMeans[i], indexMeans[i], true)).ToArray();
            if (!includeSignal) return (line.Select(v => v.Publish()).ToArray(), Array.Empty<double>(), Array.Empty<Signal>());
            var signal = Mean(line, signalLength); var points = line.Select((v, i) => window.Finish(v, signal[i], true)).ToArray();
            return (points.Select(v => v.Line.Publish()).ToArray(), points.Select(v => v.SignalLine.Publish()).ToArray(), points.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset()
    {
        _index.Reset(); _priceMean.Reset(); _indexMean.Reset(); _signalMean.Reset(); _priceVariance.Reset(); _indexVariance.Reset();
        _previousLine = default; _previousState = 0;
    }
    public void Dispose() { _priceMean.Dispose(); _indexMean.Dispose(); _signalMean.Dispose(); _priceVariance.Reset(); _indexVariance.Reset(); }
    private sealed class Population
    {
        private readonly int _length;
        private readonly Queue<Number> _history = new();
        private Number _sum, _squares;
        internal Population(int length) => _length = length;
        internal Number Next(Number current, bool final)
        {
            var sum = _sum + current; var squares = _squares + current * current;
            var full = _history.Count == _length;
            if (full) { var old = _history.Peek(); sum -= old; squares -= old * old; }
            var variance = _history.Count < _length - 1 ? default : (squares.Times(_length) - sum * sum).Divide((long)_length * _length);
            if (final) { if (full) _history.Dequeue(); _history.Enqueue(current); _sum = sum; _squares = squares; }
            return variance;
        }
        internal void Reset() { _history.Clear(); _sum = _squares = default; }
    }
}
