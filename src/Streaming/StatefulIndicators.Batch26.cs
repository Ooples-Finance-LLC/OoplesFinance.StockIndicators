using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class UpsideDownsideVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _upSum;
    private readonly RollingWindowSum _downSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public UpsideDownsideVolumeState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _upSum = new RollingWindowSum(_length);
        _downSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UpsideDownsideVolumeState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _upSum = new RollingWindowSum(_length);
        _downSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UpsideDownsideVolume;

    public void Reset()
    {
        _upSum.Reset();
        _downSum.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = _hasPrev ? _prevValue : 0;
        var upVol = value > prevValue ? volume : 0;
        var downVol = value < prevValue ? volume * -1 : 0;
        var upSum = isFinal ? _upSum.Add(upVol, out _) : _upSum.Preview(upVol, out _);
        var downSum = isFinal ? _downSum.Add(downVol, out _) : _downSum.Preview(downVol, out _);
        var udv = downSum != 0 ? upSum / downSum : 0;

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
                { "Udv", udv }
            };
        }

        return new StreamingIndicatorStateResult(udv, outputs);
    }

    public void Dispose()
    {
        _upSum.Dispose();
        _downSum.Dispose();
    }
}

public sealed class UpsidePotentialRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly double _ratio;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _retValues;
    private readonly StreamingInputResolver _input;

    public UpsidePotentialRatioState(int length = 30, double bmk = 0.05, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _ratio = 1d / _length;
        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = Math.Pow(1 + bmk, _length / barsPerYr) - 1;
        _values = new PooledRingBuffer<double>(_length + 1);
        _retValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UpsidePotentialRatioState(int length, double bmk, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _ratio = 1d / _length;
        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = Math.Pow(1 + bmk, _length / barsPerYr) - 1;
        _values = new PooledRingBuffer<double>(_length + 1);
        _retValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UpsidePotentialRatio;

    public void Reset()
    {
        _values.Clear();
        _retValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var ret = priorValue != 0 ? (value / priorValue) - 1 : 0;

        double downSide = 0;
        double upSide = 0;
        for (var j = 0; j < _length; j++)
        {
            var retValue = EhlersStreamingWindow.GetOffsetValue(_retValues, ret, j);
            if (retValue < _bench)
            {
                var diff = retValue - _bench;
                downSide += (diff * diff) * _ratio;
            }
            else if (retValue > _bench)
            {
                upSide += (retValue - _bench) * _ratio;
            }
        }

        var upr = downSide >= 0 ? upSide / Math.Sqrt(downSide) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _retValues.TryAdd(ret, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Upr", upr }
            };
        }

        return new StreamingIndicatorStateResult(upr, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _retValues.Dispose();
    }
}

public sealed class ValueChartIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _varp;
    private readonly IMovingAverageSmoother _ma;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private readonly PooledRingBuffer<double> _closeValues;
    private readonly StreamingInputResolver _input;

    public ValueChartIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.MedianPrice, int length = 5)
    {
        _length = Math.Max(1, length);
        _varp = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 5));
        _ma = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_varp);
        _lowWindow = new RollingWindowMin(_varp);
        _highestValues = new PooledRingBuffer<double>(5);
        _lowestValues = new PooledRingBuffer<double>(5);
        _closeValues = new PooledRingBuffer<double>(6);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ValueChartIndicatorState(MovingAvgType maType, InputName inputName, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _varp = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 5));
        _ma = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_varp);
        _lowWindow = new RollingWindowMin(_varp);
        _highestValues = new PooledRingBuffer<double>(5);
        _lowestValues = new PooledRingBuffer<double>(5);
        _closeValues = new PooledRingBuffer<double>(6);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.ValueChartIndicator;

    public void Reset()
    {
        _ma.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _closeValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mba = _ma.Next(value, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        var prevHighest1 = EhlersStreamingWindow.GetOffsetValue(_highestValues, highest, 1);
        var prevHighest2 = EhlersStreamingWindow.GetOffsetValue(_highestValues, highest, 2);
        var prevHighest3 = EhlersStreamingWindow.GetOffsetValue(_highestValues, highest, 3);
        var prevHighest4 = EhlersStreamingWindow.GetOffsetValue(_highestValues, highest, 4);

        var prevLowest1 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, lowest, 1);
        var prevLowest2 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, lowest, 2);
        var prevLowest3 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, lowest, 3);
        var prevLowest4 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, lowest, 4);

        var close = bar.Close;
        var prevClose1 = EhlersStreamingWindow.GetOffsetValue(_closeValues, close, 1);
        var prevClose2 = EhlersStreamingWindow.GetOffsetValue(_closeValues, close, 2);
        var prevClose3 = EhlersStreamingWindow.GetOffsetValue(_closeValues, close, 3);
        var prevClose4 = EhlersStreamingWindow.GetOffsetValue(_closeValues, close, 4);
        var prevClose5 = EhlersStreamingWindow.GetOffsetValue(_closeValues, close, 5);

        var vara = highest - lowest;
        var varr1 = vara == 0 && _varp == 1 ? Math.Abs(close - prevClose1) : vara;
        var varb = prevHighest1 - prevLowest1;
        var varr2 = varb == 0 && _varp == 1 ? Math.Abs(prevClose1 - prevClose2) : varb;
        var varc = prevHighest2 - prevLowest2;
        var varr3 = varc == 0 && _varp == 1 ? Math.Abs(prevClose2 - prevClose3) : varc;
        var vard = prevHighest3 - prevLowest3;
        var varr4 = vard == 0 && _varp == 1 ? Math.Abs(prevClose3 - prevClose4) : vard;
        var vare = prevHighest4 - prevLowest4;
        var varr5 = vare == 0 && _varp == 1 ? Math.Abs(prevClose4 - prevClose5) : vare;
        var lRange = (varr1 + varr2 + varr3 + varr4 + varr5) / 5d * 0.2d;

        var vClose = lRange != 0 ? (close - mba) / lRange : 0;
        var vOpen = lRange != 0 ? (bar.Open - mba) / lRange : 0;
        var vHigh = lRange != 0 ? (bar.High - mba) / lRange : 0;
        var vLow = lRange != 0 ? (bar.Low - mba) / lRange : 0;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _closeValues.TryAdd(close, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "vClose", vClose },
                { "vOpen", vOpen },
                { "vHigh", vHigh },
                { "vLow", vLow }
            };
        }

        return new StreamingIndicatorStateResult(vClose, outputs);
    }

    public void Dispose()
    {
        _ma.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
        _closeValues.Dispose();
    }
}

