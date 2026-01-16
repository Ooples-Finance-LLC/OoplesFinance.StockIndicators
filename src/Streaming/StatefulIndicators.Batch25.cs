using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

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
    private double _avgSum;
    private int _index;
    private bool _hasPrev;

    public TrendForceHistogramState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendForceHistogramState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        _avgSum = 0;
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
        var avgSum = _avgSum + avg;
        var rmean = _index != 0 ? avgSum / _index : 0;
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
        int length1 = 100, int length2 = 10, InputName inputName = InputName.Close)
    {
        _maxWindow = new RollingWindowMax(Math.Max(1, length1));
        _minWindow = new RollingWindowMin(Math.Max(1, length1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendImpulseFilterState(MovingAvgType maType, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _maxWindow = new RollingWindowMax(Math.Max(1, length1));
        _minWindow = new RollingWindowMin(Math.Max(1, length1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class TrendIntensityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly RollingWindowSum _upSum;
    private readonly RollingWindowSum _downSum;
    private readonly StreamingInputResolver _input;

    public TrendIntensityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 30, int slowLength = 60, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _upSum = new RollingWindowSum(resolvedFast);
        _downSum = new RollingWindowSum(resolvedFast);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendIntensityIndexState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _upSum = new RollingWindowSum(resolvedFast);
        _downSum = new RollingWindowSum(resolvedFast);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrendIntensityIndex;

    public void Reset()
    {
        _slowSmoother.Reset();
        _upSum.Reset();
        _downSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _slowSmoother.Next(value, isFinal);
        var deviationUp = value > sma ? value - sma : 0;
        var deviationDown = value < sma ? sma - value : 0;

        var upSum = isFinal ? _upSum.Add(deviationUp, out _) : _upSum.Preview(deviationUp, out _);
        var downSum = isFinal ? _downSum.Add(deviationDown, out _) : _downSum.Preview(deviationDown, out _);
        var tii = upSum + downSum != 0 ? upSum / (upSum + downSum) * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tii", tii }
            };
        }

        return new StreamingIndicatorStateResult(tii, outputs);
    }

    public void Dispose()
    {
        _slowSmoother.Dispose();
        _upSum.Dispose();
        _downSum.Dispose();
    }
}

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
        int smoothLength = 5, double mult = 0.01, double threshold = 1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _threshold = threshold;
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _ctrPSum = new RollingWindowSum(_length);
        _ctrMSum = new RollingWindowSum(_length);
        _maValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendPersistenceRateState(MovingAvgType maType, int length, int smoothLength, double mult, double threshold,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mult = mult;
        _threshold = threshold;
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _ctrPSum = new RollingWindowSum(_length);
        _ctrMSum = new RollingWindowSum(_length);
        _maValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class TrendStepState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private int _index;
    private bool _hasPrev;

    public TrendStepState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendStepState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrendStep;

    public void Reset()
    {
        _stdDev.Reset();
        _prevA = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var dev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value * 2;
        var prevA = _hasPrev ? _prevA : value;
        var a = _index < _length ? value : value > prevA + dev ? value : value < prevA - dev ? value : prevA;

        if (isFinal)
        {
            _prevA = a;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ts", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

public sealed class TrendTraderBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly double _bandStep;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _retSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevAtr;
    private double _prevHighest;
    private double _prevLowest;
    private double _prevRet;
    private double _prevClose;
    private bool _hasPrev;

    public TrendTraderBandsState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 21, double mult = 3, double bandStep = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _mult = mult;
        _bandStep = bandStep;
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _retSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendTraderBandsState(MovingAvgType maType, int length, double mult, double bandStep,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _mult = mult;
        _bandStep = bandStep;
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _retSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrendTraderBands;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _atrSmoother.Reset();
        _retSmoother.Reset();
        _prevAtr = 0;
        _prevHighest = 0;
        _prevLowest = 0;
        _prevRet = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var prevLowest = _hasPrev ? _prevLowest : 0;
        var prevAtr = _hasPrev ? _prevAtr : 0;
        var atrMult = prevAtr * _mult;
        var highLimit = prevHighest - atrMult;
        var lowLimit = prevLowest + atrMult;
        var prevRet = _hasPrev ? _prevRet : 0;
        var ret = close > highLimit && close > lowLimit ? highLimit
            : close < lowLimit && close < highLimit ? lowLimit
            : prevRet;
        var retEma = _retSmoother.Next(ret, isFinal);
        var upper = retEma + _bandStep;
        var lower = retEma - _bandStep;

        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);

        if (isFinal)
        {
            _prevRet = ret;
            _prevAtr = atr;
            _prevHighest = _maxWindow.Add(close, out _);
            _prevLowest = _minWindow.Add(close, out _);
            _prevClose = close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", retEma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(retEma, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _atrSmoother.Dispose();
        _retSmoother.Dispose();
    }
}

public sealed class TrendTriggerFactorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public TrendTriggerFactorState(int length = 15, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _highestValues = new PooledRingBuffer<double>(_length);
        _lowestValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrendTriggerFactorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _highestValues = new PooledRingBuffer<double>(_length);
        _lowestValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrendTriggerFactor;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_highestValues, highest, _length) : 0;
        var prevLowest = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_lowestValues, lowest, _length) : 0;
        var buyPower = highest - prevLowest;
        var sellPower = prevHighest - lowest;
        var ttf = buyPower + sellPower != 0 ? 200 * (buyPower - sellPower) / (buyPower + sellPower) : 0;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ttf", ttf }
            };
        }

        return new StreamingIndicatorStateResult(ttf, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
    }
}

public sealed class TreynorRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _beta;
    private readonly double _bench;
    private readonly RollingWindowSum _retSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public TreynorRatioState(int length = 30, double beta = 1, double bmk = 0.02, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _beta = beta;
        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = Math.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TreynorRatioState(int length, double beta, double bmk, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _beta = beta;
        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = Math.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TreynorRatio;

    public void Reset()
    {
        _retSum.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var ret = prevValue != 0 ? (value / prevValue) - 1 : 0;

        var retSum = isFinal ? _retSum.Add(ret, out var countAfter) : _retSum.Preview(ret, out countAfter);
        var retSma = countAfter > 0 ? retSum / countAfter : 0;
        var treynor = _beta != 0 ? (retSma - _bench) / _beta : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tr", treynor }
            };
        }

        return new StreamingIndicatorStateResult(treynor, outputs);
    }

    public void Dispose()
    {
        _retSum.Dispose();
        _values.Dispose();
    }
}

