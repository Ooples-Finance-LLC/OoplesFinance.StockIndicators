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
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _bw;
    private readonly EhlersRoofingFilterV2State _roofingFilter;

    // One bandpass per period in the comb, each with its own two-sample recursion and its own history.
    // See the batch calculation. A single shared buffer drove every period from another period's output
    // and summed the deciding power over a mixture of periods.
    private readonly double[] _bpPrev1;
    private readonly double[] _bpPrev2;
    private readonly double[,] _bpHistory;
    private readonly double[] _bpCurrent;
    private readonly double[] _powers;
    private readonly int _ring;
    private double _prevRoofingFilter1;
    private double _prevRoofingFilter2;
    private int _index;

    public EhlersCombFilterSpectralEstimateState(int length1 = 48, int length2 = 10, double bw = 0.3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _bw = bw;
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _ring = _length1;
        _bpPrev1 = new double[_length1 + 1];
        _bpPrev2 = new double[_length1 + 1];
        _bpHistory = new double[_length1 + 1, _ring];
        _bpCurrent = new double[_length1 + 1];
        _powers = new double[_length1 + 1];
    }

    public IndicatorName Name => IndicatorName.EhlersCombFilterSpectralEstimate;

    public void Reset()
    {
        _roofingFilter.Reset();
        Array.Clear(_bpPrev1, 0, _bpPrev1.Length);
        Array.Clear(_bpPrev2, 0, _bpPrev2.Length);
        Array.Clear(_bpHistory, 0, _bpHistory.Length);
        Array.Clear(_bpCurrent, 0, _bpCurrent.Length);
        _prevRoofingFilter1 = 0;
        _prevRoofingFilter2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRoofingFilter2 = _prevRoofingFilter2;

        double maxPwr = 0;
        double spx = 0;
        double sp = 0;
        var slot = _index % _ring;
        for (var j = _length2; j <= _length1; j++)
        {
            var beta = Math.Cos(2 * Math.PI / j);
            var gamma = 1 / Math.Cos(2 * Math.PI * _bw / j);
            var alpha = MathHelper.MinOrMax(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99, 0.01);
            var bp = (0.5 * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) +
                 (beta * (1 + alpha) * _bpPrev1[j]) - (alpha * _bpPrev2[j]);
            _bpCurrent[j] = bp;

            double pwr = 0;
            for (var k = 1; k <= j; k++)
            {
                // This period's own output k bars ago, and every one of them: a power is a sum of
                // squares and cannot depend on the sign of what is squared.
                var prevBp = _index >= k ? _bpHistory[j, ((slot - k) % _ring + _ring) % _ring] : 0;
                pwr += MathHelper.Pow(prevBp / j, 2);
            }

            _powers[j] = pwr;
            maxPwr = Math.Max(pwr, maxPwr);
        }

        for (var j = _length2; j <= _length1; j++)
        {
            var pwr = maxPwr != 0 ? _powers[j] / maxPwr : 0;
            if (pwr >= 0.5)
            {
                spx += j * pwr;
                sp += pwr;
            }
        }

        var domCyc = sp != 0 ? spx / sp : 0;

        if (isFinal)
        {
            _prevRoofingFilter2 = _prevRoofingFilter1;
            _prevRoofingFilter1 = roofingFilter;
            for (var j = _length2; j <= _length1; j++)
            {
                _bpHistory[j, slot] = _bpCurrent[j];
                _bpPrev2[j] = _bpPrev1[j];
                _bpPrev1[j] = _bpCurrent[j];
            }

            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ecfse", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
    }
}

[PrimaryOutput("Eacr")]
public sealed class EhlersAutoCorrelationReversalsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly EhlersAutoCorrelationIndicatorState _autoCorrelation;
    private readonly PooledRingBuffer<double> _corrValues;

    public EhlersAutoCorrelationReversalsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length3 = Math.Max(length3, 1);
        _autoCorrelation = new EhlersAutoCorrelationIndicatorState(_length1, Math.Max(1, length2));
        _corrValues = new PooledRingBuffer<double>(_length1);
    }

    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationReversals;

    public void Reset()
    {
        _autoCorrelation.Reset();
        _corrValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var corr = _autoCorrelation.Update(bar, isFinal, includeOutputs: false).Value;
        var start = _length3;

        double delta = 0;
        for (var j = start; j <= _length1; j++)
        {
            var corrValue = EhlersStreamingWindow.GetOffsetValue(_corrValues, corr, j);
            var prevCorr = EhlersStreamingWindow.GetOffsetValue(_corrValues, corr, j - 1);
            if ((corrValue > 0.5 && prevCorr < 0.5) || (corrValue < 0.5 && prevCorr > 0.5))
            {
                delta += 1;
            }
        }

        var reversal = delta > _length1 / 2.0 ? 1 : 0;

        if (isFinal)
        {
            _corrValues.TryAdd(corr, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eacr", reversal }
            };
        }

        return new StreamingIndicatorStateResult(reversal, outputs);
    }

    public void Dispose()
    {
        _autoCorrelation.Dispose();
        _corrValues.Dispose();
    }
}

