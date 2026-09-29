#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("E3ssf")]
public sealed class Ehlers3PoleSuperSmootherFilterState : IStreamingIndicatorState
{
    private readonly ThreePoleWindow _window;
    public Ehlers3PoleSuperSmootherFilterState(int length = 20) => _window = new(length, 2);
    public IndicatorName Name => IndicatorName.Ehlers3PoleSuperSmootherFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "E3ssf", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("E3bf")]
public sealed class Ehlers3PoleButterworthFilterV1State : IStreamingIndicatorState
{
    private readonly ThreePoleWindow _window;
    public Ehlers3PoleButterworthFilterV1State(int length = 10) => _window = new(length, 0);
    public IndicatorName Name => IndicatorName.Ehlers3PoleButterworthFilterV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "E3bf", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("E3bf")]
public sealed class Ehlers3PoleButterworthFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly ThreePoleWindow _window;
    public Ehlers3PoleButterworthFilterV2State(int length = 15) => _window = new(length, 1);
    public IndicatorName Name => IndicatorName.Ehlers3PoleButterworthFilterV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "E3bf", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Eacc")]
public sealed class EhlersAdaptiveCyberCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCyberWindow _window;
    public EhlersAdaptiveCyberCycleState(int length = 5, double alpha = .07) => _window = new(length, alpha);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveCyberCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Cycle, includeOutputs ? new Dictionary<string, double> { { "Eacc", point.Cycle }, { "Period", point.Period } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Eacog")]
public sealed class EhlersAdaptiveCenterOfGravityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveGravityWindow _window;
    public EhlersAdaptiveCenterOfGravityOscillatorState(int length = 5) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveCenterOfGravityOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Eacog", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Ealf")]
public sealed class EhlersAdaptiveLaguerreFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveLaguerreWindow _window;
    public EhlersAdaptiveLaguerreFilterState(int length1 = 14, int length2 = 5) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveLaguerreFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ealf", value } } : null);
    }
    public void Dispose() { }

}

[PrimaryOutput("Eapps")]
public sealed class EhlersAllPassPhaseShifterState : IStreamingIndicatorState, IDisposable
{
    private readonly AllPassPhaseWindow _window; private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersAllPassPhaseShifterState(int length = 20, double qq = .5) => _window = new(length, qq);
    public IndicatorName Name => IndicatorName.EhlersAllPassPhaseShifter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Eapps", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Mama")]
public sealed class EhlersMotherOfAdaptiveMovingAveragesState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _engine;

    public EhlersMotherOfAdaptiveMovingAveragesState(double fastAlpha = 0.5, double slowAlpha = 0.05)
    {
        _input = new StreamingInputResolver(InputName.Close, null);
        _engine = new EhlersMotherOfAdaptiveMovingAveragesEngine(fastAlpha, slowAlpha);
    }

    public IndicatorName Name => IndicatorName.EhlersMotherOfAdaptiveMovingAverages;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _input.GetValue(bar);
        var snapshot = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "Fama", snapshot.Fama },
                { "Mama", snapshot.Mama },
                { "I1", snapshot.I1 },
                { "Q1", snapshot.Q1 },
                { "SmoothPeriod", snapshot.SmoothPeriod },
                { "Smooth", snapshot.Smooth },
                { "Real", snapshot.Real },
                { "Imag", snapshot.Imag }
            };
        }

        return new StreamingIndicatorStateResult(snapshot.Mama, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

[PrimaryOutput("Earsi")]
public sealed class EhlersAdaptiveRelativeStrengthIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveRsiV1Window _window;
    public EhlersAdaptiveRelativeStrengthIndexV1State(double cycPart = .5) => _window = new AdaptiveRsiV1Window(cycPart, false);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveRelativeStrengthIndexV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Earsi", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Earsift")]
public sealed class EhlersAdaptiveRsiFisherTransformV1State : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveRsiV1Window _window;
    public EhlersAdaptiveRsiFisherTransformV1State() => _window = new AdaptiveRsiV1Window(.5, true);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveRsiFisherTransformV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Earsift", point.Value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Easi")]
public sealed class EhlersAdaptiveStochasticIndicatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveRangeV1Window _window;

    public EhlersAdaptiveStochasticIndicatorV1State(double cycPart = .5) => _window = new AdaptiveRangeV1Window(cycPart, false, .015);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveStochasticIndicatorV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var price = bar.Close; var point = _window.Next(price, bar.High, bar.Low, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Easi", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Eacci")]
