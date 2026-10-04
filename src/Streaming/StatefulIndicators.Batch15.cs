#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Ppo")]
public sealed class ImpulsePercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly int _signalLength;
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly RoundedPartialMeanSmoother _signalSum;
    private StreamingInputResolver _input;

    public ImpulsePercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 34, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _signalLength = Math.Max(1, signalLength);
        _ema1 = new EmaState(resolved);
        _ema2 = new EmaState(resolved);
        _highSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSum = new RoundedPartialMeanSmoother(_signalLength);
        _input = new StreamingInputResolver(InputName.TypicalPrice, b => RollingMoneyFlowIndex.TypicalPrice(b.High, b.Low, b.Close));
    }

    public IndicatorName Name => IndicatorName.ImpulsePercentagePriceOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _highSmoother.Reset();
        _lowSmoother.Reset();
        _signalSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        var mi = ExponentialExtrapolation.Double(ema1, ema2);
        var hi = _highSmoother.Next(bar.High, isFinal);
        var lo = _lowSmoother.Next(bar.Low, isFinal);
        var ppo = RoundedImpulseOscillator.Line(mi, hi, lo, true);
        var signal = _signalSum.Next(ppo, isFinal);
        var histogram = ppo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ppo", ppo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _signalSum.Dispose();
    }
}

[PrimaryOutput("Inertia")]
public sealed class InertiaIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityIndexEngine _rviHigh;
    private readonly RelativeVolatilityIndexEngine _rviLow;
    private readonly IMovingAverageSmoother? _smoother;
    private readonly InertiaSmoother? _exact;

    public InertiaIndicatorState(MovingAvgType maType = MovingAvgType.LinearRegression, int length = 20, int rviLength = 14)
    {
        var resolved = Math.Max(1, length);
        var resolvedRvi = Math.Max(1, rviLength);
        _rviHigh = new RelativeVolatilityIndexEngine(MovingAvgType.WildersSmoothingMethod, 10, resolvedRvi);
        _rviLow = new RelativeVolatilityIndexEngine(MovingAvgType.WildersSmoothingMethod, 10, resolvedRvi);
        if (InertiaSmoother.Supports(maType)) _exact = new(maType, resolved);
        else _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.InertiaIndicator;

    public void Reset()
    {
        _rviHigh.Reset();
        _rviLow.Reset();
        _exact?.Reset();
        _smoother?.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var rviHigh = _rviHigh.Next(bar.High, bar, isFinal);
        var rviLow = _rviLow.Next(bar.Low, bar, isFinal);
        var rvi = RelativeVolatilityWindow.Mean(rviHigh, rviLow);

        var inertia = _exact is not null ? _exact.Next(rvi, isFinal) : _smoother!.Next(rvi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Inertia", inertia }
            };
        }

        return new StreamingIndicatorStateResult(inertia, outputs);
    }

    public void Dispose()
    {
        _rviHigh.Dispose();
        _rviLow.Dispose();
        _smoother?.Dispose();
    }
}

[PrimaryOutput("Ir")]
public sealed class InformationRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly ReturnScoreWindow _window;
    public InformationRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = .05)
        => _window = new ReturnScoreWindow(maType, length, bmk, true);
    public IndicatorName Name => IndicatorName.InformationRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Ir", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Iidx")]
public sealed class InsyncIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private bool _customInput;
    private CustomInputRange _componentRange;
    private readonly InsyncWindow _window;
    public InsyncIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14, int mfiLength = 20, int bbLength = 20,
        int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14, int stochLength = 14, int stochKLength = 1,
        int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
        => _window = new(fastLength, slowLength, mfiLength, bbLength, cciLength, dpoLength, rocLength, rsiLength, stochLength, stochKLength, stochDLength, smaLength, stdDevMult, divisor);
    public IndicatorName Name => IndicatorName.InsyncIndex;
    void ICustomInputConsumer.ReadCloseAsInput() => _customInput = true;
    public void Reset() { _componentRange.Reset(); _window.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var ranged = _customInput ? _componentRange.Next(bar, bar.Close, isFinal) : bar;
        var typical = _customInput ? bar.Close : CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close);
        var value = _window.Next(bar.Close, typical, bar.High, bar.Low, bar.Volume, ranged.High, ranged.Low, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Iidx", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ibs")]
