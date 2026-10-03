#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Top")]
public sealed class GatorOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly AlligatorLineWindow _jaw, _teeth, _lips;
    private StreamingInputResolver _input = new(InputName.MedianPrice, null);
    public GatorOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13, int jawOffset = 8, int teethLength = 8, int teethOffset = 5, int lipsLength = 5, int lipsOffset = 3)
    { _jaw = new(maType, jawLength, jawOffset); _teeth = new(maType, teethLength, teethOffset); _lips = new(maType, lipsLength, lipsOffset); }
    public IndicatorName Name => IndicatorName.GatorOscillator;
    void ICustomInputConsumer.ReadCloseAsInput() => _input = new StreamingInputResolver(InputName.Close, null);
    public void Reset() { _jaw.Reset(); _teeth.Reset(); _lips.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var price = _input.GetValue(bar);
        var jaw = _jaw.Next(price, isFinal); var teeth = _teeth.Next(price, isFinal); var lips = _lips.Next(price, isFinal);
        var top = Math.Abs(jaw - teeth); var bottom = -Math.Abs(teeth - lips);
        return new(top, includeOutputs ? new Dictionary<string, double> { { "Top", top }, { "Bottom", bottom } } : null);
    }
    public void Dispose() { _jaw.Dispose(); _teeth.Dispose(); _lips.Dispose(); }
}

[PrimaryOutput("Gfe")]
public sealed class GeneralFilterEstimatorState : IStreamingIndicatorState, IDisposable
{
    private readonly GeneralFilterWindow _window; private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public GeneralFilterEstimatorState(int length = 100, double beta = 5.25, double gamma = 1, double zeta = 1) => _window = new(length, beta, gamma, zeta);
    public IndicatorName Name => IndicatorName.GeneralFilterEstimator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Gfe", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Gdema")]
public sealed class GeneralizedDoubleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly GeneralizedDoubleWindow _window;
    public GeneralizedDoubleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 5, double factor = .7) => _window = new(maType, length, factor);
    public IndicatorName Name => IndicatorName.GeneralizedDoubleExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Gdema", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("GOsc")]
public sealed class GOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _bSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public GOscillatorState(int length = 14)
    {
        _length = Math.Max(1, length);
        _bSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
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

[PrimaryOutput("Gtf")]
public sealed class GrandTrendForecastingState : IStreamingIndicatorState, IDisposable
{
    private readonly GrandForecastWindow _window;
    public GrandTrendForecastingState(int length = 100, int forecastLength = 200, double mult = 2) => _window = new(length, forecastLength, mult);
    public IndicatorName Name => IndicatorName.GrandTrendForecasting;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Trend, includeOutputs ? new Dictionary<string, double> { { "Gtf", point.Trend }, { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Gla")]
public sealed class GroverLlorensActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly GroverWindow? _wide;
    private readonly IMovingAverageSmoother _atrSmoother = null!;
    private readonly double _mult;
    private readonly StreamingInputResolver _input = default;
    private double _prevValue;
    private double _prevTs;
    private bool _hasPrev;

    public GroverLlorensActivatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, double mult = 5)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        if (StrengthWindow.Supports(maType)) { _wide = new(maType, length, 1, mult, false); return; }
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GroverLlorensActivator;

    public void Reset()
    {
        if (_wide is not null) { _wide.Reset(); return; }
        _atrSmoother.Reset();
        _prevValue = 0;
        _prevTs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null) { var point = _wide.Next(bar.Close, bar.High, bar.Low, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Gla", point.Line } } : null); }
        var value = _input.GetValue(bar);
        // For TrueRange on first bar, use current close to avoid inflated TR
        var prevValue = _hasPrev ? _prevValue : value;
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
        if (_wide is not null) return;
        _atrSmoother.Dispose();
    }
}

[PrimaryOutput("Glco")]
public sealed class GroverLlorensCycleOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly GroverWindow? _wide;
    private readonly IMovingAverageSmoother _atrSmoother = null!;
    private readonly IMovingAverageSmoother _oscSmoother = null!;
    private readonly RsiState _rsi = null!;
    private readonly double _mult;
    private readonly StreamingInputResolver _input = default;
    private double _prevValue;
    private double _prevTs;
    private bool _hasPrev;

    public GroverLlorensCycleOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 100, int smoothLength = 20, double mult = 10)
    {
        if (double.IsNaN(mult) || double.IsInfinity(mult)) throw new ArgumentOutOfRangeException(nameof(mult));
        if (StrengthWindow.Supports(maType)) { _wide = new(maType, length, smoothLength, mult, true); return; }
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _oscSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _rsi = new RsiState(maType, Math.Max(1, smoothLength));
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GroverLlorensCycleOscillator;

    public void Reset()
    {
        if (_wide is not null) { _wide.Reset(); return; }
        _atrSmoother.Reset();
        _oscSmoother.Reset();
        _rsi.Reset();
        _prevValue = 0;
        _prevTs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null) { var point = _wide.Next(bar.Close, bar.High, bar.Low, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Glco", point.Line } } : null); }
        var value = _input.GetValue(bar);
        // The first bar has no previous close, so it stands in for itself (TR = High - Low), as the batch's
        // true range and AverageTrueRangeState both do. A zero there made the first TR the whole high.
        var prevValue = _hasPrev ? _prevValue : value;
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
        if (_wide is not null) return;
        _atrSmoother.Dispose();
        _oscSmoother.Dispose();
        _rsi.Dispose();
    }
}

[PrimaryOutput("Cbl")]
public sealed class GuppyCountBackLineState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly StreamingInputResolver _input;

    public GuppyCountBackLineState(int length = 21)
    {
        _length = Math.Max(1, length);
        _highs = new PooledRingBuffer<double>((_length * 2) + 1);
        _lows = new PooledRingBuffer<double>((_length * 2) + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GuppyCountBackLine;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var cbl = value;
        int highPivot = 0, lowPivot = 0;
        var highest = EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, 0);
        var lowest = EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, 0);
        for (var offset = 1; offset < _length && offset <= _highs.Count; offset++)
        {
            var h = EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, offset); var l = EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, offset);
            if (h > highest) { highest = h; highPivot = offset; }
            if (l < lowest) { lowest = l; lowPivot = offset; }
        }
        // The latest extreme determines direction; an outside-bar tie uses its high.
        var rising = highPivot <= lowPivot;
        var pivot = rising ? highPivot : lowPivot;
        var level = rising ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, pivot) : EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, pivot);
        var count = 0;
        for (var offset = pivot + 1; offset <= pivot + _length && offset <= _highs.Count; offset++)
        {
            var candidate = rising ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, offset) : EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, offset);
            if (rising ? candidate < level : candidate > level)
            {
                level = candidate;
                if (++count == 2) { cbl = level; break; }
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
        _highs.Dispose();
        _lows.Dispose();
    }
}

