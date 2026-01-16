using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class CoralTrendIndicatorState : IStreamingIndicatorState
{
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _c4;
    private readonly double _c5;
    private readonly double _cdCube;
    private double _i1;
    private double _i2;
    private double _i3;
    private double _i4;
    private double _i5;
    private double _i6;
    private readonly StreamingInputResolver _input;

    public CoralTrendIndicatorState(int length = 21, double cd = 0.4, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var di = ((double)(resolved - 1) / 2) + 1;
        _c1 = 2 / (di + 1);
        _c2 = 1 - _c1;
        var cdSquared = cd * cd;
        _cdCube = cdSquared * cd;
        _c3 = 3 * (cdSquared + _cdCube);
        _c4 = -3 * ((2 * cdSquared) + cd + _cdCube);
        _c5 = (3 * cd) + 1 + _cdCube + (3 * cdSquared);
        _input = new StreamingInputResolver(inputName, null);
    }

    public CoralTrendIndicatorState(int length, double cd, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var di = ((double)(resolved - 1) / 2) + 1;
        _c1 = 2 / (di + 1);
        _c2 = 1 - _c1;
        var cdSquared = cd * cd;
        _cdCube = cdSquared * cd;
        _c3 = 3 * (cdSquared + _cdCube);
        _c4 = -3 * ((2 * cdSquared) + cd + _cdCube);
        _c5 = (3 * cd) + 1 + _cdCube + (3 * cdSquared);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.CoralTrendIndicator;

    public void Reset()
    {
        _i1 = 0;
        _i2 = 0;
        _i3 = 0;
        _i4 = 0;
        _i5 = 0;
        _i6 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var i1 = (_c1 * value) + (_c2 * _i1);
        var i2 = (_c1 * i1) + (_c2 * _i2);
        var i3 = (_c1 * i2) + (_c2 * _i3);
        var i4 = (_c1 * i3) + (_c2 * _i4);
        var i5 = (_c1 * i4) + (_c2 * _i5);
        var i6 = (_c1 * i5) + (_c2 * _i6);
        var bfr = (-_cdCube * i6) + (_c3 * i5) + (_c4 * i4) + (_c5 * i3);

        if (isFinal)
        {
            _i1 = i1;
            _i2 = i2;
            _i3 = i3;
            _i4 = i4;
            _i5 = i5;
            _i6 = i6;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cti", bfr }
            };
        }

        return new StreamingIndicatorStateResult(bfr, outputs);
    }
}

public sealed class DecisionPointPriceMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _smPmol2;
    private readonly double _smPmol;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevPmol2;
    private double _prevPmol;
    private double _prevValue;
    private bool _hasPrev;

    public DecisionPointPriceMomentumOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 35, int length2 = 20, int signalLength = 10, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _smPmol2 = (double)2 / resolved1;
        _smPmol = (double)2 / resolved2;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DecisionPointPriceMomentumOscillatorState(MovingAvgType maType, int length1, int length2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _smPmol2 = (double)2 / resolved1;
        _smPmol = (double)2 / resolved2;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DecisionPointPriceMomentumOscillator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevPmol2 = 0;
        _prevPmol = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var ival = prevValue != 0 ? value / prevValue * 100 : 100;

        var pmol2 = ((ival - 100 - _prevPmol2) * _smPmol2) + _prevPmol2;
        var pmol = (((10 * pmol2) - _prevPmol) * _smPmol) + _prevPmol;

        var signal = _signalSmoother.Next(pmol, isFinal);
        var histogram = pmol - signal;

        if (isFinal)
        {
            _prevValue = value;
            _prevPmol2 = pmol2;
            _prevPmol = pmol;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Dppmo", pmol },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(pmol, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class DemarkerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _maxSmoother;
    private readonly IMovingAverageSmoother _minSmoother;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public DemarkerState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _maxSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _minSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.Demarker;

    public void Reset()
    {
        _maxSmoother.Reset();
        _minSmoother.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;

        var dMax = bar.High > prevHigh ? bar.High - prevHigh : 0;
        var dMin = bar.Low < prevLow ? prevLow - bar.Low : 0;

        var maxMa = _maxSmoother.Next(dMax, isFinal);
        var minMa = _minSmoother.Next(dMin, isFinal);
        var demarker = maxMa + minMa != 0
            ? MathHelper.MinOrMax((maxMa / (maxMa + minMa)) * 100, 100, 0)
            : 0;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dm", demarker }
            };
        }

        return new StreamingIndicatorStateResult(demarker, outputs);
    }

    public void Dispose()
    {
        _maxSmoother.Dispose();
        _minSmoother.Dispose();
    }
}