public sealed class VanillaABCDPatternState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevValue3;
    private double _prevOs;
    private int _index;

    public VanillaABCDPatternState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public VanillaABCDPatternState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VanillaABCDPattern;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevValue3 = 0;
        _prevOs = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevValue3 = _index >= 3 ? _prevValue3 : 0;

        var up = prevValue3 > prevValue2 && prevValue1 > prevValue2 && value < prevValue2 ? 1d : 0d;
        var dn = prevValue3 < prevValue2 && prevValue1 < prevValue2 && value > prevValue2 ? 1d : 0d;

        var prevOs = _index >= 1 ? _prevOs : 0;
        var os = up == 1d ? 1d : dn == 1d ? 0d : prevOs;
        var dos = os - prevOs;

        if (isFinal)
        {
            _prevValue3 = _prevValue2;
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevOs = os;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vabcd", dos }
            };
        }

        return new StreamingIndicatorStateResult(dos, outputs);
    }
}

public sealed class VaradiOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _ratioMa;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _order;

    public VaradiOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _ratioMa = MovingAverageSmootherFactory.Create(maType, _length);
        _order = new RollingOrderStatistic(_length);
        _order.Add(0);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VaradiOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _ratioMa = MovingAverageSmootherFactory.Create(maType, _length);
        _order = new RollingOrderStatistic(_length);
        _order.Add(0);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VaradiOscillator;

    public void Reset()
    {
        _ratioMa.Reset();
        _order.Dispose();
        _order = new RollingOrderStatistic(_length);
        _order.Add(0);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var median = (bar.High + bar.Low) / 2d;
        var ratio = median != 0 ? value / median : 0;
        var a = _ratioMa.Next(ratio, isFinal);
        var countLe = _order.CountLessThanOrEqual(a);
        var dvo = MathHelper.MinOrMax(countLe / (double)_length * 100, 100, 0);

        if (isFinal)
        {
            _order.Add(a);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vo", dvo }
            };
        }

        return new StreamingIndicatorStateResult(dvo, outputs);
    }

    public void Dispose()
    {
        _ratioMa.Dispose();
        _order.Dispose();
    }
}

public sealed class VariableAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _closeMa;
    private readonly IMovingAverageSmoother _openMa;
    private readonly IMovingAverageSmoother _highMa;
    private readonly IMovingAverageSmoother _lowMa;
    private readonly StreamingInputResolver _input;
    private double _prevVma;
    private bool _hasPrev;

    public VariableAdaptiveMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _closeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _openMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _highMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VariableAdaptiveMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _closeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _openMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _highMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VariableAdaptiveMovingAverage;

    public void Reset()
    {
        _closeMa.Reset();
        _openMa.Reset();
        _highMa.Reset();
        _lowMa.Reset();
        _prevVma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var c = _closeMa.Next(value, isFinal);
        var o = _openMa.Next(bar.Open, isFinal);
        var h = _highMa.Next(bar.High, isFinal);
        var l = _lowMa.Next(bar.Low, isFinal);
        var lv = h - l != 0 ? MathHelper.MinOrMax(Math.Abs(c - o) / (h - l), 0.99, 0.01) : 0;

        var prevVma = _hasPrev ? _prevVma : value;
        var vma = (lv * value) + ((1 - lv) * prevVma);

        if (isFinal)
        {
            _prevVma = vma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vama", vma }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        _closeMa.Dispose();
        _openMa.Dispose();
        _highMa.Dispose();
        _lowMa.Dispose();
    }
}

public sealed class VariableIndexDynamicAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumOscillatorState _cmo;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha;
    private double _prevVidya;
    private bool _hasPrev;

    public VariableIndexDynamicAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _alpha = (double)2 / (Math.Max(1, length) + 1);
        _cmo = new ChandeMomentumOscillatorState(maType, Math.Max(1, length), 3, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VariableIndexDynamicAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = (double)2 / (Math.Max(1, length) + 1);
        _cmo = new ChandeMomentumOscillatorState(maType, Math.Max(1, length), 3, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VariableIndexDynamicAverage;

    public void Reset()
    {
        _cmo.Reset();
        _prevVidya = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var cmo = _cmo.Update(bar, isFinal, includeOutputs: false).Value;
        var currentCmo = Math.Abs(cmo / 100);
        var prevVidya = _hasPrev ? _prevVidya : value;
        var vidya = (value * _alpha * currentCmo) + (prevVidya * (1 - (_alpha * currentCmo)));

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
                { "Vidya", vidya }
            };
        }

        return new StreamingIndicatorStateResult(vidya, outputs);
    }

    public void Dispose()
    {
        _cmo.Dispose();
    }
}

