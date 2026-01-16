using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class EhlersCorrelationTrendIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _sy;
    private readonly double _syy;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersCorrelationTrendIndicatorState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        (_sy, _syy) = BuildYAxisSums(_length);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersCorrelationTrendIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        (_sy, _syy) = BuildYAxisSums(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersCorrelationTrendIndicator;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sx = 0;
        double sxx = 0;
        double sxy = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var x = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var y = -j;
            sx += x;
            sxx += x * x;
            sxy += x * y;
        }

        var denom = ((_length * sxx) - (sx * sx)) * ((_length * _syy) - (_sy * _sy));
        var corr = denom > 0
            ? ((_length * sxy) - (sx * _sy)) / MathHelper.Sqrt(denom)
            : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ecti", corr }
            };
        }

        return new StreamingIndicatorStateResult(corr, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }

    private static (double sy, double syy) BuildYAxisSums(int length)
    {
        double sy = 0;
        double syy = 0;
        for (var j = 0; j <= length - 1; j++)
        {
            var y = -j;
            sy += y;
            syy += y * y;
        }

        return (sy, syy);
    }
}

public sealed class EhlersCenterofGravityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _halfLength;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersCenterofGravityOscillatorState(int length = 10, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _halfLength = (_length + 1) / 2.0;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersCenterofGravityOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _halfLength = (_length + 1) / 2.0;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersCenterofGravityOscillator;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double num = 0;
        double denom = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var weight = 1 + j;
            num += weight * prevValue;
            denom += prevValue;
        }

        var cg = denom != 0 ? (-num / denom) + _halfLength : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ecog", cg }
            };
        }

        return new StreamingIndicatorStateResult(cg, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersDecyclerOscillatorV1State : IStreamingIndicatorState
{
    private readonly double _fastMult;
    private readonly double _slowMult;
    private readonly HighPassFilterV1Engine _fastHp;
    private readonly HighPassFilterV1Engine _slowHp;
    private readonly HighPassFilterV1Engine _fastDecHp;
    private readonly HighPassFilterV1Engine _slowDecHp;
    private readonly StreamingInputResolver _input;

    public EhlersDecyclerOscillatorV1State(int fastLength = 100, int slowLength = 125,
        double fastMult = 1.2, double slowMult = 1, InputName inputName = InputName.Close)
    {
        _fastMult = fastMult;
        _slowMult = slowMult;
        _fastHp = new HighPassFilterV1Engine(fastLength, 1);
        _slowHp = new HighPassFilterV1Engine(slowLength, 1);
        _fastDecHp = new HighPassFilterV1Engine(fastLength, 0.5);
        _slowDecHp = new HighPassFilterV1Engine(slowLength, 0.5);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersDecyclerOscillatorV1State(int fastLength, int slowLength, double fastMult, double slowMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastMult = fastMult;
        _slowMult = slowMult;
        _fastHp = new HighPassFilterV1Engine(fastLength, 1);
        _slowHp = new HighPassFilterV1Engine(slowLength, 1);
        _fastDecHp = new HighPassFilterV1Engine(fastLength, 0.5);
        _slowDecHp = new HighPassFilterV1Engine(slowLength, 0.5);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersDecyclerOscillatorV1;

    public void Reset()
    {
        _fastHp.Reset();
        _slowHp.Reset();
        _fastDecHp.Reset();
        _slowDecHp.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fastHp = _fastHp.Next(value, isFinal);
        var slowHp = _slowHp.Next(value, isFinal);
        var fastDec = value - fastHp;
        var slowDec = value - slowHp;
        var fastFiltered = _fastDecHp.Next(fastDec, isFinal);
        var slowFiltered = _slowDecHp.Next(slowDec, isFinal);

        var fastOsc = value != 0 ? 100 * _fastMult * fastFiltered / value : 0;
        var slowOsc = value != 0 ? 100 * _slowMult * slowFiltered / value : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "FastEdo", fastOsc },
                { "SlowEdo", slowOsc }
            };
        }

        return new StreamingIndicatorStateResult(slowOsc, outputs);
    }
}

public sealed class EhlersDecyclerOscillatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly HighPassFilterV2Engine _fastHp;
    private readonly HighPassFilterV2Engine _slowHp;
    private readonly StreamingInputResolver _input;

    public EhlersDecyclerOscillatorV2State(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 20, InputName inputName = InputName.Close)
    {
        _fastHp = new HighPassFilterV2Engine(maType, fastLength);
        _slowHp = new HighPassFilterV2Engine(maType, slowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersDecyclerOscillatorV2State(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastHp = new HighPassFilterV2Engine(maType, fastLength);
        _slowHp = new HighPassFilterV2Engine(maType, slowLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersDecyclerOscillatorV2;

    public void Reset()
    {
        _fastHp.Reset();
        _slowHp.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hp1 = _fastHp.Next(value, isFinal);
        var hp2 = _slowHp.Next(value, isFinal);
        var dec = hp2 - hp1;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edo", dec }
            };
        }

        return new StreamingIndicatorStateResult(dec, outputs);
    }

    public void Dispose()
    {
        _fastHp.Dispose();
        _slowHp.Dispose();
    }
}

public sealed class EhlersDecyclerState : IStreamingIndicatorState
{
    private readonly double _alpha1;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevDec;
    private int _index;

    public EhlersDecyclerState(int length = 60, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var alphaArg = Math.Min(2 * Math.PI / resolved, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersDecyclerState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var alphaArg = Math.Min(2 * Math.PI / resolved, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersDecycler;

    public void Reset()
    {
        _prevValue = 0;
        _prevDec = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue : 0;
        var prevDec = _prevDec;
        var dec = (_alpha1 / 2 * (value + prevValue1)) + ((1 - _alpha1) * prevDec);

        if (isFinal)
        {
            _prevValue = value;
            _prevDec = dec;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ed", dec }
            };
        }

        return new StreamingIndicatorStateResult(dec, outputs);
    }
}

public sealed class EhlersCorrelationCycleIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _sy;
    private readonly double _syy;
    private readonly double _nsy;
    private readonly double _nsyy;
    private readonly double[] _cosValues;
    private readonly double[] _negSinValues;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersCorrelationCycleIndicatorState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _cosValues = new double[_length];
        _negSinValues = new double[_length];
        (_sy, _syy, _nsy, _nsyy) = BuildTrigonometricSums(_length, _cosValues, _negSinValues);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersCorrelationCycleIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _cosValues = new double[_length];
        _negSinValues = new double[_length];
        (_sy, _syy, _nsy, _nsyy) = BuildTrigonometricSums(_length, _cosValues, _negSinValues);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersCorrelationCycleIndicator;

    internal double LastImag { get; private set; }

    public void Reset()
    {
        _values.Clear();
        LastImag = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sx = 0;
        double sxx = 0;
        double sxy = 0;
        double nsxy = 0;
        for (var j = 1; j <= _length; j++)
        {
            var x = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            var y = _cosValues[j - 1];
            var ny = _negSinValues[j - 1];
            sx += x;
            sxx += x * x;
            sxy += x * y;
            nsxy += x * ny;
        }

        var realDenom = ((_length * sxx) - (sx * sx)) * ((_length * _syy) - (_sy * _sy));
        var real = realDenom > 0
            ? ((_length * sxy) - (sx * _sy)) / MathHelper.Sqrt(realDenom)
            : 0;

        var imagDenom = ((_length * sxx) - (sx * sx)) * ((_length * _nsyy) - (_nsy * _nsy));
        var imag = imagDenom > 0
            ? ((_length * nsxy) - (sx * _nsy)) / MathHelper.Sqrt(imagDenom)
            : 0;
        LastImag = imag;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
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
        _values.Dispose();
    }

    private static (double sy, double syy, double nsy, double nsyy) BuildTrigonometricSums(
        int length, double[] cosValues, double[] negSinValues)
    {
        double sy = 0;
        double syy = 0;
        double nsy = 0;
        double nsyy = 0;
        for (var j = 1; j <= length; j++)
        {
            var v = MathHelper.MinOrMax(2 * Math.PI * ((double)(j - 1) / length), 0.99, 0.01);
            var cos = Math.Cos(v);
            var negSin = -Math.Sin(v);
            cosValues[j - 1] = cos;
            negSinValues[j - 1] = negSin;
            sy += cos;
            syy += cos * cos;
            nsy += negSin;
            nsyy += negSin * negSin;
        }

        return (sy, syy, nsy, nsyy);
    }
}

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
        var real = _cycle.Update(bar, isFinal, includeOutputs: false).Value;
        var imag = _cycle.LastImag;
        var prevAngle = _hasPrev ? _prevAngle : 0;
        var angle = imag != 0 ? 90 + Math.Atan(real / imag).ToDegrees() : 900;
        angle = imag > 0 ? angle - 180 : angle;
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

public sealed class EhlersCombFilterSpectralEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _bw;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _bpValues;
    private double _prevRoofingFilter1;
    private double _prevRoofingFilter2;
    private double _prevBp1;
    private double _prevBp2;

    public EhlersCombFilterSpectralEstimateState(int length1 = 48, int length2 = 10, double bw = 0.3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _bw = bw;
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _bpValues = new PooledRingBuffer<double>(_length1);
    }

    public IndicatorName Name => IndicatorName.EhlersCombFilterSpectralEstimate;

    public void Reset()
    {
        _roofingFilter.Reset();
        _bpValues.Clear();
        _prevRoofingFilter1 = 0;
        _prevRoofingFilter2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRoofingFilter2 = _prevRoofingFilter2;
        var prevBp1 = _prevBp1;
        var prevBp2 = _prevBp2;

        double bp = 0;
        double maxPwr = 0;
        double spx = 0;
        double sp = 0;
        for (var j = _length2; j <= _length1; j++)
        {
            var beta = Math.Cos(2 * Math.PI / j);
            var gamma = 1 / Math.Cos(2 * Math.PI * _bw / j);
            var alpha = MathHelper.MinOrMax(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99, 0.01);
            bp = (0.5 * (1 - alpha) * (roofingFilter - prevRoofingFilter2)) +
                 (beta * (1 + alpha) * prevBp1) - (alpha * prevBp2);

            double pwr = 0;
            for (var k = 1; k <= j; k++)
            {
                var prevBp = EhlersStreamingWindow.GetOffsetValue(_bpValues, k);
                if (prevBp >= 0)
                {
                    pwr += MathHelper.Pow(prevBp / j, 2);
                }
            }

            maxPwr = Math.Max(pwr, maxPwr);
            pwr = maxPwr != 0 ? pwr / maxPwr : 0;
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
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _bpValues.TryAdd(bp, out _);
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
        _bpValues.Dispose();
    }
}

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
        _length3 = Math.Max(length3, 0);
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
        var corr = _autoCorrelation.Update(bar, isFinal, includeOutputs: false).Value;
        var start = _length3;
        if (start < 0)
        {
            start = 0;
        }

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

public sealed class EhlersBandPassFilterV1State : IStreamingIndicatorState
{
    private readonly double _alpha1;
    private readonly double _alpha2;
    private readonly double _alpha3;
    private readonly double _beta;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevBp1;
    private double _prevBp2;
    private double _prevPeak;
    private double _prevSig;
    private double _prevTrigger;
    private int _index;

    public EhlersBandPassFilterV1State(int length = 20, double bw = 0.3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var twoPiPrd1 = MathHelper.MinOrMax(0.25 * bw * 2 * Math.PI / resolved, 0.99, 0.01);
        var twoPiPrd2 = MathHelper.MinOrMax(1.5 * bw * 2 * Math.PI / resolved, 0.99, 0.01);
        _beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(2 * Math.PI * bw / resolved, 0.99, 0.01));
        _alpha1 = gamma - MathHelper.Sqrt(MathHelper.Pow(gamma, 2) - 1);
        _alpha2 = (Math.Cos(twoPiPrd1) + Math.Sin(twoPiPrd1) - 1) / Math.Cos(twoPiPrd1);
        _alpha3 = (Math.Cos(twoPiPrd2) + Math.Sin(twoPiPrd2) - 1) / Math.Cos(twoPiPrd2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersBandPassFilterV1State(int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var twoPiPrd1 = MathHelper.MinOrMax(0.25 * bw * 2 * Math.PI / resolved, 0.99, 0.01);
        var twoPiPrd2 = MathHelper.MinOrMax(1.5 * bw * 2 * Math.PI / resolved, 0.99, 0.01);
        _beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(2 * Math.PI * bw / resolved, 0.99, 0.01));
        _alpha1 = gamma - MathHelper.Sqrt(MathHelper.Pow(gamma, 2) - 1);
        _alpha2 = (Math.Cos(twoPiPrd1) + Math.Sin(twoPiPrd1) - 1) / Math.Cos(twoPiPrd1);
        _alpha3 = (Math.Cos(twoPiPrd2) + Math.Sin(twoPiPrd2) - 1) / Math.Cos(twoPiPrd2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersBandPassFilterV1;

    public void Reset()
    {
        _prevValue = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _prevPeak = 0;
        _prevSig = 0;
        _prevTrigger = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevHp1 = _index >= 1 ? _prevHp1 : 0;
        var prevHp2 = _index >= 2 ? _prevHp2 : 0;
        var prevBp1 = _index >= 1 ? _prevBp1 : 0;
        var prevBp2 = _index >= 2 ? _prevBp2 : 0;
        var prevSig = _prevSig;
        var prevTrigger = _prevTrigger;
        var diff = _index >= 1 ? value - prevValue : 0;

        var hp = ((1 + (_alpha2 / 2)) * diff) + ((1 - _alpha2) * prevHp1);
        var bp = _index > 2
            ? (0.5 * (1 - _alpha1) * (hp - prevHp2)) + (_beta * (1 + _alpha1) * prevBp1) - (_alpha1 * prevBp2)
            : 0;

        var peak = Math.Max(0.991 * _prevPeak, Math.Abs(bp));
        var sig = peak != 0 ? bp / peak : 0;
        var trigger = ((1 + (_alpha3 / 2)) * (sig - prevSig)) + ((1 - _alpha3) * prevTrigger);

        if (isFinal)
        {
            _prevValue = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _prevPeak = peak;
            _prevSig = sig;
            _prevTrigger = trigger;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ebpf", sig },
                { "Signal", trigger }
            };
        }

        return new StreamingIndicatorStateResult(sig, outputs);
    }
}

public sealed class EhlersBandPassFilterV2State : IStreamingIndicatorState
{
    private readonly double _l1;
    private readonly double _s1;
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevBp1;
    private double _prevBp2;
    private int _index;

    public EhlersBandPassFilterV2State(int length = 20, double bw = 0.3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / resolved, 0.99, 0.01));
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / MathHelper.Pow(g1, 2)) - 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersBandPassFilterV2State(int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / resolved, 0.99, 0.01));
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / MathHelper.Pow(g1, 2)) - 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersBandPassFilterV2;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevBp1 = _index >= 1 ? _prevBp1 : 0;
        var prevBp2 = _index >= 2 ? _prevBp2 : 0;
        var bp = _index < 3
            ? 0
            : (0.5 * (1 - _s1) * (value - prevValue2)) + (_l1 * (1 + _s1) * prevBp1) - (_s1 * prevBp2);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ebpf", bp }
            };
        }

        return new StreamingIndicatorStateResult(bp, outputs);
    }
}

