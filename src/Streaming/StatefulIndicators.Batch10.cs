using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Trend")]
public sealed class EhlersEmpiricalModeDecompositionState : IStreamingIndicatorState, IDisposable
{
    private readonly EmpiricalDecompositionWindow _window;
    public EhlersEmpiricalModeDecompositionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 20, int length2 = 50, double delta = .5, double fraction = .1) => _window = new(maType, length1, length2, delta, fraction);
    public IndicatorName Name => IndicatorName.EhlersEmpiricalModeDecomposition;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Trend, includeOutputs ? new Dictionary<string, double> { { "Trend", point.Trend }, { "Peak", point.Peak }, { "Valley", point.Valley } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ehwi")]
public sealed class EhlersHammingWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly HammingIndicatorWindow _window;
    public EhlersHammingWindowIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHammingMovingAverage, int length = 20, double pedestal = 10)
        { _ = pedestal; _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.EhlersHammingWindowIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Open, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ehwi", point.Line }, { "Roc", point.Roc } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ehma")]
public sealed class EhlersHannMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersHannMovingAverageState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _smoother = MovingAverageSmootherFactory.Create(MovingAvgType.EhlersHannMovingAverage, resolved);
    }

    public IndicatorName Name => IndicatorName.EhlersHannMovingAverage;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var filt = _smoother.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ehma", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

[PrimaryOutput("Ehwi")]
public sealed class EhlersHannWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly HannIndicatorWindow _window;
    public EhlersHannWindowIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage, int length = 20)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.EhlersHannWindowIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Open, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ehwi", point.Line }, { "Roc", point.Roc } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Hp")]
public sealed class EhlersHighPassFilterV1State : IStreamingIndicatorState
{
    private readonly HighPassWindow _window;
    public EhlersHighPassFilterV1State(int length = 125, double mult = 1) => _window = new(length, mult);
    public IndicatorName Name => IndicatorName.EhlersHighPassFilterV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Hp", value } } : null);
    }
}

[PrimaryOutput("Ehpf")]
public sealed class EhlersHighPassFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV2Engine _engine;

    public EhlersHighPassFilterV2State(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20)
    {
        _input = new StreamingInputResolver(InputName.Close, null);
        _engine = new HighPassFilterV2Engine(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.EhlersHighPassFilterV2;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hp = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ehpf", hp }
            };
        }

        return new StreamingIndicatorStateResult(hp, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
[PrimaryOutput("IQ")]
public sealed class EhlersHilbertOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly MamaDerivedWindow _window = new(1);
    public EhlersHilbertOscillatorState(int length = 7) { }
    public IndicatorName Name => IndicatorName.EhlersHilbertOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Second, includeOutputs ? new Dictionary<string, double> { { "I3", point.First }, { "IQ", point.Second } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Quad")]
public sealed class EhlersHilbertTransformIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly HilbertPhaseWindow _window;
    public EhlersHilbertTransformIndicatorState(int length = 7, double iMult = .635, double qMult = .338) => _window = new(length, iMult, qMult, 1, false);
    public IndicatorName Name => IndicatorName.EhlersHilbertTransformIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Imaginary, includeOutputs ? new Dictionary<string, double> { { "Quad", point.Imaginary }, { "Inphase", point.Real } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Real")]
public sealed class EhlersHilbertTransformerState : IStreamingIndicatorState, IDisposable
{
    private readonly HilbertTransformerWindow _window;
    public EhlersHilbertTransformerState(int length1 = 48, int length2 = 20) => _window = new(length1, length2, 1, false);
    public IndicatorName Name => IndicatorName.EhlersHilbertTransformer;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Real, includeOutputs ? new Dictionary<string, double> { { "Real", point.Real }, { "Imag", point.Imaginary } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Real")]
public sealed class EhlersHilbertTransformerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly HilbertTransformerWindow _window;
    public EhlersHilbertTransformerIndicatorState(int length1 = 48, int length2 = 20, int length3 = 10) => _window = new(length1, length2, length3, true);
    public IndicatorName Name => IndicatorName.EhlersHilbertTransformerIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Real, includeOutputs ? new Dictionary<string, double> { { "Real", point.Real }, { "Imag", point.Imaginary } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Ehdc")]
public sealed class EhlersHomodyneDominantCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly HilbertCycleWindow _window;
    public EhlersHomodyneDominantCycleState(int length1 = 48, int length2 = 20, int length3 = 10) => _window = new(length1, length2, length3, 1, 1);
    public IndicatorName Name => IndicatorName.EhlersHomodyneDominantCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Ehdc", point.Value } } : null);
    }
    public void Dispose() => _window.Reset();
}
[PrimaryOutput("Ehplprf")]
public sealed class EhlersHpLpRoofingFilterState : IStreamingIndicatorState
{
    private readonly HpLpRoofingWindow _window; private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersHpLpRoofingFilterState(int length1 = 48, int length2 = 10) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.EhlersHpLpRoofingFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal).Roof;
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ehplprf", value } } : null);
    }
}

[PrimaryOutput("Ehc")]
public sealed class EhlersHurstCoefficientState : IStreamingIndicatorState, IDisposable
{
    private readonly HurstCoefficientWindow _window;
    public EhlersHurstCoefficientState(int length1 = 30, int length2 = 20) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.EhlersHurstCoefficient;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ehc", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Eir")]
public sealed class EhlersImpulseReactionState : IStreamingIndicatorState, IDisposable
{
    private readonly ImpulseReactionWindow _window; private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersImpulseReactionState(int length1 = 2, int length2 = 20, double qq = .9) => _window = new(length1, length2, qq);
    public IndicatorName Name => IndicatorName.EhlersImpulseReaction;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Eir", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Eiirf")]
