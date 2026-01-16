using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class EhlersEmpiricalModeDecompositionState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _fraction;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _trendSmoother;
    private readonly IMovingAverageSmoother _peakSmoother;
    private readonly IMovingAverageSmoother _valleySmoother;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevBp1;
    private double _prevBp2;
    private double _prevPeak;
    private double _prevValley;
    private int _index;

    public EhlersEmpiricalModeDecompositionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 50, double delta = 0.5, double fraction = 0.1,
        InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _fraction = fraction;
        _input = new StreamingInputResolver(inputName, null);
        _trendSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1 * 2);
        _peakSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
        _valleySmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);

        _beta = Math.Max(Math.Cos(2 * Math.PI / resolvedLength1), 0.99);
        var gamma = 1 / Math.Cos(4 * Math.PI * delta / resolvedLength1);
        _alpha = Math.Max(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99);
    }

    public EhlersEmpiricalModeDecompositionState(MovingAvgType maType, int length1, int length2, double delta, double fraction,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _fraction = fraction;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _trendSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1 * 2);
        _peakSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
        _valleySmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);

        _beta = Math.Max(Math.Cos(2 * Math.PI / resolvedLength1), 0.99);
        var gamma = 1 / Math.Cos(4 * Math.PI * delta / resolvedLength1);
        _alpha = Math.Max(gamma - MathHelper.Sqrt((gamma * gamma) - 1), 0.99);
    }

    public IndicatorName Name => IndicatorName.EhlersEmpiricalModeDecomposition;

    public void Reset()
    {
        _trendSmoother.Reset();
        _peakSmoother.Reset();
        _valleySmoother.Reset();
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevBp1 = 0;
        _prevBp2 = 0;
        _prevPeak = 0;
        _prevValley = 0;
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
        var trend = _trendSmoother.Next(bp, isFinal);

        var peak = prevBp1 > bp && prevBp1 > prevBp2 ? prevBp1 : _prevPeak;
        var valley = prevBp1 < bp && prevBp1 < prevBp2 ? prevBp1 : _prevValley;

        var peakAvg = _peakSmoother.Next(peak, isFinal);
        var valleyAvg = _valleySmoother.Next(valley, isFinal);
        var peakAvgFrac = _fraction * peakAvg;
        var valleyAvgFrac = _fraction * valleyAvg;

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevBp2 = _prevBp1;
            _prevBp1 = bp;
            _prevPeak = peak;
            _prevValley = valley;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Trend", trend },
                { "Peak", peakAvgFrac },
                { "Valley", valleyAvgFrac }
            };
        }

        return new StreamingIndicatorStateResult(trend, outputs);
    }

    public void Dispose()
    {
        _trendSmoother.Dispose();
        _peakSmoother.Dispose();
        _valleySmoother.Dispose();
    }
}

