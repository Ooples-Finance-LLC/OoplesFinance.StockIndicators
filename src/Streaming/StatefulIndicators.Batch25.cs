#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Tfh")]
public sealed class TrendForceHistogramState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;
    private double _prevHighest;
    private double _prevLowest;
    private double _prevA;
    private double _prevB;
    private double _prevC;
    private double _prevD;
    private ExactMeanAccumulator _avgSum;
    private int _index;
    private bool _hasPrev;

    public TrendForceHistogramState(int length = 14)
    {
        _length = Math.Max(2, length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrendForceHistogram;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _prevHighest = 0;
        _prevLowest = 0;
        _prevA = 0;
        _prevB = 0;
        _prevC = 0;
        _prevD = 0;
        _avgSum = default;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var prevLowest = _hasPrev ? _prevLowest : 0;
        var a = value > prevHighest ? 1d : 0d;
        var b = value < prevLowest ? 1d : 0d;
        var c = a == 1d ? _prevC + 1 : b - _prevB == 1d ? 0 : _prevC;
        var d = b == 1d ? _prevD + 1 : a - _prevA == 1d ? 0 : _prevD;
        var avg = (c + d) / 2;
        var avgSum = _avgSum;
        avgSum.Add(avg);
        var rmean = avgSum.Mean(_index + 1);
        var osc = avg - rmean;

        if (isFinal)
        {
            _prevHighest = _maxWindow.Add(value, out _);
            _prevLowest = _minWindow.Add(value, out _);
            _prevA = a;
            _prevB = b;
            _prevC = c;
            _prevD = d;
            _avgSum = avgSum;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tfh", osc }
            };
        }

        return new StreamingIndicatorStateResult(osc, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

[PrimaryOutput("Tif")]
public sealed class TrendImpulseFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;
    private double _prevHighest;
    private double _prevLowest;
    private double _prevB;
    private bool _hasPrev;

    public TrendImpulseFilterState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 100, int length2 = 10)
    {
        _maxWindow = new RollingWindowMax(Math.Max(2, length1));
        _minWindow = new RollingWindowMin(Math.Max(2, length1));
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length2)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrendImpulseFilter;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _signalSmoother.Reset();
        _prevHighest = 0;
        _prevLowest = 0;
        _prevB = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var prevLowest = _hasPrev ? _prevLowest : 0;
        var prevB = _hasPrev ? _prevB : value;
        var a = value > prevHighest || value < prevLowest ? 1d : 0d;
        var b = (a * value) + ((1 - a) * prevB);
        var tif = _signalSmoother.Next(b, isFinal);

        if (isFinal)
        {
            _prevB = b;
            _prevHighest = _maxWindow.Add(value, out _);
            _prevLowest = _minWindow.Add(value, out _);
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tif", tif }
            };
        }

        return new StreamingIndicatorStateResult(tif, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Tii")]
public sealed class TrendIntensityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendIntensityWindow _window;
    public TrendIntensityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 30, int slowLength = 60)
        => _window = new TrendIntensityWindow(maType, fastLength, slowLength);
    public IndicatorName Name => IndicatorName.TrendIntensityIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Tii", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tpr")]