public sealed class InternalBarStrengthIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly InternalBarStrengthWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public InternalBarStrengthIndicatorState(int length=14,int smoothLength=3)=>_window=new(length,smoothLength);
    public IndicatorName Name=>IndicatorName.InternalBarStrengthIndicator;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var result=_window.Next(bar.High,bar.Low,_input.GetValue(bar),isFinal);
        return new(result.Value,includeOutputs?new Dictionary<string,double>{{"Ibs",result.Value},{"Signal",result.Signal}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class InterquartileRangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _order;

    public InterquartileRangeBandsState(int length = 14, double mult = 1.5)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.InterquartileRangeBands;

    public void Reset()
    {
        _order.Dispose();
        _order = new RollingOrderStatistic(_length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (isFinal)
        {
            _order.Add(value);
        }

        var q1 = isFinal ? _order.PercentileNearestRank(25) : _order.PercentileNearestRank(25, value);
        var q3 = isFinal ? _order.PercentileNearestRank(75) : _order.PercentileNearestRank(75, value);
        var upper = RangeBandArithmetic.Band(q3, q3, q1, _mult);
        var lower = RangeBandArithmetic.Band(q1, q3, q1, -_mult);
        var middle = RangeBandArithmetic.Midpoint(q1, q3);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _order.Dispose();
    }
}

[PrimaryOutput("Idwma")]
public sealed class InverseDistanceWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly DistanceMassWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public InverseDistanceWeightedMovingAverageState(int length = 14)
    {
        _mean = new DistanceMassWindowMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.InverseDistanceWeightedMovingAverage;

    public void Reset()
    {
        _mean.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var idwma = _mean.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Idwma", idwma }
            };
        }

        return new StreamingIndicatorStateResult(idwma, outputs);
    }

    public void Dispose()
    {
        _mean.Dispose();
    }
}

[PrimaryOutput("Iffzs")]
public sealed class InverseFisherFastZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly FastZScoreState _score;
    public InverseFisherFastZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50) => _score = new FastZScoreState(maType, length);
    public IndicatorName Name => IndicatorName.InverseFisherFastZScore;
    public void Reset() => _score.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = StandardizedScoreWindow.Inverse(_score.Update(bar, isFinal, false).Value, true);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Iffzs", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _score.Dispose();
}

[PrimaryOutput("Ifzs")]
public sealed class InverseFisherZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly ZScoreState _score;
    public InverseFisherZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100) => _score = new ZScoreState(maType, length);
    public IndicatorName Name => IndicatorName.InverseFisherZScore;
    public void Reset() => _score.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = StandardizedScoreWindow.Inverse(_score.Update(bar, isFinal, false).Value, false);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Ifzs", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _score.Dispose();
}

[PrimaryOutput("Jo")]
public sealed class JapaneseCorrelationCoefficientState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _length1;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly IMovingAverageSmoother _closeSmoother;
    private readonly RollingWindowMax _highest;
    private readonly RollingWindowMin _lowest;
    private readonly PooledRingBuffer<double> _closeValues;
    private readonly StreamingInputResolver _input;

    public JapaneseCorrelationCoefficientState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling(_length / 2d));
        _highSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _highest = new RollingWindowMax(_length1);
        _lowest = new RollingWindowMin(_length1);
        _closeValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.JapaneseCorrelationCoefficient;

    public void Reset()
    {
        _highSmoother.Reset();
        _lowSmoother.Reset();
        _closeSmoother.Reset();
        _highest.Reset();
        _lowest.Reset();
        _closeValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highMa = _highSmoother.Next(bar.High, isFinal);
        var lowMa = _lowSmoother.Next(bar.Low, isFinal);
        var closeMa = _closeSmoother.Next(value, isFinal);
        var highest = isFinal ? _highest.Add(highMa, out _) : _highest.Preview(highMa, out _);
        var lowest = isFinal ? _lowest.Add(lowMa, out _) : _lowest.Preview(lowMa, out _);
        var prevC = EhlersStreamingWindow.GetOffsetValue(_closeValues, closeMa, _length);
        var jo = ExactDifferenceRatio.Of(closeMa, prevC, highest, lowest);

        if (isFinal)
        {
            _closeValues.TryAdd(closeMa, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Jo", jo }
            };
        }

        return new StreamingIndicatorStateResult(jo, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _closeSmoother.Dispose();
        _highest.Dispose();
        _lowest.Dispose();
        _closeValues.Dispose();
    }
}

[PrimaryOutput("Rsx")]
public sealed class JmaRsxCloneState : IStreamingIndicatorState
{
    private readonly RsxWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public JmaRsxCloneState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.JmaRsxClone;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(_input.GetValue(bar), isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Rsx", value } } : null);
    }
}