public sealed class EhlersHammingWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private double _prevFilt;
    private bool _hasPrev;

    public EhlersHammingWindowIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHammingMovingAverage,
        int length = 20, double pedestal = 10, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _ = pedestal;
    }

    public EhlersHammingWindowIndicatorState(MovingAvgType maType, int length, double pedestal,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _ = pedestal;
    }

    public IndicatorName Name => IndicatorName.EhlersHammingWindowIndicator;

    public void Reset()
    {
        _smoother.Reset();
        _prevFilt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var deriv = value - bar.Open;
        var filt = _smoother.Next(deriv, isFinal);
        var prevFilt = _hasPrev ? _prevFilt : 0;
        var roc = _length / 2.0 * Math.PI * (filt - prevFilt);

        if (isFinal)
        {
            _prevFilt = filt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ehwi", filt },
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

public sealed class EhlersHannMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersHannMovingAverageState(int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(MovingAvgType.EhlersHannMovingAverage, resolved);
    }

    public EhlersHannMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class EhlersHannWindowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _smoother;
    private double _prevFilt;
    private bool _hasPrev;

    public EhlersHannWindowIndicatorState(MovingAvgType maType = MovingAvgType.EhlersHannMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public EhlersHannWindowIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
    }

    public IndicatorName Name => IndicatorName.EhlersHannWindowIndicator;

    public void Reset()
    {
        _smoother.Reset();
        _prevFilt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var deriv = value - bar.Open;
        var filt = _smoother.Next(deriv, isFinal);
        var prevFilt = _hasPrev ? _prevFilt : 0;
        var roc = _length / 2.0 * Math.PI * (filt - prevFilt);

        if (isFinal)
        {
            _prevFilt = filt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ehwi", filt },
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

public sealed class EhlersHighPassFilterV1State : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV1Engine _engine;

    public EhlersHighPassFilterV1State(int length = 125, double mult = 1, InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _engine = new HighPassFilterV1Engine(Math.Max(1, length), mult);
    }

    public EhlersHighPassFilterV1State(int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
        _engine = new HighPassFilterV1Engine(Math.Max(1, length), mult);
    }

    public IndicatorName Name => IndicatorName.EhlersHighPassFilterV1;

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
                { "Hp", hp }
            };
        }

        return new StreamingIndicatorStateResult(hp, outputs);
    }
}

public sealed class EhlersHighPassFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly HighPassFilterV2Engine _engine;

    public EhlersHighPassFilterV2State(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 220,
        InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _engine = new HighPassFilterV2Engine(maType, Math.Max(1, length));
    }

    public EhlersHighPassFilterV2State(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
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
public sealed class EhlersHilbertOscillatorState : IStreamingIndicatorState, IDisposable
{
    private const int MaxSmoothPeriod = 52;
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _smoothValues;
    private readonly PooledRingBuffer<double> _q3Values;

    public EhlersHilbertOscillatorState(int length = 7, InputName inputName = InputName.Close)
    {
        _ = length;
        _input = new StreamingInputResolver(inputName, null);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _smoothValues = new PooledRingBuffer<double>(2);
        _q3Values = new PooledRingBuffer<double>(MaxSmoothPeriod);
    }

    public EhlersHilbertOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = length;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _smoothValues = new PooledRingBuffer<double>(2);
        _q3Values = new PooledRingBuffer<double>(MaxSmoothPeriod);
    }

    public IndicatorName Name => IndicatorName.EhlersHilbertOscillator;

    public void Reset()
    {
        _mama.Reset();
        _smoothValues.Clear();
        _q3Values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mama = _mama.Next(value, isFinal);
        var smooth = mama.Smooth;
        var smoothPeriod = mama.SmoothPeriod;
        var prevSmooth2 = EhlersStreamingWindow.GetOffsetValue(_smoothValues, smooth, 2);

        var q3 = 0.5 * (smooth - prevSmooth2) * ((0.1759 * smoothPeriod) + 0.4607);

        var sp = (int)Math.Ceiling(smoothPeriod / 2);
        double i3 = 0;
        for (var j = 0; j <= sp - 1; j++)
        {
            var prevQ3 = EhlersStreamingWindow.GetOffsetValue(_q3Values, q3, j);
            i3 += prevQ3;
        }
        i3 = sp != 0 ? 1.57 * i3 / sp : i3;

        var maxCount = (int)Math.Ceiling(smoothPeriod / 4);
        double iq = 0;
        for (var j = 0; j <= maxCount - 1; j++)
        {
            var prevQ3 = EhlersStreamingWindow.GetOffsetValue(_q3Values, q3, j);
            iq += prevQ3;
        }
        iq = maxCount != 0 ? 1.25 * iq / maxCount : iq;

        if (isFinal)
        {
            _smoothValues.TryAdd(smooth, out _);
            _q3Values.TryAdd(q3, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "I3", i3 },
                { "IQ", iq }
            };
        }

        return new StreamingIndicatorStateResult(iq, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _smoothValues.Dispose();
        _q3Values.Dispose();
    }
}