public sealed class EhlersAdaptiveCommodityChannelIndexV1State : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly AdaptiveRangeV1Window _window;
    private bool _selected;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public EhlersAdaptiveCommodityChannelIndexV1State(double cycPart = 1, double constant = .015) => _window = new AdaptiveRangeV1Window(cycPart, true, constant);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveCommodityChannelIndexV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var price = _selected ? bar.Close : CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close); var point = _window.Next(price, bar.High, bar.Low, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eacci", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Esnr")]
public sealed class EhlersAlternateSignalToNoiseRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private double _prevRange;
    private double _prevSnr;

    public EhlersAlternateSignalToNoiseRatioState(int length = 6)
    {
        _length = Math.Max(1, length);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
    }

    public IndicatorName Name => IndicatorName.EhlersAlternateSignalToNoiseRatio;

    public void Reset()
    {
        _mama.Reset();
        _prevRange = 0;
        _prevSnr = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var snapshot = _mama.Next(bar.Close, isFinal);
        var range = (0.1 * (bar.High - bar.Low)) + (0.9 * _prevRange);
        var temp = range != 0 ? (snapshot.Real + snapshot.Imag) / (range * range) : 0;
        var logTemp = temp > 0 ? Math.Log10(temp) : 0;
        var snr = (0.25 * ((10 * logTemp) + _length)) + (0.75 * _prevSnr);

        if (isFinal)
        {
            _prevRange = range;
            _prevSnr = snr;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esnr", snr }
            };
        }

        return new StreamingIndicatorStateResult(snr, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
    }
}

[PrimaryOutput("Eamd")]
public sealed class EhlersAMDetectorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private readonly AmDetectorWindow _window;
    public EhlersAMDetectorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 4, int length2 = 8) => _window = new(maType, length1, length2);
    public IndicatorName Name => IndicatorName.EhlersAMDetector;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Open, bar.Close, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Eamd", point.Line }, { "Signal", point.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}


[PrimaryOutput("Eir")]
public sealed class EhlersImpulseResponseState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersImpulseResponseEngine _engine;

    public EhlersImpulseResponseState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 20, double bw = 1)
    {
        _input = new StreamingInputResolver(InputName.Close, null);
        _engine = new EhlersImpulseResponseEngine(maType, length, bw);
    }

    public IndicatorName Name => IndicatorName.EhlersImpulseResponse;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var filt = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eir", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

