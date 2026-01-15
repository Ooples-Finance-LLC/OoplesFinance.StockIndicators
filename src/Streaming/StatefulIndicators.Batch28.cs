using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class ZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanMa;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _inputValue;

    public ZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _meanMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _inputValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZScoreState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _meanMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _inputValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ZScore;

    public void Reset()
    {
        _meanMa.Reset();
        _stdDev.Reset();
        _inputValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _inputValue = value;
        var mean = _meanMa.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var zscore = stdDev != 0 ? (value - mean) / stdDev : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zscore", zscore }
            };
        }

        return new StreamingIndicatorStateResult(zscore, outputs);
    }

    public void Dispose()
    {
        _meanMa.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class ZweigMarketBreadthIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advances;
    private readonly RollingWindowSum _declines;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ZweigMarketBreadthIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _advances = new RollingWindowSum(resolved);
        _declines = new RollingWindowSum(resolved);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZweigMarketBreadthIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _advances = new RollingWindowSum(resolved);
        _declines = new RollingWindowSum(resolved);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ZweigMarketBreadthIndicator;

    public void Reset()
    {
        _advances.Reset();
        _declines.Reset();
        _smoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var advance = value > prevValue ? 1d : 0d;
        var decline = value < prevValue ? 1d : 0d;

        var advSum = isFinal ? _advances.Add(advance, out _) : _advances.Preview(advance, out _);
        var decSum = isFinal ? _declines.Add(decline, out _) : _declines.Preview(decline, out _);
        var denom = advSum + decSum;
        var advDiff = denom != 0 ? advSum / denom : 0;
        var zmbti = _smoother.Next(advDiff, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zmbti", zmbti }
            };
        }

        return new StreamingIndicatorStateResult(zmbti, outputs);
    }

    public void Dispose()
    {
        _advances.Dispose();
        _declines.Dispose();
        _smoother.Dispose();
    }
}
