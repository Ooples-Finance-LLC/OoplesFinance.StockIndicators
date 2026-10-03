using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Mi")]
public sealed class MarketFacilitationIndexState : IStreamingIndicatorState
{
    public IndicatorName Name => IndicatorName.MarketFacilitationIndex;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var mfi = RoundedRangeRatio.Of(bar.High, bar.Low, bar.Volume);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mi", mfi }
            };
        }

        return new StreamingIndicatorStateResult(mfi, outputs);
    }
}

[PrimaryOutput("Mmi")]
public sealed class MarketMeannessIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly MeannessWindow _window;
    public MarketMeannessIndexState(MovingAvgType maType = MovingAvgType.EhlersNoiseEliminationTechnology, int length = 100) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.MarketMeannessIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Mmi", value.Line }, { "MmiSmoothed", value.Smoothed } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mr")]
public sealed class MartinRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly MartinWindow _window;
    public MartinRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = .02)
        => _window = new MartinWindow(maType, length, bmk);
    public IndicatorName Name => IndicatorName.MartinRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Mr", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mi")]
public sealed class MassIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly MassIndexWindow _window;
    public MassIndexState(MovingAvgType maType=MovingAvgType.ExponentialMovingAverage,int length1=21,int length2=21,int length3=25,int signalLength=9)
        =>_window=new(maType,length1,length2,length3,signalLength);
    public IndicatorName Name=>IndicatorName.MassIndex;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.High,bar.Low,isFinal);
        return new(value.Value,includeOutputs?new Dictionary<string,double>{{"Mi",value.Value},{"Signal",value.Signal}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Mti")]
public sealed class MassThrustIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly MassThrustWindow _window;
    public MassThrustIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) => _window = new(false, maType, length);
    public IndicatorName Name => IndicatorName.MassThrustIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Mti", point.Value }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mto")]
public sealed class MassThrustOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly MassThrustWindow _window;
    public MassThrustOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) => _window = new(true, maType, length);
    public IndicatorName Name => IndicatorName.MassThrustOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Mto", point.Value }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mm")]
public sealed class MayerMultipleState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public MayerMultipleState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 200, double threshold = 2.4)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
        _ = threshold;
    }

    public IndicatorName Name => IndicatorName.MayerMultiple;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var mm = sma != 0 ? value / sma : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mm", mm }
            };
        }

        return new StreamingIndicatorStateResult(mm, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

[PrimaryOutput("Mo")]
public sealed class McClellanOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _decSum;
    private readonly MacdEngine _macd;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;
    private double _prevValue;
    private bool _hasPrev;

    public McClellanOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 19, int slowLength = 39, int signalLength = 9, double mult = 1000)
    {
        var resolvedFast = Math.Max(1, fastLength);
        _advSum = new RollingWindowSum(resolvedFast);
        _decSum = new RollingWindowSum(resolvedFast);
        _macd = new MacdEngine(maType, resolvedFast, Math.Max(1, slowLength), Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
        _mult = mult;
    }

    public IndicatorName Name => IndicatorName.McClellanOscillator;

    public void Reset()
    {
        _advSum.Reset();
        _decSum.Reset();
        _macd.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var advance = value > prevValue ? 1d : 0d;
        var decline = value < prevValue ? 1d : 0d;

        var advanceSum = isFinal ? _advSum.Add(advance, out _) : _advSum.Preview(advance, out _);
        var declineSum = isFinal ? _decSum.Add(decline, out _) : _decSum.Preview(decline, out _);
        var rana = advanceSum + declineSum != 0 ? _mult * (advanceSum - declineSum) / (advanceSum + declineSum) : 0;

        var macd = _macd.Next(rana, isFinal, out var signal, out var histogram);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "AdvSum", advanceSum },
                { "DecSum", declineSum },
                { "Mo", macd },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _advSum.Dispose();
        _decSum.Dispose();
        _macd.Dispose();
    }
}

[PrimaryOutput("Mdi")]
public sealed class McGinleyDynamicIndicatorState : IStreamingIndicatorState
{
    private readonly McGinleyWindow _window;
    public McGinleyDynamicIndicatorState(int length = 14, double k = 0.6) => _window = new(length, k);
    public IndicatorName Name => IndicatorName.McGinleyDynamicIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { ["Mdi"] = value } : null);
    }
}

[PrimaryOutput("Mnma")]
public sealed class McNichollMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly McNichollWindow _window;
    public McNichollMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.McNichollMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Mnma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mhlma")]
