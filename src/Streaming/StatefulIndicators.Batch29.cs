#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// Streaming form of <c>CalculateSchaffTrendCycleShk</c>: the double-smoothed Schaff Trend Cycle from
/// the "STC Indicator - A Better MACD [SHK]" script.
/// </summary>
public sealed class SchaffTrendCycleShkState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastEma;
    private readonly IMovingAverageSmoother _slowEma;
    private readonly RollingWindowMax _macdMax;
    private readonly RollingWindowMin _macdMin;
    private readonly RollingWindowMax _fastDMax;
    private readonly RollingWindowMin _fastDMin;
    private readonly StreamingInputResolver _input;
    private readonly double _d1Alpha;
    private readonly double _d2Alpha;
    private double _prevFastK;
    private double _prevFastD;
    private double _prevSlowK;
    private double _prevStc;
    private bool _hasPrev;

    public SchaffTrendCycleShkState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 23, int slowLength = 50, int cycleLength = 10, int d1Length = 3, int d2Length = 3,
        InputName inputName = InputName.Close)
        : this(maType, fastLength, slowLength, cycleLength, d1Length, d2Length,
            new StreamingInputResolver(inputName, null))
    {
    }

    private SchaffTrendCycleShkState(MovingAvgType maType, int fastLength, int slowLength, int cycleLength,
        int d1Length, int d2Length, StreamingInputResolver input)
    {
        var cycle = Math.Max(1, cycleLength);
        _fastEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _macdMax = new RollingWindowMax(cycle);
        _macdMin = new RollingWindowMin(cycle);
        _fastDMax = new RollingWindowMax(cycle);
        _fastDMin = new RollingWindowMin(cycle);
        _d1Alpha = (double)2 / (Math.Max(1, d1Length) + 1);
        _d2Alpha = (double)2 / (Math.Max(1, d2Length) + 1);
        _input = input;
    }

    public IndicatorName Name => IndicatorName.SchaffTrendCycleShk;

    public void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _macdMax.Reset();
        _macdMin.Reset();
        _fastDMax.Reset();
        _fastDMin.Reset();
        _prevFastK = 0;
        _prevFastD = 0;
        _prevSlowK = 0;
        _prevStc = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var macd = _fastEma.Next(value, isFinal) - _slowEma.Next(value, isFinal);

        // First stochastic pass, over the MACD line.
        var macdHigh = isFinal ? _macdMax.Add(macd, out _) : _macdMax.Preview(macd, out _);
        var macdLow = isFinal ? _macdMin.Add(macd, out _) : _macdMin.Preview(macd, out _);
        var macdRange = macdHigh - macdLow;

        // A flat window has no range to normalise against. Carry the previous reading forward rather
        // than snapping to zero, which is what the Pine nz() chain and the batch calculation both do.
        var fastK = macdRange > 0
            ? MathHelper.MinOrMax((macd - macdLow) / macdRange * 100, 100, 0)
            : _prevFastK;
        var fastD = _hasPrev ? _prevFastD + (_d1Alpha * (fastK - _prevFastD)) : fastK;

        // Second stochastic pass, over the smoothed result of the first. This pass is what separates
        // the SHK indicator from the single-pass SchaffTrendCycle already in the library.
        var fastDHigh = isFinal ? _fastDMax.Add(fastD, out _) : _fastDMax.Preview(fastD, out _);
        var fastDLow = isFinal ? _fastDMin.Add(fastD, out _) : _fastDMin.Preview(fastD, out _);
        var fastDRange = fastDHigh - fastDLow;

        var slowK = fastDRange > 0
            ? MathHelper.MinOrMax((fastD - fastDLow) / fastDRange * 100, 100, 0)
            : _prevSlowK;
        var stc = _hasPrev
            ? MathHelper.MinOrMax(_prevStc + (_d2Alpha * (slowK - _prevStc)), 100, 0)
            : MathHelper.MinOrMax(slowK, 100, 0);

        if (isFinal)
        {
            _prevFastK = fastK;
            _prevFastD = fastD;
            _prevSlowK = slowK;
            _prevStc = stc;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Stc", stc },
                { "Macd", macd }
            };
        }

        return new StreamingIndicatorStateResult(stc, outputs);
    }

    public void Dispose()
    {
        _fastEma.Dispose();
        _slowEma.Dispose();
        _macdMax.Dispose();
        _macdMin.Dispose();
        _fastDMax.Dispose();
        _fastDMin.Dispose();
    }
}

/// <summary>
/// Streaming form of <c>CalculateUtBotAlerts</c>: an ATR trailing stop that ratchets in the direction
/// of the trend and flips when price closes through it.
/// </summary>
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
        double keyValue = 1, InputName inputName = InputName.Close)
        : this(maType, length, keyValue, new StreamingInputResolver(inputName, null))
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
