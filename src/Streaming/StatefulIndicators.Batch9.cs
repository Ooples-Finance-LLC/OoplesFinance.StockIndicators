using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Deli")]
public sealed class EhlersDetrendedLeadingIndicatorState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _alpha2;
    private double _ema1;
    private double _ema2;
    private double _temp;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;
    private bool _hasEma;
    private bool _hasTemp;

    public EhlersDetrendedLeadingIndicatorState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _alpha = length > 2 ? 2.0 / (resolved + 1) : 0.67;
        _alpha2 = _alpha / 2;
    }

    public IndicatorName Name => IndicatorName.EhlersDetrendedLeadingIndicator;

    public void Reset()
    {
        _ema1 = 0;
        _ema2 = 0;
        _temp = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
        _hasEma = false;
        _hasTemp = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var currentHigh = Math.Max(prevHigh, bar.High);
        var currentLow = Math.Min(prevLow, bar.Low);
        var currentPrice = (currentHigh + currentLow) / 2;
        var prevEma1 = _hasEma ? _ema1 : currentPrice;
        var prevEma2 = _hasEma ? _ema2 : currentPrice;
        var ema1 = (_alpha * currentPrice) + ((1 - _alpha) * prevEma1);
        var ema2 = (_alpha2 * currentPrice) + ((1 - _alpha2) * prevEma2);
        var dsp = ema1 - ema2;
        var prevTemp = _hasTemp ? _temp : 0;
        var temp = (_alpha * dsp) + ((1 - _alpha) * prevTemp);
        var deli = dsp - temp;

        if (isFinal)
        {
            _ema1 = ema1;
            _ema2 = ema2;
            _temp = temp;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
            _hasEma = true;
            _hasTemp = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dsp", dsp },
                { "Deli", deli }
            };
        }

        return new StreamingIndicatorStateResult(deli, outputs);
    }
}

[PrimaryOutput("Edsma")]
public sealed class EhlersDeviationScaledMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersDeviationScaledMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;

    public EhlersDeviationScaledMovingAverageState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
        int fastLength = 20, int slowLength = 40)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _engine = new EhlersDeviationScaledMovingAverageEngine(maType, resolvedFast, resolvedSlow);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EhlersDeviationScaledMovingAverage;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var edsma = _engine.Next(value, isFinal, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edsma", edsma }
            };
        }

        return new StreamingIndicatorStateResult(edsma, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

[PrimaryOutput("Edsss")]
public sealed class EhlersDeviationScaledSuperSmootherState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly RollingWindowSum _filtPowSum;
    private readonly PooledRingBuffer<double> _values;
    private double _prevDsss1;
    private double _prevDsss2;
    private double _prevValue;
    private int _index;

    public EhlersDeviationScaledSuperSmootherState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 12, int length2 = 50)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var hannLength = (int)Math.Ceiling(_length1 / 1.4);
        _input = new StreamingInputResolver(InputName.Close, null);
        _smoother = EhlersStreamingSmootherFactory.Create(maType, hannLength);
        _filtPowSum = new RollingWindowSum(resolvedLength2);
        _values = new PooledRingBuffer<double>(_length1);
    }

    public IndicatorName Name => IndicatorName.EhlersDeviationScaledSuperSmoother;

    public void Reset()
    {
        _smoother.Reset();
        _filtPowSum.Reset();
        _values.Clear();
        _prevDsss1 = 0;
        _prevDsss2 = 0;
        _prevValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, _length1);
        var mom = value - priorValue;
        var filt = _smoother.Next(mom, isFinal);

        int countAfter;
        var sum = isFinal ? _filtPowSum.Add(filt * filt, out countAfter) : _filtPowSum.Preview(filt * filt, out countAfter);
        var filtPowMa = countAfter != 0 ? sum / countAfter : 0;
        var rms = filtPowMa > 0 ? MathHelper.Sqrt(filtPowMa) : 0;
        var scaledFilt = rms != 0 ? filt / rms : 0;

        // A ratio of zero sets c1 to zero and leaves a double integrator that stops reading its input;
        // see the batch calculation. With no deviation to scale by, the neutral magnitude of one gives
        // the nominal period rather than an infinite one.
        var scaledAbs = Math.Abs(scaledFilt);
        scaledAbs = scaledAbs != 0 ? scaledAbs : 1;
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI * scaledAbs / _length1);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI * scaledAbs / _length1);
        var c2 = b1;
        var c3 = -a1 * a1;
        var c1 = 1 - c2 - c3;

        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevDsss1 = _index >= 1 ? _prevDsss1 : 0;
        var prevDsss2 = _index >= 2 ? _prevDsss2 : 0;
        var dsss = (c1 * ((value + prevValue) / 2)) + (c2 * prevDsss1) + (c3 * prevDsss2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _prevDsss2 = _prevDsss1;
            _prevDsss1 = dsss;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edsss", dsss }
            };
        }

        return new StreamingIndicatorStateResult(dsss, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _filtPowSum.Dispose();
        _values.Dispose();
    }
}

[PrimaryOutput("Edft")]
public sealed class EhlersDiscreteFourierTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly DiscreteFourierCycle _spectrum;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public EhlersDiscreteFourierTransformState(int minLength = 8, int maxLength = 50, int length = 40)
    { _spectrum = new(minLength, maxLength, length); }
    public IndicatorName Name => IndicatorName.EhlersDiscreteFourierTransform;
    public void Reset() => _spectrum.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var cycle = _spectrum.Next(_input.GetValue(bar), isFinal, out _);
        return new StreamingIndicatorStateResult(cycle, includeOutputs
            ? new Dictionary<string, double> { { "Edft", cycle } } : null);
    }
    public void Dispose() => _spectrum.Dispose();
}