public sealed class EhlersCycleBandPassFilterState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _beta;
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevBp1;
    private double _prevBp2;
    private int _index;

    public EhlersCycleBandPassFilterState(int length = 20, double delta = 0.1, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(4 * Math.PI * delta / resolved, 0.99, 0.01));
        _alpha = gamma - MathHelper.Sqrt(MathHelper.Pow(gamma, 2) - 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersCycleBandPassFilterState(int length, double delta, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(4 * Math.PI * delta / resolved, 0.99, 0.01));
        _alpha = gamma - MathHelper.Sqrt(MathHelper.Pow(gamma, 2) - 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersCycleBandPassFilter;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevBp1 = _index >= 1 ? _prevBp1 : 0;
        var prevBp2 = _index >= 2 ? _prevBp2 : 0;
        var diff = _index >= 2 ? value - prevValue2 : 0;
        var bp = (0.5 * (1 - _alpha) * diff) + (_beta * (1 + _alpha) * prevBp1) - (_alpha * prevBp2);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ecbpf", bp }
            };
        }

        return new StreamingIndicatorStateResult(bp, outputs);
    }
}

public sealed class EhlersCycleAmplitudeState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly EhlersCycleBandPassFilterState _bpState;

    public EhlersCycleAmplitudeState(int length = 20, double delta = 0.1)
    {
        _length = Math.Max(1, length);
        _bpState = new EhlersCycleBandPassFilterState(_length, delta);
    }

    public IndicatorName Name => IndicatorName.EhlersCycleAmplitude;

    public void Reset()
    {
        _bpState.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _bpState.Update(bar, isFinal, includeOutputs: false).Value;
        var ptop = 2 * MathHelper.Sqrt2 * MathHelper.Sqrt(0 / (double)_length);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eca", ptop }
            };
        }

        return new StreamingIndicatorStateResult(ptop, outputs);
    }

}

