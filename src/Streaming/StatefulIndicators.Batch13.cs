#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Fsrsi")]
public sealed class FastandSlowRelativeStrengthIndexOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FastSlowCompositeWindow _window;
    public FastandSlowRelativeStrengthIndexOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 6) => _window = new(true, maType, length1, length2, length3, length4);
    public IndicatorName Name => IndicatorName.FastandSlowRelativeStrengthIndexOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Fsrsi", point.Line }, { "Signal", point.SignalLine } } : null); }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fsst")]
public sealed class FastandSlowStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FastSlowCompositeWindow _window;
    public FastandSlowStochasticOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 9) => _window = new(false, maType, length1, length2, length3, length4);
    public IndicatorName Name => IndicatorName.FastandSlowStochasticOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Fsst", point.Line }, { "Signal", point.SignalLine } } : null); }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fsdo")]
public sealed class FastSlowDegreeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FastSlowDegreeWindow _window;
    public FastSlowDegreeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 100, int fastLength = 3, int slowLength = 2, int signalLength = 14)
        => _window = new(maType, length, fastLength, slowLength, signalLength);
    public IndicatorName Name => IndicatorName.FastSlowDegreeOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Fsdo", point.Line }, { "Signal", point.SignalLine }, { "Histogram", point.Histogram } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fgi")]
public sealed class FearAndGreedIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FearGreedWindow _window;
    public FearAndGreedIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int fastLength = 10, int slowLength = 30, int smoothLength = 2)
        => _window = new(maType, fastLength, slowLength, smoothLength);
    public IndicatorName Name => IndicatorName.FearAndGreedIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Fgi", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Pivot")]
public sealed class FibonacciPivotPointsState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyPivotLevels _daily = new(false, fibonacci: true);
    public FibonacciPivotPointsState() { }
    public IndicatorName Name => IndicatorName.FibonacciPivotPoints;
    public void Reset() => _daily.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var levels = _daily.Next(bar.StartTime, bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            var values = new Dictionary<string, double>(levels.Length);
            for (var i = 0; i < levels.Length; i++) values[_daily.Keys[i]] = levels[i];
            outputs = values;
        }
        return new StreamingIndicatorStateResult(levels[0], outputs);
    }
}

[PrimaryOutput("UpperBand")]
public sealed class FibonacciRetraceState : IStreamingIndicatorState, IDisposable
{
    private readonly FibonacciRetraceWindow _window;
    public FibonacciRetraceState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 15, int length2 = 50, double factor = .382)
        => _window = new(maType, length1, length2, factor);
    public IndicatorName Name => IndicatorName.FibonacciRetrace;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Upper, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fwma")]
public sealed class FibonacciWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly FibonacciWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public FibonacciWeightedMovingAverageState(int length = 14)
    {
        _mean = new FibonacciWindowMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FibonacciWeightedMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Fwma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

[PrimaryOutput("Fve")]
public sealed class FiniteVolumeElementsState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly FiniteVolumeWindow _window;
    private readonly StreamingInputResolver _input;
    public FiniteVolumeElementsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 22, double factor = .3)
    { _window = new(maType, length, factor); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.FiniteVolumeElements;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, price, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Fve", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fo")]
public sealed class FireflyOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FireflyWindow _window;
    public FireflyOscillatorState(MovingAvgType maType = MovingAvgType.ZeroLagExponentialMovingAverage, int length = 10, int smoothLength = 3)
        => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.FireflyOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Fo", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Flsma")]
public sealed class FisherLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly FisherLeastSquaresWindow _window;
    public FisherLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.FisherLeastSquaresMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Flsma", point.Line } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ftso")]
public sealed class FisherTransformStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FisherStochasticWindow _window;
    public FisherTransformStochasticOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 2, int stochLength = 30, int smoothLength = 5)
        => _window = new(maType, length, stochLength, smoothLength);
    public IndicatorName Name => IndicatorName.FisherTransformStochasticOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ftso", point.Line } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class FlaggingBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly FlaggingBandWindow _window;
    public FlaggingBandsState(int length = 14) { _window = new(length); }
    public IndicatorName Name => IndicatorName.FlaggingBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower }, { "TrailingStop", point.Stop } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Pivot")]