public sealed class TrigonometricOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly LinearRegressionState _sRegression;
    private readonly LinearRegressionState _uRegression;
    private double _uValue;
    private double _prevS;
    private bool _hasPrev;

    public TrigonometricOscillatorState(int length = 200, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sRegression = new LinearRegressionState(resolved, inputName);
        _uRegression = new LinearRegressionState(resolved, _ => _uValue);
    }

    public TrigonometricOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sRegression = new LinearRegressionState(resolved, selector);
        _uRegression = new LinearRegressionState(resolved, _ => _uValue);
    }

    public IndicatorName Name => IndicatorName.TrigonometricOscillator;

    public void Reset()
    {
        _sRegression.Reset();
        _uRegression.Reset();
        _uValue = 0;
        _prevS = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var s = _sRegression.Update(bar, isFinal, includeOutputs: false).Value;
        var prevS = _hasPrev ? _prevS : 0;
        var wa = Math.Asin(Math.Sign(s - prevS)) * 2;
        var wb = Math.Asin(Math.Sign(1)) * 2;
        var u = wa + (2 * Math.PI * Math.Round((wa - wb) / (2 * Math.PI)));
        _uValue = u;
        var uReg = _uRegression.Update(bar, isFinal, includeOutputs: false).Value;
        var o = Math.Atan(uReg);

        if (isFinal)
        {
            _prevS = s;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "To", o }
            };
        }

        return new StreamingIndicatorStateResult(o, outputs);
    }

    public void Dispose()
    {
        _sRegression.Dispose();
        _uRegression.Dispose();
    }
}

public sealed class TrimeanState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _order;

    public TrimeanState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrimeanState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

        var q1 = _order.PercentileNearestRank(25);
        var median = _order.PercentileNearestRank(50);
        var q3 = _order.PercentileNearestRank(75);
        var trimean = (q1 + (2 * median) + q3) / 4;

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