public sealed class DemarkPivotPointsState : IStreamingIndicatorState
{
    private double _prevClose;
    private double _prevOpen;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.DemarkPivotPoints;

    public void Reset()
    {
        _prevClose = 0;
        _prevOpen = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevOpen = _hasPrev ? _prevOpen : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var x = prevClose < prevOpen
            ? prevHigh + (2 * prevLow) + prevClose
            : prevClose > prevOpen
                ? (2 * prevHigh) + prevLow + prevClose
                : prevHigh + prevLow + (2 * prevClose);

        var pivot = x / 4;
        var ratio = x / 2;
        var support = ratio - prevHigh;
        var resistance = ratio - prevLow;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _prevOpen = bar.Open;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
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

public sealed class DemarkPressureRatioV1State : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _bpSum;
    private readonly RollingWindowSum _spSum;
    private double _prevClose;
    private bool _hasPrev;

    public DemarkPressureRatioV1State(int length = 13)
    {
        var resolved = Math.Max(1, length);
        _bpSum = new RollingWindowSum(resolved);
        _spSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.DemarkPressureRatioV1;

    public void Reset()
    {
        _bpSum.Reset();
        _spSum.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var gapup = prevClose != 0 ? (bar.Open - prevClose) / prevClose : 0;
        var gapdown = bar.Open != 0 ? (prevClose - bar.Open) / bar.Open : 0;

        var bp = gapup > 0.15
            ? (bar.High - prevClose + bar.Close - bar.Low) * bar.Volume
            : bar.Close > bar.Open ? (bar.Close - bar.Open) * bar.Volume : 0;

        var sp = gapdown > 0.15
            ? (prevClose - bar.Low + bar.High - bar.Close) * bar.Volume
            : bar.Close < bar.Open ? (bar.Close - bar.Open) * bar.Volume : 0;

        var bpSum = isFinal ? _bpSum.Add(bp, out _) : _bpSum.Preview(bp, out _);
        var spSum = isFinal ? _spSum.Add(sp, out _) : _spSum.Preview(sp, out _);
        var pressureRatio = bpSum - spSum != 0
            ? MathHelper.MinOrMax(100 * bpSum / (bpSum - spSum), 100, 0)
            : 0;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dpr", pressureRatio }
            };
        }

        return new StreamingIndicatorStateResult(pressureRatio, outputs);
    }

    public void Dispose()
    {
        _bpSum.Dispose();
        _spSum.Dispose();
    }
}

public sealed class DemarkPressureRatioV2State : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _bpSum;
    private readonly RollingWindowSum _spSum;

    public DemarkPressureRatioV2State(int length = 10)
    {
        var resolved = Math.Max(1, length);
        _bpSum = new RollingWindowSum(resolved);
        _spSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.DemarkPressureRatioV2;

    public void Reset()
    {
        _bpSum.Reset();
        _spSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var delta = bar.Close - bar.Open;
        var trueRange = bar.High - bar.Low;
        var ratio = trueRange != 0 ? delta / trueRange : 0;
        var buyingPressure = delta > 0 ? ratio * bar.Volume : 0;
        var sellingPressure = delta < 0 ? ratio * bar.Volume : 0;

        var bpSum = isFinal ? _bpSum.Add(buyingPressure, out _) : _bpSum.Preview(buyingPressure, out _);
        var spSum = isFinal ? _spSum.Add(sellingPressure, out _) : _spSum.Preview(sellingPressure, out _);
        var denom = bpSum + Math.Abs(spSum);
        var pressureRatio = denom != 0 ? MathHelper.MinOrMax(100 * bpSum / denom, 100, 0) : 50;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dpr", pressureRatio }
            };
        }

        return new StreamingIndicatorStateResult(pressureRatio, outputs);
    }

    public void Dispose()
    {
        _bpSum.Dispose();
        _spSum.Dispose();
    }
}

