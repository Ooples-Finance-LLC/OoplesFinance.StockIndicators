using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

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

public sealed class EhlersDeviationScaledMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersDeviationScaledMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;

    public EhlersDeviationScaledMovingAverageState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
        int fastLength = 20, int slowLength = 40, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _engine = new EhlersDeviationScaledMovingAverageEngine(maType, resolvedFast, resolvedSlow);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersDeviationScaledMovingAverageState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _engine = new EhlersDeviationScaledMovingAverageEngine(maType, resolvedFast, resolvedSlow);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        int length1 = 12, int length2 = 50, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var hannLength = (int)Math.Ceiling(_length1 / 1.4);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = EhlersStreamingSmootherFactory.Create(maType, hannLength);
        _filtPowSum = new RollingWindowSum(resolvedLength2);
        _values = new PooledRingBuffer<double>(_length1);
    }

    public EhlersDeviationScaledSuperSmootherState(MovingAvgType maType, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var hannLength = (int)Math.Ceiling(_length1 / 1.4);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var scaledAbs = Math.Abs(scaledFilt);
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

public sealed class EhlersDiscreteFourierTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _hpValues;
    private readonly PooledRingBuffer<double> _cleanedValues;
    private readonly PooledRingBuffer<double> _powerValues;
    private double _prevValue;
    private int _index;

    public EhlersDiscreteFourierTransformState(int minLength = 8, int maxLength = 50, int length = 40,
        InputName inputName = InputName.Close)
    {
        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(maxLength, _minLength);
        var resolvedLength = Math.Max(1, length);
        var twoPiPrd = MathHelper.MinOrMax(2 * Math.PI / resolvedLength, 0.99, 0.01);
        _alpha = (1 - Math.Sin(twoPiPrd)) / Math.Cos(twoPiPrd);
        _input = new StreamingInputResolver(inputName, null);
        _hpValues = new PooledRingBuffer<double>(5);
        _cleanedValues = new PooledRingBuffer<double>(_maxLength);
        _powerValues = new PooledRingBuffer<double>(_maxLength);
    }

    public EhlersDiscreteFourierTransformState(int minLength, int maxLength, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(maxLength, _minLength);
        var resolvedLength = Math.Max(1, length);
        var twoPiPrd = MathHelper.MinOrMax(2 * Math.PI / resolvedLength, 0.99, 0.01);
        _alpha = (1 - Math.Sin(twoPiPrd)) / Math.Cos(twoPiPrd);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _hpValues = new PooledRingBuffer<double>(5);
        _cleanedValues = new PooledRingBuffer<double>(_maxLength);
        _powerValues = new PooledRingBuffer<double>(_maxLength);
    }

    public IndicatorName Name => IndicatorName.EhlersDiscreteFourierTransform;

    public void Reset()
    {
        _hpValues.Clear();
        _cleanedValues.Clear();
        _powerValues.Clear();
        _prevValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue : 0;
        var prevHp1 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 1);
        var prevHp2 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 2);
        var prevHp3 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 3);
        var prevHp4 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 4);
        var prevHp5 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 5);

        var hp = _index <= 5 ? value : (0.5 * (1 + _alpha) * (value - prevValue1)) + (_alpha * prevHp1);
        var cleaned = _index <= 5 ? value : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) +
                                             (2 * prevHp4) + prevHp5) / 12;

        double pwr = 0;
        double cosPart = 0;
        double sinPart = 0;
        for (var j = _minLength; j <= _maxLength; j++)
        {
            for (var n = 0; n <= _maxLength - 1; n++)
            {
                var prevCleaned = EhlersStreamingWindow.GetOffsetValue(_cleanedValues, cleaned, n);
                cosPart += prevCleaned * Math.Cos(MathHelper.MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
                sinPart += prevCleaned * Math.Sin(MathHelper.MinOrMax(2 * Math.PI * ((double)n / j), 0.99, 0.01));
            }

            pwr = (cosPart * cosPart) + (sinPart * sinPart);
        }

        var maxPwr = _index >= _minLength
            ? EhlersStreamingWindow.GetOffsetValue(_powerValues, pwr, _minLength)
            : 0;
        double num = 0;
        double denom = 0;
        for (var period = _minLength; period <= _maxLength; period++)
        {
            var prevPwr = EhlersStreamingWindow.GetOffsetValue(_powerValues, pwr, period);
            maxPwr = prevPwr > maxPwr ? prevPwr : maxPwr;
            var db = maxPwr > 0 && prevPwr > 0
                ? -10 * Math.Log(0.01 / (1 - (0.99 * prevPwr / maxPwr))) / Math.Log(10)
                : 0;
            db = db > 20 ? 20 : db;

            if (db < 3)
            {
                num += period * (3 - db);
                denom += 3 - db;
            }
        }

        var dominantCycle = denom != 0 ? num / denom : 0;

        if (isFinal)
        {
            _hpValues.TryAdd(hp, out _);
            _cleanedValues.TryAdd(cleaned, out _);
            _powerValues.TryAdd(pwr, out _);
            _prevValue = value;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Edft", dominantCycle }
            };
        }

        return new StreamingIndicatorStateResult(dominantCycle, outputs);
    }

    public void Dispose()
    {
        _hpValues.Dispose();
        _cleanedValues.Dispose();
        _powerValues.Dispose();
    }
}

