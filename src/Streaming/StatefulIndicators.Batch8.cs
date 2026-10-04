using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Ecti")]
public sealed class EhlersCorrelationTrendIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersCorrelationWindow _correlation;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersCorrelationTrendIndicatorState(int length = 20) => _correlation = new EhlersCorrelationWindow(length, true);
    public IndicatorName Name => IndicatorName.EhlersCorrelationTrendIndicator;

    public void Reset() { _correlation.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var (real, imag) = _correlation.Next(_input.GetValue(bar), isFinal);

        return new StreamingIndicatorStateResult(real, includeOutputs
            ? new Dictionary<string, double> { { "Ecti", real } } : null);
    }
    public void Dispose() => _correlation.Dispose();
}

[PrimaryOutput("Ecog")]
public sealed class EhlersCenterofGravityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly CenterGravityWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public EhlersCenterofGravityOscillatorState(int length=10){_window=new(length);}
    public IndicatorName Name=>IndicatorName.EhlersCenterofGravityOscillator;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {StreamingInputValidation.Validate(bar);var value=_window.Next(_input.GetValue(bar),isFinal);return new(value,includeOutputs?new Dictionary<string,double>{{"Ecog",value}}:null);}
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("FastEdo")]
public sealed class EhlersDecyclerOscillatorV1State : IStreamingIndicatorState
{
    private readonly DecyclerOscillatorWindow _fast, _slow;
    public EhlersDecyclerOscillatorV1State(int fastLength = 100, int slowLength = 125, double fastMult = 1.2, double slowMult = 1)
        : this(new DecyclerOscillatorWindow(fastLength, fastMult), new DecyclerOscillatorWindow(slowLength, slowMult)) { }
    private EhlersDecyclerOscillatorV1State(DecyclerOscillatorWindow fast, DecyclerOscillatorWindow slow) { _fast = fast; _slow = slow; }
    internal static EhlersDecyclerOscillatorV1State ForPeriod(int length) => new(new DecyclerOscillatorWindow(length), new DecyclerOscillatorWindow(length, 1, 2));
    public IndicatorName Name => IndicatorName.EhlersDecyclerOscillatorV1;
    public void Reset() { _fast.Reset(); _slow.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var fast = _fast.Next(bar.Close, isFinal); var slow = _slow.Next(bar.Close, isFinal);
        return new(fast, includeOutputs ? new Dictionary<string, double> { { "FastEdo", fast }, { "SlowEdo", slow } } : null);
    }
}

[PrimaryOutput("Edo")]
public sealed class EhlersDecyclerOscillatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly DecyclerV2Window _window;
    public EhlersDecyclerOscillatorV2State(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int fastLength = 10, int slowLength = 20)
        => _window = new(maType, fastLength, slowLength);
    public IndicatorName Name => IndicatorName.EhlersDecyclerOscillatorV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Edo", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ed")]
public sealed class EhlersDecyclerState : IStreamingIndicatorState
{
    private readonly DecyclerWindow _window;
    public EhlersDecyclerState(int length = 60) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersDecycler;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ed", value } } : null);
    }
}

[PrimaryOutput("Real")]
public sealed class EhlersCorrelationCycleIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersCorrelationWindow _correlation;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersCorrelationCycleIndicatorState(int length = 20) => _correlation = new EhlersCorrelationWindow(length, false);
    public IndicatorName Name => IndicatorName.EhlersCorrelationCycleIndicator;
    internal double LastImag { get; private set; }
    public void Reset() { _correlation.Reset(); LastImag = 0; }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var (real, imag) = _correlation.Next(_input.GetValue(bar), isFinal);
        LastImag = imag;
        return new StreamingIndicatorStateResult(real, includeOutputs
            ? new Dictionary<string, double> { { "Real", real }, { "Imag", imag } } : null);
    }
    public void Dispose() => _correlation.Dispose();
}

