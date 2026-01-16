using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class GatorOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _jawOffset;
    private readonly int _teethOffset;
    private readonly int _lipsOffset;
    private readonly IMovingAverageSmoother _jawSmoother;
    private readonly IMovingAverageSmoother _teethSmoother;
    private readonly IMovingAverageSmoother _lipsSmoother;
    private readonly PooledRingBuffer<double> _jawWindow;
    private readonly PooledRingBuffer<double> _teethWindow;
    private readonly PooledRingBuffer<double> _lipsWindow;
    private readonly StreamingInputResolver _input;

    public GatorOscillatorState(InputName inputName = InputName.MedianPrice,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13,
        int jawOffset = 8, int teethLength = 8, int teethOffset = 5, int lipsLength = 5, int lipsOffset = 3)
    {
        _jawOffset = Math.Max(0, jawOffset);
        _teethOffset = Math.Max(0, teethOffset);
        _lipsOffset = Math.Max(0, lipsOffset);
        _jawSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, jawLength));
        _teethSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, teethLength));
        _lipsSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, lipsLength));
        _jawWindow = new PooledRingBuffer<double>(_jawOffset + 1);
        _teethWindow = new PooledRingBuffer<double>(_teethOffset + 1);
        _lipsWindow = new PooledRingBuffer<double>(_lipsOffset + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GatorOscillatorState(InputName inputName, MovingAvgType maType, int jawLength, int jawOffset, int teethLength, int teethOffset,
        int lipsLength, int lipsOffset, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _jawOffset = Math.Max(0, jawOffset);
        _teethOffset = Math.Max(0, teethOffset);
        _lipsOffset = Math.Max(0, lipsOffset);
        _jawSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, jawLength));
        _teethSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, teethLength));
        _lipsSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, lipsLength));
        _jawWindow = new PooledRingBuffer<double>(_jawOffset + 1);
        _teethWindow = new PooledRingBuffer<double>(_teethOffset + 1);
        _lipsWindow = new PooledRingBuffer<double>(_lipsOffset + 1);
        _input = new StreamingInputResolver(inputName, selector);
    }

    public IndicatorName Name => IndicatorName.GatorOscillator;

    public void Reset()
    {
        _jawSmoother.Reset();
        _teethSmoother.Reset();
        _lipsSmoother.Reset();
        _jawWindow.Clear();
        _teethWindow.Clear();
        _lipsWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var jaw = _jawSmoother.Next(value, isFinal);
        var teeth = _teethSmoother.Next(value, isFinal);
        var lips = _lipsSmoother.Next(value, isFinal);

        if (isFinal)
        {
            _jawWindow.TryAdd(jaw, out _);
            _teethWindow.TryAdd(teeth, out _);
            _lipsWindow.TryAdd(lips, out _);
        }

        var displacedJaw = _jawOffset == 0 ? jaw : _jawWindow.Count > _jawOffset ? _jawWindow[0] : 0;
        var displacedTeeth = _teethOffset == 0 ? teeth : _teethWindow.Count > _teethOffset ? _teethWindow[0] : 0;
        var displacedLips = _lipsOffset == 0 ? lips : _lipsWindow.Count > _lipsOffset ? _lipsWindow[0] : 0;
        var top = Math.Abs(displacedJaw - displacedTeeth);
        var bottom = -Math.Abs(displacedTeeth - displacedLips);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Top", top },
                { "Bottom", bottom }
            };
        }

        return new StreamingIndicatorStateResult(top, outputs);
    }

    public void Dispose()
    {
        _jawSmoother.Dispose();
        _teethSmoother.Dispose();
        _lipsSmoother.Dispose();
        _jawWindow.Dispose();
        _teethWindow.Dispose();
        _lipsWindow.Dispose();
    }
}

public sealed class GeneralFilterEstimatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _p;
    private readonly double _gamma;
    private readonly double _zeta;
    private readonly PooledRingBuffer<double> _bValues;
    private readonly PooledRingBuffer<double> _dValues;
    private readonly StreamingInputResolver _input;

    public GeneralFilterEstimatorState(int length = 100, double beta = 5.25, double gamma = 1, double zeta = 1,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _p = beta != 0 ? (int)Math.Ceiling(_length / beta) : 0;
        _gamma = gamma;
        _zeta = zeta;
        var windowLength = Math.Max(1, _p);
        _bValues = new PooledRingBuffer<double>(windowLength);
        _dValues = new PooledRingBuffer<double>(windowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GeneralFilterEstimatorState(int length, double beta, double gamma, double zeta,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _p = beta != 0 ? (int)Math.Ceiling(_length / beta) : 0;
        _gamma = gamma;
        _zeta = zeta;
        var windowLength = Math.Max(1, _p);
        _bValues = new PooledRingBuffer<double>(windowLength);
        _dValues = new PooledRingBuffer<double>(windowLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GeneralFilterEstimator;

    public void Reset()
    {
        _bValues.Clear();
        _dValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorB = _p > 0 && _bValues.Count >= _p ? _bValues[0] : value;
        var a = value - priorB;
        var prevB = _bValues.Count > 0 ? _bValues[_bValues.Count - 1] : value;
        var b = prevB + (a / _p * _gamma);
        var priorD = _p > 0 && _dValues.Count >= _p ? _dValues[0] : b;
        var c = b - priorD;
        var prevD = _dValues.Count > 0 ? _dValues[_dValues.Count - 1] : value;
        var d = prevD + (((_zeta * a) + ((1 - _zeta) * c)) / _p * _gamma);

        if (isFinal)
        {
            _bValues.TryAdd(b, out _);
            _dValues.TryAdd(d, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gfe", d }
            };
        }

        return new StreamingIndicatorStateResult(d, outputs);
    }

    public void Dispose()
    {
        _bValues.Dispose();
        _dValues.Dispose();
    }
}

public sealed class GeneralizedDoubleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly double _factor;
    private readonly StreamingInputResolver _input;

    public GeneralizedDoubleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 5, double factor = 0.7, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public GeneralizedDoubleExponentialMovingAverageState(MovingAvgType maType, int length, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GeneralizedDoubleExponentialMovingAverage;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var gdema = (ema1 * (1 + _factor)) - (ema2 * _factor);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gdema", gdema }
            };
        }

        return new StreamingIndicatorStateResult(gdema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

public sealed class GOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _bSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public GOscillatorState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _bSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _bSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GOscillator;

    public void Reset()
    {
        _bSum.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var b = value > prevValue ? 100.0 / _length : 0;
        var bSum = isFinal ? _bSum.Add(b, out _) : _bSum.Preview(b, out _);

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
                { "GOsc", bSum }
            };
        }

        return new StreamingIndicatorStateResult(bSum, outputs);
    }

public void Dispose()
{
    _bSum.Dispose();
}
}