public sealed class EhlersDiscreteFourierTransformSpectralEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly PooledRingBuffer<double> _roofingValues;
    private readonly double[] _rArray;

    public EhlersDiscreteFourierTransformSpectralEstimateState(int length1 = 48, int length2 = 10,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2, inputName);
        _roofingValues = new PooledRingBuffer<double>(_length1 + 1);
        _rArray = new double[_length1 + 1];
    }

    public EhlersDiscreteFourierTransformSpectralEstimateState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, _length2, selector);
        _roofingValues = new PooledRingBuffer<double>(_length1 + 1);
        _rArray = new double[_length1 + 1];
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

            maxPwr = Math.Max(r, maxPwr);
            var pwr = maxPwr != 0 ? r / maxPwr : 0;

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

public sealed class EhlersDistanceCoefficientFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersDistanceCoefficientFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length * 2);
    }

    public EhlersDistanceCoefficientFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolvedMin = Math.Max(1, minLength);
        var resolvedMax = Math.Max(maxLength, resolvedMin);
        var resolvedLength1 = Math.Max(1, length1);
        var twoPiPer = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);
        _input = new StreamingInputResolver(inputName, null);
        _sdfb = new EhlersSpectrumDerivedFilterBankEngine(resolvedMin, resolvedMax, resolvedLength1, Math.Max(1, length2));
        _hpValues = new PooledRingBuffer<double>(5);
        _v1Values = new PooledRingBuffer<double>(2);
    }

    public EhlersDominantCycleTunedBypassFilterState(int minLength, int maxLength, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedMin = Math.Max(1, minLength);
        var resolvedMax = Math.Max(maxLength, resolvedMin);
        var resolvedLength1 = Math.Max(1, length1);
        var twoPiPer = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

    public EhlersDualDifferentiatorDominantCycleState(int length1 = 48, int length2 = 20, int length3 = 8,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, resolvedLength2, inputName);
        _realValues = new PooledRingBuffer<double>(2);
        _imagValues = new PooledRingBuffer<double>(2);
    }

    public EhlersDualDifferentiatorDominantCycleState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV2State(_length1, resolvedLength2, selector);
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
        var period = (real * qDot) - (imag * iDot) != 0
            ? 2 * Math.PI * ((real * real) + (imag * imag)) / ((-real * qDot) + (imag * iDot))
            : 0;
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

public sealed class EhlersEarlyOnsetTrendIndicatorState : IStreamingIndicatorState
{
    private readonly double _k;
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV1Engine _hp;
    private readonly EhlersSuperSmootherFilterEngine _smoother;
    private double _peak;
    private bool _hasPeak;

    public EhlersEarlyOnsetTrendIndicatorState(int length1 = 30, int length2 = 100, double k = 0.85,
        InputName inputName = InputName.Close)
    {
        _k = k;
        _input = new StreamingInputResolver(inputName, null);
        _hp = new HighPassFilterV1Engine(Math.Max(1, length2), 1);
        _smoother = new EhlersSuperSmootherFilterEngine(Math.Max(1, length1));
    }