public sealed class TripleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _ema3;
    private readonly StreamingInputResolver _input;

    public TripleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TripleExponentialMovingAverageState(MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var tema = (3 * ema1) - (3 * ema2) + ema3;

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

public sealed class TStepLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _bSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StandardDeviationVolatilityState _bStdDev;
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private double _bValue;
    private double _prevB;
    private double _chgSum;
    private int _chgCount;
    private bool _hasPrev;

    public TStepLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, double sc = 0.5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ = sc;
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _bStdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _bValue);
        _corrWindow = new RollingWindowCorrelation(resolved);
        _er = new EfficiencyRatioState(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TStepLeastSquaresMovingAverageState(MovingAvgType maType, int length, double sc,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ = sc;
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _bStdDev = new StandardDeviationVolatilityState(maType, resolved, _ => _bValue);
        _corrWindow = new RollingWindowCorrelation(resolved);
        _er = new EfficiencyRatioState(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TStepLeastSquaresMovingAverage;

    public void Reset()
    {
        _smaSmoother.Reset();
        _bSmoother.Reset();
        _stdDev.Reset();
        _bStdDev.Reset();
        _corrWindow.Reset();
        _er.Reset();
        _bValue = 0;
        _prevB = 0;
        _chgSum = 0;
        _chgCount = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = 1 - _er.Next(value, isFinal);
        var prevB = _hasPrev ? _prevB : value;
        var chg = Math.Abs(value - prevB);
        var chgSum = _chgSum + chg;
        var chgCount = _chgCount + 1;
        var avgChg = chgCount > 0 ? chgSum / chgCount : 0;
        var a = avgChg * (1 + er);
        var b = value > prevB + a ? value : value < prevB - a ? value : prevB;

        var corr = isFinal ? _corrWindow.Add(b, value, out _) : _corrWindow.Preview(b, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var sma = _smaSmoother.Next(value, isFinal);
        var bSma = _bSmoother.Next(b, isFinal);
        _bValue = b;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var bStdDev = _bStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var alpha = bStdDev != 0 ? corr * stdDev / bStdDev : 0;
        var beta = sma - (alpha * bSma);
        var ls = (alpha * b) + beta;

        if (isFinal)
        {
            _prevB = b;
            _chgSum = chgSum;
            _chgCount = chgCount;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tslsma", ls }
            };
        }

        return new StreamingIndicatorStateResult(ls, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _bSmoother.Dispose();
        _stdDev.Dispose();
        _bStdDev.Dispose();
        _corrWindow.Dispose();
        _er.Dispose();
    }
}

public sealed class TTMScalperIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _closes;
    private readonly StreamingInputResolver _input;
    private double _prevBuySellSwitch;
    private double _prevSbs;
    private double _prevClrs;

    public TTMScalperIndicatorState(InputName inputName = InputName.Close)
    {
        _closes = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TTMScalperIndicatorState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _closes = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class TurboScalerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _sma2Smoother;
    private readonly RollingWindowMax _smoMaxWindow;
    private readonly RollingWindowMin _smoMinWindow;
    private readonly RollingWindowMax _smoSmaMaxWindow;
    private readonly RollingWindowMin _smoSmaMinWindow;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha;

    public TurboScalerState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50,
        double alpha = 0.5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _sma2Smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _smoMaxWindow = new RollingWindowMax(resolved);
        _smoMinWindow = new RollingWindowMin(resolved);
        _smoSmaMaxWindow = new RollingWindowMax(resolved);
        _smoSmaMinWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
        _alpha = alpha;
    }

    public TurboScalerState(MovingAvgType maType, int length, double alpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _sma2Smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _smoMaxWindow = new RollingWindowMax(resolved);
        _smoMinWindow = new RollingWindowMin(resolved);
        _smoSmaMaxWindow = new RollingWindowMax(resolved);
        _smoSmaMinWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _alpha = alpha;
    }

    public IndicatorName Name => IndicatorName.TurboScaler;

    public void Reset()
    {
        _smaSmoother.Reset();
        _sma2Smoother.Reset();
        _smoMaxWindow.Reset();
        _smoMinWindow.Reset();
        _smoSmaMaxWindow.Reset();
        _smoSmaMinWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smaSmoother.Next(value, isFinal);
        var sma2 = _sma2Smoother.Next(sma, isFinal);

        var smoSma = (_alpha * sma) + ((1 - _alpha) * sma2);
        var smo = (_alpha * value) + ((1 - _alpha) * sma);

        var smoSmaHighest = isFinal ? _smoSmaMaxWindow.Add(smoSma, out _) : _smoSmaMaxWindow.Preview(smoSma, out _);
        var smoSmaLowest = isFinal ? _smoSmaMinWindow.Add(smoSma, out _) : _smoSmaMinWindow.Preview(smoSma, out _);
        var smoHighest = isFinal ? _smoMaxWindow.Add(smo, out _) : _smoMaxWindow.Preview(smo, out _);
        var smoLowest = isFinal ? _smoMinWindow.Add(smo, out _) : _smoMinWindow.Preview(smo, out _);

        var a = smoHighest - smoLowest != 0 ? (value - smoLowest) / (smoHighest - smoLowest) : 0;
        var b = smoSmaHighest - smoSmaLowest != 0 ? (sma - smoSmaLowest) / (smoSmaHighest - smoSmaLowest) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ts", a },
                { "Trigger", b }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _sma2Smoother.Dispose();
        _smoMaxWindow.Dispose();
        _smoMinWindow.Dispose();
        _smoSmaMaxWindow.Dispose();
        _smoSmaMinWindow.Dispose();
    }
}

public sealed class TurboStochasticsFastState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly LinearRegressionState _fastKRegression;
    private readonly LinearRegressionState _fastDRegression;
    private readonly StreamingInputResolver _input;
    private double _fastKValue;
    private double _fastDValue;

    public TurboStochasticsFastState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;
        var regressionLength = Math.Max(1, length2 + turbo);
        _highWindow = new RollingWindowMax(resolvedLength1);
        _lowWindow = new RollingWindowMin(resolvedLength1);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _fastKRegression = new LinearRegressionState(regressionLength, _ => _fastKValue);
        _fastDRegression = new LinearRegressionState(regressionLength, _ => _fastDValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TurboStochasticsFastState(MovingAvgType maType, int length1, int length2, int turboLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;
        var regressionLength = Math.Max(1, length2 + turbo);
        _highWindow = new RollingWindowMax(resolvedLength1);
        _lowWindow = new RollingWindowMin(resolvedLength1);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _fastKRegression = new LinearRegressionState(regressionLength, _ => _fastKValue);
        _fastDRegression = new LinearRegressionState(regressionLength, _ => _fastDValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TurboStochasticsFast;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
        _fastKRegression.Reset();
        _fastDRegression.Reset();
        _fastKValue = 0;
        _fastDValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((close - lowestLow) / range * 100, 100, 0) : 0;
        var fastD = _fastSmoother.Next(fastK, isFinal);

        _fastKValue = fastK;
        _fastDValue = fastD;
        var tsfK = _fastKRegression.Update(bar, isFinal, includeOutputs: false).Value;
        var tsfD = _fastDRegression.Update(bar, isFinal, includeOutputs: false).Value;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tsf", tsfK },
                { "Signal", tsfD }
            };
        }

        return new StreamingIndicatorStateResult(tsfK, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
        _fastKRegression.Dispose();
        _fastDRegression.Dispose();
    }
}

public sealed class TurboStochasticsSlowState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _slowKSmoother;
    private readonly IMovingAverageSmoother _slowDSmoother;
    private readonly LinearRegressionState _slowKRegression;
    private readonly LinearRegressionState _slowDRegression;
    private readonly StreamingInputResolver _input;
    private double _slowKValue;
    private double _slowDValue;

    public TurboStochasticsSlowState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 10, int turboLength = 2, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;
        var regressionLength = Math.Max(1, length2 + turbo);
        _highWindow = new RollingWindowMax(resolvedLength1);
        _lowWindow = new RollingWindowMin(resolvedLength1);
        _slowKSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _slowDSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _slowKRegression = new LinearRegressionState(regressionLength, _ => _slowKValue);
        _slowDRegression = new LinearRegressionState(regressionLength, _ => _slowDValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TurboStochasticsSlowState(MovingAvgType maType, int length1, int length2, int turboLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var turbo = turboLength < 0 ? Math.Max(turboLength, length2 * -1) : turboLength > 0 ? Math.Min(turboLength, length2) : 0;
        var regressionLength = Math.Max(1, length2 + turbo);
        _highWindow = new RollingWindowMax(resolvedLength1);
        _lowWindow = new RollingWindowMin(resolvedLength1);
        _slowKSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _slowDSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _slowKRegression = new LinearRegressionState(regressionLength, _ => _slowKValue);
        _slowDRegression = new LinearRegressionState(regressionLength, _ => _slowDValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TurboStochasticsSlow;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _slowKSmoother.Reset();
        _slowDSmoother.Reset();
        _slowKRegression.Reset();
        _slowDRegression.Reset();
        _slowKValue = 0;
        _slowDValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((close - lowestLow) / range * 100, 100, 0) : 0;
        var slowK = _slowKSmoother.Next(fastK, isFinal);
        var slowD = _slowDSmoother.Next(slowK, isFinal);

        _slowKValue = slowK;
        _slowDValue = slowD;
        var tsfK = _slowKRegression.Update(bar, isFinal, includeOutputs: false).Value;
        var tsfD = _slowDRegression.Update(bar, isFinal, includeOutputs: false).Value;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tsf", tsfK },
                { "Signal", tsfD }
            };
        }

        return new StreamingIndicatorStateResult(tsfK, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _slowKSmoother.Dispose();
        _slowDSmoother.Dispose();
        _slowKRegression.Dispose();
        _slowDRegression.Dispose();
    }
}

public sealed class TurboTriggerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _closeSmoother;
    private readonly IMovingAverageSmoother _openSmoother;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly IMovingAverageSmoother _avgSmoother;
    private readonly IMovingAverageSmoother _hySmoother;
    private readonly IMovingAverageSmoother _ylSmoother;
    private readonly IMovingAverageSmoother _oscSmoother;
    private readonly StreamingInputResolver _input;

    public TurboTriggerState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        int smoothLength = 2, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _openSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _avgSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _hySmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _ylSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _oscSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TurboTriggerState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _openSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _avgSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _hySmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _ylSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _oscSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TurboTrigger;

    public void Reset()
    {
        _closeSmoother.Reset();
        _openSmoother.Reset();
        _highSmoother.Reset();
        _lowSmoother.Reset();
        _avgSmoother.Reset();
        _hySmoother.Reset();
        _ylSmoother.Reset();
        _oscSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _closeSmoother.Next(_input.GetValue(bar), isFinal);
        var open = _openSmoother.Next(bar.Open, isFinal);
        var high = _highSmoother.Next(bar.High, isFinal);
        var low = _lowSmoother.Next(bar.Low, isFinal);
        var avg = (close + open) / 2;
        var y = _avgSmoother.Next(avg, isFinal);
        var hy = high - y;
        var yl = y - low;
        var a = _hySmoother.Next(hy, isFinal);
        var b = _ylSmoother.Next(yl, isFinal);
        var osc = _oscSmoother.Next(a - b, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "BullLine", a },
                { "Trigger", osc }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _closeSmoother.Dispose();
        _openSmoother.Dispose();
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _avgSmoother.Dispose();
        _hySmoother.Dispose();
        _ylSmoother.Dispose();
        _oscSmoother.Dispose();
    }
}

public sealed class TwiggsMoneyFlowState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _adSmoother;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevPrice;
    private bool _hasPrev;

    public TwiggsMoneyFlowState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _adSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TwiggsMoneyFlowState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _adSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TwiggsMoneyFlow;

    public void Reset()
    {
        _adSmoother.Reset();
        _volumeSmoother.Reset();
        _prevPrice = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var volume = bar.Volume;
        var prevPrice = _hasPrev ? _prevPrice : 0;
        var trh = Math.Max(high, prevPrice);
        var trl = Math.Min(low, prevPrice);
        var ad = trh - trl != 0 && volume != 0 ? (price - trl - (trh - price)) / (trh - trl) * volume : 0;
        var smoothAd = _adSmoother.Next(ad, isFinal);
        var smoothVolume = _volumeSmoother.Next(volume, isFinal);
        var tmf = smoothVolume != 0 ? MathHelper.MinOrMax(smoothAd / smoothVolume, 1, -1) : 0;

        if (isFinal)
        {
            _prevPrice = price;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tmf", tmf }
            };
        }

        return new StreamingIndicatorStateResult(tmf, outputs);
    }

    public void Dispose()
    {
        _adSmoother.Dispose();
        _volumeSmoother.Dispose();
    }
}