public sealed class EhlersHilbertTransformIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly EhlersHilbertTransformIndicatorEngine _engine;

    public EhlersHilbertTransformIndicatorState(int length = 7, double iMult = 0.635, double qMult = 0.338,
        InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _engine = new EhlersHilbertTransformIndicatorEngine(length, iMult, qMult);
    }

    public EhlersHilbertTransformIndicatorState(int length, double iMult, double qMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
        _engine = new EhlersHilbertTransformIndicatorEngine(length, iMult, qMult);
    }

    public IndicatorName Name => IndicatorName.EhlersHilbertTransformIndicator;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _engine.Next(value, isFinal, out var inPhase, out var quad);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Quad", quad },
                { "Inphase", inPhase }
            };
        }

        return new StreamingIndicatorStateResult(quad, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class EhlersHilbertTransformerState : IStreamingIndicatorState, IDisposable
{
    private readonly EhlersHilbertTransformerEngine _engine;

    public EhlersHilbertTransformerState(int length1 = 48, int length2 = 20, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _engine = new EhlersHilbertTransformerEngine(resolvedLength1, resolvedLength2, inputName);
    }

    public EhlersHilbertTransformerState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _engine = new EhlersHilbertTransformerEngine(resolvedLength1, resolvedLength2, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersHilbertTransformer;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _engine.Next(bar, isFinal, out var real, out var imag);

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
        _engine.Dispose();
    }
}

public sealed class EhlersHilbertTransformerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersRoofingFilterV2State _roofingFilter;
    private double _prevPeak;
    private double _prevReal;
    private double _prevQPeak;
    private double _prevQFilt;
    private double _prevImag1;
    private double _prevImag2;

    public EhlersHilbertTransformerIndicatorState(int length1 = 48, int length2 = 20, int length3 = 10,
        InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength3);
        var b2 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength3);
        _c2 = b2;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV2State(resolvedLength1, resolvedLength2, inputName);
    }

    public EhlersHilbertTransformerIndicatorState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength3);
        var b2 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength3);
        _c2 = b2;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _roofingFilter = new EhlersRoofingFilterV2State(resolvedLength1, resolvedLength2, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersHilbertTransformerIndicator;

    public void Reset()
    {
        _roofingFilter.Reset();
        _prevPeak = 0;
        _prevReal = 0;
        _prevQPeak = 0;
        _prevQFilt = 0;
        _prevImag1 = 0;
        _prevImag2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roofingFilter = _roofingFilter.Update(bar, isFinal, includeOutputs: false).Value;
        var peak = Math.Max(0.991 * _prevPeak, Math.Abs(roofingFilter));
        var real = peak != 0 ? roofingFilter / peak : 0;
        var qFilt = real - _prevReal;
        var qPeak = Math.Max(0.991 * _prevQPeak, Math.Abs(qFilt));
        var normalizedQ = qPeak != 0 ? qFilt / qPeak : 0;
        var imag = (_c1 * ((normalizedQ + _prevQFilt) / 2)) + (_c2 * _prevImag1) + (_c3 * _prevImag2);

        if (isFinal)
        {
            _prevPeak = peak;
            _prevReal = real;
            _prevQPeak = qPeak;
            _prevQFilt = normalizedQ;
            _prevImag2 = _prevImag1;
            _prevImag1 = imag;
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
    }
}

public sealed class EhlersHomodyneDominantCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length3;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly EhlersHilbertTransformerEngine _engine;
    private double _prevReal1;
    private double _prevReal2;
    private double _prevImag1;
    private double _prevPeriod;
    private double _prevDomCyc1;
    private double _prevDomCyc2;

    public EhlersHomodyneDominantCycleState(int length1 = 48, int length2 = 20, int length3 = 10,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length3 = Math.Max(1, length3);
        var resolvedLength2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _engine = new EhlersHilbertTransformerEngine(_length1, resolvedLength2, inputName);
    }

    public EhlersHomodyneDominantCycleState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length3 = Math.Max(1, length3);
        var resolvedLength2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-1.414 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / resolvedLength2);
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _engine = new EhlersHilbertTransformerEngine(_length1, resolvedLength2, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersHomodyneDominantCycle;

    public void Reset()
    {
        _engine.Reset();
        _prevReal1 = 0;
        _prevReal2 = 0;
        _prevImag1 = 0;
        _prevPeriod = 0;
        _prevDomCyc1 = 0;
        _prevDomCyc2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _engine.Next(bar, isFinal, out var real, out var imag);

        var re = (real * _prevReal1) + (imag * _prevImag1);
        var im = (_prevReal1 * imag) - (real * _prevImag1);

        var period = im != 0 && re != 0 ? 2 * Math.PI / Math.Abs(im / re) : 0;
        period = MathHelper.MinOrMax(period, _length1, _length3);

        var domCyc = (_c1 * ((period + _prevPeriod) / 2)) + (_c2 * _prevDomCyc1) + (_c3 * _prevDomCyc2);

        if (isFinal)
        {
            _prevReal2 = _prevReal1;
            _prevReal1 = real;
            _prevImag1 = imag;
            _prevPeriod = period;
            _prevDomCyc2 = _prevDomCyc1;
            _prevDomCyc1 = domCyc;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ehdc", domCyc }
            };
        }

        return new StreamingIndicatorStateResult(domCyc, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
public sealed class EhlersHpLpRoofingFilterState : IStreamingIndicatorState
{
    private readonly double _alpha1;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevHp1;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public EhlersHpLpRoofingFilterState(int length1 = 48, int length2 = 10, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var alphaArg = Math.Min(2 * Math.PI / resolvedLength1, 0.99);
        var alphaCos = Math.Cos(alphaArg);
        _alpha1 = alphaCos != 0 ? (alphaCos + Math.Sin(alphaArg) - 1) / alphaCos : 0;
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolvedLength2, 0.99));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersHpLpRoofingFilterState(int length1, int length2, Func<OhlcvBar, double> selector)
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
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolvedLength2, 0.99));
        _c2 = b1;
        _c3 = -1 * a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersHpLpRoofingFilter;

    public void Reset()
    {
        _prevValue = 0;
        _prevHp1 = 0;
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= 1 ? _prevValue : 0;
        var prevHp1 = _index >= 1 ? _prevHp1 : 0;
        var prevFilter1 = _index >= 1 ? _prevFilter1 : 0;
        var prevFilter2 = _index >= 2 ? _prevFilter2 : 0;

        var diff = _index >= 1 ? value - prevValue : 0;
        var hp = ((1 - (_alpha1 / 2)) * diff) + ((1 - _alpha1) * prevHp1);
        var filter = (_c1 * ((hp + prevHp1) / 2)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        if (isFinal)
        {
            _prevValue = value;
            _prevHp1 = hp;
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filter;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ehplprf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }
}

public sealed class EhlersHurstCoefficientState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _halfLength;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly RollingWindowMax _maxWindow1;
    private readonly RollingWindowMin _minWindow1;
    private readonly RollingWindowMax _maxWindow2;
    private readonly RollingWindowMin _minWindow2;
    private readonly PooledRingBuffer<double> _values;
    private double _prevDimen;
    private double _prevHurst;
    private double _prevSmoothHurst1;
    private double _prevSmoothHurst2;
    private int _index;

    public EhlersHurstCoefficientState(int length1 = 30, int length2 = 20, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _halfLength = (int)Math.Ceiling((double)_length1 / 2);
        var resolvedLength2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolvedLength2, 0.99));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
        _maxWindow1 = new RollingWindowMax(Math.Max(_length1, 2));
        _minWindow1 = new RollingWindowMin(Math.Max(_length1, 2));
        _maxWindow2 = new RollingWindowMax(Math.Max(_halfLength, 2));
        _minWindow2 = new RollingWindowMin(Math.Max(_halfLength, 2));
        _values = new PooledRingBuffer<double>(_length1);
    }

    public EhlersHurstCoefficientState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _halfLength = (int)Math.Ceiling((double)_length1 / 2);
        var resolvedLength2 = Math.Max(1, length2);
        var a1 = MathHelper.Exp(-MathHelper.Sqrt2 * Math.PI / resolvedLength2);
        var b1 = 2 * a1 * Math.Cos(Math.Min(MathHelper.Sqrt2 * Math.PI / resolvedLength2, 0.99));
        _c2 = b1;
        _c3 = -a1 * a1;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _maxWindow1 = new RollingWindowMax(Math.Max(_length1, 2));
        _minWindow1 = new RollingWindowMin(Math.Max(_length1, 2));
        _maxWindow2 = new RollingWindowMax(Math.Max(_halfLength, 2));
        _minWindow2 = new RollingWindowMin(Math.Max(_halfLength, 2));
        _values = new PooledRingBuffer<double>(_length1);
    }

    public IndicatorName Name => IndicatorName.EhlersHurstCoefficient;

    public void Reset()
    {
        _maxWindow1.Reset();
        _minWindow1.Reset();
        _maxWindow2.Reset();
        _minWindow2.Reset();
        _values.Clear();
        _prevDimen = 0;
        _prevHurst = 0;
        _prevSmoothHurst1 = 0;
        _prevSmoothHurst2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int countAfter;
        var hh3 = isFinal ? _maxWindow1.Add(value, out countAfter) : _maxWindow1.Preview(value, out countAfter);
        var ll3 = isFinal ? _minWindow1.Add(value, out countAfter) : _minWindow1.Preview(value, out countAfter);
        var hh1 = isFinal ? _maxWindow2.Add(value, out countAfter) : _maxWindow2.Preview(value, out countAfter);
        var ll1 = isFinal ? _minWindow2.Add(value, out countAfter) : _minWindow2.Preview(value, out countAfter);

        var n3 = (hh3 - ll3) / _length1;
        var n1 = (hh1 - ll1) / _halfLength;
        var priorValue = _index >= _halfLength ? EhlersStreamingWindow.GetOffsetValue(_values, value, _halfLength) : value;
        var hh2 = _index >= _halfLength ? priorValue : value;
        var ll2 = _index >= _halfLength ? priorValue : value;

        for (var j = _halfLength; j < _length1; j++)
        {
            var price = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            hh2 = price > hh2 ? price : hh2;
            ll2 = price < ll2 ? price : ll2;
        }

        var n2 = (hh2 - ll2) / _halfLength;
        var dimen = 0.5 * (((Math.Log(n1 + n2) - Math.Log(n3)) / Math.Log(2)) + _prevDimen);
        var hurst = 2 - dimen;
        var smoothHurst = (_c1 * ((hurst + _prevHurst) / 2)) + (_c2 * _prevSmoothHurst1) + (_c3 * _prevSmoothHurst2);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevDimen = dimen;
            _prevHurst = hurst;
            _prevSmoothHurst2 = _prevSmoothHurst1;
            _prevSmoothHurst1 = smoothHurst;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ehc", smoothHurst }
            };
        }

        return new StreamingIndicatorStateResult(smoothHurst, outputs);
    }

    public void Dispose()
    {
        _maxWindow1.Dispose();
        _minWindow1.Dispose();
        _maxWindow2.Dispose();
        _minWindow2.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersImpulseReactionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevReaction1;
    private double _prevReaction2;
    private double _prevIReact1;
    private double _prevIReact2;
    private int _index;

    public EhlersImpulseReactionState(int length1 = 2, int length2 = 20, double qq = 0.9, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_length1);
        _c2 = 2 * qq * Math.Cos(2 * Math.PI / resolvedLength2);
        _c3 = -qq * qq;
        _c1 = (1 + _c3) / 2;
    }

    public EhlersImpulseReactionState(int length1, int length2, double qq, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_length1);
        _c2 = 2 * qq * Math.Cos(2 * Math.PI / resolvedLength2);
        _c3 = -qq * qq;
        _c1 = (1 + _c3) / 2;
    }

    public IndicatorName Name => IndicatorName.EhlersImpulseReaction;

    public void Reset()
    {
        _values.Clear();
        _prevReaction1 = 0;
        _prevReaction2 = 0;
        _prevIReact1 = 0;
        _prevIReact2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var prevReaction1 = _index >= 1 ? _prevReaction1 : 0;
        var prevReaction2 = _index >= 2 ? _prevReaction2 : 0;
        var reaction = (_c1 * (value - priorValue)) + (_c2 * prevReaction1) + (_c3 * prevReaction2);
        var ireact = value != 0 ? 100 * reaction / value : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevReaction2 = prevReaction1;
            _prevReaction1 = reaction;
            _prevIReact2 = _prevIReact1;
            _prevIReact1 = ireact;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eir", ireact }
            };
        }

        return new StreamingIndicatorStateResult(ireact, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersInfiniteImpulseResponseFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly int _lag;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevFilter;
    private int _index;

    public EhlersInfiniteImpulseResponseFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _alpha = 2.0 / (resolved + 1);
        _lag = MathHelper.MinOrMax((int)Math.Ceiling((1 / _alpha) - 1));
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(_lag);
    }

    public EhlersInfiniteImpulseResponseFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _alpha = 2.0 / (resolved + 1);
        _lag = MathHelper.MinOrMax((int)Math.Ceiling((1 / _alpha) - 1));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(_lag);
    }

    public IndicatorName Name => IndicatorName.EhlersInfiniteImpulseResponseFilter;

    public void Reset()
    {
        _values.Clear();
        _prevFilter = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _lag);
        var diff = _index >= _lag ? value - prevValue : 0;
        var prevFilter = _index >= 1 ? _prevFilter : 0;
        var filter = (_alpha * (value + diff)) + ((1 - _alpha) * prevFilter);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevFilter = filter;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eiirf", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EhlersInstantaneousPhaseIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly StreamingInputResolver _input;
    private readonly EhlersHilbertTransformIndicatorEngine _engine;
    private readonly PooledRingBuffer<double> _dPhaseValues;
    private double _prevPhase;
    private double _prevDcPeriod;
    private double _prevIp;
    private double _prevQu;
    private int _index;

    public EhlersInstantaneousPhaseIndicatorState(int length1 = 7, int length2 = 50, InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(inputName, null);
        _engine = new EhlersHilbertTransformIndicatorEngine(Math.Max(1, length1), 0.635, 0.338);
        _dPhaseValues = new PooledRingBuffer<double>(_length2 + 1);
    }

    public EhlersInstantaneousPhaseIndicatorState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _engine = new EhlersHilbertTransformIndicatorEngine(Math.Max(1, length1), 0.635, 0.338);
        _dPhaseValues = new PooledRingBuffer<double>(_length2 + 1);
    }

    public IndicatorName Name => IndicatorName.EhlersInstantaneousPhaseIndicator;

    public void Reset()
    {
        _engine.Reset();
        _dPhaseValues.Clear();
        _prevPhase = 0;
        _prevDcPeriod = 0;
        _prevIp = 0;
        _prevQu = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _engine.Next(value, isFinal, out var inPhase, out var quad);

        var prevIp = _index >= 1 ? _prevIp : 0;
        var prevQu = _index >= 1 ? _prevQu : 0;
        var phase = Math.Abs(inPhase + prevIp) > 0
            ? Math.Atan(Math.Abs((quad + prevQu) / (inPhase + prevIp))).ToDegrees()
            : 0;
        phase = inPhase < 0 && quad > 0 ? 180 - phase : phase;
        phase = inPhase < 0 && quad < 0 ? 180 + phase : phase;
        phase = inPhase > 0 && quad < 0 ? 360 - phase : phase;

        var prevPhase = _index >= 1 ? _prevPhase : 0;
        var dPhase = prevPhase - phase;
        dPhase = prevPhase < 90 && phase > 270 ? 360 + prevPhase - phase : dPhase;
        dPhase = MathHelper.MinOrMax(dPhase, 60, 1);

        double instPeriod = 0;
        double v4 = 0;
        for (var j = 0; j <= _length2; j++)
        {
            var prevDPhase = EhlersStreamingWindow.GetOffsetValue(_dPhaseValues, dPhase, j);
            v4 += prevDPhase;
            if (v4 > 360 && instPeriod == 0)
            {
                instPeriod = j;
            }
        }

        var prevDcPeriod = _index >= 1 ? _prevDcPeriod : 0;
        var dcPeriod = (0.25 * instPeriod) + (0.75 * prevDcPeriod);

        if (isFinal)
        {
            _dPhaseValues.TryAdd(dPhase, out _);
            _prevPhase = phase;
            _prevDcPeriod = dcPeriod;
            _prevIp = inPhase;
            _prevQu = quad;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eipi", dcPeriod }
            };
        }

        return new StreamingIndicatorStateResult(dcPeriod, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _dPhaseValues.Dispose();
    }
}
public sealed class EhlersInstantaneousTrendlineV1State : IStreamingIndicatorState, IDisposable
{
    private const int MaxTrendPeriod = 52;
    private readonly StreamingInputResolver _input;
    private readonly EhlersMotherOfAdaptiveMovingAveragesEngine _mama;
    private readonly PooledRingBuffer<double> _values;
    private double _prevIt1;
    private double _prevIt2;
    private double _prevIt3;

