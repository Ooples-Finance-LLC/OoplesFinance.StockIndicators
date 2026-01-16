using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class EhlersLaguerreFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevL0;
    private double _prevL1;
    private double _prevL2;
    private double _prevL3;
    private bool _hasPrev;

    public EhlersLaguerreFilterState(double alpha = 0.2, InputName inputName = InputName.Close)
    {
        _alpha = alpha;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(3);
    }

    public EhlersLaguerreFilterState(double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = alpha;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersLaguerreFilter;

    public void Reset()
    {
        _values.Clear();
        _prevL0 = 0;
        _prevL1 = 0;
        _prevL2 = 0;
        _prevL3 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevL0 = _hasPrev ? _prevL0 : value;
        var prevL1 = _hasPrev ? _prevL1 : value;
        var prevL2 = _hasPrev ? _prevL2 : value;
        var prevL3 = _hasPrev ? _prevL3 : value;

        var l0 = (_alpha * value) + ((1 - _alpha) * prevL0);
        var l1 = (-1 * (1 - _alpha) * l0) + prevL0 + ((1 - _alpha) * prevL1);
        var l2 = (-1 * (1 - _alpha) * l1) + prevL1 + ((1 - _alpha) * prevL2);
        var l3 = (-1 * (1 - _alpha) * l2) + prevL2 + ((1 - _alpha) * prevL3);

        var filter = (l0 + (2 * l1) + (2 * l2) + l3) / 6;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevL0 = l0;
            _prevL1 = l1;
            _prevL2 = l2;
            _prevL3 = l3;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Elf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

public void Dispose()
{
    _values.Dispose();
}
}

public sealed class EhlersReflexIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _filterValues;
    private double _prevMs;

    public EhlersReflexIndicatorState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(1);
        _filterValues = new PooledRingBuffer<double>(_length);
    }

    public EhlersReflexIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(1);
        _filterValues = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersReflexIndicator;

    public void Reset()
    {
        _values.Clear();
        _filterValues.Clear();
        _prevMs = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevFilter1 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 1);
        var prevFilter2 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 2);

        var filter = (_c1 * ((value + prevValue) / 2)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);
        var priorFilter = EhlersStreamingWindow.GetOffsetValue(_filterValues, filter, _length);
        var slope = _length != 0 ? (priorFilter - filter) / _length : 0;

        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var prevFilterCount = EhlersStreamingWindow.GetOffsetValue(_filterValues, filter, j);
            sum += filter + (j * slope) - prevFilterCount;
        }
        sum /= _length;

        var ms = (0.04 * sum * sum) + (0.96 * _prevMs);
        var reflex = ms > 0 ? sum / MathHelper.Sqrt(ms) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _filterValues.TryAdd(filter, out _);
            _prevMs = ms;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eri", reflex }
            };
        }

        return new StreamingIndicatorStateResult(reflex, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _filterValues.Dispose();
    }
}

public sealed class EhlersRelativeStrengthIndexInverseFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public EhlersRelativeStrengthIndexInverseFisherTransformState(
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14, int signalLength = 9,
        InputName inputName = InputName.Close)
    {
        _rsi = new RsiState(maType, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersRelativeStrengthIndexInverseFisherTransformState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rsi = new RsiState(maType, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform;

    public void Reset()
    {
        _rsi.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var v1 = 0.1 * (rsi - 50);
        var v2 = _signalSmoother.Next(v1, isFinal);
        var expValue = MathHelper.Exp(2 * v2);
        var iFish = expValue + 1 != 0 ? MathHelper.MinOrMax((expValue - 1) / (expValue + 1), 1, -1) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eiftrsi", iFish }
            };
        }

        return new StreamingIndicatorStateResult(iFish, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersRelativeVigorIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _rviSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public EhlersRelativeVigorIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        int signalLength = 4, InputName inputName = InputName.Close)
    {
        _rviSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersRelativeVigorIndexState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rviSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRelativeVigorIndex;

    public void Reset()
    {
        _rviSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var open = bar.Open;
        var high = bar.High;
        var low = bar.Low;
        var rvi = high - low != 0 ? (close - open) / (high - low) : 0;
        var rviMa = _rviSmoother.Next(rvi, isFinal);
        var signal = _signalSmoother.Next(rviMa, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ervi", rviMa },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rviMa, outputs);
    }

    public void Dispose()
    {
        _rviSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersRestoringPullIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersSpectrumDerivedFilterBankEngine _sdfb;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public EhlersRestoringPullIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolvedMin = Math.Max(1, minLength);
        var resolvedMax = Math.Max(maxLength, resolvedMin);
        _sdfb = new EhlersSpectrumDerivedFilterBankEngine(resolvedMin, resolvedMax, length1, length2);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedMin);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersRestoringPullIndicatorState(MovingAvgType maType, int minLength, int maxLength, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedMin = Math.Max(1, minLength);
        var resolvedMax = Math.Max(maxLength, resolvedMin);
        _sdfb = new EhlersSpectrumDerivedFilterBankEngine(resolvedMin, resolvedMax, length1, length2);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedMin);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRestoringPullIndicator;

    public void Reset()
    {
        _sdfb.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var domCyc = _sdfb.Next(value, isFinal);
        var rpi = bar.Volume * MathHelper.Pow(MathHelper.MinOrMax(2 * Math.PI / domCyc, 0.99, 0.01), 2);
        var rpiEma = _signalSmoother.Next(rpi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Rpi", rpi },
                { "Signal", rpiEma }
            };
        }

        return new StreamingIndicatorStateResult(rpi, outputs);
    }

    public void Dispose()
    {
        _sdfb.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersReverseExponentialMovingAverageIndicatorV1State : IStreamingIndicatorState
{
    private readonly ReverseEmaEngine _engine;
    private readonly StreamingInputResolver _input;

    public EhlersReverseExponentialMovingAverageIndicatorV1State(double alpha = 0.1, InputName inputName = InputName.Close)
    {
        _engine = new ReverseEmaEngine(alpha);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersReverseExponentialMovingAverageIndicatorV1State(double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new ReverseEmaEngine(alpha);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV1;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wave = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Erema", wave }
            };
        }

        return new StreamingIndicatorStateResult(wave, outputs);
    }
}

public sealed class EhlersReverseExponentialMovingAverageIndicatorV2State : IStreamingIndicatorState
{
    private readonly ReverseEmaEngine _trendEngine;
    private readonly ReverseEmaEngine _cycleEngine;
    private readonly StreamingInputResolver _input;

    public EhlersReverseExponentialMovingAverageIndicatorV2State(double trendAlpha = 0.05, double cycleAlpha = 0.3,
        InputName inputName = InputName.Close)
    {
        _trendEngine = new ReverseEmaEngine(trendAlpha);
        _cycleEngine = new ReverseEmaEngine(cycleAlpha);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersReverseExponentialMovingAverageIndicatorV2State(double trendAlpha, double cycleAlpha,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _trendEngine = new ReverseEmaEngine(trendAlpha);
        _cycleEngine = new ReverseEmaEngine(cycleAlpha);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV2;

    public void Reset()
    {
        _trendEngine.Reset();
        _cycleEngine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var trend = _trendEngine.Next(value, isFinal);
        var cycle = _cycleEngine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "EremaCycle", cycle },
                { "EremaTrend", trend }
            };
        }

        return new StreamingIndicatorStateResult(cycle, outputs);
    }
}

public sealed class EhlersRocketRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly RollingWindowSum _upSum;
    private readonly RollingWindowSum _downSum;
    private readonly double _mult;
    private double _prevMom;
    private double _prevSsf;
    private double _prevTmp;

    public EhlersRocketRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV2,
        int length1 = 10, int length2 = 8, double obosLevel = 2, double mult = 1, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _values = new PooledRingBuffer<double>(_length1);
        _upSum = new RollingWindowSum(_length1);
        _downSum = new RollingWindowSum(_length1);
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersRocketRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, double obosLevel,
        double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _values = new PooledRingBuffer<double>(_length1);
        _upSum = new RollingWindowSum(_length1);
        _downSum = new RollingWindowSum(_length1);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRocketRelativeStrengthIndex;

    public void Reset()
    {
        _values.Clear();
        _upSum.Reset();
        _downSum.Reset();
        _smoother.Reset();
        _prevMom = 0;
        _prevSsf = 0;
        _prevTmp = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var lookback = _length1 - 1;
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, lookback);
        var mom = _values.Count >= lookback ? value - prevValue : 0;
        var arg = (mom + _prevMom) / 2;
        var ssf = _smoother.Next(arg, isFinal);
        var ssfMom = ssf - _prevSsf;

        var upChg = ssfMom > 0 ? ssfMom : 0;
        var downChg = ssfMom < 0 ? Math.Abs(ssfMom) : 0;

        var upSum = isFinal ? _upSum.Add(upChg, out _) : _upSum.Preview(upChg, out _);
        var downSum = isFinal ? _downSum.Add(downChg, out _) : _downSum.Preview(downChg, out _);
        var denom = upSum + downSum;
        var tmp = denom != 0 ? MathHelper.MinOrMax((upSum - downSum) / denom, 0.999, -0.999) : _prevTmp;
        var tempLog = 1 - tmp != 0 ? (1 + tmp) / (1 - tmp) : 0;
        var logVal = Math.Log(tempLog);
        var rocketRsi = 0.5 * logVal * _mult;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevMom = mom;
            _prevSsf = ssf;
            _prevTmp = tmp;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Errsi", rocketRsi }
            };
        }

        return new StreamingIndicatorStateResult(rocketRsi, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _upSum.Dispose();
        _downSum.Dispose();
        _smoother.Dispose();
    }
}