public sealed class GrandTrendForecastingState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _forecastLength;
    private readonly double _mult;
    private readonly RollingWindowSum _tSum;
    private readonly RollingWindowSum _diffSum;
    private readonly PooledRingBuffer<double> _tValues;
    private readonly PooledRingBuffer<double> _fcastValues;
    private readonly PooledRingBuffer<double> _chgValues;
    private readonly StreamingInputResolver _input;

    public GrandTrendForecastingState(int length = 100, int forecastLength = 200, double mult = 2,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _forecastLength = Math.Max(1, forecastLength);
        _mult = mult;
        _tSum = new RollingWindowSum(_length);
        _diffSum = new RollingWindowSum(_forecastLength);
        var bufferLength = Math.Max(_length, _forecastLength);
        _tValues = new PooledRingBuffer<double>(bufferLength);
        _fcastValues = new PooledRingBuffer<double>(bufferLength);
        _chgValues = new PooledRingBuffer<double>(bufferLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GrandTrendForecastingState(int length, int forecastLength, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _forecastLength = Math.Max(1, forecastLength);
        _mult = mult;
        _tSum = new RollingWindowSum(_length);
        _diffSum = new RollingWindowSum(_forecastLength);
        var bufferLength = Math.Max(_length, _forecastLength);
        _tValues = new PooledRingBuffer<double>(bufferLength);
        _fcastValues = new PooledRingBuffer<double>(bufferLength);
        _chgValues = new PooledRingBuffer<double>(bufferLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GrandTrendForecasting;

    public void Reset()
    {
        _tSum.Reset();
        _diffSum.Reset();
        _tValues.Clear();
        _fcastValues.Clear();
        _chgValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevT = _tValues.Count >= _length ? _tValues[_tValues.Count - _length] : value;
        var priorT = _tValues.Count >= _forecastLength ? _tValues[_tValues.Count - _forecastLength] : 0;
        var prevFcast = _fcastValues.Count >= _forecastLength ? _fcastValues[_fcastValues.Count - _forecastLength] : 0;
        var prevChg = _chgValues.Count >= _length ? _chgValues[_chgValues.Count - _length] : value;

        var chg = 0.9 * prevT;
        var t = (0.9 * prevT) + (0.1 * value) + (chg - prevChg);
        var tSum = isFinal ? _tSum.Add(t, out var tCount) : _tSum.Preview(t, out tCount);
        var trend = tCount > 0 ? tSum / tCount : 0;

        var fcast = t + (t - priorT);
        var diff = Math.Abs(value - prevFcast);
        var diffSum = isFinal ? _diffSum.Add(diff, out var diffCount) : _diffSum.Preview(diff, out diffCount);
        var diffSma = diffCount > 0 ? diffSum / diffCount : 0;
        var dev = diffSma * _mult;
        var upper = fcast + dev;
        var lower = fcast - dev;

        if (isFinal)
        {
            _chgValues.TryAdd(chg, out _);
            _tValues.TryAdd(t, out _);
            _fcastValues.TryAdd(fcast, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Gtf", trend },
                { "UpperBand", upper },
                { "MiddleBand", fcast },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(trend, outputs);
    }

    public void Dispose()
    {
        _tSum.Dispose();
        _diffSum.Dispose();
        _tValues.Dispose();
        _fcastValues.Dispose();
        _chgValues.Dispose();
    }
}

public sealed class GroverLlorensActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevTs;
    private bool _hasPrev;

    public GroverLlorensActivatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, double mult = 5, InputName inputName = InputName.Close)
    {
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public GroverLlorensActivatorState(MovingAvgType maType, int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GroverLlorensActivator;

    public void Reset()
    {
        _atrSmoother.Reset();
        _prevValue = 0;
        _prevTs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var prevTs = _hasPrev ? _prevTs : value;
        if (prevTs == 0)
        {
            prevTs = prevValue;
        }

        var diff = value - prevTs;
        var ts = diff > 0 ? prevTs - (atr * _mult) : diff < 0 ? prevTs + (atr * _mult) : prevTs;

        if (isFinal)
        {
            _prevValue = value;
            _prevTs = ts;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gla", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
    }
}

public sealed class GroverLlorensCycleOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _oscSmoother;
    private readonly RsiState _rsi;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevTs;
    private bool _hasPrev;

    public GroverLlorensCycleOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, int smoothLength = 20, double mult = 10, InputName inputName = InputName.Close)
    {
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _oscSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rsi = new RsiState(maType, Math.Max(1, smoothLength));
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public GroverLlorensCycleOscillatorState(MovingAvgType maType, int length, int smoothLength, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _oscSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rsi = new RsiState(maType, Math.Max(1, smoothLength));
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GroverLlorensCycleOscillator;

    public void Reset()
    {
        _atrSmoother.Reset();
        _oscSmoother.Reset();
        _rsi.Reset();
        _prevValue = 0;
        _prevTs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var prevTs = _hasPrev ? _prevTs : value;
        var diff = value - prevTs;
        var ts = diff > 0 ? prevTs - (atr * _mult) : diff < 0 ? prevTs + (atr * _mult) : prevTs;
        var osc = value - ts;
        var smoothOsc = _oscSmoother.Next(osc, isFinal);
        var rsi = _rsi.Next(smoothOsc, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevTs = ts;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Glco", rsi }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

public void Dispose()
{
    _atrSmoother.Dispose();
    _oscSmoother.Dispose();
    _rsi.Dispose();
}
}

public sealed class GuppyCountBackLineState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;

    public GuppyCountBackLineState(int length = 21, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _highs = new PooledRingBuffer<double>((_length * 2) + 1);
        _lows = new PooledRingBuffer<double>((_length * 2) + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GuppyCountBackLineState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _highs = new PooledRingBuffer<double>((_length * 2) + 1);
        _lows = new PooledRingBuffer<double>((_length * 2) + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GuppyCountBackLine;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highs.Clear();
        _lows.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var ll = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        int hCount = 0;
        int lCount = 0;
        var cbl = value;
        for (var j = 0; j <= _length; j++)
        {
            var currentLow = EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, j);
            var currentHigh = EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, j);

            if (currentLow == ll)
            {
                for (var k = j + 1; k <= j + _length; k++)
                {
                    var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, k);
                    lCount += prevHigh > currentHigh ? 1 : 0;
                    if (lCount == 2)
                    {
                        cbl = prevHigh;
                        break;
                    }
                }
            }

            if (currentHigh == hh)
            {
                for (var k = j + 1; k <= j + _length; k++)
                {
                    var prevLow = EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, k);
                    hCount += prevLow > currentLow ? 1 : 0;
                    if (hCount == 2)
                    {
                        cbl = prevLow;
                        break;
                    }
                }
            }
        }

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cbl", cbl }
            };
        }

        return new StreamingIndicatorStateResult(cbl, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highs.Dispose();
        _lows.Dispose();
    }
}

public sealed class GuppyDistanceIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _ema3;
    private readonly IMovingAverageSmoother _ema4;
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema6;
    private readonly IMovingAverageSmoother _ema7;
    private readonly IMovingAverageSmoother _ema8;
    private readonly IMovingAverageSmoother _ema9;
    private readonly IMovingAverageSmoother _ema10;
    private readonly IMovingAverageSmoother _ema11;
    private readonly IMovingAverageSmoother _ema12;
    private readonly StreamingInputResolver _input;

    public GuppyDistanceIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 3, int length2 = 5, int length3 = 8, int length4 = 10, int length5 = 12, int length6 = 15,
        int length7 = 30, int length8 = 35, int length9 = 40, int length10 = 45, int length11 = 11,
        int length12 = 60, InputName inputName = InputName.Close)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _ema7 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length7));
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length8));
        _ema9 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length9));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length10));
        _ema11 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length11));
        _ema12 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length12));
        _input = new StreamingInputResolver(inputName, null);
    }

    public GuppyDistanceIndicatorState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _ema7 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length7));
        _ema8 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length8));
        _ema9 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length9));
        _ema10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length10));
        _ema11 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length11));
        _ema12 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length12));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GuppyDistanceIndicator;

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
        _ema9.Reset();
        _ema10.Reset();
        _ema11.Reset();
        _ema12.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(value, isFinal);
        var ema3 = _ema3.Next(value, isFinal);
        var ema4 = _ema4.Next(value, isFinal);
        var ema5 = _ema5.Next(value, isFinal);
        var ema6 = _ema6.Next(value, isFinal);
        var ema7 = _ema7.Next(value, isFinal);
        var ema8 = _ema8.Next(value, isFinal);
        var ema9 = _ema9.Next(value, isFinal);
        var ema10 = _ema10.Next(value, isFinal);
        var ema11 = _ema11.Next(value, isFinal);
        var ema12 = _ema12.Next(value, isFinal);

        var diff12 = Math.Abs(ema1 - ema2);
        var diff23 = Math.Abs(ema2 - ema3);
        var diff34 = Math.Abs(ema3 - ema4);
        var diff45 = Math.Abs(ema4 - ema5);
        var diff56 = Math.Abs(ema5 - ema6);
        var diff78 = Math.Abs(ema7 - ema8);
        var diff89 = Math.Abs(ema8 - ema9);
        var diff910 = Math.Abs(ema9 - ema10);
        var diff1011 = Math.Abs(ema10 - ema11);
        var diff1112 = Math.Abs(ema11 - ema12);

        var fastDistance = diff12 + diff23 + diff34 + diff45 + diff56;
        var slowDistance = diff78 + diff89 + diff910 + diff1011 + diff1112;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "FastDistance", fastDistance },
                { "SlowDistance", slowDistance }
            };
        }

        return new StreamingIndicatorStateResult(fastDistance, outputs);
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
        _ema9.Dispose();
        _ema10.Dispose();
        _ema11.Dispose();
        _ema12.Dispose();
    }
}