[PrimaryOutput("Predict")]
public sealed class EhlersAnticipateIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly EhlersAnticipatePhase _phaseMatcher;
    private readonly double[] _history;
    private readonly StreamingInputResolver _input;
    private readonly EhlersImpulseResponseEngine _engine;
    private readonly PooledRingBuffer<double> _filters;

    public EhlersAnticipateIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 14, double bw = 1)
    {
        _length = Math.Max(1, length);
        _phaseMatcher = new EhlersAnticipatePhase(_length);
        _history = new double[_length];
        _input = new StreamingInputResolver(InputName.Close, null);
        _engine = new EhlersImpulseResponseEngine(maType, length, bw);
        _filters = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersAnticipateIndicator;

    public void Reset()
    {
        _engine.Reset();
        _filters.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hFilt = _engine.Next(value, isFinal);

        for (var lag = 0; lag < _length; lag++)
            _history[lag] = EhlersStreamingWindow.GetOffsetValue(_filters, hFilt, lag);
        var predict = _phaseMatcher.Predict(_history);

        if (isFinal)
        {
            _filters.TryAdd(hFilt, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Predict", predict }
            };
        }

        return new StreamingIndicatorStateResult(predict, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _filters.Dispose();
    }
}

[PrimaryOutput("Erf")]
public sealed class EhlersRoofingFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersRoofingFilterV2Kernel _filter;
    private readonly StreamingInputResolver _input;

    public EhlersRoofingFilterV2State(int upperLength = 80, int lowerLength = 40)
    {
        _filter = new EhlersRoofingFilterV2Kernel(upperLength, lowerLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    internal EhlersRoofingFilterV2State(int upperLength, int lowerLength, InputName inputName)
    {
        _filter = new EhlersRoofingFilterV2Kernel(upperLength, lowerLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    internal EhlersRoofingFilterV2State(int upperLength, int lowerLength, Func<OhlcvBar, double> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        _filter = new EhlersRoofingFilterV2Kernel(upperLength, lowerLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRoofingFilterV2;
    public void Reset() => _filter.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _filter.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs
            ? new Dictionary<string, double> { ["Erf"] = value } : null);
    }

    public void Dispose() { }
}

[PrimaryOutput("Eaci")]
public sealed class EhlersAutoCorrelationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly double[] _xWindow;
    private readonly double[] _yWindow;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _roofingValues;

    public EhlersAutoCorrelationIndicatorState(int length1 = 48, int length2 = 10)
    {
        _length1 = Math.Max(1, length1);
        _roofingFilter = new EhlersRoofingFilterV2State(length1, length2);
        _roofingValues = new PooledRingBuffer<double>(2 * _length1);
        _xWindow = new double[_length1];
        _yWindow = new double[_length1];
    }

    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationIndicator;

    public void Reset()
    {
        _roofingFilter.Reset();
        _roofingValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var count = Math.Min(_roofingValues.Count + 1, _length1);
        for (var j = 0; j < count; j++)
        {
            var offset = count - 1 - j;
            _xWindow[j] = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, offset);
            _yWindow[j] = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, offset + _length1);
        }
        var corr = EhlersAutocorrelation.Normalized(_xWindow.AsSpan(0, count), _yWindow.AsSpan(0, count));

        if (isFinal)
        {
            _roofingValues.TryAdd(roofingFilter, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eaci", corr }
            };
        }

        return new StreamingIndicatorStateResult(corr, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _roofingValues.Dispose();
    }
}

[PrimaryOutput("Eacp")]
public sealed class EhlersAutoCorrelationPeriodogramState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _length3;
    private readonly EhlersAutoCorrelationIndicatorState _corrState;
    private readonly PooledRingBuffer<double> _corrValues;
    private readonly double[] _rArray;
    private readonly double[] _rNext;

    public EhlersAutoCorrelationPeriodogramState(int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(0, length3);
        _corrState = new EhlersAutoCorrelationIndicatorState(_length1, _length2);
        _corrValues = new PooledRingBuffer<double>(_length1);
        _rArray = new double[_length1 + 1];
        _rNext = new double[_length1 + 1];
    }

    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationPeriodogram;

    public void Reset()
    {
        _corrState.Reset();
        _corrValues.Clear();
        Array.Clear(_rArray, 0, _rArray.Length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var corr = _corrState.Update(bar, isFinal, includeOutputs: false).Value;

        double maxPwr = 0;
        for (var j = _length2; j <= _length1; j++)
        {
            double cosPart = 0;
            double sinPart = 0;
            for (var k = _length3; k <= _length1; k++)
            {
                var prevCorr = EhlersStreamingWindow.GetOffsetValue(_corrValues, corr, k);
                cosPart += prevCorr * Math.Cos(2 * Math.PI * ((double)k / j));
                sinPart += prevCorr * Math.Sin(2 * Math.PI * ((double)k / j));
            }

            var sqSum = MathHelper.Pow(cosPart, 2) + MathHelper.Pow(sinPart, 2);
            var r = (0.2 * MathHelper.Pow(sqSum, 2)) + (0.8 * _rArray[j]);
            _rNext[j] = r;
            maxPwr = Math.Max(r, maxPwr);
        }

        // The powers are normalised by this bar's maximum, so they must be this bar's powers too. Reading
        // _rArray here made a preview divide the previous bar's powers by the current bar's maximum.
        double spx = 0;
        double sp = 0;
        for (var j = _length2; j <= _length1; j++)
        {
            var pwr = maxPwr != 0 ? _rNext[j] / maxPwr : 0;
            if (pwr >= 0.5)
            {
                spx += j * pwr;
                sp += pwr;
            }
        }

        var domCyc = sp != 0 ? spx / sp : 0;

        if (isFinal)
        {
            _corrValues.TryAdd(corr, out _);
            Array.Copy(_rNext, _rArray, _rArray.Length);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eacp", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _corrState.Dispose();
        _corrValues.Dispose();
    }
}

[PrimaryOutput("Earsi")]
public sealed class EhlersAdaptiveRelativeStrengthIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersAutoCorrelationPeriodogramState _periodogram;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _roofingValues;
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _prevUpChg;
    private double _prevDenom;
    private double _prevArsi1;
    private double _prevArsi2;

    public EhlersAdaptiveRelativeStrengthIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / _length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / _length2, 0.99));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _periodogram = new EhlersAutoCorrelationPeriodogramState(_length1, _length2, resolved3);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _roofingValues = new PooledRingBuffer<double>(_length1);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2;

    public void Reset()
    {
        _periodogram.Reset();
        _roofingFilter.Reset();
        _roofingValues.Clear();
        _signalSmoother.Reset();
        _prevUpChg = 0;
        _prevDenom = 0;
        _prevArsi1 = 0;
        _prevArsi2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;

        var length = MathHelper.CeilingCycle(domCyc / 2);
        double upChg = 0;
        double dnChg = 0;
        for (var j = 0; j < length; j++)
        {
            var filt = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, j);
            var prevFilt = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, j + 1);
            if (filt > prevFilt)
            {
                upChg += filt - prevFilt;
            }
            else if (filt < prevFilt)
            {
                dnChg += prevFilt - filt;
            }
        }

        var denom = upChg + dnChg;
        var arsi = denom != 0 && _prevDenom != 0
            ? (_c1 * ((upChg / denom) + (_prevUpChg / _prevDenom)) / 2) + (_c2 * _prevArsi1) + (_c3 * _prevArsi2)
            : 0;
        var signal = _signalSmoother.Next(arsi, isFinal);

        if (isFinal)
        {
            _roofingValues.TryAdd(roofingFilter, out _);
            _prevUpChg = upChg;
            _prevDenom = denom;
            _prevArsi2 = _prevArsi1;
            _prevArsi1 = arsi;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Earsi", arsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(arsi, outputs);
    }

    public void Dispose()
    {
        _periodogram.Dispose();
        _roofingFilter.Dispose();
        _roofingValues.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Earsift")]
public sealed class EhlersAdaptiveRsiFisherTransformV2State : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersAdaptiveRelativeStrengthIndexV2State _arsiState;
    private double _prevFish1;
    private double _prevFish2;

    public EhlersAdaptiveRsiFisherTransformV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _arsiState = new EhlersAdaptiveRelativeStrengthIndexV2State(maType, length1, length2, length3);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveRsiFisherTransformV2;

    public void Reset()
    {
        _arsiState.Reset();
        _prevFish1 = 0;
        _prevFish2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var arsi = _arsiState.Update(bar, isFinal, includeOutputs: false).Value;
        var tranRsi = 2 * (arsi - 0.5);
        var ampRsi = MathHelper.MinOrMax(1.5 * tranRsi, 0.999, -0.999);
        var fish = 0.5 * Math.Log((1 + ampRsi) / (1 - ampRsi));

        if (isFinal)
        {
            _prevFish2 = _prevFish1;
            _prevFish1 = fish;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Earsift", fish }
            };
        }

        return new StreamingIndicatorStateResult(fish, outputs);
    }

    public void Dispose()
    {
        _arsiState.Dispose();
    }
}