[PrimaryOutput("Jrcfd")]
public sealed class JrcFractalDimensionState : IStreamingIndicatorState, IDisposable
{
    private readonly JrcWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public JrcFractalDimensionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 5, int smoothLength = 5) => _window = new(maType, length1, length2, smoothLength);
    public IndicatorName Name => IndicatorName.JrcFractalDimension;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.High, bar.Low, _input.GetValue(bar), isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Jrcfd", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Jma")]
public sealed class JsaMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public JsaMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.JsaMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var jma = PriceMean.Of(value, priorValue);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Jma", jma }
            };
        }

        return new StreamingIndicatorStateResult(jma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

[PrimaryOutput("Jma")]
public sealed class JurikMovingAverageState : IStreamingIndicatorState
{
    private readonly JmaWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public JurikMovingAverageState(int length = 7, double phase = 50, double power = 2) => _window = new(length, phase, power);
    public IndicatorName Name => IndicatorName.JurikMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(_input.GetValue(bar), isFinal).Value;
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Jma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
}

[PrimaryOutput("Ks")]
public sealed class KalmanSmootherState : IStreamingIndicatorState
{
    private readonly KalmanSmootherWindow _window;
    public KalmanSmootherState(int length = 200) => _window = new(length);
    public IndicatorName Name => IndicatorName.KalmanSmoother;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Line;
        return new(value, includeOutputs ? new Dictionary<string, double> { ["Ks"] = value } : null);
    }
}

[PrimaryOutput("Ko")]
public sealed class KarobeinOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly KarobeinWindow _window;
    public KarobeinOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 50) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.KarobeinOscillator;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Line;
        return new(value, includeOutputs ? new Dictionary<string, double> { ["Ko"] = value } : null);
    }
}

[PrimaryOutput("Kcd")]
public sealed class KaseConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly KaseConvergenceWindow _window;
    public KaseConvergenceDivergenceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 30, int length2 = 3, int length3 = 8)
        => _window = new(maType, length1, length2, length3);
    public IndicatorName Name => IndicatorName.KaseConvergenceDivergence;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Kcd", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Dev1")]