    public EhlersEarlyOnsetTrendIndicatorState(int length1, int length2, double k, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _k = k;
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class EhlersEnhancedSignalToNoiseRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _smoothValues;
    private readonly PooledRingBuffer<double> _q3Values;
    private double _prevNoise;
    private double _prevSnr;

    public EhlersEnhancedSignalToNoiseRatioState(int length = 6, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _smoothValues = new PooledRingBuffer<double>(2);
        _q3Values = new PooledRingBuffer<double>(50);
    }

    public EhlersEnhancedSignalToNoiseRatioState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var snr = (0.33 * (10 * Math.Log(temp) / Math.Log(10))) + (0.67 * _prevSnr);

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

public sealed class EhlersEvenBetterSineWaveIndicatorState : IStreamingIndicatorState
{
    private readonly double _a1;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevHp;
    private double _prevFilt1;
    private double _prevFilt2;
    private int _index;

    public EhlersEvenBetterSineWaveIndicatorState(int length1 = 40, int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var piHp = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _a1 = (1 - Math.Sin(piHp)) / Math.Cos(piHp);
        var a2 = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength2, -0.01, -0.99));
        var b = 2 * a2 * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength2, 0.99, 0.01));
        _c2 = b;
        _c3 = -a2 * a2;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersEvenBetterSineWaveIndicatorState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var piHp = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _a1 = (1 - Math.Sin(piHp)) / Math.Cos(piHp);
        var a2 = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength2, -0.01, -0.99));
        var b = 2 * a2 * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength2, 0.99, 0.01));
        _c2 = b;
        _c3 = -a2 * a2;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersEvenBetterSineWaveIndicator;

    public void Reset()
    {
        _prevValue = 0;
        _prevHp = 0;
        _prevFilt1 = 0;
        _prevFilt2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevHp = _index >= 1 ? _prevHp : 0;
        var prevFilt1 = _index >= 1 ? _prevFilt1 : 0;
        var prevFilt2 = _index >= 2 ? _prevFilt2 : 0;

        var diff = _index >= 1 ? value - prevValue : 0;
        var hp = ((0.5 * (1 + _a1)) * diff) + (_a1 * prevHp);
        var filt = (_c1 * ((hp + prevHp) / 2)) + (_c2 * prevFilt1) + (_c3 * prevFilt2);
        var wave = (filt + prevFilt1 + prevFilt2) / 3;
        var pwr = ((filt * filt) + (prevFilt1 * prevFilt1) + (prevFilt2 * prevFilt2)) / 3;
        var ebsi = pwr > 0 ? wave / MathHelper.Sqrt(pwr) : 0;

        if (isFinal)
        {
            _prevValue = value;
            _prevHp = hp;
            _prevFilt2 = _prevFilt1;
            _prevFilt1 = filt;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ebsi", ebsi }
            };
        }

        return new StreamingIndicatorStateResult(ebsi, outputs);
    }
}

