using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The highest high of a rolling window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateHighestHigh</c>. The window expands rather than warming
/// up, and a preview bar is measured against the window without joining it.
/// </remarks>
[PrimaryOutput("HighestHigh")]
public sealed class HighestHighState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _window;
    private readonly StreamingInputResolver _input;

    public HighestHighState(int length = 14)
    {
        _window = new RollingWindowMax(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.HighestHigh;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _window.Add(bar.High, out _) : _window.Preview(bar.High, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "HighestHigh", highest }
            };
        }

        return new StreamingIndicatorStateResult(highest, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The lowest low of a rolling window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateLowestLow</c>. The window expands rather than warming up,
/// and a preview bar is measured against the window without joining it.
/// </remarks>
[PrimaryOutput("LowestLow")]
public sealed class LowestLowState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMin _window;
    private readonly StreamingInputResolver _input;

    public LowestLowState(int length = 14)
    {
        _window = new RollingWindowMin(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.LowestLow;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var lowest = isFinal ? _window.Add(bar.Low, out _) : _window.Preview(bar.Low, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "LowestLow", lowest }
            };
        }

        return new StreamingIndicatorStateResult(lowest, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The largest value of a rolling window of the input series, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRollingMax</c>. Unlike the highest high, this measures the
/// series being streamed - the caller's own values when they supply them - not the bar's high.
/// </remarks>
[PrimaryOutput("RollingMax")]
public sealed class RollingMaxState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _window;
    private readonly StreamingInputResolver _input;

    public RollingMaxState(int length = 14)
    {
        _window = new RollingWindowMax(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RollingMax;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var max = isFinal ? _window.Add(value, out _) : _window.Preview(value, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "RollingMax", max }
            };
        }

        return new StreamingIndicatorStateResult(max, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The smallest value of a rolling window of the input series, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRollingMin</c>. Unlike the lowest low, this measures the
/// series being streamed - the caller's own values when they supply them - not the bar's low.
/// </remarks>
[PrimaryOutput("RollingMin")]
public sealed class RollingMinState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMin _window;
    private readonly StreamingInputResolver _input;

    public RollingMinState(int length = 14)
    {
        _window = new RollingWindowMin(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RollingMin;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var min = isFinal ? _window.Add(value, out _) : _window.Preview(value, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "RollingMin", min }
            };
        }

        return new StreamingIndicatorStateResult(min, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The running total of the input series, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateCumulativeSum</c>. It has no length and never warms up; a
/// preview bar is added to the total that would result without keeping it.
/// </remarks>
[PrimaryOutput("CumulativeSum")]
public sealed class CumulativeSumState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _sum;

    public CumulativeSumState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CumulativeSum;

    public void Reset()
    {
        _sum = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sum = _sum + value;
        if (isFinal)
        {
            _sum = sum;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "CumulativeSum", sum }
            };
        }

        return new StreamingIndicatorStateResult(sum, outputs);
    }
}