[PrimaryOutput("Easi")]
public sealed class EhlersAdaptiveStochasticIndicatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersAutoCorrelationPeriodogramState _periodogram;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _roofingValues;
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _prevStoc;
    private double _prevAstoc1;
    private double _prevAstoc2;

    public EhlersAdaptiveStochasticIndicatorV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / _length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / _length2, 0.99));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _periodogram = new EhlersAutoCorrelationPeriodogramState(_length1, _length2, resolved3);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _roofingValues = new PooledRingBuffer<double>(_length1);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveStochasticIndicatorV2;

    public void Reset()
    {
        _periodogram.Reset();
        _roofingFilter.Reset();
        _roofingValues.Clear();
        _signalSmoother.Reset();
        _prevStoc = 0;
        _prevAstoc1 = 0;
        _prevAstoc2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;

        var length = MathHelper.CeilingCycle(domCyc);
        var highest = roofingFilter;
        var lowest = roofingFilter;
        // Match batch: only look back at values that exist (j <= count of stored values)
        for (var j = 1; j < length && j <= _roofingValues.Count; j++)
        {
            var filt = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, j);
            if (filt > highest)
            {
                highest = filt;
            }
            if (filt < lowest)
            {
                lowest = filt;
            }
        }

        var stoc = highest != lowest ? (roofingFilter - lowest) / (highest - lowest) : 0;
        var astoc = (_c1 * ((stoc + _prevStoc) / 2)) + (_c2 * _prevAstoc1) + (_c3 * _prevAstoc2);
        var signal = _signalSmoother.Next(astoc, isFinal);

        if (isFinal)
        {
            _roofingValues.TryAdd(roofingFilter, out _);
            _prevStoc = stoc;
            _prevAstoc2 = _prevAstoc1;
            _prevAstoc1 = astoc;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Easi", astoc },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(astoc, outputs);
    }

    public void Dispose()
    {
        _periodogram.Dispose();
        _roofingFilter.Dispose();
        _roofingValues.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Easift")]
public sealed class EhlersAdaptiveStochasticInverseFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersAdaptiveStochasticIndicatorV2State _astocState;
    private double _prevFish;

    public EhlersAdaptiveStochasticInverseFisherTransformState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _astocState = new EhlersAdaptiveStochasticIndicatorV2State(maType, length1, length2, length3);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveStochasticInverseFisherTransform;

    public void Reset()
    {
        _astocState.Reset();
        _prevFish = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var astoc = _astocState.Update(bar, isFinal, includeOutputs: false).Value;
        var v1 = 2 * (astoc - 0.5);
        var fish = (Math.Exp(6 * v1) - 1) / (Math.Exp(6 * v1) + 1);
        var trigger = 0.9 * _prevFish;

        if (isFinal)
        {
            _prevFish = fish;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Easift", fish },
                { "Signal", trigger }
            };
        }

        return new StreamingIndicatorStateResult(fish, outputs);
    }

    public void Dispose()
    {
        _astocState.Dispose();
    }
}

