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
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _keyValue;
    private double _prevValue;
    private double _prevStop;
    private double _prevPosition;
    private bool _hasPrev;

    public UtBotAlertsState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10,
        double keyValue = 1)
        : this(maType, length, keyValue, new StreamingInputResolver(InputName.Close, null))
    {
    }

    private UtBotAlertsState(MovingAvgType maType, int length, double keyValue, StreamingInputResolver input)
    {
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _keyValue = keyValue;
        _input = input;
    }

    public IndicatorName Name => IndicatorName.UtBotAlerts;

    public void Reset()
    {
        _atrSmoother.Reset();
        _prevValue = 0;
        _prevStop = 0;
        _prevPosition = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        // The first bar has no previous close. Seeding it with 0 would make the true range the bar's
        // price rather than its range, which is exactly what the batch true-range helper avoids by
        // reusing the bar's own close.
        var prevValue = _hasPrev ? _prevValue : value;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var nLoss = _keyValue * atr;
        var prevStop = _hasPrev ? _prevStop : 0;

        double trailingStop;
        if (value > prevStop && prevValue > prevStop)
        {
            // Still above the stop: raise it, never lower it.
            trailingStop = Math.Max(prevStop, value - nLoss);
        }
        else if (value < prevStop && prevValue < prevStop)
        {
            // Still below the stop: lower it, never raise it.
            trailingStop = Math.Min(prevStop, value + nLoss);
        }
        else
        {
            // Price crossed the stop, so it flips to the other side of price.
            trailingStop = value > prevStop ? value - nLoss : value + nLoss;
        }

        double position;
        if (prevValue < prevStop && value > prevStop)
        {
            position = 1;
        }
        else if (prevValue > prevStop && value < prevStop)
        {
            position = -1;
        }
        else
        {
            position = _hasPrev ? _prevPosition : 0;
        }

        var buy = _hasPrev && prevValue <= prevStop && value > trailingStop ? 1d : 0d;
        var sell = _hasPrev && prevValue >= prevStop && value < trailingStop ? 1d : 0d;

        if (isFinal)
        {
            _prevValue = value;
            _prevStop = trailingStop;
            _prevPosition = position;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "TrailingStop", trailingStop },
                { "Position", position },
                { "Buy", buy },
                { "Sell", sell }
            };
        }

        return new StreamingIndicatorStateResult(trailingStop, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
    }
}