    public EhlersInstantaneousTrendlineV1State(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _values = new PooledRingBuffer<double>(MaxTrendPeriod);
    }

    public EhlersInstantaneousTrendlineV1State(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
        _mama = new EhlersMotherOfAdaptiveMovingAveragesEngine(0.5, 0.05);
        _values = new PooledRingBuffer<double>(MaxTrendPeriod);
    }

    public IndicatorName Name => IndicatorName.EhlersInstantaneousTrendlineV1;

    public void Reset()
    {
        _mama.Reset();
        _values.Clear();
        _prevIt1 = 0;
        _prevIt2 = 0;
        _prevIt3 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mama = _mama.Next(value, isFinal);
        var dcPeriod = (int)Math.Ceiling(mama.SmoothPeriod + 0.5);
        double iTrend = 0;
        for (var j = 0; j <= dcPeriod - 1; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            iTrend += prevValue;
        }
        iTrend = dcPeriod != 0 ? iTrend / dcPeriod : iTrend;

        var trendLine = ((4 * iTrend) + (3 * _prevIt1) + (2 * _prevIt2) + _prevIt3) / 10;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevIt3 = _prevIt2;
            _prevIt2 = _prevIt1;
            _prevIt1 = iTrend;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eit", iTrend },
                { "Signal", trendLine }
            };
        }

        return new StreamingIndicatorStateResult(iTrend, outputs);
    }

    public void Dispose()
    {
        _mama.Dispose();
        _values.Dispose();
    }
}