[PrimaryOutput("Real")]
public sealed class EhlersClassicHilbertTransformerState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _realValues;
    private double _prevPeak;

    public EhlersClassicHilbertTransformerState(int length1 = 48, int length2 = 10)
    {
        _roofingFilter = new EhlersRoofingFilterV2State(Math.Max(1, length1), Math.Max(1, length2));
        _realValues = new PooledRingBuffer<double>(23);
    }

    public IndicatorName Name => IndicatorName.EhlersClassicHilbertTransformer;

    public void Reset()
    {
        _roofingFilter.Reset();
        _realValues.Clear();
        _prevPeak = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var peak = Math.Max(0.991 * _prevPeak, Math.Abs(roofingFilter));
        var real = peak != 0 ? roofingFilter / peak : 0;

        var prevReal2 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 2);
        var prevReal4 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 4);
        var prevReal6 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 6);
        var prevReal8 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 8);
        var prevReal10 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 10);
        var prevReal12 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 12);
        var prevReal14 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 14);
        var prevReal16 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 16);
        var prevReal18 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 18);
        var prevReal20 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 20);
        var prevReal22 = EhlersStreamingWindow.GetOffsetValue(_realValues, real, 22);

        var imag = ((0.091 * real) + (0.111 * prevReal2) + (0.143 * prevReal4) + (0.2 * prevReal6) +
                    (0.333 * prevReal8) + prevReal10 - prevReal12 - (0.333 * prevReal14) -
                    (0.2 * prevReal16) - (0.143 * prevReal18) - (0.111 * prevReal20) -
                    (0.091 * prevReal22)) / 1.865;

        if (isFinal)
        {
            _prevPeak = peak;
            _realValues.TryAdd(real, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Real", real },
                { "Imag", imag }
            };
        }

        return new StreamingIndicatorStateResult(real, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _realValues.Dispose();
    }
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
    private readonly int _length3;
    private readonly double[] _xWindow;
    private readonly double[] _yWindow;
    private readonly double _alpha;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly List<double> _roofingValues;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevRoofingFilter1;
    private double _prevRoofingFilter2;
    private int _index;

    public EhlersConvolutionIndicatorState(int length1 = 80, int length2 = 40, int length3 = 48)
    {
        _length3 = Math.Max(1, length3);
        _xWindow = new double[_length3]; _yWindow = new double[_length3];
        var piPrd = Math.Min(.99, MathHelper.Sqrt2 * Math.PI / Math.Max(1, length1));
        _alpha = 1-Math.Cos(piPrd)/(1+Math.Sin(piPrd));
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, null);
        _roofingValues = new List<double>(128);
    }

    public IndicatorName Name => IndicatorName.EhlersConvolutionIndicator;

    public void Reset()
    {
        _roofingValues.Clear();
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevRoofingFilter1 = 0;
        _prevRoofingFilter2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevHp1 = _index >= 1 ? _prevHp1 : 0;
        var prevHp2 = _index >= 2 ? _prevHp2 : 0;
        var prevRoofingFilter1 = _prevRoofingFilter1;
        var prevRoofingFilter2 = _prevRoofingFilter2;
        var pow1 = MathHelper.Pow(1 - (_alpha / 2), 2);
        var pow2 = MathHelper.Pow(1 - _alpha, 2);

        var highPass = (pow1 * (value - (2 * prevValue1) + prevValue2)) + (2 * (1 - _alpha) * prevHp1) -
                       (pow2 * prevHp2);
        var roofingFilter = (_c1 * ((highPass + prevHp1) / 2)) + (_c2 * prevRoofingFilter1) + (_c3 * prevRoofingFilter2);

        var n = Math.Min(_index + 1, _length3);
        for (var lag = 0; lag < n; lag++)
        {
            _xWindow[lag] = GetRoofingOffsetValue(roofingFilter, lag);
            _yWindow[lag] = GetRoofingOffsetValue(roofingFilter, lag+1);
        }
        var corr = WindowCorrelation.Pearson(_xWindow.AsSpan(0, n), _yWindow.AsSpan(0, n));
        var expValue = MathHelper.Exp(3 * corr);
        var conv = expValue / (expValue + 1) / 2;

        var filtLength = (int)Math.Ceiling(0.5 * n);
        var prevFilt = GetRoofingOffsetValue(roofingFilter, filtLength);
        var slope = roofingFilter-prevFilt > 1e-12*Math.Max(1, Math.Max(Math.Abs(roofingFilter), Math.Abs(prevFilt))) ? -1 : 1;

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = highPass;
            _prevRoofingFilter2 = _prevRoofingFilter1;
            _prevRoofingFilter1 = roofingFilter;
            _roofingValues.Add(roofingFilter);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eci", conv },
                { "Slope", slope }
            };
        }

        return new StreamingIndicatorStateResult(conv, outputs);
    }

    private double GetRoofingOffsetValue(double pendingValue, int offset)
    {
        if (offset <= 0)
        {
            return pendingValue;
        }

        var index = _roofingValues.Count - offset;
        return index >= 0 ? _roofingValues[index] : 0;
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