public sealed class FloorPivotPointsState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyPivotLevels _daily = new(false, floor: true);
    private readonly int _primarySlot;
    public FloorPivotPointsState() : this(0) { }
    internal FloorPivotPointsState(int primarySlot) => _primarySlot = primarySlot;
    public IndicatorName Name => IndicatorName.FloorPivotPoints;
    public void Reset() => _daily.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var levels = _daily.Next(bar.StartTime, bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            var values = new Dictionary<string, double>(levels.Length);
            for (var i = 0; i < levels.Length; i++) values[_daily.Keys[i]] = levels[i];
            outputs = values;
        }
        return new StreamingIndicatorStateResult(levels[_primarySlot], outputs);
    }
}

[PrimaryOutput("Frsi")]
public sealed class FoldedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RsiState _rsi;
    private readonly FoldedRsiSum _absSum;
    private readonly StrengthAverage? _exactSignal;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public FoldedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _absSum = new FoldedRsiSum(_length);
        if (StrengthWindow.Supports(maType)) _exactSignal = new StrengthAverage(maType, _length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FoldedRelativeStrengthIndex;

    public void Reset()
    {
        _exactSignal?.Reset();
        _rsi.Reset();
        _absSum.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var frsi = _absSum.Next(rsi, isFinal);
        var signal = _exactSignal is null ? _signalSmoother.Next(frsi, isFinal) : _exactSignal.Next(new StrengthValue(frsi), isFinal).Mantissa;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Frsi", frsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(frsi, outputs);
    }

    public void Dispose()
    {
        _exactSignal?.Dispose();
        _rsi.Dispose();
        _absSum.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Fi")]
public sealed class ForceIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly ForceWindow _window;
    public ForceIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
        => _window = new ForceWindow(maType, length);
    public IndicatorName Name => IndicatorName.ForceIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Fi", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Fo")]