public sealed class EhlersCyberCycleState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevValue3;
    private double _prevSmooth1;
    private double _prevSmooth2;
    private double _prevCycle1;
    private double _prevCycle2;
    private int _index;

    public EhlersCyberCycleState(double alpha = 0.07, InputName inputName = InputName.Close)
    {
        _alpha = alpha;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersCyberCycleState(double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = alpha;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersCyberCycle;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevValue3 = 0;
        _prevSmooth1 = 0;
        _prevSmooth2 = 0;
        _prevCycle1 = 0;
        _prevCycle2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevValue3 = _index >= 3 ? _prevValue3 : 0;
        var prevSmooth1 = _index >= 1 ? _prevSmooth1 : 0;
        var prevSmooth2 = _index >= 2 ? _prevSmooth2 : 0;
        var prevCycle1 = _index >= 1 ? _prevCycle1 : 0;
        var prevCycle2 = _index >= 2 ? _prevCycle2 : 0;

        var smooth = (value + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;
        var cycle = _index < 7
            ? (value - (2 * prevValue1) + prevValue2) / 4
            : (MathHelper.Pow(1 - (0.5 * _alpha), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2)) +
              (2 * (1 - _alpha) * prevCycle1) - (MathHelper.Pow(1 - _alpha, 2) * prevCycle2);

        if (isFinal)
        {
            _prevValue3 = _prevValue2;
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevSmooth2 = _prevSmooth1;
            _prevSmooth1 = smooth;
            _prevCycle2 = _prevCycle1;
            _prevCycle1 = cycle;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ecc", cycle }
            };
        }

        return new StreamingIndicatorStateResult(cycle, outputs);
    }
}