public sealed class EhlersInfiniteImpulseResponseFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersIirWindow _window;
    public EhlersInfiniteImpulseResponseFilterState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersInfiniteImpulseResponseFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Eiirf", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Eipi")]
public sealed class EhlersInstantaneousPhaseIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly HilbertPhaseWindow _window;
    public EhlersInstantaneousPhaseIndicatorState(int length1 = 7, int length2 = 50) => _window = new(length1, .635, .338, length2, true);
    public IndicatorName Name => IndicatorName.EhlersInstantaneousPhaseIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Cycle, includeOutputs ? new Dictionary<string, double> { { "Eipi", point.Cycle } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Eit")]
public sealed class EhlersInstantaneousTrendlineV1State : IStreamingIndicatorState, IDisposable
{
    private readonly MamaDerivedWindow _window = new(2);
    public EhlersInstantaneousTrendlineV1State() { }
    public IndicatorName Name => IndicatorName.EhlersInstantaneousTrendlineV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.First, includeOutputs ? new Dictionary<string, double> { { "Eit", point.First }, { "Signal", point.Second } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Eit")]
public sealed class EhlersInstantaneousTrendlineV2State : IStreamingIndicatorState
{
    private readonly InstantaneousTrendWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersInstantaneousTrendlineV2State(double alpha = 0.07) { _window = new(alpha); }
    public IndicatorName Name => IndicatorName.EhlersInstantaneousTrendlineV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var p = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(p.Line, includeOutputs ? new Dictionary<string, double> { { "Eit", p.Line }, { "Signal", p.Signal } } : null);
    }
}

[PrimaryOutput("Eift")]
public sealed class EhlersInverseFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly RsiState _rsi;
    private readonly StrengthAverage? _exactAverage;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersInverseFisherTransformState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 5, int length2 = 9)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, null);
        _rsi = new RsiState(maType, resolvedLength1);
        if (StrengthWindow.Supports(maType)) _exactAverage = new StrengthAverage(maType, resolvedLength2);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
    }

    public IndicatorName Name => IndicatorName.EhlersInverseFisherTransform;

    public void Reset()
    {
        _exactAverage?.Reset();
        _rsi.Reset();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var v1 = 0.1 * (rsi - 50);
        var v2 = _exactAverage is null ? _smoother.Next(v1, isFinal) : _exactAverage.Next(new StrengthValue(v1), isFinal).Mantissa;
        var inverseFisherTransform = Math.Tanh(v2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eift", inverseFisherTransform }
            };
        }

        return new StreamingIndicatorStateResult(inverseFisherTransform, outputs);
    }

    public void Dispose()
    {
        _exactAverage?.Dispose();
        _rsi.Dispose();
        _smoother.Dispose();
    }
}

