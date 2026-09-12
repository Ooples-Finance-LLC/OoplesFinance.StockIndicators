using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The population variance of the input series over a rolling window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVariance</c>: the mean of the squared deviations from the
/// window's own mean, divided by the window length. The window holds the values already final, so a preview
/// bar is measured against them without joining them, and publishes zero until the window fills.
/// </remarks>
public sealed class VarianceState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public VarianceState(int length = 20)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Variance;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double variance = 0;
        if (_window.Count + 1 >= _length)
        {
            // The bar being measured is the newest of the window's values, so only the newest _length - 1 of
            // those already held take part in it.
            var start = _window.Count - (_length - 1);
            var sum = value;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            var mean = sum / _length;
            var currentDiff = value - mean;
            variance = currentDiff * currentDiff;
            for (var i = start; i < _window.Count; i++)
            {
                var diff = _window[i] - mean;
                variance += diff * diff;
            }

            variance /= _length;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Variance", variance }
            };
        }

        return new StreamingIndicatorStateResult(variance, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