public sealed class EhlersFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersFilterState(int length1 = 15, int length2 = 5, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length1 + _length2);
    }

    public EhlersFilterState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class EhlersFiniteImpulseResponseFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly double _coef4;
    private readonly double _coef5;
    private readonly double _coef6;
    private readonly double _coef7;
    private readonly double _coefSum;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersFiniteImpulseResponseFilterState(double coef1 = 1, double coef2 = 3.5, double coef3 = 4.5,
        double coef4 = 3, double coef5 = 0.5, double coef6 = -0.5, double coef7 = -1.5,
        InputName inputName = InputName.Close)
    {
        _coef1 = coef1;
        _coef2 = coef2;
        _coef3 = coef3;
        _coef4 = coef4;
        _coef5 = coef5;
        _coef6 = coef6;
        _coef7 = coef7;
        _coefSum = coef1 + coef2 + coef3 + coef4 + coef5 + coef6 + coef7;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(6);
    }

    public EhlersFiniteImpulseResponseFilterState(double coef1, double coef2, double coef3, double coef4,
        double coef5, double coef6, double coef7, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _coef1 = coef1;
        _coef2 = coef2;
        _coef3 = coef3;
        _coef4 = coef4;
        _coef5 = coef5;
        _coef6 = coef6;
        _coef7 = coef7;
        _coefSum = coef1 + coef2 + coef3 + coef4 + coef5 + coef6 + coef7;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(6);
    }

    public IndicatorName Name => IndicatorName.EhlersFiniteImpulseResponseFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevValue3 = EhlersStreamingWindow.GetOffsetValue(_values, value, 3);
        var prevValue4 = EhlersStreamingWindow.GetOffsetValue(_values, value, 4);
        var prevValue5 = EhlersStreamingWindow.GetOffsetValue(_values, value, 5);
        var prevValue6 = EhlersStreamingWindow.GetOffsetValue(_values, value, 6);
        var filter = ((_coef1 * value) + (_coef2 * prevValue1) + (_coef3 * prevValue2) +
                      (_coef4 * prevValue3) + (_coef5 * prevValue4) + (_coef6 * prevValue5) +
                      (_coef7 * prevValue6)) / _coefSum;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Efirf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersFisherizedDeviationScaledOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersDeviationScaledMovingAverageEngine? _edsmaEngine;
    private readonly IMovingAverageSmoother? _smoother;
    private double _prevEfdso;
    private bool _hasPrev;

    public EhlersFisherizedDeviationScaledOscillatorState(MovingAvgType maType = MovingAvgType.EhlersDeviationScaledMovingAverage,
        int fastLength = 20, int slowLength = 40, InputName inputName = InputName.Close)
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

        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersFisherizedDeviationScaledOscillatorState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

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

        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class EhlersFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;
    private double _prevNValue;
    private double _prevFisher;
    private bool _hasPrev;

    public EhlersFisherTransformState(int length = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersFisherTransformState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var ratio = maxH - minL != 0 ? (value - minL) / (maxH - minL) : 0;
        var prevNValue = _hasPrev ? _prevNValue : 0;
        var nValue = MathHelper.MinOrMax((0.33 * 2 * (ratio - 0.5)) + (0.67 * prevNValue), 0.999, -0.999);
        var prevFisher = _hasPrev ? _prevFisher : 0;
        var fisher = (0.5 * Math.Log((1 + nValue) / (1 - nValue))) + (0.5 * prevFisher);

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

public sealed class EhlersFMDemodulatorIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersFMDemodulatorIndicatorState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
        int fastLength = 10, int slowLength = 30, InputName inputName = InputName.Close)
    {
        _fastLength = Math.Max(1, fastLength);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = EhlersStreamingSmootherFactory.Create(maType, Math.Max(1, slowLength));
    }

    public EhlersFMDemodulatorIndicatorState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

    public EhlersFourierSeriesAnalysisState(int length = 20, double bw = 0.1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
        _bp1Values = new PooledRingBuffer<double>(_length);
        _bp2Values = new PooledRingBuffer<double>(_length);
        _bp3Values = new PooledRingBuffer<double>(_length);
        _q1Values = new PooledRingBuffer<double>(_length);
        _q2Values = new PooledRingBuffer<double>(_length);
        _q3Values = new PooledRingBuffer<double>(_length);
        _waveValues = new PooledRingBuffer<double>(2);

        _l1 = Math.Cos(2 * Math.PI / _length);
        var g1 = Math.Cos(bw * 2 * Math.PI / _length);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);

        _l2 = Math.Cos(2 * Math.PI / ((double)_length / 2));
        var g2 = Math.Cos(bw * 2 * Math.PI / ((double)_length / 2));
        _s2 = (1 / g2) - MathHelper.Sqrt((1 / (g2 * g2)) - 1);

        _l3 = Math.Cos(2 * Math.PI / ((double)_length / 3));
        var g3 = Math.Cos(bw * 2 * Math.PI / ((double)_length / 3));
        _s3 = (1 / g3) - MathHelper.Sqrt((1 / (g3 * g3)) - 1);
    }

    public EhlersFourierSeriesAnalysisState(int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
        _bp1Values = new PooledRingBuffer<double>(_length);
        _bp2Values = new PooledRingBuffer<double>(_length);
        _bp3Values = new PooledRingBuffer<double>(_length);
        _q1Values = new PooledRingBuffer<double>(_length);
        _q2Values = new PooledRingBuffer<double>(_length);
        _q3Values = new PooledRingBuffer<double>(_length);
        _waveValues = new PooledRingBuffer<double>(2);

        _l1 = Math.Cos(2 * Math.PI / _length);
        var g1 = Math.Cos(bw * 2 * Math.PI / _length);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);

        _l2 = Math.Cos(2 * Math.PI / ((double)_length / 2));
        var g2 = Math.Cos(bw * 2 * Math.PI / ((double)_length / 2));
        _s2 = (1 / g2) - MathHelper.Sqrt((1 / (g2 * g2)) - 1);

        _l3 = Math.Cos(2 * Math.PI / ((double)_length / 3));
        var g3 = Math.Cos(bw * 2 * Math.PI / ((double)_length / 3));
        _s3 = (1 / g3) - MathHelper.Sqrt((1 / (g3 * g3)) - 1);
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
        var q1 = _index <= 4 ? 0 : (_length / 2) * Math.PI * (bp1 - prevBp1_1);

        var bp2 = _index <= 3
            ? 0
            : (0.5 * (1 - _s2) * (value - prevValue)) + (_l2 * (1 + _s2) * prevBp2_1) - (_s2 * prevBp2_2);
        var q2 = _index <= 4 ? 0 : (_length / 2) * Math.PI * (bp2 - prevBp2_1);

        var bp3 = _index <= 3
            ? 0
            : (0.5 * (1 - _s3) * (value - prevValue)) + (_l3 * (1 + _s3) * prevBp3_1) - (_s3 * prevBp3_2);
        var q3 = _index <= 4 ? 0 : (_length / 2) * Math.PI * (bp3 - prevBp3_1);

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
        var roc = _length / Math.PI * 4 * (wave - prevWave2);

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