public sealed class GuppyMultipleMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema3;
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema7;
    private readonly IMovingAverageSmoother _ema9;
    private readonly IMovingAverageSmoother _ema11;
    private readonly IMovingAverageSmoother _ema13;
    private readonly IMovingAverageSmoother _ema15;
    private readonly IMovingAverageSmoother _ema17;
    private readonly IMovingAverageSmoother _ema19;
    private readonly IMovingAverageSmoother _ema21;
    private readonly IMovingAverageSmoother _ema23;
    private readonly IMovingAverageSmoother _ema25;
    private readonly IMovingAverageSmoother _ema28;
    private readonly IMovingAverageSmoother _ema31;
    private readonly IMovingAverageSmoother _ema34;
    private readonly IMovingAverageSmoother _ema37;
    private readonly IMovingAverageSmoother _ema40;
    private readonly IMovingAverageSmoother _ema43;
    private readonly IMovingAverageSmoother _ema46;
    private readonly IMovingAverageSmoother _ema49;
    private readonly IMovingAverageSmoother _ema52;
    private readonly IMovingAverageSmoother _ema55;
    private readonly IMovingAverageSmoother _ema58;
    private readonly IMovingAverageSmoother _ema61;
    private readonly IMovingAverageSmoother _ema64;
    private readonly IMovingAverageSmoother _ema67;
    private readonly IMovingAverageSmoother _ema70;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly RollingWindowSum _oscRawSum;
    private readonly StreamingInputResolver _input;
    private readonly int _smoothLength;

    public GuppyMultipleMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 3, int length2 = 5,
        int length3 = 7, int length4 = 8, int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12,
        int length9 = 13, int length10 = 15, int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23,
        int length15 = 25, int length16 = 28, int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35,
        int length21 = 37, int length22 = 40, int length23 = 43, int length24 = 45, int length25 = 46, int length26 = 49,
        int length27 = 50, int length28 = 52, int length29 = 55, int length30 = 58, int length31 = 60, int length32 = 61,
        int length33 = 64, int length34 = 67, int length35 = 70, int smoothLength = 1, int signalLength = 13,
        InputName inputName = InputName.Close)
    {
        _ema3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema7 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema9 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema11 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length7));
        _ema13 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length9));
        _ema15 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length10));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length11));
        _ema19 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length12));
        _ema21 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length13));
        _ema23 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length14));
        _ema25 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length15));
        _ema28 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length16));
        _ema31 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length18));
        _ema34 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length19));
        _ema37 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length21));
        _ema40 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length22));
        _ema43 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length23));
        _ema46 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length25));
        _ema49 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length26));
        _ema52 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length28));
        _ema55 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length29));
        _ema58 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length30));
        _ema61 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length32));
        _ema64 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length33));
        _ema67 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length34));
        _ema70 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length35));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _smoothLength = Math.Max(1, smoothLength);
        _oscRawSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public GuppyMultipleMovingAverageState(MovingAvgType maType, int length1, int length2, int length3, int length4, int length5, int length6,
        int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16,
        int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26,
        int length27, int length28, int length29, int length30, int length31, int length32, int length33, int length34, int length35,
        int smoothLength, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema7 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema9 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema11 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length7));
        _ema13 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length9));
        _ema15 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length10));
        _ema17 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length11));
        _ema19 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length12));
        _ema21 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length13));
        _ema23 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length14));
        _ema25 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length15));
        _ema28 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length16));
        _ema31 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length18));
        _ema34 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length19));
        _ema37 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length21));
        _ema40 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length22));
        _ema43 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length23));
        _ema46 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length25));
        _ema49 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length26));
        _ema52 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length28));
        _ema55 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length29));
        _ema58 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length30));
        _ema61 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length32));
        _ema64 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length33));
        _ema67 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length34));
        _ema70 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length35));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _smoothLength = Math.Max(1, smoothLength);
        _oscRawSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GuppyMultipleMovingAverage;

    public void Reset()
    {
        _ema3.Reset();
        _ema5.Reset();
        _ema7.Reset();
        _ema9.Reset();
        _ema11.Reset();
        _ema13.Reset();
        _ema15.Reset();
        _ema17.Reset();
        _ema19.Reset();
        _ema21.Reset();
        _ema23.Reset();
        _ema25.Reset();
        _ema28.Reset();
        _ema31.Reset();
        _ema34.Reset();
        _ema37.Reset();
        _ema40.Reset();
        _ema43.Reset();
        _ema46.Reset();
        _ema49.Reset();
        _ema52.Reset();
        _ema55.Reset();
        _ema58.Reset();
        _ema61.Reset();
        _ema64.Reset();
        _ema67.Reset();
        _ema70.Reset();
        _signalSmoother.Reset();
        _oscRawSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var emaF1 = _ema3.Next(value, isFinal);
        var emaF2 = _ema5.Next(value, isFinal);
        var emaF3 = _ema7.Next(value, isFinal);
        var emaF4 = _ema9.Next(value, isFinal);
        var emaF5 = _ema11.Next(value, isFinal);
        var emaF6 = _ema13.Next(value, isFinal);
        var emaF7 = _ema15.Next(value, isFinal);
        var emaF8 = _ema17.Next(value, isFinal);
        var emaF9 = _ema19.Next(value, isFinal);
        var emaF10 = _ema21.Next(value, isFinal);
        var emaF11 = _ema23.Next(value, isFinal);
        var emaS1 = _ema25.Next(value, isFinal);
        var emaS2 = _ema28.Next(value, isFinal);
        var emaS3 = _ema31.Next(value, isFinal);
        var emaS4 = _ema34.Next(value, isFinal);
        var emaS5 = _ema37.Next(value, isFinal);
        var emaS6 = _ema40.Next(value, isFinal);
        var emaS7 = _ema43.Next(value, isFinal);
        var emaS8 = _ema46.Next(value, isFinal);
        var emaS9 = _ema49.Next(value, isFinal);
        var emaS10 = _ema52.Next(value, isFinal);
        var emaS11 = _ema55.Next(value, isFinal);
        var emaS12 = _ema58.Next(value, isFinal);
        var emaS13 = _ema61.Next(value, isFinal);
        var emaS14 = _ema64.Next(value, isFinal);
        var emaS15 = _ema67.Next(value, isFinal);
        var emaS16 = _ema70.Next(value, isFinal);

        var superGmmaFast = (emaF1 + emaF2 + emaF3 + emaF4 + emaF5 + emaF6 + emaF7 + emaF8 + emaF9 + emaF10 + emaF11) / 11;
        var superGmmaSlow = (emaS1 + emaS2 + emaS3 + emaS4 + emaS5 + emaS6 + emaS7 + emaS8 +
            emaS9 + emaS10 + emaS11 + emaS12 + emaS13 + emaS14 + emaS15 + emaS16) / 16;
        var superGmmaOscRaw = superGmmaSlow != 0 ? (superGmmaFast - superGmmaSlow) / superGmmaSlow * 100 : 0;
        var oscSum = isFinal ? _oscRawSum.Add(superGmmaOscRaw, out var oscCount) : _oscRawSum.Preview(superGmmaOscRaw, out oscCount);
        var superGmmaOsc = oscCount > 0 ? oscSum / oscCount : 0;
        var signal = _signalSmoother.Next(superGmmaOscRaw, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "SuperGmmaOsc", superGmmaOsc },
                { "SuperGmmaSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(superGmmaOsc, outputs);
    }

    public void Dispose()
    {
        _ema3.Dispose();
        _ema5.Dispose();
        _ema7.Dispose();
        _ema9.Dispose();
        _ema11.Dispose();
        _ema13.Dispose();
        _ema15.Dispose();
        _ema17.Dispose();
        _ema19.Dispose();
        _ema21.Dispose();
        _ema23.Dispose();
        _ema25.Dispose();
        _ema28.Dispose();
        _ema31.Dispose();
        _ema34.Dispose();
        _ema37.Dispose();
        _ema40.Dispose();
        _ema43.Dispose();
        _ema46.Dispose();
        _ema49.Dispose();
        _ema52.Dispose();
        _ema55.Dispose();
        _ema58.Dispose();
        _ema61.Dispose();
        _ema64.Dispose();
        _ema67.Dispose();
        _ema70.Dispose();
        _signalSmoother.Dispose();
        _oscRawSum.Dispose();
    }
}