public sealed class EhlersInstantaneousTrendlineV2State : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevIt1;
    private double _prevIt2;
    private int _index;

    public EhlersInstantaneousTrendlineV2State(double alpha = 0.07, InputName inputName = InputName.Close)
    {
        _alpha = alpha;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EhlersInstantaneousTrendlineV2State(double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = alpha;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EhlersInstantaneousTrendlineV2;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevIt1 = 0;
        _prevIt2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevIt1 = _index >= 1 ? _prevIt1 : 0;
        var prevIt2 = _index >= 2 ? _prevIt2 : 0;

        var alpha2 = MathHelper.Pow(_alpha, 2);
        var it = _index < 7
            ? (value + (2 * prevValue1) + prevValue2) / 4
            : ((_alpha - (alpha2 / 4)) * value) + (0.5 * alpha2 * prevValue1) - ((_alpha - (0.75 * alpha2)) * prevValue2) +
              (2 * (1 - _alpha) * prevIt1) - (MathHelper.Pow(1 - _alpha, 2) * prevIt2);

        var lag = (2 * it) - prevIt2;

        if (isFinal)
        {
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevIt2 = _prevIt1;
            _prevIt1 = it;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eit", it },
                { "Signal", lag }
            };
        }

        return new StreamingIndicatorStateResult(it, outputs);
    }
}

