using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class SmoothedDeltaRatioOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _absSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smaValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public SmoothedDeltaRatioOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _absSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
        _smaValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SmoothedDeltaRatioOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _absSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
        _smaValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SmoothedDeltaRatioOscillator;

    public void Reset()
    {
        _sma.Reset();
        _absSmoother.Reset();
        _values.Clear();
        _smaValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
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
        _sma.Dispose();
        _absSmoother.Dispose();
        _values.Dispose();
        _smaValues.Dispose();
    }
}

public sealed class SmoothedRateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _maValues;
    private readonly StreamingInputResolver _input;
    private int _index;

    public SmoothedRateOfChangeState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21,
        int smoothingLength = 13, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _maValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SmoothedRateOfChangeState(MovingAvgType maType, int length, int smoothingLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _maValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SmoothedRateOfChange;

    public void Reset()
    {
        _smoother.Reset();
        _maValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _smoother.Next(value, isFinal);
        var prevMa = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_maValues, _length) : 0;
        var mom = ma - prevMa;
        var sroc = prevMa != 0 ? 100 * mom / prevMa : 100;

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
        _smoother.Dispose();
        _maValues.Dispose();
    }
}

public sealed class SmoothedWilliamsAccumulationDistributionState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private double _prevHigh;
    private double _prevLow;
    private double _prevWad;
    private bool _hasPrev;

    public SmoothedWilliamsAccumulationDistributionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public SmoothedWilliamsAccumulationDistributionState(MovingAvgType maType, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SmoothedWilliamsAccumulationDistribution;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevClose = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevWad = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevWad = _hasPrev ? _prevWad : 0;
        var wad = close > prevClose ? prevWad + close - prevLow :
            close < prevClose ? prevWad + close - prevHigh : 0;
        var signal = _signalSmoother.Next(wad, isFinal);

        if (isFinal)
        {
            _prevClose = close;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevWad = wad;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Swad", wad },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(wad, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class SortinoRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly IMovingAverageSmoother _retSmoother;
    private readonly IMovingAverageSmoother _devSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SortinoRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = 0.02,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var barMin = 60d * 24;
        var minPerYr = 60d * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = MathHelper.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _devSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SortinoRatioState(MovingAvgType maType, int length, double bmk, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var barMin = 60d * 24;
        var minPerYr = 60d * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = MathHelper.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _devSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SortinoRatio;

    public void Reset()
    {
        _retSmoother.Reset();
        _devSmoother.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, _length);
        var ret = prevValue != 0 ? (value / prevValue) - 1 - _bench : 0;
        var retSma = _retSmoother.Next(ret, isFinal);
        var deviation = Math.Min(ret - retSma, 0);
        var deviationSquared = deviation * deviation;
        var divisionOfSum = _devSmoother.Next(deviationSquared, isFinal);
        var stdDeviation = MathHelper.Sqrt(divisionOfSum);
        var sortino = stdDeviation != 0 ? retSma / stdDeviation : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sr", sortino }
            };
        }

        return new StreamingIndicatorStateResult(sortino, outputs);
    }

    public void Dispose()
    {
        _retSmoother.Dispose();
        _devSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class SpearmanIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SpearmanIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10,
        int signalLength = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SpearmanIndicatorState(MovingAvgType maType, int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class Spencer15PointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private static readonly double[] Weights =
    {
        -3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3
    };

    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public Spencer15PointMovingAverageState(InputName inputName = InputName.Close)
    {
        _weightSum = SumWeights();
        _values = new PooledRingBuffer<double>(Weights.Length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public Spencer15PointMovingAverageState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _weightSum = SumWeights();
        _values = new PooledRingBuffer<double>(Weights.Length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Spencer15PointMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        for (var j = 0; j < Weights.Length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * Weights[j];
        }

        var spma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "S15ma", spma }
            };
        }

        return new StreamingIndicatorStateResult(spma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }

    private static double SumWeights()
    {
        double sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += Weights[i];
        }

        return sum;
    }
}

public sealed class Spencer21PointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private static readonly double[] Weights =
    {
        -1, -3, -5, -5, -2, 6, 18, 33, 47, 57, 60, 57, 47, 33, 18, 6, -2, -5, -5, -3, -1
    };

    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public Spencer21PointMovingAverageState(InputName inputName = InputName.Close)
    {
        _weightSum = SumWeights();
        _values = new PooledRingBuffer<double>(Weights.Length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public Spencer21PointMovingAverageState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _weightSum = SumWeights();
        _values = new PooledRingBuffer<double>(Weights.Length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Spencer21PointMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        for (var j = 0; j < Weights.Length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * Weights[j];
        }

        var spma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "S21ma", spma }
            };
        }

        return new StreamingIndicatorStateResult(spma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }

    private static double SumWeights()
    {
        double sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += Weights[i];
        }

        return sum;
    }
}