public sealed class EhlersRoofingFilterIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _a1;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevFilter1;
    private double _prevFilter2;

    public EhlersRoofingFilterIndicatorState(int length1 = 80, int length2 = 40, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / resolved1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _a1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a2 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved2);
        var b1 = 2 * a2 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolved2, 0.99));
        _c2 = b1;
        _c3 = -a2 * a2;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
    }

    public EhlersRoofingFilterIndicatorState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var alphaArg = Math.Min(MathHelper.Sqrt2 * Math.PI / resolved1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _a1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a2 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved2);
        var b1 = 2 * a2 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolved2, 0.99));
        _c2 = b1;
        _c3 = -a2 * a2;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersRoofingFilterIndicator;

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
        var prevValue1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);

        var hp = (MathHelper.Pow(1 - (_a1 / 2), 2) * (value - (2 * prevValue1) + prevValue2)) +
            (2 * (1 - _a1) * _prevHp1) - (MathHelper.Pow(1 - _a1, 2) * _prevHp2);
        var filter = (_c1 * ((hp + _prevHp1) / 2)) + (_c2 * _prevFilter1) + (_c3 * _prevFilter2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _prevFilter2 = _prevFilter1;
            _prevFilter1 = filter;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Erfi", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersRoofingFilterV1State : IStreamingIndicatorState, IDisposable
{
    private readonly HighPassFilterV1Engine _hp;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevHp;

    public EhlersRoofingFilterV1State(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1,
        int length1 = 48, int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _hp = new HighPassFilterV1Engine(resolved1, 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersRoofingFilterV1State(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _hp = new HighPassFilterV1Engine(resolved1, 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersRoofingFilterV1;

    public void Reset()
    {
        _hp.Reset();
        _smoother.Reset();
        _prevHp = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highPass = _hp.Next(value, isFinal);
        var arg = (highPass + _prevHp) / 2;
        var roofingFilter = _smoother.Next(arg, isFinal);

        if (isFinal)
        {
            _prevHp = highPass;
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
        _smoother.Dispose();
    }
}

public sealed class EhlersSignalToNoiseRatioV1State : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersHilbertTransformIndicatorEngine _engine;
    private readonly IMovingAverageSmoother _ema;
    private readonly StreamingInputResolver _input;
    private double _prevV2;
    private double _prevRange;
    private double _prevAmp;

    public EhlersSignalToNoiseRatioV1State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 7,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _engine = new EhlersHilbertTransformIndicatorEngine(resolved, 0.635, 0.338);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersSignalToNoiseRatioV1State(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _engine = new EhlersHilbertTransformIndicatorEngine(resolved, 0.635, 0.338);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersSignalToNoiseRatioV1;

    public void Reset()
    {
        _engine.Reset();
        _ema.Reset();
        _prevV2 = 0;
        _prevRange = 0;
        _prevAmp = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _engine.Next(value, isFinal, out var inPhase, out var quad);
        var ema = _ema.Next(value, isFinal);
        var range = (0.2 * (bar.High - bar.Low)) + (0.8 * _prevRange);
        var v2 = (0.2 * ((inPhase * inPhase) + (quad * quad))) + (0.8 * _prevV2);
        var amp = range != 0
            ? (0.25 * ((10 * Math.Log(v2 / (range * range)) / Math.Log(10)) + 1.9)) + (0.75 * _prevAmp)
            : 0;

        if (isFinal)
        {
            _prevV2 = v2;
            _prevRange = range;
            _prevAmp = amp;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esnr", amp }
            };
        }

        return new StreamingIndicatorStateResult(amp, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _ema.Dispose();
    }
}

public sealed class EhlersSignalToNoiseRatioV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly StreamingInputResolver _input;
    private double _prevRange;
    private double _prevSnr;

    public EhlersSignalToNoiseRatioV2State(int length = 6, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersSignalToNoiseRatioV2State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersSignalToNoiseRatioV2;

    public void Reset()
    {
        _mama.Reset();
        _prevRange = 0;
        _prevSnr = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var snapshot = _mama.Next(value, isFinal);
        var range = (0.1 * (bar.High - bar.Low)) + (0.9 * _prevRange);
        var temp = range != 0 ? ((snapshot.I1 * snapshot.I1) + (snapshot.Q1 * snapshot.Q1)) / (range * range) : 0;
        var snr = range > 0
            ? (0.25 * ((10 * Math.Log(temp) / Math.Log(10)) + _length)) + (0.75 * _prevSnr)
            : 0;

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

public sealed class EhlersLaguerreRelativeStrengthIndexState : IStreamingIndicatorState
{
    private readonly double _gamma;
    private readonly StreamingInputResolver _input;
    private double _prevL0;
    private double _prevL1;
    private double _prevL2;
    private double _prevL3;
    private bool _hasPrev;

    public EhlersLaguerreRelativeStrengthIndexState(double gamma = 0.5, InputName inputName = InputName.Close)
    {
        _gamma = gamma;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersLaguerreRelativeStrengthIndexState(double gamma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _gamma = gamma;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersLaguerreRelativeStrengthIndex;

    public void Reset()
    {
        _prevL0 = 0;
        _prevL1 = 0;
        _prevL2 = 0;
        _prevL3 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevL0 = _hasPrev ? _prevL0 : value;
        var prevL1 = _hasPrev ? _prevL1 : value;
        var prevL2 = _hasPrev ? _prevL2 : value;
        var prevL3 = _hasPrev ? _prevL3 : value;

        var l0 = ((1 - _gamma) * value) + (_gamma * prevL0);
        var l1 = (-1 * _gamma * l0) + prevL0 + (_gamma * prevL1);
        var l2 = (-1 * _gamma * l1) + prevL1 + (_gamma * prevL2);
        var l3 = (-1 * _gamma * l2) + prevL2 + (_gamma * prevL3);

        var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
        var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
        var laguerreRsi = cu + cd != 0 ? MathHelper.MinOrMax(cu / (cu + cd), 1, 0) : 0;

        if (isFinal)
        {
            _prevL0 = l0;
            _prevL1 = l1;
            _prevL2 = l2;
            _prevL3 = l3;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Elrsi", laguerreRsi }
            };
        }

        return new StreamingIndicatorStateResult(laguerreRsi, outputs);
    }
}

public sealed class EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly RollingWindowMax _highMax;
    private readonly RollingWindowMin _lowMin;
    private readonly RollingWindowSum _ratioSum;
    private double _prevL0;
    private double _prevL1;
    private double _prevL2;
    private double _prevL3;
    private double _prevValue;
    private bool _hasPrev;

    public EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(int length = 13, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _highMax = new RollingWindowMax(_length);
        _lowMin = new RollingWindowMin(_length);
        _ratioSum = new RollingWindowSum(_length);
    }

    public EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _highMax = new RollingWindowMax(_length);
        _lowMin = new RollingWindowMin(_length);
        _ratioSum = new RollingWindowSum(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha;

    public void Reset()
    {
        _highMax.Reset();
        _lowMin.Reset();
        _ratioSum.Reset();
        _prevL0 = 0;
        _prevL1 = 0;
        _prevL2 = 0;
        _prevL3 = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var high = bar.High;
        var low = bar.Low;
        var open = bar.Open;

        var highestHigh = isFinal ? _highMax.Add(high, out _) : _highMax.Preview(high, out _);
        var lowestLow = isFinal ? _lowMin.Add(low, out _) : _lowMin.Preview(low, out _);

        var oc = (open + prevValue) / 2;
        var hc = Math.Max(high, prevValue);
        var lc = Math.Min(low, prevValue);
        var feValue = (oc + hc + lc + value) / 4;

        var ratio = highestHigh - lowestLow != 0 ? (hc - lc) / (highestHigh - lowestLow) : 0;
        var ratioSum = isFinal ? _ratioSum.Add(ratio, out _) : _ratioSum.Preview(ratio, out _);
        var alpha = ratioSum > 0
            ? MathHelper.MinOrMax(Math.Log(ratioSum) / Math.Log(_length), 0.99, 0.01)
            : 0.01;

        var l0 = (alpha * feValue) + ((1 - alpha) * _prevL0);
        var l1 = (-(1 - alpha) * l0) + _prevL0 + ((1 - alpha) * _prevL1);
        var l2 = (-(1 - alpha) * l1) + _prevL1 + ((1 - alpha) * _prevL2);
        var l3 = (-(1 - alpha) * l2) + _prevL2 + ((1 - alpha) * _prevL3);

        var cu = (l0 >= l1 ? l0 - l1 : 0) + (l1 >= l2 ? l1 - l2 : 0) + (l2 >= l3 ? l2 - l3 : 0);
        var cd = (l0 >= l1 ? 0 : l1 - l0) + (l1 >= l2 ? 0 : l2 - l1) + (l2 >= l3 ? 0 : l3 - l2);
        var laguerreRsi = cu + cd != 0 ? MathHelper.MinOrMax(cu / (cu + cd), 1, 0) : 0;

        if (isFinal)
        {
            _prevL0 = l0;
            _prevL1 = l1;
            _prevL2 = l2;
            _prevL3 = l3;
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Elrsiwsa", laguerreRsi }
            };
        }

        return new StreamingIndicatorStateResult(laguerreRsi, outputs);
    }

    public void Dispose()
    {
        _highMax.Dispose();
        _lowMin.Dispose();
        _ratioSum.Dispose();
    }
}

public sealed class EhlersLeadingIndicatorState : IStreamingIndicatorState
{
    private readonly double _alpha1;
    private readonly double _alpha2;
    private readonly StreamingInputResolver _input;
    private double _prevLead;
    private double _prevLeadIndicator;
    private double _prevValue;
    private bool _hasPrev;

    public EhlersLeadingIndicatorState(double alpha1 = 0.25, double alpha2 = 0.33, InputName inputName = InputName.Close)
    {
        _alpha1 = alpha1;
        _alpha2 = alpha2;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersLeadingIndicatorState(double alpha1, double alpha2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha1 = alpha1;
        _alpha2 = alpha2;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersLeadingIndicator;

    public void Reset()
    {
        _prevLead = 0;
        _prevLeadIndicator = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevLead = _hasPrev ? _prevLead : 0;
        var prevLeadIndicator = _hasPrev ? _prevLeadIndicator : 0;

        var lead = (2 * value) + ((_alpha1 - 2) * prevValue) + ((1 - _alpha1) * prevLead);
        var leadIndicator = (_alpha2 * lead) + ((1 - _alpha2) * prevLeadIndicator);

        if (isFinal)
        {
            _prevLead = lead;
            _prevLeadIndicator = leadIndicator;
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eli", leadIndicator }
            };
        }

        return new StreamingIndicatorStateResult(leadIndicator, outputs);
    }
}

public sealed class EhlersMarketStateIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersCorrelationAngleIndicatorState _angleState;
    private double _prevAngle;
    private double _prevState;
    private bool _hasPrev;

    public EhlersMarketStateIndicatorState(int length = 20)
    {
        _angleState = new EhlersCorrelationAngleIndicatorState(length);
    }

    public IndicatorName Name => IndicatorName.EhlersMarketStateIndicator;

    public void Reset()
    {
        _angleState.Reset();
        _prevAngle = 0;
        _prevState = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var angle = _angleState.Update(bar, isFinal, includeOutputs: false).Value;
        var prevAngle = _hasPrev ? _prevAngle : 0;
        var state = Math.Abs(angle - prevAngle) < 9 && angle < 0 ? -1 : Math.Abs(angle - prevAngle) < 9 && angle >= 0 ? 1 : 0;

        if (isFinal)
        {
            _prevAngle = angle;
            _prevState = state;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Emsi", state }
            };
        }

        return new StreamingIndicatorStateResult(state, outputs);
    }

    public void Dispose()
    {
        _angleState.Dispose();
    }
}

public sealed class EhlersMedianAverageAdaptiveFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _threshold;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smthValues;
    private readonly double[] _windowScratch;
    private readonly double[] _medianScratch;
    private double _prevValue;
    private double _prevValue2;
    private double _prevFilter;

    public EhlersMedianAverageAdaptiveFilterState(int length = 39, double threshold = 0.002, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _threshold = threshold;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(3);
        _smthValues = new PooledRingBuffer<double>(_length);
        _windowScratch = new double[_length];
        _medianScratch = new double[_length];
    }

    public EhlersMedianAverageAdaptiveFilterState(int length, double threshold, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _threshold = threshold;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(3);
        _smthValues = new PooledRingBuffer<double>(_length);
        _windowScratch = new double[_length];
        _medianScratch = new double[_length];
    }

    public IndicatorName Name => IndicatorName.EhlersMedianAverageAdaptiveFilter;

    public void Reset()
    {
        _values.Clear();
        _smthValues.Clear();
        _prevValue = 0;
        _prevValue2 = 0;
        _prevFilter = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevP1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevP2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevP3 = EhlersStreamingWindow.GetOffsetValue(_values, value, 3);

        var smth = (value + (2 * prevP1) + (2 * prevP2) + prevP3) / 6;

        var existingCount = _smthValues.Count;
        var available = existingCount < _length ? existingCount + 1 : _length;
        if (existingCount < _length)
        {
            for (var i = 0; i < existingCount; i++)
            {
                _windowScratch[i] = _smthValues[i];
            }

            _windowScratch[existingCount] = smth;
        }
        else
        {
            for (var i = 1; i < existingCount; i++)
            {
                _windowScratch[i - 1] = _smthValues[i];
            }

            _windowScratch[available - 1] = smth;
        }

        var len = _length;
        double value3 = 0.2;
        double value2 = 0;
        var prevV2 = _prevValue2;
        var removedOffset = 0;

        while (value3 > _threshold && len > 0)
        {
            var remaining = available - removedOffset;
            var count = Math.Min(len, remaining);
            var alpha = (double)2 / (len + 1);
            var median = GetMedian(_windowScratch, removedOffset, count, _medianScratch);
            value2 = (alpha * smth) + ((1 - alpha) * prevV2);
            value3 = median != 0 ? Math.Abs(median - value2) / median : value3;
            len -= 2;

            if (value3 > _threshold && len > 0 && len < available && removedOffset + 1 < available)
            {
                removedOffset = Math.Min(removedOffset + 2, available);
            }
        }

        len = len < 3 ? 3 : len;
        var finalAlpha = (double)2 / (len + 1);
        var filter = (finalAlpha * smth) + ((1 - finalAlpha) * _prevFilter);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smthValues.TryAdd(smth, out _);
            _prevValue = value;
            _prevValue2 = value2;
            _prevFilter = filter;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Maaf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _smthValues.Dispose();
    }

    private static double GetMedian(double[] values, int start, int count, double[] scratch)
    {
        if (count <= 0)
        {
            return 0;
        }

        Array.Copy(values, start, scratch, 0, count);
        Array.Sort(scratch, 0, count);
        var mid = count / 2;
        if ((count & 1) == 1)
        {
            return scratch[mid];
        }

        return (scratch[mid - 1] + scratch[mid]) / 2;
    }
}
public sealed class EhlersMesaPredictIndicatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _length3;
    private readonly int _lowerLength;
    private readonly int _upperLength;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly StreamingInputResolver _input;
    private readonly RollingWindowSum _pwrSum;
    private readonly PooledRingBuffer<double> _ssfValues;
    private readonly double[] _bb1Array;
    private readonly double[] _bb2Array;
    private readonly double[] _coefArray;
    private readonly double[] _coefAArray;
    private readonly double[] _pArray;
    private readonly double[] _coef1Array;
    private readonly double[] _hCoefArray;
    private readonly double[] _xxArray;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevSsf1;
    private double _prevSsf2;
    private double _prevPrePredict;
    private double _prevPredict;
    private int _index;

    public EhlersMesaPredictIndicatorV1State(int length1 = 5, int length2 = 4, int length3 = 10,
        int lowerLength = 12, int upperLength = 54, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _lowerLength = Math.Max(1, lowerLength);
        _upperLength = Math.Max(1, upperLength);
        _input = new StreamingInputResolver(inputName, null);

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / _upperLength, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / _upperLength, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = (1 + _c2 - _c3) / 4;

        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / _lowerLength, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / _lowerLength, 0.99, 0.01));
        _coef2 = b;
        _coef3 = -a * a;
        _coef1 = 1 - _coef2 - _coef3;

        _pwrSum = new RollingWindowSum(_upperLength);
        _ssfValues = new PooledRingBuffer<double>(_upperLength);
        _bb1Array = new double[_upperLength + 2];
        _bb2Array = new double[_upperLength + 2];
        _coefArray = new double[_length2 + 2];
        _coefAArray = new double[_length2 + 2];
        _pArray = new double[_length2 + 2];
        _coef1Array = new double[_lowerLength + 2];
        _hCoefArray = new double[_length2 + 2];
        _xxArray = new double[_upperLength + Math.Max(_length1, _length3) + 2];
    }

    public EhlersMesaPredictIndicatorV1State(int length1, int length2, int length3, int lowerLength, int upperLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _lowerLength = Math.Max(1, lowerLength);
        _upperLength = Math.Max(1, upperLength);
        _input = new StreamingInputResolver(InputName.Close, selector);

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / _upperLength, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / _upperLength, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = (1 + _c2 - _c3) / 4;

        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / _lowerLength, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / _lowerLength, 0.99, 0.01));
        _coef2 = b;
        _coef3 = -a * a;
        _coef1 = 1 - _coef2 - _coef3;

        _pwrSum = new RollingWindowSum(_upperLength);
        _ssfValues = new PooledRingBuffer<double>(_upperLength);
        _bb1Array = new double[_upperLength + 2];
        _bb2Array = new double[_upperLength + 2];
        _coefArray = new double[_length2 + 2];
        _coefAArray = new double[_length2 + 2];
        _pArray = new double[_length2 + 2];
        _coef1Array = new double[_lowerLength + 2];
        _hCoefArray = new double[_length2 + 2];
        _xxArray = new double[_upperLength + Math.Max(_length1, _length3) + 2];
    }

    public IndicatorName Name => IndicatorName.EhlersMesaPredictIndicatorV1;

    public void Reset()
    {
        _pwrSum.Reset();
        _ssfValues.Clear();
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevSsf1 = 0;
        _prevSsf2 = 0;
        _prevPrePredict = 0;
        _prevPredict = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        Array.Clear(_bb1Array, 0, _bb1Array.Length);
        Array.Clear(_bb2Array, 0, _bb2Array.Length);
        Array.Clear(_coefArray, 0, _coefArray.Length);
        Array.Clear(_coefAArray, 0, _coefAArray.Length);
        Array.Clear(_pArray, 0, _pArray.Length);
        Array.Clear(_coef1Array, 0, _coef1Array.Length);
        Array.Clear(_hCoefArray, 0, _hCoefArray.Length);
        Array.Clear(_xxArray, 0, _xxArray.Length);

        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;

        var hp = _index < 4
            ? 0
            : (_c1 * (value - (2 * prevValue1) + prevValue2)) + (_c2 * _prevHp1) + (_c3 * _prevHp2);

        var ssf = _index < 3
            ? hp
            : (_coef1 * ((hp + _prevHp1) / 2)) + (_coef2 * _prevSsf1) + (_coef3 * _prevSsf2);

        var ssfPow = ssf * ssf;
        var pwrSum = isFinal ? _pwrSum.Add(ssfPow, out _) : _pwrSum.Preview(ssfPow, out _);
        var pwr = pwrSum / _upperLength;

        var priorSsf = EhlersStreamingWindow.GetOffsetValue(_ssfValues, ssf, _upperLength - 1);
        _bb1Array[1] = ssf;
        if (_upperLength > 1)
        {
            _bb2Array[_upperLength - 1] = priorSsf;
        }

        for (var j = 2; j < _upperLength; j++)
        {
            var prevSsf = EhlersStreamingWindow.GetOffsetValue(_ssfValues, ssf, j - 1);
            _bb1Array[j] = prevSsf;
            _bb2Array[j - 1] = prevSsf;
        }

        double num = 0;
        double denom = 0;
        for (var j = 1; j < _upperLength; j++)
        {
            num += _bb1Array[j] * _bb2Array[j];
            denom += MathHelper.Pow(_bb1Array[j], 2) + MathHelper.Pow(_bb2Array[j], 2);
        }

        var coef = denom != 0 ? 2 * num / denom : 0;
        var p = pwr * (1 - MathHelper.Pow(coef, 2));
        _coefArray[1] = coef;
        _pArray[1] = p;
        for (var j = 2; j <= _length2; j++)
        {
            for (var k = 1; k < j; k++)
            {
                _coefAArray[k] = _coefArray[k];
            }

            for (var k = 1; k < _upperLength; k++)
            {
                _bb1Array[k] = _bb1Array[k] - (_coefAArray[j - 1] * _bb2Array[k]);
                _bb2Array[k] = _bb2Array[k + 1] - (_coefAArray[j - 1] * _bb1Array[k + 1]);
            }

            double num1 = 0;
            double denom1 = 0;
            for (var k = 1; k <= _upperLength - j; k++)
            {
                num1 += _bb1Array[k] * _bb2Array[k];
                denom1 += MathHelper.Pow(_bb1Array[k], 2) + MathHelper.Pow(_bb2Array[k], 2);
            }

            _coefArray[j] = denom1 != 0 ? 2 * num1 / denom1 : 0;
            _pArray[j] = _pArray[j - 1] * (1 - MathHelper.Pow(_coefArray[j], 2));
            for (var k = 1; k < j; k++)
            {
                _coefArray[k] = _coefAArray[k] - (_coefArray[j] * _coefAArray[j - k]);
            }
        }

        for (var j = 1; j <= _length2; j++)
        {
            _coef1Array[1] = _coefArray[j];
            for (var k = _lowerLength; k >= 2; k--)
            {
                _coef1Array[k] = _coef1Array[k - 1];
            }
        }

        for (var j = 1; j <= _length2; j++)
        {
            _hCoefArray[j] = 0;
            double cc = 0;
            for (var k = 1; k <= _lowerLength; k++)
            {
                var weight = 1 - Math.Cos(MathHelper.MinOrMax(2 * Math.PI * ((double)k / (_lowerLength + 1)), 0.99, 0.01));
                _hCoefArray[j] = _hCoefArray[j] + (weight * _coef1Array[k]);
                cc += weight;
            }

            _hCoefArray[j] = cc != 0 ? _hCoefArray[j] / cc : 0;
        }

        for (var j = 1; j <= _upperLength; j++)
        {
            _xxArray[j] = EhlersStreamingWindow.GetOffsetValue(_ssfValues, ssf, _upperLength - j);
        }

        for (var j = 1; j <= _length3; j++)
        {
            _xxArray[_upperLength + j] = 0;
            for (var k = 1; k <= _length2; k++)
            {
                _xxArray[_upperLength + j] = _xxArray[_upperLength + j] + (_hCoefArray[k] * _xxArray[_upperLength + j - k]);
            }
        }

        var prePredict = _xxArray[_upperLength + _length1];
        var predict = (prePredict + _prevPrePredict) / 2;

        if (isFinal)
        {
            _ssfValues.TryAdd(ssf, out _);
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _prevSsf2 = _prevSsf1;
            _prevSsf1 = ssf;
            _prevPrePredict = prePredict;
            _prevPredict = predict;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ssf", ssf },
                { "Predict", predict },
                { "PrePredict", prePredict }
            };
        }

        return new StreamingIndicatorStateResult(predict, outputs);
    }

    public void Dispose()
    {
        _pwrSum.Dispose();
        _ssfValues.Dispose();
    }
}

public sealed class EhlersMesaPredictIndicatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length4;
    private readonly double[] _coefArray;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _filtValues;
    private readonly double[] _xxArray;
    private readonly double[] _yyArray;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevSsf1;
    private double _prevSsf2;
    private double _prevPredict;
    private double _prevPredict2;
    private int _index;

    public EhlersMesaPredictIndicatorV2State(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 5, int length2 = 135, int length3 = 12, int length4 = 4, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        _length4 = Math.Max(1, length4);
        _input = new StreamingInputResolver(inputName, null);

        _coefArray = new[] { 4.525, -8.45, 8.145, -4.045, 0.825 };

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength2, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength2, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = (1 + _c2 - _c3) / 4;

        var a = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength3, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength3, 0.99, 0.01));
        _coef2 = b;
        _coef3 = -a * a;
        _coef1 = 1 - _coef2 - _coef3;

        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength3);
        _filtValues = new PooledRingBuffer<double>(_length1);
        _xxArray = new double[_length1 + _length4 + 2];
        _yyArray = new double[_length1 + _length4 + 2];
    }

    public EhlersMesaPredictIndicatorV2State(MovingAvgType maType, int length1, int length2, int length3, int length4,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        _length4 = Math.Max(1, length4);
        _input = new StreamingInputResolver(InputName.Close, selector);

        _coefArray = new[] { 4.525, -8.45, 8.145, -4.045, 0.825 };

        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength2, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength2, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = (1 + _c2 - _c3) / 4;

        var a = MathHelper.Exp(MathHelper.MinOrMax(-1.414 * Math.PI / resolvedLength3, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength3, 0.99, 0.01));
        _coef2 = b;
        _coef3 = -a * a;
        _coef1 = 1 - _coef2 - _coef3;

        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength3);
        _filtValues = new PooledRingBuffer<double>(_length1);
        _xxArray = new double[_length1 + _length4 + 2];
        _yyArray = new double[_length1 + _length4 + 2];
    }

    public IndicatorName Name => IndicatorName.EhlersMesaPredictIndicatorV2;

    public void Reset()
    {
        _smoother.Reset();
        _filtValues.Clear();
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevSsf1 = 0;
        _prevSsf2 = 0;
        _prevPredict = 0;
        _prevPredict2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        Array.Clear(_xxArray, 0, _xxArray.Length);
        Array.Clear(_yyArray, 0, _yyArray.Length);

        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;

        var hp = _index < 4
            ? 0
            : (_c1 * (value - (2 * prevValue1) + prevValue2)) + (_c2 * _prevHp1) + (_c3 * _prevHp2);

        var ssf = _index < 3
            ? hp
            : (_coef1 * ((hp + _prevHp1) / 2)) + (_coef2 * _prevSsf1) + (_coef3 * _prevSsf2);

        var filt = _smoother.Next(ssf, isFinal);

        for (var j = 1; j <= _length1; j++)
        {
            var prevFilt = EhlersStreamingWindow.GetOffsetValue(_filtValues, filt, _length1 - j);
            _xxArray[j] = prevFilt;
            _yyArray[j] = prevFilt;
        }

        for (var j = 1; j <= _length1; j++)
        {
            _xxArray[_length1 + j] = 0;
            for (var k = 1; k <= 5; k++)
            {
                _xxArray[_length1 + j] = _xxArray[_length1 + j] + (_coefArray[k - 1] * _xxArray[_length1 + j - (k - 1)]);
            }
        }

        for (var j = 0; j <= _length1; j++)
        {
            _yyArray[_length1 + j + 1] = (2 * _yyArray[_length1 + j]) - _yyArray[_length1 + j - 1];
        }

        var predict = _xxArray[_length1 + _length4];
        var extrap = _yyArray[_length1 + _length4];

        if (isFinal)
        {
            _filtValues.TryAdd(filt, out _);
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevHp2 = _prevHp1;
            _prevHp1 = hp;
            _prevSsf2 = _prevSsf1;
            _prevSsf1 = ssf;
            _prevPredict2 = _prevPredict;
            _prevPredict = predict;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ssf", filt },
                { "Predict", predict },
                { "Extrap", extrap }
            };
        }

        return new StreamingIndicatorStateResult(predict, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _filtValues.Dispose();
    }
}

public sealed class EhlersModifiedOptimumEllipticFilterState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevValue3;
    private double _prevMoef1;
    private double _prevMoef2;
    private int _index;

    public EhlersModifiedOptimumEllipticFilterState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersModifiedOptimumEllipticFilterState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersModifiedOptimumEllipticFilter;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevValue3 = 0;
        _prevMoef1 = 0;
        _prevMoef2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : value;
        var prevValue2 = _index >= 2 ? _prevValue2 : prevValue1;
        var prevValue3 = _index >= 3 ? _prevValue3 : prevValue2;
        var prevMoef1 = _index >= 1 ? _prevMoef1 : value;
        var prevMoef2 = _index >= 2 ? _prevMoef2 : prevMoef1;

        var moef = (0.13785 * ((2 * value) - prevValue1)) + (0.0007 * ((2 * prevValue1) - prevValue2)) +
            (0.13785 * ((2 * prevValue2) - prevValue3)) + (1.2103 * prevMoef1) - (0.4867 * prevMoef2);

        if (isFinal)
        {
            _prevValue3 = _prevValue2;
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevMoef2 = _prevMoef1;
            _prevMoef1 = moef;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Emoef", moef }
            };
        }

        return new StreamingIndicatorStateResult(moef, outputs);
    }
}

public sealed class EhlersModifiedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly RollingWindowSum _upChgSum;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private double _prevRoofingFilter;
    private double _prevUpChgSum;
    private double _prevDenom;
    private double _prevMrsi1;
    private double _prevMrsi2;
    private double _prevMrsiSig1;
    private double _prevMrsiSig2;
    private bool _hasPrev;

    public EhlersModifiedRelativeStrengthIndexState(int length1 = 48, int length2 = 10, int length3 = 10)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved2);
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved2, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _upChgSum = new RollingWindowSum(resolved3);
        _roofingFilter = new EhlersRoofingFilterV2State(resolved1, resolved2);
    }

    public IndicatorName Name => IndicatorName.EhlersModifiedRelativeStrengthIndex;

    public void Reset()
    {
        _upChgSum.Reset();
        _roofingFilter.Reset();
        _prevRoofingFilter = 0;
        _prevUpChgSum = 0;
        _prevDenom = 0;
        _prevMrsi1 = 0;
        _prevMrsi2 = 0;
        _prevMrsiSig1 = 0;
        _prevMrsiSig2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRoofingFilter = _hasPrev ? _prevRoofingFilter : 0;
        var upChg = roofingFilter > prevRoofingFilter ? roofingFilter - prevRoofingFilter : 0;
        var upChgSum = isFinal ? _upChgSum.Add(upChg, out _) : _upChgSum.Preview(upChg, out _);
        var dnChg = roofingFilter < prevRoofingFilter ? prevRoofingFilter - roofingFilter : 0;
        var denom = upChg + dnChg;

        var mrsi = denom != 0 && _prevDenom != 0
            ? (_c1 * (((upChgSum / denom) + (_prevUpChgSum / _prevDenom)) / 2)) + (_c2 * _prevMrsi1) + (_c3 * _prevMrsi2)
            : 0;

        var mrsiSig = (_c1 * ((mrsi + _prevMrsi1) / 2)) + (_c2 * _prevMrsiSig1) + (_c3 * _prevMrsiSig2);

        if (isFinal)
        {
            _prevRoofingFilter = roofingFilter;
            _prevUpChgSum = upChgSum;
            _prevDenom = denom;
            _prevMrsi2 = _prevMrsi1;
            _prevMrsi1 = mrsi;
            _prevMrsiSig2 = _prevMrsiSig1;
            _prevMrsiSig1 = mrsiSig;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Emrsi", mrsi },
                { "Signal", mrsiSig }
            };
        }

        return new StreamingIndicatorStateResult(mrsi, outputs);
    }

    public void Dispose()
    {
        _upChgSum.Dispose();
        _roofingFilter.Dispose();
    }
}

public sealed class EhlersModifiedStochasticIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersRoofingFilterV1State _roofingFilter;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private double _prevStoc;
    private double _prevModStoc1;
    private double _prevModStoc2;

    public EhlersModifiedStochasticIndicatorState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1,
        int length1 = 48, int length2 = 10, int length3 = 20, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved1);
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved1, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV1State(maType, resolved1, resolved2, inputName);
        _maxWindow = new RollingWindowMax(resolved3);
        _minWindow = new RollingWindowMin(resolved3);
    }

    public EhlersModifiedStochasticIndicatorState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolved1);
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved1, 0.99, 0.01));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV1State(maType, resolved1, resolved2, selector);
        _maxWindow = new RollingWindowMax(resolved3);
        _minWindow = new RollingWindowMin(resolved3);
    }

    public IndicatorName Name => IndicatorName.EhlersModifiedStochasticIndicator;

    public void Reset()
    {
        _roofingFilter.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _prevStoc = 0;
        _prevModStoc1 = 0;
        _prevModStoc2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var highest = isFinal ? _maxWindow.Add(roofingFilter, out _) : _maxWindow.Preview(roofingFilter, out _);
        var lowest = isFinal ? _minWindow.Add(roofingFilter, out _) : _minWindow.Preview(roofingFilter, out _);

        var stoc = highest - lowest != 0 ? (roofingFilter - lowest) / (highest - lowest) * 100 : 0;
        var modStoc = (_c1 * ((stoc + _prevStoc) / 2)) + (_c2 * _prevModStoc1) + (_c3 * _prevModStoc2);

        if (isFinal)
        {
            _prevStoc = stoc;
            _prevModStoc2 = _prevModStoc1;
            _prevModStoc1 = modStoc;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Emsi", modStoc }
            };
        }

        return new StreamingIndicatorStateResult(modStoc, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

public sealed class EhlersMovingAverageDifferenceIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _shortSmoother;
    private readonly IMovingAverageSmoother _longSmoother;
    private readonly StreamingInputResolver _input;

    public EhlersMovingAverageDifferenceIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 8, int slowLength = 23, InputName inputName = InputName.Close)
    {
        _shortSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _longSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersMovingAverageDifferenceIndicatorState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _shortSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _longSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersMovingAverageDifferenceIndicator;

    public void Reset()
    {
        _shortSmoother.Reset();
        _longSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var shortMa = _shortSmoother.Next(value, isFinal);
        var longMa = _longSmoother.Next(value, isFinal);
        var mad = longMa != 0 ? 100 * (shortMa - longMa) / longMa : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Emad", mad }
            };
        }

        return new StreamingIndicatorStateResult(mad, outputs);
    }

    public void Dispose()
    {
        _shortSmoother.Dispose();
        _longSmoother.Dispose();
    }
}