public sealed class HalfTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly IMovingAverageSmoother _highMa;
    private readonly IMovingAverageSmoother _lowMa;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private double _prevTrend;
    private double _prevNextTrend;
    private double _prevUp;
    private double _prevDown;
    private bool _hasPrev;

    public HalfTrendState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 2,
        int atrLength = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var resolvedAtr = Math.Max(1, atrLength);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolvedAtr);
        _highMa = MovingAverageSmootherFactory.Create(maType, _length);
        _lowMa = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HalfTrendState(MovingAvgType maType, int length, int atrLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var resolvedAtr = Math.Max(1, atrLength);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolvedAtr);
        _highMa = MovingAverageSmootherFactory.Create(maType, _length);
        _lowMa = MovingAverageSmootherFactory.Create(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HalfTrend;

    public void Reset()
    {
        _atrSmoother.Reset();
        _highMa.Reset();
        _lowMa.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevTrend = 0;
        _prevNextTrend = 0;
        _prevUp = 0;
        _prevDown = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var highMa = _highMa.Next(bar.High, isFinal);
        var lowMa = _lowMa.Next(bar.Low, isFinal);
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        _ = _atrSmoother.Next(tr, isFinal);

        var maxLow = _hasPrev ? prevLow : lowest;
        var minHigh = _hasPrev ? prevHigh : highest;
        var prevNextTrend = _hasPrev ? _prevNextTrend : 0;
        var prevTrend = _hasPrev ? _prevTrend : 0;
        var prevUp = _hasPrev ? _prevUp : 0;
        var prevDown = _hasPrev ? _prevDown : 0;

        double trend = 0;
        double nextTrend = 0;
        if (prevNextTrend == 1)
        {
            maxLow = Math.Max(lowest, maxLow);

            if (highMa < maxLow && value < (prevLow != 0 ? prevLow : lowest))
            {
                trend = 1;
                nextTrend = 0;
                minHigh = highest;
            }
            else
            {
                minHigh = Math.Min(highest, minHigh);

                if (lowMa > minHigh && value > (prevHigh != 0 ? prevHigh : highest))
                {
                    trend = 0;
                    nextTrend = 1;
                    maxLow = lowest;
                }
            }
        }

        double up = 0;
        double down = 0;
        if (trend == 0)
        {
            if (prevTrend != 0)
            {
                up = prevDown;
            }
            else
            {
                up = Math.Max(maxLow, prevUp);
            }
        }
        else
        {
            if (prevTrend != 1)
            {
                down = prevUp;
            }
            else
            {
                down = Math.Min(minHigh, prevDown);
            }
        }

        var ht = trend == 0 ? up : down;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevTrend = trend;
            _prevNextTrend = nextTrend;
            _prevUp = up;
            _prevDown = down;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ht", ht }
            };
        }

        return new StreamingIndicatorStateResult(ht, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _highMa.Dispose();
        _lowMa.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class HampelFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly double _scalingFactor;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _absDiffs;
    private readonly double[] _medianScratch;
    private readonly double[] _absMedianScratch;
    private readonly StreamingInputResolver _input;
    private double _prevHfEma;

    public HampelFilterState(int length = 14, double scalingFactor = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _alpha = (double)2 / (_length + 1);
        _scalingFactor = scalingFactor;
        _values = new PooledRingBuffer<double>(_length);
        _absDiffs = new PooledRingBuffer<double>(_length);
        _medianScratch = new double[_length];
        _absMedianScratch = new double[_length];
        _input = new StreamingInputResolver(inputName, null);
    }

    public HampelFilterState(int length, double scalingFactor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _alpha = (double)2 / (_length + 1);
        _scalingFactor = scalingFactor;
        _values = new PooledRingBuffer<double>(_length);
        _absDiffs = new PooledRingBuffer<double>(_length);
        _medianScratch = new double[_length];
        _absMedianScratch = new double[_length];
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HampelFilter;

    public void Reset()
    {
        _values.Clear();
        _absDiffs.Clear();
        _prevHfEma = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sampleMedian = EhlersStreamingWindow.GetMedian(_values, value, _medianScratch);
        var absDiff = Math.Abs(value - sampleMedian);
        var mad = EhlersStreamingWindow.GetMedian(_absDiffs, absDiff, _absMedianScratch);
        var hf = absDiff <= _scalingFactor * mad ? value : sampleMedian;
        var hfEma = (_alpha * hf) + ((1 - _alpha) * _prevHfEma);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _absDiffs.TryAdd(absDiff, out _);
            _prevHfEma = hfEma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hf", hfEma }
            };
        }

        return new StreamingIndicatorStateResult(hfEma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _absDiffs.Dispose();
    }
}