[PrimaryOutput("Edftse")]
public sealed class EhlersDiscreteFourierTransformSpectralEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _roofingValues;
    private readonly double[] _rArray;
    private readonly double[] _pendingPower;

    public EhlersDiscreteFourierTransformSpectralEstimateState(int length1 = 48, int length2 = 10)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2);
        _roofingValues = new PooledRingBuffer<double>(_length1 + 1);
        _rArray = new double[_length1 + 1];
        _pendingPower = new double[_length1 + 1];
    }

    public IndicatorName Name => IndicatorName.EhlersDiscreteFourierTransformSpectralEstimate;

    public void Reset()
    {
        _roofingFilter.Reset();
        _roofingValues.Clear();
        Array.Clear(_rArray, 0, _rArray.Length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, false).Value;

        double maxPwr = 0;
        double spx = 0;
        double sp = 0;
        for (var j = _length2; j <= _length1; j++)
        {
            double cosPart = 0;
            double sinPart = 0;
            for (var k = 0; k <= _length1; k++)
            {
                var prevFilt = EhlersStreamingWindow.GetOffsetValue(_roofingValues, roofingFilter, k);
                cosPart += prevFilt * Math.Cos(2 * Math.PI * ((double)k / j));
                sinPart += prevFilt * Math.Sin(2 * Math.PI * ((double)k / j));
            }

            var sqSum = MathHelper.Pow(cosPart, 2) + MathHelper.Pow(sinPart, 2);
            var prevR = _rArray[j];
            var r = (0.2 * MathHelper.Pow(sqSum, 2)) + (0.8 * prevR);
            if (isFinal)
            {
                _rArray[j] = r;
            }

            _pendingPower[j] = r;
            maxPwr = Math.Max(r, maxPwr);
        }

        for (var j = _length2; j <= _length1; j++)
        {
            var pwr = maxPwr != 0 ? _pendingPower[j] / maxPwr : 0;

            if (pwr >= 0.5)
            {
                spx += j * pwr;
                sp += pwr;
            }
        }

        var domCyc = sp != 0 ? spx / sp : 0;

        if (isFinal)
        {
            _roofingValues.TryAdd(roofingFilter, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edftse", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _roofingValues.Dispose();
    }
}

[PrimaryOutput("Edcf")]
public sealed class EhlersDistanceCoefficientFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersDistanceCoefficientFilterState(int length = 14)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _values = new PooledRingBuffer<double>(_length * 2);
    }

    public IndicatorName Name => IndicatorName.EhlersDistanceCoefficientFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double srcSum = 0;
        double coefSum = 0;
        for (var count = 0; count <= _length - 1; count++)
        {
            var prevCount = EhlersStreamingWindow.GetOffsetValue(_values, value, count);

            double distance = 0;
            for (var lookBack = 1; lookBack <= _length - 1; lookBack++)
            {
                var prevCountLookBack = EhlersStreamingWindow.GetOffsetValue(_values, value, count + lookBack);
                distance += MathHelper.Pow(prevCount - prevCountLookBack, 2);
            }

            srcSum += distance * prevCount;
            coefSum += distance;
        }

        var filter = coefSum != 0 ? srcSum / coefSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edcf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

[PrimaryOutput("V2")]
public sealed class EhlersDominantCycleTunedBypassFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha1;
    private readonly StreamingInputResolver _input;
    private readonly EhlersSpectrumDerivedFilterBankEngine _sdfb;
    private readonly PooledRingBuffer<double> _hpValues;
    private readonly PooledRingBuffer<double> _v1Values;
    private double _prevSmoothHp;
    private double _prevValue;
    private int _index;

    public EhlersDominantCycleTunedBypassFilterState(int minLength = 8, int maxLength = 50, int length1 = 40,
        int length2 = 10)
    {
        var resolvedMin = Math.Max(1, minLength);
        var resolvedMax = Math.Max(maxLength, resolvedMin);
        var resolvedLength1 = Math.Max(1, length1);
        var twoPiPer = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);
        _input = new StreamingInputResolver(InputName.Close, null);
        _sdfb = new EhlersSpectrumDerivedFilterBankEngine(resolvedMin, resolvedMax, resolvedLength1, Math.Max(1, length2));
        _hpValues = new PooledRingBuffer<double>(5);
        _v1Values = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersDominantCycleTunedBypassFilter;

    public void Reset()
    {
        _sdfb.Reset();
        _hpValues.Clear();
        _v1Values.Clear();
        _prevSmoothHp = 0;
        _prevValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var domCyc = _sdfb.Next(value, isFinal);
        var beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / domCyc, 0.99, 0.01));
        var delta = Math.Max((-0.015 * _index) + 0.5, 0.15);
        var gamma = 1 / Math.Cos(MathHelper.MinOrMax(4 * Math.PI * (delta / domCyc), 0.99, 0.01));
        var alpha = gamma - MathHelper.Sqrt((gamma * gamma) - 1);

        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevHp1 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 1);
        var prevHp2 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 2);
        var prevHp3 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 3);
        var prevHp4 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 4);
        var prevHp5 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 5);

        var hp = _index < 7 ? value : (0.5 * (1 + _alpha1) * (value - prevValue)) + (_alpha1 * prevHp1);
        var smoothHp = _index < 7
            ? value - prevValue
            : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;

        var prevSmoothHp = _index >= 1 ? _prevSmoothHp : 0;
        var prevV1 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 1);
        var prevV1_2 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 2);
        var v1 = (0.5 * (1 - alpha) * (smoothHp - prevSmoothHp)) + (beta * (1 + alpha) * prevV1) - (alpha * prevV1_2);
        var v2 = domCyc / Math.PI * 2 * (v1 - prevV1);

        if (isFinal)
        {
            _hpValues.TryAdd(hp, out _);
            _v1Values.TryAdd(v1, out _);
            _prevSmoothHp = smoothHp;
            _prevValue = value;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "V1", v1 },
                { "V2", v2 }
            };
        }

        return new StreamingIndicatorStateResult(v2, outputs);
    }

    public void Dispose()
    {
        _sdfb.Dispose();
        _hpValues.Dispose();
        _v1Values.Dispose();
    }
}