public sealed class UberTrendIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _decSum;
    private readonly RollingWindowSum _advVolSum;
    private readonly RollingWindowSum _decVolSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public UberTrendIndicatorState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UberTrendIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UberTrendIndicator;

    public void Reset()
    {
        _advSum.Reset();
        _decSum.Reset();
        _advVolSum.Reset();
        _decVolSum.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var volume = bar.Volume;
        var adv = _hasPrev && value > prevValue ? value - prevValue : 0;
        var dec = _hasPrev && value < prevValue ? prevValue - value : 0;

        var advSum = isFinal ? _advSum.Add(adv, out _) : _advSum.Preview(adv, out _);
        var decSum = isFinal ? _decSum.Add(dec, out _) : _decSum.Preview(dec, out _);
        var advVol = _hasPrev && value > prevValue && advSum != 0 ? volume / advSum : 0;
        var decVol = _hasPrev && value < prevValue && decSum != 0 ? volume / decSum : 0;
        var advVolSum = isFinal ? _advVolSum.Add(advVol, out _) : _advVolSum.Preview(advVol, out _);
        var decVolSum = isFinal ? _decVolSum.Add(decVol, out _) : _decVolSum.Preview(decVol, out _);
        var top = decSum != 0 ? advSum / decSum : 0;
        var bot = decVolSum != 0 ? advVolSum / decVolSum : 0;
        var ut = bot != 0 ? top / bot : 0;
        var uti = ut + 1 != 0 ? (ut - 1) / (ut + 1) : 0;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Uti", uti }
            };
        }

        return new StreamingIndicatorStateResult(uti, outputs);
    }

    public void Dispose()
    {
        _advSum.Dispose();
        _decSum.Dispose();
        _advVolSum.Dispose();
        _decVolSum.Dispose();
    }
}

