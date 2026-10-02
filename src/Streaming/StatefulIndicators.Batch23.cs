#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Sdro")]
public sealed class SmoothedDeltaRatioOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly SmoothedDeltaWindow? _wide;
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _absSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smaValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public SmoothedDeltaRatioOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        if (StrengthWindow.Supports(maType)) _wide = new SmoothedDeltaWindow(maType, length);
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _absSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
        _smaValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SmoothedDeltaRatioOscillator;

    public void Reset()
    {
        _wide?.Reset();
        _sma.Reset();
        _absSmoother.Reset();
        _values.Clear();
        _smaValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (_wide is not null)
        {
            var next = _wide.Next(value, isFinal);
            return new StreamingIndicatorStateResult(next, includeOutputs ? new Dictionary<string, double> { { "Sdro", next } } : null);
        }
        var sma = _sma.Next(value, isFinal);
        var prevValue = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var prevSma = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_smaValues, _length) : 0;
        var absChg = _index >= _length ? Math.Abs(value - prevValue) : 0;
        var b = _index >= _length ? sma - prevSma : 0;
        var a = _absSmoother.Next(absChg, isFinal);
        var c = a != 0 ? MathHelper.MinOrMax(b / a, 1, 0) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smaValues.TryAdd(sma, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sdro", c }
            };
        }

        return new StreamingIndicatorStateResult(c, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _sma.Dispose();
        _absSmoother.Dispose();
        _values.Dispose();
        _smaValues.Dispose();
    }
}

[PrimaryOutput("Sroc")]
public sealed class SmoothedRateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly SmoothedReturnWindow? _wide;
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _maValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public SmoothedRateOfChangeState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21,
        int smoothingLength = 13)
    {
        if (StrengthWindow.Supports(maType)) _wide = new SmoothedReturnWindow(maType, length, smoothingLength);
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _maValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SmoothedRateOfChange;

    public void Reset()
    {
        _wide?.Reset();
        _smoother.Reset();
        _maValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null)
        {
            var next = _wide.Next(bar.Close, isFinal);
            return new StreamingIndicatorStateResult(next, includeOutputs
                ? new Dictionary<string, double> { { "Sroc", next } } : null);
        }
        var value = _input.GetValue(bar);
        var ma = _smoother.Next(value, isFinal);
        var prevMa = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_maValues, _length) : 0;
        var mom = ma - prevMa;
        var sroc = prevMa != 0 ? RoundedPercentageChange.Of(ma, prevMa) : 100;

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
                { "Sroc", sroc }
            };
        }

        return new StreamingIndicatorStateResult(sroc, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _smoother.Dispose();
        _maValues.Dispose();
    }
}

[PrimaryOutput("Swad")]
public sealed class SmoothedWilliamsAccumulationDistributionState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly WilliamsAccumulationWindow _window = new();
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    public SmoothedWilliamsAccumulationDistributionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        if (StrengthWindow.Supports(maType)) _average = new RocBankAverage(maType, length, int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }
    public IndicatorName Name => IndicatorName.SmoothedWilliamsAccumulationDistribution;
    public void Reset() { _window.Reset(); _average?.Reset(); _fallback?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var wide = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        var value = wide.Publish();
        var signal = _average is null ? _fallback!.Next(value, isFinal) : _average.Next(wide, isFinal).Publish();
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Swad", value }, { "Signal", signal } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() { _average?.Dispose(); _fallback?.Dispose(); }
}

[PrimaryOutput("Sr")]
public sealed class SortinoRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly SortinoWindow _window;
    public SortinoRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = .02)
        => _window = new SortinoWindow(maType, length, bmk);
    public IndicatorName Name => IndicatorName.SortinoRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Sr", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Si")]