[PrimaryOutput("Edddc")]
public sealed class EhlersDualDifferentiatorDominantCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _realValues;
    private readonly PooledRingBuffer<double> _imagValues;
    private double _peak;
    private double _qPeak;
    private double _prevPeriod;
    private double _prevDomCyc1;
    private double _prevDomCyc2;
    private int _index;

    public EhlersDualDifferentiatorDominantCycleState(int length1 = 48, int length2 = 20, int length3 = 8)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, resolvedLength2);
        _realValues = new PooledRingBuffer<double>(2);
        _imagValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersDualDifferentiatorDominantCycle;

    public void Reset()
    {
        _roofingFilter.Reset();
        _realValues.Clear();
        _imagValues.Clear();
        _peak = 0;
        _qPeak = 0;
        _prevPeriod = 0;
        _prevDomCyc1 = 0;
        _prevDomCyc2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, false).Value;
        var prevReal1 = EhlersStreamingWindow.GetOffsetValue(_realValues, 1);
        var prevReal2 = EhlersStreamingWindow.GetOffsetValue(_realValues, 2);
        var prevImag1 = EhlersStreamingWindow.GetOffsetValue(_imagValues, 1);

        var peak = Math.Max(0.991 * _peak, Math.Abs(roofingFilter));
        var real = peak != 0 ? roofingFilter / peak : 0;
        var qFilt = real - prevReal1;
        var qPeak = Math.Max(0.991 * _qPeak, Math.Abs(qFilt));
        var imag = qPeak != 0 ? qFilt / qPeak : 0;

        var iDot = real - prevReal1;
        var qDot = imag - prevImag1;
        var prevPeriod = _index >= 1 ? _prevPeriod : 0;
        var determinant = real*prevImag1-imag*prevReal1;
        var resolution = 1e-12*(Math.Abs(real*prevImag1)+Math.Abs(imag*prevReal1));
        var period = Math.Abs(determinant) <= resolution ? 0 : 2*Math.PI*(real*real+imag*imag)/determinant;
        period = MathHelper.MinOrMax(period, _length1, _length3);
        var domCyc = (_c1 * ((period + prevPeriod) / 2)) + (_c2 * _prevDomCyc1) + (_c3 * _prevDomCyc2);

        if (isFinal)
        {
            _realValues.TryAdd(real, out _);
            _imagValues.TryAdd(imag, out _);
            _peak = peak;
            _qPeak = qPeak;
            _prevPeriod = period;
            _prevDomCyc2 = _prevDomCyc1;
            _prevDomCyc1 = domCyc;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edddc", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _realValues.Dispose();
        _imagValues.Dispose();
    }
}

[PrimaryOutput("Eoti")]
public sealed class EhlersEarlyOnsetTrendIndicatorState : IStreamingIndicatorState
{
    private readonly double _k;
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV1Engine _hp;
    private readonly EhlersSuperSmootherFilterEngine _smoother;
    private double _peak;
    private bool _hasPeak;

    public EhlersEarlyOnsetTrendIndicatorState(int length1 = 30, int length2 = 100, double k = 0.85)
    {
        _k = k;
        _input = new StreamingInputResolver(InputName.Close, null);
        _hp = new HighPassFilterV1Engine(Math.Max(1, length2), 1);
        _smoother = new EhlersSuperSmootherFilterEngine(Math.Max(1, length1));
    }

    public IndicatorName Name => IndicatorName.EhlersEarlyOnsetTrendIndicator;

    public void Reset()
    {
        _hp.Reset();
        _smoother.Reset();
        _peak = 0;
        _hasPeak = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hp = _hp.Next(value, isFinal);
        var filter = _smoother.Next(hp, isFinal);

        var prevPeak = _hasPeak ? _peak : 0;
        var peak = Math.Abs(filter) > 0.991 * prevPeak ? Math.Abs(filter) : 0.991 * prevPeak;
        var ratio = peak != 0 ? filter / peak : 0;
        var denom = (_k * ratio) + 1;
        var quotient = denom != 0 ? (ratio + _k) / denom : 0;

        if (isFinal)
        {
            _peak = peak;
            _hasPeak = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eoti", quotient }
            };
        }

        return new StreamingIndicatorStateResult(quotient, outputs);
    }
}