public sealed class EhlersConvolutionIndicatorState : IStreamingIndicatorState
{
    private readonly int _length3;
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

    public EhlersConvolutionIndicatorState(int length1 = 80, int length2 = 40, int length3 = 48,
        InputName inputName = InputName.Close)
    {
        _length3 = Math.Max(1, length3);
        var piPrd = MathHelper.Sqrt2 * Math.PI / Math.Max(1, length1);
        _alpha = (Math.Cos(piPrd) + Math.Sin(piPrd) - 1) / Math.Cos(piPrd);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _roofingValues = new List<double>(128);
    }

    public EhlersConvolutionIndicatorState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length3 = Math.Max(1, length3);
        var piPrd = MathHelper.Sqrt2 * Math.PI / Math.Max(1, length1);
        _alpha = (Math.Cos(piPrd) + Math.Sin(piPrd) - 1) / Math.Cos(piPrd);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / Math.Max(1, length2));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
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

        var n = _index + 1;
        double sx = 0;
        double sy = 0;
        double sxx = 0;
        double syy = 0;
        double sxy = 0;
        for (var j = 1; j <= _length3; j++)
        {
            var x = GetRoofingOffsetValue(roofingFilter, j - 1);
            var y = GetRoofingOffsetValue(roofingFilter, j);
            sx += x;
            sy += y;
            sxx += x * x;
            sxy += x * y;
            syy += y * y;
        }

        var denom = ((n * sxx) - (sx * sx)) * ((n * syy) - (sy * sy));
        var corr = denom > 0 ? (((n * sxy) - (sx * sy)) / MathHelper.Sqrt(denom)) : 0;
        var expValue = MathHelper.Exp(3 * corr);
        var conv = (1 + (expValue - 1)) / (expValue + 1) / 2;

        var filtLength = (int)Math.Ceiling(0.5 * n);
        var prevFilt = GetRoofingOffsetValue(roofingFilter, filtLength);
        var slope = prevFilt < roofingFilter ? -1 : 1;

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

