using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class ComparePriceMomentumOscillatorState : IMultiSeriesIndicatorState
{
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly PriceMomentumOscillatorEngine _primaryEngine;
    private readonly PriceMomentumOscillatorEngine _marketEngine;
    private double _lastMarket;
    private bool _hasMarket;

    public ComparePriceMomentumOscillatorState(SeriesKey primarySeries, SeriesKey marketSeries,
        int length1 = 20, int length2 = 35)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        _primaryEngine = new PriceMomentumOscillatorEngine(length1, length2);
        _marketEngine = new PriceMomentumOscillatorEngine(length1, length2);
    }

    public IndicatorName Name => IndicatorName.ComparePriceMomentumOscillator;

    public void Reset()
    {
        _primaryEngine.Reset();
        _marketEngine.Reset();
        _lastMarket = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (series.Equals(_primarySeries))
        {
            var primaryPmo = _primaryEngine.Next(bar.Close, isFinal);
            if (!_hasMarket)
            {
                return new MultiSeriesIndicatorStateResult(false, 0d, null);
            }

            var cpmo = primaryPmo - _lastMarket;
            IReadOnlyDictionary<string, double>? outputs = null;
            if (includeOutputs)
            {
                outputs = new Dictionary<string, double>(1)
                {
                    { "Cpmo", cpmo }
                };
            }

            return new MultiSeriesIndicatorStateResult(true, cpmo, outputs);
        }

        if (series.Equals(_marketSeries))
        {
            var marketPmo = _marketEngine.Next(bar.Close, isFinal);
            _lastMarket = marketPmo;
            _hasMarket = true;
        }

        return new MultiSeriesIndicatorStateResult(false, 0d, null);
    }
}