public sealed class TrendPersistenceRateState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _mult;
    private readonly double _threshold;
    private readonly IMovingAverageSmoother _smoother;
    private readonly RollingWindowSum _ctrPSum;
    private readonly RollingWindowSum _ctrMSum;
    private readonly PooledRingBuffer<double> _maValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public TrendPersistenceRateState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        int smoothLength = 5, double mult = 0.01, double threshold = 1)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _threshold = threshold;
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _ctrPSum = new RollingWindowSum(_length);
        _ctrMSum = new RollingWindowSum(_length);
        _maValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrendPersistenceRate;

    public void Reset()
    {
        _smoother.Reset();
        _ctrPSum.Reset();
        _ctrMSum.Reset();
        _maValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _smoother.Next(value, isFinal);
        var prevMa1 = _index >= 1 ? EhlersStreamingWindow.GetOffsetValue(_maValues, ma, 1) : 0;
        var prevMa2 = _index >= 2 ? EhlersStreamingWindow.GetOffsetValue(_maValues, ma, 2) : 0;
        var diff = (prevMa1 - prevMa2) / _mult;
        var ctrP = diff > _threshold ? 1d : 0d;
        var ctrM = diff < -_threshold ? 1d : 0d;
        var ctrPSum = isFinal ? _ctrPSum.Add(ctrP, out _) : _ctrPSum.Preview(ctrP, out _);
        var ctrMSum = isFinal ? _ctrMSum.Add(ctrM, out _) : _ctrMSum.Preview(ctrM, out _);
        var tpr = _length != 0 ? Math.Abs(100 * (ctrPSum - ctrMSum) / _length) : 0;

        if (isFinal)
        {
            _maValues.TryAdd(ma, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tpr", tpr }
            };
        }

        return new StreamingIndicatorStateResult(tpr, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _ctrPSum.Dispose();
        _ctrMSum.Dispose();
        _maValues.Dispose();
    }
}

[PrimaryOutput("Ts")]
public sealed class TrendStepState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendStepWindow _window;
    private readonly StreamingInputResolver _input;
    public TrendStepState(int length = 50) { _window = new(length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.TrendStep;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ts", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class TrendTraderBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendTraderWindow _window;
    public TrendTraderBandsState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 21, double mult = 3, double bandStep = 20) { _window = new(maType, length, mult, bandStep); }
    public IndicatorName Name => IndicatorName.TrendTraderBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ttf")]
public sealed class TrendTriggerFactorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly TrendTriggerWindow _window;
    public TrendTriggerFactorState(int length = 15) => _window = new(length);
    public IndicatorName Name => IndicatorName.TrendTriggerFactor;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ttf", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tr")]
public sealed class TreynorRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly TargetReturnWindow _window;
    public TreynorRatioState(int length = 30, double beta = 1, double bmk = .02) => _window = new TargetReturnWindow(length, bmk, false, beta);
    public IndicatorName Name => IndicatorName.TreynorRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Tr", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("To")]
public sealed class TrigonometricOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly TrigonometricWindow _window;
    public TrigonometricOscillatorState(int length = 200) => _window = new(length);
    public IndicatorName Name => IndicatorName.TrigonometricOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "To", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Trimean")]
public sealed class TrimeanState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _order;

    public TrimeanState(int length = 14)
    {
        _length = Math.Max(1, length);
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Trimean;

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
        var median = isFinal ? _order.PercentileNearestRank(50) : _order.PercentileNearestRank(50, value);
        var q3 = isFinal ? _order.PercentileNearestRank(75) : _order.PercentileNearestRank(75, value);
        var trimean = PriceMean.Of(q1, median, median, q3);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Trimean", trimean },
                { "Q1", q1 },
                { "Median", median },
                { "Q3", q3 }
            };
        }

        return new StreamingIndicatorStateResult(trimean, outputs);
    }

    public void Dispose()
    {
        _order.Dispose();
    }
}

[PrimaryOutput("Tema")]
public sealed class TripleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _ema3;
    private readonly StreamingInputResolver _input;

    public TripleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TripleExponentialMovingAverage;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var ema3 = _ema3.Next(ema2, isFinal);
        var tema = ExponentialExtrapolation.Triple(ema1, ema2, ema3);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tema", tema }
            };
        }

        return new StreamingIndicatorStateResult(tema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _ema3.Dispose();
    }
}

[PrimaryOutput("Tslsma")]
public sealed class TStepLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly TStepLeastSquaresWindow _window;
    public TStepLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100, double sc = 0.5)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.TStepLeastSquaresMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Tslsma", point.Line } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sbs")]