[PrimaryOutput("Eacci")]
public sealed class EhlersAdaptiveCommodityChannelIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersAutoCorrelationPeriodogramState _periodogram;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly AdaptiveWindowMean _tempSum;
    private readonly AdaptiveWindowMean _mdSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _prevRatio;
    private double _prevAcci1;
    private double _prevAcci2;

    public EhlersAdaptiveCommodityChannelIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / _length2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(1.414 * Math.PI / _length2, 0.99));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _periodogram = new EhlersAutoCorrelationPeriodogramState(_length1, _length2, resolved3);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _tempSum = new AdaptiveWindowMean(_length1);
        _mdSum = new AdaptiveWindowMean(_length1);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveCommodityChannelIndexV2;

    public void Reset()
    {
        _periodogram.Reset();
        _roofingFilter.Reset();
        _tempSum.Reset();
        _mdSum.Reset();
        _signalSmoother.Reset();
        _prevRatio = 0;
        _prevAcci1 = 0;
        _prevAcci2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var cycLength = MathHelper.CeilingCycle(domCyc);

        var avg = _tempSum.Next(roofingFilter, cycLength, isFinal);
        var md = MathHelper.Pow(roofingFilter - avg, 2);
        var mdAvg = _mdSum.Next(md, cycLength, isFinal);
        var rms = cycLength >= 0 ? MathHelper.Sqrt(mdAvg) : 0;
        var num = roofingFilter - avg;
        var denom = 0.015 * rms;
        var ratio = denom != 0 ? num / denom : 0;
        var acci = (_c1 * ((ratio + _prevRatio) / 2)) + (_c2 * _prevAcci1) + (_c3 * _prevAcci2);
        var signal = _signalSmoother.Next(acci, isFinal);

        if (isFinal)
        {
            _prevRatio = ratio;
            _prevAcci2 = _prevAcci1;
            _prevAcci1 = acci;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eacci", acci },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(acci, outputs);
    }

    public void Dispose()
    {
        _periodogram.Dispose();
        _roofingFilter.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Eabpf")]
public sealed class EhlersAdaptiveBandPassFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly double _bw;
    private readonly EhlersAutoCorrelationPeriodogramState _periodogram;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private double _prevRoofingFilter1;
    private double _prevRoofingFilter2;
    private double _prevBp1;
    private double _prevBp2;
    private double _prevPeak;
    private double _prevSignal1;
    private double _prevSignal2;
    private double _prevSignal3;
    private double _prevLeadPeak;
    private int _index;

    public EhlersAdaptiveBandPassFilterState(int length1 = 48, int length2 = 10, int length3 = 3, double bw = 0.3)
    {
        _length1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _bw = bw;
        _periodogram = new EhlersAutoCorrelationPeriodogramState(_length1, resolved2, _length3);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, resolved2);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveBandPassFilter;

    public void Reset()
    {
        _periodogram.Reset();
        _roofingFilter.Reset();
        _prevRoofingFilter1 = 0;
        _prevRoofingFilter2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _prevPeak = 0;
        _prevSignal1 = 0;
        _prevSignal2 = 0;
        _prevSignal3 = 0;
        _prevLeadPeak = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length3);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var beta = Math.Cos(2 * Math.PI / (0.9 * domCyc));
        var gamma = 1 / Math.Cos(2 * Math.PI * _bw / (0.9 * domCyc));
        var alpha = MathHelper.MinOrMax(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99, 0.01);

        var bp = _index > 2
            ? (0.5 * (1 - alpha) * (roofingFilter - _prevRoofingFilter2)) + (beta * (1 + alpha) * _prevBp1) -
              (alpha * _prevBp2)
            : 0;
        var peak = Math.Max(0.991 * _prevPeak, Math.Abs(bp));
        var sig = peak != 0 ? bp / peak : 0;
        var lead = 1.3 * (sig + _prevSignal1 - _prevSignal2 - _prevSignal3) / 4;
        var leadPeak = Math.Max(0.93 * _prevLeadPeak, Math.Abs(lead));
        var trigger = 0.9 * _prevSignal1;

        if (isFinal)
        {
            _prevRoofingFilter2 = _prevRoofingFilter1;
            _prevRoofingFilter1 = roofingFilter;
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _prevPeak = peak;
            _prevSignal3 = _prevSignal2;
            _prevSignal2 = _prevSignal1;
            _prevSignal1 = sig;
            _prevLeadPeak = leadPeak;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eabpf", sig },
                { "Signal", trigger }
            };
        }

        return new StreamingIndicatorStateResult(sig, outputs);
    }

    public void Dispose()
    {
        _periodogram.Dispose();
        _roofingFilter.Dispose();
    }
}