public sealed class VariableLengthMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _prevLength;
    private double _prevVlma;
    private bool _hasPrev;

    public VariableLengthMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5,
        int maxLength = 50, InputName inputName = InputName.Close)
    {
        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(_minLength, maxLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _maxLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _maxLength, inputName);
        _input = new StreamingInputResolver(inputName, null);
        _prevLength = _maxLength;
    }

    public VariableLengthMovingAverageState(MovingAvgType maType, int minLength, int maxLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _minLength = Math.Max(1, minLength);
        _maxLength = Math.Max(_minLength, maxLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _maxLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _maxLength, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _prevLength = _maxLength;
    }

    public IndicatorName Name => IndicatorName.VariableLengthMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _stdDev.Reset();
        _prevLength = _maxLength;
        _prevVlma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var a = sma - (1.75 * stdDev);
        var b = sma - (0.25 * stdDev);
        var c = sma + (0.25 * stdDev);
        var d = sma + (1.75 * stdDev);
        var prevLength = _hasPrev ? _prevLength : _maxLength;
        var length = MathHelper.MinOrMax(value >= b && value <= c ? prevLength + 1 : value < a || value > d ? prevLength - 1 : prevLength,
            _maxLength, _minLength);
        var sc = 2 / (length + 1);
        var prevVlma = _hasPrev ? _prevVlma : value;
        var vlma = (value * sc) + ((1 - sc) * prevVlma);

        if (isFinal)
        {
            _prevLength = length;
            _prevVlma = vlma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Length", length },
                { "Vlma", vlma }
            };
        }

        return new StreamingIndicatorStateResult(vlma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class VariableMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VariableMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;

    public VariableMovingAverageState(int length = 6, InputName inputName = InputName.Close)
    {
        _engine = new VariableMovingAverageEngine(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VariableMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _engine = new VariableMovingAverageEngine(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VariableMovingAverage;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var vma = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vma", vma }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class VariableMovingAverageBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly bool _useVariable;
    private readonly double _mult;
    private readonly VariableMovingAverageEngine? _vmaEngine;
    private readonly VariableMovingAverageEngine? _atrEngine;
    private readonly IMovingAverageSmoother? _ma;
    private readonly IMovingAverageSmoother? _atrMa;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public VariableMovingAverageBandsState(MovingAvgType maType = MovingAvgType.VariableMovingAverage, int length = 6,
        double mult = 1.5, InputName inputName = InputName.Close)
    {
        _mult = mult;
        _useVariable = maType == MovingAvgType.VariableMovingAverage;
        if (_useVariable)
        {
            _vmaEngine = new VariableMovingAverageEngine(length);
            _atrEngine = new VariableMovingAverageEngine(length);
        }
        else
        {
            var resolved = Math.Max(1, length);
            _ma = MovingAverageSmootherFactory.Create(maType, resolved);
            _atrMa = MovingAverageSmootherFactory.Create(maType, resolved);
        }

        _input = new StreamingInputResolver(inputName, null);
    }

    public VariableMovingAverageBandsState(MovingAvgType maType, int length, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _mult = mult;
        _useVariable = maType == MovingAvgType.VariableMovingAverage;
        if (_useVariable)
        {
            _vmaEngine = new VariableMovingAverageEngine(length);
            _atrEngine = new VariableMovingAverageEngine(length);
        }
        else
        {
            var resolved = Math.Max(1, length);
            _ma = MovingAverageSmootherFactory.Create(maType, resolved);
            _atrMa = MovingAverageSmootherFactory.Create(maType, resolved);
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VariableMovingAverageBands;

    public void Reset()
    {
        _vmaEngine?.Reset();
        _atrEngine?.Reset();
        _ma?.Reset();
        _atrMa?.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var vma = _useVariable
            ? _vmaEngine!.Next(value, isFinal)
            : _ma!.Next(value, isFinal);

        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _useVariable
            ? _atrEngine!.Next(tr, isFinal)
            : _atrMa!.Next(tr, isFinal);

        var offset = _mult * atr;
        var upper = vma + offset;
        var lower = vma - offset;

        if (isFinal)
        {
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", vma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        _vmaEngine?.Dispose();
        _atrEngine?.Dispose();
        _ma?.Dispose();
        _atrMa?.Dispose();
    }
}

public sealed class VerticalHorizontalMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _changeSum;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevVhma;
    private bool _hasPrev;

    public VerticalHorizontalMovingAverageState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _changeSum = new RollingWindowSum(_length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VerticalHorizontalMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _changeSum = new RollingWindowSum(_length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VerticalHorizontalMovingAverage;

    public void Reset()
    {
        _changeSum.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _values.Clear();
        _prevVhma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var priceChange = Math.Abs(value - priorValue);
        var changeSum = isFinal ? _changeSum.Add(priceChange, out _) : _changeSum.Preview(priceChange, out _);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var vhf = changeSum != 0 ? (highest - lowest) / changeSum : 0;
        var prevVhma = _hasPrev ? _prevVhma : 0;
        var vhma = prevVhma + (MathHelper.Pow(vhf, 2) * (value - prevVhma));

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevVhma = vhma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vhma", vhma }
            };
        }

        return new StreamingIndicatorStateResult(vhma, outputs);
    }

    public void Dispose()
    {
        _changeSum.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _values.Dispose();
    }
}

public sealed class VervoortHeikenAshiCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _haMa1;
    private readonly IMovingAverageSmoother _haMa2;
    private readonly IMovingAverageSmoother _medianMa1;
    private readonly IMovingAverageSmoother _medianMa2;
    private readonly StreamingInputResolver _input;
    private double _prevInput;
    private double _prevHao;
    private double _prevHac;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _prevDnKeeping;
    private bool _prevDnKeepAll;
    private bool _prevDnTrend;
    private bool _prevUpKeeping;
    private bool _prevUpKeepAll;
    private bool _prevUpTrend;
    private double _prevHaco;
    private bool _hasPrev;

    public VervoortHeikenAshiCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ZeroLagTripleExponentialMovingAverage,
        InputName inputName = InputName.FullTypicalPrice, int length = 34)
    {
        var resolved = Math.Max(1, length);
        _haMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _haMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VervoortHeikenAshiCandlestickOscillatorState(MovingAvgType maType, InputName inputName, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _haMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _haMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VervoortHeikenAshiCandlestickOscillator;

    public void Reset()
    {
        _haMa1.Reset();
        _haMa2.Reset();
        _medianMa1.Reset();
        _medianMa2.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _prevHac = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevDnKeeping = false;
        _prevDnKeepAll = false;
        _prevDnTrend = false;
        _prevUpKeeping = false;
        _prevUpKeepAll = false;
        _prevUpTrend = false;
        _prevHaco = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var inputValue = _input.GetValue(bar);
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;
        var medianPrice = (bar.High + bar.Low) / 2;

        var tma1 = _haMa1.Next(hac, isFinal);
        var tma2 = _haMa2.Next(tma1, isFinal);
        var tma12 = _medianMa1.Next(medianPrice, isFinal);
        var tma22 = _medianMa2.Next(tma12, isFinal);
        var zlHa = tma1 + (tma1 - tma2);
        var zlCl = tma12 + (tma12 - tma22);
        var zlDiff = zlCl - zlHa;

        var prevHac = _hasPrev ? _prevHac : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var dnKeep1 = hac < hao && prevHac < prevHao;
        var dnKeep2 = zlDiff < 0;
        var dnKeep3 = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * 0.35 && bar.Low <= prevHigh;
        var dnKeeping = dnKeep1 || dnKeep2;
        var dnKeepAll = (dnKeeping || _prevDnKeeping) && ((bar.Close < bar.Open) || (bar.Close < prevClose));
        var dnTrend = dnKeepAll || (_prevDnKeepAll && dnKeep3);

        var upKeep1 = hac >= hao && prevHac >= prevHao;
        var upKeep2 = zlDiff >= 0;
        var upKeep3 = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * 0.35 && bar.High >= prevLow;
        var upKeeping = upKeep1 || upKeep2;
        var upKeepAll = (upKeeping || _prevUpKeeping) && ((bar.Close >= bar.Open) || (bar.Close >= prevClose));
        var upTrend = upKeepAll || (_prevUpKeepAll && upKeep3);

        var upw = dnTrend == false && _prevDnTrend && upTrend;
        var dnw = upTrend == false && _prevUpTrend && dnTrend;
        var haco = upw ? 1 : dnw ? -1 : _prevHaco;

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _prevHac = hac;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevDnKeeping = dnKeeping;
            _prevDnKeepAll = dnKeepAll;
            _prevDnTrend = dnTrend;
            _prevUpKeeping = upKeeping;
            _prevUpKeepAll = upKeepAll;
            _prevUpTrend = upTrend;
            _prevHaco = haco;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vhaco", haco }
            };
        }

        return new StreamingIndicatorStateResult(haco, outputs);
    }

    public void Dispose()
    {
        _haMa1.Dispose();
        _haMa2.Dispose();
        _medianMa1.Dispose();
        _medianMa2.Dispose();
    }
}

public sealed class VervoortHeikenAshiLongTermCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _tacMa1;
    private readonly IMovingAverageSmoother _tacMa2;
    private readonly IMovingAverageSmoother _thlMa1;
    private readonly IMovingAverageSmoother _thlMa2;
    private readonly StreamingInputResolver _input;
    private readonly double _factor;
    private double _prevInput;
    private double _prevHao;
    private double _prevHac;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _prevKeepN1;
    private bool _prevKeepAll1;
    private bool _prevUtr;
    private bool _prevKeepN2;
    private bool _prevKeepAll2;
    private bool _prevDtr;
    private double _prevHaco;
    private bool _hasPrev;

    public VervoortHeikenAshiLongTermCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage,
        InputName inputName = InputName.FullTypicalPrice, int length = 55, double factor = 1.1)
    {
        var resolved = Math.Max(1, length);
        _tacMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _tacMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _factor = factor;
    }

    public VervoortHeikenAshiLongTermCandlestickOscillatorState(MovingAvgType maType, InputName inputName, int length, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _tacMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _tacMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, selector);
        _factor = factor;
    }

    public IndicatorName Name => IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;

    public void Reset()
    {
        _tacMa1.Reset();
        _tacMa2.Reset();
        _thlMa1.Reset();
        _thlMa2.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _prevHac = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevKeepN1 = false;
        _prevKeepAll1 = false;
        _prevUtr = false;
        _prevKeepN2 = false;
        _prevKeepAll2 = false;
        _prevDtr = false;
        _prevHaco = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var inputValue = _input.GetValue(bar);
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;
        var medianPrice = (bar.High + bar.Low) / 2;

        var tac = _tacMa1.Next(hac, isFinal);
        var tacTema = _tacMa2.Next(tac, isFinal);
        var thl2 = _thlMa1.Next(medianPrice, isFinal);
        var thl2Tema = _thlMa2.Next(thl2, isFinal);
        var hacSmooth = (2 * tac) - tacTema;
        var hl2Smooth = (2 * thl2) - thl2Tema;

        var prevHac = _hasPrev ? _prevHac : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var shortCandle = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * _factor;
        var keepN1 = ((hac >= hao) && (prevHac >= prevHao)) || bar.Close >= hac ||
            bar.High > prevHigh || bar.Low > prevLow || hl2Smooth >= hacSmooth;
        var keepAll1 = keepN1 || (_prevKeepN1 && (bar.Close >= bar.Open || bar.Close >= prevClose));
        var keep13 = shortCandle && bar.High >= prevLow;
        var utr = keepAll1 || (_prevKeepAll1 && keep13);

        var keepN2 = (hac < hao && prevHac < prevHao) || hl2Smooth < hacSmooth;
        var keepAll2 = keepN2 || (_prevKeepN2 && (bar.Close < bar.Open || bar.Close < prevClose));
        var keep23 = shortCandle && bar.Low <= prevHigh;
        var dtr = (keepAll2 || _prevKeepAll2) && keep23;

        var upw = dtr == false && _prevDtr && utr;
        var dnw = utr == false && _prevUtr && dtr;
        var haco = upw ? 1 : dnw ? -1 : _prevHaco;

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _prevHac = hac;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevKeepN1 = keepN1;
            _prevKeepAll1 = keepAll1;
            _prevUtr = utr;
            _prevKeepN2 = keepN2;
            _prevKeepAll2 = keepAll2;
            _prevDtr = dtr;
            _prevHaco = haco;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vhaltco", haco }
            };
        }

        return new StreamingIndicatorStateResult(haco, outputs);
    }

    public void Dispose()
    {
        _tacMa1.Dispose();
        _tacMa2.Dispose();
        _thlMa1.Dispose();
        _thlMa2.Dispose();
    }
}

public sealed class VervoortModifiedBollingerBandIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _hacMa1;
    private readonly IMovingAverageSmoother _hacMa2;
    private readonly IMovingAverageSmoother _zlhaMa;
    private readonly IMovingAverageSmoother _wma;
    private readonly StandardDeviationVolatilityState _zlhaStdDev;
    private readonly StandardDeviationVolatilityState _percbStdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevMult;
    private double _prevInput;
    private double _prevHao;
    private double _zlhaTemaValue;
    private double _percbValue;
    private bool _hasPrev;

    public VervoortModifiedBollingerBandIndicatorState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage,
        InputName inputName = InputName.FullTypicalPrice, int length1 = 18, int length2 = 200,
        int smoothLength = 8, double stdDevMult = 1.6)
    {
        _stdDevMult = stdDevMult;
        _hacMa1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _hacMa2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _zlhaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length1));
        _zlhaStdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length1), _ => _zlhaTemaValue);
        _percbStdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length2), _ => _percbValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VervoortModifiedBollingerBandIndicatorState(MovingAvgType maType, InputName inputName, int length1, int length2,
        int smoothLength, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _stdDevMult = stdDevMult;
        _hacMa1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _hacMa2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _zlhaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length1));
        _zlhaStdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length1), _ => _zlhaTemaValue);
        _percbStdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length2), _ => _percbValue);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VervoortModifiedBollingerBandIndicator;

    public void Reset()
    {
        _hacMa1.Reset();
        _hacMa2.Reset();
        _zlhaMa.Reset();
        _wma.Reset();
        _zlhaStdDev.Reset();
        _percbStdDev.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _zlhaTemaValue = 0;
        _percbValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var inputValue = _input.GetValue(bar);
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;

        var tma1 = _hacMa1.Next(hac, isFinal);
        var tma2 = _hacMa2.Next(tma1, isFinal);
        var zlha = tma1 + (tma1 - tma2);
        var zlhaTema = _zlhaMa.Next(zlha, isFinal);
        _zlhaTemaValue = zlhaTema;

        var zlhaStdDev = _zlhaStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var wma = _wma.Next(zlhaTema, isFinal);
        var percb = zlhaStdDev != 0
            ? (zlhaTema + (2 * zlhaStdDev) - wma) / (4 * zlhaStdDev) * 100
            : 0;
        _percbValue = percb;
        var percbStdDev = _percbStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = 50 + (_stdDevMult * percbStdDev);
        var lower = 50 - (_stdDevMult * percbStdDev);

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", percb },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(percb, outputs);
    }

    public void Dispose()
    {
        _hacMa1.Dispose();
        _hacMa2.Dispose();
        _zlhaMa.Dispose();
        _wma.Dispose();
        _zlhaStdDev.Dispose();
        _percbStdDev.Dispose();
    }
}

