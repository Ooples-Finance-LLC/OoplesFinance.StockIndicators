using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The relative volatility index of one series, which the high and the low readings share.
/// </summary>
/// <remarks>
/// The state machine of <c>Calculations.CalculateRelativeVolatilityIndexHigh</c> and its low twin, kept in
/// one place so the two readings cannot drift apart: the deviation is sorted into the bars that rose and
/// the bars that fell, summed while the window fills, averaged once at the length, and smoothed Wilder's
/// way after that.
/// </remarks>
internal sealed class RelativeVolatilityIndexCore : IDisposable
{
    private readonly int _length;
    private readonly int _stdDevLength;
    private readonly double _k;
    private readonly PooledRingBuffer<double> _window;
    private double _prevValue;
    private double _upSum;
    private double _downSum;
    private double _upEma;
    private double _downEma;
    private int _barIndex;

    public RelativeVolatilityIndexCore(int length, int stdDevLength)
    {
        _length = Math.Max(1, length);
        _stdDevLength = Math.Max(1, stdDevLength);
        _k = 1.0 / _length;
        _window = new PooledRingBuffer<double>(_stdDevLength);
    }

    public void Reset()
    {
        _window.Clear();
        _prevValue = 0;
        _upSum = 0;
        _downSum = 0;
        _upEma = 0;
        _downEma = 0;
        _barIndex = 0;
    }

    public double Next(double value, bool isFinal)
    {
        double stdDev = 0;
        if (_window.Count + 1 >= _stdDevLength)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _window.Count - (_stdDevLength - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += value;

            var mean = sum / _stdDevLength;
            double variance = 0;
            for (var i = start; i < _window.Count; i++)
            {
                var diff = _window[i] - mean;
                variance += diff * diff;
            }

            var currentDiff = value - mean;
            variance += currentDiff * currentDiff;
            stdDev = Sqrt(variance / _stdDevLength);
        }

        double rvi = 0;
        if (_barIndex >= 1)
        {
            var change = value - _prevValue;
            var upMove = change > 0 ? stdDev : 0;
            var downMove = change < 0 ? stdDev : 0;

            if (_barIndex < _length)
            {
                var upSum = _upSum + upMove;
                var downSum = _downSum + downMove;
                if (isFinal)
                {
                    _upSum = upSum;
                    _downSum = downSum;
                }
            }
            else if (_barIndex == _length)
            {
                var upSum = _upSum + upMove;
                var downSum = _downSum + downMove;
                var upEma = upSum / _length;
                var downEma = downSum / _length;
                rvi = upEma + downEma != 0 ? 100 * upEma / (upEma + downEma) : 50;
                if (isFinal)
                {
                    _upSum = upSum;
                    _downSum = downSum;
                    _upEma = upEma;
                    _downEma = downEma;
                }
            }
            else
            {
                var upEma = (upMove * _k) + (_upEma * (1 - _k));
                var downEma = (downMove * _k) + (_downEma * (1 - _k));
                rvi = upEma + downEma != 0 ? 100 * upEma / (upEma + downEma) : 50;
                if (isFinal)
                {
                    _upEma = upEma;
                    _downEma = downEma;
                }
            }
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _prevValue = value;
            _barIndex++;
        }

        return rvi;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The relative volatility index read from each bar's high, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRelativeVolatilityIndexHigh</c>.
/// </remarks>
public sealed class RelativeVolatilityIndexHighState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityIndexCore _core;
    private readonly StreamingInputResolver _input;

    public RelativeVolatilityIndexHighState(int length = 14, int stdDevLength = 10)
    {
        _core = new RelativeVolatilityIndexCore(length, stdDevLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeVolatilityIndexHigh;

    public void Reset()
    {
        _core.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var rvi = _core.Next(bar.High, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "RviHigh", rvi } };
        }

        return new StreamingIndicatorStateResult(rvi, outputs);
    }

    public void Dispose()
    {
        _core.Dispose();
    }
}

/// <summary>
/// The relative volatility index read from each bar's low, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRelativeVolatilityIndexLow</c>, and the mirror of
/// <see cref="RelativeVolatilityIndexHighState"/>.
/// </remarks>
public sealed class RelativeVolatilityIndexLowState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityIndexCore _core;
    private readonly StreamingInputResolver _input;