public sealed class HawkeyeVolumeIndicatorState : IStreamingIndicatorState
{
    private readonly double _divisor;
    private readonly StreamingInputResolver _input;
    private double _prevHigh;
    private double _prevLow;
    private double _prevMidpoint;
    private bool _hasPrev;

    public HawkeyeVolumeIndicatorState(InputName inputName = InputName.MedianPrice, int length = 200,
        double divisor = 3.6)
    {
        _divisor = divisor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public HawkeyeVolumeIndicatorState(int length, double divisor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _divisor = divisor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HawkeyeVolumeIndicator;

    public void Reset()
    {
        _prevHigh = 0;
        _prevLow = 0;
        _prevMidpoint = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var midpoint = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevMidpoint = _hasPrev ? _prevMidpoint : 0;

        var u1 = _divisor != 0 ? prevMidpoint + ((prevHigh - prevLow) / _divisor) : prevMidpoint;
        var d1 = _divisor != 0 ? prevMidpoint - ((prevHigh - prevLow) / _divisor) : prevMidpoint;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevMidpoint = midpoint;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Up", u1 },
                { "Dn", d1 }
            };
        }

        return new StreamingIndicatorStateResult(u1, outputs);
    }
}

public sealed class HendersonWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public HendersonWeightedMovingAverageState(int length = 7, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var termMult = MathHelper.MinOrMax((int)Math.Floor((double)(resolved - 1) / 2));
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var m = termMult;
            var n = j - termMult;
            var numerator = 315 * (MathHelper.Pow(m + 1, 2) - MathHelper.Pow(n, 2)) *
                (MathHelper.Pow(m + 2, 2) - MathHelper.Pow(n, 2)) *
                (MathHelper.Pow(m + 3, 2) - MathHelper.Pow(n, 2)) *
                ((3 * MathHelper.Pow(m + 2, 2)) - (11 * MathHelper.Pow(n, 2)) - 16);
            var denominator = 8 * (m + 2) * (MathHelper.Pow(m + 2, 2) - 1) *
                ((4 * MathHelper.Pow(m + 2, 2)) - 1) * ((4 * MathHelper.Pow(m + 2, 2)) - 9) *
                ((4 * MathHelper.Pow(m + 2, 2)) - 25);
            var weight = denominator != 0 ? numerator / denominator : 0;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HendersonWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var termMult = MathHelper.MinOrMax((int)Math.Floor((double)(resolved - 1) / 2));
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var m = termMult;
            var n = j - termMult;
            var numerator = 315 * (MathHelper.Pow(m + 1, 2) - MathHelper.Pow(n, 2)) *
                (MathHelper.Pow(m + 2, 2) - MathHelper.Pow(n, 2)) *
                (MathHelper.Pow(m + 3, 2) - MathHelper.Pow(n, 2)) *
                ((3 * MathHelper.Pow(m + 2, 2)) - (11 * MathHelper.Pow(n, 2)) - 16);
            var denominator = 8 * (m + 2) * (MathHelper.Pow(m + 2, 2) - 1) *
                ((4 * MathHelper.Pow(m + 2, 2)) - 1) * ((4 * MathHelper.Pow(m + 2, 2)) - 9) *
                ((4 * MathHelper.Pow(m + 2, 2)) - 25);
            var weight = denominator != 0 ? numerator / denominator : 0;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HendersonWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        for (var j = 0; j < _weights.Length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * _weights[j];
        }

        var hwma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hwma", hwma }
            };
        }

        return new StreamingIndicatorStateResult(hwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class HerrickPayoffIndexState : IStreamingIndicatorState
{
    private readonly double _pointValue;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevOpen;
    private double _prevClose;
    private double _prevK;
    private bool _hasPrev;

    public HerrickPayoffIndexState(InputName inputName = InputName.MedianPrice, double pointValue = 100)
    {
        _pointValue = pointValue;
        _input = new StreamingInputResolver(inputName, null);
    }

    public HerrickPayoffIndexState(double pointValue, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _pointValue = pointValue;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HerrickPayoffIndex;

    public void Reset()
    {
        _prevValue = 0;
        _prevOpen = 0;
        _prevClose = 0;
        _prevK = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var k = diff * _pointValue * bar.Volume;
        var prevOpen = _hasPrev ? _prevOpen : 0;
        var prevClose = _hasPrev ? _prevClose : 0;
        var absDiff = Math.Abs(bar.Close - prevClose);
        var g = Math.Min(bar.Open, prevOpen);
        var temp = g != 0 ? value < prevValue ? 1 - (absDiff / 2 / g) : 1 + (absDiff / 2 / g) : 1;
        k *= temp;
        var hpi = _prevK + (k - _prevK);

        if (isFinal)
        {
            _prevValue = value;
            _prevOpen = bar.Open;
            _prevClose = bar.Close;
            _prevK = k;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hpi", hpi }
            };
        }

        return new StreamingIndicatorStateResult(hpi, outputs);
    }
}

public sealed class HighLowIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _loSum;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevHighest;
    private double _prevLowest;
    private bool _hasPrev;

    public HighLowIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _advSum = new RollingWindowSum(_length);
        _loSum = new RollingWindowSum(_length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HighLowIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _advSum = new RollingWindowSum(_length);
        _loSum = new RollingWindowSum(_length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HighLowIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _advSum.Reset();
        _loSum.Reset();
        _smoother.Reset();
        _prevHighest = 0;
        _prevLowest = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var prevLowest = _hasPrev ? _prevLowest : 0;
        var adv = highest > prevHighest ? 1 : 0;
        var lo = lowest < prevLowest ? 1 : 0;
        var advSum = isFinal ? _advSum.Add(adv, out _) : _advSum.Preview(adv, out _);
        var loSum = isFinal ? _loSum.Add(lo, out _) : _loSum.Preview(lo, out _);
        var advDiff = advSum + loSum != 0
            ? MathHelper.MinOrMax(advSum / (advSum + loSum) * 100, 100, 0)
            : 0;
        var zmbti = _smoother.Next(advDiff, isFinal);

        if (isFinal)
        {
            _prevHighest = highest;
            _prevLowest = lowest;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zmbti", zmbti }
            };
        }

        return new StreamingIndicatorStateResult(zmbti, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _advSum.Dispose();
        _loSum.Dispose();
        _smoother.Dispose();
    }
}

public sealed class HirashimaSugitaRSState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly IMovingAverageSmoother _wma;
    private readonly LinearRegressionState _s1Regression;
    private readonly LinearRegressionState _s2Regression;
    private readonly StreamingInputResolver _input;
    private double _d1Value;
    private double _d2Value;
    private double _prevS2;
    private bool _hasPrev;

    public HirashimaSugitaRSState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 1000,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved);
        _wma = MovingAverageSmootherFactory.Create(maType, resolved);
        _s1Regression = new LinearRegressionState(resolved, _ => _d1Value);
        _s2Regression = new LinearRegressionState(resolved, _ => _d2Value);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HirashimaSugitaRSState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved);
        _wma = MovingAverageSmootherFactory.Create(maType, resolved);
        _s1Regression = new LinearRegressionState(resolved, _ => _d1Value);
        _s2Regression = new LinearRegressionState(resolved, _ => _d2Value);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HirashimaSugitaRS;

    public void Reset()
    {
        _ema.Reset();
        _wma.Reset();
        _s1Regression.Reset();
        _s2Regression.Reset();
        _d1Value = 0;
        _d2Value = 0;
        _prevS2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var d1 = value - ema;
        var wma = _wma.Next(Math.Abs(d1), isFinal);
        _d1Value = d1;
        var s1 = _s1Regression.Update(bar, isFinal, includeOutputs: false).Value;
        var d2 = value - (ema + s1);
        _d2Value = d2;
        var s2 = _s2Regression.Update(bar, isFinal, includeOutputs: false).Value;
        var prevS2 = _hasPrev ? _prevS2 : 0;
        var basis = ema + s1 + (s2 - prevS2);
        var upper1 = basis + wma;
        var lower1 = basis - wma;
        var upper2 = upper1 + wma;
        var lower2 = lower1 - wma;

        if (isFinal)
        {
            _prevS2 = s2;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "UpperBand1", upper1 },
                { "UpperBand2", upper2 },
                { "MiddleBand", basis },
                { "LowerBand1", lower1 },
                { "LowerBand2", lower2 }
            };
        }

        return new StreamingIndicatorStateResult(basis, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _wma.Dispose();
        _s1Regression.Dispose();
        _s2Regression.Dispose();
    }
}