public sealed class EhlersInverseFisherTransformState : IStreamingIndicatorState, IDisposable
{
    private readonly StreamingInputResolver _input;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _smoother;

    public EhlersInverseFisherTransformState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 5, int length2 = 9, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(inputName, null);
        _rsi = new RsiState(maType, resolvedLength1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
    }

    public EhlersInverseFisherTransformState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _rsi = new RsiState(maType, resolvedLength1);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
    }

    public IndicatorName Name => IndicatorName.EhlersInverseFisherTransform;

    public void Reset()
    {
        _rsi.Reset();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var v1 = 0.1 * (rsi - 50);
        var v2 = _smoother.Next(v1, isFinal);
        var bottom = MathHelper.Exp(2 * v2) + 1;
        var inverseFisherTransform = bottom != 0
            ? MathHelper.MinOrMax((MathHelper.Exp(2 * v2) - 1) / bottom, 1, -1)
            : 0;

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
        _rsi.Dispose();
        _smoother.Dispose();
    }
}

public sealed class EhlersKaufmanAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private readonly RollingWindowSum _diffSum;
    private readonly PooledRingBuffer<double> _values;
    private double _prevValue;
    private double _prevKama;
    private bool _hasPrev;
    private int _index;

    public EhlersKaufmanAdaptiveMovingAverageState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
        _diffSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
    }

    public EhlersKaufmanAdaptiveMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _diffSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.EhlersKaufmanAdaptiveMovingAverage;

    public void Reset()
    {
        _diffSum.Reset();
        _values.Clear();
        _prevValue = 0;
        _prevKama = 0;
        _hasPrev = false;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = Math.Abs(value - prevValue);
        var diffSum = isFinal ? _diffSum.Add(diff, out _) : _diffSum.Preview(diff, out _);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length - 1);
        var ef = diffSum != 0 ? Math.Min(Math.Abs(value - priorValue) / diffSum, 1) : 0;
        var s = MathHelper.Pow((0.6667 * ef) + 0.0645, 2);
        var prevKama = _index >= 1 ? _prevKama : 0;
        var kama = (s * value) + ((1 - s) * prevKama);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _prevKama = kama;
            _hasPrev = true;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ekama", kama }
            };
        }

        return new StreamingIndicatorStateResult(kama, outputs);
    }

    public void Dispose()
    {
        _diffSum.Dispose();
        _values.Dispose();
    }
}

internal sealed class EhlersHammingMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;

    public EhlersHammingMovingAverageSmoother(int length, double pedestal = 3)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = Math.Sin(pedestal + ((Math.PI - (2 * pedestal)) * ((double)j / (_length - 1))));
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, bool isFinal)
    {
        var count = _values.Count;
        double sum = _weights[0] * value;

        for (var j = 1; j < _length; j++)
        {
            var offset = j;
            var prevValue = offset <= count ? _values[count - offset] : 0;
            sum += _weights[j] * prevValue;
        }

        var result = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        return result;
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