public sealed class ForecastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly OneBarReturnWindow _window;
    public ForecastOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 3) { _window = new(false, maType, length); }
    public IndicatorName Name => IndicatorName.ForecastOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "Fo", p.Value }, { "Signal", p.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

/// <summary>
/// The five-bar fractal test shared by <see cref="FractalChaosBandsState"/> and
/// <see cref="FractalChaosOscillatorState"/>.
/// </summary>
/// <remarks>
/// One copy rather than two. The oscillator's batch twin chains CalculateFractalChaosBands and reads that
/// indicator's bands, so in streaming the two states are the only places this test lives and they have to
/// agree - keeping a second copy is what lets them drift apart. See #202.
/// </remarks>
internal static class StreamingFractal
{
    /// <summary>
    /// Reports the fractals confirmed by <paramref name="bar"/>, or <see langword="null"/> where there is
    /// none. The centre is two bars back: its right-hand neighbours are the previous bar and this one, its
    /// left-hand neighbours are three and four bars back.
    /// </summary>
    /// <remarks>
    /// Both buffers hold only finalised bars, so offset N is N bars back from <paramref name="bar"/>.
    /// </remarks>
    public static void Find(
        PooledRingBuffer<double> highs,
        PooledRingBuffer<double> lows,
        OhlcvBar bar,
        out double? upFractal,
        out double? downFractal)
    {
        upFractal = null;
        downFractal = null;

        // Nothing is judged until four prior bars have been seen. An unfilled buffer reads as 0, and 0 is
        // below any positive price, so judging earlier would confirm a fractal whose left-hand neighbour
        // never happened - the same fabricated-value mistake fixed for the log-return windows in #205
        // and #209.
        if (highs.Count < 4 || lows.Count < 4)
        {
            return;
        }

        var prevHigh1 = EhlersStreamingWindow.GetOffsetValue(highs, 1);
        var prevHigh2 = EhlersStreamingWindow.GetOffsetValue(highs, 2);
        var prevHigh3 = EhlersStreamingWindow.GetOffsetValue(highs, 3);
        var prevHigh4 = EhlersStreamingWindow.GetOffsetValue(highs, 4);
        if (prevHigh1 < prevHigh2 && bar.High < prevHigh2 && prevHigh3 < prevHigh2 && prevHigh4 < prevHigh2)
        {
            upFractal = prevHigh2;
        }

        var prevLow1 = EhlersStreamingWindow.GetOffsetValue(lows, 1);
        var prevLow2 = EhlersStreamingWindow.GetOffsetValue(lows, 2);
        var prevLow3 = EhlersStreamingWindow.GetOffsetValue(lows, 3);
        var prevLow4 = EhlersStreamingWindow.GetOffsetValue(lows, 4);
        if (prevLow1 > prevLow2 && bar.Low > prevLow2 && prevLow3 > prevLow2 && prevLow4 > prevLow2)
        {
            downFractal = prevLow2;
        }
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class FractalChaosBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private double _prevUpper;
    private double _prevLower;

    public FractalChaosBandsState()
    {
        // Four, not three: a five-bar fractal centred two bars back reaches four bars back on its left,
        // and its right-hand neighbours are the previous bar and the current one. See #202.
        _highs = new PooledRingBuffer<double>(4);
        _lows = new PooledRingBuffer<double>(4);
    }

    public IndicatorName Name => IndicatorName.FractalChaosBands;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _prevUpper = 0;
        _prevLower = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        StreamingFractal.Find(_highs, _lows, bar, out var upFractal, out var downFractal);

        var upper = upFractal ?? _prevUpper;
        var lower = downFractal ?? _prevLower;
        var midpoint = new ExactMeanAccumulator(); midpoint.Add(upper); midpoint.Add(lower); var middle = midpoint.Mean(2);

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevUpper = upper;
            _prevLower = lower;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

[PrimaryOutput("Fco")]
public sealed class FractalChaosOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private double _prevUpper;
    private double _prevLower;

    public FractalChaosOscillatorState()
    {
        // Four, matching the bands this oscillator reports on. Its batch twin chains
        // CalculateFractalChaosBands and reads that indicator's bands, so this state is the only place the
        // oscillator's own fractal test lives - it has to move with them or the two engines disagree.
        // See #202.
        _highs = new PooledRingBuffer<double>(4);
        _lows = new PooledRingBuffer<double>(4);
    }

    public IndicatorName Name => IndicatorName.FractalChaosOscillator;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _prevUpper = 0;
        _prevLower = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        // Its batch twin chains CalculateFractalChaosBands and reads that indicator's bands, so this state
        // is the only place the oscillator's own fractal test lives - it shares one with the bands state
        // rather than keeping a second copy. See #202.
        StreamingFractal.Find(_highs, _lows, bar, out var upFractal, out var downFractal);

        var upper = upFractal ?? _prevUpper;
        var lower = downFractal ?? _prevLower;
        var fco = upper != _prevUpper ? 1 : lower != _prevLower ? -1 : 0;

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevUpper = upper;
            _prevLower = lower;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fco", fco }
            };
        }

        return new StreamingIndicatorStateResult(fco, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

[PrimaryOutput("Fom")]
public sealed class FreedomOfMovementState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private readonly RollingStandardDeviation _volumeStdDev;
    private readonly RollingWindowMax _aMoveMax;
    private readonly RollingWindowMin _aMoveMin;
    private readonly RollingWindowMax _relVolMax;
    private readonly RollingWindowMin _relVolMin;
    private readonly RollingWindowSum _vBymSum;
    private readonly RollingStandardDeviation _vBymStdDev;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevDpl;
    private bool _hasPrev;
    private bool _hasPrevDpl;
    private double _vBymValue;

    public FreedomOfMovementState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        _length = Math.Max(1, length);
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _volumeStdDev = new RollingStandardDeviation(_length);
        _aMoveMax = new RollingWindowMax(_length);
        _aMoveMin = new RollingWindowMin(_length);
        _relVolMax = new RollingWindowMax(_length);
        _relVolMin = new RollingWindowMin(_length);
        _vBymSum = new RollingWindowSum(_length);
        _vBymStdDev = new RollingStandardDeviation(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FreedomOfMovement;

    public void Reset()
    {
        _volumeSmoother.Reset();
        _volumeStdDev.Reset();
        _aMoveMax.Reset();
        _aMoveMin.Reset();
        _relVolMax.Reset();
        _relVolMin.Reset();
        _vBymSum.Reset();
        _vBymStdDev.Reset();
        _prevValue = 0;
        _prevDpl = 0;
        _hasPrev = false;
        _hasPrevDpl = false;
        _vBymValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var avgVolume = _volumeSmoother.Next(bar.Volume, isFinal);
        var sdVolume = _volumeStdDev.Next(bar.Volume, isFinal);
        var relVol = sdVolume != 0 ? (bar.Volume - avgVolume) / sdVolume : 0;

        var priceChg = _hasPrev ? value - prevValue : 0;
        var aMove = prevValue != 0 ? Math.Abs(priceChg / prevValue) : 0;
        var aMoveMax = isFinal ? _aMoveMax.Add(aMove, out _) : _aMoveMax.Preview(aMove, out _);
        var aMoveMin = isFinal ? _aMoveMin.Add(aMove, out _) : _aMoveMin.Preview(aMove, out _);
        var theMove = aMoveMax - aMoveMin != 0
            ? 1 + 9 * (aMove - aMoveMin) / (aMoveMax - aMoveMin)
            : 0;
        var relVolMax = isFinal ? _relVolMax.Add(relVol, out _) : _relVolMax.Preview(relVol, out _);
        var relVolMin = isFinal ? _relVolMin.Add(relVol, out _) : _relVolMin.Preview(relVol, out _);
        var theVol = relVolMax - relVolMin != 0
            ? 1 + 9 * (relVol - relVolMin) / (relVolMax - relVolMin)
            : 0;
        var vBym = theMove != 0 ? theVol / theMove : 0;
        var vBymSum = isFinal ? _vBymSum.Add(vBym, out var countAfter) : _vBymSum.Preview(vBym, out countAfter);
        var avf = countAfter > 0 ? vBymSum / countAfter : 0;

        _vBymValue = vBym;
        var sdf = _vBymStdDev.Next(_vBymValue, isFinal);
        var theFom = sdf != 0 ? (vBym - avf) / sdf : 0;
        var prevDpl = _hasPrevDpl ? _prevDpl : 0;
        var dpl = theFom >= 2 ? prevValue : _hasPrevDpl ? prevDpl : value;

        if (isFinal)
        {
            _prevValue = value;
            _prevDpl = dpl;
            _hasPrev = true;
            _hasPrevDpl = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fom", theFom },
                { "Dpl", dpl }
            };
        }

        return new StreamingIndicatorStateResult(theFom, outputs);
    }

    public void Dispose()
    {
        _volumeSmoother.Dispose();
        _volumeStdDev.Dispose();
        _aMoveMax.Dispose();
        _aMoveMin.Dispose();
        _relVolMax.Dispose();
        _relVolMin.Dispose();
        _vBymSum.Dispose();
        _vBymStdDev.Dispose();
    }
}

[PrimaryOutput("Close")]
public sealed class FunctionToCandlesState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsiC;
    private readonly RsiState _rsiO;
    private readonly RsiState _rsiH;
    private readonly RsiState _rsiL;

    public FunctionToCandlesState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        var resolved = Math.Max(1, length);
        _rsiC = new RsiState(maType, resolved);
        _rsiO = new RsiState(maType, resolved);
        _rsiH = new RsiState(maType, resolved);
        _rsiL = new RsiState(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.FunctionToCandles;

    public void Reset()
    {
        _rsiC.Reset();
        _rsiO.Reset();
        _rsiH.Reset();
        _rsiL.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var rsiC = _rsiC.Next(bar.Close, isFinal);
        var rsiO = _rsiO.Next(bar.Open, isFinal);
        var rsiH = _rsiH.Next(bar.High, isFinal);
        var rsiL = _rsiL.Next(bar.Low, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Close", rsiC },
                { "Open", rsiO },
                { "High", rsiH },
                { "Low", rsiL }
            };
        }

        return new StreamingIndicatorStateResult(rsiC, outputs);
    }

    public void Dispose()
    {
        _rsiC.Dispose();
        _rsiO.Dispose();
        _rsiH.Dispose();
        _rsiL.Dispose();
    }
}