public sealed class SpearmanIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SpearmanIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        int signalLength = 3)
    {
        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SpearmanIndicator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var window = BuildWindowValues(value);
        var sc = CalculateSpearman(window);
        sc = MathHelper.IsValueNullOrInfinity(sc) ? 0 : sc;
        var coef = sc * 100;
        var signal = _signalSmoother.Next(coef, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Si", coef },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(coef, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
        _values.Dispose();
    }

    private double[] BuildWindowValues(double value)
    {
        var count = _values.Count;
        var useCount = Math.Min(_length, count + 1);
        var window = new double[useCount];
        var existingCount = useCount - 1;
        var start = Math.Max(0, count - existingCount);
        for (var i = 0; i < existingCount; i++)
        {
            window[i] = _values[start + i];
        }

        window[useCount - 1] = value;
        return window;
    }

    private static double CalculateSpearman(IReadOnlyList<double> window)
    {
        var count = window.Count;
        if (count <= 1)
        {
            return 0;
        }

        var sorted = new double[count];
        for (var i = 0; i < count; i++)
        {
            sorted[i] = window[i];
        }

        Array.Sort(sorted);

        var rankByValue = new Dictionary<double, double>(count);
        var rankY = new double[count];
        double sumY = 0;
        double sumY2 = 0;
        var rank = 1;
        for (var i = 0; i < count; )
        {
            var value = sorted[i];
            var j = i + 1;
            while (j < count && sorted[j] == value)
            {
                j++;
            }

            var span = j - i;
            var avgRank = (rank + (rank + span - 1)) / 2.0;
            rankByValue[value] = avgRank;
            sumY += avgRank * span;
            sumY2 += avgRank * avgRank * span;
            for (var k = i; k < j; k++)
            {
                rankY[k] = avgRank;
            }

            rank += span;
            i = j;
        }

        double sumXY = 0;
        for (var i = 0; i < count; i++)
        {
            var rankX = rankByValue[window[i]];
            sumXY += rankX * rankY[i];
        }

        var n = (double)count;
        var numerator = (n * sumXY) - (sumY * sumY);
        var denomLeft = (n * sumY2) - (sumY * sumY);
        var denomRight = (n * sumY2) - (sumY * sumY);
        var denom = Math.Sqrt(denomLeft * denomRight);
        return denom != 0 ? numerator / denom : 0;
    }
}

[PrimaryOutput("S15ma")]
public sealed class Spencer15PointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SpencerWindow _window = new(false);
    public IndicatorName Name => IndicatorName.Spencer15PointMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "S15ma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("S21ma")]
public sealed class Spencer21PointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SpencerWindow _window = new(true);
    public IndicatorName Name => IndicatorName.Spencer21PointMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "S21ma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Srwma")]
