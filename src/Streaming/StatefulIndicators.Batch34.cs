using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// How far the input series strays from its own regression line, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateStandardError</c>. It uses the same
/// exact centered moments as the batch path, without rounding fitted coefficients
/// before measuring the residuals.
/// </remarks>
[PrimaryOutput("StandardError")]
public sealed class StandardErrorState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactStandardErrorWindow _window;
    private readonly StreamingInputResolver _input;

    public StandardErrorState(int length = 14)
    {
        _window = new ExactStandardErrorWindow(length, true);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StandardError;
    public void Reset() => _window.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var result = _window.Next(value, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double> { { "StandardError", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose() => _window.Dispose();
}

/// <summary>
/// How precisely the window's mean is known, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateStandardErrorOfTheMean</c>: the window's standard
/// deviation over the root of its length.
/// </remarks>
[PrimaryOutput("Sem")]
public sealed class StandardErrorOfTheMeanState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactStandardErrorWindow _window;
    private readonly StreamingInputResolver _input;

    public StandardErrorOfTheMeanState(int length = 20)
    {
        _window = new ExactStandardErrorWindow(length, false);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StandardErrorOfTheMean;
    public void Reset() => _window.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var result = _window.Next(value, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double> { { "Sem", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose() => _window.Dispose();
}

/// <summary>
/// The bar's volume signed by its direction, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateNetVolume</c>. The direction is taken from the series
/// being streamed, so a caller's own values decide the sign as they do in the batch engine.
/// </remarks>
[PrimaryOutput("NetVolume")]
public sealed class NetVolumeState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public NetVolumeState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NetVolume;

    public void Reset()
    {
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double netVolume = 0;
        if (_hasPrev)
        {
            netVolume = value > _prevValue ? bar.Volume : value < _prevValue ? -bar.Volume : 0;
        }

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "NetVolume", netVolume } };
        }

        return new StreamingIndicatorStateResult(netVolume, outputs);
    }
}

/// <summary>
/// A running total of signed volume, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateCumulativeVolumeIndex</c>. It accumulates a change rather
/// than a level, so a market that stops moving leaves the total where it stands.
/// </remarks>
[PrimaryOutput("Cvi")]
public sealed class CumulativeVolumeIndexState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private ExactMeanAccumulator _cvi;
    private double _prevValue;
    private bool _hasPrev;

    public CumulativeVolumeIndexState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CumulativeVolumeIndex;

    public void Reset()
    {
        _cvi = default;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var total = _cvi;
        if (_hasPrev)
        {
            if (value > _prevValue)
            {
                total.Add(bar.Volume);
            }
            else if (value < _prevValue)
            {
                total.Add(bar.Volume, -1);
            }
        }

        if (isFinal)
        {
            _cvi = total;
            _prevValue = value;
            _hasPrev = true;
        }

        var cvi = total.Mean(1);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Cvi", cvi } };
        }

        return new StreamingIndicatorStateResult(cvi, outputs);
    }
}

/// <summary>
/// The bar's volume as a multiple of its recent average, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateNormalizedVolume</c>. It retains the exact window
/// sum and rounds only the final volume-to-average ratio, without rounding an intermediate mean.
/// </remarks>
[PrimaryOutput("NormalizedVolume")]
public sealed class NormalizedVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private ExactMeanAccumulator _sum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public NormalizedVolumeState(int length = 20)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NormalizedVolume;

    public void Reset()
    {
        _sum = default;
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;
        var sum = _sum;
        sum.Add(volume);
        if (_values.Count == _length) sum.Add(_values[0], -1);
        var numerator = new ExactMeanAccumulator();
        numerator.Add(volume, _length);
        var normalizedVolume = _values.Count + 1 < _length ? 0 : numerator.Ratio(sum);
        if (isFinal)
        {
            _sum = sum;
            _values.TryAdd(volume, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "NormalizedVolume", normalizedVolume } };
        }

        return new StreamingIndicatorStateResult(normalizedVolume, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

/// <summary>
/// The gap between a short and a long average of volume, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeOscillator</c>, as a percentage of the long average.
/// </remarks>
[PrimaryOutput("Vo")]
public sealed class VolumeOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly RoundedSimpleMovingAverageSmoother _fast;
    private readonly RoundedSimpleMovingAverageSmoother _slow;
    private StreamingInputResolver _input;

    public VolumeOscillatorState(int fastLength = 5, int slowLength = 20)
    {
        _fast = new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength));
        _slow = new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Volume, null);
    }

    public IndicatorName Name => IndicatorName.VolumeOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() => _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var volume = _input.GetValue(bar);
        var fastSma = _fast.Next(volume, isFinal);
        var slowSma = _slow.Next(volume, isFinal);
        var volumeOscillator = RoundedPercentageChange.Of(fastSma, slowSma);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vo", volumeOscillator } };
        }

        return new StreamingIndicatorStateResult(volumeOscillator, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
    }
}

/// <summary>
/// The change in volume over a few bars, in shares, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeMomentum</c>.
/// </remarks>
[PrimaryOutput("VolumeMomentum")]
public sealed class VolumeMomentumState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public VolumeMomentumState(int length = 10)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeMomentum;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumeMomentum = _window.Count >= _length ? volume - _window[0] : 0;

        if (isFinal)
        {
            _window.TryAdd(volume, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "VolumeMomentum", volumeMomentum } };
        }

        return new StreamingIndicatorStateResult(volumeMomentum, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The change in volume over a few bars as a percentage, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeRateOfChange</c>. A bar whose earlier volume was
/// zero has no proportion to take and publishes zero.
/// </remarks>
[PrimaryOutput("Vroc")]
public sealed class VolumeRateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public VolumeRateOfChangeState(int length = 12)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeRateOfChange;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;

        double vroc = 0;
        if (_window.Count >= _length)
        {
            var prevVolume = _window[0];
            vroc = RoundedPercentageChange.Of(volume, prevVolume);
        }

        if (isFinal)
        {
            _window.TryAdd(volume, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vroc", vroc } };
        }

        return new StreamingIndicatorStateResult(vroc, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