[PrimaryOutput("FastDistance")]
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
        int length12 = 60)
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
        _input = new StreamingInputResolver(InputName.Close, null);
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


        var fastDistance = GuppyRibbonArithmetic.Distance(ema1, ema2, ema3, ema4, ema5, ema6);
        var slowDistance = GuppyRibbonArithmetic.Distance(ema7, ema8, ema9, ema10, ema11, ema12);

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

[PrimaryOutput("SuperGmmaOsc")]
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
    private readonly ExactPartialMeanWindow _oscRawSum;
    private readonly StreamingInputResolver _input;
    private readonly int _smoothLength;

    public GuppyMultipleMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 3, int length2 = 5,
        int length3 = 7, int length4 = 8, int length5 = 9, int length6 = 10, int length7 = 11, int length8 = 12,
        int length9 = 13, int length10 = 15, int length11 = 17, int length12 = 19, int length13 = 21, int length14 = 23,
        int length15 = 25, int length16 = 28, int length17 = 30, int length18 = 31, int length19 = 34, int length20 = 35,
        int length21 = 37, int length22 = 40, int length23 = 43, int length24 = 45, int length25 = 46, int length26 = 49,
        int length27 = 50, int length28 = 52, int length29 = 55, int length30 = 58, int length31 = 60, int length32 = 61,
        int length33 = 64, int length34 = 67, int length35 = 70, int smoothLength = 1, int signalLength = 13)
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
        _oscRawSum = new ExactPartialMeanWindow(_smoothLength);
        _input = new StreamingInputResolver(InputName.Close, null);
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

        var superGmmaFast = GuppyRibbonArithmetic.Mean(stackalloc double[] { emaF1, emaF2, emaF3, emaF4, emaF5, emaF6, emaF7, emaF8, emaF9, emaF10, emaF11 });
        var superGmmaSlow = GuppyRibbonArithmetic.Mean(stackalloc double[] { emaS1, emaS2, emaS3, emaS4, emaS5, emaS6, emaS7, emaS8, emaS9, emaS10, emaS11, emaS12, emaS13, emaS14, emaS15, emaS16 });
        var superGmmaOscRaw = GuppyRibbonArithmetic.Percent(superGmmaFast, superGmmaSlow);
        var superGmmaOsc = _oscRawSum.Next(superGmmaOscRaw, isFinal);
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