public sealed class ConfluenceIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _stl;
    private readonly int _itl;
    private readonly int _ltl;
    private readonly int _hoff;
    private readonly int _soff;
    private readonly int _ioff;
    private readonly int _hLength;
    private readonly int _sLength;
    private readonly int _iLength;
    private readonly int _lLength;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _hAvg;
    private readonly IMovingAverageSmoother _sAvg;
    private readonly IMovingAverageSmoother _iAvg;
    private readonly IMovingAverageSmoother _lAvg;
    private readonly IMovingAverageSmoother _h2Avg;
    private readonly IMovingAverageSmoother _s2Avg;
    private readonly IMovingAverageSmoother _i2Avg;
    private readonly IMovingAverageSmoother _l2Avg;
    private readonly IMovingAverageSmoother _ftpAvg;
    private readonly PooledRingBuffer<double> _hAvgValues;
    private readonly PooledRingBuffer<double> _sAvgValues;
    private readonly PooledRingBuffer<double> _iAvgValues;
    private readonly PooledRingBuffer<double> _lAvgValues;
    private readonly PooledRingBuffer<double> _value5Values;
    private readonly PooledRingBuffer<double> _value6Values;
    private readonly PooledRingBuffer<double> _value7Values;
    private readonly PooledRingBuffer<double> _sumValues;
    private readonly RollingWindowSum _errSumWindow;
    private readonly RollingWindowSum _value70Window;
    private double _prevErrSum;
    private double _prevMom;
    private double _prevValue70;

    public ConfluenceIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.FullTypicalPrice, int length = 10)
    {
        _length = Math.Max(1, length);
        _stl = (int)Math.Ceiling((_length * 2) - 1 - 0.5m);
        _itl = (int)Math.Ceiling((_stl * 2) - 1 - 0.5m);
        _ltl = (int)Math.Ceiling((_itl * 2) - 1 - 0.5m);
        _hoff = (int)Math.Ceiling(((double)_length / 2) - 0.5);
        _soff = (int)Math.Ceiling(((double)_stl / 2) - 0.5);
        _ioff = (int)Math.Ceiling(((double)_itl / 2) - 0.5);
        _hLength = MathHelper.MinOrMax(_length - 1);
        _sLength = _stl - 1;
        _iLength = _itl - 1;
        _lLength = _ltl - 1;

        _hAvg = MovingAverageSmootherFactory.Create(maType, _length);
        _sAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _stl));
        _iAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _itl));
        _lAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _ltl));
        _h2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _hLength));
        _s2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _sLength));
        _i2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _iLength));
        _l2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _lLength));
        _ftpAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _lLength));
        _input = new StreamingInputResolver(inputName, null);

        var maxOffset = Math.Max(Math.Max(_hoff, _soff), _ioff);
        var capacity = Math.Max(1, maxOffset);
        _hAvgValues = new PooledRingBuffer<double>(capacity);
        _sAvgValues = new PooledRingBuffer<double>(capacity);
        _iAvgValues = new PooledRingBuffer<double>(capacity);
        _lAvgValues = new PooledRingBuffer<double>(capacity);
        _value5Values = new PooledRingBuffer<double>(capacity);
        _value6Values = new PooledRingBuffer<double>(capacity);
        _value7Values = new PooledRingBuffer<double>(capacity);
        _sumValues = new PooledRingBuffer<double>(capacity);
        _errSumWindow = new RollingWindowSum(Math.Max(1, _soff));
        _value70Window = new RollingWindowSum(Math.Max(1, _length));
    }

    public ConfluenceIndicatorState(MovingAvgType maType, InputName inputName, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stl = (int)Math.Ceiling((_length * 2) - 1 - 0.5m);
        _itl = (int)Math.Ceiling((_stl * 2) - 1 - 0.5m);
        _ltl = (int)Math.Ceiling((_itl * 2) - 1 - 0.5m);
        _hoff = (int)Math.Ceiling(((double)_length / 2) - 0.5);
        _soff = (int)Math.Ceiling(((double)_stl / 2) - 0.5);
        _ioff = (int)Math.Ceiling(((double)_itl / 2) - 0.5);
        _hLength = MathHelper.MinOrMax(_length - 1);
        _sLength = _stl - 1;
        _iLength = _itl - 1;
        _lLength = _ltl - 1;

        _hAvg = MovingAverageSmootherFactory.Create(maType, _length);
        _sAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _stl));
        _iAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _itl));
        _lAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _ltl));
        _h2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _hLength));
        _s2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _sLength));
        _i2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _iLength));
        _l2Avg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _lLength));
        _ftpAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, _lLength));
        _input = new StreamingInputResolver(inputName, selector);

        var maxOffset = Math.Max(Math.Max(_hoff, _soff), _ioff);
        var capacity = Math.Max(1, maxOffset);
        _hAvgValues = new PooledRingBuffer<double>(capacity);
        _sAvgValues = new PooledRingBuffer<double>(capacity);
        _iAvgValues = new PooledRingBuffer<double>(capacity);
        _lAvgValues = new PooledRingBuffer<double>(capacity);
        _value5Values = new PooledRingBuffer<double>(capacity);
        _value6Values = new PooledRingBuffer<double>(capacity);
        _value7Values = new PooledRingBuffer<double>(capacity);
        _sumValues = new PooledRingBuffer<double>(capacity);
        _errSumWindow = new RollingWindowSum(Math.Max(1, _soff));
        _value70Window = new RollingWindowSum(Math.Max(1, _length));
    }

    public IndicatorName Name => IndicatorName.ConfluenceIndicator;

    public void Reset()
    {
        _hAvg.Reset();
        _sAvg.Reset();
        _iAvg.Reset();
        _lAvg.Reset();
        _h2Avg.Reset();
        _s2Avg.Reset();
        _i2Avg.Reset();
        _l2Avg.Reset();
        _ftpAvg.Reset();
        _hAvgValues.Clear();
        _sAvgValues.Clear();
        _iAvgValues.Clear();
        _lAvgValues.Clear();
        _value5Values.Clear();
        _value6Values.Clear();
        _value7Values.Clear();
        _sumValues.Clear();
        _errSumWindow.Reset();
        _value70Window.Reset();
        _prevErrSum = 0;
        _prevMom = 0;
        _prevValue70 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var inputValue = _input.GetValue(bar);
        var close = bar.Close;

        var hAvg = _hAvg.Next(close, isFinal);
        var sAvg = _sAvg.Next(close, isFinal);
        var iAvg = _iAvg.Next(close, isFinal);
        var lAvg = _lAvg.Next(close, isFinal);
        var h2 = _h2Avg.Next(close, isFinal);
        var s2 = _s2Avg.Next(close, isFinal);
        var i2 = _i2Avg.Next(close, isFinal);
        var l2 = _l2Avg.Next(close, isFinal);
        var ftpAvg = _ftpAvg.Next(inputValue, isFinal);

        var priorSAvg = GetOffsetValue(_sAvgValues, _soff);
        var priorHAvg = GetOffsetValue(_hAvgValues, _hoff);
        var priorIAvg = GetOffsetValue(_iAvgValues, _ioff);
        var priorValue5 = GetOffsetValue(_value5Values, _hoff);
        var priorValue6 = GetOffsetValue(_value6Values, _soff);
        var priorValue7 = GetOffsetValue(_value7Values, _ioff);
        var priorSum = GetOffsetValue(_sumValues, _soff);
        var priorHAvg2 = GetOffsetValue(_hAvgValues, _soff);

        var prevSAvg = _sAvgValues.Count > 0 ? _sAvgValues[_sAvgValues.Count - 1] : 0;
        var prevHAvg = _hAvgValues.Count > 0 ? _hAvgValues[_hAvgValues.Count - 1] : 0;
        var prevIAvg = _iAvgValues.Count > 0 ? _iAvgValues[_iAvgValues.Count - 1] : 0;
        var prevLAvg = _lAvgValues.Count > 0 ? _lAvgValues[_lAvgValues.Count - 1] : 0;

        var value2 = sAvg - priorHAvg;
        var value3 = iAvg - priorSAvg;
        var value12 = lAvg - priorIAvg;
        var momSig = value2 + value3 + value12;
        var derivH = (hAvg * 2) - prevHAvg;
        var derivS = (sAvg * 2) - prevSAvg;
        var derivI = (iAvg * 2) - prevIAvg;
        var derivL = (lAvg * 2) - prevLAvg;
        var sumDH = _length * derivH;
        var sumDS = _stl * derivS;
        var sumDI = _itl * derivI;
        var sumDL = _ltl * derivL;
        var n1h = h2 * _hLength;
        var n1s = s2 * _sLength;
        var n1i = i2 * _iLength;
        var n1l = l2 * _lLength;
        var drh = sumDH - n1h;
        var drs = sumDS - n1s;
        var dri = sumDI - n1i;
        var drl = sumDL - n1l;
        var hSum = h2 * (_length - 1);
        var sSum = s2 * (_stl - 1);
        var iSum = i2 * (_itl - 1);
        var lSum = ftpAvg * (_ltl - 1);

        var value5 = _length != 0 ? (hSum + drh) / _length : 0;
        var value6 = _stl != 0 ? (sSum + drs) / _stl : 0;
        var value7 = _itl != 0 ? (iSum + dri) / _itl : 0;
        var value13 = _ltl != 0 ? (lSum + drl) / _ltl : 0;
        var value9 = value6 - priorValue5;
        var value10 = value7 - priorValue6;
        var value14 = value13 - priorValue7;
        var mom = value9 + value10 + value14;

        var ht = Math.Sin(value5 * 2 * Math.PI / 360) + Math.Cos(value5 * 2 * Math.PI / 360);
        var hta = Math.Sin(hAvg * 2 * Math.PI / 360) + Math.Cos(hAvg * 2 * Math.PI / 360);
        var st = Math.Sin(value6 * 2 * Math.PI / 360) + Math.Cos(value6 * 2 * Math.PI / 360);
        var sta = Math.Sin(sAvg * 2 * Math.PI / 360) + Math.Cos(sAvg * 2 * Math.PI / 360);
        var it = Math.Sin(value7 * 2 * Math.PI / 360) + Math.Cos(value7 * 2 * Math.PI / 360);
        var ita = Math.Sin(iAvg * 2 * Math.PI / 360) + Math.Cos(iAvg * 2 * Math.PI / 360);

        var sum = ht + st + it;
        var err = hta + sta + ita;
        double cond2 = (sum > priorSum && hAvg < priorHAvg2) || (sum < priorSum && hAvg > priorHAvg2) ? 1 : 0;
        double phase = cond2 == 1 ? -1 : 1;

        var errSumValue = (sum - err) * phase;
        var errSumTotal = isFinal
            ? _errSumWindow.Add(errSumValue, out var errCount)
            : _errSumWindow.Preview(errSumValue, out errCount);
        var errSig = _soff > 0 && errCount > 0 ? errSumTotal / errCount : 0;

        var value70 = value5 - value13;
        var value70Total = isFinal
            ? _value70Window.Add(value70, out var value70Count)
            : _value70Window.Preview(value70, out value70Count);
        var value71 = value70Count > 0 ? value70Total / value70Count : 0;

        double errNum = errSumValue > 0 && errSumValue < _prevErrSum && errSumValue < errSig ? 1 :
            errSumValue > 0 && errSumValue < _prevErrSum && errSumValue > errSig ? 2 :
            errSumValue > 0 && errSumValue > _prevErrSum && errSumValue < errSig ? 2 :
            errSumValue > 0 && errSumValue > _prevErrSum && errSumValue > errSig ? 3 :
            errSumValue < 0 && errSumValue > _prevErrSum && errSumValue > errSig ? -1 :
            errSumValue < 0 && errSumValue < _prevErrSum && errSumValue > errSig ? -2 :
            errSumValue < 0 && errSumValue > _prevErrSum && errSumValue < errSig ? -2 :
            errSumValue < 0 && errSumValue < _prevErrSum && errSumValue < errSig ? -3 : 0;

        double momNum = mom > 0 && mom < _prevMom && mom < momSig ? 1 :
            mom > 0 && mom < _prevMom && mom > momSig ? 2 :
            mom > 0 && mom > _prevMom && mom < momSig ? 2 :
            mom > 0 && mom > _prevMom && mom > momSig ? 3 :
            mom < 0 && mom > _prevMom && mom > momSig ? -1 :
            mom < 0 && mom < _prevMom && mom > momSig ? -2 :
            mom < 0 && mom > _prevMom && mom < momSig ? -2 :
            mom < 0 && mom < _prevMom && mom < momSig ? -3 : 0;

        double tcNum = value70 > 0 && value70 < _prevValue70 && value70 < value71 ? 1 :
            value70 > 0 && value70 < _prevValue70 && value70 > value71 ? 2 :
            value70 > 0 && value70 > _prevValue70 && value70 < value71 ? 2 :
            value70 > 0 && value70 > _prevValue70 && value70 > value71 ? 3 :
            value70 < 0 && value70 > _prevValue70 && value70 > value71 ? -1 :
            value70 < 0 && value70 < _prevValue70 && value70 > value71 ? -2 :
            value70 < 0 && value70 > _prevValue70 && value70 < value71 ? -2 :
            value70 < 0 && value70 < _prevValue70 && value70 < value71 ? -3 : 0;

        var value42 = errNum + momNum + tcNum;

        var confluence = value42 > 0 && value70 > 0 ? value42 :
            value42 < 0 && value70 < 0 ? value42 :
            (value42 > 0 && value70 < 0) || (value42 < 0 && value70 > 0) ? value42 / 10 : 0;

        if (isFinal)
        {
            _hAvgValues.TryAdd(hAvg, out _);
            _sAvgValues.TryAdd(sAvg, out _);
            _iAvgValues.TryAdd(iAvg, out _);
            _lAvgValues.TryAdd(lAvg, out _);
            _value5Values.TryAdd(value5, out _);
            _value6Values.TryAdd(value6, out _);
            _value7Values.TryAdd(value7, out _);
            _sumValues.TryAdd(sum, out _);
            _prevErrSum = errSumValue;
            _prevMom = mom;
            _prevValue70 = value70;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ci", confluence }
            };
        }

        return new StreamingIndicatorStateResult(confluence, outputs);
    }

    public void Dispose()
    {
        _hAvg.Dispose();
        _sAvg.Dispose();
        _iAvg.Dispose();
        _lAvg.Dispose();
        _h2Avg.Dispose();
        _s2Avg.Dispose();
        _i2Avg.Dispose();
        _l2Avg.Dispose();
        _ftpAvg.Dispose();
        _hAvgValues.Dispose();
        _sAvgValues.Dispose();
        _iAvgValues.Dispose();
        _lAvgValues.Dispose();
        _value5Values.Dispose();
        _value6Values.Dispose();
        _value7Values.Dispose();
        _sumValues.Dispose();
        _errSumWindow.Dispose();
        _value70Window.Dispose();
    }

    private static double GetOffsetValue(PooledRingBuffer<double> buffer, int offset)
    {
        if (offset <= 0 || buffer.Count < offset)
        {
            return 0;
        }

        return buffer[buffer.Count - offset];
    }
}

public sealed class DampedSineWaveWeightedFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public DampedSineWaveWeightedFilterState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var w = Math.Sin(MathHelper.MinOrMax(2 * Math.PI * ((double)j / _length), 0.99, 0.01)) / j;
            _weights[j - 1] = w;
            sum += w;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DampedSineWaveWeightedFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var w = Math.Sin(MathHelper.MinOrMax(2 * Math.PI * ((double)j / _length), 0.99, 0.01)) / j;
            _weights[j - 1] = w;
            sum += w;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DampedSineWaveWeightedFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double wvSum = 0;
        for (var j = 0; j < _length; j++)
        {
            double sample;
            if (j == 0)
            {
                sample = value;
            }
            else if (_values.Count >= j)
            {
                sample = _values[_values.Count - j];
            }
            else
            {
                sample = 0;
            }

            wvSum += _weights[j] * sample;
        }

        var dswwf = _weightSum != 0 ? wvSum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dswwf", dswwf }
            };
        }

        return new StreamingIndicatorStateResult(dswwf, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class DecisionPointBreadthSwenlinTradingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _firstSmoother;
    private readonly IMovingAverageSmoother _secondSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public DecisionPointBreadthSwenlinTradingOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DecisionPointBreadthSwenlinTradingOscillatorState(MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _firstSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _secondSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DecisionPointBreadthSwenlinTradingOscillator;

    public void Reset()
    {
        _firstSmoother.Reset();
        _secondSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        double advance = value > prevValue ? 1 : 0;
        double decline = value < prevValue ? 1 : 0;
        var iVal = advance + decline != 0 ? 1000 * (advance - decline) / (advance + decline) : 0;

        var ema = _firstSmoother.Next(iVal, isFinal);
        var sto = _secondSmoother.Next(ema, isFinal);
        var signal = _signalSmoother.Next(sto, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dpbsto", sto },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(sto, outputs);
    }

    public void Dispose()
    {
        _firstSmoother.Dispose();
        _secondSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class DominantCycleTunedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCyberCyclePeriodState _periodState;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevA;
    private double _prevB;
    private bool _hasPrev;

    public DominantCycleTunedRelativeStrengthIndexState(int length = 5, InputName inputName = InputName.Close)
    {
        _periodState = new AdaptiveCyberCyclePeriodState(length, 0.07);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DominantCycleTunedRelativeStrengthIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _periodState = new AdaptiveCyberCyclePeriodState(length, 0.07);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DominantCycleTunedRelativeStrengthIndex;

    public void Reset()
    {
        _periodState.Reset();
        _prevValue = 0;
        _prevA = 0;
        _prevB = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var period = _periodState.Next(value, isFinal);
        var p = period != 0 ? 1 / period : 0.07;
        var prevValue = _hasPrev ? _prevValue : 0;
        var aChg = value > prevValue ? Math.Abs(value - prevValue) : 0;
        var bChg = value < prevValue ? Math.Abs(value - prevValue) : 0;

        var prevA = _hasPrev ? _prevA : aChg;
        var a = (p * aChg) + ((1 - p) * prevA);

        var prevB = _hasPrev ? _prevB : bChg;
        var b = (p * bChg) + ((1 - p) * prevB);

        var r = b != 0 ? a / b : 0;
        var rsi = b == 0 ? 100 : a == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + r)), 100, 0);

        if (isFinal)
        {
            _prevValue = value;
            _prevA = a;
            _prevB = b;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "DctRsi", rsi }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _periodState.Dispose();
    }
}

public sealed class DrunkardWalkState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;
    private readonly int _length2;
    private double _prevClose;
    private double _prevAtrUp;
    private double _prevAtrDn;
    private bool _hasPrev;

    public DrunkardWalkState(int length1 = 80, int length2 = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highs = new PooledRingBuffer<double>(resolved);
        _lows = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(inputName, null);
        _length2 = length2;
    }

    public DrunkardWalkState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highs = new PooledRingBuffer<double>(resolved);
        _lows = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _length2 = length2;
    }

    public IndicatorName Name => IndicatorName.DrunkardWalk;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highs.Clear();
        _lows.Clear();
        _prevClose = 0;
        _prevAtrUp = 0;
        _prevAtrDn = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var dnRun = StreamingWindowMath.LastOffset(_highs, bar.High, highestHigh);
        var upRun = StreamingWindowMath.LastOffset(_lows, bar.Low, lowestLow);

        var upK = upRun != 0 ? (double)1 / upRun : 0;
        var atrUp = (tr * upK) + (_prevAtrUp * (1 - upK));

        var dnK = dnRun != 0 ? (double)1 / dnRun : 0;
        var atrDn = (tr * dnK) + (_prevAtrDn * (1 - dnK));

        var upDen = atrUp > 0 ? atrUp : 1;
        var upWalk = upRun > 0 ? (bar.High - lowestLow) / (MathHelper.Sqrt(upRun) * upDen) : 0;

        var dnDen = atrDn > 0 ? atrDn : 1;
        var dnWalk = dnRun > 0 ? (highestHigh - bar.Low) / (MathHelper.Sqrt(dnRun) * dnDen) : 0;

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevClose = bar.Close;
            _prevAtrUp = atrUp;
            _prevAtrDn = atrDn;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpWalk", upWalk },
                { "DnWalk", dnWalk }
            };
        }

        return new StreamingIndicatorStateResult(upWalk, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highs.Dispose();
        _lows.Dispose();
    }
}