[PrimaryOutput("FXSniper")]
public sealed class FXSniperIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly FxSniperWindow _window; private bool _readClose;
    public FXSniperIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int cciLength = 14, int t3Length = 5, double b = MathHelper.InversePhi)
        => _window = new(maType, cciLength, t3Length, b);
    public IndicatorName Name => IndicatorName.FXSniperIndicator;
    void ICustomInputConsumer.ReadCloseAsInput() => _readClose = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var price = _readClose ? bar.Close : CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close);
        var point = _window.Next(price, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "FXSniper", point.Line } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Glma")]
public sealed class GainLossMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly GainLossAverageWindow _window;
    public GainLossMovingAverageState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14, int signalLength = 7) => _window = new(maType, length, signalLength);
    public IndicatorName Name => IndicatorName.GainLossMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value.Value, includeOutputs ? new Dictionary<string, double> { { "Glma", value.Value }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ghla")]
public sealed class GannHiLoActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _highMa;
    private readonly IMovingAverageSmoother _lowMa;
    private readonly StreamingInputResolver _input;
    private double _prevHighMa;
    private double _prevLowMa;
    private double _prevGhla;
    private bool _hasPrev;

    public GannHiLoActivatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3)
    {
        _highMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _lowMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GannHiLoActivator;

    public void Reset()
    {
        _highMa.Reset();
        _lowMa.Reset();
        _prevHighMa = 0;
        _prevLowMa = 0;
        _prevGhla = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highMa = _highMa.Next(bar.High, isFinal);
        var lowMa = _lowMa.Next(bar.Low, isFinal);
        var prevHighMa = _hasPrev ? _prevHighMa : 0;
        var prevLowMa = _hasPrev ? _prevLowMa : 0;
        var prevGhla = _hasPrev ? _prevGhla : 0;
        var ghla = value > prevHighMa ? lowMa : value < prevLowMa ? highMa : prevGhla;

        if (isFinal)
        {
            _prevHighMa = highMa;
            _prevLowMa = lowMa;
            _prevGhla = ghla;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ghla", ghla }
            };
        }

        return new StreamingIndicatorStateResult(ghla, outputs);
    }

    public void Dispose()
    {
        _highMa.Dispose();
        _lowMa.Dispose();
    }
}