public sealed class UhlMaCrossoverSystemState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _stdDevValues;
    private readonly StreamingInputResolver _input;
    private double _prevCma;
    private double _prevCts;
    private bool _hasPrev;

    public UhlMaCrossoverSystemState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _stdDevValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UhlMaCrossoverSystemState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _stdDevValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevVar = EhlersStreamingWindow.GetOffsetValue(_stdDevValues, _length);
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

public sealed class UltimateMomentumIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly McClellanOscillatorState _mo;
    private readonly BollingerBandsPercentBState _bbPct;
    private readonly MoneyFlowIndexState _mfi1;
    private readonly MoneyFlowIndexState _mfi2;
    private readonly MoneyFlowIndexState _mfi3;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _utmSmoother;

    public UltimateMomentumIndicatorState(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 13, int length2 = 19,
        int length3 = 21, int length4 = 39, int length5 = 50, int length6 = 200, double stdDevMult = 1.5)
    {
        _ = length6;
        _mo = new McClellanOscillatorState(maType, length2, length4, 9, 1000);
        _bbPct = new BollingerBandsPercentBState(stdDevMult, maType, length5);
        _mfi1 = new MoneyFlowIndexState(length2, inputName);
        _mfi2 = new MoneyFlowIndexState(length3, inputName);
        _mfi3 = new MoneyFlowIndexState(length4, inputName);
        _rsi = new RsiState(maType, Math.Max(1, length1));
        _utmSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
    }

    public UltimateMomentumIndicatorState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int length6, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = length6;
        _mo = new McClellanOscillatorState(maType, length2, length4, 9, 1000, selector);
        _bbPct = new BollingerBandsPercentBState(stdDevMult, maType, length5, selector);
        _mfi1 = new MoneyFlowIndexState(length2, selector);
        _mfi2 = new MoneyFlowIndexState(length3, selector);
        _mfi3 = new MoneyFlowIndexState(length4, selector);
        _rsi = new RsiState(maType, Math.Max(1, length1));
        _utmSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
    }

    public IndicatorName Name => IndicatorName.UltimateMomentumIndicator;

    public void Reset()
    {
        _mo.Reset();
        _bbPct.Reset();
        _mfi1.Reset();
        _mfi2.Reset();
        _mfi3.Reset();
        _rsi.Reset();
        _utmSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var moResult = _mo.Update(bar, isFinal, includeOutputs: true);
        var moOutputs = moResult.Outputs!;
        var advSum = moOutputs["AdvSum"];
        var decSum = moOutputs["DecSum"];
        var mo = moResult.Value;
        var bbPct = _bbPct.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi1 = _mfi1.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi2 = _mfi2.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi3 = _mfi3.Update(bar, isFinal, includeOutputs: false).Value;
        var ratio = decSum != 0 ? advSum / decSum : 0;
        var utm = (200 * bbPct) + (100 * ratio) + (2 * mo) + (1.5 * mfi3) + (3 * mfi2) + (3 * mfi1);
        var utmRsi = _rsi.Next(utm, isFinal);
        var utmi = _utmSmoother.Next(utmRsi, isFinal);

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
        _rsi.Dispose();
        _utmSmoother.Dispose();
    }
}