public sealed class VervoortSmoothedOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _r1Sma;
    private readonly IMovingAverageSmoother _r2Sma;
    private readonly IMovingAverageSmoother _r3Sma;
    private readonly IMovingAverageSmoother _r4Sma;
    private readonly IMovingAverageSmoother _r5Sma;
    private readonly IMovingAverageSmoother _r6Sma;
    private readonly IMovingAverageSmoother _r7Sma;
    private readonly IMovingAverageSmoother _r8Sma;
    private readonly IMovingAverageSmoother _r9Sma;
    private readonly IMovingAverageSmoother _r10Sma;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _tema;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _wma;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly RollingWindowMin _rbcMinWindow;
    private readonly RollingWindowSum _fastKSum;
    private readonly StreamingInputResolver _input;

    public VervoortSmoothedOscillatorState(InputName inputName = InputName.TypicalPrice, int length1 = 18,
        int length2 = 30, int length3 = 2, int smoothLength = 3, double stdDevMult = 2)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        var resolvedSmoothLength = Math.Max(1, smoothLength);
        _stdDevMult = stdDevMult;
        _r1Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r2Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r3Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r4Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r5Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r6Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r7Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r8Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r9Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r10Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _ema1 = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolvedSmoothLength);
        _ema2 = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolvedSmoothLength);
        _tema = MovingAverageSmootherFactory.Create(MovingAvgType.TripleExponentialMovingAverage, resolvedSmoothLength);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolvedLength1);
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, resolvedLength1);
        _highWindow = new RollingWindowMax(resolvedLength2);
        _lowWindow = new RollingWindowMin(resolvedLength2);
        _rbcMinWindow = new RollingWindowMin(resolvedLength2);
        _fastKSum = new RollingWindowSum(resolvedSmoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VervoortSmoothedOscillatorState(InputName inputName, int length1, int length2, int length3,
        int smoothLength, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedLength3 = Math.Max(1, length3);
        var resolvedSmoothLength = Math.Max(1, smoothLength);
        _stdDevMult = stdDevMult;
        _r1Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r2Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r3Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r4Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r5Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r6Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r7Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r8Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r9Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _r10Sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedLength3);
        _ema1 = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolvedSmoothLength);
        _ema2 = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolvedSmoothLength);
        _tema = MovingAverageSmootherFactory.Create(MovingAvgType.TripleExponentialMovingAverage, resolvedSmoothLength);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolvedLength1);
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, resolvedLength1);
        _highWindow = new RollingWindowMax(resolvedLength2);
        _lowWindow = new RollingWindowMin(resolvedLength2);
        _rbcMinWindow = new RollingWindowMin(resolvedLength2);
        _fastKSum = new RollingWindowSum(resolvedSmoothLength);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VervoortSmoothedOscillator;

    public void Reset()
    {
        _r1Sma.Reset();
        _r2Sma.Reset();
        _r3Sma.Reset();
        _r4Sma.Reset();
        _r5Sma.Reset();
        _r6Sma.Reset();
        _r7Sma.Reset();
        _r8Sma.Reset();
        _r9Sma.Reset();
        _r10Sma.Reset();
        _ema1.Reset();
        _ema2.Reset();
        _tema.Reset();
        _stdDev.Reset();
        _wma.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _rbcMinWindow.Reset();
        _fastKSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var inputValue = _input.GetValue(bar);
        var close = bar.Close;
        var r1 = _r1Sma.Next(close, isFinal);
        var r2 = _r2Sma.Next(r1, isFinal);
        var r3 = _r3Sma.Next(r2, isFinal);
        var r4 = _r4Sma.Next(r3, isFinal);
        var r5 = _r5Sma.Next(r4, isFinal);
        var r6 = _r6Sma.Next(r5, isFinal);
        var r7 = _r7Sma.Next(r6, isFinal);
        var r8 = _r8Sma.Next(r7, isFinal);
        var r9 = _r9Sma.Next(r8, isFinal);
        var r10 = _r10Sma.Next(r9, isFinal);
        var rainbow = ((5 * r1) + (4 * r2) + (3 * r3) + (2 * r4) + r5 + r6 + r7 + r8 + r9 + r10) / 20d;

        var ema1 = _ema1.Next(rainbow, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var zlrb = (2 * ema1) - ema2;
        var tz = _tema.Next(zlrb, isFinal);
        var hwidth = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var wmatz = _wma.Next(tz, isFinal);
        var zlrbpercb = hwidth != 0
            ? (tz + (_stdDevMult * hwidth) - wmatz) / (2 * _stdDevMult * hwidth * 100)
            : 0;

        var rbc = (rainbow + inputValue) / 2;
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var lowestRbc = isFinal ? _rbcMinWindow.Add(rbc, out _) : _rbcMinWindow.Preview(rbc, out _);
        var nom = rbc - lowest;
        var den = highest - lowestRbc;
        var fastK = den != 0 ? MathHelper.MinOrMax(100 * nom / den, 100, 0) : 0;
        int fastKCount;
        var fastKSum = isFinal ? _fastKSum.Add(fastK, out fastKCount) : _fastKSum.Preview(fastK, out fastKCount);
        var sk = fastKCount > 0 ? fastKSum / fastKCount : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vso", zlrbpercb },
                { "Sk", sk }
            };
        }

        return new StreamingIndicatorStateResult(zlrbpercb, outputs);
    }

    public void Dispose()
    {
        _r1Sma.Dispose();
        _r2Sma.Dispose();
        _r3Sma.Dispose();
        _r4Sma.Dispose();
        _r5Sma.Dispose();
        _r6Sma.Dispose();
        _r7Sma.Dispose();
        _r8Sma.Dispose();
        _r9Sma.Dispose();
        _r10Sma.Dispose();
        _ema1.Dispose();
        _ema2.Dispose();
        _tema.Dispose();
        _stdDev.Dispose();
        _wma.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _rbcMinWindow.Dispose();
        _fastKSum.Dispose();
    }
}