[PrimaryOutput("Esnr")]
public sealed class EhlersEnhancedSignalToNoiseRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _smoothValues;
    private readonly PooledRingBuffer<double> _q3Values;
    private double _prevNoise;
    private double _prevSnr;

    public EhlersEnhancedSignalToNoiseRatioState(int length = 6)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _smoothValues = new PooledRingBuffer<double>(2);
        _q3Values = new PooledRingBuffer<double>(50);
    }

    public IndicatorName Name => IndicatorName.EhlersEnhancedSignalToNoiseRatio;

    public void Reset()
    {
        _mama.Reset();
        _smoothValues.Clear();
        _q3Values.Clear();
        _prevNoise = 0;
        _prevSnr = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mama = _mama.Next(value, isFinal);
        var smooth = mama.Smooth;
        var smoothPeriod = mama.SmoothPeriod;
        var prevSmooth2 = EhlersStreamingWindow.GetOffsetValue(_smoothValues, 2);

        var q3 = 0.5 * (smooth - prevSmooth2) * ((0.1759 * smoothPeriod) + 0.4607);
        var sp = (int)Math.Ceiling(smoothPeriod / 2);
        double i3 = 0;
        for (var j = 0; j <= sp - 1; j++)
        {
            var prevQ3 = EhlersStreamingWindow.GetOffsetValue(_q3Values, q3, j);
            i3 += prevQ3;
        }
        i3 = sp != 0 ? 1.57 * i3 / sp : i3;

        var signalValue = (i3 * i3) + (q3 * q3);
        var diff = bar.High - bar.Low;
        var noise = (0.1 * diff * diff * 0.25) + (0.9 * _prevNoise);
        var temp = noise != 0 ? signalValue / noise : 0;

        // A ratio in decibels is only defined for a positive ratio; see the batch calculation for the
        // full reasoning. On a market with no range at all the noise estimate decays to zero and takes
        // the signal with it, so temp is zero and the unguarded logarithm publishes negative infinity
        // for every bar of the series.
        var logTemp = temp > 0 ? 10 * Math.Log(temp) / Math.Log(10) : 0;
        var snr = (0.33 * logTemp) + (0.67 * _prevSnr);

        if (isFinal)
        {
            _smoothValues.TryAdd(smooth, out _);
            _q3Values.TryAdd(q3, out _);
            _prevNoise = noise;
            _prevSnr = snr;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Esnr", snr },
                { "I3", i3 },
                { "Q3", q3 },
                { "SmoothPeriod", smoothPeriod }
            };
        }

        return new StreamingIndicatorStateResult(snr, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _smoothValues.Dispose();
        _q3Values.Dispose();
    }
}

[PrimaryOutput("Ebsi")]
public sealed class EhlersEvenBetterSineWaveIndicatorState : IStreamingIndicatorState
{
    private readonly EvenBetterSineWaveKernel _kernel;
    private readonly StreamingInputResolver _input;
    public EhlersEvenBetterSineWaveIndicatorState(int length1 = 40, int length2 = 10)
    {
        _kernel = new EvenBetterSineWaveKernel(length1, length2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.EhlersEvenBetterSineWaveIndicator;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _kernel.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { ["Ebsi"] = value } : null);
    }
}

[PrimaryOutput("Ef")]
public sealed class EhlersFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersFilterState(int length1 = 15, int length2 = 5)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, null);
        _values = new PooledRingBuffer<double>(_length1 + _length2);
    }

    public IndicatorName Name => IndicatorName.EhlersFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double num = 0;
        double sumC = 0;
        for (var j = 0; j <= _length1 - 1; j++)
        {
            var currentPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var prevPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, j + _length2);
            var priceDiff = Math.Abs(currentPrice - prevPrice);
            num += priceDiff * currentPrice;
            sumC += priceDiff;
        }

        var filter = sumC != 0 ? num / sumC : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ef", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

[PrimaryOutput("Efirf")]
public sealed class EhlersFiniteImpulseResponseFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersFirWindow _window;
    public EhlersFiniteImpulseResponseFilterState(double coef1 = 1, double coef2 = 3.5, double coef3 = 4.5,
        double coef4 = 3, double coef5 = .5, double coef6 = -.5, double coef7 = -1.5) => _window = new(coef1, coef2, coef3, coef4, coef5, coef6, coef7);
    public IndicatorName Name => IndicatorName.EhlersFiniteImpulseResponseFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Efirf", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Efdso")]
public sealed class EhlersFisherizedDeviationScaledOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersDeviationScaledMovingAverageEngine? _edsmaEngine;
    private readonly IMovingAverageSmoother? _smoother;
    private double _prevEfdso;
    private bool _hasPrev;

    public EhlersFisherizedDeviationScaledOscillatorState(MovingAvgType maType = MovingAvgType.EhlersDeviationScaledMovingAverage,
        int fastLength = 20, int slowLength = 40)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        if (maType == MovingAvgType.EhlersDeviationScaledMovingAverage)
        {
            _edsmaEngine = new EhlersDeviationScaledMovingAverageEngine(MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
                resolvedFast, resolvedSlow);
        }
        else
        {
            _smoother = EhlersStreamingSmootherFactory.Create(maType, resolvedFast);
        }

        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EhlersFisherizedDeviationScaledOscillator;

    public void Reset()
    {
        _edsmaEngine?.Reset();
        _smoother?.Reset();
        _prevEfdso = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double scaledFilter;
        if (_edsmaEngine != null)
        {
            scaledFilter = _edsmaEngine.Next(value, isFinal, out _);
        }
        else
        {
            scaledFilter = _smoother!.Next(value, isFinal);
        }

        var prevEfdso = _hasPrev ? _prevEfdso : 0;
        var efdso = Math.Abs(scaledFilter) < 2
            ? 0.5 * Math.Log((1 + (scaledFilter / 2)) / (1 - (scaledFilter / 2)))
            : prevEfdso;

        if (isFinal)
        {
            _prevEfdso = efdso;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Efdso", efdso }
            };
        }

        return new StreamingIndicatorStateResult(efdso, outputs);
    }

    public void Dispose()
    {
        _edsmaEngine?.Dispose();
        _smoother?.Dispose();
    }
}