public sealed class DynamicallyAdjustableFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _srcWindow;
    private readonly RollingWindowSum _srcDevWindow;
    private readonly StreamingInputResolver _input;
    private double _prevOut;
    private double _prevK;
    private bool _hasPrev;

    public DynamicallyAdjustableFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _srcWindow = new RollingWindowSum(_length);
        _srcDevWindow = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DynamicallyAdjustableFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _srcWindow = new RollingWindowSum(_length);
        _srcDevWindow = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DynamicallyAdjustableFilter;

    public void Reset()
    {
        _srcWindow.Reset();
        _srcDevWindow.Reset();
        _prevOut = 0;
        _prevK = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevOut = _hasPrev ? _prevOut : value;
        var prevK = _hasPrev ? _prevK : 0;

        var src = value + (value - prevOut);
        var outVal = prevOut + (prevK * (src - prevOut));

        var srcSum = isFinal ? _srcWindow.Add(src, out var srcCount) : _srcWindow.Preview(src, out srcCount);
        var srcSma = srcCount > 0 ? srcSum / srcCount : 0;
        var srcDev = (src - srcSma) * (src - srcSma);
        var srcDevSum = isFinal ? _srcDevWindow.Add(srcDev, out var devCount) : _srcDevWindow.Preview(srcDev, out devCount);
        var srcStdDev = devCount > 0 ? Math.Sqrt(srcDevSum / devCount) : 0;

        var diff = Math.Abs(src - outVal);
        var k = diff != 0 ? diff / (diff + (srcStdDev * _length)) : 0;

        if (isFinal)
        {
            _prevOut = outVal;
            _prevK = k;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Daf", outVal }
            };
        }

        return new StreamingIndicatorStateResult(outVal, outputs);
    }

    public void Dispose()
    {
        _srcWindow.Dispose();
        _srcDevWindow.Dispose();
    }
}