public sealed class DemarkRangeExpansionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _s1Sum;
    private readonly RollingWindowSum _s2Sum;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private readonly PooledRingBuffer<double> _closes;

    public DemarkRangeExpansionIndexState(int length = 5)
    {
        var resolved = Math.Max(1, length);
        _s1Sum = new RollingWindowSum(resolved);
        _s2Sum = new RollingWindowSum(resolved);
        _highs = new PooledRingBuffer<double>(9);
        _lows = new PooledRingBuffer<double>(9);
        _closes = new PooledRingBuffer<double>(9);
    }

    public IndicatorName Name => IndicatorName.DemarkRangeExpansionIndex;

    public void Reset()
    {
        _s1Sum.Reset();
        _s2Sum.Reset();
        _highs.Clear();
        _lows.Clear();
        _closes.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;

        var prevHigh2 = StreamingWindowHelper.GetRecentValue(_highs, 2, high);
        var prevHigh5 = StreamingWindowHelper.GetRecentValue(_highs, 5, high);
        var prevHigh6 = StreamingWindowHelper.GetRecentValue(_highs, 6, high);
        var prevLow2 = StreamingWindowHelper.GetRecentValue(_lows, 2, low);
        var prevLow5 = StreamingWindowHelper.GetRecentValue(_lows, 5, low);
        var prevLow6 = StreamingWindowHelper.GetRecentValue(_lows, 6, low);
        var prevClose7 = StreamingWindowHelper.GetRecentValue(_closes, 7, close);
        var prevClose8 = StreamingWindowHelper.GetRecentValue(_closes, 8, close);

        double n = (high >= prevLow5 || high >= prevLow6) && (low <= prevHigh5 || low <= prevHigh6) ? 0 : 1;
        double m = prevHigh2 >= prevClose8 && (prevLow2 <= prevClose7 || prevLow2 <= prevClose8) ? 0 : 1;
        var s = high - prevHigh2 + (low - prevLow2);

        var s1 = n * m * s;
        var s2 = Math.Abs(s);

        var s1Sum = isFinal ? _s1Sum.Add(s1, out _) : _s1Sum.Preview(s1, out _);
        var s2Sum = isFinal ? _s2Sum.Add(s2, out _) : _s2Sum.Preview(s2, out _);
        var rei = s2Sum != 0 ? s1Sum / s2Sum * 100 : 0;

        if (isFinal)
        {
            _highs.TryAdd(high, out _);
            _lows.TryAdd(low, out _);
            _closes.TryAdd(close, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Drei", rei }
            };
        }

        return new StreamingIndicatorStateResult(rei, outputs);
    }

    public void Dispose()
    {
        _s1Sum.Dispose();
        _s2Sum.Dispose();
        _highs.Dispose();
        _lows.Dispose();
        _closes.Dispose();
    }
}