public sealed class EhlersFractalAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _halfP;
    private readonly RollingWindowMax _highWindow1;
    private readonly RollingWindowMin _lowWindow1;
    private readonly RollingWindowMax _highWindow2;
    private readonly RollingWindowMin _lowWindow2;
    private readonly StreamingInputResolver _input;
    private double _prevFilter;
    private bool _hasPrev;

    public EhlersFractalAdaptiveMovingAverageState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _halfP = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _highWindow1 = new RollingWindowMax(_length);
        _lowWindow1 = new RollingWindowMin(_length);
        _highWindow2 = new RollingWindowMax(_halfP);
        _lowWindow2 = new RollingWindowMin(_halfP);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersFractalAdaptiveMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _halfP = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _highWindow1 = new RollingWindowMax(_length);
        _lowWindow1 = new RollingWindowMin(_length);
        _highWindow2 = new RollingWindowMax(_halfP);
        _lowWindow2 = new RollingWindowMin(_halfP);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersFractalAdaptiveMovingAverage;

    public void Reset()
    {
        _highWindow1.Reset();
        _lowWindow1.Reset();
        _highWindow2.Reset();
        _lowWindow2.Reset();
        _prevFilter = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highestHigh1 = isFinal ? _highWindow1.Add(bar.High, out _) : _highWindow1.Preview(bar.High, out _);
        var lowestLow1 = isFinal ? _lowWindow1.Add(bar.Low, out _) : _lowWindow1.Preview(bar.Low, out _);
        var highestHigh2 = isFinal ? _highWindow2.Add(bar.High, out _) : _highWindow2.Preview(bar.High, out _);
        var lowestLow2 = isFinal ? _lowWindow2.Add(bar.Low, out _) : _lowWindow2.Preview(bar.Low, out _);
        var highestHigh3 = highestHigh2;
        var lowestLow3 = lowestLow2;

        var n3 = (highestHigh1 - lowestLow1) / _length;
        var n1 = (highestHigh2 - lowestLow2) / _halfP;
        var n2 = (highestHigh3 - lowestLow3) / _halfP;
        var dm = n1 > 0 && n2 > 0 && n3 > 0 ? (Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2) : 0;

        var alpha = MathHelper.MinOrMax(MathHelper.Exp(-4.6 * (dm - 1)), 1, 0.01);
        var prevFilter = _hasPrev ? _prevFilter : value;
        var filter = (alpha * value) + ((1 - alpha) * prevFilter);

        if (isFinal)
        {
            _prevFilter = filter;
            _hasPrev = true;
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

    public void Dispose()
    {
        _highWindow1.Dispose();
        _lowWindow1.Dispose();
        _highWindow2.Dispose();
        _lowWindow2.Dispose();
    }
}

public sealed class EhlersGaussianFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha1;
    private readonly double _alpha2;
    private readonly double _alpha3;
    private readonly double _alpha4;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _gf1Values;
    private readonly PooledRingBuffer<double> _gf2Values;
    private readonly PooledRingBuffer<double> _gf3Values;
    private readonly PooledRingBuffer<double> _gf4Values;

    public EhlersGaussianFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var cosVal = MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01);
        var beta1 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 1.0) - 1);
        var beta2 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 0.5) - 1);
        var beta3 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 1.0 / 3) - 1);
        var beta4 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 0.25) - 1);
        _alpha1 = -beta1 + MathHelper.Sqrt(MathHelper.Pow(beta1, 2) + (2 * beta1));
        _alpha2 = -beta2 + MathHelper.Sqrt(MathHelper.Pow(beta2, 2) + (2 * beta2));
        _alpha3 = -beta3 + MathHelper.Sqrt(MathHelper.Pow(beta3, 2) + (2 * beta3));
        _alpha4 = -beta4 + MathHelper.Sqrt(MathHelper.Pow(beta4, 2) + (2 * beta4));
        _input = new StreamingInputResolver(inputName, null);
        _gf1Values = new PooledRingBuffer<double>(4);
        _gf2Values = new PooledRingBuffer<double>(4);
        _gf3Values = new PooledRingBuffer<double>(4);
        _gf4Values = new PooledRingBuffer<double>(4);
    }

    public EhlersGaussianFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var cosVal = MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01);
        var beta1 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 1.0) - 1);
        var beta2 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 0.5) - 1);
        var beta3 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 1.0 / 3) - 1);
        var beta4 = (1 - Math.Cos(cosVal)) / (MathHelper.Pow(2, 0.25) - 1);
        _alpha1 = -beta1 + MathHelper.Sqrt(MathHelper.Pow(beta1, 2) + (2 * beta1));
        _alpha2 = -beta2 + MathHelper.Sqrt(MathHelper.Pow(beta2, 2) + (2 * beta2));
        _alpha3 = -beta3 + MathHelper.Sqrt(MathHelper.Pow(beta3, 2) + (2 * beta3));
        _alpha4 = -beta4 + MathHelper.Sqrt(MathHelper.Pow(beta4, 2) + (2 * beta4));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _gf1Values = new PooledRingBuffer<double>(4);
        _gf2Values = new PooledRingBuffer<double>(4);
        _gf3Values = new PooledRingBuffer<double>(4);
        _gf4Values = new PooledRingBuffer<double>(4);
    }

    public IndicatorName Name => IndicatorName.EhlersGaussianFilter;

    public void Reset()
    {
        _gf1Values.Clear();
        _gf2Values.Clear();
        _gf3Values.Clear();
        _gf4Values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevGf1 = EhlersStreamingWindow.GetOffsetValue(_gf1Values, 1);
        var prevGf2_1 = EhlersStreamingWindow.GetOffsetValue(_gf2Values, 1);
        var prevGf2_2 = EhlersStreamingWindow.GetOffsetValue(_gf2Values, 2);
        var prevGf3_1 = EhlersStreamingWindow.GetOffsetValue(_gf3Values, 1);
        var prevGf3_2 = EhlersStreamingWindow.GetOffsetValue(_gf3Values, 2);
        var prevGf3_3 = EhlersStreamingWindow.GetOffsetValue(_gf3Values, 3);
        var prevGf4_1 = EhlersStreamingWindow.GetOffsetValue(_gf4Values, 1);
        var prevGf4_2 = EhlersStreamingWindow.GetOffsetValue(_gf4Values, 2);
        var prevGf4_3 = EhlersStreamingWindow.GetOffsetValue(_gf4Values, 3);
        var prevGf4_4 = EhlersStreamingWindow.GetOffsetValue(_gf4Values, 4);

        var gf1 = (_alpha1 * value) + ((1 - _alpha1) * prevGf1);
        var gf2 = (MathHelper.Pow(_alpha2, 2) * value) + (2 * (1 - _alpha2) * prevGf2_1) -
                  (MathHelper.Pow(1 - _alpha2, 2) * prevGf2_2);
        var gf3 = (MathHelper.Pow(_alpha3, 3) * value) + (3 * (1 - _alpha3) * prevGf3_1) -
                  (3 * MathHelper.Pow(1 - _alpha3, 2) * prevGf3_2) + (MathHelper.Pow(1 - _alpha3, 3) * prevGf3_3);
        var gf4 = (MathHelper.Pow(_alpha4, 4) * value) + (4 * (1 - _alpha4) * prevGf4_1) -
                  (6 * MathHelper.Pow(1 - _alpha4, 2) * prevGf4_2) +
                  (4 * MathHelper.Pow(1 - _alpha4, 3) * prevGf4_3) - (MathHelper.Pow(1 - _alpha4, 4) * prevGf4_4);

        if (isFinal)
        {
            _gf1Values.TryAdd(gf1, out _);
            _gf2Values.TryAdd(gf2, out _);
            _gf3Values.TryAdd(gf3, out _);
            _gf4Values.TryAdd(gf4, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Egf1", gf1 },
                { "Egf2", gf2 },
                { "Egf3", gf3 },
                { "Egf4", gf4 }
            };
        }

        return new StreamingIndicatorStateResult(gf4, outputs);
    }

    public void Dispose()
    {
        _gf1Values.Dispose();
        _gf2Values.Dispose();
        _gf3Values.Dispose();
        _gf4Values.Dispose();
    }
}