public sealed class KaseDevStopV1State : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly KaseStopV1Window _window;
    private bool _selectedInput;
    public KaseDevStopV1State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6) => _window = new(maType, fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4);
    void ICustomInputConsumer.ReadCloseAsInput() => _selectedInput = true;
    public IndicatorName Name => IndicatorName.KaseDevStopV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var input = _selectedInput ? bar.Close : KaseStopV1Window.Typical(bar.High, bar.Low, bar.Close); var point = _window.Next(bar.High, bar.Low, bar.Close, input, isFinal);
        return new(point.Dev1, includeOutputs ? new Dictionary<string, double> { { "Dev1", point.Dev1 }, { "Dev2", point.Dev2 }, { "Dev3", point.Dev3 }, { "WarningLine", point.WarningLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Dev1")]
public sealed class KaseDevStopV2State : IStreamingIndicatorState, IDisposable
{
    private readonly KaseStopV2Window _window;
    public KaseDevStopV2State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 10, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6) => _window = new(maType, fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4);
    public IndicatorName Name => IndicatorName.KaseDevStopV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Dev1, includeOutputs ? new Dictionary<string, double> { { "Dev1", point.Dev1 }, { "Dev2", point.Dev2 }, { "Dev3", point.Dev3 }, { "Dev4", point.Dev4 } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("KaseUp")]
public sealed class KaseIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly KaseRatioWindow _window;
    public KaseIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.KaseIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new(point.Up, includeOutputs ? new Dictionary<string, double> { { "KaseUp", point.Up }, { "KaseDn", point.Down } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Kpo")]
public sealed class KasePeakOscillatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly KasePeakOscillatorV1Engine _engine;
    private double _prevPk;

    public KasePeakOscillatorV1State(int length = 30, int smoothLength = 3)
    {
        _engine = new KasePeakOscillatorV1Engine(Math.Max(1, length), Math.Max(1, smoothLength));
    }

    public IndicatorName Name => IndicatorName.KasePeakOscillatorV1;

    public void Reset()
    {
        _engine.Reset();
        _prevPk = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var pk = _engine.Next(bar, isFinal, out var mn, out var sd);
        var v1 = mn + (1.33 * sd) > 2.08 ? mn + (1.33 * sd) : 2.08;
        var v2 = mn - (1.33 * sd) < -1.92 ? mn - (1.33 * sd) : -1.92;
        var prevPk = _prevPk;
        var ln = prevPk >= 0 && pk > 0 ? v1 : prevPk <= 0 && pk < 0 ? v2 : 0;

        if (isFinal)
        {
            _prevPk = pk;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Kpo", ln },
                { "Pk", pk }
            };
        }

        return new StreamingIndicatorStateResult(ln, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

[PrimaryOutput("Kpo")]
public sealed class KasePeakOscillatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly KasePeakV2Window _window;
    public KasePeakOscillatorV2State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 8, int slowLength = 65, int length1 = 9, int length2 = 30, int length3 = 50, int smoothLength = 3, double devFactor = 2, double sensitivity = 40)
        => _window = new(maType, fastLength, slowLength, length1, length2, smoothLength, sensitivity);
    public IndicatorName Name => IndicatorName.KasePeakOscillatorV2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Kpo", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("KsdiUp")]
public sealed class KaseSerialDependencyIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingStandardDeviation _stdDev;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly StreamingInputResolver _input;
    private double _tempLog;
    private double _prevValue;
    private bool _hasPrev;

    public KaseSerialDependencyIndexState(int length = 14)
    {
        _length = Math.Max(1, length);
        _stdDev = new RollingStandardDeviation(_length);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.KaseSerialDependencyIndex;

    public void Reset()
    {
        _stdDev.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _tempLog = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        _tempLog = StableLogRatio.OfSameSign(value, prevValue);
        var volatility = _stdDev.Next(_tempLog, isFinal);

        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, _length);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, _length);
        var ksdiUpLog = StableLogRatio.OfSameSign(bar.High, prevLow);
        var ksdiDownLog = StableLogRatio.OfSameSign(bar.Low, prevHigh);
        var ksdiUp = volatility != 0 ? ksdiUpLog / volatility : 0;
        var ksdiDown = volatility != 0 ? ksdiDownLog / volatility : 0;

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "KsdiUp", ksdiUp },
                { "KsdiDn", ksdiDown }
            };
        }

        return new StreamingIndicatorStateResult(ksdiUp, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class KaufmanAdaptiveBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;
    private double _prevMiddle;
    private double _prevPowMa;

    public KaufmanAdaptiveBandsState(int length = 100, double stdDevFactor = 3)
    {
        _stdDevFactor = Builder.Specs.KaufmanAdaptiveBandsSpecOptions.ValidateExponent(stdDevFactor);
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveBands;

    public void Reset()
    {
        _er.Reset();
        _prevMiddle = 0;
        _prevPowMa = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var erPow = MathHelper.Pow(er, _stdDevFactor);
        var middle = (value * erPow) + ((1 - erPow) * _prevMiddle);
        var powMa = (MathHelper.Pow(value, 2) * erPow) + ((1 - erPow) * _prevPowMa);
        var middleSq = middle * middle;
        var dev = powMa - middleSq >= 0 ? MathHelper.Sqrt(powMa - middleSq) : 0;
        var upper = middle + dev;
        var lower = middle - dev;

        if (isFinal)
        {
            _prevMiddle = middle;
            _prevPowMa = powMa;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

[PrimaryOutput("Kaco")]
public sealed class KaufmanAdaptiveCorrelationOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _srcMa = null!;
    private readonly IMovingAverageSmoother _indexMa = null!;
    private readonly IMovingAverageSmoother _indexSrcMa = null!;
    private readonly IMovingAverageSmoother _index2Ma = null!;
    private readonly IMovingAverageSmoother _src2Ma = null!;
    private readonly StreamingInputResolver _input;
    private int _index;
    private readonly KaufmanRegressionMoments? _moments;

    public KaufmanAdaptiveCorrelationOscillatorState(MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage,
        int length = 14)
    {
        var resolved = Math.Max(1, length);
        if (maType == MovingAvgType.KaufmanAdaptiveMovingAverage)
        { _moments = new KaufmanRegressionMoments(resolved); _input = new StreamingInputResolver(InputName.Close, null); return; }
        _srcMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _indexMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _indexSrcMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _index2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
        _src2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveCorrelationOscillator;

    public void Reset()
    {
        _srcMa?.Reset();
        _indexMa?.Reset();
        _indexSrcMa?.Reset();
        _index2Ma?.Reset();
        _src2Ma?.Reset();
        _index = 0;
        _moments?.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_moments is not null)
        {
            _moments.Next(bar.Close, isFinal, out var time, out var price, out var correlation);
            return new(correlation, includeOutputs ? new Dictionary<string, double> { { "IndexSt", time }, { "SrcSt", price }, { "Kaco", correlation } } : null);
        }
        var value = _input.GetValue(bar);
        double index = _index;
        var indexSrc = index * value;
        var src2 = value * value;
        var index2 = index * index;

        var srcMa = _srcMa.Next(value, isFinal);
        var indexMa = _indexMa.Next(index, isFinal);
        var indexSrcMa = _indexSrcMa.Next(indexSrc, isFinal);
        var index2Ma = _index2Ma.Next(index2, isFinal);
        var src2Ma = _src2Ma.Next(src2, isFinal);

        var indexSqrt = index2Ma - (indexMa * indexMa);
        var indexSt = indexSqrt >= 0 ? MathHelper.Sqrt(indexSqrt) : 0;
        var srcSqrt = src2Ma - (srcMa * srcMa);
        var srcSt = srcSqrt >= 0 ? MathHelper.Sqrt(srcSqrt) : 0;
        var denom = indexSt * srcSt;
        var r = denom != 0 ? (indexSrcMa - (indexMa * srcMa)) / denom : 0;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "IndexSt", indexSt },
                { "SrcSt", srcSt },
                { "Kaco", r }
            };
        }

        return new StreamingIndicatorStateResult(r, outputs);
    }

    public void Dispose()
    {
        _moments?.Dispose();
        _srcMa?.Dispose();
        _indexMa?.Dispose();
        _indexSrcMa?.Dispose();
        _index2Ma?.Dispose();
        _src2Ma?.Dispose();
    }
}

internal sealed class RelativeVolatilityIndexEngine : IDisposable
{
    private readonly RelativeVolatilityWindow _window;
    public RelativeVolatilityIndexEngine(MovingAvgType maType, int length, int smoothLength) { _window = new(maType, length, smoothLength); }
    public double Next(double value, OhlcvBar bar, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class KasePeakOscillatorV1Engine : IDisposable
{
    private readonly int _length;
    private readonly double _sqrtLength;
    private readonly WilderState _atr;
    private readonly IMovingAverageSmoother _pkSmoother;
    private readonly IMovingAverageSmoother _mnSmoother;

    // The deviation of the peak-oscillator window about its own mean, matching the batch calculation; see
    // #190. This engine is the third implementation of the Kase peak oscillator: the batch, the V1 state,
    // and this, which KasePeakOscillatorV1State and KaseConvergenceDivergenceState both compose.
    private readonly RollingStandardDeviation _stdDev;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private double _prevClose;
    private bool _hasPrev;

    public KasePeakOscillatorV1Engine(int length, int smoothLength)
    {
        _length = Math.Max(1, length);
        _sqrtLength = MathHelper.Sqrt(_length);
        _atr = new WilderState(_length);
        _pkSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, smoothLength));
        _mnSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, _length);
        _stdDev = new RollingStandardDeviation(_length);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
    }

    public double Next(OhlcvBar bar, bool isFinal, out double mn, out double stdDev)
    {
        // For TrueRange on first bar, use current close to avoid inflated TR
        var prevClose = _hasPrev ? _prevClose : bar.Close;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atr.GetNext(tr, isFinal);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, _length);
        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, _length);
        var rwh = atr != 0 ? (bar.High - prevLow) / atr * _sqrtLength : 0;
        var rwl = atr != 0 ? (prevHigh - bar.Low) / atr * _sqrtLength : 0;
        var diff = rwh - rwl;
        var pk = _pkSmoother.Next(diff, isFinal);
        stdDev = _stdDev.Next(pk, isFinal);
        mn = _mnSmoother.Next(pk, isFinal);

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        return pk;
    }

    public void Reset()
    {
        _atr.Reset();
        _pkSmoother.Reset();
        _mnSmoother.Reset();
        _stdDev.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _prevClose = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _pkSmoother.Dispose();
        _mnSmoother.Dispose();
        _stdDev.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

internal sealed class KaufmanAdaptiveMovingAverageEngine : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly EfficiencyRatioState _er;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private double _prevKama;
    private int _index;

    public KaufmanAdaptiveMovingAverageEngine(int length, int fastLength = 2, int slowLength = 30)
    {
        _length = Math.Max(1, length);
        _er = new EfficiencyRatioState(_length);
        _fastAlpha = 2d / (fastLength + 1);
        _slowAlpha = 2d / (slowLength + 1);
    }

    public double Next(double value, bool isFinal)
    {
        // Always call ER to build up history, even during warmup
        var er = _er.Next(value, isFinal);

        // Match Core behavior: during warmup (index < length), just return the input value
        if (_index < _length)
        {
            if (isFinal)
            {
                _prevKama = value;
                _index++;
            }
            return value;
        }

        var sc = MathHelper.Pow((er * (_fastAlpha - _slowAlpha)) + _slowAlpha, 2);
        var kama = _prevKama + (sc * (value - _prevKama));
        if (isFinal)
        {
            _prevKama = kama;
            _index++;
        }

        return kama;
    }

    public void Reset()
    {
        _er.Reset();
        _prevKama = 0;
        _index = 0;
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}