public sealed class DemarkReversalPointsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public DemarkReversalPointsState(int length1 = 9, int length2 = 4, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _values = new PooledRingBuffer<double>(_length1 + _length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DemarkReversalPointsState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _values = new PooledRingBuffer<double>(_length1 + _length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DemarkReversalPoints;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double uCount = 0;
        double dCount = 0;

        for (var j = 0; j < _length1; j++)
        {
            var current = StreamingWindowHelper.GetRecentValue(_values, j, value);
            var prev = StreamingWindowHelper.GetRecentValue(_values, j + _length2, value);

            uCount += current > prev ? 1 : 0;
            dCount += current < prev ? 1 : 0;
        }

        var drp = dCount == _length1 ? 1 : uCount == _length1 ? -1 : 0;
        var drpPrice = drp != 0 ? value : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Drp", drpPrice }
            };
        }

        return new StreamingIndicatorStateResult(drpPrice, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class DemarkSetupIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public DemarkSetupIndicatorState(int length = 4, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DemarkSetupIndicatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DemarkSetupIndicator;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double uCount = 0;
        double dCount = 0;

        for (var j = 0; j < _length; j++)
        {
            var current = StreamingWindowHelper.GetRecentValue(_values, j, value);
            var prev = StreamingWindowHelper.GetRecentValue(_values, j + _length, value);

            uCount += current > prev ? 1 : 0;
            dCount += current < prev ? 1 : 0;
        }

        var drp = dCount == _length ? 1 : uCount == _length ? -1 : 0;
        var drpPrice = drp != 0 ? value : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dsi", drpPrice }
            };
        }

        return new StreamingIndicatorStateResult(drpPrice, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class DiNapoliMovingAverageConvergenceDivergenceState : IStreamingIndicatorState
{
    private readonly double _scAlpha;
    private readonly double _lcAlpha;
    private readonly double _spAlpha;
    private readonly StreamingInputResolver _input;
    private double _fast;
    private double _slow;
    private double _signal;

    public DiNapoliMovingAverageConvergenceDivergenceState(double lc = 17.5185, double sc = 8.3896, double sp = 9.0503,
        InputName inputName = InputName.Close)
    {
        _scAlpha = 2 / (1 + sc);
        _lcAlpha = 2 / (1 + lc);
        _spAlpha = 2 / (1 + sp);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DiNapoliMovingAverageConvergenceDivergenceState(double lc, double sc, double sp, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _scAlpha = 2 / (1 + sc);
        _lcAlpha = 2 / (1 + lc);
        _spAlpha = 2 / (1 + sp);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DiNapoliMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _fast = 0;
        _slow = 0;
        _signal = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast + (_scAlpha * (value - _fast));
        var slow = _slow + (_lcAlpha * (value - _slow));
        var macd = fast - slow;
        var signal = _signal + (_spAlpha * (macd - _signal));
        var histogram = macd - signal;

        if (isFinal)
        {
            _fast = fast;
            _slow = slow;
            _signal = signal;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "FastS", fast },
                { "SlowS", slow },
                { "Macd", macd },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }
}

public sealed class DiNapoliPercentagePriceOscillatorState : IStreamingIndicatorState
{
    private readonly double _scAlpha;
    private readonly double _lcAlpha;
    private readonly double _spAlpha;
    private readonly StreamingInputResolver _input;
    private double _fast;
    private double _slow;
    private double _signal;

    public DiNapoliPercentagePriceOscillatorState(double lc = 17.5185, double sc = 8.3896, double sp = 9.0503,
        InputName inputName = InputName.Close)
    {
        _scAlpha = 2 / (1 + sc);
        _lcAlpha = 2 / (1 + lc);
        _spAlpha = 2 / (1 + sp);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DiNapoliPercentagePriceOscillatorState(double lc, double sc, double sp, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _scAlpha = 2 / (1 + sc);
        _lcAlpha = 2 / (1 + lc);
        _spAlpha = 2 / (1 + sp);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DiNapoliPercentagePriceOscillator;

    public void Reset()
    {
        _fast = 0;
        _slow = 0;
        _signal = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast + (_scAlpha * (value - _fast));
        var slow = _slow + (_lcAlpha * (value - _slow));
        var macd = fast - slow;
        var ppo = slow != 0 ? 100 * macd / slow : 0;
        var signal = _signal + (_spAlpha * (ppo - _signal));
        var histogram = ppo - signal;

        if (isFinal)
        {
            _fast = fast;
            _slow = slow;
            _signal = signal;
        }

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
}

public sealed class DiNapoliPreferredStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly double _length2;
    private readonly double _length3;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _r;
    private double _s;

    public DiNapoliPreferredStochasticOscillatorState(int length1 = 8, int length2 = 3, int length3 = 3,
        InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DiNapoliPreferredStochasticOscillatorState(int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _length3 = Math.Max(1, length3);
        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DiNapoliPreferredStochasticOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _r = 0;
        _s = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var fast = range != 0 ? MathHelper.MinOrMax((value - lowest) / range * 100, 100, 0) : 0;
        var r = _r + ((fast - _r) / _length2);
        var s = _s + ((r - _s) / _length3);

        if (isFinal)
        {
            _r = r;
            _s = s;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dpso", r },
                { "Signal", s }
            };
        }

        return new StreamingIndicatorStateResult(r, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class DistanceWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public DistanceWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DistanceWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DistanceWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        double weightedSum = 0;

        for (var j = 0; j < _length; j++)
        {
            var prevValue = StreamingWindowHelper.GetRecentValue(_values, j, value);
            double distanceSum = 0;
            for (var k = 0; k < _length; k++)
            {
                var prevValue2 = StreamingWindowHelper.GetRecentValue(_values, k, value);
                distanceSum += Math.Abs(prevValue - prevValue2);
            }

            var weight = distanceSum != 0 ? 1 / distanceSum : 0;
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var dwma = weightedSum != 0 ? sum / weightedSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dwma", dwma }
            };
        }

        return new StreamingIndicatorStateResult(dwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class DMIStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _dmPlus;
    private readonly IMovingAverageSmoother _dmMinus;
    private readonly IMovingAverageSmoother _tr;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _slowK;
    private readonly IMovingAverageSmoother _dmiStoch;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public DMIStochasticState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10, int length2 = 10,
        int length3 = 3, int length4 = 3)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _dmPlus = MovingAverageSmootherFactory.Create(maType, resolved1);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, resolved1);
        _tr = MovingAverageSmootherFactory.Create(maType, resolved1);
        _maxWindow = new RollingWindowMax(resolved2);
        _minWindow = new RollingWindowMin(resolved2);
        _slowK = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _dmiStoch = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
    }

    public IndicatorName Name => IndicatorName.DMIStochastic;

    public void Reset()
    {
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _slowK.Reset();
        _dmiStoch.Reset();
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

        var highDiff = bar.High - prevHigh;
        var lowDiff = prevLow - bar.Low;
        var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);

        var dmPlus14 = _dmPlus.Next(dmPlus, isFinal);
        var dmMinus14 = _dmMinus.Next(dmMinus, isFinal);
        var tr14 = _tr.Next(tr, isFinal);

        var diPlus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmPlus14 / tr14, 100, 0) : 0;
        var diMinus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmMinus14 / tr14, 100, 0) : 0;
        var dmiOscillator = diMinus - diPlus;

        var highest = isFinal ? _maxWindow.Add(dmiOscillator, out _) : _maxWindow.Preview(dmiOscillator, out _);
        var lowest = isFinal ? _minWindow.Add(dmiOscillator, out _) : _minWindow.Preview(dmiOscillator, out _);
        var range = highest - lowest;
        var fastK = range != 0
            ? MathHelper.MinOrMax((dmiOscillator - lowest) / range * 100, 100, 0)
            : 0;

        var slowK = _slowK.Next(fastK, isFinal);
        var dmiStoch = _dmiStoch.Next(slowK, isFinal);

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
            outputs = new Dictionary<string, double>(1)
            {
                { "DmiStochastic", dmiStoch }
            };
        }

        return new StreamingIndicatorStateResult(dmiStoch, outputs);
    }

    public void Dispose()
    {
        _dmPlus.Dispose();
        _dmMinus.Dispose();
        _tr.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _slowK.Dispose();
        _dmiStoch.Dispose();
    }
}