internal readonly struct EhlersMamaSnapshot
{
    public EhlersMamaSnapshot(double fama, double mama, double i1, double q1, double smoothPeriod, double smooth,
        double real, double imag)
    {
        Fama = fama;
        Mama = mama;
        I1 = i1;
        Q1 = q1;
        SmoothPeriod = smoothPeriod;
        Smooth = smooth;
        Real = real;
        Imag = imag;
    }

    public double Fama { get; }
    public double Mama { get; }
    public double I1 { get; }
    public double Q1 { get; }
    public double SmoothPeriod { get; }
    public double Smooth { get; }
    public double Real { get; }
    public double Imag { get; }
}

internal sealed class EhlersMotherOfAdaptiveMovingAveragesEngine : IDisposable
{
    private readonly MamaWindow _window;
    public EhlersMotherOfAdaptiveMovingAveragesEngine(double fastAlpha, double slowAlpha) => _window = new(fastAlpha, slowAlpha);
    public EhlersMamaSnapshot Next(double value, bool isFinal) => _window.Next(value, isFinal).Values;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Reset();
}

internal sealed class EhlersImpulseResponseEngine : IDisposable
{
    private readonly ImpulseResponseWindow _window;
    public EhlersImpulseResponseEngine(MovingAvgType maType, int length, double bw) => _window = new(maType, length, bw);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}


internal static class EhlersStreamingWindow
{
    public static double GetOffsetValue(PooledRingBuffer<double> buffer, int offset)
    {
        if (offset <= 0 || buffer.Count < offset)
        {
            return 0;
        }

        return buffer[buffer.Count - offset];
    }

    public static double GetOffsetValue(PooledRingBuffer<double> buffer, double pendingValue, int offset)
    {
        if (offset <= 0)
        {
            return pendingValue;
        }

        return offset <= buffer.Count ? buffer[buffer.Count - offset] : 0;
    }

    public static double GetMedian(PooledRingBuffer<double> buffer, double pendingValue, double[] scratch)
    {
        var count = buffer.Count;
        var start = count == buffer.Capacity ? 1 : 0;
        var scratchCount = count - start + 1;
        for (var i = 0; i < count - start; i++)
        {
            scratch[i] = buffer[start + i];
        }

        scratch[scratchCount - 1] = pendingValue;
        Array.Sort(scratch, 0, scratchCount);
        var mid = scratchCount / 2;
        if ((scratchCount & 1) == 1)
        {
            return scratch[mid];
        }

        return (scratch[mid - 1] + scratch[mid]) / 2;
    }
}