public sealed class SquareRootWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SquareRootWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public SquareRootWeightedMovingAverageState(int length = 14)
    {
        _mean = new SquareRootWindowMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SquareRootWeightedMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Srwma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

[PrimaryOutput("Smi")]
public sealed class SqueezeMomentumIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private readonly SqueezeMomentumWindow _window;
    public SqueezeMomentumIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.SqueezeMomentumIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var result = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(result.Value, includeOutputs ? new Dictionary<string, double> { { "Smi", result.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Pivot")]
public sealed class StandardPivotPointsState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyPivotLevels _daily = new(true);
    public StandardPivotPointsState() { }
    public IndicatorName Name => IndicatorName.StandardPivotPoints;
    public void Reset() => _daily.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var levels = _daily.Next(bar.StartTime, bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            var values = new Dictionary<string, double>(levels.Length);
            for (var i = 0; i < levels.Length; i++) values[_daily.Keys[i]] = levels[i];
            outputs = values;
        }
        return new StreamingIndicatorStateResult(levels[0], outputs);
    }
}

[PrimaryOutput("Selo")]
public sealed class StationaryExtrapolatedLevelsOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _stochLength;
    private readonly IMovingAverageSmoother _sma;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly PooledRingBuffer<double> _yValues;
    private readonly StreamingInputResolver _input;
    private int _index;
    private double _previousExtrapolation;

    public StationaryExtrapolatedLevelsOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 200)
    {
        _length = Math.Max(1, length);
        _stochLength = Math.Max(1, _length * 2);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _maxWindow = new RollingWindowMax(_stochLength);
        _minWindow = new RollingWindowMin(_stochLength);
        _yValues = new PooledRingBuffer<double>(_stochLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StationaryExtrapolatedLevelsOscillator;

    public void Reset()
    {
        _sma.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _yValues.Clear();
        _index = 0;
        _previousExtrapolation = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var y = value - sma;
        var prevY = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_yValues, _length) : 0;
        var prevY2 = _index >= _stochLength ? EhlersStreamingWindow.GetOffsetValue(_yValues, _stochLength) : 0;
        var ext = ((2 * prevY) - prevY2) / 2;
        // Apply the same per-bar range contract as a chained stochastic input.
        var withinBar = CalculationsHelper.IsWithinBarRange(ext, bar.Low, bar.High);
        var previous = _index == 0 ? ext : _previousExtrapolation;
        var high = withinBar ? bar.High : Math.Max(previous, ext);
        var low = withinBar ? bar.Low : Math.Min(previous, ext);
        var highest = isFinal ? _maxWindow.Add(high, out _) : _maxWindow.Preview(high, out _);
        var lowest = isFinal ? _minWindow.Add(low, out _) : _minWindow.Preview(low, out _);
        var range = highest - lowest;
        var osc = range != 0 ? MathHelper.MinOrMax((ext - lowest) / range * 100, 100, 0) : 0;

        if (isFinal)
        {
            _yValues.TryAdd(y, out _);
            _previousExtrapolation = ext;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Selo", osc }
            };
        }

        return new StreamingIndicatorStateResult(osc, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _yValues.Dispose();
    }
}

[PrimaryOutput("Si")]
public sealed class StiffnessIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StiffnessWindow _window;
    private readonly StreamingInputResolver _input;
    public StiffnessIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 100, int length2 = 60, int smoothingLength = 3, double threshold = 90)
    { _window = new(maType, length1, length2, smoothingLength); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.StiffnessIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Si", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sco")]
public sealed class StochasticCustomOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly StochasticCustomWindow _window;
    public StochasticCustomOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 7, int length2 = 3, int length3 = 12)
        => _window = new(maType, length1, length2, length3);
    public IndicatorName Name => IndicatorName.StochasticCustomOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Sco", value.Line }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sfo")]
public sealed class StochasticFastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticFastOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 2)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength1))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength2))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StochasticFastOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var fastK = ClampedRangePosition.Percent(value, lowest, highest);
        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sfo", fastD },
                { "Signal", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastD, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

[PrimaryOutput("Macd")]
public sealed class StochasticMovingAverageConvergenceDivergenceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public StochasticMovingAverageConvergenceDivergenceOscillatorState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 45, int fastLength = 12,
        int slowLength = 26, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var macd = RoundedStochasticMacd.Of(fast, slow, highest, lowest);
        var invalid = _signalInvalid || double.IsInfinity(macd) || double.IsNaN(macd);
        var signal = invalid ? double.NaN : _signal.Next(macd, isFinal);
        if (isFinal) _signalInvalid = invalid;
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
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Sco")]
public sealed class StochasticRegularState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticRegularState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5,
        int length2 = 3)
    {
        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length2))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StochasticRegular;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var fastK = ClampedRangePosition.Percent(value, lowest, highest);
        var fastD = _fastSmoother.Next(fastK, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sco", fastK },
                { "Signal", fastD }
            };
        }

        return new StreamingIndicatorStateResult(fastK, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
    }
}

[PrimaryOutput("Som")]
public sealed class StrengthOfMovementState : IStreamingIndicatorState, IDisposable
{
    private readonly MovementStrengthWindow _window;
    private readonly StreamingInputResolver _input;
    public StrengthOfMovementState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 10, int length2 = 3, int smoothingLength = 3)
    { _window = new(maType, length1, length2, smoothingLength); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.StrengthOfMovement;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Som", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Trend")]