public sealed class UltimateMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly double _acc;
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly RollingCumulativeSum _posFlowSum;
    private readonly RollingCumulativeSum _negFlowSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevTypical;
    private double _prevLength;
    private bool _hasPrev;

    public UltimateMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5,
        int maxLength = 50, double acc = 1, InputName inputName = InputName.Close)
    {
        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(_minLength, maxLength);
        _acc = acc;
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, _maxLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _maxLength, inputName);
        _posFlowSum = new RollingCumulativeSum();
        _negFlowSum = new RollingCumulativeSum();
        _values = new PooledRingBuffer<double>(_maxLength);
        _input = new StreamingInputResolver(inputName, null);
        _prevLength = _maxLength;
    }

    public UltimateMovingAverageState(MovingAvgType maType, int minLength, int maxLength, double acc,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(_minLength, maxLength);
        _acc = acc;
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, _maxLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _maxLength, selector);
        _posFlowSum = new RollingCumulativeSum();
        _negFlowSum = new RollingCumulativeSum();
        _values = new PooledRingBuffer<double>(_maxLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _prevLength = _maxLength;
    }

    public IndicatorName Name => IndicatorName.UltimateMovingAverage;

    public void Reset()
    {
        _smaSmoother.Reset();
        _stdDev.Reset();
        _posFlowSum.Reset();
        _negFlowSum.Reset();
        _values.Clear();
        _prevTypical = 0;
        _prevLength = _maxLength;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smaSmoother.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var a = sma - (1.75 * stdDev);
        var b = sma - (0.25 * stdDev);
        var c = sma + (0.25 * stdDev);
        var d = sma + (1.75 * stdDev);
        var prevLength = _hasPrev ? _prevLength : _maxLength;
        var length = MathHelper.MinOrMax(value >= b && value <= c ? prevLength + 1 : value < a || value > d ? prevLength - 1 : prevLength,
            _maxLength, _minLength);
        var len = Math.Max(1, (int)length);
        var typical = (bar.High + bar.Low + bar.Close) / 3d;
        var rawFlow = typical * bar.Volume;
        var posFlow = _hasPrev && typical > _prevTypical ? rawFlow : 0;
        var negFlow = _hasPrev && typical < _prevTypical ? rawFlow : 0;
        var posTotal = isFinal ? _posFlowSum.Add(posFlow, len) : _posFlowSum.Preview(posFlow, len);
        var negTotal = isFinal ? _negFlowSum.Add(negFlow, len) : _negFlowSum.Preview(negFlow, len);
        var mfiRatio = negTotal != 0 ? posTotal / negTotal : 0;
        var mfi = negTotal == 0 ? 100 : posTotal == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + mfiRatio)), 100, 0);
        var mfScaled = (mfi * 2) - 100;
        var p = _acc + (Math.Abs(mfScaled) / 25);
        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= len - 1; j++)
        {
            var weight = MathHelper.Pow(len - j, p);
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var uma = weightedSum != 0 ? sum / weightedSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevTypical = typical;
            _prevLength = length;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Uma", uma }
            };
        }

        return new StreamingIndicatorStateResult(uma, outputs);
    }

    public void Dispose()
    {
        _smaSmoother.Dispose();
        _stdDev.Dispose();
        _values.Dispose();
    }
}