public sealed class DynamicallyAdjustableMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly StandardDeviationVolatilityState _fastStdDev;
    private readonly StandardDeviationVolatilityState _slowStdDev;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _kValues;
    private double _tempSum;
    private double _fastStdDevValue;

    public DynamicallyAdjustableMovingAverageState(int fastLength = 6, int slowLength = 200,
        InputName inputName = InputName.Close)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _fastLength, inputName);
        _slowStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _slowLength, _ => _fastStdDevValue);
        _input = new StreamingInputResolver(inputName, null);
        _kValues = new PooledRingBuffer<double>(_slowLength);
    }

    public DynamicallyAdjustableMovingAverageState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _fastLength, selector);
        _slowStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _slowLength, _ => _fastStdDevValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _kValues = new PooledRingBuffer<double>(_slowLength);
    }

    public IndicatorName Name => IndicatorName.DynamicallyAdjustableMovingAverage;

    public void Reset()
    {
        _fastStdDev.Reset();
        _slowStdDev.Reset();
        _kValues.Clear();
        _tempSum = 0;
        _fastStdDevValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fastStdDev = _fastStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevFastStdDev = _fastStdDevValue;
        _fastStdDevValue = fastStdDev;
        var slowStdDev = _slowStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        if (!isFinal)
        {
            _fastStdDevValue = prevFastStdDev;
        }
        var v = fastStdDev != 0 ? (slowStdDev / fastStdDev) + _fastLength : _fastLength;
        var p = (int)Math.Round(MathHelper.MinOrMax(v, _slowLength, _fastLength));

        var tempSum = _tempSum + value;
        var prevK = _kValues.Count >= p ? _kValues[_kValues.Count - p] : 0;
        var ama = p != 0 ? (tempSum - prevK) / p : 0;

        if (isFinal)
        {
            _tempSum = tempSum;
            _kValues.TryAdd(tempSum, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dama", ama }
            };
        }

        return new StreamingIndicatorStateResult(ama, outputs);
    }

    public void Dispose()
    {
        _fastStdDev.Dispose();
        _slowStdDev.Dispose();
        _kValues.Dispose();
    }
}

