using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class OnBalanceVolumeModifiedState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _obvSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private double _prevObv;
    private bool _hasPrev;

    public OnBalanceVolumeModifiedState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 7,
        int length2 = 10, InputName inputName = InputName.Close)
    {
        _obvSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public OnBalanceVolumeModifiedState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _obvSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolumeModified;

    public void Reset()
    {
        _obvSmoother.Reset();
        _signalSmoother.Reset();
        _prevClose = 0;
        _prevObv = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevObv = _hasPrev ? _prevObv : 0;
        var obv = value > prevClose ? prevObv + bar.Volume
            : value < prevClose ? prevObv - bar.Volume
            : prevObv;
        var obvm = _obvSmoother.Next(obv, isFinal);
        var signal = _signalSmoother.Next(obvm, isFinal);

        if (isFinal)
        {
            _prevClose = value;
            _prevObv = obv;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Obvm", obvm },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(obvm, outputs);
    }

    public void Dispose()
    {
        _obvSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class OnBalanceVolumeReflexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevOvr;
    private bool _hasPrev;

    public OnBalanceVolumeReflexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 4,
        int signalLength = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OnBalanceVolumeReflexState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolumeReflex;

    public void Reset()
    {
        _signalSmoother.Reset();
        _values.Clear();
        _prevOvr = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var prevOvr = _hasPrev ? _prevOvr : 0;
        var ovr = value > prevValue ? prevOvr + bar.Volume
            : value < prevValue ? prevOvr - bar.Volume
            : prevOvr;
        var signal = _signalSmoother.Next(ovr, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevOvr = ovr;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Obvr", ovr },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ovr, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class OptimalWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevOwma;
    private bool _hasPrev;

    public OptimalWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OptimalWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OptimalWeightedMovingAverage;

    public void Reset()
    {
        _corrWindow.Reset();
        _values.Clear();
        _prevOwma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevOwma = _hasPrev ? _prevOwma : 0;
        var corr = isFinal ? _corrWindow.Add(value, prevOwma, out _) : _corrWindow.Preview(value, prevOwma, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, corr);
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);

            sum += prevValue * weight;
            weightedSum += weight;
        }

        var owma = weightedSum != 0 ? sum / weightedSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevOwma = owma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Owma", owma }
            };
        }

        return new StreamingIndicatorStateResult(owma, outputs);
    }

    public void Dispose()
    {
        _corrWindow.Dispose();
        _values.Dispose();
    }
}

public sealed class OptimizedTrendTrackerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ma;
    private readonly StreamingInputResolver _input;
    private readonly double _percent;
    private double _prevLongStop;
    private double _prevShortStop;
    private bool _hasPrev;

    public OptimizedTrendTrackerState(MovingAvgType maType = MovingAvgType.VariableIndexDynamicAverage, int length = 2,
        double percent = 1.4, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ma = maType == MovingAvgType.VariableIndexDynamicAverage
            ? new VariableIndexDynamicAverageEngine(resolved)
            : MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _percent = percent;
    }

    public OptimizedTrendTrackerState(MovingAvgType maType, int length, double percent, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ma = maType == MovingAvgType.VariableIndexDynamicAverage
            ? new VariableIndexDynamicAverageEngine(resolved)
            : MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _percent = percent;
    }

    public IndicatorName Name => IndicatorName.OptimizedTrendTracker;

    public void Reset()
    {
        _ma.Reset();
        _prevLongStop = 0;
        _prevShortStop = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _ma.Next(value, isFinal);
        var fark = ma * _percent * 0.01;

        var prevLongStop = _hasPrev ? _prevLongStop : 0;
        var longStop = ma - fark;
        longStop = ma > prevLongStop ? Math.Max(longStop, prevLongStop) : longStop;

        var prevShortStop = _hasPrev ? _prevShortStop : 0;
        var shortStop = ma + fark;

        var mt = ma > prevShortStop ? longStop : ma < prevLongStop ? shortStop : 0;
        var ott = ma > mt ? mt * (200 + _percent) / 200 : mt * (200 - _percent) / 200;

        if (isFinal)
        {
            _prevLongStop = longStop;
            _prevShortStop = shortStop;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ott", ott }
            };
        }

        return new StreamingIndicatorStateResult(ott, outputs);
    }

    public void Dispose()
    {
        _ma.Dispose();
    }
}