public sealed class DoubleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;

    public DoubleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DoubleExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleExponentialMovingAverage;

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
        var dema = (2 * ema1) - ema2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dema", dema }
            };
        }

        return new StreamingIndicatorStateResult(dema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

public sealed class DoubleExponentialSmoothingState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _gamma;
    private readonly StreamingInputResolver _input;
    private double _prevS;
    private double _prevS2;

    public DoubleExponentialSmoothingState(double alpha = 0.01, double gamma = 0.9, InputName inputName = InputName.Close)
    {
        _alpha = alpha;
        _gamma = gamma;
        _input = new StreamingInputResolver(inputName, null);
    }

    public DoubleExponentialSmoothingState(double alpha, double gamma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = alpha;
        _gamma = gamma;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleExponentialSmoothing;

    public void Reset()
    {
        _prevS = 0;
        _prevS2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sChg = _prevS - _prevS2;
        var s = (_alpha * value) + ((1 - _alpha) * (_prevS + (_gamma * (sChg + ((1 - _gamma) * sChg)))));

        if (isFinal)
        {
            _prevS2 = _prevS;
            _prevS = s;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Des", s }
            };
        }

        return new StreamingIndicatorStateResult(s, outputs);
    }
}

public sealed class DoubleSmoothedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _topSmoother1;
    private readonly IMovingAverageSmoother _topSmoother2;
    private readonly IMovingAverageSmoother _botSmoother1;
    private readonly IMovingAverageSmoother _botSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public DoubleSmoothedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 5, int length3 = 25, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _maxWindow = new RollingWindowMax(resolved1);
        _minWindow = new RollingWindowMin(resolved1);
        _topSmoother1 = MovingAverageSmootherFactory.Create(maType, resolved2);
        _topSmoother2 = MovingAverageSmootherFactory.Create(maType, resolved3);
        _botSmoother1 = MovingAverageSmootherFactory.Create(maType, resolved2);
        _botSmoother2 = MovingAverageSmootherFactory.Create(maType, resolved3);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public DoubleSmoothedRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        _maxWindow = new RollingWindowMax(resolved1);
        _minWindow = new RollingWindowMin(resolved1);
        _topSmoother1 = MovingAverageSmootherFactory.Create(maType, resolved2);
        _topSmoother2 = MovingAverageSmootherFactory.Create(maType, resolved3);
        _botSmoother1 = MovingAverageSmootherFactory.Create(maType, resolved2);
        _botSmoother2 = MovingAverageSmootherFactory.Create(maType, resolved3);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleSmoothedRelativeStrengthIndex;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _topSmoother1.Reset();
        _topSmoother2.Reset();
        _botSmoother1.Reset();
        _botSmoother2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);

        var srcLc = value - lowest;
        var hcSrc = highest - value;
        var top1 = _topSmoother1.Next(srcLc, isFinal);
        var top2 = _topSmoother2.Next(top1, isFinal);
        var bot1 = _botSmoother1.Next(hcSrc, isFinal);
        var bot2 = _botSmoother2.Next(bot1, isFinal);
        var rs = bot2 != 0 ? MathHelper.MinOrMax(top2 / bot2, 1, 0) : 0;
        var rsi = bot2 == 0 ? 100 : top2 == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        var signal = _signalSmoother.Next(rsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dsrsi", rsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _topSmoother1.Dispose();
        _topSmoother2.Dispose();
        _botSmoother1.Dispose();
        _botSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class DoubleSmoothedStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _ssNum;
    private readonly IMovingAverageSmoother _ssDenom;
    private readonly IMovingAverageSmoother _dsNum;
    private readonly IMovingAverageSmoother _dsDenom;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public DoubleSmoothedStochasticState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 3, int length3 = 15, int length4 = 3, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _ssNum = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ssDenom = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _dsNum = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _dsDenom = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _input = new StreamingInputResolver(inputName, null);
    }

    public DoubleSmoothedStochasticState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _ssNum = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ssDenom = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _dsNum = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _dsDenom = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleSmoothedStochastic;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _ssNum.Reset();
        _ssDenom.Reset();
        _dsNum.Reset();
        _dsDenom.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var num = value - lowestLow;
        var denom = highestHigh - lowestLow;

        var ssNum = _ssNum.Next(num, isFinal);
        var ssDenom = _ssDenom.Next(denom, isFinal);
        var dsNum = _dsNum.Next(ssNum, isFinal);
        var dsDenom = _dsDenom.Next(ssDenom, isFinal);
        var dss = dsDenom != 0 ? MathHelper.MinOrMax(100 * dsNum / dsDenom, 100, 0) : 0;
        var signal = _signal.Next(dss, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dss", dss },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(dss, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _ssNum.Dispose();
        _ssDenom.Dispose();
        _dsNum.Dispose();
        _dsDenom.Dispose();
        _signal.Dispose();
    }
}