public sealed class SquareRootWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SquareRootWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, 0.5);
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SquareRootWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
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
            var weight = MathHelper.Pow(_length - j, 0.5);
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SquareRootWeightedMovingAverage;

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

        var srwma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Srwma", srwma }
            };
        }

        return new StreamingIndicatorStateResult(srwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class SqueezeMomentumIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _sma;
    private readonly LinearRegressionState _linReg;
    private readonly StreamingInputResolver _input;
    private double _diffValue;

    public SqueezeMomentumIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _linReg = new LinearRegressionState(_length, _ => _diffValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SqueezeMomentumIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _linReg = new LinearRegressionState(_length, _ => _diffValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SqueezeMomentumIndicator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _sma.Reset();
        _linReg.Reset();
        _diffValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var midprice = (highest + lowest) / 2;
        var sma = _sma.Next(value, isFinal);
        var midpriceSmaAvg = (midprice + sma) / 2;
        _diffValue = value - midpriceSmaAvg;
        var linreg = _linReg.Update(bar, isFinal, includeOutputs: false).Value;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Smi", linreg }
            };
        }

        return new StreamingIndicatorStateResult(linreg, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _sma.Dispose();
        _linReg.Dispose();
    }
}

public sealed class StandardPivotPointsState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private double _prevOpen;
    private bool _hasPrev;

    public StandardPivotPointsState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public StandardPivotPointsState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StandardPivotPoints;

    public void Reset()
    {
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevOpen = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevOpen = _hasPrev ? _prevOpen : 0;
        var range = prevHigh - prevLow;
        var pivot = (prevHigh + prevLow + prevClose + prevOpen) / 4;
        var support1 = (pivot * 2) - prevHigh;
        var resistance1 = (pivot * 2) - prevLow;
        var range2 = resistance1 - support1;
        var support2 = pivot - range;
        var resistance2 = pivot + range;
        var support3 = pivot - range2;
        var resistance3 = pivot + range2;
        var midpoint1 = (support3 + support2) / 2;
        var midpoint2 = (support2 + support1) / 2;
        var midpoint3 = (support1 + pivot) / 2;
        var midpoint4 = (resistance1 + pivot) / 2;
        var midpoint5 = (resistance2 + resistance1) / 2;
        var midpoint6 = (resistance3 + resistance2) / 2;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = close;
            _prevOpen = bar.Open;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(13)
            {
                { "Pivot", pivot },
                { "S1", support1 },
                { "S2", support2 },
                { "S3", support3 },
                { "R1", resistance1 },
                { "R2", resistance2 },
                { "R3", resistance3 },
                { "M1", midpoint1 },
                { "M2", midpoint2 },
                { "M3", midpoint3 },
                { "M4", midpoint4 },
                { "M5", midpoint5 },
                { "M6", midpoint6 }
            };
        }

        return new StreamingIndicatorStateResult(pivot, outputs);
    }
}

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

    public StationaryExtrapolatedLevelsOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 200, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stochLength = Math.Max(1, _length * 2);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _maxWindow = new RollingWindowMax(_stochLength);
        _minWindow = new RollingWindowMin(_stochLength);
        _yValues = new PooledRingBuffer<double>(_stochLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public StationaryExtrapolatedLevelsOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stochLength = Math.Max(1, _length * 2);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _maxWindow = new RollingWindowMax(_stochLength);
        _minWindow = new RollingWindowMin(_stochLength);
        _yValues = new PooledRingBuffer<double>(_stochLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StationaryExtrapolatedLevelsOscillator;

    public void Reset()
    {
        _sma.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _yValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var y = value - sma;
        var prevY = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_yValues, _length) : 0;
        var prevY2 = _index >= _stochLength ? EhlersStreamingWindow.GetOffsetValue(_yValues, _stochLength) : 0;
        var ext = ((2 * prevY) - prevY2) / 2;
        var highest = isFinal ? _maxWindow.Add(ext, out _) : _maxWindow.Preview(ext, out _);
        var lowest = isFinal ? _minWindow.Add(ext, out _) : _minWindow.Preview(ext, out _);
        var range = highest - lowest;
        var osc = range != 0 ? MathHelper.MinOrMax((ext - lowest) / range * 100, 100, 0) : 0;

        if (isFinal)
        {
            _yValues.TryAdd(y, out _);
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

public sealed class StiffnessIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly RollingWindowSum _aboveSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public StiffnessIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 100, int length2 = 60, int smoothingLength = 3, double threshold = 90,
        InputName inputName = InputName.Close)
    {
        _ = threshold;
        _length2 = Math.Max(1, length2);
        var resolved = Math.Max(1, length1);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _aboveSum = new RollingWindowSum(_length2);
        _signal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, smoothingLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StiffnessIndicatorState(MovingAvgType maType, int length1, int length2, int smoothingLength, double threshold,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = threshold;
        _length2 = Math.Max(1, length2);
        var resolved = Math.Max(1, length1);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _aboveSum = new RollingWindowSum(_length2);
        _signal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, smoothingLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StiffnessIndicator;

    public void Reset()
    {
        _sma.Reset();
        _stdDev.Reset();
        _aboveSum.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var bound = sma - (0.2 * stdDev);
        var above = value > bound ? 1 : 0;
        var aboveSum = isFinal ? _aboveSum.Add(above, out _) : _aboveSum.Preview(above, out _);
        var stiffValue = _length2 != 0 ? aboveSum * 100 / _length2 : 0;
        var stiffness = _signal.Next(stiffValue, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Si", stiffness }
            };
        }

        return new StreamingIndicatorStateResult(stiffness, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _stdDev.Dispose();
        _aboveSum.Dispose();
        _signal.Dispose();
    }
}

public sealed class StochasticCustomOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _numSmoother;
    private readonly IMovingAverageSmoother _denomSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticCustomOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 7,
        int length2 = 3, int length3 = 12, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _numSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _denomSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticCustomOscillatorState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved1);
        _lowWindow = new RollingWindowMin(resolved1);
        _numSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _denomSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticCustomOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _numSmoother.Reset();
        _denomSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var num = value - lowest;
        var denom = highest - lowest;
        var numSma = _numSmoother.Next(num, isFinal);
        var denomSma = _denomSmoother.Next(denom, isFinal);
        var sck = denomSma != 0 ? MathHelper.MinOrMax(numSma / denomSma * 100, 100, 0) : 0;
        var scd = _signalSmoother.Next(sck, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sco", sck },
                { "Signal", scd }
            };
        }

        return new StreamingIndicatorStateResult(sck, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _numSmoother.Dispose();
        _denomSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class StochasticFastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticFastOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 2, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticFastOscillatorState(MovingAvgType maType, int length, int smoothLength1, int smoothLength2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((value - lowest) / range * 100, 100, 0) : 0;
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

public sealed class StochasticMovingAverageConvergenceDivergenceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public StochasticMovingAverageConvergenceDivergenceOscillatorState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 45, int fastLength = 12,
        int slowLength = 26, int signalLength = 9, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticMovingAverageConvergenceDivergenceOscillatorState(
        MovingAvgType maType, int length, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var fastStochastic = range != 0 ? (fast - lowest) / range : 0;
        var slowStochastic = range != 0 ? (slow - lowest) / range : 0;
        var macd = 10 * (fastStochastic - slowStochastic);
        var signal = _signal.Next(macd, isFinal);
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

public sealed class StochasticRegularState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticRegularState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 5,
        int length2 = 3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticRegularState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((value - lowest) / range * 100, 100, 0) : 0;
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

public sealed class StrengthOfMovementState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _bSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public StrengthOfMovementState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 10,
        int length2 = 3, int smoothingLength = 3, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _maxWindow = new RollingWindowMax(_length1);
        _minWindow = new RollingWindowMin(_length1);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _values = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public StrengthOfMovementState(MovingAvgType maType, int length1, int length2, int smoothingLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _maxWindow = new RollingWindowMax(_length1);
        _minWindow = new RollingWindowMin(_length1);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _values = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StrengthOfMovement;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _bSmoother.Reset();
        _signalSmoother.Reset();
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _index >= _length2 - 1
            ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length2 - 1)
            : 0;
        var moveSe = _index >= _length2 - 1 ? value - prevValue : 0;
        var avgMoveSe = _length2 > 1 ? moveSe / (_length2 - 1) : 0;
        var aaSe = prevValue != 0 ? avgMoveSe / prevValue : 0;
        var b = _bSmoother.Next(aaSe, isFinal);
        var highest = isFinal ? _maxWindow.Add(b, out _) : _maxWindow.Preview(b, out _);
        var lowest = isFinal ? _minWindow.Add(b, out _) : _minWindow.Preview(b, out _);
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((b - lowest) / range * 100, 100, 0) : 0;
        var sSe = (fastK * 2) - 100;
        var som = _signalSmoother.Next(sSe, isFinal);

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
                { "Som", som }
            };
        }

        return new StreamingIndicatorStateResult(som, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _bSmoother.Dispose();
        _signalSmoother.Dispose();
        _values.Dispose();
    }
}

public sealed class SuperTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly AverageTrueRangeSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _atrMult;
    private double _prevValue;
    private double _prevLongStop;
    private double _prevShortStop;
    private int _prevDir;
    private bool _hasPrev;

    public SuperTrendState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22,
        double atrMult = 3, InputName inputName = InputName.Close)
    {
        _atrSmoother = new AverageTrueRangeSmoother(maType, Math.Max(1, length), inputName);
        _input = new StreamingInputResolver(inputName, null);
        _atrMult = atrMult;
    }

    public SuperTrendState(MovingAvgType maType, int length, double atrMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _atrSmoother = new AverageTrueRangeSmoother(maType, Math.Max(1, length), selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _atrMult = atrMult;
    }

    public IndicatorName Name => IndicatorName.SuperTrend;

    public void Reset()
    {
        _atrSmoother.Reset();
        _prevValue = 0;
        _prevLongStop = 0;
        _prevShortStop = 0;
        _prevDir = 1;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var atr = _atrSmoother.Next(bar, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;

        var atrValue = _atrMult * atr;
        var tempLongStop = value - atrValue;
        var tempShortStop = value + atrValue;

        var prevLongStop = _hasPrev ? _prevLongStop : tempLongStop;
        var longStop = prevValue > prevLongStop ? Math.Max(tempLongStop, prevLongStop) : tempLongStop;

        var prevShortStop = _hasPrev ? _prevShortStop : tempShortStop;
        var shortStop = prevValue < prevShortStop ? Math.Min(tempShortStop, prevShortStop) : tempShortStop;

        var prevDir = _hasPrev ? _prevDir : 1;
        var dir = prevDir == -1 && value > prevShortStop ? 1 : prevDir == 1 && value < prevLongStop ? -1 : prevDir;
        var trend = dir > 0 ? longStop : shortStop;

        if (isFinal)
        {
            _prevValue = value;
            _prevLongStop = longStop;
            _prevShortStop = shortStop;
            _prevDir = dir;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Trend", trend }
            };
        }

        return new StreamingIndicatorStateResult(trend, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
    }
}

public sealed class SuperTrendFilterState : IStreamingIndicatorState
{
    private readonly double _a;
    private readonly double _factor;
    private readonly StreamingInputResolver _input;
    private double _prevTsl;
    private double _prevT;
    private double _prevSrc;
    private double _prevTrendUp;
    private double _prevTrendDn;
    private double _prevTrend;
    private bool _hasPrev;

    public SuperTrendFilterState(int length = 200, double factor = 0.9, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var p = MathHelper.Pow(resolved, 2);
        _a = 2 / (p + 1);
        _factor = factor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public SuperTrendFilterState(int length, double factor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var p = MathHelper.Pow(resolved, 2);
        _a = 2 / (p + 1);
        _factor = factor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SuperTrendFilter;

    public void Reset()
    {
        _prevTsl = 0;
        _prevT = 0;
        _prevSrc = 0;
        _prevTrendUp = 0;
        _prevTrendDn = 0;
        _prevTrend = 1;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevTsl1 = _hasPrev ? _prevTsl : value;
        var d = Math.Abs(value - prevTsl1);

        var prevT = _hasPrev ? _prevT : d;
        var t = (_a * d) + ((1 - _a) * prevT);

        var prevSrc = _hasPrev ? _prevSrc : 0;
        var src = (_factor * prevTsl1) + ((1 - _factor) * value);

        var up = prevTsl1 - t;
        var dn = prevTsl1 + t;

        var prevTrendUp = _hasPrev ? _prevTrendUp : 0;
        var trendUp = prevSrc > prevTrendUp ? Math.Max(up, prevTrendUp) : up;

        var prevTrendDn = _hasPrev ? _prevTrendDn : 0;
        var trendDn = prevSrc < prevTrendDn ? Math.Min(dn, prevTrendDn) : dn;

        var prevTrend = _hasPrev ? _prevTrend : 1;
        var trend = src > prevTrendDn ? 1 : src < prevTrendUp ? -1 : prevTrend;
        var tsl = trend == 1 ? trendDn : trendUp;

        if (isFinal)
        {
            _prevTsl = tsl;
            _prevT = t;
            _prevSrc = src;
            _prevTrendUp = trendUp;
            _prevTrendDn = trendDn;
            _prevTrend = trend;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Stf", tsl }
            };
        }

        return new StreamingIndicatorStateResult(tsl, outputs);
    }
}

public sealed class SupportAndResistanceOscillatorState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public SupportAndResistanceOscillatorState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public SupportAndResistanceOscillatorState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SupportAndResistanceOscillator;

    public void Reset()
    {
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var sro = tr != 0
            ? MathHelper.MinOrMax((bar.High - bar.Open + (value - bar.Low)) / (2 * tr), 1, 0)
            : 0;

        if (isFinal)
        {
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sro", sro }
            };
        }

        return new StreamingIndicatorStateResult(sro, outputs);
    }
}

public sealed class SurfaceRoughnessEstimatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public SurfaceRoughnessEstimatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        _ = maType;
        var resolved = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SurfaceRoughnessEstimatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = maType;
        var resolved = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SurfaceRoughnessEstimator;

    public void Reset()
    {
        _corrWindow.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var corr = isFinal ? _corrWindow.Add(prevValue, value, out _) : _corrWindow.Preview(prevValue, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;
        var sre = 1 - ((corr + 1) / 2);

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
                { "Sre", sre }
            };
        }

        return new StreamingIndicatorStateResult(sre, outputs);
    }

    public void Dispose()
    {
        _corrWindow.Dispose();
    }
}

public sealed class SvamaState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevH;
    private double _prevL;
    private double _prevCMax;
    private double _prevCMin;
    private bool _hasPrev;

    public SvamaState(int length = 14, InputName inputName = InputName.Close)
    {
        _ = length;
        _input = new StreamingInputResolver(inputName, null);
    }

    public SvamaState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ = length;
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class SwamiStochasticsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevNum;
    private double _prevDenom;
    private double _prevStoch;
    private bool _hasPrev;

    public SwamiStochasticsState(int fastLength = 12, int slowLength = 48, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, slowLength - fastLength);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SwamiStochasticsState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, slowLength - fastLength);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class SymmetricallyWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SymmetricallyWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        var floorLength = (int)Math.Floor((double)_length / 2);
        var roundLength = (int)Math.Round((double)_length / 2);
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            double weight;
            if (floorLength == roundLength)
            {
                weight = j < floorLength ? (j + 1) * _length : (_length - j) * _length;
            }
            else
            {
                weight = j <= floorLength ? (j + 1) * _length : (_length - j) * _length;
            }

            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SymmetricallyWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _weights = new double[_length];
        var floorLength = (int)Math.Floor((double)_length / 2);
        var roundLength = (int)Math.Round((double)_length / 2);
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            double weight;
            if (floorLength == roundLength)
            {
                weight = j < floorLength ? (j + 1) * _length : (_length - j) * _length;
            }
            else
            {
                weight = j <= floorLength ? (j + 1) * _length : (_length - j) * _length;
            }

            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SymmetricallyWeightedMovingAverage;

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

        var swma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

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
        _values.Dispose();
    }
}

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
        int length5 = 12, int length6 = 26, int length7 = 9, int length8 = 3, int length9 = 14,
        InputName inputName = InputName.Close)
    {
        _length8 = Math.Max(1, length8);
        _ma1 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length1));
        _ma2 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length3));
        _rocLong = new RateOfChangeState(Math.Max(1, length2), inputName);
        _rocShort = new RateOfChangeState(Math.Max(1, length4), inputName);
        _rsi = new RelativeStrengthIndexState(Math.Max(1, length9), 3, inputName);
        _ppoFast = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length5));
        _ppoSlow = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length6));
        _ppoSignal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length7));
        _histValues = new PooledRingBuffer<double>(_length8);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TechnicalRankState(int length1, int length2, int length3, int length4, int length5, int length6,
        int length7, int length8, int length9, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length8 = Math.Max(1, length8);
        _ma1 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length1));
        _ma2 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length3));
        _rocLong = new RateOfChangeState(Math.Max(1, length2), selector);
        _rocShort = new RateOfChangeState(Math.Max(1, length4), selector);
        _rsi = new RelativeStrengthIndexState(Math.Max(1, length9), 3, selector);
        _ppoFast = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length5));
        _ppoSlow = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length6));
        _ppoSignal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length7));
        _histValues = new PooledRingBuffer<double>(_length8);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var ltRoc = 0.3 * 100 * rocLong;
        var mtMa = ma2 != 0 ? 0.15 * 100 * (value - ma2) / ma2 : 0;
        var mtRoc = 0.15 * 100 * rocShort;
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