public sealed class EhlersCommodityChannelIndexInverseFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly CommodityChannelIndexState _cciState;
    private readonly IMovingAverageSmoother _signalSmoother;

    public EhlersCommodityChannelIndexInverseFisherTransformState(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20, int signalLength = 9,
        double constant = 0.015)
    {
        _cciState = new CommodityChannelIndexState(inputName, maType, Math.Max(1, length), constant);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public EhlersCommodityChannelIndexInverseFisherTransformState(InputName inputName, MovingAvgType maType,
        int length, int signalLength, double constant, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _cciState = new CommodityChannelIndexState(inputName, maType, Math.Max(1, length), constant, selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;

    public void Reset()
    {
        _cciState.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var cci = _cciState.Update(bar, isFinal, includeOutputs: false).Value;
        var v1 = 0.1 * (cci - 50);
        var v2 = _signalSmoother.Next(v1, isFinal);
        var expValue = MathHelper.Exp(2 * v2);
        var iFish = expValue + 1 != 0 ? (expValue - 1) / (expValue + 1) : 0;

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
        _cciState.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersAverageErrorFilterState : IStreamingIndicatorState
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevE11;
    private double _prevE12;
    private double _prevSsf1;
    private double _prevSsf2;
    private int _index;

    public EhlersAverageErrorFilterState(int length = 27, InputName inputName = InputName.Close)
    {
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / Math.Max(1, length), -0.01, -0.999));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / Math.Max(1, length), 0.99, 0.01));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersAverageErrorFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / Math.Max(1, length), -0.01, -0.999));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / Math.Max(1, length), 0.99, 0.01));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersAverageErrorFilter;

    public void Reset()
    {
        _prevValue = 0;
        _prevE11 = 0;
        _prevE12 = 0;
        _prevSsf1 = 0;
        _prevSsf2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevE11 = _index >= 1 ? _prevE11 : 0;
        var prevE12 = _index >= 2 ? _prevE12 : 0;
        var prevSsf1 = _index >= 1 ? _prevSsf1 : 0;
        var prevSsf2 = _index >= 2 ? _prevSsf2 : 0;

        var ssf = _index < 3
            ? value
            : (0.5 * _c1 * (value + prevValue)) + (_c2 * prevSsf1) + (_c3 * prevSsf2);
        var e1 = _index < 3 ? 0 : (_c1 * (value - ssf)) + (_c2 * prevE11) + (_c3 * prevE12);
        var filt = ssf + e1;

        if (isFinal)
        {
            _prevValue = value;
            _prevE12 = _prevE11;
            _prevE11 = e1;
            _prevSsf2 = _prevSsf1;
            _prevSsf1 = ssf;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eaef", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

public sealed class EhlersChebyshevLowPassFilterState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevV1Neg2_1;
    private double _prevV1Neg2_2;
    private double _prevWaveNeg2_1;
    private double _prevWaveNeg2_2;
    private double _prevV1Neg1_1;
    private double _prevV1Neg1_2;
    private double _prevWaveNeg1_1;
    private double _prevWaveNeg1_2;
    private double _prevV10_1;
    private double _prevV10_2;
    private double _prevWave0_1;
    private double _prevWave0_2;
    private double _prevV11_1;
    private double _prevV11_2;
    private double _prevWave1_1;
    private double _prevWave1_2;
    private double _prevV12_1;
    private double _prevV12_2;
    private double _prevWave2_1;
    private double _prevWave2_2;
    private double _prevV13_1;
    private double _prevV13_2;
    private double _prevWave3_1;
    private double _prevWave3_2;
    private double _prevV14_1;
    private double _prevV14_2;
    private double _prevWave4_1;
    private double _prevWave4_2;
    private double _prevV15_1;
    private double _prevV15_2;
    private double _prevWave5_1;
    private double _prevWave5_2;
    private double _prevV16_1;
    private double _prevV16_2;
    private double _prevWave6_1;
    private double _prevWave6_2;
    private int _index;

    public EhlersChebyshevLowPassFilterState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersChebyshevLowPassFilterState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersChebyshevLowPassFilter;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevV1Neg2_1 = 0;
        _prevV1Neg2_2 = 0;
        _prevWaveNeg2_1 = 0;
        _prevWaveNeg2_2 = 0;
        _prevV1Neg1_1 = 0;
        _prevV1Neg1_2 = 0;
        _prevWaveNeg1_1 = 0;
        _prevWaveNeg1_2 = 0;
        _prevV10_1 = 0;
        _prevV10_2 = 0;
        _prevWave0_1 = 0;
        _prevWave0_2 = 0;
        _prevV11_1 = 0;
        _prevV11_2 = 0;
        _prevWave1_1 = 0;
        _prevWave1_2 = 0;
        _prevV12_1 = 0;
        _prevV12_2 = 0;
        _prevWave2_1 = 0;
        _prevWave2_2 = 0;
        _prevV13_1 = 0;
        _prevV13_2 = 0;
        _prevWave3_1 = 0;
        _prevWave3_2 = 0;
        _prevV14_1 = 0;
        _prevV14_2 = 0;
        _prevWave4_1 = 0;
        _prevWave4_2 = 0;
        _prevV15_1 = 0;
        _prevV15_2 = 0;
        _prevWave5_1 = 0;
        _prevWave5_2 = 0;
        _prevV16_1 = 0;
        _prevV16_2 = 0;
        _prevWave6_1 = 0;
        _prevWave6_2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;

        var v1Neg2 = (0.080778 * (value + (1.907 * prevValue1) + prevValue2)) +
                     (0.293 * _prevV1Neg2_1) - (0.063 * _prevV1Neg2_2);
        var waveNeg2 = v1Neg2 + (0.513 * _prevV1Neg2_1) + _prevV1Neg2_2 +
                       (0.4451 * _prevWaveNeg2_1) - (0.481 * _prevWaveNeg2_2);

        var v1Neg1 = (0.021394 * (value + (1.777 * prevValue1) + prevValue2)) +
                     (0.731 * _prevV1Neg1_1) - (0.166 * _prevV1Neg1_2);
        var waveNeg1 = v1Neg1 + (0.977 * _prevV1Neg1_1) + _prevV1Neg1_2 +
                       (1.0008 * _prevWaveNeg1_1) - (0.561 * _prevWaveNeg1_2);

        var v10 = (0.0095822 * (value + (1.572 * prevValue1) + prevValue2)) +
                  (1.026 * _prevV10_1) - (0.282 * _prevV10_2);
        var wave0 = v10 + (0.356 * _prevV10_1) + _prevV10_2 +
                    (1.329 * _prevWave0_1) - (0.644 * _prevWave0_2);

        var v11 = (0.00461 * (value + (1.192 * prevValue1) + prevValue2)) +
                  (1.281 * _prevV11_1) - (0.426 * _prevV11_2);
        var wave1 = v11 - (0.384 * _prevV11_1) + _prevV11_2 +
                    (1.565 * _prevWave1_1) - (0.729 * _prevWave1_2);

        var v12 = (0.0026947 * (value + (0.681 * prevValue1) + prevValue2)) +
                  (1.46 * _prevV12_1) - (0.543 * _prevV12_2);
        var wave2 = v12 - (0.966 * _prevV12_1) + _prevV12_2 +
                    (1.703 * _prevWave2_1) - (0.793 * _prevWave2_2);

        var v13 = (0.0017362 * (value + (0.012 * prevValue1) + prevValue2)) +
                  (1.606 * _prevV13_1) - (0.65 * _prevV13_2);
        var wave3 = v13 - (1.408 * _prevV13_1) + _prevV13_2 +
                    (1.801 * _prevWave3_1) - (0.848 * _prevWave3_2);

        var v14 = (0.0013738 * (value - (0.669 * prevValue1) + prevValue2)) +
                  (1.716 * _prevV14_1) - (0.74 * _prevV14_2);
        var wave4 = v14 - (1.685 * _prevV14_1) + _prevV14_2 +
                    (1.866 * _prevWave4_1) - (0.89 * _prevWave4_2);

        var v15 = (0.0010794 * (value - (1.226 * prevValue1) + prevValue2)) +
                  (1.8 * _prevV15_1) - (0.811 * _prevV15_2);
        var wave5 = v15 - (1.842 * _prevV15_1) + _prevV15_2 +
                    (1.91 * _prevWave5_1) - (0.922 * _prevWave5_2);

        var v16 = (0.001705 * (value - (1.659 * prevValue1) + prevValue2)) +
                  (1.873 * _prevV16_1) - (0.878 * _prevV16_2);
        var wave6 = v16 - (1.957 * _prevV16_1) + _prevV16_2 +
                    (1.946 * _prevWave6_1) - (0.951 * _prevWave6_2);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;

            _prevV1Neg2_2 = _prevV1Neg2_1;
            _prevV1Neg2_1 = v1Neg2;
            _prevWaveNeg2_2 = _prevWaveNeg2_1;
            _prevWaveNeg2_1 = waveNeg2;

            _prevV1Neg1_2 = _prevV1Neg1_1;
            _prevV1Neg1_1 = v1Neg1;
            _prevWaveNeg1_2 = _prevWaveNeg1_1;
            _prevWaveNeg1_1 = waveNeg1;

            _prevV10_2 = _prevV10_1;
            _prevV10_1 = v10;
            _prevWave0_2 = _prevWave0_1;
            _prevWave0_1 = wave0;

            _prevV11_2 = _prevV11_1;
            _prevV11_1 = v11;
            _prevWave1_2 = _prevWave1_1;
            _prevWave1_1 = wave1;

            _prevV12_2 = _prevV12_1;
            _prevV12_1 = v12;
            _prevWave2_2 = _prevWave2_1;
            _prevWave2_1 = wave2;

            _prevV13_2 = _prevV13_1;
            _prevV13_1 = v13;
            _prevWave3_2 = _prevWave3_1;
            _prevWave3_1 = wave3;

            _prevV14_2 = _prevV14_1;
            _prevV14_1 = v14;
            _prevWave4_2 = _prevWave4_1;
            _prevWave4_1 = wave4;

            _prevV15_2 = _prevV15_1;
            _prevV15_1 = v15;
            _prevWave5_2 = _prevWave5_1;
            _prevWave5_1 = wave5;

            _prevV16_2 = _prevV16_1;
            _prevV16_1 = v16;
            _prevWave6_2 = _prevWave6_1;
            _prevWave6_1 = wave6;

            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(9)
            {
                { "Eclpf-2", waveNeg2 },
                { "Eclpf-1", waveNeg1 },
                { "Eclpf0", wave0 },
                { "Eclpf1", wave1 },
                { "Eclpf2", wave2 },
                { "Eclpf3", wave3 },
                { "Eclpf4", wave4 },
                { "Eclpf5", wave5 },
                { "Eclpf6", wave6 }
            };
        }

        return new StreamingIndicatorStateResult(waveNeg2, outputs);
    }
}

