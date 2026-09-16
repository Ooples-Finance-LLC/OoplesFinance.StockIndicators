using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// How much of the bar's volume the buyers took against how much the sellers took, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateDemandIndex</c>. It reads one bar at a time, so a preview
/// bar costs it nothing beyond the count of bars it has seen.
/// </remarks>
[PrimaryOutput("Di")]
public sealed class DemandIndexState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private int _barIndex;

    public DemandIndexState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DemandIndex;

    public void Reset()
    {
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double demandIndex = 0;
        if (_barIndex >= 1)
        {
            var range = bar.High - bar.Low;
            var buyingPressure = value - bar.Low;
            var sellingPressure = bar.High - value;
            var buyingPercent = range != 0 ? buyingPressure / range : 0;
            var sellingPercent = range != 0 ? sellingPressure / range : 0;
            var buyVolume = bar.Volume * buyingPercent;
            var sellVolume = bar.Volume * sellingPercent;
            demandIndex = sellVolume != 0 ? (buyVolume / sellVolume) - 1 : 0;
        }

        if (isFinal)
        {
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Di", demandIndex } };
        }

        return new StreamingIndicatorStateResult(demandIndex, outputs);
    }
}

/// <summary>
/// Elder's traffic light, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateElderImpulseSystem</c>. Its average and the three that
/// make up the convergence and divergence histogram all come from the same <c>EmaState</c> the batch path's
/// exponential average is built on, so the verdict cannot differ between the engines.
/// </remarks>
[PrimaryOutput("Eis")]
public sealed class ElderImpulseSystemState : IStreamingIndicatorState
{
    private readonly EmaState _trendEma;
    private readonly EmaState _fastEma;
    private readonly EmaState _slowEma;
    private readonly EmaState _signalEma;
    private readonly StreamingInputResolver _input;
    private double _prevTrendEma;
    private double _prevHistogram;
    private int _barIndex;