public sealed class VervoortVolatilityBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly double _devMult;
    private readonly double _lowBandMult;
    private readonly IMovingAverageSmoother _medianAvg;
    private readonly IMovingAverageSmoother _medianAvgEma;
    private readonly IMovingAverageSmoother _devHighMa;
    private readonly RollingWindowSum _medianAvgSum;
    private readonly RollingWindowSum _typicalSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevLow;
    private bool _hasPrev;

    public VervoortVolatilityBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 8,
        int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _devMult = devMult;
        _lowBandMult = lowBandMult;
        _medianAvg = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgEma = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _devHighMa = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgSum = new RollingWindowSum(resolvedLength1);
        _typicalSum = new RollingWindowSum(resolvedLength2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VervoortVolatilityBandsState(MovingAvgType maType, int length1, int length2, double devMult,
        double lowBandMult, InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _devMult = devMult;
        _lowBandMult = lowBandMult;
        _medianAvg = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgEma = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _devHighMa = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgSum = new RollingWindowSum(resolvedLength1);
        _typicalSum = new RollingWindowSum(resolvedLength2);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VervoortVolatilityBands;

    public void Reset()
    {
        _medianAvg.Reset();
        _medianAvgEma.Reset();
        _devHighMa.Reset();
        _medianAvgSum.Reset();
        _typicalSum.Reset();
        _prevValue = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var medianAvg = _medianAvg.Next(value, isFinal);
        var medianAvgEma = _medianAvgEma.Next(medianAvg, isFinal);
        int medianCount;
        var medianAvgSum = isFinal ? _medianAvgSum.Add(medianAvg, out medianCount) : _medianAvgSum.Preview(medianAvg, out medianCount);
        var medianAvgSma = medianCount > 0 ? medianAvgSum / medianCount : 0;

        var prevValue = _hasPrev ? _prevValue : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var typical = value >= prevValue ? value - prevLow : prevValue - bar.Low;
        int typicalCount;
        var typicalSum = isFinal ? _typicalSum.Add(typical, out typicalCount) : _typicalSum.Preview(typical, out typicalCount);
        var typicalSma = typicalCount > 0 ? typicalSum / typicalCount : 0;
        var deviation = _devMult * typicalSma;
        var devHigh = _devHighMa.Next(deviation, isFinal);
        var devLow = _lowBandMult * devHigh;

        var upper = medianAvgEma + devHigh;
        var lower = medianAvgEma - devLow;

        if (isFinal)
        {
            _prevValue = value;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", medianAvgSma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(medianAvgSma, outputs);
    }

    public void Dispose()
    {
        _medianAvg.Dispose();
        _medianAvgEma.Dispose();
        _devHighMa.Dispose();
        _medianAvgSum.Dispose();
        _typicalSum.Dispose();
    }
}

public sealed class VixTradingSystemState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;
    private double _prevCount;
    private bool _hasPrev;

    public VixTradingSystemState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50,
        double maxCount = 11, double minCount = -11, InputName inputName = InputName.Close)
    {
        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
        _ = maxCount;
        _ = minCount;
    }

    public VixTradingSystemState(MovingAvgType maType, int length, double maxCount, double minCount,
        InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, selector);
        _ = maxCount;
        _ = minCount;
    }

    public IndicatorName Name => IndicatorName.VixTradingSystem;

    public void Reset()
    {
        _sma.Reset();
        _prevCount = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var vixts = _sma.Next(value, isFinal);
        var prevCount = _hasPrev ? _prevCount : 0;
        var count = value > vixts && prevCount >= 0 ? prevCount + 1
            : value <= vixts && prevCount <= 0 ? prevCount - 1 : prevCount;

        if (isFinal)
        {
            _prevCount = count;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vix", count }
            };
        }

        return new StreamingIndicatorStateResult(count, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
    }
}

public sealed class VolatilityIndexDynamicAverageIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _stdDevSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha1;
    private readonly double _alpha2;
    private double _prevVidya1;
    private double _prevVidya2;
    private bool _hasPrev;

    public VolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, double alpha1 = 0.2, double alpha2 = 0.04, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _alpha1 = alpha1;
        _alpha2 = alpha2;
    }

    public VolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType, int length, double alpha1, double alpha2,
        InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, selector);
        _alpha1 = alpha1;
        _alpha2 = alpha2;
    }

    public IndicatorName Name => IndicatorName.VolatilityIndexDynamicAverageIndicator;

    public void Reset()
    {
        _stdDev.Reset();
        _stdDevSmoother.Reset();
        _prevVidya1 = 0;
        _prevVidya2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var stdDevEma = _stdDevSmoother.Next(stdDev, isFinal);
        var ratio = stdDevEma != 0 ? stdDev / stdDevEma : 0;
        var prevVidya1 = _hasPrev ? _prevVidya1 : value;
        var prevVidya2 = _hasPrev ? _prevVidya2 : value;
        var vidya1 = (_alpha1 * ratio * value) + ((1 - (_alpha1 * ratio)) * prevVidya1);
        var vidya2 = (_alpha2 * ratio * value) + ((1 - (_alpha2 * ratio)) * prevVidya2);

        if (isFinal)
        {
            _prevVidya1 = vidya1;
            _prevVidya2 = vidya2;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vida1", vidya1 },
                { "Vida2", vidya2 }
            };
        }

        return new StreamingIndicatorStateResult(vidya1, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _stdDevSmoother.Dispose();
    }
}