[PrimaryOutput("Cai")]
public sealed class EhlersCorrelationAngleIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersCorrelationCycleIndicatorState _cycle;
    private double _prevAngle;
    private bool _hasPrev;

    public EhlersCorrelationAngleIndicatorState(int length = 20)
    {
        _cycle = new EhlersCorrelationCycleIndicatorState(length);
    }

    public IndicatorName Name => IndicatorName.EhlersCorrelationAngleIndicator;

    public void Reset()
    {
        _cycle.Reset();
        _prevAngle = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var real = _cycle.Update(bar, isFinal, includeOutputs: false).Value;
        var imag = _cycle.LastImag;
        var prevAngle = _hasPrev ? _prevAngle : 0;
        var angle = EhlersCorrelationPhase.Angle(real, imag);
        angle = prevAngle - angle < 270 && angle < prevAngle ? prevAngle : angle;

        if (isFinal)
        {
            _prevAngle = angle;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cai", angle }
            };
        }

        return new StreamingIndicatorStateResult(angle, outputs);
    }

    public void Dispose()
    {
        _cycle.Dispose();
    }
}

[PrimaryOutput("Ecfse")]
public sealed class EhlersCombFilterSpectralEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly CombSpectrumWindow _window;
    public EhlersCombFilterSpectralEstimateState(int length1 = 48, int length2 = 10, double bw = 0.3) => _window = new(length1, length2, bw);
    public IndicatorName Name => IndicatorName.EhlersCombFilterSpectralEstimate;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ecfse", value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Eacr")]
public sealed class EhlersAutoCorrelationReversalsState : IStreamingIndicatorState, IDisposable
{
    private readonly AutocorrelationReversalWindow _window;
    public EhlersAutoCorrelationReversalsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 48, int length2 = 10, int length3 = 3) => _window = new(maType, length1, length2, length3, true);
    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationReversals;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal, 0);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eacr", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Real")]
public sealed class EhlersClassicHilbertTransformerState : IStreamingIndicatorState, IDisposable
{
    private readonly ClassicHilbertWindow _window;
    public EhlersClassicHilbertTransformerState(int length1 = 48, int length2 = 10) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.EhlersClassicHilbertTransformer;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Real, includeOutputs ? new Dictionary<string, double> { { "Real", point.Real }, { "Imag", point.Imaginary } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Ebpf")]
public sealed class EhlersBandPassFilterV1State : IStreamingIndicatorState
{
    private readonly ClampedBandPassWindow _window;
    public EhlersBandPassFilterV1State(int length = 20, double bw = .3) { _window = new(length, bw, 0); }
    public IndicatorName Name => IndicatorName.EhlersBandPassFilterV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "Ebpf", p.Value }, { "Signal", p.Signal } } : null);
    }
}

[PrimaryOutput("Ebpf")]
public sealed class EhlersBandPassFilterV2State : IStreamingIndicatorState
{
    private readonly ClampedBandPassWindow _window;
    public EhlersBandPassFilterV2State(int length = 20, double bw = .3) { _window = new(length, bw, 1); }
    public IndicatorName Name => IndicatorName.EhlersBandPassFilterV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "Ebpf", p.Value } } : null);
    }
}

[PrimaryOutput("Ecbpf")]
public sealed class EhlersCycleBandPassFilterState : IStreamingIndicatorState
{
    private readonly ClampedBandPassWindow _window;
    public EhlersCycleBandPassFilterState(int length = 20, double delta = .1) { _window = new(length, delta, 2); }
    public IndicatorName Name => IndicatorName.EhlersCycleBandPassFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "Ecbpf", p.Value } } : null);
    }
}

[PrimaryOutput("Eca")]
public sealed class EhlersCycleAmplitudeState : IStreamingIndicatorState, IDisposable
{
    private readonly CycleAmplitudeWindow _window;
    public EhlersCycleAmplitudeState(int length = 20, double delta = .1) { _window = new(length, delta); }
    public IndicatorName Name => IndicatorName.EhlersCycleAmplitude;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Eca", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ecc")]
public sealed class EhlersCyberCycleState : IStreamingIndicatorState
{
    private readonly CyberCycleWindow _window;
    public EhlersCyberCycleState(double alpha = .07) => _window = new(alpha);
    public IndicatorName Name => IndicatorName.EhlersCyberCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string,double> { { "Ecc", value } } : null);
    }
}