public sealed class SuperTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly SuperTrendWindow _window;
    public SuperTrendState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22, double atrMult = 3) => _window = new(maType, length, atrMult);
    public IndicatorName Name => IndicatorName.SuperTrend;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Trend", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Stf")]
public sealed class SuperTrendFilterState : IStreamingIndicatorState
{
    private readonly SuperTrendFilterWindow _window;
    public SuperTrendFilterState(int length = 200, double factor = .9) => _window = new(length, factor);
    public IndicatorName Name => IndicatorName.SuperTrendFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Stf", point.Value } } : null);
    }
}

[PrimaryOutput("Sro")]
public sealed class SupportAndResistanceOscillatorState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    private readonly SupportResistanceOscillatorWindow _window = new();
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    public IndicatorName Name => IndicatorName.SupportAndResistanceOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.High, bar.Low, _input.GetValue(bar), isFinal).Line;
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Sro", value } } : null);
    }
}

[PrimaryOutput("Sre")]
public sealed class SurfaceRoughnessEstimatorState : IStreamingIndicatorState, IDisposable
{
    private readonly SurfaceRoughnessWindow _window;
    private readonly StreamingInputResolver _input;
    public SurfaceRoughnessEstimatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 100)
    { _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.SurfaceRoughnessEstimator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Line(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Sre", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Svama")]
public sealed class SvamaState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevH;
    private double _prevL;
    private double _prevCMax;
    private double _prevCMin;
    private bool _hasPrev;

    public SvamaState(int length = 14)
    {
        _ = length;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Svama;

    public void Reset()
    {
        _prevH = 0;
        _prevL = 0;
        _prevCMax = 0;
        _prevCMin = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var a = bar.Volume;

        var prevH = _hasPrev ? _prevH : a;
        var h = a > prevH ? a : prevH;

        var prevL = _hasPrev ? _prevL : a;
        var l = a < prevL ? a : prevL;

        var bMax = h != 0 ? a / h : 0;
        var bMin = a != 0 ? l / a : 0;

        var prevCMax = _hasPrev ? _prevCMax : value;
        var cMax = (bMax * value) + ((1 - bMax) * prevCMax);

        var prevCMin = _hasPrev ? _prevCMin : value;
        var cMin = (bMin * value) + ((1 - bMin) * prevCMin);

        if (isFinal)
        {
            _prevH = h;
            _prevL = l;
            _prevCMax = cMax;
            _prevCMin = cMin;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Svama", cMax }
            };
        }

        return new StreamingIndicatorStateResult(cMax, outputs);
    }
}

[PrimaryOutput("Ss")]
public sealed class SwamiStochasticsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevNum;
    private double _prevDenom;
    private double _prevStoch;
    private bool _hasPrev;

    public SwamiStochasticsState(int fastLength = 12, int slowLength = 48)
    {
        var resolved = Math.Max(1, slowLength - fastLength);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SwamiStochastics;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevNum = 0;
        _prevDenom = 0;
        _prevStoch = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        var prevNum = _hasPrev ? _prevNum : 0;
        var num = (value - lowest + prevNum) / 2;

        var prevDenom = _hasPrev ? _prevDenom : 0;
        var denom = (highest - lowest + prevDenom) / 2;

        var prevStoch = _hasPrev ? _prevStoch : 0;
        var stoch = denom != 0
            ? MathHelper.MinOrMax((0.2 * num / denom) + (0.8 * prevStoch), 1, 0)
            : 0;

        if (isFinal)
        {
            _prevNum = num;
            _prevDenom = denom;
            _prevStoch = stoch;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ss", stoch }
            };
        }

        return new StreamingIndicatorStateResult(stoch, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("Swma")]