public sealed class HoltExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _gamma;
    private readonly StreamingInputResolver _input;
    private double _prevB;
    private double _prevHema;
    private bool _hasPrev;

    public HoltExponentialMovingAverageState(int alphaLength = 20, int gammaLength = 20,
        InputName inputName = InputName.Close)
    {
        var resolvedAlpha = Math.Max(1, alphaLength);
        var resolvedGamma = Math.Max(1, gammaLength);
        _alpha = (double)2 / (resolvedAlpha + 1);
        _gamma = (double)2 / (resolvedGamma + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HoltExponentialMovingAverageState(int alphaLength, int gammaLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedAlpha = Math.Max(1, alphaLength);
        var resolvedGamma = Math.Max(1, gammaLength);
        _alpha = (double)2 / (resolvedAlpha + 1);
        _gamma = (double)2 / (resolvedGamma + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HoltExponentialMovingAverage;

    public void Reset()
    {
        _prevB = 0;
        _prevHema = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevB = _hasPrev ? _prevB : value;
        var prevHema = _hasPrev ? _prevHema : 0;
        var hema = ((1 - _alpha) * (prevHema + prevB)) + (_alpha * value);
        var b = ((1 - _gamma) * prevB) + (_gamma * (hema - prevHema));

        if (isFinal)
        {
            _prevB = b;
            _prevHema = hema;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hema", hema }
            };
        }

        return new StreamingIndicatorStateResult(hema, outputs);
    }
}

public sealed class HullEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _wma;
    private readonly IMovingAverageSmoother _ema;
    private readonly StreamingInputResolver _input;

    public HullEstimateState(int length = 50, InputName inputName = InputName.Close)
    {
        var maLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, maLength);
        _ema = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, maLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HullEstimateState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var maLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, maLength);
        _ema = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, maLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HullEstimate;

    public void Reset()
    {
        _wma.Reset();
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma = _wma.Next(value, isFinal);
        var ema = _ema.Next(value, isFinal);
        var he = (3 * wma) - (2 * ema);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "He", he }
            };
        }

        return new StreamingIndicatorStateResult(he, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
        _ema.Dispose();
    }
}