public sealed class OscarIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevOscar;
    private bool _hasPrev;

    public OscarIndicatorState(int length = 8, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OscarIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OscarIndicator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevOscar = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var rough = range != 0 ? MathHelper.MinOrMax((value - lowest) / range * 100, 100, 0) : 0;
        var prevOscar = _hasPrev ? _prevOscar : 0;
        var oscar = (prevOscar / 6) + (rough / 3);

        if (isFinal)
        {
            _prevOscar = oscar;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Oscar", oscar }
            };
        }

        return new StreamingIndicatorStateResult(oscar, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class OscOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public OscOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 7, int slowLength = 14,
        InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public OscOscillatorState(MovingAvgType maType, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OscOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var osc = slow - fast;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "OscOscillator", osc }
            };
        }

        return new StreamingIndicatorStateResult(osc, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class OvershootReductionMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _length1;
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly RollingWindowSum _bSum;
    private readonly RollingWindowMax _bSmaMax;
    private readonly IMovingAverageSmoother _indexSmoother;
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StandardDeviationVolatilityState _indexStdDev;
    private readonly StreamingInputResolver _input;
    private double _indexValue;
    private double _prevValue;
    private double _prevD;
    private int _index;
    private bool _hasPrev;

    public OvershootReductionMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _length1 = (int)Math.Ceiling((double)_length / 2);
        _corrWindow = new RollingWindowCorrelation(_length);
        _bSum = new RollingWindowSum(_length1);
        _bSmaMax = new RollingWindowMax(_length);
        _indexSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OvershootReductionMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _length1 = (int)Math.Ceiling((double)_length / 2);
        _corrWindow = new RollingWindowCorrelation(_length);
        _bSum = new RollingWindowSum(_length1);
        _bSmaMax = new RollingWindowMax(_length);
        _indexSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OvershootReductionMovingAverage;

    public void Reset()
    {
        _corrWindow.Reset();
        _bSum.Reset();
        _bSmaMax.Reset();
        _indexSmoother.Reset();
        _sma.Reset();
        _stdDev.Reset();
        _indexStdDev.Reset();
        _indexValue = 0;
        _prevValue = 0;
        _prevD = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_index;
        _indexValue = index;

        var corr = isFinal ? _corrWindow.Add(index, value, out _) : _corrWindow.Preview(index, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var indexSma = _indexSmoother.Next(index, isFinal);
        var sma = _sma.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var indexStdDev = _indexStdDev.Update(bar, isFinal, includeOutputs: false).Value;

        var prevValue = _hasPrev ? _prevValue : 0;
        var prevD = _hasPrev ? (_prevD != 0 ? _prevD : prevValue) : prevValue;
        var a = indexStdDev != 0 && corr != 0 ? (index - indexSma) / indexStdDev * corr : 0;

        var b = Math.Abs(prevD - value);
        int bCount;
        var bSum = isFinal ? _bSum.Add(b, out bCount) : _bSum.Preview(b, out bCount);
        var bSma = bCount > 0 ? bSum / bCount : 0;
        var highest = isFinal ? _bSmaMax.Add(bSma, out _) : _bSmaMax.Preview(bSma, out _);
        var c = highest != 0 ? b / highest : 0;

        var d = sma + (a * (stdDev * c));

        if (isFinal)
        {
            _prevValue = value;
            _prevD = d;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Orma", d }
            };
        }

        return new StreamingIndicatorStateResult(d, outputs);
    }

    public void Dispose()
    {
        _corrWindow.Dispose();
        _bSum.Dispose();
        _bSmaMax.Dispose();
        _indexSmoother.Dispose();
        _sma.Dispose();
        _stdDev.Dispose();
        _indexStdDev.Dispose();
    }
}

public sealed class ParabolicSARState : IStreamingIndicatorState, IDisposable
{
    private readonly double _start;
    private readonly double _increment;
    private readonly double _maximum;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private double _prevValue;
    private int _index;
    private bool _hasPrev;

    public ParabolicSARState(double start = 0.02, double increment = 0.02, double maximum = 0.2,
        InputName inputName = InputName.Close)
    {
        _start = start;
        _increment = increment;
        _maximum = maximum;
        _input = new StreamingInputResolver(inputName, null);
        _highValues = new PooledRingBuffer<double>(3);
        _lowValues = new PooledRingBuffer<double>(3);
    }