public sealed class TTMScalperIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _closes;
    private readonly StreamingInputResolver _input;
    private double _prevBuySellSwitch;
    private double _prevSbs;
    private double _prevClrs;

    public TTMScalperIndicatorState()
    {
        _closes = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TTMScalperIndicator;

    public void Reset()
    {
        _closes.Clear();
        _prevBuySellSwitch = 0;
        _prevSbs = 0;
        _prevClrs = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var prevClose1 = EhlersStreamingWindow.GetOffsetValue(_closes, close, 1);
        var prevClose2 = EhlersStreamingWindow.GetOffsetValue(_closes, close, 2);
        var prevClose3 = EhlersStreamingWindow.GetOffsetValue(_closes, close, 3);
        var high = bar.High;
        var low = bar.Low;
        double triggerSell = prevClose1 < close && (prevClose2 < prevClose1 || prevClose3 < prevClose1) ? 1 : 0;
        double triggerBuy = prevClose1 > close && (prevClose2 > prevClose1 || prevClose3 > prevClose1) ? 1 : 0;
        var buySellSwitch = triggerSell == 1 ? 1 : triggerBuy == 1 ? 0 : _prevBuySellSwitch;
        var sbs = triggerSell == 1 && _prevBuySellSwitch == 0 ? high :
            triggerBuy == 1 && _prevBuySellSwitch == 1 ? low : _prevSbs;
        var clrs = triggerSell == 1 && _prevBuySellSwitch == 0 ? 1 :
            triggerBuy == 1 && _prevBuySellSwitch == 1 ? -1 : _prevClrs;

        if (isFinal)
        {
            _closes.TryAdd(close, out _);
            _prevBuySellSwitch = buySellSwitch;
            _prevSbs = sbs;
            _prevClrs = clrs;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sbs", sbs }
            };
        }

        return new StreamingIndicatorStateResult(sbs, outputs);
    }

    public void Dispose()
    {
        _closes.Dispose();
    }
}

[PrimaryOutput("Ts")]
public sealed class TurboScalerState : IStreamingIndicatorState, IDisposable
{
    private readonly TurboScalerWindow _window;
    public TurboScalerState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, double alpha = 0.5)
        => _window = new(maType, length, alpha);
    public IndicatorName Name => IndicatorName.TurboScaler;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ts", point.Line }, { "Trigger", point.Trigger } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tsf")]
public sealed class TurboStochasticsFastState : IStreamingIndicatorState, IDisposable
{
    private readonly TurboStochasticsWindow _window;
    public TurboStochasticsFastState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 20, int length2 = 10, int turboLength = 2)
        => _window = new(maType, length1, length2, turboLength, false);
    public IndicatorName Name => IndicatorName.TurboStochasticsFast;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Tsf", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tsf")]
public sealed class TurboStochasticsSlowState : IStreamingIndicatorState, IDisposable
{
    private readonly TurboStochasticsWindow _window;
    public TurboStochasticsSlowState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 20, int length2 = 10, int turboLength = 2)
        => _window = new(maType, length1, length2, turboLength, true);
    public IndicatorName Name => IndicatorName.TurboStochasticsSlow;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Tsf", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("BullLine")]
public sealed class TurboTriggerState : IStreamingIndicatorState, IDisposable
{
    private readonly TurboTriggerWindow _window;
    public TurboTriggerState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100, int smoothLength = 2) => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.TurboTrigger;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, isFinal);
        return new(value.Bull, includeOutputs ? new Dictionary<string, double> { { "BullLine", value.Bull }, { "Trigger", value.Trigger } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tmf")]
public sealed class TwiggsMoneyFlowState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly MoneyFlowPercentWindow _window;
    public TwiggsMoneyFlowState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21)
        => _window = new MoneyFlowPercentWindow(length, maType);
    public IndicatorName Name => IndicatorName.TwiggsMoneyFlow;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Tmf", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Uti")]
public sealed class UberTrendIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly UberTrendWindow _window;
    public UberTrendIndicatorState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.UberTrendIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Uti", point.Value } } : null);
    }
    public void Dispose() => Reset();
}