public sealed class HurstBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _displacement;
    private readonly double _innerMult;
    private readonly double _outerMult;
    private readonly double _extremeMult;
    private readonly RollingWindowSum _dPriceSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevCma1;
    private double _prevCma2;

    public HurstBandsState(int length = 10, double innerMult = 1.6, double outerMult = 2.6,
        double extremeMult = 4.2, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _displacement = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2) + 1);
        _innerMult = innerMult;
        _outerMult = outerMult;
        _extremeMult = extremeMult;
        _dPriceSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_displacement);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HurstBandsState(int length, double innerMult, double outerMult, double extremeMult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _displacement = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2) + 1);
        _innerMult = innerMult;
        _outerMult = outerMult;
        _extremeMult = extremeMult;
        _dPriceSum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_displacement);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HurstBands;

    public void Reset()
    {
        _dPriceSum.Reset();
        _values.Clear();
        _prevCma1 = 0;
        _prevCma2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var dPrice = EhlersStreamingWindow.GetOffsetValue(_values, value, _displacement);
        var dPriceSum = isFinal ? _dPriceSum.Add(dPrice, out var count) : _dPriceSum.Preview(dPrice, out count);
        var cma = dPrice == 0 ? _prevCma1 + (_prevCma1 - _prevCma2) : count > 0 ? dPriceSum / count : 0;

        var extremeBand = cma * _extremeMult / 100;
        var outerBand = cma * _outerMult / 100;
        var innerBand = cma * _innerMult / 100;
        var upperExtremeBand = cma + extremeBand;
        var lowerExtremeBand = cma - extremeBand;
        var upperOuterBand = cma + outerBand;
        var lowerOuterBand = cma - outerBand;
        var upperInnerBand = cma + innerBand;
        var lowerInnerBand = cma - innerBand;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevCma2 = _prevCma1;
            _prevCma1 = cma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(7)
            {
                { "UpperExtremeBand", upperExtremeBand },
                { "UpperOuterBand", upperOuterBand },
                { "UpperInnerBand", upperInnerBand },
                { "MiddleBand", cma },
                { "LowerExtremeBand", lowerExtremeBand },
                { "LowerOuterBand", lowerOuterBand },
                { "LowerInnerBand", lowerInnerBand }
            };
        }

        return new StreamingIndicatorStateResult(cma, outputs);
    }

    public void Dispose()
    {
        _dPriceSum.Dispose();
        _values.Dispose();
    }
}

public sealed class HurstCycleChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _sclOffset;
    private readonly int _mclOffset;
    private readonly IMovingAverageSmoother _sclAtrSmoother;
    private readonly IMovingAverageSmoother _mclAtrSmoother;
    private readonly IMovingAverageSmoother _sclRmaSmoother;
    private readonly IMovingAverageSmoother _mclRmaSmoother;
    private readonly PooledRingBuffer<double> _sclRmaValues;
    private readonly PooledRingBuffer<double> _mclRmaValues;
    private readonly double _fastMult;
    private readonly double _slowMult;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public HurstCycleChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3,
        InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var scl = MathHelper.MinOrMax((int)Math.Ceiling((double)resolvedFast / 2));
        var mcl = MathHelper.MinOrMax((int)Math.Ceiling((double)resolvedSlow / 2));
        _sclOffset = MathHelper.MinOrMax((int)Math.Ceiling((double)scl / 2));
        _mclOffset = MathHelper.MinOrMax((int)Math.Ceiling((double)mcl / 2));
        _sclAtrSmoother = MovingAverageSmootherFactory.Create(maType, scl);
        _mclAtrSmoother = MovingAverageSmootherFactory.Create(maType, mcl);
        _sclRmaSmoother = MovingAverageSmootherFactory.Create(maType, scl);
        _mclRmaSmoother = MovingAverageSmootherFactory.Create(maType, mcl);
        _sclRmaValues = new PooledRingBuffer<double>(Math.Max(1, _sclOffset));
        _mclRmaValues = new PooledRingBuffer<double>(Math.Max(1, _mclOffset));
        _fastMult = fastMult;
        _slowMult = slowMult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public HurstCycleChannelState(MovingAvgType maType, int fastLength, int slowLength, double fastMult,
        double slowMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var scl = MathHelper.MinOrMax((int)Math.Ceiling((double)resolvedFast / 2));
        var mcl = MathHelper.MinOrMax((int)Math.Ceiling((double)resolvedSlow / 2));
        _sclOffset = MathHelper.MinOrMax((int)Math.Ceiling((double)scl / 2));
        _mclOffset = MathHelper.MinOrMax((int)Math.Ceiling((double)mcl / 2));
        _sclAtrSmoother = MovingAverageSmootherFactory.Create(maType, scl);
        _mclAtrSmoother = MovingAverageSmootherFactory.Create(maType, mcl);
        _sclRmaSmoother = MovingAverageSmootherFactory.Create(maType, scl);
        _mclRmaSmoother = MovingAverageSmootherFactory.Create(maType, mcl);
        _sclRmaValues = new PooledRingBuffer<double>(Math.Max(1, _sclOffset));
        _mclRmaValues = new PooledRingBuffer<double>(Math.Max(1, _mclOffset));
        _fastMult = fastMult;
        _slowMult = slowMult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HurstCycleChannel;

    public void Reset()
    {
        _sclAtrSmoother.Reset();
        _mclAtrSmoother.Reset();
        _sclRmaSmoother.Reset();
        _mclRmaSmoother.Reset();
        _sclRmaValues.Clear();
        _mclRmaValues.Clear();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var sclAtr = _sclAtrSmoother.Next(tr, isFinal);
        var mclAtr = _mclAtrSmoother.Next(tr, isFinal);
        var sclRma = _sclRmaSmoother.Next(value, isFinal);
        var mclRma = _mclRmaSmoother.Next(value, isFinal);
        var prevSclRma = _sclRmaValues.Count >= _sclOffset ? _sclRmaValues[_sclRmaValues.Count - _sclOffset] : value;
        var prevMclRma = _mclRmaValues.Count >= _mclOffset ? _mclRmaValues[_mclRmaValues.Count - _mclOffset] : value;
        var scmOff = _fastMult * sclAtr;
        var mcmOff = _slowMult * mclAtr;
        var sct = prevSclRma + scmOff;
        var scb = prevSclRma - scmOff;
        var mct = prevMclRma + mcmOff;
        var mcb = prevMclRma - mcmOff;
        var scmm = (sct + scb) / 2;
        var mcmm = (mct + mcb) / 2;
        var omed = mct - mcb != 0 ? (scmm - mcb) / (mct - mcb) : 0;
        var oshort = mct - mcb != 0 ? (value - mcb) / (mct - mcb) : 0;

        if (isFinal)
        {
            _sclRmaValues.TryAdd(sclRma, out _);
            _mclRmaValues.TryAdd(mclRma, out _);
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "FastUpperBand", sct },
                { "SlowUpperBand", mct },
                { "FastMiddleBand", scmm },
                { "SlowMiddleBand", mcmm },
                { "FastLowerBand", scb },
                { "SlowLowerBand", mcb },
                { "OMed", omed },
                { "OShort", oshort }
            };
        }

        return new StreamingIndicatorStateResult(scmm, outputs);
    }

    public void Dispose()
    {
        _sclAtrSmoother.Dispose();
        _mclAtrSmoother.Dispose();
        _sclRmaSmoother.Dispose();
        _mclRmaSmoother.Dispose();
        _sclRmaValues.Dispose();
        _mclRmaValues.Dispose();
    }
}