    public RelativeVolatilityIndexLowState(int length = 14, int stdDevLength = 10)
    {
        _core = new RelativeVolatilityIndexCore(length, stdDevLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeVolatilityIndexLow;

    public void Reset()
    {
        _core.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var rvi = _core.Next(bar.Low, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "RviLow", rvi } };
        }

        return new StreamingIndicatorStateResult(rvi, outputs);
    }

    public void Dispose()
    {
        _core.Dispose();
    }
}

/// <summary>
/// The Ichimoku lagging span, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateIchimokuChikouSpan</c>: the close itself, published at the
/// bar that carries it, with the backward shift left to whatever draws the chart.
/// </remarks>
public sealed class IchimokuChikouSpanState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public IchimokuChikouSpanState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.IchimokuChikouSpan;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var chikouSpan = _input.GetValue(bar);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "ChikouSpan", chikouSpan } };
        }

        return new StreamingIndicatorStateResult(chikouSpan, outputs);
    }
}

/// <summary>
/// Williams %R smoothed exponentially, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateSmoothedWilliamsR</c>. Both the bars before the window
/// fills and a window with no range read the midpoint of minus fifty, as the batch engine reads them.
/// </remarks>
public sealed class SmoothedWilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;
    private double _prevSmoothed;
    private int _barIndex;

    public SmoothedWilliamsRState(int length = 14, int smoothLength = 3)
    {
        _length = Math.Max(1, length);
        _k = 2.0 / (Math.Max(1, smoothLength) + 1);
        _highs = new PooledRingBuffer<double>(_length);
        _lows = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SmoothedWilliamsR;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _prevSmoothed = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double rawWilliamsR;
        if (_highs.Count + 1 < _length)
        {
            rawWilliamsR = -50;
        }
        else
        {
            var start = _highs.Count - (_length - 1);
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            for (var i = start; i < _highs.Count; i++)
            {
                if (_highs[i] > highestHigh)
                {
                    highestHigh = _highs[i];
                }

                if (_lows[i] < lowestLow)
                {
                    lowestLow = _lows[i];
                }
            }

            if (bar.High > highestHigh)
            {
                highestHigh = bar.High;
            }

            if (bar.Low < lowestLow)
            {
                lowestLow = bar.Low;
            }

            rawWilliamsR = highestHigh != lowestLow
                ? (highestHigh - value) / (highestHigh - lowestLow) * -100
                : -50;
        }

        var smoothed = _barIndex == 0 ? rawWilliamsR : (rawWilliamsR * _k) + (_prevSmoothed * (1 - _k));

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevSmoothed = smoothed;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Swr", smoothed } };
        }

        return new StreamingIndicatorStateResult(smoothed, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

/// <summary>
/// The gap between a fast and a slow exponential average as a percentage of the slow one, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateNormalizedMacd</c>. Both averages start at the first
/// bar's value rather than warming up, and that first bar, having no change behind it, publishes zero.
/// </remarks>
public sealed class NormalizedMacdState : IStreamingIndicatorState
{
    private readonly double _fastK;
    private readonly double _slowK;
    private readonly StreamingInputResolver _input;
    private double _fastEma;
    private double _slowEma;
    private int _barIndex;

    public NormalizedMacdState(int fastLength = 12, int slowLength = 26)
    {
        _fastK = 2.0 / (Math.Max(1, fastLength) + 1);
        _slowK = 2.0 / (Math.Max(1, slowLength) + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NormalizedMacd;

    public void Reset()
    {
        _fastEma = 0;
        _slowEma = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double macd = 0;
        var fastEma = _barIndex == 0 ? value : (value * _fastK) + (_fastEma * (1 - _fastK));
        var slowEma = _barIndex == 0 ? value : (value * _slowK) + (_slowEma * (1 - _slowK));
        if (_barIndex >= 1)
        {
            macd = slowEma != 0 ? (fastEma - slowEma) / slowEma * 100 : 0;
        }

        if (isFinal)
        {
            _fastEma = fastEma;
            _slowEma = slowEma;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "NormalizedMacd", macd } };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }
}
