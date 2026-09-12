using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// How far the input series strays from its own regression line, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateStandardError</c>. It fits the line with the same
/// <c>RollingLeastSquares</c> the batch path fits it with, so the two agree rather than drifting apart as a
/// second implementation of the same regression would.
/// </remarks>
[PrimaryOutput("StandardError")]
public sealed class StandardErrorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingLeastSquares _regression;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public StandardErrorState(int length = 14)
    {
        _length = Math.Max(1, length);
        _regression = new RollingLeastSquares(_length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StandardError;

    public void Reset()
    {
        _regression.Reset();
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fit = _regression.Next(value, isFinal);

        double standardError = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window, and each
            // residual taken against the line where that bar sits. Against the line's endpoint instead,
            // a window lying exactly on a sloped line reports scatter where there is none.
            var start = _window.Count - (_length - 1);
            double sumSquaredDiff = 0;
            for (var i = start; i < _window.Count; i++)
            {
                var fitted = fit.Intercept + (fit.Slope * (i - start));
                var diff = _window[i] - fitted;
                sumSquaredDiff += diff * diff;
            }

            var currentFitted = fit.Intercept + (fit.Slope * (_length - 1));
            var currentDiff = value - currentFitted;
            sumSquaredDiff += currentDiff * currentDiff;

            standardError = Sqrt(sumSquaredDiff / _length);
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "StandardError", standardError } };
        }

        return new StreamingIndicatorStateResult(standardError, outputs);
    }

    public void Dispose()
    {
        _regression.Dispose();
        _window.Dispose();
    }
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
    private readonly double _sqrtLength;
    private readonly RollingStandardDeviation _stdDev;
    private readonly StreamingInputResolver _input;

    public StandardErrorOfTheMeanState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _sqrtLength = Sqrt(resolved);
        _stdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StandardErrorOfTheMean;

    public void Reset()
    {
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        // The window's population standard deviation, taken from the primitive the other deviation-based
        // states already share rather than summed again here: the same two passes over the window, oldest
        // value first with this bar last, and the same zero until the window fills.
        var stdDev = _stdDev.Next(value, isFinal);
        var standardError = stdDev / _sqrtLength;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Sem", standardError } };
        }

        return new StreamingIndicatorStateResult(standardError, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
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
    private double _cvi;
    private double _prevValue;
    private bool _hasPrev;

    public CumulativeVolumeIndexState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CumulativeVolumeIndex;

    public void Reset()
    {
        _cvi = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var cvi = _cvi;
        if (_hasPrev)
        {
            if (value > _prevValue)
            {
                cvi += bar.Volume;
            }
            else if (value < _prevValue)
            {
                cvi -= bar.Volume;
            }
        }

        if (isFinal)
        {
            _cvi = cvi;
            _prevValue = value;
            _hasPrev = true;
        }

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
/// The streaming twin of <c>Calculations.CalculateNormalizedVolume</c>. Its window sums the way
/// <c>MovingAverageCore.SimpleMovingAverage</c> sums, so the average the two engines divide by is the same.
/// </remarks>
[PrimaryOutput("NormalizedVolume")]
public sealed class NormalizedVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _sum;
    private readonly StreamingInputResolver _input;

    public NormalizedVolumeState(int length = 20)
    {
        _length = Math.Max(1, length);
        _sum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NormalizedVolume;

    public void Reset()
    {
        _sum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;
        var total = isFinal ? _sum.Add(volume, out var countAfter) : _sum.Preview(volume, out countAfter);
        var averageVolume = countAfter >= _length ? total / _length : 0;
        var normalizedVolume = averageVolume != 0 ? volume / averageVolume : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "NormalizedVolume", normalizedVolume } };
        }

        return new StreamingIndicatorStateResult(normalizedVolume, outputs);
    }

    public void Dispose()
    {
        _sum.Dispose();
    }
}

/// <summary>
/// The gap between a short and a long average of volume, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeOscillator</c>, as a percentage of the long average.
/// </remarks>
[PrimaryOutput("Vo")]
public sealed class VolumeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly RollingWindowSum _fastSum;
    private readonly RollingWindowSum _slowSum;
    private readonly StreamingInputResolver _input;

    public VolumeOscillatorState(int fastLength = 5, int slowLength = 20)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeOscillator;

    public void Reset()
    {
        _fastSum.Reset();
        _slowSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;
        var fastTotal = isFinal ? _fastSum.Add(volume, out var fastCount) : _fastSum.Preview(volume, out fastCount);
        var slowTotal = isFinal ? _slowSum.Add(volume, out var slowCount) : _slowSum.Preview(volume, out slowCount);

        var fastSma = fastCount >= _fastLength ? fastTotal / _fastLength : 0;
        var slowSma = slowCount >= _slowLength ? slowTotal / _slowLength : 0;
        var volumeOscillator = slowSma != 0 ? (fastSma - slowSma) / slowSma * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vo", volumeOscillator } };
        }

        return new StreamingIndicatorStateResult(volumeOscillator, outputs);
    }

    public void Dispose()
    {
        _fastSum.Dispose();
        _slowSum.Dispose();
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
            vroc = prevVolume != 0 ? (volume - prevVolume) / prevVolume * 100 : 0;
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