public sealed class EhlersBetterExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevEma;
    private int _index;

    public EhlersBetterExponentialMovingAverageState(int length = 20, InputName inputName = InputName.Close)
    {
        var val = length != 0 ? Math.Cos(2 * Math.PI / length) + Math.Sin(2 * Math.PI / length) : 0;
        _alpha = val != 0 ? MathHelper.MinOrMax((val - 1) / val, 0.99, 0.01) : 0.01;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersBetterExponentialMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var val = length != 0 ? Math.Cos(2 * Math.PI / length) + Math.Sin(2 * Math.PI / length) : 0;
        _alpha = val != 0 ? MathHelper.MinOrMax((val - 1) / val, 0.99, 0.01) : 0.01;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersBetterExponentialMovingAverage;

    public void Reset()
    {
        _prevValue = 0;
        _prevEma = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevEma = _index >= 1 ? _prevEma : 0;

        var ema = (_alpha * value) + ((1 - _alpha) * prevEma);
        var bEma = (_alpha * ((value + prevValue) / 2)) + ((1 - _alpha) * prevEma);

        if (isFinal)
        {
            _prevValue = value;
            _prevEma = ema;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ebema", bEma }
            };
        }

        return new StreamingIndicatorStateResult(bEma, outputs);
    }
}