    public ParabolicSARState(double start, double increment, double maximum, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _start = start;
        _increment = increment;
        _maximum = maximum;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _highValues = new PooledRingBuffer<double>(3);
        _lowValues = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.ParabolicSAR;

    public void Reset()
    {
        _highValues.Clear();
        _lowValues.Clear();
        _prevValue = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var prevHigh1 = EhlersStreamingWindow.GetOffsetValue(_highValues, currentHigh, 1);
        var prevLow1 = EhlersStreamingWindow.GetOffsetValue(_lowValues, currentLow, 1);
        var prevHigh2 = EhlersStreamingWindow.GetOffsetValue(_highValues, currentHigh, 2);
        var prevLow2 = EhlersStreamingWindow.GetOffsetValue(_lowValues, currentLow, 2);

        bool uptrend;
        double ep;
        double prevSar;
        double prevEp;
        double sar;
        double af = _start;
        if (value > prevValue)
        {
            uptrend = true;
            ep = currentHigh;
            prevSar = prevLow1;
            prevEp = currentHigh;
        }
        else
        {
            uptrend = false;
            ep = currentLow;
            prevSar = prevHigh1;
            prevEp = currentLow;
        }
        sar = prevSar + (_start * (prevEp - prevSar));

        if (uptrend)
        {
            if (sar > currentLow)
            {
                uptrend = false;
                sar = Math.Max(ep, currentHigh);
                ep = currentLow;
                af = _start;
            }
        }
        else
        {
            if (sar < currentHigh)
            {
                uptrend = true;
                sar = Math.Min(ep, currentLow);
                ep = currentHigh;
                af = _start;
            }
        }

        if (uptrend)
        {
            if (currentHigh > ep)
            {
                ep = currentHigh;
                af = Math.Min(af + _increment, _maximum);
            }
        }
        else
        {
            if (currentLow < ep)
            {
                ep = currentLow;
                af = Math.Min(af + _increment, _maximum);
            }
        }

        if (uptrend)
        {
            sar = _index > 1 ? Math.Min(sar, prevLow2) : Math.Min(sar, prevLow1);
        }
        else
        {
            sar = _index > 1 ? Math.Max(sar, prevHigh2) : Math.Max(sar, prevHigh1);
        }

        var nextSar = sar + (af * (ep - sar));

        if (isFinal)
        {
            _highValues.TryAdd(currentHigh, out _);
            _lowValues.TryAdd(currentLow, out _);
            _prevValue = value;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sar", nextSar }
            };
        }

        return new StreamingIndicatorStateResult(nextSar, outputs);
    }

    public void Dispose()
    {
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class ParabolicWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public ParabolicWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, 2);
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ParabolicWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, 2);
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ParabolicWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * _weights[j];
        }

        var pwma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pwma", pwma }
            };
        }

        return new StreamingIndicatorStateResult(pwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class ParametricCorrectiveLinearMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly double _per;
    private readonly RollingWindowSum _w1Sum;
    private readonly RollingWindowSum _w2Sum;
    private readonly RollingWindowSum _vw1Sum;
    private readonly RollingWindowSum _vw2Sum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public ParametricCorrectiveLinearMovingAverageState(int length = 50, double alpha = 1, double per = 35,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _alpha = alpha;
        _per = per;
        _w1Sum = new RollingWindowSum(_length);
        _w2Sum = new RollingWindowSum(_length);
        _vw1Sum = new RollingWindowSum(_length);
        _vw2Sum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ParametricCorrectiveLinearMovingAverageState(int length, double alpha, double per, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _alpha = alpha;
        _per = per;
        _w1Sum = new RollingWindowSum(_length);
        _w2Sum = new RollingWindowSum(_length);
        _vw1Sum = new RollingWindowSum(_length);
        _vw2Sum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ParametricCorrectiveLinearMovingAverage;

    public void Reset()
    {
        _w1Sum.Reset();
        _w2Sum.Reset();
        _vw1Sum.Reset();
        _vw2Sum.Reset();
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var p1 = _index + 1 - ((_per / 100) * _length);
        var p2 = _index + 1 - (((100 - _per) / 100) * _length);

        var w1 = p1 >= 0 ? p1 : _alpha * p1;
        var w2 = p2 >= 0 ? p2 : _alpha * p2;
        var vw1 = prevValue * w1;
        var vw2 = prevValue * w2;

        var wSum1 = isFinal ? _w1Sum.Add(w1, out _) : _w1Sum.Preview(w1, out _);
        var wSum2 = isFinal ? _w2Sum.Add(w2, out _) : _w2Sum.Preview(w2, out _);
        var sum1 = isFinal ? _vw1Sum.Add(vw1, out _) : _vw1Sum.Preview(vw1, out _);
        var sum2 = isFinal ? _vw2Sum.Add(vw2, out _) : _vw2Sum.Preview(vw2, out _);

        var rrma1 = wSum1 != 0 ? sum1 / wSum1 : 0;
        var rrma2 = wSum2 != 0 ? sum2 / wSum2 : 0;
        _ = rrma2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pclma", rrma1 }
            };
        }

        return new StreamingIndicatorStateResult(rrma1, outputs);
    }

    public void Dispose()
    {
        _w1Sum.Dispose();
        _w2Sum.Dispose();
        _vw1Sum.Dispose();
        _vw2Sum.Dispose();
        _values.Dispose();
    }
}