[PrimaryOutput("Cts")]
public sealed class UhlMaCrossoverSystemState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smaSmoother;

    // ka and kb divide the buffered value by a squared distance, so what is buffered has to square into a
    // variance. See issue #223.
    private readonly RollingStandardDeviation _stdDev;
    private readonly PooledRingBuffer<double> _stdDevValues;
    private readonly StreamingInputResolver _input;
    private double _prevCma;
    private double _prevCts;
    private bool _hasPrev;

    public UhlMaCrossoverSystemState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        _length = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new RollingStandardDeviation(_length);
        _stdDevValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.UhlMaCrossoverSystem;

    public void Reset()
    {
        _smaSmoother.Reset();
        _stdDev.Reset();
        _stdDevValues.Clear();
        _prevCma = 0;
        _prevCts = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smaSmoother.Next(value, isFinal);
        var stdDev = _stdDev.Next(value, isFinal);
        var prevDev = EhlersStreamingWindow.GetOffsetValue(_stdDevValues, _length);
        var prevVar = prevDev * prevDev;
        var prevCma = _hasPrev ? _prevCma : value;
        var prevCts = _hasPrev ? _prevCts : value;
        var secma = MathHelper.Pow(sma - prevCma, 2);
        var sects = MathHelper.Pow(value - prevCts, 2);
        var ka = prevVar < secma && secma != 0 ? 1 - (prevVar / secma) : 0;
        var kb = prevVar < sects && sects != 0 ? 1 - (prevVar / sects) : 0;
        var cma = (ka * sma) + ((1 - ka) * prevCma);
        var cts = (kb * value) + ((1 - kb) * prevCts);

        if (isFinal)
        {
            _stdDevValues.TryAdd(stdDev, out _);
            _prevCma = cma;
            _prevCts = cts;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Cts", cts },
                { "Cma", cma }
            };
        }

        return new StreamingIndicatorStateResult(cts, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _stdDev.Dispose();
        _stdDevValues.Dispose();
    }
}

[PrimaryOutput("Utm")]
public sealed class UltimateMomentumIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly McClellanOscillatorState _mo;
    private double _previousBlend;
    private bool _hasBlend;
    private readonly UltimateMomentumBand _bbPct;
    private readonly MoneyFlowIndexState _mfi1;
    private readonly MoneyFlowIndexState _mfi2;
    private readonly MoneyFlowIndexState _mfi3;
    private readonly UltimateMomentumStrength _strength;

    public UltimateMomentumIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 13, int length2 = 19,
        int length3 = 21, int length4 = 39, int length5 = 50, int length6 = 200, double stdDevMult = 1.5)
    {
        _ = length6;
        _mo = new McClellanOscillatorState(maType, length2, length4, 9, 1000);
        _bbPct = new UltimateMomentumBand(maType, length5, stdDevMult);
        _mfi1 = new MoneyFlowIndexState(length2);
        _mfi2 = new MoneyFlowIndexState(length3);
        _mfi3 = new MoneyFlowIndexState(length4);
        _strength = new(maType, length1);
    }

    public IndicatorName Name => IndicatorName.UltimateMomentumIndicator;

    // No resolver of its own: the input was handed to these inner states, so they are the
    // ones that must switch to reading the close.
    void ICustomInputConsumer.ReadCloseAsInput()
    {
        ((ICustomInputConsumer)_mfi1).ReadCloseAsInput();
        ((ICustomInputConsumer)_mfi2).ReadCloseAsInput();
        ((ICustomInputConsumer)_mfi3).ReadCloseAsInput();
    }

    public void Reset()
    {
        _previousBlend = 0; _hasBlend = false;
        _mo.Reset();
        _bbPct.Reset();
        _mfi1.Reset();
        _mfi2.Reset();
        _mfi3.Reset();
        _strength.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var moResult = _mo.Update(bar, isFinal, includeOutputs: true);
        var moOutputs = moResult.Outputs!;
        var advSum = moOutputs["AdvSum"];
        var decSum = moOutputs["DecSum"];
        var mo = moResult.Value;
        var bbPct = _bbPct.Next(bar.Close, isFinal);
        var mfi1 = _mfi1.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi2 = _mfi2.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi3 = _mfi3.Update(bar, isFinal, includeOutputs: false).Value;
        var ratio = decSum != 0 ? advSum / decSum : 0;
        var utm = (200 * bbPct) + (100 * ratio) + (2 * mo) + (1.5 * mfi3) + (3 * mfi2) + (3 * mfi1);
        if (_hasBlend && Math.Abs(utm-_previousBlend) <= 1.4210854715202004e-14*Math.Max(Math.Abs(utm), Math.Abs(_previousBlend)))
            utm = _previousBlend;
        if (isFinal) { _previousBlend = utm; _hasBlend = true; }
        var utmi = _strength.Next(utm, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Utm", utmi }
            };
        }

        return new StreamingIndicatorStateResult(utmi, outputs);
    }

    public void Dispose()
    {
        _mo.Dispose();
        _bbPct.Dispose();
        _mfi1.Dispose();
        _mfi2.Dispose();
        _mfi3.Dispose();
        _strength.Dispose();
    }
}