[PrimaryOutput("Eft")]
public sealed class EhlersFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;
    private double _prevNValue;
    private double _prevFisher;
    private bool _hasPrev;

    public EhlersFisherTransformState(int length = 10)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EhlersFisherTransform;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _prevNValue = 0;
        _prevFisher = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var maxH = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var minL = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var ratio = FisherArithmetic.Position(value, minL, maxH);
        var prevNValue = _hasPrev ? _prevNValue : 0;
        var nValue = MathHelper.MinOrMax((0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue), 0.999, -0.999);
        var prevFisher = _hasPrev ? _prevFisher : 0;
        var fisher = FisherArithmetic.Transform(nValue) + (0.5 * prevFisher);

        if (isFinal)
        {
            _prevNValue = nValue;
            _prevFisher = fisher;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eft", fisher }
            };
        }

        return new StreamingIndicatorStateResult(fisher, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

[PrimaryOutput("Efmd")]
public sealed class EhlersFMDemodulatorIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersFMDemodulatorIndicatorState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
        int fastLength = 10, int slowLength = 30)
    {
        _fastLength = Math.Max(1, fastLength);
        _input = new StreamingInputResolver(InputName.Close, null);
        _smoother = EhlersStreamingSmootherFactory.Create(maType, Math.Max(1, slowLength));
    }

    public IndicatorName Name => IndicatorName.EhlersFMDemodulatorIndicator;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var der = close - bar.Open;
        var hlRaw = _fastLength * der;
        var hl = MathHelper.MinOrMax(hlRaw, 1, -1);
        var ss = _smoother.Next(hl, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Efmd", ss }
            };
        }

        return new StreamingIndicatorStateResult(ss, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

[PrimaryOutput("Wave")]
public sealed class EhlersFourierSeriesAnalysisState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _l1;
    private readonly double _s1;
    private readonly double _l2;
    private readonly double _s2;
    private readonly double _l3;
    private readonly double _s3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _bp1Values;
    private readonly PooledRingBuffer<double> _bp2Values;
    private readonly PooledRingBuffer<double> _bp3Values;
    private readonly PooledRingBuffer<double> _q1Values;
    private readonly PooledRingBuffer<double> _q2Values;
    private readonly PooledRingBuffer<double> _q3Values;
    private readonly PooledRingBuffer<double> _waveValues;
    private int _index;

    public EhlersFourierSeriesAnalysisState(int length = 20, double bw = 0.1)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _values = new PooledRingBuffer<double>(2);
        _bp1Values = new PooledRingBuffer<double>(_length);
        _bp2Values = new PooledRingBuffer<double>(_length);
        _bp3Values = new PooledRingBuffer<double>(_length);
        _q1Values = new PooledRingBuffer<double>(_length);
        _q2Values = new PooledRingBuffer<double>(_length);
        _q3Values = new PooledRingBuffer<double>(_length);
        _waveValues = new PooledRingBuffer<double>(2);

        _l1 = Math.Cos(2 * Math.PI / _length);
        _s1 = FourierHarmonicPole.For((double)_length/1, bw);

        _l2 = Math.Cos(2 * Math.PI / ((double)_length / 2));
        _s2 = FourierHarmonicPole.For((double)_length/2, bw);

        _l3 = Math.Cos(2 * Math.PI / ((double)_length / 3));
        _s3 = FourierHarmonicPole.For((double)_length/3, bw);
    }

    public IndicatorName Name => IndicatorName.EhlersFourierSeriesAnalysis;

    public void Reset()
    {
        _values.Clear();
        _bp1Values.Clear();
        _bp2Values.Clear();
        _bp3Values.Clear();
        _q1Values.Clear();
        _q2Values.Clear();
        _q3Values.Clear();
        _waveValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevBp1_1 = EhlersStreamingWindow.GetOffsetValue(_bp1Values, 1);
        var prevBp2_1 = EhlersStreamingWindow.GetOffsetValue(_bp2Values, 1);
        var prevBp3_1 = EhlersStreamingWindow.GetOffsetValue(_bp3Values, 1);
        var prevBp1_2 = EhlersStreamingWindow.GetOffsetValue(_bp1Values, 2);
        var prevBp2_2 = EhlersStreamingWindow.GetOffsetValue(_bp2Values, 2);
        var prevBp3_2 = EhlersStreamingWindow.GetOffsetValue(_bp3Values, 2);
        var prevWave2 = EhlersStreamingWindow.GetOffsetValue(_waveValues, 2);

        var bp1 = _index <= 3
            ? 0
            : (0.5 * (1 - _s1) * (value - prevValue)) + (_l1 * (1 + _s1) * prevBp1_1) - (_s1 * prevBp1_2);
        var q1 = _index <= 4 ? 0 : _length / (2 * Math.PI) * (bp1 - prevBp1_1);

        var bp2 = _index <= 3
            ? 0
            : (0.5 * (1 - _s2) * (value - prevValue)) + (_l2 * (1 + _s2) * prevBp2_1) - (_s2 * prevBp2_2);
        var q2 = _index <= 4 ? 0 : _length / (4 * Math.PI) * (bp2 - prevBp2_1);

        var bp3 = _index <= 3
            ? 0
            : (0.5 * (1 - _s3) * (value - prevValue)) + (_l3 * (1 + _s3) * prevBp3_1) - (_s3 * prevBp3_2);
        var q3 = _index <= 4 ? 0 : _length / (6 * Math.PI) * (bp3 - prevBp3_1);

        double p1 = 0;
        double p2 = 0;
        double p3 = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var prevBp1 = EhlersStreamingWindow.GetOffsetValue(_bp1Values, bp1, j);
            var prevBp2 = EhlersStreamingWindow.GetOffsetValue(_bp2Values, bp2, j);
            var prevBp3 = EhlersStreamingWindow.GetOffsetValue(_bp3Values, bp3, j);
            var prevQ1 = EhlersStreamingWindow.GetOffsetValue(_q1Values, q1, j);
            var prevQ2 = EhlersStreamingWindow.GetOffsetValue(_q2Values, q2, j);
            var prevQ3 = EhlersStreamingWindow.GetOffsetValue(_q3Values, q3, j);
            p1 += (prevBp1 * prevBp1) + (prevQ1 * prevQ1);
            p2 += (prevBp2 * prevBp2) + (prevQ2 * prevQ2);
            p3 += (prevBp3 * prevBp3) + (prevQ3 * prevQ3);
        }

        var wave = p1 != 0 ? bp1 + (MathHelper.Sqrt(p2 / p1) * bp2) + (MathHelper.Sqrt(p3 / p1) * bp3) : 0;
        var roc = _length / (4 * Math.PI) * (wave - prevWave2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _bp1Values.TryAdd(bp1, out _);
            _bp2Values.TryAdd(bp2, out _);
            _bp3Values.TryAdd(bp3, out _);
            _q1Values.TryAdd(q1, out _);
            _q2Values.TryAdd(q2, out _);
            _q3Values.TryAdd(q3, out _);
            _waveValues.TryAdd(wave, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Wave", wave },
                { "Roc", roc }
            };
        }

        return new StreamingIndicatorStateResult(wave, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _bp1Values.Dispose();
        _bp2Values.Dispose();
        _bp3Values.Dispose();
        _q1Values.Dispose();
        _q2Values.Dispose();
        _q3Values.Dispose();
        _waveValues.Dispose();
    }
}