public sealed class ParametricKalmanFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _estValues;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevErr;
    private double _prevEst;
    private int _index;
    private bool _hasPrev;

    public ParametricKalmanFilterState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _estValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ParametricKalmanFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _estValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ParametricKalmanFilter;

    public void Reset()
    {
        _estValues.Clear();
        _prevValue = 0;
        _prevErr = 0;
        _prevEst = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priorEst = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_estValues, 0, _length) : prevValue;
        var errMea = Math.Abs(priorEst - value);
        var errPrv = Math.Abs(_hasPrev ? (value - prevValue) * -1 : 0);
        var prevErr = _hasPrev ? _prevErr : errPrv;
        var kg = prevErr != 0 ? prevErr / (prevErr + errMea) : 0;
        var prevEst = _hasPrev ? _prevEst : prevValue;
        var est = prevEst + (kg * (value - prevEst));
        var err = (1 - kg) * errPrv;

        if (isFinal)
        {
            _estValues.TryAdd(est, out _);
            _prevValue = value;
            _prevErr = err;
            _prevEst = est;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pkf", est }
            };
        }

        return new StreamingIndicatorStateResult(est, outputs);
    }

    public void Dispose()
    {
        _estValues.Dispose();
    }
}

public sealed class PeakValleyEstimationState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly LinearRegressionState _regression;
    private readonly RollingWindowMax _highestWindow;
    private readonly StreamingInputResolver _input;
    private double _absOs;
    private double _prevH;
    private bool _hasPrev;

    public PeakValleyEstimationState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500,
        int smoothLength = 100, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _regression = new LinearRegressionState(Math.Max(1, smoothLength), _ => _absOs);
        _highestWindow = new RollingWindowMax(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PeakValleyEstimationState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _regression = new LinearRegressionState(Math.Max(1, smoothLength), _ => _absOs);
        _highestWindow = new RollingWindowMax(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PeakValleyEstimation;

    public void Reset()
    {
        _sma.Reset();
        _regression.Reset();
        _highestWindow.Reset();
        _absOs = 0;
        _prevH = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var os = value - sma;
        _absOs = Math.Abs(os);
        var p = _regression.Update(bar, isFinal, includeOutputs: false).Value;
        var highest = isFinal ? _highestWindow.Add(p, out _) : _highestWindow.Preview(p, out _);

        var prevH = _hasPrev ? _prevH : 0;
        var h = highest != 0 ? p / highest : 0;

        double mod1 = h == 1 && prevH != 1 ? 1 : 0;
        double mod2 = h < 0.8 ? 1 : 0;
        double mod3 = prevH == 1 && h < prevH ? 1 : 0;

        double sign1 = mod1 == 1 && os < 0 ? 1 : mod1 == 1 && os > 0 ? -1 : 0;
        double sign2 = mod2 == 1 && os < 0 ? 1 : mod2 == 1 && os > 0 ? -1 : 0;
        double sign3 = mod3 == 1 && os < 0 ? 1 : mod3 == 1 && os > 0 ? -1 : 0;

        if (isFinal)
        {
            _prevH = h;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Sign1", sign1 },
                { "Sign2", sign2 },
                { "Sign3", sign3 }
            };
        }

        return new StreamingIndicatorStateResult(sign1, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _regression.Dispose();
        _highestWindow.Dispose();
    }
}

public sealed class PentupleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _ema3;
    private readonly IMovingAverageSmoother _ema4;
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema6;
    private readonly IMovingAverageSmoother _ema7;
    private readonly IMovingAverageSmoother _ema8;
    private readonly StreamingInputResolver _input;

    public PentupleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema4 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema6 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema7 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema8 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PentupleExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema4 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema6 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema7 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema8 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PentupleExponentialMovingAverage;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
        _ema4.Reset();
        _ema5.Reset();
        _ema6.Reset();
        _ema7.Reset();
        _ema8.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var ema3 = _ema3.Next(ema2, isFinal);
        var ema4 = _ema4.Next(ema3, isFinal);
        var ema5 = _ema5.Next(ema4, isFinal);
        var ema6 = _ema6.Next(ema5, isFinal);
        var ema7 = _ema7.Next(ema6, isFinal);
        var ema8 = _ema8.Next(ema7, isFinal);
        var pema = (8 * ema1) - (28 * ema2) + (56 * ema3) - (70 * ema4) + (56 * ema5) - (28 * ema6) + (8 * ema7) - ema8;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pema", pema }
            };
        }

        return new StreamingIndicatorStateResult(pema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _ema3.Dispose();
        _ema4.Dispose();
        _ema5.Dispose();
        _ema6.Dispose();
        _ema7.Dispose();
        _ema8.Dispose();
    }
}

