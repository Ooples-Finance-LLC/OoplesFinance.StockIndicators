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
[PrimaryOutput("Variance")]
public sealed class VarianceState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactVarianceWindow _window;
    private readonly StreamingInputResolver _input;

    public VarianceState(int length = 20)
    {
        _window = new ExactVarianceWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Variance;
    public void Reset() => _window.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var variance = _window.Next(value, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double> { { "Variance", variance } } : null;
        return new StreamingIndicatorStateResult(variance, outputs);
    }

    public void Dispose() => _window.Dispose();
}