public sealed class EhlersNoiseEliminationTechnologyState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _denom;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _scratch;

    public EhlersNoiseEliminationTechnologyState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _denom = 0.5 * _length * (_length - 1);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
        _scratch = new double[_length + 1];
    }

    public EhlersNoiseEliminationTechnologyState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _denom = 0.5 * _length * (_length - 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
        _scratch = new double[_length + 1];
    }

    public IndicatorName Name => IndicatorName.EhlersNoiseEliminationTechnology;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
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

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Enet", net }
            };
        }

        return new StreamingIndicatorStateResult(net, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersOptimumEllipticFilterState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevOef1;
    private double _prevOef2;
    private int _index;

    public EhlersOptimumEllipticFilterState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersOptimumEllipticFilterState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersOptimumEllipticFilter;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevOef1 = 0;
        _prevOef2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevOef1 = _index >= 1 ? _prevOef1 : 0;
        var prevOef2 = _index >= 2 ? _prevOef2 : 0;

        var oef = (0.13785 * value) + (0.0007 * prevValue1) + (0.13785 * prevValue2) +
            (1.2103 * prevOef1) - (0.4867 * prevOef2);

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevOef2 = _prevOef1;
            _prevOef1 = oef;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Emoef", oef }
            };
        }

        return new StreamingIndicatorStateResult(oef, outputs);
    }
}

public sealed class EhlersPhaseAccumulationDominantCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly int _length4;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersHilbertTransformerEngine _hilbert;
    private readonly PooledRingBuffer<double> _dPhaseValues;
    private double _prevPhase;
    private double _prevInstPeriod;
    private double _prevDomCyc1;
    private double _prevDomCyc2;
    private int _index;

    public EhlersPhaseAccumulationDominantCycleState(int length1 = 48, int length2 = 20, int length3 = 10,
        int length4 = 40, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _length4 = Math.Max(1, length4);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolved2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolved2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _hilbert = new EhlersHilbertTransformerEngine(_length1, resolved2, inputName);
        _dPhaseValues = new PooledRingBuffer<double>(_length4);
    }

    public EhlersPhaseAccumulationDominantCycleState(int length1, int length2, int length3, int length4,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _length4 = Math.Max(1, length4);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolved2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolved2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _hilbert = new EhlersHilbertTransformerEngine(_length1, resolved2, selector);
        _dPhaseValues = new PooledRingBuffer<double>(_length4);
    }

    public IndicatorName Name => IndicatorName.EhlersPhaseAccumulationDominantCycle;

    public void Reset()
    {
        _hilbert.Reset();
        _dPhaseValues.Clear();
        _prevPhase = 0;
        _prevInstPeriod = 0;
        _prevDomCyc1 = 0;
        _prevDomCyc2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _hilbert.Next(bar, isFinal, out var real, out var imag);

        var prevPhase = _index >= 1 ? _prevPhase : 0;
        var phase = Math.Abs(real) > 0 ? Math.Atan(Math.Abs(imag / real)).ToDegrees() : 0;
        phase = real < 0 && imag > 0 ? 180 - phase : phase;
        phase = real < 0 && imag < 0 ? 180 + phase : phase;
        phase = real > 0 && imag < 0 ? 360 - phase : phase;

        var dPhase = prevPhase - phase;
        dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
        dPhase = MathHelper.MinOrMax(dPhase, _length1, _length3);

        var prevInstPeriod = _prevInstPeriod;
        double instPeriod = 0;
        double phaseSum = 0;
        for (var j = 0; j < _length4; j++)
        {
            var prevDPhase = EhlersStreamingWindow.GetOffsetValue(_dPhaseValues, dPhase, j);
            phaseSum += prevDPhase;
            if (phaseSum > 360 && instPeriod == 0)
            {
                instPeriod = j;
            }
        }

        instPeriod = instPeriod == 0 ? prevInstPeriod : instPeriod;
        var prevDomCyc1 = _index >= 1 ? _prevDomCyc1 : 0;
        var prevDomCyc2 = _index >= 2 ? _prevDomCyc2 : 0;
        var domCyc = (_c1 * ((instPeriod + prevInstPeriod) / 2)) + (_c2 * prevDomCyc1) + (_c3 * prevDomCyc2);

        if (isFinal)
        {
            _dPhaseValues.TryAdd(dPhase, out _);
            _prevPhase = phase;
            _prevInstPeriod = instPeriod;
            _prevDomCyc2 = _prevDomCyc1;
            _prevDomCyc1 = domCyc;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Epadc", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _hilbert.Dispose();
        _dPhaseValues.Dispose();
    }
}

public sealed class EhlersPhaseCalculationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _values;

    public EhlersPhaseCalculationState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 15,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersPhaseCalculationState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersPhaseCalculation;

    public void Reset()
    {
        _values.Clear();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double realPart = 0;
        double imagPart = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            realPart += Math.Cos(2 * Math.PI * j / _length) * weight;
            imagPart += Math.Sin(2 * Math.PI * j / _length) * weight;
        }

        var phase = Math.Abs(realPart) > 0.001 ? Math.Atan(imagPart / realPart).ToDegrees() : 90 * Math.Sign(imagPart);
        phase = realPart < 0 ? phase + 180 : phase;
        phase += 90;
        phase = phase < 0 ? phase + 360 : phase;
        phase = phase > 360 ? phase - 360 : phase;

        var phaseEma = _smoother.Next(phase, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Phase", phase },
                { "Signal", phaseEma }
            };
        }

        return new StreamingIndicatorStateResult(phase, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _smoother.Dispose();
    }
}

public sealed class EhlersRecursiveMedianFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _medianScratch;
    private double _prevRmf;

    public EhlersRecursiveMedianFilterState(int length1 = 5, int length2 = 12, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var alphaArg = MathHelper.MinOrMax(2 * Math.PI / resolved2, 0.99, 0.01);
        var alphaArgCos = Math.Cos(alphaArg);
        _alpha = alphaArgCos != 0 ? (alphaArgCos + Math.Sin(alphaArg) - 1) / alphaArgCos : 0;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(resolved1);
        _medianScratch = new double[resolved1];
    }

    public EhlersRecursiveMedianFilterState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var alphaArg = MathHelper.MinOrMax(2 * Math.PI / resolved2, 0.99, 0.01);
        var alphaArgCos = Math.Cos(alphaArg);
        _alpha = alphaArgCos != 0 ? (alphaArgCos + Math.Sin(alphaArg) - 1) / alphaArgCos : 0;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(resolved1);
        _medianScratch = new double[resolved1];
    }

    public IndicatorName Name => IndicatorName.EhlersRecursiveMedianFilter;

    public void Reset()
    {
        _values.Clear();
        _prevRmf = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var median = EhlersStreamingWindow.GetMedian(_values, value, _medianScratch);
        var rmf = (_alpha * median) + ((1 - _alpha) * _prevRmf);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevRmf = rmf;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ermf", rmf }
            };
        }

        return new StreamingIndicatorStateResult(rmf, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersRecursiveMedianOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha1;
    private readonly double _alpha2;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _medianScratch;
    private double _prevRm1;
    private double _prevRm2;
    private double _prevRmo1;
    private double _prevRmo2;

    public EhlersRecursiveMedianOscillatorState(int length1 = 5, int length2 = 12, int length3 = 30,
        InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var alpha1Arg = MathHelper.MinOrMax(2 * Math.PI / resolved2, 0.99, 0.01);
        var alpha1ArgCos = Math.Cos(alpha1Arg);
        var alpha2Arg = MathHelper.MinOrMax((1 / MathHelper.Sqrt2) * 2 * Math.PI / resolved3, 0.99, 0.01);
        var alpha2ArgCos = Math.Cos(alpha2Arg);
        _alpha1 = alpha1ArgCos != 0 ? (alpha1ArgCos + Math.Sin(alpha1Arg) - 1) / alpha1ArgCos : 0;
        _alpha2 = alpha2ArgCos != 0 ? (alpha2ArgCos + Math.Sin(alpha2Arg) - 1) / alpha2ArgCos : 0;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(resolved1);
        _medianScratch = new double[resolved1];
    }

    public EhlersRecursiveMedianOscillatorState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var alpha1Arg = MathHelper.MinOrMax(2 * Math.PI / resolved2, 0.99, 0.01);
        var alpha1ArgCos = Math.Cos(alpha1Arg);
        var alpha2Arg = MathHelper.MinOrMax((1 / MathHelper.Sqrt2) * 2 * Math.PI / resolved3, 0.99, 0.01);
        var alpha2ArgCos = Math.Cos(alpha2Arg);
        _alpha1 = alpha1ArgCos != 0 ? (alpha1ArgCos + Math.Sin(alpha1Arg) - 1) / alpha1ArgCos : 0;
        _alpha2 = alpha2ArgCos != 0 ? (alpha2ArgCos + Math.Sin(alpha2Arg) - 1) / alpha2ArgCos : 0;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(resolved1);
        _medianScratch = new double[resolved1];
    }

    public IndicatorName Name => IndicatorName.EhlersRecursiveMedianOscillator;

    public void Reset()
    {
        _values.Clear();
        _prevRm1 = 0;
        _prevRm2 = 0;
        _prevRmo1 = 0;
        _prevRmo2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var median = EhlersStreamingWindow.GetMedian(_values, value, _medianScratch);
        var rm = (_alpha1 * median) + ((1 - _alpha1) * _prevRm1);
        var rmo = (MathHelper.Pow(1 - (_alpha2 / 2), 2) * (rm - (2 * _prevRm1) + _prevRm2)) +
            (2 * (1 - _alpha2) * _prevRmo1) - (MathHelper.Pow(1 - _alpha2, 2) * _prevRmo2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevRm2 = _prevRm1;
            _prevRm1 = rm;
            _prevRmo2 = _prevRmo1;
            _prevRmo1 = rmo;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ermo", rmo }
            };
        }

        return new StreamingIndicatorStateResult(rmo, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersSimpleClipIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _derivValues;
    private readonly PooledRingBuffer<double> _clipValues;

    public EhlersSimpleClipIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 10, int length3 = 50, int signalLength = 22,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length3 = Math.Max(1, length3);
        _input = new StreamingInputResolver(inputName, null);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length1);
        _derivValues = new PooledRingBuffer<double>(_length3);
        _clipValues = new PooledRingBuffer<double>(3);
    }

    public EhlersSimpleClipIndicatorState(MovingAvgType maType, int length1, int length2, int length3,
        int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length3 = Math.Max(1, length3);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length1);
        _derivValues = new PooledRingBuffer<double>(_length3);
        _clipValues = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersSimpleClipIndicator;

    public void Reset()
    {
        _values.Clear();
        _derivValues.Clear();
        _clipValues.Clear();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var deriv = _values.Count >= _length1 ? value - prevValue : 0;

        double rms = 0;
        for (var j = 0; j < _length3; j++)
        {
            var prevDeriv = EhlersStreamingWindow.GetOffsetValue(_derivValues, deriv, j);
            rms += MathHelper.Pow(prevDeriv, 2);
        }

        var clip = rms != 0 ? MathHelper.MinOrMax(2 * deriv / MathHelper.Sqrt(rms / _length3), 1, -1) : 0;
        var prevClip1 = EhlersStreamingWindow.GetOffsetValue(_clipValues, 1);
        var prevClip2 = EhlersStreamingWindow.GetOffsetValue(_clipValues, 2);
        var prevClip3 = EhlersStreamingWindow.GetOffsetValue(_clipValues, 3);
        var z3 = clip + prevClip1 + prevClip2 + prevClip3;
        var z3Ema = _signalSmoother.Next(z3, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _derivValues.TryAdd(deriv, out _);
            _clipValues.TryAdd(clip, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Esci", z3 },
                { "Signal", z3Ema }
            };
        }

        return new StreamingIndicatorStateResult(z3, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _derivValues.Dispose();
        _clipValues.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersSimpleCycleIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smoothValues;
    private readonly PooledRingBuffer<double> _cycleValues;
    private int _index;

    public EhlersSimpleCycleIndicatorState(double alpha = 0.07, InputName inputName = InputName.Close)
    {
        _alpha = alpha;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(4);
        _smoothValues = new PooledRingBuffer<double>(3);
        _cycleValues = new PooledRingBuffer<double>(3);
    }

    public EhlersSimpleCycleIndicatorState(double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = alpha;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(4);
        _smoothValues = new PooledRingBuffer<double>(3);
        _cycleValues = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersSimpleCycleIndicator;

    public void Reset()
    {
        _values.Clear();
        _smoothValues.Clear();
        _cycleValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevValue3 = EhlersStreamingWindow.GetOffsetValue(_values, value, 3);
        var prevSmooth1 = EhlersStreamingWindow.GetOffsetValue(_smoothValues, 1);
        var prevSmooth2 = EhlersStreamingWindow.GetOffsetValue(_smoothValues, 2);
        var prevCycle1 = EhlersStreamingWindow.GetOffsetValue(_cycleValues, 1);
        var prevCycle2 = EhlersStreamingWindow.GetOffsetValue(_cycleValues, 2);

        var smooth = (value + (2 * prevValue1) + (2 * prevValue2) + prevValue3) / 6;
        var cycle_ = (MathHelper.Pow(1 - (0.5 * _alpha), 2) * (smooth - (2 * prevSmooth1) + prevSmooth2)) +
            (2 * (1 - _alpha) * prevCycle1) - (MathHelper.Pow(1 - _alpha, 2) * prevCycle2);
        var cycle = _index < 7 ? (value - (2 * prevValue1) + prevValue2) / 4 : cycle_;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smoothValues.TryAdd(smooth, out _);
            _cycleValues.TryAdd(cycle_, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esci", cycle }
            };
        }

        return new StreamingIndicatorStateResult(cycle, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _smoothValues.Dispose();
        _cycleValues.Dispose();
    }
}

public sealed class EhlersSimpleDecyclerState : IStreamingIndicatorState, IDisposable
{
    private readonly double _upperPct;
    private readonly double _lowerPct;
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV1Engine _hp;

    public EhlersSimpleDecyclerState(int length = 125, double upperPct = 0.5, double lowerPct = 0.5,
        InputName inputName = InputName.Close)
    {
        _upperPct = upperPct;
        _lowerPct = lowerPct;
        _input = new StreamingInputResolver(inputName, null);
        _hp = new HighPassFilterV1Engine(Math.Max(1, length), 1);
    }

    public EhlersSimpleDecyclerState(int length, double upperPct, double lowerPct, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _upperPct = upperPct;
        _lowerPct = lowerPct;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _hp = new HighPassFilterV1Engine(Math.Max(1, length), 1);
    }

    public IndicatorName Name => IndicatorName.EhlersSimpleDecycler;

    public void Reset()
    {
        _hp.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highPass = _hp.Next(value, isFinal);
        var decycler = value - highPass;
        var upperBand = (1 + (_upperPct / 100)) * decycler;
        var lowerBand = (1 - (_lowerPct / 100)) * decycler;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upperBand },
                { "MiddleBand", decycler },
                { "LowerBand", lowerBand }
            };
        }

        return new StreamingIndicatorStateResult(decycler, outputs);
    }

    public void Dispose()
    {
    }
}

