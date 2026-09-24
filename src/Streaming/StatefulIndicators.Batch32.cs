using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The average of the last few daily ranges, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateAverageDayRange</c>. It sums high-minus-low
/// differences exactly, rounds only the final mean, and publishes zero until the window fills.
/// </remarks>
[PrimaryOutput("Adr")]
public sealed class AverageDayRangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private ExactMeanAccumulator _sum;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;

    public AverageDayRangeState(int length = 14)
    {
        _length = Math.Max(1, length);
        _highs = new PooledRingBuffer<double>(_length);
        _lows = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AverageDayRange;

    public void Reset()
    {
        _sum = default;
        _highs.Clear();
        _lows.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var sum = _sum;
        sum.Add(bar.High); sum.Add(bar.Low, -1);
        if (_highs.Count == _length) { sum.Add(_highs[0], -1); sum.Add(_lows[0]); }
        var adr = _highs.Count + 1 < _length ? 0 : sum.Mean(_length);
        if (isFinal)
        {
            _sum = sum;
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Adr", adr }
            };
        }

        return new StreamingIndicatorStateResult(adr, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

/// <summary>
/// Wilder's true range, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateTrueRange</c>. The close it measures against is the series
/// being streamed, so a caller's own values are honoured as the batch engine honours them.
/// </remarks>
[PrimaryOutput("TrueRange")]
public sealed class TrueRangeState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public TrueRangeState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrueRange;

    public void Reset()
    {
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var trueRange = _hasPrev
            ? Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - _prevClose), Math.Abs(bar.Low - _prevClose)))
            : bar.High - bar.Low;

        if (isFinal)
        {
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "TrueRange", trueRange }
            };
        }

        return new StreamingIndicatorStateResult(trueRange, outputs);
    }
}

/// <summary>
/// The range of each bar: its high less its low.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRange</c>. It looks at no other bar, so it keeps no state
/// and a preview bar costs it nothing.
/// </remarks>
[PrimaryOutput("Range")]
public sealed class RangeState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public RangeState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Range;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var range = bar.High - bar.Low;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Range", range }
            };
        }

        return new StreamingIndicatorStateResult(range, outputs);
    }
}

/// <summary>
/// The change in the input series over a few bars, as a fraction of the earlier value.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateSimpleReturns</c>. Until the window holds a value that far
/// back, and whenever that value is zero, it publishes zero.
/// </remarks>
[PrimaryOutput("Returns")]
public sealed class SimpleReturnsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public SimpleReturnsState(int length = 1)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SimpleReturns;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double returns = 0;
        if (_window.Count >= _length)
        {
            var prevValue = _window[0];
            returns = RoundedRangeRatio.Of(value, prevValue, prevValue);
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
                { "Returns", returns }
            };
        }

        return new StreamingIndicatorStateResult(returns, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The natural logarithm of the input series' ratio to itself a few bars back.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateLogReturns</c>. A bar with no value that far back, or with
/// a value of zero or less at either end, has no logarithm to take and publishes zero.
/// </remarks>
[PrimaryOutput("Returns")]
public sealed class LogReturnsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public LogReturnsState(int length = 1)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.LogReturns;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double returns = 0;
        if (_window.Count >= _length)
        {
            var prevValue = _window[0];
            returns = StableLogRatio.Of(value, prevValue);
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
                { "Returns", returns }
            };
        }

        return new StreamingIndicatorStateResult(returns, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
