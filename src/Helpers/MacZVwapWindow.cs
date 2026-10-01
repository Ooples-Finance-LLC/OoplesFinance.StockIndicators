using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
using Average = OoplesFinance.StockIndicators.Helpers.MacZWindow.Average;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MacZVwapWindow : IDisposable
{
    private readonly int _volumeLength, _deviationLength;
    private readonly double _gamma;
    private readonly Average _fast, _slow, _signal;
    private readonly Queue<(Number Product, Number Volume)> _volumes = new();
    private readonly Queue<Number> _residuals = new();
    private readonly Queue<double> _prices = new();
    private Number _products, _volume, _energy, _l0, _l1, _l2, _l3, _previousHistogram;
    private BigInteger _sum, _squares;
    private bool _hasPrevious, _constant;
    private Number _raw1, _raw2, _raw3;
    internal MacZVwapWindow(MovingAvgType kind, int fast, int slow, int signal, int volumeLength, int deviationLength, double gamma)
    {
        StreamingInputValidation.Finite(gamma, nameof(gamma)); _gamma = gamma;
        _volumeLength = Math.Max(1, volumeLength); _deviationLength = Math.Max(1, deviationLength);
        _fast = new(kind, fast); _slow = new(kind, slow); _signal = new(kind, signal);
    }
    private Number Standardize(double price, double volume, Number fast, Number slow, bool final)
    {
        var value = Number.Of(price); var weight = Number.Of(volume); var product = value * weight;
        var products = _products + product; var weights = _volume + weight;
        if (_volumes.Count == _volumeLength) { var old = _volumes.Peek(); products -= old.Product; weights -= old.Volume; }
        var mean = weights.Sign == 0 ? default : products.Divide(weights);
        var residual = value - mean; var square = residual * residual; var energy = _energy + square;
        if (_residuals.Count == _volumeLength) energy -= _residuals.Peek();
        var zscore = _residuals.Count < _volumeLength - 1 ? default : residual.OverRoot(energy.Divide(_volumeLength));
        var units = ExactVarianceWindow.Units(price); var sum = _sum + units; var squares = _squares + units * units;
        if (_prices.Count == _deviationLength) { var old = ExactVarianceWindow.Units(_prices.Peek()); sum -= old; squares -= old * old; }
        var radicand = _prices.Count < _deviationLength - 1 ? BigInteger.Zero : _deviationLength * squares - sum * sum;
        var combined = zscore + (fast - slow).OverDeviation(radicand, _deviationLength);
        if (final)
        {
            if (_volumes.Count == _volumeLength) _volumes.Dequeue(); _volumes.Enqueue((product, weight));
            if (_residuals.Count == _volumeLength) _residuals.Dequeue(); _residuals.Enqueue(square);
            if (_prices.Count == _deviationLength) _prices.Dequeue(); _prices.Enqueue(price);
            _products = products; _volume = weights; _energy = energy; _sum = sum; _squares = squares;
        }
        return combined;
    }
    internal Number Filter(Number raw, bool final)
    {
        var gamma = Number.Of(_gamma); var complement = Number.Of(1) - gamma;
        if (!_hasPrevious)
        {
            if (final)
            {
                _l0 = raw * complement * complement * complement; _l1 = raw * complement * complement;
                _l2 = raw * complement; _l3 = raw; _raw1 = _raw2 = _raw3 = raw;
                _hasPrevious = true; _constant = true;
            }
            return raw;
        }
        if (_constant && (raw - _raw1).Sign == 0) return raw;
        // Equivalent Laguerre transfer: symmetric cubic numerator followed by
        // four poles at gamma. Cancel alternating inputs before the recursive
        // stages, so a decaying output cannot acquire a stage-rounding floor.
        var scale = (complement * complement).Divide(6);
        var a = scale * (Number.Of(1) - gamma + gamma * gamma);
        var b = scale * (Number.Of(2) - gamma.Times(5) + (gamma * gamma).Times(2));
        var feed = a * (raw + _raw3) + b * (_raw1 + _raw2);
        var l0 = feed + gamma * _l0;
        var l1 = l0 + gamma * _l1;
        var l2 = l1 + gamma * _l2;
        var line = l2 + gamma * _l3;
        if (final)
        {
            var exact = _gamma == 0 || _gamma == 1;
            _l0 = exact ? l0 : l0.Round(160); _l1 = exact ? l1 : l1.Round(160);
            _l2 = exact ? l2 : l2.Round(160); _l3 = exact ? line : line.Round(160);
            _raw3 = _raw2; _raw2 = _raw1; _raw1 = raw; _constant = false;
        }
        return line;
    }
    private (double Line, double SignalLine, double Histogram, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var histogram = line - signal; var change = histogram - _previousHistogram;
        var trade = histogram.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : histogram.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : histogram.Sign > 0 ? Signal.Buy : histogram.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousHistogram = histogram;
        return (line.Publish(), signal.Publish(), histogram.Publish(), trade);
    }
    internal (double Line, double SignalLine, double Histogram, Signal Trade) Next(double price, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(volume, nameof(volume)); var value = Number.Of(price);
        var raw = Standardize(price, volume, _fast.Next(value, final), _slow.Next(value, final), final);
        var line = Filter(raw, final); return Finish(line, _signal.Next(line, final), final);
    }
    internal static (double[] Line, double[] SignalLine, double[] Histogram, Signal[] Trades) Calculate(StockData data, List<double> prices,
        MovingAvgType kind, int fast, int slow, int signal, int volumeLength, int deviationLength, double gamma, bool callbacks)
    {
        StreamingInputValidation.Finite(gamma, nameof(gamma));
        if (data.Volumes.Count < prices.Count) throw new ArgumentException("Volume input is shorter than the selected prices.", nameof(data));
        for (var i = 0; i < prices.Count; i++) { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(data.Volumes[i], nameof(data.Volumes)); }
        var caller = data.CaptureInputSeries();
        Number[] Mean(Number[] source, int period)
        {
            period = Math.Max(1, period); var published = callbacks || !StrengthWindow.Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, period) : null;
            if (custom is not null) return custom.Select(Number.Of).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, period, published!.ToList()).Select(Number.Of).ToArray();
            using var average = new Average(kind, period); return source.Select(v => average.Next(v, true)).ToArray();
        }
        try
        {
            var source = prices.Select(Number.Of).ToArray(); var fastValues = Mean(source, fast); var slowValues = Mean(source, slow);
            using var window = new MacZVwapWindow(kind, fast, slow, signal, volumeLength, deviationLength, gamma);
            var line = prices.Select((v, i) => window.Filter(window.Standardize(v, data.Volumes[i], fastValues[i], slowValues[i], true), true)).ToArray();
            var signals = Mean(line, signal); var result = line.Select((v, i) => window.Finish(v, signals[i], true)).ToArray();
            return (result.Select(v => v.Line).ToArray(), result.Select(v => v.SignalLine).ToArray(), result.Select(v => v.Histogram).ToArray(), result.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset()
    { _fast.Reset(); _slow.Reset(); _signal.Reset(); _volumes.Clear(); _residuals.Clear(); _prices.Clear(); _products = _volume = _energy = _l0 = _l1 = _l2 = _l3 = _previousHistogram = default; _sum = _squares = default; _hasPrevious = _constant = false; _raw1 = _raw2 = _raw3 = default; }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }
}