public sealed class VolatilityMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _lbLength;
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _kSmoother;
    private readonly IMovingAverageSmoother _vmaSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public VolatilityMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        int lbLength = 10, int smoothLength = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _lbLength = Math.Max(1, lbLength);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _lbLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _lbLength, inputName);
        _kSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _vmaSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolatilityMovingAverageState(MovingAvgType maType, int length, int lbLength, int smoothLength, InputName inputName,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _lbLength = Math.Max(1, lbLength);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _lbLength);
        _stdDev = new StandardDeviationVolatilityState(maType, _lbLength, selector);
        _kSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _vmaSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolatilityMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _stdDev.Reset();
        _kSmoother.Reset();
        _vmaSmoother.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var dev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = sma + dev;
        var lower = sma - dev;
        var k = upper - lower != 0 ? (value - sma) / (upper - lower) * 100 * 2 : 0;
        var kMa = _kSmoother.Next(k, isFinal);
        var kNorm = Math.Min(Math.Max(kMa, -100), 100);
        var kAbs = Math.Round(Math.Abs(kNorm) / _lbLength);
        var kRescaled = CalculationsHelper.RescaleValue(kAbs, 10, 0, _length, 0, true);
        var vLength = (int)Math.Round(Math.Max(kRescaled, 1));

        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= vLength - 1; j++)
        {
            var weight = vLength - j;
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var vma1 = weightedSum != 0 ? sum / weightedSum : 0;
        var vma = _vmaSmoother.Next(vma1, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vma", vma }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _stdDev.Dispose();
        _kSmoother.Dispose();
        _vmaSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class VolatilityRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevHighest;
    private double _prevLowest;
    private bool _hasPrev;
    private bool _hasWindow;

    public VolatilityRatioState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        double breakoutLevel = 0.5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var windowLength = Math.Max(1, _length - 1);
        _ema = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(windowLength);
        _lowWindow = new RollingWindowMin(windowLength);
        _values = new PooledRingBuffer<double>(_length + 2);
        _input = new StreamingInputResolver(inputName, null);
        _ = breakoutLevel;
    }

    public VolatilityRatioState(MovingAvgType maType, int length, double breakoutLevel, InputName inputName,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var windowLength = Math.Max(1, _length - 1);
        _ema = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(windowLength);
        _lowWindow = new RollingWindowMin(windowLength);
        _values = new PooledRingBuffer<double>(_length + 2);
        _input = new StreamingInputResolver(inputName, selector);
        _ = breakoutLevel;
    }

    public IndicatorName Name => IndicatorName.VolatilityRatio;

    public void Reset()
    {
        _ema.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _values.Clear();
        _prevValue = 0;
        _prevHighest = 0;
        _prevLowest = 0;
        _hasPrev = false;
        _hasWindow = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _ema.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevHighest = _hasWindow ? _prevHighest : 0;
        var prevLowest = _hasWindow ? _prevLowest : 0;
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length + 1);
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var max = priorValue != 0 ? Math.Max(prevHighest, priorValue) : prevHighest;
        var min = priorValue != 0 ? Math.Min(prevLowest, priorValue) : prevLowest;
        var vr = max - min != 0 ? tr / (max - min) : 0;

        var currentHighest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var currentLowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _prevHighest = currentHighest;
            _prevLowest = currentLowest;
            _hasPrev = true;
            _hasWindow = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vr", vr }
            };
        }

        return new StreamingIndicatorStateResult(vr, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _values.Dispose();
    }
}

public sealed class VolatilityWaveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _kf;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _wmap1;
    private readonly IMovingAverageSmoother _wmap2;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public VolatilityWaveMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20,
        double kf = 2.5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _kf = kf;
        var s = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(_length)));
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _wmap1 = MovingAverageSmootherFactory.Create(maType, s);
        _wmap2 = MovingAverageSmootherFactory.Create(maType, s);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolatilityWaveMovingAverageState(MovingAvgType maType, int length, double kf, InputName inputName,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _kf = kf;
        var s = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(_length)));
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _wmap1 = MovingAverageSmootherFactory.Create(maType, s);
        _wmap2 = MovingAverageSmootherFactory.Create(maType, s);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolatilityWaveMovingAverage;

    public void Reset()
    {
        _stdDev.Reset();
        _wmap1.Reset();
        _wmap2.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var sdPct = value != 0 ? stdDev / value * 100 : 0;
        var p = sdPct >= 0 ? MathHelper.MinOrMax(MathHelper.Sqrt(sdPct) * _kf, 4, 1) : 1;

        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, p);
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var pma = weightedSum != 0 ? sum / weightedSum : 0;
        var wmap1 = _wmap1.Next(pma, isFinal);
        var wmap2 = _wmap2.Next(wmap1, isFinal);
        var zlmap = (2 * wmap1) - wmap2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vwma", zlmap }
            };
        }

        return new StreamingIndicatorStateResult(zlmap, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _wmap1.Dispose();
        _wmap2.Dispose();
        _values.Dispose();
    }
}

public sealed class VolumeAccumulationOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _vaoSum;
    private readonly StreamingInputResolver _input;

    public VolumeAccumulationOscillatorState(int length = 14, InputName inputName = InputName.Close)
    {
        _vaoSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeAccumulationOscillatorState(int length, InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _vaoSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeAccumulationOscillator;

    public void Reset()
    {
        _vaoSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var medianValue = (bar.High + bar.Low) / 2d;
        var vao = value != medianValue ? bar.Volume * (value - medianValue) : bar.Volume;
        int countAfter;
        var vaoSum = isFinal ? _vaoSum.Add(vao, out countAfter) : _vaoSum.Preview(vao, out countAfter);
        var vaoAvg = countAfter > 0 ? vaoSum / countAfter : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vao", vaoAvg }
            };
        }

        return new StreamingIndicatorStateResult(vaoAvg, outputs);
    }

    public void Dispose()
    {
        _vaoSum.Dispose();
    }
}

public sealed class VolumeAccumulationPercentState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumeSum;
    private readonly RollingWindowSum _tvaSum;
    private readonly StreamingInputResolver _input;

    public VolumeAccumulationPercentState(int length = 10, InputName inputName = InputName.Close)
    {
        _volumeSum = new RollingWindowSum(Math.Max(1, length));
        _tvaSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeAccumulationPercentState(int length, InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _volumeSum = new RollingWindowSum(Math.Max(1, length));
        _tvaSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeAccumulationPercent;

    public void Reset()
    {
        _volumeSum.Reset();
        _tvaSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var xt = bar.High - bar.Low != 0 ? ((2 * close) - bar.High - bar.Low) / (bar.High - bar.Low) : 0;
        var tva = bar.Volume * xt;
        var volumeSum = isFinal ? _volumeSum.Add(bar.Volume, out _) : _volumeSum.Preview(bar.Volume, out _);
        var tvaSum = isFinal ? _tvaSum.Add(tva, out _) : _tvaSum.Preview(tva, out _);
        var vapc = volumeSum != 0 ? MathHelper.MinOrMax(100 * tvaSum / volumeSum, 100, 0) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vapc", vapc }
            };
        }

        return new StreamingIndicatorStateResult(vapc, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
        _tvaSum.Dispose();
    }
}