public sealed class SymmetricallyWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SymmetricWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public SymmetricallyWeightedMovingAverageState(int length = 14)
    {
        _mean = new SymmetricWindowMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SymmetricallyWeightedMovingAverage;

    public void Reset()
    {
        _mean.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var swma = _mean.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Swma", swma }
            };
        }

        return new StreamingIndicatorStateResult(swma, outputs);
    }

    public void Dispose()
    {
        _mean.Dispose();
    }
}

[PrimaryOutput("Tr")]
public sealed class TechnicalRankState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length8;
    private readonly IMovingAverageSmoother _ma1;
    private readonly IMovingAverageSmoother _ma2;
    private readonly RateOfChangeState _rocLong;
    private readonly RateOfChangeState _rocShort;
    private readonly RelativeStrengthIndexState _rsi;
    private readonly IMovingAverageSmoother _ppoFast;
    private readonly IMovingAverageSmoother _ppoSlow;
    private readonly IMovingAverageSmoother _ppoSignal;
    private readonly PooledRingBuffer<double> _histValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public TechnicalRankState(int length1 = 200, int length2 = 125, int length3 = 50, int length4 = 20,
        int length5 = 12, int length6 = 26, int length7 = 9, int length8 = 3, int length9 = 14)
    {
        _length8 = Math.Max(1, length8);
        _ma1 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length1));
        _ma2 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length3));
        _rocLong = new RateOfChangeState(Math.Max(1, length2));
        _rocShort = new RateOfChangeState(Math.Max(1, length4));
        _rsi = new RelativeStrengthIndexState(Math.Max(1, length9), 3);
        _ppoFast = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length5));
        _ppoSlow = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length6));
        _ppoSignal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length7));
        _histValues = new PooledRingBuffer<double>(_length8);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TechnicalRank;

    public void Reset()
    {
        _ma1.Reset();
        _ma2.Reset();
        _rocLong.Reset();
        _rocShort.Reset();
        _rsi.Reset();
        _ppoFast.Reset();
        _ppoSlow.Reset();
        _ppoSignal.Reset();
        _histValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma1 = _ma1.Next(value, isFinal);
        var ma2 = _ma2.Next(value, isFinal);
        var rocLong = _rocLong.Update(bar, isFinal, includeOutputs: false).Value;
        var rocShort = _rocShort.Update(bar, isFinal, includeOutputs: false).Value;
        var rsi = _rsi.Update(bar, isFinal, includeOutputs: false).Value;

        var fast = _ppoFast.Next(value, isFinal);
        var slow = _ppoSlow.Next(value, isFinal);
        var ppo = slow != 0 ? 100 * (fast - slow) / slow : 0;
        var signal = _ppoSignal.Next(ppo, isFinal);
        var histogram = ppo - signal;

        var prevHistogram = _index >= _length8 ? EhlersStreamingWindow.GetOffsetValue(_histValues, histogram, _length8) : 0;
        var slope = _index >= _length8 ? (histogram - prevHistogram) / _length8 : 0;

        var ltMa = ma1 != 0 ? 0.3 * 100 * (value - ma1) / ma1 : 0;
        var ltRoc = 0.3 * rocLong;
        var mtMa = ma2 != 0 ? 0.15 * 100 * (value - ma2) / ma2 : 0;
        var mtRoc = 0.15 * rocShort;
        var stPpo = 0.05 * 100 * slope;
        var stRsi = 0.05 * rsi;

        var tr = Math.Min(100, Math.Max(0, ltMa + ltRoc + mtMa + mtRoc + stPpo + stRsi));

        if (isFinal)
        {
            _histValues.TryAdd(histogram, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tr", tr }
            };
        }

        return new StreamingIndicatorStateResult(tr, outputs);
    }

    public void Dispose()
    {
        _ma1.Dispose();
        _ma2.Dispose();
        _rocLong.Dispose();
        _rocShort.Dispose();
        _ppoFast.Dispose();
        _ppoSlow.Dispose();
        _ppoSignal.Dispose();
        _histValues.Dispose();
    }
}