public sealed class MiddleHighLowMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly MidpointState _midpoint;

    public MiddleHighLowMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 14, int length2 = 10)
    {
        _smoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(length1)
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _midpoint = new MidpointState(Math.Max(1, length2));
    }

    public IndicatorName Name => IndicatorName.MiddleHighLowMovingAverage;

    public void Reset()
    {
        _smoother.Reset();
        _midpoint.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var midpoint = _midpoint.Update(bar, isFinal, includeOutputs: false).Value;
        var mhlma = _smoother.Next(midpoint, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mhlma", mhlma }
            };
        }

        return new StreamingIndicatorStateResult(mhlma, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _midpoint.Dispose();
    }
}

[PrimaryOutput("Mo")]
public sealed class MidpointOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MidpointOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 26, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MidpointOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var mo = RoundedMidpointOscillator.Of(value, highest, lowest);
        var signal = _signalSmoother.Next(mo, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", mo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mo, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Macd")]
public sealed class MirroredMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _openMa;
    private readonly IMovingAverageSmoother _closeMa;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly IMovingAverageSmoother _mirrorSignalSmoother;
    private bool _signalInvalid;
    private bool _mirrorSignalInvalid;

    public MirroredMovingAverageConvergenceDivergenceState(MovingAvgType maType =
        MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _openMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _closeMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _mirrorSignalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.MirroredMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _openMa.Reset();
        _closeMa.Reset();
        _signalSmoother.Reset();
        _mirrorSignalSmoother.Reset();
        _signalInvalid = false;
        _mirrorSignalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var emaOpen = _openMa.Next(bar.Open, isFinal);
        var emaClose = _closeMa.Next(bar.Close, isFinal);
        var macd = emaClose - emaOpen;
        var mirrorMacd = emaOpen - emaClose;
        var invalidSignal = _signalInvalid || double.IsInfinity(macd);
        var invalidMirrorSignal = _mirrorSignalInvalid || double.IsInfinity(mirrorMacd);
        var signal = invalidSignal ? double.NaN : _signalSmoother.Next(macd, isFinal);
        var mirrorSignal = invalidMirrorSignal ? double.NaN : _mirrorSignalSmoother.Next(mirrorMacd, isFinal);
        if (isFinal)
        {
            _signalInvalid = invalidSignal;
            _mirrorSignalInvalid = invalidMirrorSignal;
        }
        var histogram = macd - signal;
        var mirrorHistogram = mirrorMacd - mirrorSignal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Macd", macd },
                { "Signal", signal },
                { "Histogram", histogram },
                { "MirrorMacd", mirrorMacd },
                { "MirrorSignal", mirrorSignal },
                { "MirrorHistogram", mirrorHistogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _openMa.Dispose();
        _closeMa.Dispose();
        _signalSmoother.Dispose();
        _mirrorSignalSmoother.Dispose();
    }
}

[PrimaryOutput("Ppo")]
public sealed class MirroredPercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _openMa;
    private readonly IMovingAverageSmoother _closeMa;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly IMovingAverageSmoother _mirrorSignalSmoother;
    private bool _signalInvalid;
    private bool _mirrorSignalInvalid;

    public MirroredPercentagePriceOscillatorState(MovingAvgType maType =
        MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _openMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _closeMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _mirrorSignalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.MirroredPercentagePriceOscillator;

    public void Reset()
    {
        _openMa.Reset();
        _closeMa.Reset();
        _signalSmoother.Reset();
        _mirrorSignalSmoother.Reset();
        _signalInvalid = false;
        _mirrorSignalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var emaOpen = _openMa.Next(bar.Open, isFinal);
        var emaClose = _closeMa.Next(bar.Close, isFinal);
        var ppo = RoundedPercentageChange.Of(emaClose, emaOpen);
        var mirrorPpo = RoundedPercentageChange.Of(emaOpen, emaClose);
        var invalidSignal = _signalInvalid || double.IsInfinity(ppo);
        var invalidMirrorSignal = _mirrorSignalInvalid || double.IsInfinity(mirrorPpo);
        var signal = invalidSignal ? double.NaN : _signalSmoother.Next(ppo, isFinal);
        var mirrorSignal = invalidMirrorSignal ? double.NaN : _mirrorSignalSmoother.Next(mirrorPpo, isFinal);
        if (isFinal)
        {
            _signalInvalid = invalidSignal;
            _mirrorSignalInvalid = invalidMirrorSignal;
        }
        var histogram = ppo - signal;
        var mirrorHistogram = mirrorPpo - mirrorSignal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Ppo", ppo },
                { "Signal", signal },
                { "Histogram", histogram },
                { "MirrorPpo", mirrorPpo },
                { "MirrorSignal", mirrorSignal },
                { "MirrorHistogram", mirrorHistogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo, outputs);
    }

    public void Dispose()
    {
        _openMa.Dispose();
        _closeMa.Dispose();
        _signalSmoother.Dispose();
        _mirrorSignalSmoother.Dispose();
    }
}