public sealed class VolumeAdaptiveBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _upMa;
    private readonly IMovingAverageSmoother _downMa;
    private readonly StreamingInputResolver _input;
    private double _prevUp;
    private double _prevDn;
    private bool _hasPrev;

    public VolumeAdaptiveBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _upMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _downMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeAdaptiveBandsState(MovingAvgType maType, int length, InputName inputName,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _upMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _downMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeAdaptiveBands;

    public void Reset()
    {
        _volumeMa.Reset();
        _upMa.Reset();
        _downMa.Reset();
        _prevUp = 0;
        _prevDn = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volumeMa = _volumeMa.Next(bar.Volume, isFinal);
        var a = Math.Max(volumeMa, 1);
        var b = a * -1;
        var prevUp = _hasPrev ? _prevUp : value;
        var up = a != 0 ? (prevUp + (value * a)) / a : 0;
        var prevDn = _hasPrev ? _prevDn : value;
        var dn = b != 0 ? (prevDn + (value * b)) / b : 0;
        var upperBand = _upMa.Next(up, isFinal);
        var lowerBand = _downMa.Next(dn, isFinal);
        var middleBand = (upperBand + lowerBand) / 2;

        if (isFinal)
        {
            _prevUp = up;
            _prevDn = dn;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upperBand },
                { "MiddleBand", middleBand },
                { "LowerBand", lowerBand }
            };
        }

        return new StreamingIndicatorStateResult(middleBand, outputs);
    }

    public void Dispose()
    {
        _volumeMa.Dispose();
        _upMa.Dispose();
        _downMa.Dispose();
    }
}

public sealed class VolumeAdjustedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double _factor;
    private readonly IMovingAverageSmoother _volumeSma;
    private readonly RollingWindowSum _volumeRatioSum;
    private readonly RollingWindowSum _priceVolumeRatioSum;
    private readonly StreamingInputResolver _input;

    public VolumeAdjustedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double factor = 0.67, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _factor = factor;
        _volumeSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _volumeRatioSum = new RollingWindowSum(resolved);
        _priceVolumeRatioSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeAdjustedMovingAverageState(MovingAvgType maType, int length, double factor, InputName inputName,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _factor = factor;
        _volumeSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _volumeRatioSum = new RollingWindowSum(resolved);
        _priceVolumeRatioSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeAdjustedMovingAverage;

    public void Reset()
    {
        _volumeSma.Reset();
        _volumeRatioSum.Reset();
        _priceVolumeRatioSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volumeSma = _volumeSma.Next(bar.Volume, isFinal);
        var volumeIncrement = volumeSma * _factor;
        var volumeRatio = volumeIncrement != 0 ? bar.Volume / volumeIncrement : 0;
        var volumeRatioSum = isFinal ? _volumeRatioSum.Add(volumeRatio, out _) : _volumeRatioSum.Preview(volumeRatio, out _);
        var priceVolumeRatio = value * volumeRatio;
        var priceVolumeRatioSum = isFinal ? _priceVolumeRatioSum.Add(priceVolumeRatio, out _) : _priceVolumeRatioSum.Preview(priceVolumeRatio, out _);
        var vama = volumeRatioSum != 0 ? priceVolumeRatioSum / volumeRatioSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vama", vama }
            };
        }

        return new StreamingIndicatorStateResult(vama, outputs);
    }

    public void Dispose()
    {
        _volumeSma.Dispose();
        _volumeRatioSum.Dispose();
        _priceVolumeRatioSum.Dispose();
    }
}

internal sealed class VariableMovingAverageEngine : IDisposable
{
    private readonly double _k;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private double _prevValue;
    private double _prevPdms;
    private double _prevMdms;
    private double _prevPdis;
    private double _prevMdis;
    private double _prevIs;
    private double _prevVma;
    private bool _hasPrev;

    public VariableMovingAverageEngine(int length)
    {
        var resolved = Math.Max(1, length);
        _k = 1d / resolved;
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
    }

    public double Next(double value, bool isFinal)
    {
        var pdm = _hasPrev ? Math.Max(value - _prevValue, 0) : 0;
        var mdm = _hasPrev ? Math.Max(_prevValue - value, 0) : 0;

        var pdmS = ((1 - _k) * _prevPdms) + (_k * pdm);
        var mdmS = ((1 - _k) * _prevMdms) + (_k * mdm);

        var s = pdmS + mdmS;
        var pdi = s != 0 ? pdmS / s : 0;
        var mdi = s != 0 ? mdmS / s : 0;

        var pdiS = ((1 - _k) * _prevPdis) + (_k * pdi);
        var mdiS = ((1 - _k) * _prevMdis) + (_k * mdi);

        var d = Math.Abs(pdiS - mdiS);
        var s1 = pdiS + mdiS;
        var dS1 = s1 != 0 ? d / s1 : 0;

        var iS = ((1 - _k) * _prevIs) + (_k * dS1);
        var hhv = isFinal ? _maxWindow.Add(iS, out _) : _maxWindow.Preview(iS, out _);
        var llv = isFinal ? _minWindow.Add(iS, out _) : _minWindow.Preview(iS, out _);
        var d1 = hhv - llv;
        var vI = d1 != 0 ? (iS - llv) / d1 : 0;
        var vma = ((1 - _k) * vI * _prevVma) + (_k * vI * value);

        if (isFinal)
        {
            _prevValue = value;
            _prevPdms = pdmS;
            _prevMdms = mdmS;
            _prevPdis = pdiS;
            _prevMdis = mdiS;
            _prevIs = iS;
            _prevVma = vma;
            _hasPrev = true;
        }

        return vma;
    }

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _prevValue = 0;
        _prevPdms = 0;
        _prevMdms = 0;
        _prevPdis = 0;
        _prevMdis = 0;
        _prevIs = 0;
        _prevVma = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}