[PrimaryOutput("Ekama")]
public sealed class EhlersKaufmanAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersKaufmanWindow _window;
    public EhlersKaufmanAdaptiveMovingAverageState(int length = 20) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersKaufmanAdaptiveMovingAverage;
    public void Reset() => _window.Reset();
    public void Dispose() { }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ekama", value } } : null);
    }
}

internal sealed class EhlersHammingMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly HammingWindowMean _mean;
    public EhlersHammingMovingAverageSmoother(int length, double pedestal = 3) => _mean = new HammingWindowMean(length, pedestal);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class EhlersHilbertTransformIndicatorEngine : IDisposable
{
    private readonly int _length;
    private readonly double _iMult;
    private readonly double _qMult;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _v1Values;
    private readonly PooledRingBuffer<double> _inPhaseValues;
    private readonly PooledRingBuffer<double> _quadValues;
    private int _index;

    public EhlersHilbertTransformIndicatorEngine(int length, double iMult, double qMult)
    {
        _length = Math.Max(1, length);
        _iMult = iMult;
        _qMult = qMult;
        _values = new PooledRingBuffer<double>(_length);
        _v1Values = new PooledRingBuffer<double>(4);
        _inPhaseValues = new PooledRingBuffer<double>(3);
        _quadValues = new PooledRingBuffer<double>(2);
    }

    public void Reset()
    {
        _values.Clear();
        _v1Values.Clear();
        _inPhaseValues.Clear();
        _quadValues.Clear();
        _index = 0;
    }

    public void Next(double value, bool isFinal, out double inPhase, out double quad)
    {
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var v1 = _index >= _length ? value - prevValue : 0;
        var v2 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 2);
        var v4 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 4);
        var inPhase3 = EhlersStreamingWindow.GetOffsetValue(_inPhaseValues, 3);
        var quad2 = EhlersStreamingWindow.GetOffsetValue(_quadValues, 2);

        inPhase = (1.25 * (v4 - (_iMult * v2))) + (_iMult * inPhase3);
        quad = v2 - (_qMult * v1) + (_qMult * quad2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _v1Values.TryAdd(v1, out _);
            _inPhaseValues.TryAdd(inPhase, out _);
            _quadValues.TryAdd(quad, out _);
            _index++;
        }
    }

    public void Dispose()
    {
        _values.Dispose();
        _v1Values.Dispose();
        _inPhaseValues.Dispose();
        _quadValues.Dispose();
    }
}

internal sealed class EhlersHilbertTransformerEngine : IDisposable
{
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private double _prevPeak;
    private double _prevReal;
    private double _prevQPeak;

    public EhlersHilbertTransformerEngine(int length1, int length2, InputName inputName)
    {
        _roofingFilter = new EhlersRoofingFilterV2State(length1, length2, inputName);
    }

    public EhlersHilbertTransformerEngine(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        _roofingFilter = new EhlersRoofingFilterV2State(length1, length2, selector);
    }

    public void Reset()
    {
        _roofingFilter.Reset();
        _prevPeak = 0;
        _prevReal = 0;
        _prevQPeak = 0;
    }

    public void Next(OhlcvBar bar, bool isFinal, out double real, out double imag)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var peak = Math.Max(0.991 * _prevPeak, Math.Abs(roofingFilter));
        real = peak != 0 ? roofingFilter / peak : 0;
        var qFilt = real - _prevReal;
        var qPeak = Math.Max(0.991 * _prevQPeak, Math.Abs(qFilt));
        imag = qPeak != 0 ? qFilt / qPeak : 0;

        if (isFinal)
        {
            _prevPeak = peak;
            _prevReal = real;
            _prevQPeak = qPeak;
        }
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
    }
}