public sealed class DoubleStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _slowK;
    private readonly IMovingAverageSmoother _signal;
    private readonly StochasticOscillatorState _stochastic;

    public DoubleStochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _slowK = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _stochastic = new StochasticOscillatorState(maType, resolved, 3, 3, inputName);
    }

    public DoubleStochasticOscillatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _slowK = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _stochastic = new StochasticOscillatorState(maType, resolved, 3, 3, selector);
    }

    public IndicatorName Name => IndicatorName.DoubleStochasticOscillator;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _slowK.Reset();
        _signal.Reset();
        _stochastic.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var fastK = _stochastic.Update(bar, isFinal, includeOutputs: false).Value;
        var highest = isFinal ? _maxWindow.Add(fastK, out _) : _maxWindow.Preview(fastK, out _);
        var lowest = isFinal ? _minWindow.Add(fastK, out _) : _minWindow.Preview(fastK, out _);
        var range = highest - lowest;
        var doubleK = range != 0 ? MathHelper.MinOrMax((fastK - lowest) / range * 100, 100, 0) : 0;
        var doubleSlowK = _slowK.Next(doubleK, isFinal);
        var doubleSignal = _signal.Next(doubleSlowK, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dso", doubleSlowK },
                { "Signal", doubleSignal }
            };
        }

        return new StreamingIndicatorStateResult(doubleSlowK, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _slowK.Dispose();
        _signal.Dispose();
        _stochastic.Dispose();
    }
}

