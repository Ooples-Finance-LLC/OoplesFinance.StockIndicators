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
public sealed class EhlersAdaptiveStochasticIndicatorV1State : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
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
    private readonly MamaNoiseWindow _window;
    public EhlersAlternateSignalToNoiseRatioState(int length = 6) => _window = new MamaNoiseWindow(length, 0);
    public IndicatorName Name => IndicatorName.EhlersAlternateSignalToNoiseRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(point.Snr, includeOutputs ? new Dictionary<string, double> { { "Esnr", point.Snr } } : null);
    }
    public void Dispose() => _window.Reset();
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
    private readonly AnticipateWindow? _exact;
    private readonly int _length;
    private readonly EhlersAnticipatePhase _phaseMatcher;
    private readonly double[] _history;
    private readonly StreamingInputResolver _input;
    private readonly EhlersImpulseResponseEngine _engine;
    private readonly PooledRingBuffer<double> _filters;

    public EhlersAnticipateIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 14, double bw = 1)
    {
        _length = AnticipateWindow.ValidateLength(length);
        if (ImpulseResponseWindow.Supports(maType))
        {
            _exact = new(maType, _length, bw);
            _phaseMatcher = null!; _history = Array.Empty<double>(); _input = default; _engine = null!; _filters = null!;
            return;
        }
        _phaseMatcher = new EhlersAnticipatePhase(_length);
        _history = new double[_length];
        _input = new StreamingInputResolver(InputName.Close, null);
        _engine = new EhlersImpulseResponseEngine(maType, length, bw);
        _filters = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersAnticipateIndicator;

    public void Reset()
    {
        if (_exact is not null) { _exact.Reset(); return; }
        _engine.Reset();
        _filters.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var prediction = _exact.Next(bar.Close, isFinal).Value;
            return new(prediction, includeOutputs ? new Dictionary<string, double> { { "Predict", prediction } } : null);
        }
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
        if (_exact is not null) { _exact.Dispose(); return; }
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
    private readonly RoofAutocorrelationWindow _window;
    public EhlersAutoCorrelationIndicatorState(int length1 = 48, int length2 = 10) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eaci", point.Value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Eacp")]
public sealed class EhlersAutoCorrelationPeriodogramState : IStreamingIndicatorState, IDisposable
{
    private readonly AutocorrelationSpectrumWindow _window;
    public EhlersAutoCorrelationPeriodogramState(int length1 = 48, int length2 = 10, int length3 = 3) => _window = new(length1, length2, length3);
    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationPeriodogram;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eacp", point.Value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Earsi")]
public sealed class EhlersAdaptiveRelativeStrengthIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveRsiV2Window _window;
    public EhlersAdaptiveRelativeStrengthIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3) => _window = new(length1, length2, length3, maType);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveRelativeStrengthIndexV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Earsi", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Dispose();
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
    private readonly AdaptiveRangeV2Window _window;
    public EhlersAdaptiveStochasticIndicatorV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3) => _window = new(length1, length2, length3, maType, false);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveStochasticIndicatorV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Easi", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Dispose();
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
        var fish = Math.Tanh(3 * v1);
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
    private readonly AdaptiveRangeV2Window _window;
    public EhlersAdaptiveCommodityChannelIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3) => _window = new(length1, length2, length3, maType, true);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveCommodityChannelIndexV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eacci", point.Value }, { "Signal", point.Average } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Eabpf")]
public sealed class EhlersAdaptiveBandPassFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveBandPassWindow _window;
    public EhlersAdaptiveBandPassFilterState(int length1 = 48, int length2 = 10, int length3 = 3, double bw = .3) => _window = new(length1, length2, length3, bw);
    public IndicatorName Name => IndicatorName.EhlersAdaptiveBandPassFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eabpf", point.Value }, { "Signal", point.Trigger } } : null);
    }
    public void Dispose() => _window.Dispose();
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
