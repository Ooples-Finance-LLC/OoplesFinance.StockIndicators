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
    private readonly ExactPopulationWindow _deviation;
    private double _previous;
    private int _index;
    private ExactMeanAccumulator _upSeed, _downSeed;
    private RocBankValue _up, _down;
    public RelativeVolatilityIndexCore(int length, int stdDevLength) { _length = Math.Max(1, length); _deviation = new(Math.Max(1, stdDevLength)); }
    public void Reset() { _deviation.Reset(); _previous = 0; _index = 0; _upSeed = default; _downSeed = default; _up = default; _down = default; }
    public double Next(double value, bool isFinal)
    {
        var deviation = _deviation.Next(value, isFinal); var upSeed = _upSeed; var downSeed = _downSeed; var up = _up; var down = _down;
        if (_index > 0)
        {
            var u = value > _previous ? deviation : 0; var d = value < _previous ? deviation : 0;
            if (_index <= _length)
            {
                upSeed.Add(u); downSeed.Add(d);
                if (_index == _length) { up = RocBankValue.Round(upSeed, count: _length); down = RocBankValue.Round(downSeed, count: _length); }
            }
            else
            {
                var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, _length - 1L); numerator.Add(u); up = RocBankValue.Round(numerator, count: _length);
                numerator = new ExactMeanAccumulator(); down.AddTo(ref numerator, _length - 1L); numerator.Add(d); down = RocBankValue.Round(numerator, count: _length);
            }
        }
        var result = _index < _length ? 0 : up.Mantissa == 0 && down.Mantissa == 0 ? 50 : RelativeVolatilityWindow.Ratio(up, down);
        if (isFinal) { _previous = value; _index++; _upSeed = upSeed; _downSeed = downSeed; _up = up; _down = down; }
        return result;
    }
    public void Dispose() => _deviation.Dispose();
}

/// <summary>
/// The relative volatility index read from each bar's high, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRelativeVolatilityIndexHigh</c>.
/// </remarks>
[PrimaryOutput("RviHigh")]
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
        StreamingInputValidation.Validate(bar);
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
[PrimaryOutput("RviLow")]
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
        StreamingInputValidation.Validate(bar);
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
[PrimaryOutput("ChikouSpan")]
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
[PrimaryOutput("Swr")]
public sealed class SmoothedWilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly SmoothedWilliamsWindow _window;
    public SmoothedWilliamsRState(int length = 14, int smoothLength = 3) => _window = new(length, smoothLength);
    public IndicatorName Name => IndicatorName.SmoothedWilliamsR;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Swr", value } } : null);
    }
    public void Dispose() => _window.Reset();
}

/// <summary>
/// The gap between a fast and a slow exponential average as a percentage of the slow one, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateNormalizedMacd</c>. Both averages start at the first
/// bar's value rather than warming up, and that first bar, having no change behind it, publishes zero.
/// </remarks>
[PrimaryOutput("NormalizedMacd")]
public sealed class NormalizedMacdState : IStreamingIndicatorState
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly StreamingInputResolver _input;
    private double _fastEma;
    private double _slowEma;
    private int _barIndex;

    public NormalizedMacdState(int fastLength = 12, int slowLength = 26)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
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
        var fastEma = _barIndex == 0 ? value : RoundedSeededEma.Next(value, _fastEma, _fastLength);
        var slowEma = _barIndex == 0 ? value : RoundedSeededEma.Next(value, _slowEma, _slowLength);
        if (_barIndex >= 1)
        {
            macd = RoundedPercentageChange.Of(fastEma, slowEma);
        }

        if (isFinal)
        {
            _fastEma = fastEma;
            _slowEma = slowEma;
            _barIndex = 1;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "NormalizedMacd", macd } };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }
}