[PrimaryOutput("Mo")]
public sealed class MobilityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly double[] _masses;
    private readonly int _length2;
    private readonly IMovingAverageSmoother _moSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public MobilityOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 10, int length2 = 14, int signalLength = 7)
    {
        _length1 = Math.Max(1, length1);
        _masses = new double[_length1];
        _length2 = Math.Max(1, length2);
        _moSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _highValues = new PooledRingBuffer<double>(_length2);
        _lowValues = new PooledRingBuffer<double>(_length2);
        _values = new PooledRingBuffer<double>(_length2 + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MobilityOscillator;

    public void Reset()
    {
        _moSmoother.Reset();
        _signalSmoother.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var countAvailable = Math.Min(_length2, _values.Count+1);
        var maximum = EhlersStreamingWindow.GetOffsetValue(_highValues, high, 0); var minimum = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, 0);
        for (var k = 1; k < countAvailable; k++)
        {
            maximum = Math.Max(maximum, EhlersStreamingWindow.GetOffsetValue(_highValues, high, k));
            minimum = Math.Min(minimum, EhlersStreamingWindow.GetOffsetValue(_lowValues, low, k));
        }
        var width = (maximum-minimum)/_length1;
        var rawValue = 0d;
        if (_values.Count >= _length2 && width > 0)
        {
            var comparison = EhlersStreamingWindow.GetOffsetValue(_values, value, _length2);
            var mode = 0; var largestMass = -1d; var priceMass = 0d;
            for (var bin = 0; bin < _length1; bin++)
            {
                var lower = minimum+bin*width;
                var upper = bin+1 == _length1 ? maximum : minimum+(bin+1)*width;
                double mass = 0;
                for (var k = 0; k < countAvailable; k++)
                {
                    var h = EhlersStreamingWindow.GetOffsetValue(_highValues, high, k); var l = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, k);
                    mass += h == l ? (l >= lower && (l < upper || bin+1 == _length1) ? 1 : 0) // NOSONAR: S1244 - Equal candle bounds are a point mass, not a narrow interval.
                        : Math.Max(0, Math.Min(h, upper)-Math.Max(l, lower))/(h-l);
                }
                _masses[bin] = mass; largestMass = Math.Max(largestMass, mass);
                if (comparison >= lower && (comparison < upper || bin+1 == _length1 && comparison <= upper)) priceMass = mass;
            }
            // Choose the first bin tied with the global maximum.
            while (mode+1 < _length1 && largestMass-_masses[mode] > 1e-12*countAvailable) mode++;
            largestMass = _masses[mode];
            var modePrice = minimum+(mode+0.5)*width;
            if (largestMass > 0)
                rawValue = (comparison < modePrice ? 1 : -1)*100*Math.Max(0, 1-priceMass/largestMass);
        }
        var moValue = rawValue;

        var moSmoothed = _moSmoother.Next(moValue, isFinal);
        var signal = _signalSmoother.Next(moSmoothed, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _highValues.TryAdd(high, out _);
            _lowValues.TryAdd(low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", moSmoothed },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(moSmoothed, outputs);
    }

    public void Dispose()
    {
        _moSmoother.Dispose();
        _signalSmoother.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
        _values.Dispose();
    }
}

[PrimaryOutput("Ghla")]
public sealed class ModifiedGannHiloActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ModifiedGannWindow _window;
    private readonly StreamingInputResolver _input;
    public ModifiedGannHiloActivatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, double mult = 1)
    { _window = new(maType, length, mult); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.ModifiedGannHiloActivator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.High, bar.Low, bar.Open, _input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Ghla", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mpvt")]
public sealed class ModifiedPriceVolumeTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly PriceVolumeTrendWindow _window;
    public ModifiedPriceVolumeTrendState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 23)
        => _window = new PriceVolumeTrendWindow(maType, length, true);
    public IndicatorName Name => IndicatorName.ModifiedPriceVolumeTrend;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Mpvt", value.Line }, { "Signal", value.Signal } } : null;
        return new StreamingIndicatorStateResult(value.Line, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Mf")]
public sealed class ModularFilterState : IStreamingIndicatorState
{
    private readonly ModularWindow _window; private readonly StreamingInputResolver _input;
    public ModularFilterState(int length = 200, double beta = .8, double z = .5) { _window = new(length, beta); _input = new StreamingInputResolver(InputName.Close, null); _ = z; }
    public IndicatorName Name => IndicatorName.ModularFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Mf", value } } : null);
    }
}