public sealed class EhlersSimpleDerivIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _derivValues;

    public EhlersSimpleDerivIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 2,
        int signalLength = 8, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length);
        _derivValues = new PooledRingBuffer<double>(3);
    }

    public EhlersSimpleDerivIndicatorState(MovingAvgType maType, int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length);
        _derivValues = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersSimpleDerivIndicator;

    public void Reset()
    {
        _values.Clear();
        _derivValues.Clear();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var deriv = _values.Count >= _length ? value - prevValue : 0;
        var prevDeriv1 = EhlersStreamingWindow.GetOffsetValue(_derivValues, 1);
        var prevDeriv2 = EhlersStreamingWindow.GetOffsetValue(_derivValues, 2);
        var prevDeriv3 = EhlersStreamingWindow.GetOffsetValue(_derivValues, 3);
        var z3 = deriv + prevDeriv1 + prevDeriv2 + prevDeriv3;
        var z3Ema = _signalSmoother.Next(z3, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _derivValues.TryAdd(deriv, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Esdi", z3 },
                { "Signal", z3Ema }
            };
        }

        return new StreamingIndicatorStateResult(z3, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _derivValues.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersSimpleWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _firstSmoother;
    private readonly IMovingAverageSmoother _secondSmoother;
    private readonly IMovingAverageSmoother _thirdSmoother;
    private double _prevFilt;

    public EhlersSimpleWindowIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _thirdSmoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public EhlersSimpleWindowIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _thirdSmoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public IndicatorName Name => IndicatorName.EhlersSimpleWindowIndicator;

    public void Reset()
    {
        _firstSmoother.Reset();
        _secondSmoother.Reset();
        _thirdSmoother.Reset();
        _prevFilt = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var deriv = value - bar.Open;
        var filt = _firstSmoother.Next(deriv, isFinal);
        var filtMa1 = _secondSmoother.Next(filt, isFinal);
        var filtMa2 = _thirdSmoother.Next(filtMa1, isFinal);
        var roc = (_length / 2) * Math.PI * (filtMa2 - _prevFilt);

        if (isFinal)
        {
            _prevFilt = filtMa2;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Etwi", filt },
                { "Roc", roc }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _firstSmoother.Dispose();
        _secondSmoother.Dispose();
        _thirdSmoother.Dispose();
    }
}

public sealed class EhlersSineWaveIndicatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _smoothValues;

    public EhlersSineWaveIndicatorV1State(InputName inputName = InputName.Close)
    {
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(inputName, null);
        _smoothValues = new PooledRingBuffer<double>(64);
    }

    public EhlersSineWaveIndicatorV1State(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoothValues = new PooledRingBuffer<double>(64);
    }

    public IndicatorName Name => IndicatorName.EhlersSineWaveIndicatorV1;

    public void Reset()
    {
        _mama.Reset();
        _smoothValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var snapshot = _mama.Next(value, isFinal);
        var sp = snapshot.SmoothPeriod;
        var smooth = snapshot.Smooth;
        var dcPeriod = Math.Max(1, (int)Math.Ceiling(sp + 0.5));

        double realPart = 0;
        double imagPart = 0;
        for (var j = 0; j <= dcPeriod - 1; j++)
        {
            var prevSmooth = EhlersStreamingWindow.GetOffsetValue(_smoothValues, smooth, j);
            var angle = MathHelper.MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01);
            realPart += Math.Sin(angle) * prevSmooth;
            imagPart += Math.Cos(angle) * prevSmooth;
        }

        var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
        dcPhase += 90;
        dcPhase += sp != 0 ? 360 / sp : 0;
        dcPhase += imagPart < 0 ? 180 : 0;
        dcPhase -= dcPhase > 315 ? 360 : 0;

        var sine = Math.Sin(dcPhase.ToRadians());
        var leadSine = Math.Sin((dcPhase + 45).ToRadians());

        if (isFinal)
        {
            _smoothValues.TryAdd(smooth, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sine", sine },
                { "LeadSine", leadSine }
            };
        }

        return new StreamingIndicatorStateResult(sine, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _smoothValues.Dispose();
    }
}

public sealed class EhlersSineWaveIndicatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCyberCyclePeriodState _periodState;
    private readonly EhlersCyberCycleState _cycleState;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _cycleValues;

    public EhlersSineWaveIndicatorV2State(int length = 5, double alpha = 0.07, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _periodState = new AdaptiveCyberCyclePeriodState(resolved, alpha);
        _cycleState = new EhlersCyberCycleState(0.07, inputName);
        _input = new StreamingInputResolver(inputName, null);
        _cycleValues = new PooledRingBuffer<double>(64);
    }

    public EhlersSineWaveIndicatorV2State(int length, double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _periodState = new AdaptiveCyberCyclePeriodState(resolved, alpha);
        _cycleState = new EhlersCyberCycleState(0.07, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _cycleValues = new PooledRingBuffer<double>(64);
    }

    public IndicatorName Name => IndicatorName.EhlersSineWaveIndicatorV2;

    public void Reset()
    {
        _periodState.Reset();
        _cycleState.Reset();
        _cycleValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = _periodState.Next(value, isFinal);
        var cycle = _cycleState.Update(bar, isFinal, includeOutputs: false).Value;
        var dcPeriod = Math.Max(1, (int)Math.Ceiling(period));

        double realPart = 0;
        double imagPart = 0;
        for (var j = 0; j <= dcPeriod - 1; j++)
        {
            var prevCycle = EhlersStreamingWindow.GetOffsetValue(_cycleValues, cycle, j);
            var angle = MathHelper.MinOrMax(2 * Math.PI * ((double)j / dcPeriod), 0.99, 0.01);
            realPart += Math.Sin(angle) * prevCycle;
            imagPart += Math.Cos(angle) * prevCycle;
        }

        var dcPhase = Math.Abs(imagPart) > 0.001 ? Math.Atan(realPart / imagPart).ToDegrees() : 90 * Math.Sign(realPart);
        dcPhase += 90;
        dcPhase += imagPart < 0 ? 180 : 0;
        dcPhase -= dcPhase > 315 ? 360 : 0;

        var sine = Math.Sin(dcPhase.ToRadians());
        var leadSine = Math.Sin((dcPhase + 45).ToRadians());

        if (isFinal)
        {
            _cycleValues.TryAdd(cycle, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sine", sine },
                { "LeadSine", leadSine }
            };
        }

        return new StreamingIndicatorStateResult(sine, outputs);
    }

    public void Dispose()
    {
        _periodState.Dispose();
        _cycleValues.Dispose();
    }
}

public sealed class EhlersSmoothedAdaptiveMomentumIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly double _coef4;
    private readonly AdaptiveCyberCyclePeriodState _periodState;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _f3Values;

    public EhlersSmoothedAdaptiveMomentumIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 5, int length2 = 8, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-Math.PI / resolved2);
        var b1 = 2 * a1 * Math.Cos(1.738 * Math.PI / resolved2);
        var c1 = MathHelper.Pow(a1, 2);
        _coef2 = b1 + c1;
        _coef3 = -1 * (c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _periodState = new AdaptiveCyberCyclePeriodState(resolved1, 0.07);
        _input = new StreamingInputResolver(inputName, null);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _values = new PooledRingBuffer<double>(Math.Max(64, resolved1 * 2));
        _f3Values = new PooledRingBuffer<double>(3);
    }

    public EhlersSmoothedAdaptiveMomentumIndicatorState(MovingAvgType maType, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-Math.PI / resolved2);
        var b1 = 2 * a1 * Math.Cos(1.738 * Math.PI / resolved2);
        var c1 = MathHelper.Pow(a1, 2);
        _coef2 = b1 + c1;
        _coef3 = -1 * (c1 + (b1 * c1));
        _coef4 = c1 * c1;
        _coef1 = 1 - _coef2 - _coef3 - _coef4;
        _periodState = new AdaptiveCyberCyclePeriodState(resolved1, 0.07);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _values = new PooledRingBuffer<double>(Math.Max(64, resolved1 * 2));
        _f3Values = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator;

    public void Reset()
    {
        _periodState.Reset();
        _values.Clear();
        _f3Values.Clear();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = _periodState.Next(value, isFinal);
        var pr = (int)Math.Ceiling(Math.Abs(period - 1));
        var prevValue = pr == 0 ? value : EhlersStreamingWindow.GetOffsetValue(_values, value, pr);
        var v1 = _values.Count >= pr ? value - prevValue : 0;
        var prevF3_1 = EhlersStreamingWindow.GetOffsetValue(_f3Values, 1);
        var prevF3_2 = EhlersStreamingWindow.GetOffsetValue(_f3Values, 2);
        var prevF3_3 = EhlersStreamingWindow.GetOffsetValue(_f3Values, 3);
        var f3 = (_coef1 * v1) + (_coef2 * prevF3_1) + (_coef3 * prevF3_2) + (_coef4 * prevF3_3);
        var f3Ema = _signalSmoother.Next(f3, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _f3Values.TryAdd(f3, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Esam", f3 },
                { "Signal", f3Ema }
            };
        }

        return new StreamingIndicatorStateResult(f3, outputs);
    }

    public void Dispose()
    {
        _periodState.Dispose();
        _values.Dispose();
        _f3Values.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EhlersSnakeUniversalTradingFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _l1;
    private readonly double _s1;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _bpValues;
    private readonly RollingWindowSum _powerSum;
    private int _index;

    public EhlersSnakeUniversalTradingFilterState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 23, int length2 = 50, double bw = 1.4, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / 2 * _length1, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / 2 * _length1, 0.99, 0.01));
        _s1 = (1 / g1) - MathHelper.Sqrt(1 / MathHelper.Pow(g1, 2) - 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
        _bpValues = new PooledRingBuffer<double>(2);
        _powerSum = new RollingWindowSum(_length2);
    }

    public EhlersSnakeUniversalTradingFilterState(MovingAvgType maType, int length1, int length2, double bw,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / 2 * _length1, 0.99, 0.01));
        var g1 = Math.Cos(MathHelper.MinOrMax(bw * 2 * Math.PI / 2 * _length1, 0.99, 0.01));
        _s1 = (1 / g1) - MathHelper.Sqrt(1 / MathHelper.Pow(g1, 2) - 1);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
        _bpValues = new PooledRingBuffer<double>(2);
        _powerSum = new RollingWindowSum(_length2);
    }

    public IndicatorName Name => IndicatorName.EhlersSnakeUniversalTradingFilter;

    public void Reset()
    {
        _values.Clear();
        _bpValues.Clear();
        _powerSum.Reset();
        _smoother.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevBp1 = EhlersStreamingWindow.GetOffsetValue(_bpValues, 1);
        var prevBp2 = EhlersStreamingWindow.GetOffsetValue(_bpValues, 2);

        var bp = _index < 3 ? 0 : (0.5 * (1 - _s1) * (value - prevValue)) + (_l1 * (1 + _s1) * prevBp1) - (_s1 * prevBp2);
        var filt = _smoother.Next(bp, isFinal);
        var filtPow = MathHelper.Pow(filt, 2);
        var sum = isFinal ? _powerSum.Add(filtPow, out var count) : _powerSum.Preview(filtPow, out count);
        var rms = count > 0 ? MathHelper.Sqrt(sum / count) : 0;
        var negRms = -rms;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _bpValues.TryAdd(bp, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", rms },
                { "Erf", filt },
                { "LowerBand", negRms }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _bpValues.Dispose();
        _powerSum.Dispose();
        _smoother.Dispose();
    }
}

public sealed class EhlersSpearmanRankIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _priceArray;
    private readonly double[] _rankArray;

    public EhlersSpearmanRankIndicatorState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length);
        var arrayLength = Math.Max(50, _length + 1);
        _priceArray = new double[arrayLength];
        _rankArray = new double[arrayLength];
    }

    public EhlersSpearmanRankIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length);
        var arrayLength = Math.Max(50, _length + 1);
        _priceArray = new double[arrayLength];
        _rankArray = new double[arrayLength];
    }

    public IndicatorName Name => IndicatorName.EhlersSpearmanRankIndicator;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        for (var j = 1; j <= _length; j++)
        {
            _priceArray[j] = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            _rankArray[j] = j;
        }

        for (var j = 1; j <= _length; j++)
        {
            var count = _length + 1 - j;
            for (var k = 1; k <= _length - count; k++)
            {
                var array1 = _priceArray[k + 1];
                if (array1 < _priceArray[k])
                {
                    var tempPrice = _priceArray[k];
                    var tempRank = _rankArray[k];
                    _priceArray[k] = array1;
                    _rankArray[k] = _rankArray[k + 1];
                    _priceArray[k + 1] = tempPrice;
                    _rankArray[k + 1] = tempRank;
                }
            }
        }

        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            sum += MathHelper.Pow(j - _rankArray[j], 2);
        }

        var denom = _length * (MathHelper.Pow(_length, 2) - 1);
        var sri = denom != 0 ? 2 * (0.5 - (1 - (6 * sum / denom))) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esri", sri }
            };
        }

        return new StreamingIndicatorStateResult(sri, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersSpectrumDerivedFilterBankState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersSpectrumDerivedFilterBankEngine _engine;
    private readonly StreamingInputResolver _input;

    public EhlersSpectrumDerivedFilterBankState(int minLength = 8, int maxLength = 50, int length1 = 40, int length2 = 10,
        InputName inputName = InputName.Close)
    {
        _engine = new EhlersSpectrumDerivedFilterBankEngine(minLength, maxLength, length1, length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersSpectrumDerivedFilterBankState(int minLength, int maxLength, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new EhlersSpectrumDerivedFilterBankEngine(minLength, maxLength, length1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersSpectrumDerivedFilterBank;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var domCyc = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esdfb", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class EhlersSquelchIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _length3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _v1Values;
    private readonly PooledRingBuffer<double> _dPhaseValues;
    private double _prevIp;
    private double _prevQu;
    private double _prevPhase;
    private double _prevDcPeriod;

    public EhlersSquelchIndicatorState(int length1 = 6, int length2 = 20, int length3 = 40,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length1);
        _v1Values = new PooledRingBuffer<double>(Math.Max(_length1, 4));
        _dPhaseValues = new PooledRingBuffer<double>(_length3);
    }

    public EhlersSquelchIndicatorState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length1);
        _v1Values = new PooledRingBuffer<double>(Math.Max(_length1, 4));
        _dPhaseValues = new PooledRingBuffer<double>(_length3);
    }

    public IndicatorName Name => IndicatorName.EhlersSquelchIndicator;

    public void Reset()
    {
        _values.Clear();
        _v1Values.Clear();
        _dPhaseValues.Clear();
        _prevIp = 0;
        _prevQu = 0;
        _prevPhase = 0;
        _prevDcPeriod = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var v1 = _values.Count >= _length1 ? value - prevValue : 0;
        var priorV1 = EhlersStreamingWindow.GetOffsetValue(_v1Values, v1, _length1);
        var prevV12 = EhlersStreamingWindow.GetOffsetValue(_v1Values, v1, 2);
        var prevV14 = EhlersStreamingWindow.GetOffsetValue(_v1Values, v1, 4);
        var v2 = EhlersStreamingWindow.GetOffsetValue(_v1Values, v1, 3);
        var v3 = (0.75 * (v1 - priorV1)) + (0.25 * (prevV12 - prevV14));
        var ip = (0.33 * v2) + (0.67 * _prevIp);
        var qu = (0.2 * v3) + (0.8 * _prevQu);

        var phase = Math.Abs(ip + _prevIp) > 0
            ? Math.Atan(Math.Abs((qu + _prevQu) / (ip + _prevIp))).ToDegrees()
            : 0;
        phase = ip < 0 && qu > 0 ? 180 - phase : phase;
        phase = ip < 0 && qu < 0 ? 180 + phase : phase;
        phase = ip > 0 && qu < 0 ? 360 - phase : phase;

        var dPhase = _prevPhase - phase;
        dPhase = _prevPhase < 90 && phase > 270 ? 360 + _prevPhase - phase : dPhase;
        dPhase = MathHelper.MinOrMax(dPhase, 60, 1);

        double instPeriod = 0;
        double v4 = 0;
        for (var j = 0; j <= _length3; j++)
        {
            var prevDPhase = EhlersStreamingWindow.GetOffsetValue(_dPhaseValues, dPhase, j);
            v4 += prevDPhase;
            instPeriod = v4 > 360 && instPeriod == 0 ? j : instPeriod;
        }

        var dcPeriod = (0.25 * instPeriod) + (0.75 * _prevDcPeriod);
        var si = dcPeriod < _length2 ? 0 : 1;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _v1Values.TryAdd(v1, out _);
            _dPhaseValues.TryAdd(dPhase, out _);
            _prevIp = ip;
            _prevQu = qu;
            _prevPhase = phase;
            _prevDcPeriod = dcPeriod;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esi", si }
            };
        }

        return new StreamingIndicatorStateResult(si, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _v1Values.Dispose();
        _dPhaseValues.Dispose();
    }
}

public sealed class EhlersStochasticCenterOfGravityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _windowLength;
    private readonly EhlersCenterofGravityOscillatorState _cogState;
    private readonly PooledRingBuffer<double> _cgValues;
    private readonly PooledRingBuffer<double> _v1Values;
    private readonly PooledRingBuffer<double> _v2Values;

    public EhlersStochasticCenterOfGravityOscillatorState(int length = 8, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _windowLength = Math.Max(_length, 2);
        _cogState = new EhlersCenterofGravityOscillatorState(_length, inputName);
        _cgValues = new PooledRingBuffer<double>(_windowLength);
        _v1Values = new PooledRingBuffer<double>(3);
        _v2Values = new PooledRingBuffer<double>(1);
    }

    public EhlersStochasticCenterOfGravityOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _windowLength = Math.Max(_length, 2);
        _cogState = new EhlersCenterofGravityOscillatorState(_length, selector);
        _cgValues = new PooledRingBuffer<double>(_windowLength);
        _v1Values = new PooledRingBuffer<double>(3);
        _v2Values = new PooledRingBuffer<double>(1);
    }

    public IndicatorName Name => IndicatorName.EhlersStochasticCenterOfGravityOscillator;

    public void Reset()
    {
        _cogState.Reset();
        _cgValues.Clear();
        _v1Values.Clear();
        _v2Values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var cg = _cogState.Update(bar, isFinal, includeOutputs: false).Value;
        var min = cg;
        var max = cg;
        for (var i = 0; i < _cgValues.Count; i++)
        {
            var value = _cgValues[i];
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        var v1 = max - min != 0 ? (cg - min) / (max - min) : 0;
        var prevV1_1 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 1);
        var prevV1_2 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 2);
        var prevV1_3 = EhlersStreamingWindow.GetOffsetValue(_v1Values, 3);
        var v2_ = ((4 * v1) + (3 * prevV1_1) + (2 * prevV1_2) + prevV1_3) / 10;
        var v2 = 2 * (v2_ - 0.5);
        var prevV2 = EhlersStreamingWindow.GetOffsetValue(_v2Values, 1);
        var t = MathHelper.MinOrMax(0.96 * (prevV2 + 0.02), 1, 0);

        if (isFinal)
        {
            _cgValues.TryAdd(cg, out _);
            _v1Values.TryAdd(v1, out _);
            _v2Values.TryAdd(v2, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Escog", t }
            };
        }

        return new StreamingIndicatorStateResult(t, outputs);
    }

    public void Dispose()
    {
        _cogState.Dispose();
        _cgValues.Dispose();
        _v1Values.Dispose();
        _v2Values.Dispose();
    }
}

public sealed class EhlersZeroMeanRoofingFilterState : IStreamingIndicatorState
{
    private readonly double _alpha1;
    private readonly EhlersHpLpRoofingFilterState _roofingFilter;
    private double _prevRoof;
    private double _prevZmr;
    private bool _hasPrev;

    public EhlersZeroMeanRoofingFilterState(int length1 = 48, int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var alphaArg = Math.Min(2 * Math.PI / resolvedLength1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        _roofingFilter = new EhlersHpLpRoofingFilterState(resolvedLength1, resolvedLength2, inputName);
    }

    public EhlersZeroMeanRoofingFilterState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var alphaArg = Math.Min(2 * Math.PI / resolvedLength1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        _roofingFilter = new EhlersHpLpRoofingFilterState(resolvedLength1, resolvedLength2, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersZeroMeanRoofingFilter;

    public void Reset()
    {
        _roofingFilter.Reset();
        _prevRoof = 0;
        _prevZmr = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roof = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRoof = _hasPrev ? _prevRoof : 0;
        var prevZmr = _hasPrev ? _prevZmr : 0;
        var zmr = ((1 - (_alpha1 / 2)) * (roof - prevRoof)) + ((1 - _alpha1) * prevZmr);

        if (isFinal)
        {
            _prevRoof = roof;
            _prevZmr = zmr;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ezmrf", zmr }
            };
        }

        return new StreamingIndicatorStateResult(zmr, outputs);
    }
}

public sealed class EhlersTrendflexIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _filterValues;
    private double _prevValue;
    private double _prevMs;
    private bool _hasPrev;

    public EhlersTrendflexIndicatorState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _filterValues = new PooledRingBuffer<double>(_length);
    }

    public EhlersTrendflexIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        var b1 = 2 * a1 * Math.Cos(MathHelper.Sqrt2 * Math.PI / 0.5 * _length);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _filterValues = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersTrendflexIndicator;

    public void Reset()
    {
        _filterValues.Clear();
        _prevValue = 0;
        _prevMs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevFilter1 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 1);
        var prevFilter2 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 2);

        var filter = (_c1 * ((value + prevValue) / 2)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var prevFilterCount = EhlersStreamingWindow.GetOffsetValue(_filterValues, filter, j);
            sum += filter - prevFilterCount;
        }
        sum /= _length;

        var ms = (0.04 * sum * sum) + (0.96 * _prevMs);
        var trendflex = ms > 0 ? sum / MathHelper.Sqrt(ms) : 0;

        if (isFinal)
        {
            _filterValues.TryAdd(filter, out _);
            _prevValue = value;
            _prevMs = ms;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eti", trendflex }
            };
        }

        return new StreamingIndicatorStateResult(trendflex, outputs);
    }

    public void Dispose()
    {
        _filterValues.Dispose();
    }
}

public sealed class EhlersTrendExtractionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly double _beta;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _trendSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _bpValues;
    private int _index;

    public EhlersTrendExtractionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double delta = 0.1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _beta = Math.Max(Math.Cos(2 * Math.PI / _length), 0.99);
        var gamma = 1 / Math.Cos(4 * Math.PI * delta / _length);
        _alpha = Math.Max(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99);
        _trendSmoother = MovingAverageSmootherFactory.Create(maType, _length * 2);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
        _bpValues = new PooledRingBuffer<double>(2);
    }

    public EhlersTrendExtractionState(MovingAvgType maType, int length, double delta,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _beta = Math.Max(Math.Cos(2 * Math.PI / _length), 0.99);
        var gamma = 1 / Math.Cos(4 * Math.PI * delta / _length);
        _alpha = Math.Max(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99);
        _trendSmoother = MovingAverageSmootherFactory.Create(maType, _length * 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
        _bpValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersTrendExtraction;

    public void Reset()
    {
        _trendSmoother.Reset();
        _values.Clear();
        _bpValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevBp1 = EhlersStreamingWindow.GetOffsetValue(_bpValues, 1);
        var prevBp2 = EhlersStreamingWindow.GetOffsetValue(_bpValues, 2);
        var diff = _index >= 2 ? value - prevValue : 0;

        var bp = (0.5 * (1 - _alpha) * diff) + (_beta * (1 + _alpha) * prevBp1) - (_alpha * prevBp2);
        var trend = _trendSmoother.Next(bp, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _bpValues.TryAdd(bp, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Trend", trend },
                { "Bp", bp }
            };
        }

        return new StreamingIndicatorStateResult(trend, outputs);
    }

    public void Dispose()
    {
        _trendSmoother.Dispose();
        _values.Dispose();
        _bpValues.Dispose();
    }
}

public sealed class EhlersUniversalTradingFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _hannLength;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly RollingWindowSum _powerSum;

    public EhlersUniversalTradingFilterState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage,
        int length1 = 16, int length2 = 50, double mult = 2, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _hannLength = (int)Math.Ceiling(mult * resolvedLength1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_hannLength);
        _powerSum = new RollingWindowSum(resolvedLength2);
    }

    public EhlersUniversalTradingFilterState(MovingAvgType maType, int length1, int length2, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _hannLength = (int)Math.Ceiling(mult * resolvedLength1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_hannLength);
        _powerSum = new RollingWindowSum(resolvedLength2);
    }

    public IndicatorName Name => IndicatorName.EhlersUniversalTradingFilter;

    public void Reset()
    {
        _smoother.Reset();
        _values.Clear();
        _powerSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _hannLength);
        var mom = value - priorValue;
        var filt = _smoother.Next(mom, isFinal);
        var filtPow = filt * filt;
        var sum = isFinal ? _powerSum.Add(filtPow, out var count) : _powerSum.Preview(filtPow, out count);
        var rms = count > 0 ? MathHelper.Sqrt(sum / count) : 0;
        var negRms = -rms;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Eutf", filt },
                { "UpperBand", rms },
                { "LowerBand", negRms }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _values.Dispose();
        _powerSum.Dispose();
    }
}

public sealed class EhlersSuperPassbandFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _a1;
    private readonly double _a2;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _espfValues;
    private readonly RollingWindowSum _powerSum;

    public EhlersSuperPassbandFilterState(int fastLength = 40, int slowLength = 60, int length1 = 5, int length2 = 50,
        InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _a1 = MathHelper.MinOrMax((double)resolvedLength1 / resolvedFast, 0.99, 0.01);
        _a2 = MathHelper.MinOrMax((double)resolvedLength1 / resolvedSlow, 0.99, 0.01);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(1);
        _espfValues = new PooledRingBuffer<double>(2);
        _powerSum = new RollingWindowSum(resolvedLength2);
    }

    public EhlersSuperPassbandFilterState(int fastLength, int slowLength, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _a1 = MathHelper.MinOrMax((double)resolvedLength1 / resolvedFast, 0.99, 0.01);
        _a2 = MathHelper.MinOrMax((double)resolvedLength1 / resolvedSlow, 0.99, 0.01);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(1);
        _espfValues = new PooledRingBuffer<double>(2);
        _powerSum = new RollingWindowSum(resolvedLength2);
    }

    public IndicatorName Name => IndicatorName.EhlersSuperPassbandFilter;

    public void Reset()
    {
        _values.Clear();
        _espfValues.Clear();
        _powerSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevEspf1 = EhlersStreamingWindow.GetOffsetValue(_espfValues, 1);
        var prevEspf2 = EhlersStreamingWindow.GetOffsetValue(_espfValues, 2);

        var espf = ((_a1 - _a2) * value) +
                   (((_a2 * (1 - _a1)) - (_a1 * (1 - _a2))) * prevValue) +
                   ((1 - _a1 + (1 - _a2)) * prevEspf1) -
                   ((1 - _a1) * (1 - _a2) * prevEspf2);

        var espfPow = espf * espf;
        var sum = isFinal ? _powerSum.Add(espfPow, out var count) : _powerSum.Preview(espfPow, out count);
        var rms = count > 0 ? MathHelper.Sqrt(sum / count) : 0;
        var negRms = -rms;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _espfValues.TryAdd(espf, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Espf", espf },
                { "UpperBand", rms },
                { "LowerBand", negRms }
            };
        }

        return new StreamingIndicatorStateResult(espf, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _espfValues.Dispose();
        _powerSum.Dispose();
    }
}

public sealed class EhlersSuperSmootherFilterState : IStreamingIndicatorState
{
    private readonly EhlersSuperSmootherFilterEngine _engine;
    private readonly StreamingInputResolver _input;