[PrimaryOutput("Eci")]
public sealed class EhlersConvolutionIndicatorState : IStreamingIndicatorState
{
    private readonly EhlersConvolutionWindow _window;
    public EhlersConvolutionIndicatorState(int length1 = 80, int length2 = 40, int length3 = 48) => _window = new(length1, length2, length3);
    public IndicatorName Name => IndicatorName.EhlersConvolutionIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Eci", point.Value }, { "Slope", point.Slope } } : null);
    }
}

[PrimaryOutput("Eiftcci")]
public sealed class EhlersCommodityChannelIndexInverseFisherTransformState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly CommodityChannelIndexState _cciState;
    private readonly StrengthAverage? _exactAverage;
    private readonly IMovingAverageSmoother _signalSmoother;

    public EhlersCommodityChannelIndexInverseFisherTransformState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20, int signalLength = 9,
        double constant = 0.015)
    {
        CommodityIndexWindow.ValidateConstant(constant);
        if (StrengthWindow.Supports(maType)) _exactAverage = new StrengthAverage(maType, signalLength);
        _cciState = new CommodityChannelIndexState(maType, Math.Max(1, length), constant);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;

    // No resolver of its own: the input was handed to these inner states, so they are the
    // ones that must switch to reading the close.
    void ICustomInputConsumer.ReadCloseAsInput()
    {
        ((ICustomInputConsumer)_cciState).ReadCloseAsInput();
    }

    public void Reset()
    {
        _exactAverage?.Reset();
        _cciState.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var cci = _cciState.Update(bar, isFinal, includeOutputs: false).Value;
        var v1 = 0.1 * (cci - 50);
        var v2 = _exactAverage is null ? _signalSmoother.Next(v1, isFinal) : _exactAverage.Next(new StrengthValue(v1), isFinal).Mantissa;
        var iFish = Math.Tanh(v2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eiftcci", iFish }
            };
        }

        return new StreamingIndicatorStateResult(iFish, outputs);
    }

    public void Dispose()
    {
        _exactAverage?.Dispose();
        _cciState.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Eaef")]
public sealed class EhlersAverageErrorFilterState : IStreamingIndicatorState
{
    private readonly AverageErrorWindow _window; private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersAverageErrorFilterState(int length = 27) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersAverageErrorFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Eaef", value } } : null);
    }
}

[PrimaryOutput("Eclpf-2")]
public sealed class EhlersChebyshevLowPassFilterState : IStreamingIndicatorState
{
    private readonly ChebyshevWaveWindow[] _windows = Enumerable.Range(0, 9).Select(w => new ChebyshevWaveWindow(w)).ToArray();
    public EhlersChebyshevLowPassFilterState() { }
    public IndicatorName Name => IndicatorName.EhlersChebyshevLowPassFilter;
    public void Reset() { foreach (var window in _windows) window.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        Dictionary<string, double>? outputs = includeOutputs ? new() : null; double primary = 0;
        for (var wave = 0; wave < _windows.Length; wave++)
        {
            var value = _windows[wave].Next(bar.Close, isFinal); if (wave == 0) primary = value;
            if (outputs is not null) outputs["Eclpf" + (wave - 2)] = value;
        }
        return new(primary, outputs);
    }
}


[PrimaryOutput("Ebema")]
public sealed class EhlersBetterExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly BetterEmaWindow _window;
    public EhlersBetterExponentialMovingAverageState(int length = 20) => _window = new(length);
    public IndicatorName Name => IndicatorName.EhlersBetterExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ebema", value } } : null);
    }
}

internal sealed class HighPassFilterV1Engine
{
    private readonly HighPassWindow _window;
    public HighPassFilterV1Engine(int length, double mult) => _window = new(length, mult);
    public void Reset() => _window.Reset();
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
}

internal sealed class HighPassFilterV2Engine : IDisposable
{
    private readonly HighPassV2Window _window;
    public HighPassFilterV2Engine(MovingAvgType maType, int length) => _window = new(maType, length);
    public void Reset() => _window.Reset();
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal).Publish();
    public void Dispose() => _window.Dispose();
}
