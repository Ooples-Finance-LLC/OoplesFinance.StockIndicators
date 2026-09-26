using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Zscore")]
public sealed class ZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardizedScoreWindow _score;
    private readonly StrengthAverage? _exact;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly StreamingInputResolver _input;
    private readonly bool _simple;
    public ZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _score = new StandardizedScoreWindow(length, false);
        if (StrengthWindow.Supports(maType)) _exact = new StrengthAverage(maType, length);
        else _fallback = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _simple = maType == MovingAvgType.SimpleMovingAverage;
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.ZScore;
    public void Reset() { _score.Reset(); _exact?.Reset(); _fallback?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mean = _exact is null ? _fallback!.Next(value, isFinal) : _exact.Next(new StrengthValue(value), isFinal).Mantissa;
        var score = _score.Next(value, mean, _simple, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Zscore", score } } : null;
        return new StreamingIndicatorStateResult(score, outputs);
    }
    public void Dispose() { _score.Dispose(); _exact?.Dispose(); _fallback?.Dispose(); }
}

[PrimaryOutput("Zmbti")]
public sealed class ZweigMarketBreadthIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advances;
    private readonly RollingWindowSum _declines;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ZweigMarketBreadthIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10)
    {
        var resolved = Math.Max(1, length);
        _advances = new RollingWindowSum(resolved);
        _declines = new RollingWindowSum(resolved);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
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