public sealed class PercentagePriceOscillatorLeaderState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _diffFastSmoother;
    private readonly IMovingAverageSmoother _diffSlowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public PercentagePriceOscillatorLeaderState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12,
        int slowLength = 26, int signalLength = 9, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _diffFastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _diffSlowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PercentagePriceOscillatorLeaderState(MovingAvgType maType, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _diffFastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _diffSlowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PercentagePriceOscillatorLeader;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _diffFastSmoother.Reset();
        _diffSlowSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var emaFast = _fastSmoother.Next(value, isFinal);
        var emaSlow = _slowSmoother.Next(value, isFinal);
        var diffFast = value - emaFast;
        var diffSlow = value - emaSlow;
        var diffFastMa = _diffFastSmoother.Next(diffFast, isFinal);
        var diffSlowMa = _diffSlowSmoother.Next(diffSlow, isFinal);
        var i1 = emaFast + diffFastMa;
        var i2 = emaSlow + diffSlowMa;
        var macd = i1 - i2;
        var ppo = i2 != 0 ? macd / i2 * 100 : 0;
        var signal = _signalSmoother.Next(ppo, isFinal);
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
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _diffFastSmoother.Dispose();
        _diffSlowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class PercentageTrailingStopsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private readonly double _pct;
    private double _prevHighest;
    private double _prevLowest;
    private double _prevStopS;
    private double _prevStopL;
    private bool _hasPrev;

    public PercentageTrailingStopsState(int length = 100, double pct = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
        _pct = pct;
    }

    public PercentageTrailingStopsState(int length, double pct, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _pct = pct;
    }

    public IndicatorName Name => IndicatorName.PercentageTrailingStops;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevHighest = 0;
        _prevLowest = 0;
        _prevStopS = 0;
        _prevStopL = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = _input.GetValue(bar);
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var prevHH = _hasPrev ? _prevHighest : currentClose;
        var prevLL = _hasPrev ? _prevLowest : currentClose;
        var prevStopS = _hasPrev ? _prevStopS : currentClose;
        var prevStopL = _hasPrev ? _prevStopL : currentClose;

        var stopL = currentHigh > prevHH ? currentHigh - (_pct * currentHigh) : prevStopL;
        var stopS = currentLow < prevLL ? currentLow + (_pct * currentLow) : prevStopS;

        var highest = isFinal ? _highWindow.Add(currentHigh, out _) : _highWindow.Preview(currentHigh, out _);
        var lowest = isFinal ? _lowWindow.Add(currentLow, out _) : _lowWindow.Preview(currentLow, out _);

        if (isFinal)
        {
            _prevHighest = highest;
            _prevLowest = lowest;
            _prevStopS = stopS;
            _prevStopL = stopL;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "LongStop", stopL },
                { "ShortStop", stopS }
            };
        }

        return new StreamingIndicatorStateResult(stopL, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class PercentageTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _pct;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public PercentageTrendState(int length = 20, double pct = 0.15, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _pct = pct;
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PercentageTrendState(int length, double pct, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _pct = pct;
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PercentageTrend;

    public void Reset()
    {
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = 0;
        var trend = value;

        for (var j = 1; j <= _length; j++)
        {
            var prevC = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            var currC = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            period = (prevC <= trend && currC > trend) || (prevC >= trend && currC < trend) ? 0 : period;

            double highest1 = currC;
            double lowest1 = currC;
            for (var k = j - period; k <= j; k++)
            {
                var c = EhlersStreamingWindow.GetOffsetValue(_values, value, j - k);
                highest1 = Math.Max(highest1, c);
                lowest1 = Math.Min(lowest1, c);
            }

            double highest2 = currC;
            double lowest2 = currC;
            for (var k = _index - _length; k <= j; k++)
            {
                var c = EhlersStreamingWindow.GetOffsetValue(_values, value, j - k);
                highest2 = Math.Max(highest2, c);
                lowest2 = Math.Min(lowest2, c);
            }

            if (period < _length)
            {
                period += 1;
                trend = currC > trend ? highest1 * (1 - _pct) : lowest1 * (1 + _pct);
            }
            else
            {
                trend = currC > trend ? highest2 * (1 - _pct) : lowest2 * (1 + _pct);
            }
        }

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pti", trend }
            };
        }

        return new StreamingIndicatorStateResult(trend, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class PercentChangeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevPcc;
    private bool _hasPrev;

    public PercentChangeOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PercentChangeOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PercentChangeOscillator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevValue = 0;
        _prevPcc = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevPcc = _hasPrev ? _prevPcc : 0;
        var pcc = prevValue - 1 != 0 ? prevPcc + (value / (prevValue - 1)) : 0;
        var signal = _signalSmoother.Next(pcc, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevPcc = pcc;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pcco", pcc },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pcc, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class PerformanceIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public PerformanceIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PerformanceIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PerformanceIndex;

    public void Reset()
    {
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var diff = _index >= _length ? value - priorValue : 0;
        var kpi = priorValue != 0 ? diff * 100 / priorValue : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Math.PI", kpi }
            };
        }

        return new StreamingIndicatorStateResult(kpi, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class PhaseChangeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public PhaseChangeIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 35,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PhaseChangeIndexState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PhaseChangeIndex;

    public void Reset()
    {
        _signalSmoother.Reset();
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var mom = _index >= _length ? value - prevValue : 0;

        double positiveSum = 0;
        double negativeSum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, _length - j);
            var gradient = prevValue + (mom * (_length - j) / (_length - 1));
            var deviation = prevValue2 - gradient;
            positiveSum = deviation > 0 ? positiveSum + deviation : positiveSum;
            negativeSum = deviation < 0 ? negativeSum - deviation : negativeSum;
        }
        var sum = positiveSum + negativeSum;
        var pci = sum != 0 ? MathHelper.MinOrMax(100 * positiveSum / sum, 100, 0) : 0;
        var signal = _signalSmoother.Next(pci, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pci", pci },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pci, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class PivotDetectorOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;

    public PivotDetectorOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 200,
        int length2 = 14, InputName inputName = InputName.Close)
    {
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PivotDetectorOscillatorState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rsi = new RsiState(maType, Math.Max(1, length2));
        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PivotDetectorOscillator;

    public void Reset()
    {
        _rsi.Reset();
        _sma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var rsi = _rsi.Next(value, isFinal);
        var pdo = value > sma ? (rsi - 35) / (85 - 35) * 100 : value <= sma ? (rsi - 20) / (70 - 20) * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pdo", pdo }
            };
        }

        return new StreamingIndicatorStateResult(pdo, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _sma.Dispose();
    }
}

public sealed class PivotPointAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _pp1Smoother;
    private readonly IMovingAverageSmoother _pp2Smoother;
    private readonly IMovingAverageSmoother _pp3Smoother;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public PivotPointAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 3)
    {
        var resolved = Math.Max(1, length);
        _pp1Smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pp2Smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _pp3Smoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.PivotPointAverage;

    public void Reset()
    {
        _pp1Smoother.Reset();
        _pp2Smoother.Reset();
        _pp3Smoother.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;
        var currentOpen = bar.Open;

        var pp1 = (prevHigh + prevLow + prevClose) / 3;
        var pp2 = (prevHigh + prevLow + prevClose + currentOpen) / 4;
        var pp3 = (prevHigh + prevLow + currentOpen) / 3;

        var signal1 = _pp1Smoother.Next(pp1, isFinal);
        var signal2 = _pp2Smoother.Next(pp2, isFinal);
        var signal3 = _pp3Smoother.Next(pp3, isFinal);

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Pivot1", pp1 },
                { "Signal1", signal1 },
                { "Pivot2", pp2 },
                { "Signal2", signal2 },
                { "Pivot3", pp3 },
                { "Signal3", signal3 }
            };
        }

        return new StreamingIndicatorStateResult(pp1, outputs);
    }

    public void Dispose()
    {
        _pp1Smoother.Dispose();
        _pp2Smoother.Dispose();
        _pp3Smoother.Dispose();
    }
}