public sealed class DynamicMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length3;
    private readonly int _upLimit;
    private readonly int _dnLimit;
    private readonly StandardDeviationVolatilityState _stdDevState;
    private readonly IMovingAverageSmoother _stdDevSmoother;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _gains;
    private readonly PooledRingBuffer<double> _losses;
    private readonly PooledRingBuffer<double> _dmiValues;
    private double _prevValue;
    private bool _hasPrev;

    public DynamicMomentumIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5,
        int length2 = 10, int length3 = 14, int upLimit = 30, int dnLimit = 5, InputName inputName = InputName.Close)
    {
        _length3 = Math.Max(1, length3);
        _upLimit = Math.Max(1, upLimit);
        _dnLimit = Math.Max(1, dnLimit);
        var capacity = Math.Max(1, Math.Max(_upLimit, _dnLimit));
        _stdDevState = new StandardDeviationVolatilityState(maType, Math.Max(1, length1), inputName);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
        _gains = new PooledRingBuffer<double>(capacity);
        _losses = new PooledRingBuffer<double>(capacity);
        _dmiValues = new PooledRingBuffer<double>(capacity);
    }

    public DynamicMomentumIndexState(MovingAvgType maType, int length1, int length2, int length3, int upLimit,
        int dnLimit, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length3 = Math.Max(1, length3);
        _upLimit = Math.Max(1, upLimit);
        _dnLimit = Math.Max(1, dnLimit);
        var capacity = Math.Max(1, Math.Max(_upLimit, _dnLimit));
        _stdDevState = new StandardDeviationVolatilityState(maType, Math.Max(1, length1), selector);
        _stdDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _gains = new PooledRingBuffer<double>(capacity);
        _losses = new PooledRingBuffer<double>(capacity);
        _dmiValues = new PooledRingBuffer<double>(capacity);
    }

    public IndicatorName Name => IndicatorName.DynamicMomentumIndex;

    public void Reset()
    {
        _stdDevState.Reset();
        _stdDevSmoother.Reset();
        _gains.Clear();
        _losses.Clear();
        _dmiValues.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceChg = _hasPrev ? value - prevValue : 0;

        var stdDev = _stdDevState.Update(bar, isFinal, includeOutputs: false).Value;
        var asd = _stdDevSmoother.Next(stdDev, isFinal);

        int dTime;
        try
        {
            dTime = asd != 0 ? Math.Min(_upLimit, (int)Math.Ceiling(_length3 / asd)) : 0;
        }
        catch
        {
            dTime = _upLimit;
        }

        var dmiLength = Math.Max(Math.Min(dTime, _upLimit), _dnLimit);

        var loss = _hasPrev && priceChg < 0 ? Math.Abs(priceChg) : 0;
        var gain = _hasPrev && priceChg > 0 ? priceChg : 0;

        var gainSum = StreamingWindowMath.SumRecent(_gains, gain, dmiLength, out var gainCount);
        var lossSum = StreamingWindowMath.SumRecent(_losses, loss, dmiLength, out var lossCount);
        var avgGain = gainCount > 0 ? gainSum / gainCount : 0;
        var avgLoss = lossCount > 0 ? lossSum / lossCount : 0;
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;

        var dmi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : 100 - (100 / (1 + rs));

        var dmiSum = StreamingWindowMath.SumRecent(_dmiValues, dmi, dmiLength, out var dmiCount);
        var dmiSignal = dmiCount > 0 ? dmiSum / dmiCount : 0;
        var histogram = dmi - dmiSignal;

        if (isFinal)
        {
            _gains.TryAdd(gain, out _);
            _losses.TryAdd(loss, out _);
            _dmiValues.TryAdd(dmi, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Dmi", dmi },
                { "Signal", dmiSignal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(dmi, outputs);
    }

    public void Dispose()
    {
        _stdDevState.Dispose();
        _stdDevSmoother.Dispose();
        _gains.Dispose();
        _losses.Dispose();
        _dmiValues.Dispose();
    }
}

public sealed class DynamicMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;
    private double _highest;
    private double _lowest;
    private bool _hasStoch;

    public DynamicMomentumOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10,
        int length2 = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DynamicMomentumOscillatorState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DynamicMomentumOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _highest = 0;
        _lowest = 0;
        _hasStoch = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((value - lowestLow) / range * 100, 100, 0) : 0;

        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        var prevHighest = _hasStoch ? _highest : 0;
        var highest = fastD > prevHighest ? fastD : prevHighest;
        var prevLowest = _hasStoch ? _lowest : double.MaxValue;
        var lowest = fastD < prevLowest ? fastD : prevLowest;

        var midpoint = MathHelper.MinOrMax((lowest + highest) / 2, 100, 0);
        var dmo = MathHelper.MinOrMax(midpoint - (slowD - fastD), 100, 0);

        if (isFinal)
        {
            _highest = highest;
            _lowest = lowest;
            _hasStoch = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dmo", dmo }
            };
        }

        return new StreamingIndicatorStateResult(dmo, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class DynamicPivotPointsState : IStreamingIndicatorState
{
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.DynamicPivotPoints;

    public void Reset()
    {
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

        var pivot = (prevHigh + prevLow + prevClose) / 3;
        var support = pivot - (prevHigh - pivot);
        var resistance = pivot + (pivot - prevLow);

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
            outputs = new Dictionary<string, double>(3)
            {
                { "Pivot", pivot },
                { "S1", support },
                { "R1", resistance }
            };
        }

        return new StreamingIndicatorStateResult(pivot, outputs);
    }
}

public sealed class EarningSupportResistanceLevelsState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _inputValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public EarningSupportResistanceLevelsState(InputName inputName = InputName.MedianPrice)
    {
        _inputValues = new PooledRingBuffer<double>(2);
        _lowValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EarningSupportResistanceLevelsState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _inputValues = new PooledRingBuffer<double>(2);
        _lowValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EarningSupportResistanceLevels;

    public void Reset()
    {
        _inputValues.Clear();
        _lowValues.Clear();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevLow = _lowValues.Count >= 2 ? _lowValues[_lowValues.Count - 2] : 0;
        var prevValue2 = _inputValues.Count >= 2 ? _inputValues[_inputValues.Count - 2] : 0;

        var mode1 = (prevLow + bar.High) / 2;
        var mode2 = (prevValue2 + currentValue + prevClose) / 3;

        if (isFinal)
        {
            _inputValues.TryAdd(currentValue, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Esr", mode1 }
            };
        }

        return new StreamingIndicatorStateResult(mode1, outputs);
    }

    public void Dispose()
    {
        _inputValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class EdgePreservingFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly LinearRegressionState _regression;
    private readonly RollingWindowMax _maxWindow;
    private readonly StreamingInputResolver _input;
    private double _regressionInput;
    private double _prevA;
    private double _prevB;
    private double _prevH;
    private bool _hasPrev;

    public EdgePreservingFilterState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200,
        int smoothLength = 50, InputName inputName = InputName.Close)
    {
        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _regression = new LinearRegressionState(Math.Max(1, smoothLength), _ => _regressionInput);
        _maxWindow = new RollingWindowMax(Math.Max(2, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EdgePreservingFilterState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _regression = new LinearRegressionState(Math.Max(1, smoothLength), _ => _regressionInput);
        _maxWindow = new RollingWindowMax(Math.Max(2, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EdgePreservingFilter;

    public void Reset()
    {
        _sma.Reset();
        _regression.Reset();
        _maxWindow.Reset();
        _prevA = 0;
        _prevB = 0;
        _prevH = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var sma = _sma.Next(currentValue, isFinal);
        var os = currentValue - sma;
        var absOs = Math.Abs(os);

        _regressionInput = absOs;
        var p = _regression.Update(bar, isFinal, includeOutputs: false).Value;
        var highest = isFinal ? _maxWindow.Add(p, out _) : _maxWindow.Preview(p, out _);

        var prevH = _hasPrev ? _prevH : 0;
        var h = highest != 0 ? p / highest : 0;
        double cnd = h == 1 && prevH != 1 ? 1 : 0;
        double sign = cnd == 1 && os < 0 ? 1 : cnd == 1 && os > 0 ? -1 : 0;
        var condition = sign != 0;

        var prevA = _hasPrev ? _prevA : 1;
        var a = condition ? 1 : prevA + 1;
        var prevB = _hasPrev ? _prevB : currentValue;
        var b = a == 1 ? currentValue : prevB + currentValue;
        var c = a != 0 ? b / a : 0;

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _prevH = h;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Epf", c }
            };
        }

        return new StreamingIndicatorStateResult(c, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _regression.Dispose();
        _maxWindow.Dispose();
    }
}

public sealed class EfficientAutoLineState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private double _prevA;
    private bool _hasPrev;
    private int _index;

    public EfficientAutoLineState(int length = 19, double fastAlpha = 0.0001, double slowAlpha = 0.005,
        InputName inputName = InputName.Close)
    {
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
        _input = new StreamingInputResolver(inputName, null);
    }

    public EfficientAutoLineState(int length, double fastAlpha, double slowAlpha, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _er = new EfficiencyRatioState(Math.Max(1, length));
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EfficientAutoLine;

    public void Reset()
    {
        _er.Reset();
        _prevA = 0;
        _hasPrev = false;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var dev = (er * _fastAlpha) + ((1 - er) * _slowAlpha);

        var prevA = _hasPrev ? _prevA : 0;
        var a = _index < 9 ? value : value > prevA + dev ? value : value < prevA - dev ? value : prevA;

        if (isFinal)
        {
            _prevA = a;
            _hasPrev = true;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eal", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

public sealed class EfficientPriceState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly EfficiencyRatioState _er;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _chgErSum;
    private int _index;

    public EfficientPriceState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _er = new EfficiencyRatioState(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EfficientPriceState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _er = new EfficiencyRatioState(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EfficientPrice;

    public void Reset()
    {
        _er.Reset();
        _values.Clear();
        _chgErSum = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var prevValue = _index >= _length && _values.Count >= _length ? _values[_values.Count - _length] : 0;
        var chgEr = _index >= _length ? (value - prevValue) * er : 0;
        var ep = _chgErSum + chgEr;

        if (isFinal)
        {
            _chgErSum = ep;
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ep", ep }
            };
        }

        return new StreamingIndicatorStateResult(ep, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
        _values.Dispose();
    }
}

public sealed class EfficientTrendStepChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StandardDeviationVolatilityState _fastStdDev;
    private readonly StandardDeviationVolatilityState _slowStdDev;
    private readonly StreamingInputResolver _input;
    private double _stdDevInput;
    private double _prevA;
    private bool _hasPrev;

    public EfficientTrendStepChannelState(int length = 100, int fastLength = 50, int slowLength = 200,
        InputName inputName = InputName.Close)
    {
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _fastStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage,
            Math.Max(1, fastLength), _ => _stdDevInput);
        _slowStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage,
            Math.Max(1, slowLength), _ => _stdDevInput);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EfficientTrendStepChannelState(int length, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _er = new EfficiencyRatioState(Math.Max(1, length));
        _fastStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage,
            Math.Max(1, fastLength), _ => _stdDevInput);
        _slowStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage,
            Math.Max(1, slowLength), _ => _stdDevInput);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EfficientTrendStepChannel;

    public void Reset()
    {
        _er.Reset();
        _fastStdDev.Reset();
        _slowStdDev.Reset();
        _prevA = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _stdDevInput = value * 2;
        var fastStdDev = _fastStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var slowStdDev = _slowStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var er = _er.Next(value, isFinal);
        var dev = (er * fastStdDev) + ((1 - er) * slowStdDev);

        var prevA = _hasPrev ? _prevA : value;
        var a = value > prevA + dev ? value : value < prevA - dev ? value : prevA;
        var upper = a + dev;
        var lower = a - dev;

        if (isFinal)
        {
            _prevA = a;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", a },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
        _fastStdDev.Dispose();
        _slowStdDev.Dispose();
    }
}

public sealed class Ehlers2PoleButterworthFilterV1State : IStreamingIndicatorState
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private double _prevFilter1;
    private double _prevFilter2;
    private bool _hasPrev;

    public Ehlers2PoleButterworthFilterV1State(int length = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * 1.25 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public Ehlers2PoleButterworthFilterV1State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * 1.25 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Ehlers2PoleButterworthFilterV1;

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevFilter1 = _hasPrev ? _prevFilter1 : 0;
        var prevFilter2 = _hasPrev ? _prevFilter2 : 0;
        var filt = (_c1 * value) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        if (isFinal)
        {
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "E2bf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

public sealed class Ehlers2PoleButterworthFilterV2State : IStreamingIndicatorState, IDisposable
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public Ehlers2PoleButterworthFilterV2State(int length = 15, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = (1 - b + (a * a)) / 4;
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(3);
    }

    public Ehlers2PoleButterworthFilterV2State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = (1 - b + (a * a)) / 4;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _values = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.Ehlers2PoleButterworthFilterV2;

    public void Reset()
    {
        _values.Clear();
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;
        var prevValue1 = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue3 = _values.Count >= 3 ? _values[_values.Count - 3] : 0;

        var filt = _index < 3
            ? value
            : (_c1 * (value + (2 * prevValue1) + prevValue3)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        if (isFinal)
        {
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
                { "E2bf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class Ehlers2PoleSuperSmootherFilterV1State : IStreamingIndicatorState
{
    private readonly double _coef1;
    private readonly double _coef2;
    private readonly double _coef3;
    private readonly StreamingInputResolver _input;
    private double _prevFilter1;
    private double _prevFilter2;
    private int _index;

    public Ehlers2PoleSuperSmootherFilterV1State(int length = 15, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _coef2 = b1;
        _coef3 = -a1 * a1;
        _coef1 = 1 - _coef2 - _coef3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public Ehlers2PoleSuperSmootherFilterV1State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a1 = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b1 = 2 * a1 * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _coef2 = b1;
        _coef3 = -a1 * a1;
        _coef1 = 1 - _coef2 - _coef3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Ehlers2PoleSuperSmootherFilterV1;

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;

        var filt = _index < 3 ? value : (_coef1 * value) + (_coef2 * prevFilter1) + (_coef3 * prevFilter2);

        if (isFinal)
        {
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
            _index++;
        }

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

public sealed class Ehlers2PoleSuperSmootherFilterV2State : IStreamingIndicatorState
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly StreamingInputResolver _input;
    private double _prevFilter1;
    private double _prevFilter2;
    private double _prevValue;
    private bool _hasPrev;

    public Ehlers2PoleSuperSmootherFilterV2State(int length = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(inputName, null);
    }

    public Ehlers2PoleSuperSmootherFilterV2State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var a = MathHelper.Exp(MathHelper.MinOrMax(-MathHelper.Sqrt2 * Math.PI / resolved, -0.01, -0.99));
        var b = 2 * a * Math.Cos(MathHelper.MinOrMax(MathHelper.Sqrt2 * Math.PI / resolved, 0.99, 0.01));
        _c2 = b;
        _c3 = -a * a;
        _c1 = 1 - _c2 - _c3;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Ehlers2PoleSuperSmootherFilterV2;

    public void Reset()
    {
        _prevFilter1 = 0;
        _prevFilter2 = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevFilter1 = _prevFilter1;
        var prevFilter2 = _prevFilter2;

        var filt = (_c1 * ((value + prevValue) / 2)) + (_c2 * prevFilter1) + (_c3 * prevFilter2);

        if (isFinal)
        {
            _prevValue = value;
            _prevFilter2 = prevFilter1;
            _prevFilter1 = filt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "E2ssf", filt }
            };
        }

        return new StreamingIndicatorStateResult(filt, outputs);
    }
}

internal sealed class PriceMomentumOscillatorEngine
{
    private readonly double _sc1;
    private readonly double _sc2;
    private double _prevValue;
    private double _prevRocMa;
    private double _prevPmo;
    private bool _hasPrev;

    public PriceMomentumOscillatorEngine(int length1, int length2)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _sc1 = (double)2 / resolved1;
        _sc2 = (double)2 / resolved2;
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var roc = prevValue != 0 ? (value - prevValue) / prevValue * 100 : 0;
        var rocMa = _prevRocMa + ((roc - _prevRocMa) * _sc1);
        var pmo = _prevPmo + (((rocMa * 10) - _prevPmo) * _sc2);

        if (isFinal)
        {
            _prevValue = value;
            _prevRocMa = rocMa;
            _prevPmo = pmo;
            _hasPrev = true;
        }

        return pmo;
    }

    public void Reset()
    {
        _prevValue = 0;
        _prevRocMa = 0;
        _prevPmo = 0;
        _hasPrev = false;
    }
}

internal sealed class AdaptiveCyberCyclePeriodState : IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smooth;
    private readonly PooledRingBuffer<double> _cycle;
    private readonly PooledRingBuffer<double> _dpValues;
    private readonly double[] _dpScratch;
    private double _prevIp;
    private double _prevP;
    private double _prevQ1;
    private double _prevI1;
    private int _index;

    public AdaptiveCyberCyclePeriodState(int length, double alpha)
    {
        _length = Math.Max(1, length);
        _alpha = alpha;
        _values = new PooledRingBuffer<double>(4);
        _smooth = new PooledRingBuffer<double>(3);
        _cycle = new PooledRingBuffer<double>(7);
        _dpValues = new PooledRingBuffer<double>(_length);
        _dpScratch = new double[_length];
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _values.Count >= 1 ? _values[_values.Count - 1] : 0;
        var prevValue2 = _values.Count >= 2 ? _values[_values.Count - 2] : 0;
        var prevValue3 = _values.Count >= 3 ? _values[_values.Count - 3] : 0;
        var prevSmooth = _smooth.Count >= 1 ? _smooth[_smooth.Count - 1] : 0;
        var prevSmooth2 = _smooth.Count >= 2 ? _smooth[_smooth.Count - 2] : 0;
        var prevCycle = _cycle.Count >= 1 ? _cycle[_cycle.Count - 1] : 0;
        var prevCycle2 = _cycle.Count >= 2 ? _cycle[_cycle.Count - 2] : 0;
        var prevCycle3 = _cycle.Count >= 3 ? _cycle[_cycle.Count - 3] : 0;
        var prevCycle4 = _cycle.Count >= 4 ? _cycle[_cycle.Count - 4] : 0;
        var prevCycle6 = _cycle.Count >= 6 ? _cycle[_cycle.Count - 6] : 0;

        var smooth = (value + (2 * prevValue) + (2 * prevValue2) + prevValue3) / 6;
        var cycle = _index < 7
            ? (value - (2 * prevValue) + prevValue2) / 4
            : (MathHelper.Pow(1 - (0.5 * _alpha), 2) * (smooth - (2 * prevSmooth) + prevSmooth2))
              + (2 * (1 - _alpha) * prevCycle) - (MathHelper.Pow(1 - _alpha, 2) * prevCycle2);

        var q1 = ((0.0962 * cycle) + (0.5769 * prevCycle2) - (0.5769 * prevCycle4) - (0.0962 * prevCycle6))
            * (0.5 + (0.08 * _prevIp));
        var i1 = prevCycle3;

        var dp = MathHelper.MinOrMax(q1 != 0 && _prevQ1 != 0
            ? ((i1 / q1) - (_prevI1 / _prevQ1)) / (1 + (i1 * _prevI1 / (q1 * _prevQ1)))
            : 0, 1.1, 0.1);

        var medianDelta = GetMedian(dp);
        var dc = medianDelta != 0 ? (6.28318 / medianDelta) + 0.5 : 15;
        var ip = (0.33 * dc) + (0.67 * _prevIp);
        var p = (0.15 * ip) + (0.85 * _prevP);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smooth.TryAdd(smooth, out _);
            _cycle.TryAdd(cycle, out _);
            _dpValues.TryAdd(dp, out _);
            _prevIp = ip;
            _prevP = p;
            _prevQ1 = q1;
            _prevI1 = i1;
            _index++;
        }

        return p;
    }

    public void Reset()
    {
        _values.Clear();
        _smooth.Clear();
        _cycle.Clear();
        _dpValues.Clear();
        _prevIp = 0;
        _prevP = 0;
        _prevQ1 = 0;
        _prevI1 = 0;
        _index = 0;
    }

    public void Dispose()
    {
        _values.Dispose();
        _smooth.Dispose();
        _cycle.Dispose();
        _dpValues.Dispose();
    }

    private double GetMedian(double value)
    {
        var count = _dpValues.Count;
        var start = count == _length ? 1 : 0;
        var scratchCount = count - start + 1;
        for (var i = 0; i < count - start; i++)
        {
            _dpScratch[i] = _dpValues[start + i];
        }

        _dpScratch[scratchCount - 1] = value;
        Array.Sort(_dpScratch, 0, scratchCount);
        var mid = scratchCount / 2;
        if ((scratchCount & 1) == 1)
        {
            return _dpScratch[mid];
        }

        return (_dpScratch[mid - 1] + _dpScratch[mid]) / 2;
    }
}

internal static class StreamingWindowMath
{
    public static double SumRecent(PooledRingBuffer<double> buffer, double pendingValue, int length,
        out int countAfter)
    {
        if (length <= 0)
        {
            countAfter = 0;
            return 0;
        }

        var available = Math.Min(length - 1, buffer.Count);
        var sum = pendingValue;
        for (var i = 0; i < available; i++)
        {
            sum += buffer[buffer.Count - 1 - i];
        }

        countAfter = available + 1;
        return sum;
    }

    public static int LastOffset(PooledRingBuffer<double> buffer, double pendingValue, double target)
    {
        if (pendingValue == target)
        {
            return 0;
        }

        if (buffer.Count == 0)
        {
            return 0;
        }

        var offset = 1;
        for (var i = buffer.Count - 1; i >= 0; i--, offset++)
        {
            if (buffer[i] == target)
            {
                return offset;
            }
        }

        return buffer.Count;
    }
}