internal sealed class HighPassFilterV1Engine
{
    private readonly double _alpha;
    private readonly double _pow1;
    private readonly double _pow2;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevHp1;
    private double _prevHp2;
    private int _index;

    public HighPassFilterV1Engine(int length, double mult)
    {
        var resolved = Math.Max(1, length);
        var alphaArg = MathHelper.MinOrMax(2 * Math.PI / (mult * resolved * MathHelper.Sqrt2), 0.99, 0.01);
        var alphaCos = Math.Cos(alphaArg);
        _alpha = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        _pow1 = MathHelper.Pow(1 - (_alpha / 2), 2);
        _pow2 = MathHelper.Pow(1 - _alpha, 2);
    }

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _index = 0;
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevHp1 = _index >= 1 ? _prevHp1 : 0;
        var prevHp2 = _index >= 2 ? _prevHp2 : 0;
        var hp = (_pow1 * (value - (2 * prevValue1) + prevValue2)) +
                 (2 * (1 - _alpha) * prevHp1) - (_pow2 * prevHp2);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _index++;
        }

        return hp;
    }
}

internal sealed class HighPassFilterV2Engine : IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly IMovingAverageSmoother _ma1;
    private readonly IMovingAverageSmoother _ma2;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevHp1;
    private double _prevHp2;
    private int _index;

    public HighPassFilterV2Engine(MovingAvgType maType, int length)
    {
        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / resolved);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = (1 + _c2 - _c3) / 4;
        _ma1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ma2 = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public void Reset()
    {
        _ma1.Reset();
        _ma2.Reset();
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _index = 0;
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevHp1 = _index >= 1 ? _prevHp1 : 0;
        var prevHp2 = _index >= 2 ? _prevHp2 : 0;
        var hp = _index < 4
            ? 0
            : (_c1 * (value - (2 * prevValue1) + prevValue2)) + (_c2 * prevHp1) + (_c3 * prevHp2);

        var hpMa1 = _ma1.Next(hp, isFinal);
        var hpMa2 = _ma2.Next(hpMa1, isFinal);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _index++;
        }

        return hpMa2;
    }

    public void Dispose()
    {
        _ma1.Dispose();
        _ma2.Dispose();
    }
}