[PrimaryOutput("Ht")]
public sealed class HalfTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly HalfTrendWindow _window;
    public HalfTrendState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 2, int atrLength = 100) => _window = new(maType, length, atrLength);
    public IndicatorName Name => IndicatorName.HalfTrend;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Ht", value } } : null); }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Hf")]
public sealed class HampelFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly HampelWindow _window;
    public HampelFilterState(int length = 14, double scalingFactor = 3) => _window = new(length, scalingFactor);
    public IndicatorName Name => IndicatorName.HampelFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { ["Hf"] = value } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Up")]
public sealed class HawkeyeVolumeIndicatorState : IStreamingIndicatorState, ICustomInputConsumer
{
    private readonly HawkeyeWindow _window;
    private StreamingInputResolver _input;
    public HawkeyeVolumeIndicatorState(int length = 200, double divisor = 3.6)
    { _window = new(length, divisor); _input = new StreamingInputResolver(InputName.MedianPrice, null); }
    public IndicatorName Name => IndicatorName.HawkeyeVolumeIndicator;
    void ICustomInputConsumer.ReadCloseAsInput() => _input = new StreamingInputResolver(InputName.Close, null);
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var midpoint = _input.GetValue(bar);
        var point = _window.Next(midpoint, bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Up, includeOutputs ? new Dictionary<string, double> { { "Up", point.Up }, { "Dn", point.Down } } : null);
    }
}

[PrimaryOutput("Hwma")]
public sealed class HendersonWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly HendersonWindow _window;
    public HendersonWeightedMovingAverageState(int length = 7) => _window = new(length);
    public IndicatorName Name => IndicatorName.HendersonWeightedMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Hwma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Hpi")]
public sealed class HerrickPayoffIndexState : IStreamingIndicatorState, ICustomInputConsumer
{
    private readonly double _pointValue;
    private StreamingInputResolver _input;
    private double _prevValue;
    private double _prevOpen;
    private double _prevClose;
    private double _prevK;
    private bool _hasPrev;

    public HerrickPayoffIndexState(double pointValue = 100)
    {
        _pointValue = pointValue;
        _input = new StreamingInputResolver(InputName.MedianPrice, null);
    }

    public IndicatorName Name => IndicatorName.HerrickPayoffIndex;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

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

[PrimaryOutput("Zmbti")]
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

    public HighLowIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _advSum = new RollingWindowSum(_length);
        _loSum = new RollingWindowSum(_length);
        _smoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(_length)
            : MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
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
            ? 100 * advSum / (advSum + loSum)
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

[PrimaryOutput("MiddleBand")]
public sealed class HirashimaSugitaRSState : IStreamingIndicatorState, IDisposable
{
    private readonly HirashimaWindow? _wide;
    private readonly IMovingAverageSmoother _ema = null!;
    private readonly IMovingAverageSmoother _wma = null!;
    private readonly LinearRegressionState _s1Regression = null!;
    private readonly LinearRegressionState _s2Regression = null!;
    private readonly StreamingInputResolver _input;
    private double _d1Value;
    private double _d2Value;
    private double _prevS2;
    private bool _hasPrev;

    public HirashimaSugitaRSState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 1000)
    {
        if (StrengthWindow.Supports(maType)) { _wide = new(maType, length); return; }
        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved);
        _wma = MovingAverageSmootherFactory.Create(maType, resolved);
        _s1Regression = new LinearRegressionState(resolved, _ => _d1Value);
        _s2Regression = new LinearRegressionState(resolved, _ => _d2Value);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.HirashimaSugitaRS;

    public void Reset()
    {
        if (_wide is not null) { _wide.Reset(); return; }
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
        if (_wide is not null)
        {
            StreamingInputValidation.Validate(bar); var point = _wide.Next(bar.Close, isFinal);
            var keys = new[] { "UpperBand1", "UpperBand2", "MiddleBand", "LowerBand1", "LowerBand2" };
            return new StreamingIndicatorStateResult(point.Bands[2], includeOutputs ? keys.Select((key, i) => (key, value: point.Bands[i])).ToDictionary(v => v.key, v => v.value) : null);
        }
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
        var upper1 = HirashimaWindow.Band(basis, wma, 1);
        var lower1 = HirashimaWindow.Band(basis, wma, -1);
        var upper2 = HirashimaWindow.Band(basis, wma, 2);
        var lower2 = HirashimaWindow.Band(basis, wma, -2);

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
        if (_wide is not null) return;
        _ema.Dispose();
        _wma.Dispose();
        _s1Regression.Dispose();
        _s2Regression.Dispose();
    }
}

[PrimaryOutput("Hema")]
public sealed class HoltExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly HoltWindow _window;
    public HoltExponentialMovingAverageState(int alphaLength = 20, int gammaLength = 20) => _window = new(alphaLength, gammaLength);
    public IndicatorName Name => IndicatorName.HoltExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Hema", value } } : null);
    }
}