    public ElderImpulseSystemState(int length = 13, int macdFastLength = 12, int macdSlowLength = 26, int macdSignalLength = 9)
    {
        _trendEma = new EmaState(Math.Max(1, length));
        _fastEma = new EmaState(Math.Max(1, macdFastLength));
        _slowEma = new EmaState(Math.Max(1, macdSlowLength));
        _signalEma = new EmaState(Math.Max(1, macdSignalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ElderImpulseSystem;

    public void Reset()
    {
        _trendEma.Reset();
        _fastEma.Reset();
        _slowEma.Reset();
        _signalEma.Reset();
        _prevTrendEma = 0;
        _prevHistogram = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var trendEma = _trendEma.GetNext(value, isFinal);
        var macdLine = _fastEma.GetNext(value, isFinal) - _slowEma.GetNext(value, isFinal);
        var histogram = macdLine - _signalEma.GetNext(macdLine, isFinal);

        double impulse = 0;
        if (_barIndex >= 1)
        {
            var emaRising = trendEma > _prevTrendEma;
            var histogramRising = histogram > _prevHistogram;
            impulse = emaRising && histogramRising ? 1 : !emaRising && !histogramRising ? -1 : 0;
        }

        if (isFinal)
        {
            _prevTrendEma = trendEma;
            _prevHistogram = histogram;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Eis", impulse } };
        }

        return new StreamingIndicatorStateResult(impulse, outputs);
    }
}

/// <summary>
/// The balance of the window's rises against its falls, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateSimplePriceZone</c>. The window keeps one value more than
/// its length, because the change that leaves the window is measured against the value before it.
/// </remarks>
[PrimaryOutput("Spz")]
public sealed class SimplePriceZoneState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private double _sumUp;
    private double _sumDown;
    private int _barIndex;

    public SimplePriceZoneState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SimplePriceZone;

    public void Reset()
    {
        _window.Clear();
        _sumUp = 0;
        _sumDown = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double zone = 0;
        var sumUp = _sumUp;
        var sumDown = _sumDown;
        if (_barIndex >= 1)
        {
            var change = value - _window[_window.Count - 1];
            sumUp += change > 0 ? change : 0;
            sumDown += change < 0 ? -change : 0;

            // A change enters the sums only from the second bar, so the one leaving is the change into
            // the bar _length back, which exists only once the window holds the bar before it too.
            if (_barIndex >= _length + 1)
            {
                var prevChange = _window[1] - _window[0];
                sumUp -= prevChange > 0 ? prevChange : 0;
                sumDown -= prevChange < 0 ? -prevChange : 0;
            }

            var total = sumUp + sumDown;
            zone = total != 0 ? 100 * (sumUp - sumDown) / total : 0;
        }

        if (isFinal)
        {
            _sumUp = sumUp;
            _sumDown = sumDown;
            _window.TryAdd(value, out _);
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Spz", zone } };
        }

        return new StreamingIndicatorStateResult(zone, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// Wilder's swing index of each bar against the bar before it, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateSwingIndex</c>, computing the index with the same
/// <c>WilderSwingIndex</c> the batch engine uses.
/// </remarks>
[PrimaryOutput("Si")]
public sealed class SwingIndexState : IStreamingIndicatorState
{
    private readonly double _limitMove;
    private readonly StreamingInputResolver _input;
    private double _prevOpen;
    private double _prevClose;
    private bool _hasPrev;

    public SwingIndexState(double limitMove = 0)
    {
        _limitMove = limitMove;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SwingIndex;

    public void Reset()
    {
        _prevOpen = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var swingIndex = _hasPrev
            ? WilderSwingIndex.Compute(bar.Open, bar.High, bar.Low, value, _prevOpen, _prevClose, _limitMove)
            : 0;

        if (isFinal)
        {
            _prevOpen = bar.Open;
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Si", swingIndex } };
        }

        return new StreamingIndicatorStateResult(swingIndex, outputs);
    }
}

/// <summary>
/// A stop trailing the price by a multiple of the average true range, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolatilityStop</c>. It wraps
/// <see cref="AverageTrueRangeState"/> so the range it trails by is the one the batch engine averages, and
/// it keeps the trend it is following between bars.
/// </remarks>
[PrimaryOutput("Vs")]
public sealed class VolatilityStopState : IStreamingIndicatorState, IDisposable
{
    private readonly double _multiplier;
    private readonly AverageTrueRangeState _averageTrueRange;
    private readonly StreamingInputResolver _input;
    private double _prevStop;
    private bool _trendIsUp = true;
    private int _barIndex;

    public VolatilityStopState(int length = 14, double multiplier = 2)
    {
        _multiplier = multiplier;
        _averageTrueRange = new AverageTrueRangeState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolatilityStop;

    public void Reset()
    {
        _averageTrueRange.Reset();
        _prevStop = 0;
        _trendIsUp = true;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var atr = _averageTrueRange.Update(bar, isFinal, includeOutputs: false).Value;

        double stop;
        var trendIsUp = _trendIsUp;
        if (_barIndex == 0)
        {
            stop = value;
        }
        else
        {
            var band = atr * _multiplier;
            if (trendIsUp)
            {
                if (value < _prevStop)
                {
                    trendIsUp = false;
                    stop = value + band;
                }
                else
                {
                    stop = Math.Max(_prevStop, value - band);
                }
            }
            else
            {
                if (value > _prevStop)
                {
                    trendIsUp = true;
                    stop = value - band;
                }
                else
                {
                    stop = Math.Min(_prevStop, value + band);
                }
            }
        }

        if (isFinal)
        {
            _prevStop = stop;
            _trendIsUp = trendIsUp;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vs", stop } };
        }

        return new StreamingIndicatorStateResult(stop, outputs);
    }

    public void Dispose()
    {
        _averageTrueRange.Dispose();
    }
}

/// <summary>
/// The gap between a short and a long exponential average of volume, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeMomentumOscillator</c>. Both averages start at the
/// first bar's volume and smooth from there, which is not how <c>EmaState</c> warms up, so the smoothing is
/// kept here rather than borrowed.
/// </remarks>
[PrimaryOutput("Vmo")]
public sealed class VolumeMomentumOscillatorState : IStreamingIndicatorState
{
    private readonly double _shortK;
    private readonly double _longK;
    private readonly StreamingInputResolver _input;
    private double _shortEma;
    private double _longEma;
    private int _barIndex;

    public VolumeMomentumOscillatorState(int shortLength = 5, int longLength = 20)
    {
        _shortK = 2.0 / (Math.Max(1, shortLength) + 1);
        _longK = 2.0 / (Math.Max(1, longLength) + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeMomentumOscillator;

    public void Reset()
    {
        _shortEma = 0;
        _longEma = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var volume = bar.Volume;

        double oscillator = 0;
        var shortEma = _barIndex == 0 ? volume : (volume * _shortK) + (_shortEma * (1 - _shortK));
        var longEma = _barIndex == 0 ? volume : (volume * _longK) + (_longEma * (1 - _longK));
        if (_barIndex >= 1)
        {
            oscillator = longEma != 0 ? (shortEma - longEma) / longEma * 100 : 0;
        }

        if (isFinal)
        {
            _shortEma = shortEma;
            _longEma = longEma;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vmo", oscillator } };
        }

        return new StreamingIndicatorStateResult(oscillator, outputs);
    }
}

/// <summary>
/// The share of recent volume that belonged to rising bars, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateVolumeZoneOscillator</c>. The first bar has no bar before
/// it to have risen against, so it contributes no signed volume, as the batch engine gives it none.
/// </remarks>
[PrimaryOutput("Vzo")]
public sealed class VolumeZoneOscillatorState : IStreamingIndicatorState
{
    private readonly EmaState _signedVolume;
    private readonly EmaState _totalVolume;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VolumeZoneOscillatorState(int length = 14)
    {
        _signedVolume = new EmaState(Math.Max(1, length));
        _totalVolume = new EmaState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeZoneOscillator;

    public void Reset()
    {
        _signedVolume.Reset();
        _totalVolume.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var signed = _hasPrev ? (value > _prevValue ? bar.Volume : -bar.Volume) : 0;

        var signedEma = _signedVolume.GetNext(signed, isFinal);
        var totalEma = _totalVolume.GetNext(bar.Volume, isFinal);
        var oscillator = totalEma != 0 ? signedEma / totalEma * 100 : 0;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Vzo", oscillator } };
        }

        return new StreamingIndicatorStateResult(oscillator, outputs);
    }
}

/// <summary>
/// An exponential average whose smoothing widens on the bars that moved, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateTrueRangeAdjustedExponentialMovingAverage</c>. The average
/// of the true range comes from the same <c>EmaState</c> the batch path uses, and the first bar starts the
/// average at its own price.
/// </remarks>
[PrimaryOutput("Trema")]
public sealed class TrueRangeAdjustedExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly double _baseAlpha;
    private readonly double _mult;
    private readonly EmaState _averageTrueRange;
    private readonly StreamingInputResolver _input;
    private double _value;
    private double _prevValue;
    private int _barIndex;

    public TrueRangeAdjustedExponentialMovingAverageState(int length = 14, double mult = 1.5)
    {
        var safeLength = Math.Max(1, length);
        _baseAlpha = 2.0 / (safeLength + 1);
        _mult = mult;
        _averageTrueRange = new EmaState(safeLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrueRangeAdjustedExponentialMovingAverage;

    public void Reset()
    {
        _averageTrueRange.Reset();
        _value = 0;
        _prevValue = 0;
        _barIndex = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var previous = _barIndex >= 1 ? _prevValue : value;
        var highLow = bar.High - bar.Low;
        var highClose = Math.Abs(bar.High - previous);
        var lowClose = Math.Abs(bar.Low - previous);
        var trueRange = Math.Max(highLow, Math.Max(highClose, lowClose));

        var averageTrueRange = _averageTrueRange.GetNext(trueRange, isFinal);
        var ratio = averageTrueRange != 0 ? trueRange / averageTrueRange : 1;
        var adjustedAlpha = _baseAlpha * Math.Min(ratio * _mult, 2);
        var trema = _barIndex == 0 ? value : _value + (adjustedAlpha * (value - _value));

        if (isFinal)
        {
            _value = trema;
            _prevValue = value;
            _barIndex++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Trema", trema } };
        }

        return new StreamingIndicatorStateResult(trema, outputs);
    }
}