public sealed class UltimateMovingAverageBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly UltimateMovingAverageState _uma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly double _stdDevMult;

    public UltimateMovingAverageBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5,
        int maxLength = 50, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        _uma = new UltimateMovingAverageState(maType, minLength, maxLength, 1, inputName);
        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, minLength), inputName);
        _stdDevMult = stdDevMult;
    }

    public UltimateMovingAverageBandsState(MovingAvgType maType, int minLength, int maxLength, double stdDevMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _uma = new UltimateMovingAverageState(maType, minLength, maxLength, 1, selector);
        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, minLength), selector);
        _stdDevMult = stdDevMult;
    }

    public IndicatorName Name => IndicatorName.UltimateMovingAverageBands;

    public void Reset()
    {
        _uma.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var uma = _uma.Update(bar, isFinal, includeOutputs: false).Value;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = uma + (_stdDevMult * stdDev);
        var lower = uma - (_stdDevMult * stdDev);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", uma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(uma, outputs);
    }

    public void Dispose()
    {
        _uma.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class UltimateOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _bpSum1;
    private readonly RollingWindowSum _bpSum2;
    private readonly RollingWindowSum _bpSum3;
    private readonly RollingWindowSum _trSum1;
    private readonly RollingWindowSum _trSum2;
    private readonly RollingWindowSum _trSum3;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public UltimateOscillatorState(int length1 = 7, int length2 = 14, int length3 = 28, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _bpSum1 = new RollingWindowSum(resolved1);
        _bpSum2 = new RollingWindowSum(resolved2);
        _bpSum3 = new RollingWindowSum(resolved3);
        _trSum1 = new RollingWindowSum(resolved1);
        _trSum2 = new RollingWindowSum(resolved2);
        _trSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UltimateOscillatorState(int length1, int length2, int length3, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _bpSum1 = new RollingWindowSum(resolved1);
        _bpSum2 = new RollingWindowSum(resolved2);
        _bpSum3 = new RollingWindowSum(resolved3);
        _trSum1 = new RollingWindowSum(resolved1);
        _trSum2 = new RollingWindowSum(resolved2);
        _trSum3 = new RollingWindowSum(resolved3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UltimateOscillator;

    public void Reset()
    {
        _bpSum1.Reset();
        _bpSum2.Reset();
        _bpSum3.Reset();
        _trSum1.Reset();
        _trSum2.Reset();
        _trSum3.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var prevClose = _hasPrev ? _prevClose : 0;
        var minValue = Math.Min(low, prevClose);
        var maxValue = Math.Max(high, prevClose);
        var bp = close - minValue;
        var tr = maxValue - minValue;

        var bpSum1 = isFinal ? _bpSum1.Add(bp, out _) : _bpSum1.Preview(bp, out _);
        var bpSum2 = isFinal ? _bpSum2.Add(bp, out _) : _bpSum2.Preview(bp, out _);
        var bpSum3 = isFinal ? _bpSum3.Add(bp, out _) : _bpSum3.Preview(bp, out _);
        var trSum1 = isFinal ? _trSum1.Add(tr, out _) : _trSum1.Preview(tr, out _);
        var trSum2 = isFinal ? _trSum2.Add(tr, out _) : _trSum2.Preview(tr, out _);
        var trSum3 = isFinal ? _trSum3.Add(tr, out _) : _trSum3.Preview(tr, out _);
        var avg1 = trSum1 != 0 ? bpSum1 / trSum1 : 0;
        var avg2 = trSum2 != 0 ? bpSum2 / trSum2 : 0;
        var avg3 = trSum3 != 0 ? bpSum3 / trSum3 : 0;
        var uo = MathHelper.MinOrMax(100 * (((4 * avg1) + (2 * avg2) + avg3) / 7), 100, 0);

        if (isFinal)
        {
            _prevClose = close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Uo", uo }
            };
        }

        return new StreamingIndicatorStateResult(uo, outputs);
    }

    public void Dispose()
    {
        _bpSum1.Dispose();
        _bpSum2.Dispose();
        _bpSum3.Dispose();
        _trSum1.Dispose();
        _trSum2.Dispose();
        _trSum3.Dispose();
    }
}

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
        int lbLength = 5, int smoothLength = 4, int rangeLength = 2, InputName inputName = InputName.Close)
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
        _input = new StreamingInputResolver(inputName, null);
    }

    public UltimateTraderOscillatorState(MovingAvgType maType, int length, int lbLength, int smoothLength, int rangeLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

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
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var tr = CalculationsHelper.CalculateTrueRange(high, low, prevClose);
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
        var dx = bearScore != 0 ? bullScore / bearScore : 0;
        var dxi = (2 * (100 - (100 / (1 + dx)))) - 100;
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
