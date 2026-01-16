using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class Ehlers3PoleSuperSmootherFilterState : IStreamingIndicatorState
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly double _coef4;
    private readonly StreamingInputResolver _input;
    private double _prevFilter1;
    private double _prevFilter2;
    private double _prevFilter3;
    private int _index;

    public Ehlers3PoleSuperSmootherFilterState(int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var arg = MathHelper.MinOrMax(Math.PI / resolved, 0.99, 0.01);
        var a1 = MathHelper.Exp(-arg);
        var b1 = 2 * a1 * Math.Cos(1.738 * arg);
        var c1 = a1 * a1;
        _coef2 = b1 + c1;
        _coef3 = -(c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _input = new StreamingInputResolver(inputName, null);
    }

    public Ehlers3PoleSuperSmootherFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var arg = MathHelper.MinOrMax(Math.PI / resolved, 0.99, 0.01);
        var a1 = MathHelper.Exp(-arg);
        var b1 = 2 * a1 * Math.Cos(1.738 * arg);
        var c1 = a1 * a1;
        _coef2 = b1 + c1;
        _coef3 = -(c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Ehlers3PoleSuperSmootherFilter;

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _prevFilter3 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;
        var prevFilter3 = _prevFilter3;
        var filt = _index < 4
            ? value
            : (_coef1 * value) + (_coef2 * prevFilter1) + (_coef3 * prevFilter2) + (_coef4 * prevFilter3);

        if (isFinal)
        {
            _prevFilter3 = prevFilter2;
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "E3ssf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

public sealed class Ehlers3PoleButterworthFilterV1State : IStreamingIndicatorState
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly double _coef4;
    private readonly StreamingInputResolver _input;
    private double _prevFilter1;
    private double _prevFilter2;
    private double _prevFilter3;

    public Ehlers3PoleButterworthFilterV1State(int length = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(1.738 * Math.PI / resolved, 0.99, 0.01));
        var c = a * a;
        _coef2 = b + c;
        _coef3 = -(c + (b * c));
        _coef4 = c * c;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _input = new StreamingInputResolver(inputName, null);
    }

    public Ehlers3PoleButterworthFilterV1State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(1.738 * Math.PI / resolved, 0.99, 0.01));
        var c = a * a;
        _coef2 = b + c;
        _coef3 = -(c + (b * c));
        _coef4 = c * c;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Ehlers3PoleButterworthFilterV1;

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _prevFilter3 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;
        var prevFilter3 = _prevFilter3;
        var filt = (_coef1 * value) + (_coef2 * prevFilter1) + (_coef3 * prevFilter2) + (_coef4 * prevFilter3);

        if (isFinal)
        {
            _prevFilter3 = prevFilter2;
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "E3bf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

public sealed class Ehlers3PoleButterworthFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly double _coef4;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevFilter1;
    private double _prevFilter2;
    private double _prevFilter3;
    private int _index;

    public Ehlers3PoleButterworthFilterV2State(int length = 15, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-Math.PI / resolved, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(1.738 * Math.PI / resolved, 0.99, 0.01));
        var c1 = a1 * a1;
        _coef2 = b1 + c1;
        _coef3 = -(c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = (1 - b1 + c1) * (1 - c1) / 8;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(3);
    }

    public Ehlers3PoleButterworthFilterV2State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-Math.PI / resolved, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(1.738 * Math.PI / resolved, 0.99, 0.01));
        var c1 = a1 * a1;
        _coef2 = b1 + c1;
        _coef3 = -(c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = (1 - b1 + c1) * (1 - c1) / 8;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.Ehlers3PoleButterworthFilterV2;

    public void Reset()
    {
        _values.Clear();
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _prevFilter3 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var prevValue3 = _values.Count >= 3 ? _values[_values.Count - 3] : 0;
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;
        var prevFilter3 = _prevFilter3;

        var filt = _index < 4
            ? value
            : (_coef1 * (value + (3 * prevValue1) + (3 * prevValue2) + prevValue3))
              + (_coef2 * prevFilter1) + (_coef3 * prevFilter2) + (_coef4 * prevFilter3);

        if (isFinal)
        {
            _prevFilter3 = prevFilter2;
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "E3bf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersAdaptiveCyberCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCyberCyclePeriodState _periodState;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smooth;
    private double _prevAc1;
    private double _prevAc2;
    private int _index;

    public EhlersAdaptiveCyberCycleState(int length = 5, double alpha = 0.07, InputName inputName = InputName.Close)
    {
        _periodState = new AdaptiveCyberCyclePeriodState(length, alpha);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(4);
        _smooth = new PooledRingBuffer<double>(3);
    }

    public EhlersAdaptiveCyberCycleState(int length, double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _periodState = new AdaptiveCyberCyclePeriodState(length, alpha);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(4);
        _smooth = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveCyberCycle;

    public void Reset()
    {
        _periodState.Reset();
        _values.Clear();
        _smooth.Clear();
        _prevAc1 = 0;
        _prevAc2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = _periodState.Next(value, isFinal);
        var prevValue1 = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var prevValue3 = _values.Count >= 3 ? _values[_values.Count - 3] : 0;
        var prevSmooth1 = _smooth.Count >= 1 ? _smooth[_smooth.Count - 1] : 0;
        var prevSmooth2 = _smooth.Count >= 2 ? _smooth[_smooth.Count - 2] : 0;
        var smooth = (value + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;
        var a1 = 2 / (period + 1);
        var ac = _index < 7
            ? (value - (2 * prevValue1) + prevValue2) / 4
            : (MathHelper.Pow(1 - (0.5 * a1), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2))
              + (2 * (1 - a1) * _prevAc1) - (MathHelper.Pow(1 - a1, 2) * _prevAc2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smooth.TryAdd(smooth, out _);
            _prevAc2 = _prevAc1;
            _prevAc1 = ac;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eacc", ac },
                { "Period", period }
            };
        }

        return new StreamingIndicatorStateResult(ac, outputs);
    }

    public void Dispose()
    {
        _periodState.Dispose();
        _values.Dispose();
        _smooth.Dispose();
    }
}

public sealed class EhlersAdaptiveCenterOfGravityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCyberCyclePeriodState _periodState;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public EhlersAdaptiveCenterOfGravityOscillatorState(int length = 5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _periodState = new AdaptiveCyberCyclePeriodState(resolved, 0.07);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(Math.Max(64, resolved * 2));
    }

    public EhlersAdaptiveCenterOfGravityOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _periodState = new AdaptiveCyberCyclePeriodState(resolved, 0.07);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(Math.Max(64, resolved * 2));
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveCenterOfGravityOscillator;

    public void Reset()
    {
        _periodState.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = _periodState.Next(value, isFinal);
        var intPeriod = (int)Math.Ceiling(period / 2);
        double num = 0;
        double denom = 0;
        for (var j = 0; j < intPeriod; j++)
        {
            var prevPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            num += (1 + j) * prevPrice;
            denom += prevPrice;
        }

        var halfPeriod = (intPeriod + 1) / 2;
        var cg = denom != 0 ? (-num / denom) + halfPeriod : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eacog", cg }
            };
        }

        return new StreamingIndicatorStateResult(cg, outputs);
    }

    public void Dispose()
    {
        _periodState.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersAdaptiveLaguerreFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly StreamingInputResolver _input;
    private readonly RollingWindowMax _diffMax;
    private readonly RollingWindowMin _diffMin;
    private readonly PooledRingBuffer<double> _midValues;
    private readonly double[] _medianScratch;
    private double _prevValue;
    private double _prevL0;
    private double _prevL1;
    private double _prevL2;
    private double _prevL3;
    private double _prevFilter;
    private double _prevAlpha;
    private bool _hasPrev;

    public EhlersAdaptiveLaguerreFilterState(int length1 = 14, int length2 = 5, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(inputName, null);
        _diffMax = new RollingWindowMax(_length1);
        _diffMin = new RollingWindowMin(_length1);
        _midValues = new PooledRingBuffer<double>(resolved2);
        _medianScratch = new double[resolved2];
        _prevAlpha = (double)2 / (_length1 + 1);
    }

    public EhlersAdaptiveLaguerreFilterState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _diffMax = new RollingWindowMax(_length1);
        _diffMin = new RollingWindowMin(_length1);
        _midValues = new PooledRingBuffer<double>(resolved2);
        _medianScratch = new double[resolved2];
        _prevAlpha = (double)2 / (_length1 + 1);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveLaguerreFilter;

    public void Reset()
    {
        _diffMax.Reset();
        _diffMin.Reset();
        _midValues.Clear();
        _prevValue = 0;
        _prevL0 = 0;
        _prevL1 = 0;
        _prevL2 = 0;
        _prevL3 = 0;
        _prevFilter = 0;
        _prevAlpha = (double)2 / (_length1 + 1);
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevL0 = _hasPrev ? _prevL0 : value;
        var prevL1 = _hasPrev ? _prevL1 : value;
        var prevL2 = _hasPrev ? _prevL2 : value;
        var prevL3 = _hasPrev ? _prevL3 : value;
        var prevFilter = _hasPrev ? _prevFilter : value;

        var diff = Math.Abs(value - prevFilter);
        var highestHigh = isFinal ? _diffMax.Add(diff, out _) : _diffMax.Preview(diff, out _);
        var lowestLow = isFinal ? _diffMin.Add(diff, out _) : _diffMin.Preview(diff, out _);
        var range = highestHigh - lowestLow;
        var mid = range != 0 ? (diff - lowestLow) / range : 0;
        var median = EhlersStreamingWindow.GetMedian(_midValues, mid, _medianScratch);
        var alpha = mid != 0 ? median : _prevAlpha;

        var l0 = (alpha * value) + ((1 - alpha) * prevL0);
        var l1 = (-1 * (1 - alpha) * l0) + prevL0 + ((1 - alpha) * prevL1);
        var l2 = (-1 * (1 - alpha) * l1) + prevL1 + ((1 - alpha) * prevL2);
        var l3 = (-1 * (1 - alpha) * l2) + prevL2 + ((1 - alpha) * prevL3);
        var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;

        if (isFinal)
        {
            _midValues.TryAdd(mid, out _);
            _prevValue = value;
            _prevL0 = l0;
            _prevL1 = l1;
            _prevL2 = l2;
            _prevL3 = l3;
            _prevFilter = filter;
            _prevAlpha = alpha;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ealf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _diffMax.Dispose();
        _diffMin.Dispose();
        _midValues.Dispose();
    }
}

public sealed class EhlersAllPassPhaseShifterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _a2;
    private readonly double _a3;
    private readonly double _b2;
    private readonly double _b3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevPhaser1;
    private double _prevPhaser2;

    public EhlersAllPassPhaseShifterState(int length = 20, double qq = 0.5, InputName inputName = InputName.Close)
    {
        _a2 = qq != 0 && length != 0 ? -2 * Math.Cos(2 * Math.PI / length) / qq : 0;
        _a3 = qq != 0 ? MathHelper.Pow(1 / qq, 2) : 0;
        _b2 = length != 0 ? -2 * qq * Math.Cos(2 * Math.PI / length) : 0;
        _b3 = MathHelper.Pow(qq, 2);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
    }

    public EhlersAllPassPhaseShifterState(int length, double qq, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _a2 = qq != 0 && length != 0 ? -2 * Math.Cos(2 * Math.PI / length) / qq : 0;
        _a3 = qq != 0 ? MathHelper.Pow(1 / qq, 2) : 0;
        _b2 = length != 0 ? -2 * qq * Math.Cos(2 * Math.PI / length) : 0;
        _b3 = MathHelper.Pow(qq, 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersAllPassPhaseShifter;

    public void Reset()
    {
        _values.Clear();
        _prevPhaser1 = 0;
        _prevPhaser2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var prevPhaser1 = _prevPhaser1;
        var prevPhaser2 = _prevPhaser2;

        var phaser = (_b3 * (value + (_a2 * prevValue1) + (_a3 * prevValue2))) - (_b2 * prevPhaser1) - (_b3 * prevPhaser2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevPhaser2 = prevPhaser1;
            _prevPhaser1 = phaser;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eapps", phaser }
            };
        }

        return new StreamingIndicatorStateResult(phaser, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersMotherOfAdaptiveMovingAveragesState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _engine;

    public EhlersMotherOfAdaptiveMovingAveragesState(double fastAlpha = 0.5, double slowAlpha = 0.05,
        InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _engine = new EhlersMotherOfAdaptiveMovingAveragesEngine(fastAlpha, slowAlpha);
    }

    public EhlersMotherOfAdaptiveMovingAveragesState(double fastAlpha, double slowAlpha,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
        _engine = new EhlersMotherOfAdaptiveMovingAveragesEngine(fastAlpha, slowAlpha);
    }

    public IndicatorName Name => IndicatorName.EhlersMotherOfAdaptiveMovingAverages;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
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

public sealed class EhlersAdaptiveRelativeStrengthIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly double _cycPart;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevArsiEma1;

    public EhlersAdaptiveRelativeStrengthIndexV1State(double cycPart = 0.5, InputName inputName = InputName.Close)
    {
        _cycPart = cycPart;
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(64);
    }

    public EhlersAdaptiveRelativeStrengthIndexV1State(double cycPart, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _cycPart = cycPart;
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(64);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveRelativeStrengthIndexV1;

    public void Reset()
    {
        _mama.Reset();
        _values.Clear();
        _prevArsiEma1 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sp = _mama.Next(bar.Close, isFinal).SmoothPeriod;
        var length = (int)Math.Ceiling(_cycPart * sp);

        double cu = 0;
        double cd = 0;
        for (var j = 0; j < length; j++)
        {
            var price = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var pPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, j + 1);
            if (price > pPrice)
            {
                cu += price - pPrice;
            }
            else if (price < pPrice)
            {
                cd += pPrice - price;
            }
        }

        var arsi = cu + cd != 0 ? 100 * cu / (cu + cd) : 0;
        var emaLength = (int)Math.Ceiling(sp);
        var arsiEma = CalculationsHelper.CalculateEMA(arsi, _prevArsiEma1, emaLength);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevArsiEma1 = arsiEma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Earsi", arsi },
                { "Signal", arsiEma }
            };
        }

        return new StreamingIndicatorStateResult(arsi, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersAdaptiveRsiFisherTransformV1State : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersAdaptiveRelativeStrengthIndexV1State _arsiState;
    private double _prevFish1;
    private double _prevFish2;

    public EhlersAdaptiveRsiFisherTransformV1State()
    {
        _arsiState = new EhlersAdaptiveRelativeStrengthIndexV1State();
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveRsiFisherTransformV1;

    public void Reset()
    {
        _arsiState.Reset();
        _prevFish1 = 0;
        _prevFish2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var arsi = _arsiState.Update(bar, isFinal, includeOutputs: false).Value / 100;
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

public sealed class EhlersAdaptiveStochasticIndicatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly double _cycPart;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private double _prevAstocEma1;

    public EhlersAdaptiveStochasticIndicatorV1State(double cycPart = 0.5)
    {
        _cycPart = cycPart;
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _highValues = new PooledRingBuffer<double>(64);
        _lowValues = new PooledRingBuffer<double>(64);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveStochasticIndicatorV1;

    public void Reset()
    {
        _mama.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _prevAstocEma1 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = bar.Close;
        var high = bar.High;
        var low = bar.Low;
        var sp = _mama.Next(close, isFinal).SmoothPeriod;
        var length = (int)Math.Ceiling(_cycPart * sp);

        double hh = high;
        double ll = low;
        for (var j = 0; j < length; j++)
        {
            var h = EhlersStreamingWindow.GetOffsetValue(_highValues, high, j);
            var l = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, j);
            if (h > hh)
            {
                hh = h;
            }
            if (l < ll)
            {
                ll = l;
            }
        }

        var astoc = hh - ll != 0 ? 100 * (close - ll) / (hh - ll) : 0;
        var astocEma = CalculationsHelper.CalculateEMA(astoc, _prevAstocEma1, length);

        if (isFinal)
        {
            _highValues.TryAdd(high, out _);
            _lowValues.TryAdd(low, out _);
            _prevAstocEma1 = astocEma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Easi", astoc },
                { "Signal", astocEma }
            };
        }

        return new StreamingIndicatorStateResult(astoc, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class EhlersAdaptiveCommodityChannelIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly double _cycPart;
    private readonly double _constant;
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _values;
    private double _prevAcciEma1;

    public EhlersAdaptiveCommodityChannelIndexV1State(InputName inputName = InputName.TypicalPrice, double cycPart = 1,
        double constant = 0.015)
    {
        _cycPart = cycPart;
        _constant = constant;
        _input = new StreamingInputResolver(inputName, null);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _values = new PooledRingBuffer<double>(64);
    }

    public EhlersAdaptiveCommodityChannelIndexV1State(InputName inputName, double cycPart, double constant,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _cycPart = cycPart;
        _constant = constant;
        _input = new StreamingInputResolver(inputName, selector);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _values = new PooledRingBuffer<double>(64);
    }

    public IndicatorName Name => IndicatorName.EhlersAdaptiveCommodityChannelIndexV1;

    public void Reset()
    {
        _mama.Reset();
        _values.Clear();
        _prevAcciEma1 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var tp = _input.GetValue(bar);
        var sp = _mama.Next(bar.Close, isFinal).SmoothPeriod;
        var length = (int)Math.Ceiling(_cycPart * sp);

        double avg = 0;
        for (var j = 0; j < length; j++)
        {
            var prevMp = EhlersStreamingWindow.GetOffsetValue(_values, tp, j);
            avg += prevMp;
        }
        avg /= length;

        double md = 0;
        for (var j = 0; j < length; j++)
        {
            var prevMp = EhlersStreamingWindow.GetOffsetValue(_values, tp, j);
            md += Math.Abs(prevMp - avg);
        }
        md /= length;

        var acci = md != 0 ? (tp - avg) / (_constant * md) : 0;
        var emaLength = (int)Math.Ceiling(sp);
        var acciEma = CalculationsHelper.CalculateEMA(acci, _prevAcciEma1, emaLength);

        if (isFinal)
        {
            _values.TryAdd(tp, out _);
            _prevAcciEma1 = acciEma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eacci", acci },
                { "Signal", acciEma }
            };
        }

        return new StreamingIndicatorStateResult(acci, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _values.Dispose();
    }
}

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
        var snapshot = _mama.Next(bar.Close, isFinal);
        var range = (0.1 * (bar.High - bar.Low)) + (0.9 * _prevRange);
        var temp = range != 0 ? (snapshot.Real + snapshot.Imag) / (range * range) : 0;
        var snr = (0.25 * ((10 * Math.Log(temp) / Math.Log(10)) + _length)) + (0.75 * _prevSnr);

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

public sealed class EhlersAMDetectorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _absDerMax;
    private readonly IMovingAverageSmoother _volMa;
    private readonly IMovingAverageSmoother _volSignalMa;

    public EhlersAMDetectorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 4,
        int length2 = 8)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _absDerMax = new RollingWindowMax(resolved1);
        _volMa = MovingAverageSmootherFactory.Create(maType, resolved2);
        _volSignalMa = MovingAverageSmootherFactory.Create(maType, resolved2);
    }

    public IndicatorName Name => IndicatorName.EhlersAMDetector;

    public void Reset()
    {
        _absDerMax.Reset();
        _volMa.Reset();
        _volSignalMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var absDer = Math.Abs(bar.Close - bar.Open);
        var env = isFinal ? _absDerMax.Add(absDer, out _) : _absDerMax.Preview(absDer, out _);
        var vol = _volMa.Next(env, isFinal);
        var volEma = _volSignalMa.Next(vol, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eamd", vol },
                { "Signal", volEma }
            };
        }

        return new StreamingIndicatorStateResult(vol, outputs);
    }

    public void Dispose()
    {
        _absDerMax.Dispose();
        _volMa.Dispose();
        _volSignalMa.Dispose();
    }
}

public sealed class EhlersImpulseResponseState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersImpulseResponseEngine _engine;

    public EhlersImpulseResponseState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 20, double bw = 1, InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _engine = new EhlersImpulseResponseEngine(maType, length, bw);
    }

    public EhlersImpulseResponseState(MovingAvgType maType, int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class EhlersAnticipateIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly EhlersImpulseResponseEngine _engine;
    private readonly PooledRingBuffer<double> _filters;

    public EhlersAnticipateIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length = 14, double bw = 1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _engine = new EhlersImpulseResponseEngine(maType, length, bw);
        _filters = new PooledRingBuffer<double>(_length);
    }

    public EhlersAnticipateIndicatorState(MovingAvgType maType, int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

        double maxCorr = -1;
        double start = 0;
        for (var j = 0; j < _length; j++)
        {
            double sx = 0;
            double sy = 0;
            double sxx = 0;
            double syy = 0;
            double sxy = 0;
            for (var k = 0; k < _length; k++)
            {
                var x = EhlersStreamingWindow.GetOffsetValue(_filters, hFilt, k);
                var y = -Math.Sin(MathHelper.MinOrMax(2 * Math.PI * ((double)(j + k) / _length), 0.99, 0.01));
                sx += x;
                sy += y;
                sxx += MathHelper.Pow(x, 2);
                sxy += x * y;
                syy += MathHelper.Pow(y, 2);
            }

            var denom = ((double)_length * sxx) - MathHelper.Pow(sx, 2);
            var corr = denom * (((double)_length * syy) - MathHelper.Pow(sy, 2)) > 0
                ? (((double)_length * sxy) - (sx * sy)) /
                  MathHelper.Sqrt(denom * (((double)_length * syy) - MathHelper.Pow(sy, 2)))
                : 0;
            start = corr > maxCorr ? _length - j : 0;
            maxCorr = corr > maxCorr ? corr : maxCorr;
        }

        var predict = Math.Sin(MathHelper.MinOrMax(2 * Math.PI * start / _length, 0.99, 0.01));

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

public sealed class EhlersRoofingFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha1;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevFilter1;
    private double _prevFilter2;

    public EhlersRoofingFilterV2State(int upperLength = 80, int lowerLength = 40, InputName inputName = InputName.Close)
    {
        var upper = Math.Max(1, upperLength);
        var lower = Math.Max(1, lowerLength);
        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / upper, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / lower);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / lower, 0.99));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
    }

    public EhlersRoofingFilterV2State(int upperLength, int lowerLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var upper = Math.Max(1, upperLength);
        var lower = Math.Max(1, lowerLength);
        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / upper, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / lower);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / lower, 0.99));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersRoofingFilterV2;

    public void Reset()
    {
        _values.Clear();
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevFilter1 = 0;
        _prevFilter2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var test1 = MathHelper.Pow((1 - _alpha1) / 2, 2);
        var test2 = value - (2 * prevValue1) + prevValue2;
        var highPass = (test1 * test2) + (2 * (1 - _alpha1) * _prevHp1) - (MathHelper.Pow(1 - _alpha1, 2) * _prevHp2);
        var roofingFilter = (_c1 * ((highPass + _prevHp1) / 2)) + (_c2 * _prevFilter1) + (_c3 * _prevFilter2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevHp2 = _prevHp1;
            _prevHp1 = highPass;
            _prevFilter2 = _prevFilter1;
            _prevFilter1 = roofingFilter;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Erf", roofingFilter }
            };
        }

        return new StreamingIndicatorStateResult(roofingFilter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersAutoCorrelationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly RollingWindowSum _xSum;
    private readonly RollingWindowSum _ySum;
    private readonly RollingWindowSum _xxSum;
    private readonly RollingWindowSum _yySum;
    private readonly RollingWindowSum _xySum;
    private readonly PooledRingBuffer<double> _roofingValues;

    public EhlersAutoCorrelationIndicatorState(int length1 = 48, int length2 = 10)
    {
        _length1 = Math.Max(1, length1);
        _roofingFilter = new EhlersRoofingFilterV2State(length1, length2);
        _xSum = new RollingWindowSum(_length1);
        _ySum = new RollingWindowSum(_length1);
        _xxSum = new RollingWindowSum(_length1);
        _yySum = new RollingWindowSum(_length1);
        _xySum = new RollingWindowSum(_length1);
        _roofingValues = new PooledRingBuffer<double>(_length1);
    }

    public IndicatorName Name => IndicatorName.EhlersAutoCorrelationIndicator;

    public void Reset()
    {
        _roofingFilter.Reset();
        _xSum.Reset();
        _ySum.Reset();
        _xxSum.Reset();
        _yySum.Reset();
        _xySum.Reset();
        _roofingValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var x = roofingFilter;
        var y = EhlersStreamingWindow.GetOffsetValue(_roofingValues, _length1);
        var xx = MathHelper.Pow(x, 2);
        var yy = MathHelper.Pow(y, 2);
        var xy = x * y;

        var sx = isFinal ? _xSum.Add(x, out _) : _xSum.Preview(x, out _);
        var sy = isFinal ? _ySum.Add(y, out _) : _ySum.Preview(y, out _);
        var sxx = isFinal ? _xxSum.Add(xx, out _) : _xxSum.Preview(xx, out _);
        var syy = isFinal ? _yySum.Add(yy, out _) : _yySum.Preview(yy, out _);
        var sxy = isFinal ? _xySum.Add(xy, out _) : _xySum.Preview(xy, out _);
        var count = Math.Min(_roofingValues.Count + 1, _length1);

        var corr = ((count * sxx) - (sx * sx)) * ((count * syy) - (sy * sy)) > 0
            ? 0.5 * ((((count * sxy) - (sx * sy)) /
                      MathHelper.Sqrt(((count * sxx) - (sx * sx)) * ((count * syy) - (sy * sy)))) + 1)
            : 0;

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
        _xSum.Dispose();
        _ySum.Dispose();
        _xxSum.Dispose();
        _yySum.Dispose();
        _xySum.Dispose();
        _roofingValues.Dispose();
    }
}

