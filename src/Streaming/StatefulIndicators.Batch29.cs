#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// Streaming form of <c>CalculateSchaffTrendCycleShk</c>: the double-smoothed Schaff Trend Cycle from
/// the "STC Indicator - A Better MACD [SHK]" script.
/// </summary>
[PrimaryOutput("Stc")]
public sealed class SchaffTrendCycleShkState : IStreamingIndicatorState, IDisposable
{
    private readonly SchaffCycleKernel _kernel;
    public SchaffTrendCycleShkState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 23, int slowLength = 50, int cycleLength = 10, int d1Length = 3, int d2Length = 3)
        => _kernel = new(maType, fastLength, slowLength, cycleLength, d1Length, d2Length);
    public IndicatorName Name => IndicatorName.SchaffTrendCycleShk;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var result = _kernel.Next(bar.Close, isFinal);
        return new(result.Stc, includeOutputs ? new Dictionary<string, double>
            { ["Stc"] = result.Stc, ["Macd"] = result.Macd } : null);
    }
    public void Dispose() => _kernel.Dispose();
}

/// <summary>
/// Streaming form of <c>CalculateUtBotAlerts</c>: an ATR trailing stop that ratchets in the direction
/// of the trend and flips when price closes through it.
/// </summary>
[PrimaryOutput("TrailingStop")]
public sealed class UtBotAlertsState : IStreamingIndicatorState, IDisposable
{
    private readonly UtBotWindow _window;
    public UtBotAlertsState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, double keyValue = 1) => _window = new(maType, length, keyValue);
    public IndicatorName Name => IndicatorName.UtBotAlerts;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Stop, includeOutputs ? new Dictionary<string, double> { { "TrailingStop", point.Stop }, { "Position", point.Position }, { "Buy", point.Buy }, { "Sell", point.Sell } } : null);
    }
    public void Dispose() => _window.Dispose();
}