[PrimaryOutput("Mrsi")]
public sealed class MomentaRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RangeGainLossWindow? _wide;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _topSmoother;
    private readonly IMovingAverageSmoother _botSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MomentaRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 14)
    {
        if (StrengthWindow.Supports(maType)) _wide = new RangeGainLossWindow(maType, Math.Max(1, length1), new[] { length2 }, length2);
        _maxWindow = new RollingWindowMax(Math.Max(1, length1));
        _minWindow = new RollingWindowMin(Math.Max(1, length1));
        _topSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MomentaRelativeStrengthIndex;

    public void Reset()
    {
        _wide?.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _topSmoother.Reset();
        _botSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null)
        {
            var next = _wide.Next(bar.Close, isFinal);
            return new StreamingIndicatorStateResult(next.Value, includeOutputs ? new Dictionary<string, double>
                { { "Mrsi", next.Value }, { "Signal", next.Signal } } : null);
        }
        var value = _input.GetValue(bar);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var srcLc = value - lowest;
        var hcSrc = highest - value;
        var top = _topSmoother.Next(srcLc, isFinal);
        var bot = _botSmoother.Next(hcSrc, isFinal);
        var rsi = bot == 0 ? 100 : top == 0 ? 0 : MathHelper.MinOrMax(100 * top / (top + bot), 100, 0);
        var signal = _signalSmoother.Next(rsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mrsi", rsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _topSmoother.Dispose();
        _botSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Mo")]
public sealed class MomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MomentumOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(_length) : MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MomentumOscillator;

    public void Reset()
    {
        _values.Clear();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var mo = RoundedMomentumRatio.Of(value, prevValue);
        var signal = double.IsInfinity(mo) ? double.NaN : _signalSmoother.Next(mo, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", mo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mo, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Msw")]
public sealed class MorphedSineWaveState : IStreamingIndicatorState
{
    private readonly MorphedSineWindow _window;
    private readonly StreamingInputResolver _input;
    public MorphedSineWaveState(int length = 14, double power = 100)
    { _window = new(length, power); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.MorphedSineWave;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Msw", point.Value } } : null);
    }
}

[PrimaryOutput("Msi")]
public sealed class MotionSmoothnessIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly MotionSmoothnessWindow _window; private readonly StreamingInputResolver _input;
    public MotionSmoothnessIndexState(int length = 50) { _window = new(length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.MotionSmoothnessIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Msi", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Ts")]
public sealed class MotionToAttractionTrailingStopState : IStreamingIndicatorState
{
    private readonly MotionAttractionWindow _window;
    public MotionToAttractionTrailingStopState(int length = 14) { _window = new(length); }
    public IndicatorName Name => IndicatorName.MotionToAttractionTrailingStop;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Stop, includeOutputs ? new Dictionary<string, double> { { "Ts", point.Stop } } : null);
    }
}

[PrimaryOutput("Mt")]
public sealed class MoveTrackerState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevMt;
    private bool _hasPrev;

    public MoveTrackerState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MoveTracker;

    public void Reset()
    {
        _prevValue = 0;
        _prevMt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevMt = _hasPrev ? _prevMt : 0;
        var mt = _hasPrev ? value - prevValue : 0;
        var mtSignal = mt - prevMt;

        if (isFinal)
        {
            _prevValue = value;
            _prevMt = mt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mt", mt },
                { "Signal", mtSignal }
            };
        }

        return new StreamingIndicatorStateResult(mt, outputs);
    }
}

[PrimaryOutput("Maaf")]
public sealed class MovingAverageAdaptiveFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAverageAdaptiveFilterWindow _window;
    private readonly StreamingInputResolver _input;
    public MovingAverageAdaptiveFilterState(int length = 10, double filter = .15, double fastAlpha = .667, double slowAlpha = .0645)
    { _window = new(length, filter, fastAlpha, slowAlpha); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.MovingAverageAdaptiveFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Maaf", point.Value } } : null);
    }
    public void Dispose() { }
}

internal sealed class MacdEngine : IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;

    public MacdEngine(MovingAvgType maType, int fastLength, int slowLength, int signalLength)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public double Next(double value, bool isFinal, out double signal, out double histogram)
    {
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var macd = fast - slow;
        signal = _signalSmoother.Next(macd, isFinal);
        histogram = macd - signal;
        return macd;
    }

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

internal sealed class NoiseEliminationTechnologyEngine : IDisposable
{
    private readonly int _length;
    private readonly double _denom;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _scratch;

    public NoiseEliminationTechnologyEngine(int length)
    {
        _length = Math.Max(1, length);
        _denom = 0.5 * _length * (_length - 1);
        _values = new PooledRingBuffer<double>(_length);
        _scratch = new double[_length + 1];
    }

    public double Next(double value, bool isFinal)
    {
        for (var j = 1; j <= _length; j++)
        {
            _scratch[j] = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
        }

        double num = 0;
        for (var j = 2; j <= _length; j++)
        {
            var xj = _scratch[j];
            for (var k = 1; k <= j - 1; k++)
            {
                num -= Math.Sign(xj - _scratch[k]);
            }
        }

        var net = _denom != 0 ? num / _denom : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        return net;
    }

    public void Reset()
    {
        _values.Clear();
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}