public sealed class EhlersHammingMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _pedestal;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersHammingMovingAverageState(int length = 20, double pedestal = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _pedestal = pedestal;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersHammingMovingAverageState(int length, double pedestal, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _pedestal = pedestal;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersHammingMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double filtSum = 0;
        double coefSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var prevV = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var sine = Math.Sin(_pedestal + ((Math.PI - (2 * _pedestal)) * ((double)j / (_length - 1))));
            filtSum += sine * prevV;
            coefSum += sine;
        }

        var filt = coefSum != 0 ? filtSum / coefSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

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
        _values.Dispose();
    }
}

internal sealed class Ehlers2PoleSuperSmootherFilterV2Smoother : IMovingAverageSmoother
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private double _prevValue;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public Ehlers2PoleSuperSmootherFilterV2Smoother(int length)
    {
        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
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

    public void Dispose()
    {
    }
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
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.999));
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
    private readonly SimpleMovingAverageSmoother _meanSmoother;
    private readonly SimpleMovingAverageSmoother _varianceSmoother;

    public StandardDeviationVolatilityEngine(int length)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = new SimpleMovingAverageSmoother(resolved);
        _varianceSmoother = new SimpleMovingAverageSmoother(resolved);
    }

    public double Next(double value, bool isFinal)
    {
        var mean = _meanSmoother.Next(value, isFinal);
        var deviation = value - mean;
        var variance = _varianceSmoother.Next(deviation * deviation, isFinal);
        return MathHelper.Sqrt(variance);
    }

    public void Reset()
    {
        _meanSmoother.Reset();
        _varianceSmoother.Reset();
    }

    public void Dispose()
    {
        _meanSmoother.Dispose();
        _varianceSmoother.Dispose();
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
    private readonly PooledRingBuffer<double> _realValues;
    private readonly PooledRingBuffer<double> _imagValues;
    private readonly PooledRingBuffer<double> _q1Values;
    private readonly PooledRingBuffer<double> _dcValues;
    private readonly double[] _medianScratch;
    private double _prevValue;
    private int _index;

    public EhlersSpectrumDerivedFilterBankEngine(int minLength, int maxLength, int length1, int length2)
    {
        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(maxLength, _minLength);
        var resolvedLength1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        var twoPiPer = MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01);
        _alpha1 = (1 - Math.Sin(twoPiPer)) / Math.Cos(twoPiPer);
        _hpValues = new PooledRingBuffer<double>(5);
        _smoothHpValues = new PooledRingBuffer<double>(_maxLength);
        _realValues = new PooledRingBuffer<double>(_maxLength * 2);
        _imagValues = new PooledRingBuffer<double>(_maxLength * 2);
        _q1Values = new PooledRingBuffer<double>(_maxLength * 2);
        _dcValues = new PooledRingBuffer<double>(_length2);
        _medianScratch = new double[_length2];
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _index >= 1 ? _prevValue : 0;
        var delta = Math.Max((-0.015 * _index) + 0.5, 0.15);
        var prevHp1 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 1);
        var prevHp2 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 2);
        var prevHp3 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 3);
        var prevHp4 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 4);
        var prevHp5 = EhlersStreamingWindow.GetOffsetValue(_hpValues, 5);

        var hp = _index < 7 ? value : (0.5 * (1 + _alpha1) * (value - prevValue)) + (_alpha1 * prevHp1);
        var prevSmoothHp = EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, 1);
        var smoothHp = _index < 7
            ? value - prevValue
            : (hp + (2 * prevHp1) + (3 * prevHp2) + (3 * prevHp3) + (2 * prevHp4) + prevHp5) / 12;

        double num = 0;
        double denom = 0;
        double dc = 0;
        double real = 0;
        double imag = 0;
        double q1 = 0;
        double maxAmpl = 0;
        for (var j = _minLength; j <= _maxLength; j++)
        {
            var beta = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / j, 0.99, 0.01));
            var gamma = 1 / Math.Cos(MathHelper.MinOrMax(4 * Math.PI * delta / j, 0.99, 0.01));
            var alpha = gamma - MathHelper.Sqrt((gamma * gamma) - 1);
            var priorSmoothHp = EhlersStreamingWindow.GetOffsetValue(_smoothHpValues, j);
            var prevReal = EhlersStreamingWindow.GetOffsetValue(_realValues, j);
            var priorReal = EhlersStreamingWindow.GetOffsetValue(_realValues, j * 2);
            var prevImag = EhlersStreamingWindow.GetOffsetValue(_imagValues, j);
            var priorImag = EhlersStreamingWindow.GetOffsetValue(_imagValues, j * 2);
            var prevQ1 = EhlersStreamingWindow.GetOffsetValue(_q1Values, j);

            q1 = j / Math.PI * 2 * (smoothHp - prevSmoothHp);
            real = (0.5 * (1 - alpha) * (smoothHp - priorSmoothHp)) + (beta * (1 + alpha) * prevReal) - (alpha * priorReal);
            imag = (0.5 * (1 - alpha) * (q1 - prevQ1)) + (beta * (1 + alpha) * prevImag) - (alpha * priorImag);
            var ampl = (real * real) + (imag * imag);
            maxAmpl = ampl > maxAmpl ? ampl : maxAmpl;
            var db = maxAmpl != 0 && ampl / maxAmpl > 0
                ? -_length2 * Math.Log(0.01 / (1 - (0.99 * ampl / maxAmpl))) / Math.Log(_length2)
                : 0;
            db = db > _maxLength ? _maxLength : db;
            if (db <= 3)
            {
                num += j * (_maxLength - db);
                denom += _maxLength - db;
            }
            dc = denom != 0 ? num / denom : 0;
        }

        var domCyc = EhlersStreamingWindow.GetMedian(_dcValues, dc, _medianScratch);

        if (isFinal)
        {
            _hpValues.TryAdd(hp, out _);
            _smoothHpValues.TryAdd(smoothHp, out _);
            _q1Values.TryAdd(q1, out _);
            _realValues.TryAdd(real, out _);
            _imagValues.TryAdd(imag, out _);
            _dcValues.TryAdd(dc, out _);
            _prevValue = value;
            _index++;
        }

        return domCyc;
    }

    public void Reset()
    {
        _hpValues.Clear();
        _smoothHpValues.Clear();
        _realValues.Clear();
        _imagValues.Clear();
        _q1Values.Clear();
        _dcValues.Clear();
        _prevValue = 0;
        _index = 0;
    }

    public void Dispose()
    {
        _hpValues.Dispose();
        _smoothHpValues.Dispose();
        _realValues.Dispose();
        _imagValues.Dispose();
        _q1Values.Dispose();
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
