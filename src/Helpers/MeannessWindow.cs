using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MeannessWindow : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _ordered;
    private readonly NoiseEliminationTechnologyEngine? _net;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal MeannessWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length); _values = new(_length); _ordered = new double[_length];
        if (kind == MovingAvgType.EhlersNoiseEliminationTechnology) _net = new(_length);
        else if (StrengthWindow.Supports(kind)) _average = new(kind, _length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(kind, _length);
    }
    internal (double Line, double Smoothed) Next(double value, bool commit, double? customerSmoothed = null)
    {
        var first = _values.Count == _length ? 1 : 0;
        var count = _values.Count - first + 1;
        for (var i = first; i < _values.Count; i++) _ordered[i - first] = _values[i];
        _ordered[count - 1] = value; Array.Sort(_ordered, 0, count);
        var middle = new ExactMeanAccumulator(); middle.Add(_ordered[(count - 1) / 2]); middle.Add(_ordered[count / 2]);
        var median = middle.Mean(2); var outward = 0;
        for (var lag = 0; lag < _length - 1; lag++)
        {
            var current = lag == 0 ? value : lag <= _values.Count ? _values[_values.Count - lag] : 0;
            var previous = lag < _values.Count ? _values[_values.Count - lag - 1] : 0;
            if (current > median && current > previous || current < median && current < previous) outward++;
        }
        var line = _length == 1 ? 0 : 100d * outward / (_length - 1);
        var smoothed = customerSmoothed ?? (_net is not null ? _net.Next(line, commit)
            : _average is not null ? _average.Next(new(line), commit).Publish() : _fallback!.Next(line, commit));
        if (commit) _values.TryAdd(value, out _);
        return (line, smoothed);
    }
    internal void Reset() { _values.Clear(); _net?.Reset(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _values.Dispose(); _net?.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