[PrimaryOutput("Fama")]
public sealed class EhlersFractalAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _halfP;
    private readonly RollingWindowMax _highWindow1;
    private readonly RollingWindowMin _lowWindow1;
    private readonly RollingWindowMax _highWindow2;
    private readonly RollingWindowMin _lowWindow2;
    private readonly PooledRingBuffer<double> _laggedHighest2;
    private readonly PooledRingBuffer<double> _laggedLowest2;
    private readonly StreamingInputResolver _input;
    private double _prevFilter;
    private bool _hasPrev;
    private int _index;
    private double _dimension;

    public EhlersFractalAdaptiveMovingAverageState(int length = 20)
    {
        length = Math.Max(2, length);
        _length = checked(length + (length & 1));
        _halfP = _length / 2;
        _highWindow1 = new RollingWindowMax(_length);
        _lowWindow1 = new RollingWindowMin(_length);
        _highWindow2 = new RollingWindowMax(_halfP);
        _lowWindow2 = new RollingWindowMin(_halfP);
        _laggedHighest2 = new PooledRingBuffer<double>(_halfP + 1);
        _laggedLowest2 = new PooledRingBuffer<double>(_halfP + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EhlersFractalAdaptiveMovingAverage;

    public void Reset()
    {
        _highWindow1.Reset();
        _lowWindow1.Reset();
        _highWindow2.Reset();
        _lowWindow2.Reset();
        _laggedHighest2.Clear();
        _laggedLowest2.Clear();
        _prevFilter = 0;
        _hasPrev = false;
        _index = 0;
        _dimension = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highestHigh1 = isFinal ? _highWindow1.Add(bar.High, out _) : _highWindow1.Preview(bar.High, out _);
        var lowestLow1 = isFinal ? _lowWindow1.Add(bar.Low, out _) : _lowWindow1.Preview(bar.Low, out _);
        var highestHigh2 = isFinal ? _highWindow2.Add(bar.High, out _) : _highWindow2.Preview(bar.High, out _);
        var lowestLow2 = isFinal ? _lowWindow2.Add(bar.Low, out _) : _lowWindow2.Preview(bar.Low, out _);

        // The value halfP bars back, counting this bar (batch: lagIndex = Math.Max(i - halfP, 0)), read before
        // this bar is committed so a preview and its commit read the same bar. Until halfP bars have passed
        // the lag stays on the first bar, which is still the oldest in the buffer then.
        var highestHigh3 = LaggedOrFirst(_laggedHighest2, highestHigh2);
        var lowestLow3 = LaggedOrFirst(_laggedLowest2, lowestLow2);

        if (isFinal)
        {
            _laggedHighest2.TryAdd(highestHigh2, out _);
            _laggedLowest2.TryAdd(lowestLow2, out _);
        }

        var n3 = (highestHigh1 - lowestLow1) / _length;
        var n1 = (highestHigh2 - lowestLow2) / _halfP;
        var n2 = (highestHigh3 - lowestLow3) / _halfP;
        var dm = _index >= _length - 1 && n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : _dimension;

        var alpha = MathHelper.MinOrMax(MathHelper.Exp(-4.6 * (dm - 1)), 1, 0.01);
        var prevFilter = _hasPrev ? _prevFilter : value;
        var filter = _index < _length ? value : (alpha * value) + ((1 - alpha) * prevFilter);

        if (isFinal)
        {
            _prevFilter = filter;
            _hasPrev = true;
            _dimension = dm;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fama", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    /// <summary>The value halfP bars back counting the pending one, or the first value until there is one.</summary>
    private double LaggedOrFirst(PooledRingBuffer<double> lagged, double pending) =>
        lagged.Count >= _halfP
            ? EhlersStreamingWindow.GetOffsetValue(lagged, pending, _halfP)
            : lagged.Count > 0 ? lagged[0] : pending;

    public void Dispose()
    {
        _highWindow1.Dispose();
        _lowWindow1.Dispose();
        _highWindow2.Dispose();
        _lowWindow2.Dispose();
        _laggedHighest2.Dispose();
        _laggedLowest2.Dispose();
    }
}

[PrimaryOutput("Egf4")]
public sealed class EhlersGaussianFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _gains;
    private readonly double[][] _stages = { new double[1], new double[2], new double[3], new double[4] };
    private readonly int _poles;
    private readonly StreamingInputResolver _input;

    public EhlersGaussianFilterState(int length = 14, int poles = 4)
    {
        _poles = Math.Max(1, Math.Min(4, poles));
        _gains = new[] { EhlersGaussian.Gain(length, 1), EhlersGaussian.Gain(length, 2), EhlersGaussian.Gain(length, 3), EhlersGaussian.Gain(length, 4) };
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.EhlersGaussianFilter;
    public void Reset()
    {
        foreach (var stages in _stages) Array.Clear(stages, 0, stages.Length);
    }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var first = EhlersGaussian.Next(value, _gains[0], _stages[0], isFinal);
        var second = EhlersGaussian.Next(value, _gains[1], _stages[1], isFinal);
        var third = EhlersGaussian.Next(value, _gains[2], _stages[2], isFinal);
        var fourth = EhlersGaussian.Next(value, _gains[3], _stages[3], isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double>(4)
        { { "Egf1", first }, { "Egf2", second }, { "Egf3", third }, { "Egf4", fourth } } : null;
        return new StreamingIndicatorStateResult(_poles switch { 1 => first, 2 => second, 3 => third, _ => fourth }, outputs);
    }
    public void Dispose() { }
}

[PrimaryOutput("Ehma")]
public sealed class EhlersHammingMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly HammingWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public EhlersHammingMovingAverageState(int length = 20, double pedestal = 3)
    {
        _mean = new HammingWindowMean(length, pedestal);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EhlersHammingMovingAverage;

    public void Reset()
    {
        _mean.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var filt = _mean.Next(value, isFinal);

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
        _mean.Dispose();
    }
}

internal sealed class Ehlers2PoleSuperSmootherFilterV2Smoother : IMovingAverageSmoother
{
    private readonly TwoPoleWindow _window;
    public Ehlers2PoleSuperSmootherFilterV2Smoother(int length) => _window = new(length, 3);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() { }
}

internal sealed class EhlersSuperSmootherFilterEngine
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private double _prevValue;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public EhlersSuperSmootherFilterEngine(int length)
    {
        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevFilter1 = _index >= 1 ? _prevFilter1 : 0;
        var prevFilter2 = _index >= 2 ? _prevFilter2 : 0;
        var filt = (_c1 * ((value + prevValue) / 2)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        if (isFinal)
        {
            _prevValue = value;
            _prevFilter2 = _prevFilter1;
            _prevFilter1 = filt;
            _index++;
        }

        return filt;
    }

    public void Reset()
    {
        _prevValue = 0;
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _index = 0;
    }
}

internal sealed class StandardDeviationVolatilityEngine : IDisposable
{
    private readonly RollingStandardDeviation _stdDev;

    public StandardDeviationVolatilityEngine(int length)
    {
        _stdDev = new RollingStandardDeviation(Math.Max(1, length));
    }

    public double Next(double value, bool isFinal)
    {
        return _stdDev.Next(value, isFinal);
    }

    public void Reset()
    {
        _stdDev.Reset();
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

internal sealed class EhlersDeviationScaledMovingAverageEngine : IDisposable
{
    private readonly int _slowLength;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StandardDeviationVolatilityEngine _stdDev;
    private readonly PooledRingBuffer<double> _values;
    private double _prevZeros;
    private double _prevScaledFilter;
    private double _prevEdsma;
    private int _index;

    public EhlersDeviationScaledMovingAverageEngine(MovingAvgType maType, int fastLength, int slowLength)
    {
        var resolvedFast = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _smoother = EhlersStreamingSmootherFactory.Create(maType, resolvedFast);
        _stdDev = new StandardDeviationVolatilityEngine(_slowLength);
        _values = new PooledRingBuffer<double>(2);
    }

    public double Next(double value, bool isFinal, out double scaledFilter)
    {
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, 2);
        var zeros = _index >= 2 ? value - prevValue : 0;
        var prevZeros = _index >= 1 ? _prevZeros : 0;
        var avgZeros = (zeros + prevZeros) / 2;
        var ssf2Pole = _smoother.Next(avgZeros, isFinal);
        var stdDev = _stdDev.Next(ssf2Pole, isFinal);
        scaledFilter = stdDev != 0 ? ssf2Pole / stdDev : _prevScaledFilter;
        var alpha2Pole = MathHelper.MinOrMax(5 * Math.Abs(scaledFilter) / _slowLength, 0.99, 0.01);
        var prevEdsma = _index >= 1 ? _prevEdsma : 0;
        var edsma = (alpha2Pole * value) + ((1 - alpha2Pole) * prevEdsma);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevZeros = zeros;
            _prevScaledFilter = scaledFilter;
            _prevEdsma = edsma;
            _index++;
        }

        return edsma;
    }

    public void Reset()
    {
        _smoother.Reset();
        _stdDev.Reset();
        _values.Clear();
        _prevZeros = 0;
        _prevScaledFilter = 0;
        _prevEdsma = 0;
        _index = 0;
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _stdDev.Dispose();
        _values.Dispose();
    }
}

internal sealed class EhlersSpectrumDerivedFilterBankEngine : IDisposable
{
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly int _length2;
    private readonly double _alpha1;
    private readonly PooledRingBuffer<double> _hpValues;
    private readonly PooledRingBuffer<double> _smoothHpValues;
    // One bandpass per period in the bank, each with its own two-sample recursion. These were read
    // from buffers holding one value per bar - the last period's - so every period was fed another
    // period's output, and the bank never settled on a market that never moved.
    private readonly double[] _realPrev1;
    private readonly double[] _realPrev2;
    private readonly double[] _imagPrev1;
    private readonly double[] _imagPrev2;
    private readonly double[] _power;
    private readonly PooledRingBuffer<double> _dcValues;
    private readonly double[] _medianScratch;
    private double _prevValue;
    private double _priceScale;
    private int _index;

    public EhlersSpectrumDerivedFilterBankEngine(int minLength, int maxLength, int length1, int length2)
    {
        _minLength = Math.Max(3, minLength);
        _maxLength = Math.Max(maxLength, _minLength);
        var resolvedLength1 = Math.Max(3, length1);
        _length2 = Math.Max(1, length2);
        _alpha1 = Math.Tan(Math.PI / 4 - Math.PI / resolvedLength1);
        _hpValues = new PooledRingBuffer<double>(5);
        _smoothHpValues = new PooledRingBuffer<double>(3);
        _realPrev1 = new double[_maxLength + 1];
        _realPrev2 = new double[_maxLength + 1];
        _imagPrev1 = new double[_maxLength + 1];
        _imagPrev2 = new double[_maxLength + 1];
        _power = new double[_maxLength + 1];
        _dcValues = new PooledRingBuffer<double>(_length2);
        _medianScratch = new double[_length2];
    }

    internal double PreviousSmoothedHighPass { get; private set; }
    internal double SmoothedHighPass => EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, 1);

    public double Next(double value, bool isFinal)
    {
        // Cycle selection is homogeneous in price. Keep the entire linear bank in
        // a common finite scale before forming differences and channel powers.
        // A preview reads rescaled history without committing the scale change.
        var priceScale = Math.Max(_priceScale, Math.Abs(value));
        var rescale = priceScale == 0 ? 1 : _priceScale / priceScale;
        value = priceScale == 0 ? 0 : value / priceScale;
        var prevValue = _index >= 1 ? _prevValue * rescale : 0;
        var delta = Math.Max((-0.015 * _index) + 0.5, 0.15);
        var prevHp1 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 1) * rescale;
        var prevHp2 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 2) * rescale;
        var prevHp3 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 3) * rescale;
        var prevHp4 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 4) * rescale;
        var prevHp5 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 5) * rescale;

        var hp = _index < 7 ? value : (0.5 * (1 + _alpha1) * (value - prevValue)) + (_alpha1 * prevHp1);
        var prevSmoothHp = EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, 1) * rescale;
        var smoothHp = _index < 7
            ? value - prevValue
            : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;

        var smoothTwoBack = EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, 2) * rescale;
        var smoothThreeBack = EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, 3) * rescale;
        double maxPower = 0;
        for (var period = _minLength; period <= _maxLength; period++)
        {
            var beta = Math.Cos(2 * Math.PI / period);
            var width = 4 * Math.PI * delta / period;
            // Stable root of alpha + 1/alpha = 2/cos(width), including wide startup bands.
            var alpha = Math.Cos(width) / (1 + Math.Abs(Math.Sin(width)));
            var scale = period / (2 * Math.PI);
            var quadrature = scale * (smoothHp - prevSmoothHp);
            var priorQuadrature = scale * (smoothTwoBack - smoothThreeBack);
            var real = .5 * (1 - alpha) * (smoothHp - smoothTwoBack)
                + beta * (1 + alpha) * (_realPrev1[period] * rescale) - alpha * (_realPrev2[period] * rescale);
            var imag = .5 * (1 - alpha) * (quadrature - priorQuadrature)
                + beta * (1 + alpha) * (_imagPrev1[period] * rescale) - alpha * (_imagPrev2[period] * rescale);
            if (isFinal)
            {
                _realPrev2[period] = _realPrev1[period] * rescale;
                _realPrev1[period] = real;
                _imagPrev2[period] = _imagPrev1[period] * rescale;
                _imagPrev1[period] = imag;
            }
            _power[period] = real * real + imag * imag;
            maxPower = Math.Max(maxPower, _power[period]);
        }

        double numerator = 0, denominator = 0;
        if (maxPower > 0)
        {
            // Normalize the completed spectrum, so the answer cannot depend on bin traversal order.
            for (var period = _minLength; period <= _maxLength; period++)
            {
                var ratio = _power[period] / maxPower;
                var db = 10 * Math.Log10(Math.Max(.01, 1 - .99 * ratio) / .01);
                if (db <= 3)
                {
                    var weight = _maxLength - db;
                    numerator += period * weight;
                    denominator += weight;
                }
            }
        }
        var dc = denominator == 0 ? _minLength : Math.Max(_minLength, Math.Min(_maxLength, numerator / denominator));

        var domCyc = EhlersStreamingWindow.GetMedian(_dcValues, dc, _medianScratch);

        if (isFinal)
        {
            if (rescale != 1)
            {
                RescaleHistory(_hpValues, rescale);
                RescaleHistory(_smoothHpValues, rescale);
            }
            PreviousSmoothedHighPass = prevSmoothHp;
            _priceScale = priceScale;
            _hpValues.TryAdd(hp, out _);
            _smoothHpValues.TryAdd(smoothHp, out _);

            _dcValues.TryAdd(dc, out _);
            _prevValue = value;
            _index++;
        }

        return domCyc;
    }

    private static void RescaleHistory(PooledRingBuffer<double> history, double factor)
    {
        var count = history.Count;
        Span<double> values = stackalloc double[count];
        history.CopyTo(values);
        history.Clear();
        for (var i = 0; i < count; i++) history.TryAdd(values[i] * factor, out _);
    }

    public void Reset()
    {
        _hpValues.Clear();
        _smoothHpValues.Clear();
        Array.Clear(_realPrev1, 0, _realPrev1.Length);
        Array.Clear(_realPrev2, 0, _realPrev2.Length);
        Array.Clear(_imagPrev1, 0, _imagPrev1.Length);
        Array.Clear(_imagPrev2, 0, _imagPrev2.Length);
        Array.Clear(_power, 0, _power.Length);
        _dcValues.Clear();
        _prevValue = 0;
        _priceScale = 0;
        PreviousSmoothedHighPass = 0;
        _index = 0;
    }

    public void Dispose()
    {
        _hpValues.Dispose();
        _smoothHpValues.Dispose();

        _dcValues.Dispose();
    }
}

internal static class EhlersStreamingSmootherFactory
{
    public static IMovingAverageSmoother Create(MovingAvgType maType, int length)
    {
        return maType switch
        {
            MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 => new Ehlers2PoleSuperSmootherFilterV2Smoother(length),
            _ => MovingAverageSmootherFactory.Create(maType, length)
        };
    }
}