[PrimaryOutput("Uma")]
public sealed class UltimateMovingAverageState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private readonly UltimateAverageWindow _window;
    public UltimateMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5,
        int maxLength = 50, double acc = 1) => _window = new(maType, minLength, maxLength, acc);
    public IndicatorName Name => IndicatorName.UltimateMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.High, bar.Low, bar.Volume, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Uma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class UltimateMovingAverageBandsState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private readonly UltimateAverageWindow _average;
    private readonly UltimateBandWindow _bands;
    public UltimateMovingAverageBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5,
        int maxLength = 50, double stdDevMult = 2)
    {
        _bands = new(minLength, stdDevMult); _average = new(maType, minLength, maxLength, 1);
    }
    public IndicatorName Name => IndicatorName.UltimateMovingAverageBands;
    public void Reset() { _average.Reset(); _bands.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var center = _average.NextPoint(bar.Close, bar.High, bar.Low, bar.Volume, isFinal);
        var bands = _bands.Next(bar.Close, center, isFinal);
        return new(bands.Middle, includeOutputs ? new Dictionary<string, double> {
            { "UpperBand", bands.Upper }, { "MiddleBand", bands.Middle }, { "LowerBand", bands.Lower } } : null);
    }
    public void Dispose() { _average.Dispose(); _bands.Reset(); }
}

[PrimaryOutput("Uo")]
public sealed class UltimateOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly UltimatePressureWindow _window;
    public UltimateOscillatorState(int length1 = 7, int length2 = 14, int length3 = 28) => _window = new(length1, length2, length3);
    public IndicatorName Name => IndicatorName.UltimateOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Uo", value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Uto")]