[PrimaryOutput("He")]
public sealed class HullEstimateState : IStreamingIndicatorState, IDisposable
{
    private readonly HullEstimateWindow _window;
    public HullEstimateState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.HullEstimate;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "He", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class HurstBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly HurstBandWindow _window;
    public HurstBandsState(int length = 10, double innerMult = 1.6, double outerMult = 2.6, double extremeMult = 4.2) { _window = new(length, innerMult, outerMult, extremeMult); }
    public IndicatorName Name => IndicatorName.HurstBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperExtremeBand", point.UpperExtreme }, { "UpperOuterBand", point.UpperOuter }, { "UpperInnerBand", point.UpperInner }, { "MiddleBand", point.Middle }, { "LowerExtremeBand", point.LowerExtreme }, { "LowerOuterBand", point.LowerOuter }, { "LowerInnerBand", point.LowerInner } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("FastMiddleBand")]
public sealed class HurstCycleChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly HurstCycleWindow _window;
    public HurstCycleChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int fastLength = 10, int slowLength = 30, double fastMult = 1, double slowMult = 3) { _window = new(maType, fastLength, slowLength, fastMult, slowMult); }
    public IndicatorName Name => IndicatorName.HurstCycleChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(p.FastMiddle, includeOutputs ? new Dictionary<string, double> { { "FastUpperBand", p.FastUpper }, { "FastMiddleBand", p.FastMiddle }, { "FastLowerBand", p.FastLower }, { "SlowUpperBand", p.SlowUpper }, { "SlowMiddleBand", p.SlowMiddle }, { "SlowLowerBand", p.SlowLower }, { "OMed", p.OMed }, { "OShort", p.OShort } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Hcf")]
public sealed class HybridConvolutionFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _prevOutput;
    private bool _hasPrev;

    public HybridConvolutionFilterState(int length = 14)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
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
            // Both cosine arguments are the window position scaled by pi, and neither is clamped, so the
            // weights d telescope across the window to exactly 1 and a constant series is a fixed point.
            var sign = 0.5 * (1 - Math.Cos((double)j / _length * Math.PI));
            var d = sign - (0.5 * (1 - Math.Cos((double)(j - 1) / _length * Math.PI)));
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

[PrimaryOutput("TenkanSen")]
public sealed class IchimokuCloudState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _tenkanHigh;
    private readonly RollingWindowMin _tenkanLow;
    private readonly RollingWindowMax _kijunHigh;
    private readonly RollingWindowMin _kijunLow;
    private readonly RollingWindowMax _senkouHigh;
    private readonly RollingWindowMin _senkouLow;
    private readonly StreamingInputResolver _input;

    public IchimokuCloudState(int tenkanLength = 9, int kijunLength = 26, int senkouLength = 52)
    {
        _tenkanHigh = new RollingWindowMax(Math.Max(1, tenkanLength));
        _tenkanLow = new RollingWindowMin(Math.Max(1, tenkanLength));
        _kijunHigh = new RollingWindowMax(Math.Max(1, kijunLength));
        _kijunLow = new RollingWindowMin(Math.Max(1, kijunLength));
        _senkouHigh = new RollingWindowMax(Math.Max(1, senkouLength));
        _senkouLow = new RollingWindowMin(Math.Max(1, senkouLength));
        _input = new StreamingInputResolver(InputName.Close, null);
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
        var tenkan = PriceMean.Of(tenkanHigh, tenkanLow);
        var kijun = PriceMean.Of(kijunHigh, kijunLow);
        var senkouSpanA = PriceMean.Of(tenkan, kijun);
        var senkouSpanB = PriceMean.Of(senkouHigh, senkouLow);

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

[PrimaryOutput("IIRLse")]
public sealed class IIRLeastSquaresEstimateState : IStreamingIndicatorState
{
    private readonly IirLeastSquaresWindow _window;
    public IIRLeastSquaresEstimateState(int length = 100) => _window = new(length);
    public IndicatorName Name => IndicatorName.IIRLeastSquaresEstimate;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "IIRLse", value } } : null);
    }
}

[PrimaryOutput("Macd")]
public sealed class ImpulseMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly int _signalLength;
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly RoundedPartialMeanSmoother _signalSum;
    private StreamingInputResolver _input;

    public ImpulseMovingAverageConvergenceDivergenceState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 34, int signalLength = 9)
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

    public IndicatorName Name => IndicatorName.ImpulseMovingAverageConvergenceDivergence;

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
        var macd = RoundedImpulseOscillator.Line(mi, hi, lo, false);
        var signal = _signalSum.Next(macd, isFinal);
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