[PrimaryOutput("Gso")]
public sealed class GannSwingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private double _prevGso;
    private bool _hasPrev;

    public GannSwingOscillatorState(int length = 5)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highestValues = new PooledRingBuffer<double>(2);
        _lowestValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.GannSwingOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _prevGso = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest1 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 1);
        var prevHighest2 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 2);
        var prevLowest1 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 1);
        var prevLowest2 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 2);
        var prevGso = _hasPrev ? _prevGso : 0;
        var gso = prevHighest2 > prevHighest1 && highest > prevHighest1 ? 1
            : prevLowest2 < prevLowest1 && lowest < prevLowest1 ? -1
            : prevGso;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _prevGso = gso;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gso", gso }
            };
        }

        return new StreamingIndicatorStateResult(gso, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
    }
}

[PrimaryOutput("Gto")]
public sealed class GannTrendOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private double _prevGto;
    private bool _hasPrev;

    public GannTrendOscillatorState(int length = 3)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highestValues = new PooledRingBuffer<double>(2);
        _lowestValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.GannTrendOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _prevGto = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest1 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 1);
        var prevHighest2 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 2);
        var prevLowest1 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 1);
        var prevLowest2 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 2);
        var prevGto = _hasPrev ? _prevGto : 0;
        var gto = prevHighest2 > prevHighest1 && highest > prevHighest1 ? 1
            : prevLowest2 < prevLowest1 && lowest < prevLowest1 ? -1
            : prevGto;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _prevGto = gto;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gto", gto }
            };
        }

        return new StreamingIndicatorStateResult(gto, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
    }
}