    public EhlersSuperSmootherFilterState(int length = 10, InputName inputName = InputName.Close)
    {
        _engine = new EhlersSuperSmootherFilterEngine(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersSuperSmootherFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new EhlersSuperSmootherFilterEngine(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersSuperSmootherFilter;

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
                { "Essf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

public sealed class EhlersTriangleMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersTriangleMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public EhlersTriangleMovingAverageState(int length = 20, InputName inputName = InputName.Close)
    {
        _smoother = new EhlersTriangleMovingAverageSmoother(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersTriangleMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = new EhlersTriangleMovingAverageSmoother(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersTriangleMovingAverage;

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
                { "Etma", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class EhlersTriangleWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private double _prevFilt;

    public EhlersTriangleWindowIndicatorState(MovingAvgType maType = MovingAvgType.EhlersTriangleMovingAverage,
        int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public EhlersTriangleWindowIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public IndicatorName Name => IndicatorName.EhlersTriangleWindowIndicator;

    public void Reset()
    {
        _smoother.Reset();
        _prevFilt = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var deriv = value - bar.Open;
        var filt = _smoother.Next(deriv, isFinal);
        var roc = (_length / 2) * Math.PI * (filt - _prevFilt);

        if (isFinal)
        {
            _prevFilt = filt;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Etwi", filt },
                { "Roc", roc }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class EhlersTruncatedBandPassFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly double _l1;
    private readonly double _s1;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _scratch;

    public EhlersTruncatedBandPassFilterState(int length1 = 20, int length2 = 10, double bw = 0.1,
        InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01));
        var g1 = Math.Cos(bw * 2 * Math.PI / resolvedLength1);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length2 + 2);
        _scratch = new double[_length2 + 3];
    }

    public EhlersTruncatedBandPassFilterState(int length1, int length2, double bw,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _l1 = Math.Cos(MathHelper.MinOrMax(2 * Math.PI / resolvedLength1, 0.99, 0.01));
        var g1 = Math.Cos(bw * 2 * Math.PI / resolvedLength1);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length2 + 2);
        _scratch = new double[_length2 + 3];
    }

    public IndicatorName Name => IndicatorName.EhlersTruncatedBandPassFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        Array.Clear(_scratch, 0, _scratch.Length);
        for (var j = _length2; j > 0; j--)
        {
            var prevValue1 = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, j + 1);
            _scratch[j] = (0.5 * (1 - _s1) * (prevValue1 - prevValue2)) + (_l1 * (1 + _s1) * _scratch[j + 1]) -
                          (_s1 * _scratch[j + 2]);
        }

        var bpt = _scratch[1];

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Etbpf", bpt }
            };
        }

        return new StreamingIndicatorStateResult(bpt, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersUniversalOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private double _prevWhitenoise;
    private double _prevFilt1;
    private double _prevFilt2;
    private double _prevPk;
    private double _prevEuo;

    public EhlersUniversalOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, int signalLength = 9, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedSignal = Math.Max(1, signalLength);
        var a1 = MathHelper.Exp(-MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength, 0.99, 0.01));
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSignal);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
    }

    public EhlersUniversalOscillatorState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedSignal = Math.Max(1, signalLength);
        var a1 = MathHelper.Exp(-MathHelper.MinOrMax(1.414 * Math.PI / resolvedLength, 0.99, 0.01));
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSignal);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.EhlersUniversalOscillator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _values.Clear();
        _prevWhitenoise = 0;
        _prevFilt1 = 0;
        _prevFilt2 = 0;
        _prevPk = 0;
        _prevEuo = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var whitenoise = _values.Count >= 2 ? (value - prevValue) / 2 : 0;
        var filt = (_c1 * ((whitenoise + _prevWhitenoise) / 2)) + (_c2 * _prevFilt1) + (_c3 * _prevFilt2);
        var pk = Math.Abs(filt) > _prevPk ? Math.Abs(filt) : 0.991 * _prevPk;
        var euo = pk == 0 ? _prevEuo : filt / pk;
        var signal = _signalSmoother.Next(euo, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevWhitenoise = whitenoise;
            _prevFilt2 = _prevFilt1;
            _prevFilt1 = filt;
            _prevPk = pk;
            _prevEuo = euo;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Euo", euo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(euo, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersZeroCrossingsDominantCycleState : IStreamingIndicatorState
{
    private readonly EhlersBandPassFilterV1State _bandPass;
    private double _prevReal;
    private double _prevDc;
    private int _counter;
    private int _index;

    public EhlersZeroCrossingsDominantCycleState(int length = 20, double bw = 0.7, InputName inputName = InputName.Close)
    {
        _bandPass = new EhlersBandPassFilterV1State(length, bw, inputName);
    }

    public EhlersZeroCrossingsDominantCycleState(int length, double bw, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _bandPass = new EhlersBandPassFilterV1State(length, bw, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersZeroCrossingsDominantCycle;

    public void Reset()
    {
        _bandPass.Reset();
        _prevReal = 0;
        _prevDc = 0;
        _counter = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var real = _bandPass.Update(bar, isFinal, includeOutputs: false).Value;
        var prevReal = _index >= 1 ? _prevReal : 0;
        var prevDc = _prevDc;
        var dc = Math.Max(prevDc, 6);
        var counter = _counter + 1;

        if ((real > 0 && prevReal <= 0) || (real < 0 && prevReal >= 0))
        {
            dc = MathHelper.MinOrMax(2 * counter, 1.25 * prevDc, 0.8 * prevDc);
            counter = 0;
        }

        if (isFinal)
        {
            _prevReal = real;
            _prevDc = dc;
            _counter = counter;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ezcdc", dc }
            };
        }

        return new StreamingIndicatorStateResult(dc, outputs);
    }
}

public sealed class EhlersStochasticCyberCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _windowLength;
    private readonly EhlersCyberCycleState _cycleState;
    private readonly PooledRingBuffer<double> _cycleValues;
    private readonly PooledRingBuffer<double> _stochValues;
    private double _prevStochCc;

    public EhlersStochasticCyberCycleState(int length = 14, double alpha = 0.7, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _windowLength = Math.Max(_length, 2);
        _cycleState = new EhlersCyberCycleState(alpha, inputName);
        _cycleValues = new PooledRingBuffer<double>(_windowLength);
        _stochValues = new PooledRingBuffer<double>(3);
    }

    public EhlersStochasticCyberCycleState(int length, double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _windowLength = Math.Max(_length, 2);
        _cycleState = new EhlersCyberCycleState(alpha, selector);
        _cycleValues = new PooledRingBuffer<double>(_windowLength);
        _stochValues = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.EhlersStochasticCyberCycle;

    public void Reset()
    {
        _cycleState.Reset();
        _cycleValues.Clear();
        _stochValues.Clear();
        _prevStochCc = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var cycle = _cycleState.Update(bar, isFinal, includeOutputs: false).Value;
        var min = cycle;
        var max = cycle;
        for (var i = 0; i < _cycleValues.Count; i++)
        {
            var value = _cycleValues[i];
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        var stoch = max - min != 0 ? MathHelper.MinOrMax((cycle - min) / (max - min), 1, 0) : 0;
        var prevStoch1 = EhlersStreamingWindow.GetOffsetValue(_stochValues, 1);
        var prevStoch2 = EhlersStreamingWindow.GetOffsetValue(_stochValues, 2);
        var prevStoch3 = EhlersStreamingWindow.GetOffsetValue(_stochValues, 3);
        var stochCc = MathHelper.MinOrMax(
            2 * ((((4 * stoch) + (3 * prevStoch1) + (2 * prevStoch2) + prevStoch3) / 10) - 0.5), 1, -1);
        var trigger = MathHelper.MinOrMax(0.96 * (_prevStochCc + 0.02), 1, -1);

        if (isFinal)
        {
            _cycleValues.TryAdd(cycle, out _);
            _stochValues.TryAdd(stoch, out _);
            _prevStochCc = stochCc;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Escc", stochCc },
                { "Signal", trigger }
            };
        }

        return new StreamingIndicatorStateResult(stochCc, outputs);
    }

    public void Dispose()
    {
        _cycleValues.Dispose();
        _stochValues.Dispose();
    }
}

public sealed class EhlersStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly int _windowLength;
    private readonly EhlersRoofingFilterV1State _roofingFilter;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _rfValues;
    private double _prevStoch;

    public EhlersStochasticState(MovingAvgType maType = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1,
        int length1 = 48, int length2 = 20, int length3 = 10, InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _windowLength = Math.Max(_length2, 2);
        _roofingFilter = new EhlersRoofingFilterV1State(maType, Math.Max(1, length1), Math.Max(1, length3), inputName);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
        _rfValues = new PooledRingBuffer<double>(_windowLength);
    }

    public EhlersStochasticState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _windowLength = Math.Max(_length2, 2);
        _roofingFilter = new EhlersRoofingFilterV1State(maType, Math.Max(1, length1), Math.Max(1, length3), selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
        _rfValues = new PooledRingBuffer<double>(_windowLength);
    }

    public IndicatorName Name => IndicatorName.EhlersStochastic;

    public void Reset()
    {
        _roofingFilter.Reset();
        _signalSmoother.Reset();
        _rfValues.Clear();
        _prevStoch = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var rf = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var min = rf;
        var max = rf;
        for (var i = 0; i < _rfValues.Count; i++)
        {
            var value = _rfValues[i];
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        var stoch = max - min != 0 ? MathHelper.MinOrMax((rf - min) / (max - min), 1, 0) : 0;
        var arg = (stoch + _prevStoch) / 2;
        var estoch = _signalSmoother.Next(arg, isFinal);

        if (isFinal)
        {
            _rfValues.TryAdd(rf, out _);
            _prevStoch = stoch;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Es", estoch }
            };
        }

        return new StreamingIndicatorStateResult(estoch, outputs);
    }

    public void Dispose()
    {
        _roofingFilter.Dispose();
        _signalSmoother.Dispose();
        _rfValues.Dispose();
    }
}

public sealed class EhlersTripleDelayLineDetrenderState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _tmp1Values;
    private readonly PooledRingBuffer<double> _tmp2Values;

    public EhlersTripleDelayLineDetrenderState(MovingAvgType maType = MovingAvgType.EhlersModifiedOptimumEllipticFilter,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _tmp1Values = new PooledRingBuffer<double>(6);
        _tmp2Values = new PooledRingBuffer<double>(12);
    }

    public EhlersTripleDelayLineDetrenderState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _tmp1Values = new PooledRingBuffer<double>(6);
        _tmp2Values = new PooledRingBuffer<double>(12);
    }

    public IndicatorName Name => IndicatorName.EhlersTripleDelayLineDetrender;

    public void Reset()
    {
        _smoother.Reset();
        _signalSmoother.Reset();
        _tmp1Values.Clear();
        _tmp2Values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevTmp1_6 = EhlersStreamingWindow.GetOffsetValue(_tmp1Values, 6);
        var prevTmp2_6 = EhlersStreamingWindow.GetOffsetValue(_tmp2Values, 6);
        var prevTmp2_12 = EhlersStreamingWindow.GetOffsetValue(_tmp2Values, 12);

        var tmp1 = value + (0.088 * prevTmp1_6);
        var tmp2 = tmp1 - prevTmp1_6 + (1.2 * prevTmp2_6) - (0.7 * prevTmp2_12);
        var detrender = prevTmp2_12 - (2 * prevTmp2_6) + tmp2;

        var tdld = _smoother.Next(detrender, isFinal);
        var tdldSignal = _signalSmoother.Next(tdld, isFinal);

        if (isFinal)
        {
            _tmp1Values.TryAdd(tmp1, out _);
            _tmp2Values.TryAdd(tmp2, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Etdld", tdld },
                { "Signal", tdldSignal }
            };
        }

        return new StreamingIndicatorStateResult(tdld, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _signalSmoother.Dispose();
        _tmp1Values.Dispose();
        _tmp2Values.Dispose();
    }
}

public sealed class EhlersVariableIndexDynamicAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _shortSmoother;
    private readonly IMovingAverageSmoother _longSmoother;
    private readonly RollingWindowSum _shortPowSum;
    private readonly RollingWindowSum _longPowSum;
    private readonly StreamingInputResolver _input;
    private double _prevVidya;
    private bool _hasPrev;

    public EhlersVariableIndexDynamicAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 9, int slowLength = 30, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _shortSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _longSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _shortPowSum = new RollingWindowSum(resolvedFast);
        _longPowSum = new RollingWindowSum(resolvedSlow);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersVariableIndexDynamicAverageState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _shortSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _longSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _shortPowSum = new RollingWindowSum(resolvedFast);
        _longPowSum = new RollingWindowSum(resolvedSlow);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersVariableIndexDynamicAverage;

    public void Reset()
    {
        _shortSmoother.Reset();
        _longSmoother.Reset();
        _shortPowSum.Reset();
        _longPowSum.Reset();
        _prevVidya = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var shortAvg = _shortSmoother.Next(value, isFinal);
        var longAvg = _longSmoother.Next(value, isFinal);

        var shortPow = MathHelper.Pow(value - shortAvg, 2);
        var shortSum = isFinal ? _shortPowSum.Add(shortPow, out var shortCount) : _shortPowSum.Preview(shortPow, out shortCount);
        var shortMa = shortCount > 0 ? shortSum / shortCount : 0;
        var shortRms = shortMa > 0 ? MathHelper.Sqrt(shortMa) : 0;

        var longPow = MathHelper.Pow(value - longAvg, 2);
        var longSum = isFinal ? _longPowSum.Add(longPow, out var longCount) : _longPowSum.Preview(longPow, out longCount);
        var longMa = longCount > 0 ? longSum / longCount : 0;
        var longRms = longMa > 0 ? MathHelper.Sqrt(longMa) : 0;
        var kk = longRms != 0 ? MathHelper.MinOrMax(0.2 * shortRms / longRms, 0.99, 0.001) : 0;

        var prevVidya = _hasPrev ? _prevVidya : 0;
        var vidya = (kk * value) + ((1 - kk) * prevVidya);

        if (isFinal)
        {
            _prevVidya = vidya;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Evidya", vidya }
            };
        }

        return new StreamingIndicatorStateResult(vidya, outputs);
    }

    public void Dispose()
    {
        _shortSmoother.Dispose();
        _longSmoother.Dispose();
        _shortPowSum.Dispose();
        _longPowSum.Dispose();
    }
}

public sealed class EhlersZeroLagExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _lag;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _values;

    public EhlersZeroLagExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _lag = MathHelper.MinOrMax((int)Math.Floor((double)(resolved - 1) / 2));
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _values = new PooledRingBuffer<double>(_lag);
    }

    public EhlersZeroLagExponentialMovingAverageState(MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _lag = MathHelper.MinOrMax((int)Math.Floor((double)(resolved - 1) / 2));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _values = new PooledRingBuffer<double>(_lag);
    }

    public IndicatorName Name => IndicatorName.EhlersZeroLagExponentialMovingAverage;

    public void Reset()
    {
        _smoother.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _lag);
        var d = value + (_values.Count >= _lag ? value - prevValue : 0);
        var zema = _smoother.Next(d, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ezlema", zema }
            };
        }

        return new StreamingIndicatorStateResult(zema, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersVossPredictiveFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _order;
    private readonly double _f1;
    private readonly double _s1;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _filterValues;
    private readonly PooledRingBuffer<double> _vossValues;
    private int _index;

    public EhlersVossPredictiveFilterState(int length = 20, double predict = 3, double bw = 0.25,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _order = MathHelper.MinOrMax((int)Math.Ceiling(3 * predict));
        _f1 = Math.Cos(2 * Math.PI / resolved);
        var g1 = Math.Cos(bw * 2 * Math.PI / resolved);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(2);
        _filterValues = new PooledRingBuffer<double>(2);
        _vossValues = new PooledRingBuffer<double>(_order);
    }

    public EhlersVossPredictiveFilterState(int length, double predict, double bw,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _order = MathHelper.MinOrMax((int)Math.Ceiling(3 * predict));
        _f1 = Math.Cos(2 * Math.PI / resolved);
        var g1 = Math.Cos(bw * 2 * Math.PI / resolved);
        _s1 = (1 / g1) - MathHelper.Sqrt((1 / (g1 * g1)) - 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(2);
        _filterValues = new PooledRingBuffer<double>(2);
        _vossValues = new PooledRingBuffer<double>(_order);
    }

    public IndicatorName Name => IndicatorName.EhlersVossPredictiveFilter;

    public void Reset()
    {
        _values.Clear();
        _filterValues.Clear();
        _vossValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevFilt1 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 1);
        var prevFilt2 = EhlersStreamingWindow.GetOffsetValue(_filterValues, 2);
        var filt = _index <= 5
            ? 0
            : (0.5 * (1 - _s1) * (value - prevValue)) + (_f1 * (1 + _s1) * prevFilt1) - (_s1 * prevFilt2);

        double sumC = 0;
        for (var j = 0; j <= _order - 1; j++)
        {
            var prevVoss = EhlersStreamingWindow.GetOffsetValue(_vossValues, _order - j);
            sumC += (double)(j + 1) / _order * prevVoss;
        }

        var voss = (((double)(3 + _order) / 2) * filt) - sumC;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _filterValues.TryAdd(filt, out _);
            _vossValues.TryAdd(voss, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Voss", voss },
                { "Filt", filt }
            };
        }

        return new StreamingIndicatorStateResult(voss, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _filterValues.Dispose();
        _vossValues.Dispose();
    }
}

public sealed class EhlersSwissArmyKnifeIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _twoPiPrd;
    private readonly double _deltaPrd;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevEma1;
    private double _prevEma2;
    private double _prevSma1;
    private double _prevSma2;
    private double _prevGauss1;
    private double _prevGauss2;
    private double _prevButter1;
    private double _prevButter2;
    private double _prevSmooth1;
    private double _prevSmooth2;
    private double _prevHp1;
    private double _prevHp2;
    private double _prevPhp1;
    private double _prevPhp2;
    private double _prevBp1;
    private double _prevBp2;
    private double _prevBs1;
    private double _prevBs2;
    private int _index;

    public EhlersSwissArmyKnifeIndicatorState(int length = 20, double delta = 0.1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _twoPiPrd = MathHelper.MinOrMax(2 * Math.PI / _length, 0.99, 0.01);
        _deltaPrd = MathHelper.MinOrMax(2 * Math.PI * 2 * delta / _length, 0.99, 0.01);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length + 2);
    }

    public EhlersSwissArmyKnifeIndicatorState(int length, double delta, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _twoPiPrd = MathHelper.MinOrMax(2 * Math.PI / _length, 0.99, 0.01);
        _deltaPrd = MathHelper.MinOrMax(2 * Math.PI * 2 * delta / _length, 0.99, 0.01);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length + 2);
    }

    public IndicatorName Name => IndicatorName.EhlersSwissArmyKnifeIndicator;

    public void Reset()
    {
        _values.Clear();
        _prevEma1 = 0;
        _prevEma2 = 0;
        _prevSma1 = 0;
        _prevSma2 = 0;
        _prevGauss1 = 0;
        _prevGauss2 = 0;
        _prevButter1 = 0;
        _prevButter2 = 0;
        _prevSmooth1 = 0;
        _prevSmooth2 = 0;
        _prevHp1 = 0;
        _prevHp2 = 0;
        _prevPhp1 = 0;
        _prevPhp2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _prevBs1 = 0;
        _prevBs2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevPrice1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prevPrice2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prevPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);

        var alpha = (Math.Cos(_twoPiPrd) + Math.Sin(_twoPiPrd) - 1) / Math.Cos(_twoPiPrd);
        var c0 = 1d;
        var c1 = 0d;
        var b0 = alpha;
        var b1 = 0d;
        var b2 = 0d;
        var a1 = 1 - alpha;
        var a2 = 0d;
        var emaFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevEma1) +
              (a2 * _prevEma2) - (c1 * prevPrice);

        var n = _length;
        c0 = 1d;
        c1 = (double)1 / n;
        b0 = (double)1 / n;
        b1 = 0d;
        b2 = 0d;
        a1 = 1d;
        a2 = 0d;
        var smaFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevSma1) +
              (a2 * _prevSma2) - (c1 * prevPrice);

        var beta = 2.415 * (1 - Math.Cos(_twoPiPrd));
        var sqrtData = MathHelper.Pow(beta, 2) + (2 * beta);
        var sqrt = sqrtData >= 0 ? MathHelper.Sqrt(sqrtData) : 0;
        alpha = (-1 * beta) + sqrt;
        c0 = MathHelper.Pow(alpha, 2);
        c1 = 0d;
        b0 = 1d;
        b1 = 0d;
        b2 = 0d;
        a1 = 2 * (1 - alpha);
        a2 = -(1 - alpha) * (1 - alpha);
        var gaussFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevGauss1) +
              (a2 * _prevGauss2) - (c1 * prevPrice);

        beta = 2.415 * (1 - Math.Cos(_twoPiPrd));
        sqrtData = (beta * beta) + (2 * beta);
        sqrt = sqrtData >= 0 ? MathHelper.Sqrt(sqrtData) : 0;
        alpha = (-1 * beta) + sqrt;
        c0 = MathHelper.Pow(alpha, 2) / 4;
        c1 = 0d;
        b0 = 1d;
        b1 = 2d;
        b2 = 1d;
        a1 = 2 * (1 - alpha);
        a2 = -(1 - alpha) * (1 - alpha);
        var butterFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevButter1) +
              (a2 * _prevButter2) - (c1 * prevPrice);

        c0 = 0.25;
        c1 = 0d;
        b0 = 1d;
        b1 = 2d;
        b2 = 1d;
        a1 = 0d;
        a2 = 0d;
        var smoothFilter = (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevSmooth1) +
                           (a2 * _prevSmooth2) - (c1 * prevPrice);

        alpha = (Math.Cos(_twoPiPrd) + Math.Sin(_twoPiPrd) - 1) / Math.Cos(_twoPiPrd);
        c0 = 1 - (alpha / 2);
        c1 = 0d;
        b0 = 1d;
        b1 = -1d;
        b2 = 0d;
        a1 = 1 - alpha;
        a2 = 0d;
        var hpFilter = _index <= _length ? 0
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevHp1) +
              (a2 * _prevHp2) - (c1 * prevPrice);

        beta = 2.415 * (1 - Math.Cos(_twoPiPrd));
        sqrtData = MathHelper.Pow(beta, 2) + (2 * beta);
        sqrt = sqrtData >= 0 ? MathHelper.Sqrt(sqrtData) : 0;
        alpha = (-1 * beta) + sqrt;
        c0 = (1 - (alpha / 2)) * (1 - (alpha / 2));
        c1 = 0d;
        b0 = 1d;
        b1 = -2d;
        b2 = 1d;
        a1 = 2 * (1 - alpha);
        a2 = -(1 - alpha) * (1 - alpha);
        var php2Filter = _index <= _length ? 0
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevPhp1) +
              (a2 * _prevPhp2) - (c1 * prevPrice);

        beta = Math.Cos(_twoPiPrd);
        var gamma = 1 / Math.Cos(_deltaPrd);
        sqrtData = MathHelper.Pow(gamma, 2) - 1;
        sqrt = MathHelper.Sqrt(sqrtData);
        alpha = gamma - sqrt;
        c0 = (1 - alpha) / 2;
        c1 = 0d;
        b0 = 1d;
        b1 = 0d;
        b2 = -1d;
        a1 = beta * (1 + alpha);
        a2 = alpha * -1;
        var bpFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevBp1) +
              (a2 * _prevBp2) - (c1 * prevPrice);

        beta = Math.Cos(_twoPiPrd);
        gamma = 1 / Math.Cos(_deltaPrd);
        sqrtData = MathHelper.Pow(gamma, 2) - 1;
        sqrt = sqrtData >= 0 ? MathHelper.Sqrt(sqrtData) : 0;
        alpha = gamma - sqrt;
        c0 = (1 + alpha) / 2;
        c1 = 0d;
        b0 = 1d;
        b1 = -2 * beta;
        b2 = 1d;
        a1 = beta * (1 + alpha);
        a2 = alpha * -1;
        var bsFilter = _index <= _length ? value
            : (c0 * ((b0 * value) + (b1 * prevPrice1) + (b2 * prevPrice2))) + (a1 * _prevBs1) +
              (a2 * _prevBs2) - (c1 * prevPrice);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevEma2 = _prevEma1;
            _prevEma1 = emaFilter;
            _prevSma2 = _prevSma1;
            _prevSma1 = smaFilter;
            _prevGauss2 = _prevGauss1;
            _prevGauss1 = gaussFilter;
            _prevButter2 = _prevButter1;
            _prevButter1 = butterFilter;
            _prevSmooth2 = _prevSmooth1;
            _prevSmooth1 = smoothFilter;
            _prevHp2 = _prevHp1;
            _prevHp1 = hpFilter;
            _prevPhp2 = _prevPhp1;
            _prevPhp1 = php2Filter;
            _prevBp2 = _prevBp1;
            _prevBp1 = bpFilter;
            _prevBs2 = _prevBs1;
            _prevBs1 = bsFilter;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(9)
            {
                { "EmaFilter", emaFilter },
                { "SmaFilter", smaFilter },
                { "GaussFilter", gaussFilter },
                { "ButterFilter", butterFilter },
                { "SmoothFilter", smoothFilter },
                { "HpFilter", hpFilter },
                { "PhpFilter", php2Filter },
                { "BpFilter", bpFilter },
                { "BsFilter", bsFilter }
            };
        }

        return new StreamingIndicatorStateResult(smaFilter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

internal sealed class ReverseEmaEngine
{
    private readonly double _alpha;
    private readonly double _cc;
    private readonly double _cc2;
    private readonly double _cc4;
    private readonly double _cc8;
    private readonly double _cc16;
    private readonly double _cc32;
    private readonly double _cc64;
    private readonly double _cc128;
    private double _ema;
    private double _re1;
    private double _re2;
    private double _re3;
    private double _re4;
    private double _re5;
    private double _re6;
    private double _re7;

    public ReverseEmaEngine(double alpha)
    {
        _alpha = alpha;
        _cc = 1 - alpha;
        _cc2 = MathHelper.Pow(_cc, 2);
        _cc4 = MathHelper.Pow(_cc, 4);
        _cc8 = MathHelper.Pow(_cc, 8);
        _cc16 = MathHelper.Pow(_cc, 16);
        _cc32 = MathHelper.Pow(_cc, 32);
        _cc64 = MathHelper.Pow(_cc, 64);
        _cc128 = MathHelper.Pow(_cc, 128);
    }

    public double Next(double value, bool isFinal)
    {
        var prevEma = _ema;
        var prevRe1 = _re1;
        var prevRe2 = _re2;
        var prevRe3 = _re3;
        var prevRe4 = _re4;
        var prevRe5 = _re5;
        var prevRe6 = _re6;
        var prevRe7 = _re7;

        var ema = (_alpha * value) + (_cc * prevEma);
        var re1 = (_cc * ema) + prevEma;
        var re2 = (_cc2 * re1) + prevRe1;
        var re3 = (_cc4 * re2) + prevRe2;
        var re4 = (_cc8 * re3) + prevRe3;
        var re5 = (_cc16 * re4) + prevRe4;
        var re6 = (_cc32 * re5) + prevRe5;
        var re7 = (_cc64 * re6) + prevRe6;
        var re8 = (_cc128 * re7) + prevRe7;
        var wave = ema - (_alpha * re8);

        if (isFinal)
        {
            _ema = ema;
            _re1 = re1;
            _re2 = re2;
            _re3 = re3;
            _re4 = re4;
            _re5 = re5;
            _re6 = re6;
            _re7 = re7;
        }

        return wave;
    }

    public void Reset()
    {
        _ema = 0;
        _re1 = 0;
        _re2 = 0;
        _re3 = 0;
        _re4 = 0;
        _re5 = 0;
        _re6 = 0;
        _re7 = 0;
    }
}

internal sealed class Ehlers2PoleSuperSmootherFilterV1Smoother : IMovingAverageSmoother
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public Ehlers2PoleSuperSmootherFilterV1Smoother(int length)
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
        var filt = _index < 3 ? value : (_c1 * value) + (_c2 * _prevFilter1) + (_c3 * _prevFilter2);

        if (isFinal)
        {
            _prevFilter2 = _prevFilter1;
            _prevFilter1 = filt;
            _index++;
        }

        return filt;
    }

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _index = 0;
    }

    public void Dispose()
    {
    }
}

internal sealed class EhlersTriangleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly double _coefSum;
    private readonly double[] _weights;
    private readonly PooledRingBuffer<double> _values;

    public EhlersTriangleMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _weights = new double[_length];
        var halfLength = (double)_length / 2;
        double coefSum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var weight = j < halfLength ? j : j > halfLength ? _length + 1 - j : halfLength;
            _weights[j - 1] = weight;
            coefSum += weight;
        }

        _coefSum = coefSum;
    }

    public double Next(double value, bool isFinal)
    {
        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            sum += _weights[j - 1] * prevValue;
        }

        var filt = _coefSum != 0 ? sum / _coefSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        return filt;
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

internal sealed class EhlersModifiedOptimumEllipticFilterSmoother : IMovingAverageSmoother
{
    private double _prevValue1;
    private double _prevValue2;
    private double _prevValue3;
    private double _prevMoef1;
    private double _prevMoef2;
    private int _count;

    public EhlersModifiedOptimumEllipticFilterSmoother(int length)
    {
        _ = length;
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue1 = _count >= 1 ? _prevValue1 : value;
        var prevValue2 = _count >= 2 ? _prevValue2 : prevValue1;
        var prevValue3 = _count >= 3 ? _prevValue3 : prevValue2;
        var prevMoef1 = _count >= 1 ? _prevMoef1 : value;
        var prevMoef2 = _count >= 2 ? _prevMoef2 : prevMoef1;

        var moef = (0.13785 * ((2 * value) - prevValue1))
            + (0.0007 * ((2 * prevValue1) - prevValue2))
            + (0.13785 * ((2 * prevValue2) - prevValue3))
            + (1.2103 * prevMoef1)
            - (0.4867 * prevMoef2);

        if (isFinal)
        {
            _prevValue3 = _prevValue2;
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevMoef2 = _prevMoef1;
            _prevMoef1 = moef;
            _count++;
        }

        return moef;
    }

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevValue3 = 0;
        _prevMoef1 = 0;
        _prevMoef2 = 0;
        _count = 0;
    }

    public void Dispose()
    {
    }
}