public sealed class EhlersAutoCorrelationPeriodogramState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _length3;
    private readonly EhlersAutoCorrelationIndicatorState _corrState;
    private readonly PooledRingBuffer<double> _corrValues;
    private readonly double[] _rArray;

    public EhlersAutoCorrelationPeriodogramState(int length1 = 48, int length2 = 10, int length3 = 3)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(0, length3);
        _corrState = new EhlersAutoCorrelationIndicatorState(_length1, _length2);
        _corrValues = new PooledRingBuffer<double>(_length1);
        _rArray = new double[_length1 + 1];
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
            var prevR = _rArray[j];
            var r = (0.2 * MathHelper.Pow(sqSum, 2)) + (0.8 * prevR);
            if (isFinal)
            {
                _rArray[j] = r;
            }
            maxPwr = Math.Max(r, maxPwr);
        }

        double spx = 0;
        double sp = 0;
        for (var j = _length2; j <= _length1; j++)
        {
            var pwr = maxPwr != 0 ? _rArray[j] / maxPwr : 0;
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
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;

        var length = (int)Math.Ceiling(domCyc / 2);
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
        var arsi = _arsiState.Update(bar, isFinal, includeOutputs: false).Value / 100;
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
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;

        double highest = 0;
        double lowest = 0;
        var length = (int)Math.Ceiling(domCyc);
        for (var j = 0; j < length; j++)
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

public sealed class EhlersAdaptiveCommodityChannelIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersAutoCorrelationPeriodogramState _periodogram;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private readonly RollingCumulativeSum _tempSum;
    private readonly RollingCumulativeSum _mdSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _prevRatio;
    private double _prevAcci1;
    private double _prevAcci2;
    private int _index;

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
        _tempSum = new RollingCumulativeSum();
        _mdSum = new RollingCumulativeSum();
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
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length2);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var cycLength = (int)Math.Ceiling(domCyc);

        var tempSum = isFinal ? _tempSum.Add(roofingFilter, cycLength) : _tempSum.Preview(roofingFilter, cycLength);
        var count = cycLength > 0 ? Math.Min(cycLength, _index + 1) : 0;
        var avg = count > 0 ? tempSum / count : 0;
        var md = MathHelper.Pow(roofingFilter - avg, 2);
        var mdSum = isFinal ? _mdSum.Add(md, cycLength) : _mdSum.Preview(md, cycLength);
        var mdAvg = count > 0 ? mdSum / count : 0;
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
            _index++;
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
        var domCyc = _periodogram.Update(bar, isFinal, includeOutputs: false).Value;
        domCyc = MathHelper.MinOrMax(domCyc, _length1, _length3);
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var beta = Math.Cos(2 * Math.PI / 0.9 * domCyc);
        var gamma = 1 / Math.Cos(2 * Math.PI * _bw / 0.9 * domCyc);
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
    private const double HilbertTransformCoeff1 = 0.0962;
    private const double HilbertTransformCoeff2 = 0.5769;
    private const double PeriodCorrectionFactor = 0.075;
    private const double PeriodCorrectionOffset = 0.54;

    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smooth;
    private readonly PooledRingBuffer<double> _det;
    private readonly PooledRingBuffer<double> _q1;
    private readonly PooledRingBuffer<double> _i1;
    private double _prevI2;
    private double _prevQ2;
    private double _prevRe;
    private double _prevIm;
    private double _prevPeriod;
    private double _prevSprd;
    private double _prevPhase;
    private double _prevMama;
    private double _prevFama;

    public EhlersMotherOfAdaptiveMovingAveragesEngine(double fastAlpha, double slowAlpha)
    {
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
        _values = new PooledRingBuffer<double>(4);
        _smooth = new PooledRingBuffer<double>(7);
        _det = new PooledRingBuffer<double>(7);
        _q1 = new PooledRingBuffer<double>(7);
        _i1 = new PooledRingBuffer<double>(7);
    }

    public EhlersMamaSnapshot Next(double value, bool isFinal)
    {
        var prevPrice1 = EhlersStreamingWindow.GetOffsetValue(_values, 1);
        var prevPrice2 = EhlersStreamingWindow.GetOffsetValue(_values, 2);
        var prevPrice3 = EhlersStreamingWindow.GetOffsetValue(_values, 3);

        var prevs2 = EhlersStreamingWindow.GetOffsetValue(_smooth, 2);
        var prevs4 = EhlersStreamingWindow.GetOffsetValue(_smooth, 4);
        var prevs6 = EhlersStreamingWindow.GetOffsetValue(_smooth, 6);

        var prevd2 = EhlersStreamingWindow.GetOffsetValue(_det, 2);
        var prevd3 = EhlersStreamingWindow.GetOffsetValue(_det, 3);
        var prevd4 = EhlersStreamingWindow.GetOffsetValue(_det, 4);
        var prevd6 = EhlersStreamingWindow.GetOffsetValue(_det, 6);

        var prevq1x2 = EhlersStreamingWindow.GetOffsetValue(_q1, 2);
        var prevq1x4 = EhlersStreamingWindow.GetOffsetValue(_q1, 4);
        var prevq1x6 = EhlersStreamingWindow.GetOffsetValue(_q1, 6);

        var previ1x2 = EhlersStreamingWindow.GetOffsetValue(_i1, 2);
        var previ1x4 = EhlersStreamingWindow.GetOffsetValue(_i1, 4);
        var previ1x6 = EhlersStreamingWindow.GetOffsetValue(_i1, 6);

        var smooth = ((4 * value) + (3 * prevPrice1) + (2 * prevPrice2) + prevPrice3) / 10;
        var det = ((HilbertTransformCoeff1 * smooth) + (HilbertTransformCoeff2 * prevs2) -
                   (HilbertTransformCoeff2 * prevs4) - (HilbertTransformCoeff1 * prevs6))
                  * ((PeriodCorrectionFactor * _prevPeriod) + PeriodCorrectionOffset);
        var q1 = ((HilbertTransformCoeff1 * det) + (HilbertTransformCoeff2 * prevd2) -
                  (HilbertTransformCoeff2 * prevd4) - (HilbertTransformCoeff1 * prevd6))
                 * ((PeriodCorrectionFactor * _prevPeriod) + PeriodCorrectionOffset);
        var i1 = prevd3;
        var j1 = ((HilbertTransformCoeff1 * i1) + (HilbertTransformCoeff2 * previ1x2) -
                  (HilbertTransformCoeff2 * previ1x4) - (HilbertTransformCoeff1 * previ1x6))
                 * ((PeriodCorrectionFactor * _prevPeriod) + PeriodCorrectionOffset);
        var jq = ((HilbertTransformCoeff1 * q1) + (HilbertTransformCoeff2 * prevq1x2) -
                  (HilbertTransformCoeff2 * prevq1x4) - (HilbertTransformCoeff1 * prevq1x6))
                 * ((PeriodCorrectionFactor * _prevPeriod) + PeriodCorrectionOffset);

        var i2 = i1 - jq;
        i2 = (0.2 * i2) + (0.8 * _prevI2);

        var q2 = q1 + j1;
        q2 = (0.2 * q2) + (0.8 * _prevQ2);

        var re = (i2 * _prevI2) + (q2 * _prevQ2);
        re = (0.2 * re) + (0.8 * _prevRe);

        var im = (i2 * _prevQ2) - (q2 * _prevI2);
        im = (0.2 * im) + (0.8 * _prevIm);

        var atan = re != 0 ? Math.Atan(im / re) : 0;
        var period = atan != 0 ? 2 * Math.PI / atan : 0;

        if (_prevPeriod != 0)
        {
            period = MathHelper.MinOrMax(period, 1.5 * _prevPeriod, 0.67 * _prevPeriod);
        }

        period = MathHelper.MinOrMax(period, 50, 6);
        period = (0.2 * period) + (0.8 * _prevPeriod);

        var sPrd = (0.33 * period) + (0.67 * _prevSprd);
        var phase = i1 != 0 ? Math.Atan(q1 / i1).ToDegrees() : 0;
        var deltaPhase = _prevPhase - phase < 1 ? 1 : _prevPhase - phase;
        var alpha = deltaPhase != 0 ? _fastAlpha / deltaPhase : 0;
        if (alpha < _slowAlpha)
        {
            alpha = _slowAlpha;
        }

        var mama = (alpha * value) + ((1 - alpha) * _prevMama);
        var fama = (0.5 * alpha * mama) + ((1 - (0.5 * alpha)) * _prevFama);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smooth.TryAdd(smooth, out _);
            _det.TryAdd(det, out _);
            _q1.TryAdd(q1, out _);
            _i1.TryAdd(i1, out _);
            _prevI2 = i2;
            _prevQ2 = q2;
            _prevRe = re;
            _prevIm = im;
            _prevPeriod = period;
            _prevSprd = sPrd;
            _prevPhase = phase;
            _prevMama = mama;
            _prevFama = fama;
        }

        return new EhlersMamaSnapshot(fama, mama, i1, q1, sPrd, smooth, re, im);
    }

    public void Reset()
    {
        _values.Clear();
        _smooth.Clear();
        _det.Clear();
        _q1.Clear();
        _i1.Clear();
        _prevI2 = 0;
        _prevQ2 = 0;
        _prevRe = 0;
        _prevIm = 0;
        _prevPeriod = 0;
        _prevSprd = 0;
        _prevPhase = 0;
        _prevMama = 0;
        _prevFama = 0;
    }

    public void Dispose()
    {
        _values.Dispose();
        _smooth.Dispose();
        _det.Dispose();
        _q1.Dispose();
        _i1.Dispose();
    }
}

internal sealed class EhlersImpulseResponseEngine : IDisposable
{
    private readonly double _l1;
    private readonly double _s1;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _values;
    private double _prevBp1;
    private double _prevBp2;
    private int _index;

    public EhlersImpulseResponseEngine(MovingAvgType maType, int length, double bw)
    {
        var resolved = Math.Max(1, length);
        var hannLength = MathHelper.MinOrMax((int)Math.Ceiling(resolved / 1.4));
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolved, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / resolved, 0.99, 0.01));
        _s1 = (1 / g1) - MathHelper.Sqrt(1 / MathHelper.Pow(g1, 2) - 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, hannLength);
        _values = new PooledRingBuffer<double>(2);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var bp = _index < 3
            ? 0
            : (0.5 * (1 - _s1) * (value - prevValue2)) + (_l1 * (1 + _s1) * _prevBp1) - (_s1 * _prevBp2);
        var filt = _smoother.Next(bp, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _index++;
        }

        return filt;
    }

    public void Reset()
    {
        _values.Clear();
        _prevBp1 = 0;
        _prevBp2 = 0;
        _index = 0;
        _smoother.Reset();
    }

    public void Dispose()
    {
        _values.Dispose();
        _smoother.Dispose();
    }
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
