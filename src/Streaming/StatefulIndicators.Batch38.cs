using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// How recently the window's highest high occurred, as a percentage, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateAroonUp</c>. The window is scanned from its oldest bar
/// forward, so where the high is equalled more than once the most recent occurrence wins, as it does in the
/// batch engine.
/// </remarks>
public sealed class AroonUpState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _highs;
    private readonly StreamingInputResolver _input;

    public AroonUpState(int length = 25)
    {
        _length = Math.Max(1, length);
        _highs = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AroonUp;

    public void Reset()
    {
        _highs.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);

        double aroonUp = 0;
        if (_highs.Count >= _length)
        {
            // The window is the last _length bars and this one, counted from the oldest at position zero to
            // this bar at _length, so the position of the highest is what the batch engine's
            // length - (i - highestIndex) comes to.
            var start = _highs.Count - _length;
            var highestPosition = 0;
            var highestValue = double.MinValue;
            for (var i = start; i < _highs.Count; i++)
            {
                if (_highs[i] >= highestValue)
                {
                    highestValue = _highs[i];
                    highestPosition = i - start;
                }
            }

            if (bar.High >= highestValue)
            {
                highestPosition = _length;
            }

            aroonUp = 100.0 * highestPosition / _length;
        }

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "AroonUp", aroonUp } };
        }

        return new StreamingIndicatorStateResult(aroonUp, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
    }
}

/// <summary>
/// How recently the window's lowest low occurred, as a percentage, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateAroonDown</c>, and the mirror of
/// <see cref="AroonUpState"/>.
/// </remarks>
public sealed class AroonDownState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;

    public AroonDownState(int length = 25)
    {
        _length = Math.Max(1, length);
        _lows = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AroonDown;

    public void Reset()
    {
        _lows.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);

        double aroonDown = 0;
        if (_lows.Count >= _length)
        {
            var start = _lows.Count - _length;
            var lowestPosition = 0;
            var lowestValue = double.MaxValue;
            for (var i = start; i < _lows.Count; i++)
            {
                if (_lows[i] <= lowestValue)
                {
                    lowestValue = _lows[i];
                    lowestPosition = i - start;
                }
            }

            if (bar.Low <= lowestValue)
            {
                lowestPosition = _length;
            }

            aroonDown = 100.0 * lowestPosition / _length;
        }

        if (isFinal)
        {
            _lows.TryAdd(bar.Low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "AroonDown", aroonDown } };
        }

        return new StreamingIndicatorStateResult(aroonDown, outputs);
    }

    public void Dispose()
    {
        _lows.Dispose();
    }
}

/// <summary>
/// How wide a channel a multiple of the average true range either side of the price would be, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateAtrChannelWidth</c>. It wraps
/// <see cref="AverageTrueRangeState"/> rather than averaging a range of its own, so the range it doubles is
/// the one the batch engine averages.
/// </remarks>
public sealed class AtrChannelWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly double _multiplier;
    private readonly AverageTrueRangeState _averageTrueRange;
    private readonly StreamingInputResolver _input;

    public AtrChannelWidthState(int length = 14, double multiplier = 2)
    {
        _multiplier = multiplier;
        _averageTrueRange = new AverageTrueRangeState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AtrChannelWidth;

    public void Reset()
    {
        _averageTrueRange.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var atr = _averageTrueRange.Update(bar, isFinal, includeOutputs: false).Value;
        var width = 2 * _multiplier * atr;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Acw", width } };
        }

        return new StreamingIndicatorStateResult(width, outputs);
    }

    public void Dispose()
    {
        _averageTrueRange.Dispose();
    }
}

/// <summary>
/// The distance between a Keltner channel's bands as a percentage of its middle, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateKeltnerChannelWidth</c>. Its middle comes from the same
/// <c>EmaState</c> the batch path's exponential average is built on, and its range from
/// <see cref="AverageTrueRangeState"/>, so neither can drift from the batch engine.
/// </remarks>
public sealed class KeltnerChannelWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly double _multiplier;
    private readonly EmaState _ema;
    private readonly AverageTrueRangeState _averageTrueRange;
    private readonly StreamingInputResolver _input;

    public KeltnerChannelWidthState(int length = 20, double multiplier = 2)
    {
        _multiplier = multiplier;
        _ema = new EmaState(Math.Max(1, length));
        _averageTrueRange = new AverageTrueRangeState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.KeltnerChannelWidth;

    public void Reset()
    {
        _ema.Reset();
        _averageTrueRange.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.GetNext(value, isFinal);
        var atr = _averageTrueRange.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = ema + (_multiplier * atr);
        var lower = ema - (_multiplier * atr);
        var width = ema != 0 ? (upper - lower) / ema * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Kcw", width } };
        }

        return new StreamingIndicatorStateResult(width, outputs);
    }

    public void Dispose()
    {
        _averageTrueRange.Dispose();
    }
}