public sealed class UltimateTraderOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _trMax;
    private readonly RollingWindowMin _trMin;
    private readonly RollingWindowMax _volMax;
    private readonly RollingWindowMin _volMin;
    private readonly RollingWindowMax _rangeHigh;
    private readonly RollingWindowMin _rangeLow;
    private readonly IMovingAverageSmoother _dxiAvgSmoother;
    private readonly IMovingAverageSmoother _dxisSmoother;
    private readonly IMovingAverageSmoother _dxissSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public UltimateTraderOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int lbLength = 5, int smoothLength = 4, int rangeLength = 2)
        : this(maType, length, lbLength, smoothLength, rangeLength, InputName.Close, null)
    {
    }

    /// <summary>
    /// The one place the windows and smoothers are built.
    /// </summary>
    private UltimateTraderOscillatorState(MovingAvgType maType, int length, int lbLength, int smoothLength,
        int rangeLength, InputName inputName, Func<OhlcvBar, double>? selector)
    {
        var resolvedLb = Math.Max(1, lbLength);
        var resolvedRange = Math.Max(1, rangeLength);
        _ = length;
        _trMax = new RollingWindowMax(resolvedLb);
        _trMin = new RollingWindowMin(resolvedLb);
        _volMax = new RollingWindowMax(resolvedLb);
        _volMin = new RollingWindowMin(resolvedLb);
        _rangeHigh = new RollingWindowMax(resolvedRange);
        _rangeLow = new RollingWindowMin(resolvedRange);
        _dxiAvgSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLb);
        _dxisSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _dxissSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.UltimateTraderOscillator;

    public void Reset()
    {
        _trMax.Reset();
        _trMin.Reset();
        _volMax.Reset();
        _volMin.Reset();
        _rangeHigh.Reset();
        _rangeLow.Reset();
        _dxiAvgSmoother.Reset();
        _dxisSmoother.Reset();
        _dxissSmoother.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var open = bar.Open;
        var prevClose = _hasPrev ? _prevClose : 0;
        // The first bar's true range uses the bar's own close, as the batch does, so it is High - Low rather
        // than the whole high. The momentum term c below keeps the batch's zero.
        var tr = CalculationsHelper.CalculateTrueRange(high, low, _hasPrev ? _prevClose : close);
        var trHigh = isFinal ? _trMax.Add(tr, out _) : _trMax.Preview(tr, out _);
        var trLow = isFinal ? _trMin.Add(tr, out _) : _trMin.Preview(tr, out _);
        var trRange = trHigh - trLow;
        var trSto = trRange != 0 ? MathHelper.MinOrMax((tr - trLow) / trRange * 100, 100, 0) : 0;

        var volume = bar.Volume;
        var volHigh = isFinal ? _volMax.Add(volume, out _) : _volMax.Preview(volume, out _);
        var volLow = isFinal ? _volMin.Add(volume, out _) : _volMin.Preview(volume, out _);
        var volRange = volHigh - volLow;
        var vSto = volRange != 0 ? MathHelper.MinOrMax((volume - volLow) / volRange * 100, 100, 0) : 0;

        var highest = isFinal ? _rangeHigh.Add(high, out _) : _rangeHigh.Preview(high, out _);
        var lowest = isFinal ? _rangeLow.Add(low, out _) : _rangeLow.Preview(low, out _);
        var body = close - open;
        var range = high - low;
        var c = close - prevClose;
        var sign = Math.Sign(c);
        var k1 = range != 0 ? body / range * 100 : 0;
        var k2 = range == 0 ? 0 : ((close - low) / range * 100 * 2) - 100;
        var k3 = c == 0 || highest - lowest == 0 ? 0 : ((close - lowest) / (highest - lowest) * 100 * 2) - 100;
        var k4 = highest - lowest != 0 ? c / (highest - lowest) * 100 : 0;
        var k5 = sign * trSto;
        var k6 = sign * vSto;
        var bullScore = Math.Max(0, k1) + Math.Max(0, k2) + Math.Max(0, k3) + Math.Max(0, k4) + Math.Max(0, k5) + Math.Max(0, k6);
        var bearScore = -1 * (Math.Min(0, k1) + Math.Min(0, k2) + Math.Min(0, k3) + Math.Min(0, k4) + Math.Min(0, k5) + Math.Min(0, k6));
        var totalScore = bullScore + bearScore;
        var dxi = totalScore == 0 ? 0 : 100 * (bullScore - bearScore) / totalScore;
        var dxiAvg = _dxiAvgSmoother.Next(dxi, isFinal);
        var dxis = _dxisSmoother.Next(dxiAvg, isFinal);
        var dxiss = _dxissSmoother.Next(dxis, isFinal);

        if (isFinal)
        {
            _prevClose = close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Uto", dxis },
                { "Signal", dxiss }
            };
        }

        return new StreamingIndicatorStateResult(dxis, outputs);
    }

    public void Dispose()
    {
        _trMax.Dispose();
        _trMin.Dispose();
        _volMax.Dispose();
        _volMin.Dispose();
        _rangeHigh.Dispose();
        _rangeLow.Dispose();
        _dxiAvgSmoother.Dispose();
        _dxisSmoother.Dispose();
        _dxissSmoother.Dispose();
    }
}