public sealed class EaseOfMovementState : IStreamingIndicatorState
{
    private readonly double _divisor;
    private double _prevHalfRange;
    private double _prevMidpointMove;
    private bool _hasPrev;

    public EaseOfMovementState(double divisor = 1000000)
    {
        _divisor = divisor;
    }

    public IndicatorName Name => IndicatorName.EaseOfMovement;

    public void Reset()
    {
        _prevHalfRange = 0;
        _prevMidpointMove = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var halfRange = (bar.High - bar.Low) * 0.5;
        var prevHalfRange = _hasPrev ? _prevHalfRange : 0;
        var midpointMove = halfRange - prevHalfRange;
        var prevMidpointMove = _hasPrev ? _prevMidpointMove : 0;
        var boxRatio = bar.High - bar.Low != 0 ? bar.Volume / (bar.High - bar.Low) : 0;
        var emv = boxRatio != 0 ? _divisor * ((midpointMove - prevMidpointMove) / boxRatio) : 0;

        if (isFinal)
        {
            _prevHalfRange = halfRange;
            _prevMidpointMove = midpointMove;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eom", emv }
            };
        }

        return new StreamingIndicatorStateResult(emv, outputs);
    }
}

internal static class StreamingWindowHelper
{
    public static double GetRecentValue(PooledRingBuffer<double> window, int offset, double current)
    {
        if (offset <= 0)
        {
            return current;
        }

        var index = window.Count - offset;
        return index >= 0 ? window[index] : 0;
    }
}