public sealed class PolarizedFractalEfficiencyState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _c2cSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private int _index;
    private bool _hasPrev;

    public PolarizedFractalEfficiencyState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 9,
        int smoothLength = 5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _c2cSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PolarizedFractalEfficiencyState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _c2cSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PolarizedFractalEfficiency;

    public void Reset()
    {
        _c2cSum.Reset();
        _signalSmoother.Reset();
        _values.Clear();
        _prevValue = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priorValue = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var diff = _index >= _length ? value - priorValue : 0;
        var pfe = MathHelper.Sqrt(MathHelper.Pow(diff, 2) + 100);

        var c2cDiff = _hasPrev ? value - prevValue : 0;
        var c2c = MathHelper.Sqrt(MathHelper.Pow(c2cDiff, 2) + 1);
        int countAfter;
        var c2cSum = isFinal ? _c2cSum.Add(c2c, out countAfter) : _c2cSum.Preview(c2c, out countAfter);
        var efRatio = c2cSum != 0 ? pfe / c2cSum * 100 : 0;
        var fracEff = _index >= _length && value - priorValue > 0 ? efRatio : -efRatio;
        var pfeSmoothed = _signalSmoother.Next(fracEff, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pfe", pfeSmoothed }
            };
        }

        return new StreamingIndicatorStateResult(pfeSmoothed, outputs);
    }

    public void Dispose()
    {
        _c2cSum.Dispose();
        _signalSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class PolynomialLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public PolynomialLeastSquaresMovingAverageState(int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PolynomialLeastSquaresMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PolynomialLeastSquaresMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double x1Pow1Sum;
        double x2Pow1Sum;
        double x1Pow2Sum;
        double x2Pow2Sum;
        double x1Pow3Sum;
        double x2Pow3Sum;
        double wPow1;
        double wPow2;
        double wPow3;
        double sumPow1 = 0;
        double sumPow2 = 0;
        double sumPow3 = 0;

        for (var j = 1; j <= _length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            var x1 = (double)j / _length;
            var x2 = (double)(j - 1) / _length;
            var ax1 = x1 * x1;
            var ax2 = x2 * x2;

            double b1Pow1Sum = 0;
            double b2Pow1Sum = 0;
            double b1Pow2Sum = 0;
            double b2Pow2Sum = 0;
            double b1Pow3Sum = 0;
            double b2Pow3Sum = 0;
            for (var k = 1; k <= 3; k++)
            {
                var b1 = (double)1 / k * Math.Sin(x1 * k * Math.PI);
                var b2 = (double)1 / k * Math.Sin(x2 * k * Math.PI);

                b1Pow1Sum += k == 1 ? b1 : 0;
                b2Pow1Sum += k == 1 ? b2 : 0;
                b1Pow2Sum += k <= 2 ? b1 : 0;
                b2Pow2Sum += k <= 2 ? b2 : 0;
                b1Pow3Sum += k <= 3 ? b1 : 0;
                b2Pow3Sum += k <= 3 ? b2 : 0;
            }

            x1Pow1Sum = ax1 + b1Pow1Sum;
            x2Pow1Sum = ax2 + b2Pow1Sum;
            wPow1 = x1Pow1Sum - x2Pow1Sum;
            sumPow1 += prevValue * wPow1;
            x1Pow2Sum = ax1 + b1Pow2Sum;
            x2Pow2Sum = ax2 + b2Pow2Sum;
            wPow2 = x1Pow2Sum - x2Pow2Sum;
            sumPow2 += prevValue * wPow2;
            x1Pow3Sum = ax1 + b1Pow3Sum;
            x2Pow3Sum = ax2 + b2Pow3Sum;
            wPow3 = x1Pow3Sum - x2Pow3Sum;
            sumPow3 += prevValue * wPow3;
        }

        _ = sumPow1;
        _ = sumPow2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Plsma", sumPow3 }
            };
        }

        return new StreamingIndicatorStateResult(sumPow3, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class PositiveVolumeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly int _initialValue;
    private double _prevClose;
    private double _prevVolume;
    private double _prevPvi;
    private bool _hasPrev;

    public PositiveVolumeIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 255,
        int initialValue = 1000, InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
        _initialValue = initialValue;
    }

    public PositiveVolumeIndexState(MovingAvgType maType, int length, int initialValue, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _initialValue = initialValue;
    }

    public IndicatorName Name => IndicatorName.PositiveVolumeIndex;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevClose = 0;
        _prevVolume = 0;
        _prevPvi = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevVolume = _hasPrev ? _prevVolume : 0;
        var prevPvi = _hasPrev ? _prevPvi : _initialValue;
        var pctChg = CalculationsHelper.CalculatePercentChange(value, prevClose);
        var pvi = volume <= prevVolume ? prevPvi : prevPvi + pctChg;
        var signal = _signalSmoother.Next(pvi, isFinal);

        if (isFinal)
        {
            _prevClose = value;
            _prevVolume = volume;
            _prevPvi = pvi;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pvi", pvi },
                { "PviSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pvi, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class PoweredKaufmanAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private readonly double _factor;
    private double _prevPkama;
    private double _prevPkamaSp;
    private bool _hasPrev;

    public PoweredKaufmanAdaptiveMovingAverageState(int length = 100, double factor = 3, InputName inputName = InputName.Close)
    {
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public PoweredKaufmanAdaptiveMovingAverageState(int length, double factor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _er = new EfficiencyRatioState(Math.Max(1, length));
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PoweredKaufmanAdaptiveMovingAverage;

    public void Reset()
    {
        _er.Reset();
        _prevPkama = 0;
        _prevPkamaSp = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var powSp = er != 0 ? 1 / er : _factor;
        var perSp = MathHelper.Pow(er, powSp);
        var per = MathHelper.Pow(er, _factor);

        var prevPkama = _hasPrev ? _prevPkama : value;
        var pkama = (per * value) + ((1 - per) * prevPkama);

        var prevPkamaSp = _hasPrev ? _prevPkamaSp : value;
        var pkamaSp = (perSp * value) + ((1 - perSp) * prevPkamaSp);

        if (isFinal)
        {
            _prevPkama = pkama;
            _prevPkamaSp = pkamaSp;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Per", per },
                { "Pkama", pkama }
            };
        }

        return new StreamingIndicatorStateResult(pkama, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

internal sealed class VariableIndexDynamicAverageEngine : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly RollingWindowSum _posSum;
    private readonly RollingWindowSum _negSum;
    private double _prevValue;
    private double _prevVidya;
    private bool _hasPrev;

    public VariableIndexDynamicAverageEngine(int length)
    {
        _length = Math.Max(1, length);
        _alpha = 2d / (_length + 1);
        _posSum = new RollingWindowSum(_length);
        _negSum = new RollingWindowSum(_length);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var pos = diff > 0 ? diff : 0;
        var neg = diff < 0 ? Math.Abs(diff) : 0;

        var posSum = isFinal ? _posSum.Add(pos, out _) : _posSum.Preview(pos, out _);
        var negSum = isFinal ? _negSum.Add(neg, out _) : _negSum.Preview(neg, out _);
        var cmo = posSum + negSum != 0 ? MathHelper.MinOrMax((posSum - negSum) / (posSum + negSum) * 100, 100, -100) : 0;
        var currentCmo = Math.Abs(cmo / 100);
        var vidya = (value * _alpha * currentCmo) + (_prevVidya * (1 - (_alpha * currentCmo)));

        if (isFinal)
        {
            _prevValue = value;
            _prevVidya = vidya;
            _hasPrev = true;
        }

        return vidya;
    }

    public void Reset()
    {
        _posSum.Reset();
        _negSum.Reset();
        _prevValue = 0;
        _prevVidya = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _posSum.Dispose();
        _negSum.Dispose();
    }
}