public sealed class HybridConvolutionFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevOutput;
    private bool _hasPrev;

    public HybridConvolutionFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public HybridConvolutionFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.HybridConvolutionFilter;

    public void Reset()
    {
        _values.Clear();
        _prevOutput = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevOutput = _hasPrev ? _prevOutput : value;
        double output = 0;
        for (var j = 1; j <= _length; j++)
        {
            var signArg = MathHelper.MinOrMax((double)j / _length * Math.PI, 0.99, 0.01);
            var sign = 0.5 * (1 - Math.Cos(signArg));
            var priorArg = MathHelper.MinOrMax((double)(j - 1) / _length * Math.PI, 0.99, 0.01);
            var d = sign - (0.5 * (1 - Math.Cos(priorArg)));
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            output += ((sign * prevOutput) + ((1 - sign) * prevValue)) * d;
        }

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevOutput = output;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Hcf", output }
            };
        }

        return new StreamingIndicatorStateResult(output, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class IchimokuCloudState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _tenkanHigh;
    private readonly RollingWindowMin _tenkanLow;
    private readonly RollingWindowMax _kijunHigh;
    private readonly RollingWindowMin _kijunLow;
    private readonly RollingWindowMax _senkouHigh;
    private readonly RollingWindowMin _senkouLow;
    private readonly StreamingInputResolver _input;

    public IchimokuCloudState(int tenkanLength = 9, int kijunLength = 26, int senkouLength = 52,
        InputName inputName = InputName.Close)
    {
        _tenkanHigh = new RollingWindowMax(Math.Max(1, tenkanLength));
        _tenkanLow = new RollingWindowMin(Math.Max(1, tenkanLength));
        _kijunHigh = new RollingWindowMax(Math.Max(1, kijunLength));
        _kijunLow = new RollingWindowMin(Math.Max(1, kijunLength));
        _senkouHigh = new RollingWindowMax(Math.Max(1, senkouLength));
        _senkouLow = new RollingWindowMin(Math.Max(1, senkouLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public IchimokuCloudState(int tenkanLength, int kijunLength, int senkouLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _tenkanHigh = new RollingWindowMax(Math.Max(1, tenkanLength));
        _tenkanLow = new RollingWindowMin(Math.Max(1, tenkanLength));
        _kijunHigh = new RollingWindowMax(Math.Max(1, kijunLength));
        _kijunLow = new RollingWindowMin(Math.Max(1, kijunLength));
        _senkouHigh = new RollingWindowMax(Math.Max(1, senkouLength));
        _senkouLow = new RollingWindowMin(Math.Max(1, senkouLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.IchimokuCloud;

    public void Reset()
    {
        _tenkanHigh.Reset();
        _tenkanLow.Reset();
        _kijunHigh.Reset();
        _kijunLow.Reset();
        _senkouHigh.Reset();
        _senkouLow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var tenkanHigh = isFinal ? _tenkanHigh.Add(bar.High, out _) : _tenkanHigh.Preview(bar.High, out _);
        var tenkanLow = isFinal ? _tenkanLow.Add(bar.Low, out _) : _tenkanLow.Preview(bar.Low, out _);
        var kijunHigh = isFinal ? _kijunHigh.Add(bar.High, out _) : _kijunHigh.Preview(bar.High, out _);
        var kijunLow = isFinal ? _kijunLow.Add(bar.Low, out _) : _kijunLow.Preview(bar.Low, out _);
        var senkouHigh = isFinal ? _senkouHigh.Add(bar.High, out _) : _senkouHigh.Preview(bar.High, out _);
        var senkouLow = isFinal ? _senkouLow.Add(bar.Low, out _) : _senkouLow.Preview(bar.Low, out _);
        var tenkan = (tenkanHigh + tenkanLow) / 2;
        var kijun = (kijunHigh + kijunLow) / 2;
        var senkouSpanA = (tenkan + kijun) / 2;
        var senkouSpanB = (senkouHigh + senkouLow) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "TenkanSen", tenkan },
                { "KijunSen", kijun },
                { "SenkouSpanA", senkouSpanA },
                { "SenkouSpanB", senkouSpanB }
            };
        }

        return new StreamingIndicatorStateResult(tenkan, outputs);
    }

    public void Dispose()
    {
        _tenkanHigh.Dispose();
        _tenkanLow.Dispose();
        _kijunHigh.Dispose();
        _kijunLow.Dispose();
        _senkouHigh.Dispose();
        _senkouLow.Dispose();
    }
}

public sealed class IIRLeastSquaresEstimateState : IStreamingIndicatorState
{
    private readonly double _a;
    private readonly int _halfLength;
    private readonly StreamingInputResolver _input;
    private double _prevS;
    private double _prevSEma;
    private bool _hasPrev;

    public IIRLeastSquaresEstimateState(int length = 100, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _a = (double)4 / (resolved + 2);
        _halfLength = MathHelper.MinOrMax((int)Math.Ceiling((double)resolved / 2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public IIRLeastSquaresEstimateState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _a = (double)4 / (resolved + 2);
        _halfLength = MathHelper.MinOrMax((int)Math.Ceiling((double)resolved / 2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.IIRLeastSquaresEstimate;

    public void Reset()
    {
        _prevS = 0;
        _prevSEma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevS = _hasPrev ? _prevS : value;
        var prevSEma = _prevSEma;
        var sEma = CalculationsHelper.CalculateEMA(prevS, prevSEma, _halfLength);
        var s = (_a * value) + prevS - (_a * sEma);

        if (isFinal)
        {
            _prevS = s;
            _prevSEma = prevSEma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "IIRLse", s }
            };
        }

        return new StreamingIndicatorStateResult(s, outputs);
    }
}

public sealed class ImpulseMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly int _signalLength;
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly RollingWindowSum _signalSum;
    private readonly StreamingInputResolver _input;

    public ImpulseMovingAverageConvergenceDivergenceState(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 34, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _signalLength = Math.Max(1, signalLength);
        _ema1 = new EmaState(resolved);
        _ema2 = new EmaState(resolved);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSum = new RollingWindowSum(_signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ImpulseMovingAverageConvergenceDivergenceState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _signalLength = Math.Max(1, signalLength);
        _ema1 = new EmaState(resolved);
        _ema2 = new EmaState(resolved);
        _highSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSum = new RollingWindowSum(_signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ImpulseMovingAverageConvergenceDivergence;

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
        var mi = (2 * ema1) - ema2;
        var hi = _highSmoother.Next(bar.High, isFinal);
        var lo = _lowSmoother.Next(bar.Low, isFinal);
        var macd = mi > hi ? mi - hi : mi < lo ? mi - lo : 0;
        var macdSum = isFinal ? _signalSum.Add(macd, out var count) : _signalSum.Preview(macd, out count);
        var signal = count > 0 ? macdSum / count : 0;
        var histogram = macd - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Macd", macd },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _signalSum.Dispose();
    }
}
