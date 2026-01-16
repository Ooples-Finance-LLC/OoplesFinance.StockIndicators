using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class FastandSlowRelativeStrengthIndexOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FastandSlowKurtosisOscillatorState _fsk;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _fskSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public FastandSlowRelativeStrengthIndexOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 6, InputName inputName = InputName.Close)
    {
        _fsk = new FastandSlowKurtosisOscillatorState(maType, Math.Max(1, length1), 0.03, inputName);
        _rsi = new RsiState(maType, Math.Max(1, length3));
        _fskSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _input = new StreamingInputResolver(inputName, null);
    }

    public FastandSlowRelativeStrengthIndexOscillatorState(MovingAvgType maType, int length1, int length2, int length3,
        int length4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fsk = new FastandSlowKurtosisOscillatorState(maType, Math.Max(1, length1), 0.03, selector);
        _rsi = new RsiState(maType, Math.Max(1, length3));
        _fskSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FastandSlowRelativeStrengthIndexOscillator;

    public void Reset()
    {
        _fsk.Reset();
        _rsi.Reset();
        _fskSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var fsk = _fsk.Update(bar, isFinal, includeOutputs: false).Value;
        var v4 = _fskSmoother.Next(fsk, isFinal);
        var fsrsi = (10000 * v4) + rsi;
        var signal = _signalSmoother.Next(fsrsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fsrsi", fsrsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fsrsi, outputs);
    }

    public void Dispose()
    {
        _fsk.Dispose();
        _rsi.Dispose();
        _fskSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class FastandSlowStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly FastandSlowKurtosisOscillatorState _fsk;
    private readonly IMovingAverageSmoother _fskSmoother;
    private readonly StochasticOscillatorState _stoch;
    private readonly IMovingAverageSmoother _slowKSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;

    public FastandSlowStochasticOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 9, InputName inputName = InputName.Close)
    {
        _fsk = new FastandSlowKurtosisOscillatorState(maType, Math.Max(1, length1), 0.03, inputName);
        _fskSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _stoch = new StochasticOscillatorState(maType, Math.Max(1, length3), 3, 3, inputName);
        _slowKSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
    }

    public FastandSlowStochasticOscillatorState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fsk = new FastandSlowKurtosisOscillatorState(maType, Math.Max(1, length1), 0.03, selector);
        _fskSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _stoch = new StochasticOscillatorState(maType, Math.Max(1, length3), 3, 3, selector);
        _slowKSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
    }

    public IndicatorName Name => IndicatorName.FastandSlowStochasticOscillator;

    public void Reset()
    {
        _fsk.Reset();
        _fskSmoother.Reset();
        _stoch.Reset();
        _slowKSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var fsk = _fsk.Update(bar, isFinal, includeOutputs: false).Value;
        var v4 = _fskSmoother.Next(fsk, isFinal);
        var fastK = _stoch.Update(bar, isFinal, includeOutputs: false).Value;
        var slowK = _slowKSmoother.Next(fastK, isFinal);
        var fsst = (500 * v4) + slowK;
        var signal = _signalSmoother.Next(fsst, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fsst", fsst },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fsst, outputs);
    }

    public void Dispose()
    {
        _fsk.Dispose();
        _fskSmoother.Dispose();
        _stoch.Dispose();
        _slowKSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class FastSlowDegreeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _fastF1bSum;
    private readonly RollingWindowSum _fastF2bSum;
    private readonly RollingWindowSum _fastVWSum;
    private readonly RollingWindowSum _slowF1bSum;
    private readonly RollingWindowSum _slowF2bSum;
    private readonly RollingWindowSum _slowVWSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;
    private int _index;

    public FastSlowDegreeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 100, int fastLength = 3, int slowLength = 2, int signalLength = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastF1bSum = new RollingWindowSum(resolvedFast);
        _fastF2bSum = new RollingWindowSum(resolvedFast);
        _fastVWSum = new RollingWindowSum(_length);
        _slowF1bSum = new RollingWindowSum(resolvedSlow);
        _slowF2bSum = new RollingWindowSum(resolvedSlow);
        _slowVWSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public FastSlowDegreeOscillatorState(MovingAvgType maType, int length, int fastLength, int slowLength,
        int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastF1bSum = new RollingWindowSum(resolvedFast);
        _fastF2bSum = new RollingWindowSum(resolvedFast);
        _fastVWSum = new RollingWindowSum(_length);
        _slowF1bSum = new RollingWindowSum(resolvedSlow);
        _slowF2bSum = new RollingWindowSum(resolvedSlow);
        _slowVWSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FastSlowDegreeOscillator;

    public void Reset()
    {
        _fastF1bSum.Reset();
        _fastF2bSum.Reset();
        _fastVWSum.Reset();
        _slowF1bSum.Reset();
        _slowF2bSum.Reset();
        _slowVWSum.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var length = _length;
        var index = _index;

        var fastF1x = (double)(index + 1) / length;
        var fastF1b = (double)1 / (index + 1) * Math.Sin(fastF1x * (index + 1) * Math.PI);
        var fastF1bSum = isFinal ? _fastF1bSum.Add(fastF1b, out _) : _fastF1bSum.Preview(fastF1b, out _);
        var fastF1pol = (fastF1x * fastF1x) + fastF1bSum;
        var fastF2x = length != 0 ? (double)index / length : 0;
        var fastF2b = (double)1 / (index + 1) * Math.Sin(fastF2x * (index + 1) * Math.PI);
        var fastF2bSum = isFinal ? _fastF2bSum.Add(fastF2b, out _) : _fastF2bSum.Preview(fastF2b, out _);
        var fastF2pol = (fastF2x * fastF2x) + fastF2bSum;
        var fastW = fastF1pol - fastF2pol;
        var fastVW = prevValue * fastW;
        var fastVWSum = isFinal ? _fastVWSum.Add(fastVW, out _) : _fastVWSum.Preview(fastVW, out _);

        var slowF1x = length != 0 ? (double)(index + 1) / length : 0;
        var slowF1b = (double)1 / (index + 1) * Math.Sin(slowF1x * (index + 1) * Math.PI);
        var slowF1bSum = isFinal ? _slowF1bSum.Add(slowF1b, out _) : _slowF1bSum.Preview(slowF1b, out _);
        var slowF1pol = (slowF1x * slowF1x) + slowF1bSum;
        var slowF2x = length != 0 ? (double)index / length : 0;
        var slowF2b = (double)1 / (index + 1) * Math.Sin(slowF2x * (index + 1) * Math.PI);
        var slowF2bSum = isFinal ? _slowF2bSum.Add(slowF2b, out _) : _slowF2bSum.Preview(slowF2b, out _);
        var slowF2pol = (slowF2x * slowF2x) + slowF2bSum;
        var slowW = slowF1pol - slowF2pol;
        var slowVW = prevValue * slowW;
        var slowVWSum = isFinal ? _slowVWSum.Add(slowVW, out _) : _slowVWSum.Preview(slowVW, out _);

        var os = fastVWSum - slowVWSum;
        var signal = _signalSmoother.Next(os, isFinal);
        var histogram = os - signal;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Fsdo", os },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(os, outputs);
    }

    public void Dispose()
    {
        _fastF1bSum.Dispose();
        _fastF2bSum.Dispose();
        _fastVWSum.Dispose();
        _slowF1bSum.Dispose();
        _slowF2bSum.Dispose();
        _slowVWSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class FearAndGreedIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastTrUp;
    private readonly IMovingAverageSmoother _fastTrDn;
    private readonly IMovingAverageSmoother _slowTrUp;
    private readonly IMovingAverageSmoother _slowTrDn;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public FearAndGreedIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int fastLength = 10, int slowLength = 30, int smoothLength = 2, InputName inputName = InputName.Close)
    {
        _fastTrUp = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _fastTrDn = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowTrUp = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _slowTrDn = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public FearAndGreedIndicatorState(MovingAvgType maType, int fastLength, int slowLength, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastTrUp = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _fastTrDn = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowTrUp = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _slowTrDn = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FearAndGreedIndicator;

    public void Reset()
    {
        _fastTrUp.Reset();
        _fastTrDn.Reset();
        _slowTrUp.Reset();
        _slowTrDn.Reset();
        _signal.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);

        var trUp = value > prevValue ? tr : 0;
        var trDn = value < prevValue ? tr : 0;

        var fastTrUp = _fastTrUp.Next(trUp, isFinal);
        var fastTrDn = _fastTrDn.Next(trDn, isFinal);
        var slowTrUp = _slowTrUp.Next(trUp, isFinal);
        var slowTrDn = _slowTrDn.Next(trDn, isFinal);
        var fgi = (fastTrUp - fastTrDn) - (slowTrUp - slowTrDn);
        var signal = _signal.Next(fgi, isFinal);

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
                { "Fgi", fgi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fgi, outputs);
    }

    public void Dispose()
    {
        _fastTrUp.Dispose();
        _fastTrDn.Dispose();
        _slowTrUp.Dispose();
        _slowTrDn.Dispose();
        _signal.Dispose();
    }
}

public sealed class FibonacciPivotPointsState : IStreamingIndicatorState
{
    private double _prevClose;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.FibonacciPivotPoints;

    public void Reset()
    {
        _prevClose = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var range = prevHigh - prevLow;
        var pivot = (prevHigh + prevLow + prevClose) / 3;

        var support1 = pivot - (range * 0.382);
        var support2 = pivot - (range * MathHelper.InversePhi);
        var support3 = pivot - (range * 1);
        var resistance1 = pivot + (range * 0.382);
        var resistance2 = pivot + (range * MathHelper.InversePhi);
        var resistance3 = pivot + (range * 1);
        var midpoint1 = (support3 + support2) / 2;
        var midpoint2 = (support2 + support1) / 2;
        var midpoint3 = (support1 + pivot) / 2;
        var midpoint4 = (resistance1 + pivot) / 2;
        var midpoint5 = (resistance2 + resistance1) / 2;
        var midpoint6 = (resistance3 + resistance2) / 2;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
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

public sealed class FibonacciRetraceState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly double _factor;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _wma;
    private readonly StreamingInputResolver _input;

    public FibonacciRetraceState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 15, int length2 = 50, double factor = 0.382, InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _factor = factor;
        _highWindow = new RollingWindowMax(_length2);
        _lowWindow = new RollingWindowMin(_length2);
        _wma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(inputName, null);
    }

    public FibonacciRetraceState(MovingAvgType maType, int length1, int length2, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _factor = factor;
        _highWindow = new RollingWindowMax(_length2);
        _lowWindow = new RollingWindowMin(_length2);
        _wma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FibonacciRetrace;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _wma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _ = _wma.Next(value, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var retrace = (highest - lowest) * _factor;
        var hret = highest - retrace;
        var lret = lowest + retrace;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpperBand", hret },
                { "LowerBand", lret }
            };
        }

        return new StreamingIndicatorStateResult(hret, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _wma.Dispose();
    }
}

public sealed class FibonacciWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public FibonacciWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var phi = (1 + Math.Sqrt(5)) / 2;
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var pow = Math.Pow(phi, resolved - j);
            var weight = (pow - (Math.Pow(-1, j) / pow)) / Math.Sqrt(5);
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FibonacciWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var phi = (1 + Math.Sqrt(5)) / 2;
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var pow = Math.Pow(phi, resolved - j);
            var weight = (pow - (Math.Pow(-1, j) / pow)) / Math.Sqrt(5);
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FibonacciWeightedMovingAverage;

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

        var fwma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fwma", fwma }
            };
        }

        return new StreamingIndicatorStateResult(fwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class FiniteVolumeElementsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _factor;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevTypicalPrice;
    private double _prevFve;
    private bool _hasPrev;

    public FiniteVolumeElementsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 22, double factor = 0.3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _factor = factor;
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FiniteVolumeElementsState(MovingAvgType maType, int length, double factor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _factor = factor;
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FiniteVolumeElements;

    public void Reset()
    {
        _volumeSmoother.Reset();
        _prevTypicalPrice = 0;
        _prevFve = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var medianPrice = (bar.High + bar.Low) / 2;
        var typicalPrice = (bar.High + bar.Low + value) / 3;
        var prevTypicalPrice = _hasPrev ? _prevTypicalPrice : 0;
        var avgVolume = _volumeSmoother.Next(bar.Volume, isFinal);

        var nmf = value - medianPrice + typicalPrice - prevTypicalPrice;
        var nvlm = nmf > _factor * value / 100 ? bar.Volume : nmf < -_factor * value / 100 ? -bar.Volume : 0;

        var prevFve = _hasPrev ? _prevFve : 0;
        var fve = avgVolume != 0 && _length != 0 ? prevFve + (nvlm / avgVolume / _length * 100) : prevFve;

        if (isFinal)
        {
            _prevTypicalPrice = typicalPrice;
            _prevFve = fve;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fve", fve }
            };
        }

        return new StreamingIndicatorStateResult(fve, outputs);
    }

    public void Dispose()
    {
        _volumeSmoother.Dispose();
    }
}

public sealed class FireflyOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _v3Smoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _v6Smoother;
    private readonly IMovingAverageSmoother _v7Smoother;
    private readonly IMovingAverageSmoother _wwSmoother;
    private readonly RollingWindowMax _maxWindow;
    private readonly StreamingInputResolver _input;
    private double _v2Value;

    public FireflyOscillatorState(MovingAvgType maType = MovingAvgType.ZeroLagExponentialMovingAverage,
        int length = 10, int smoothLength = 3, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _v3Smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _stdDev = new StandardDeviationVolatilityState(maType, resolvedLength, _ => _v2Value);
        _v6Smoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _v7Smoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _wwSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _maxWindow = new RollingWindowMax(resolvedSmooth);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FireflyOscillatorState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _v3Smoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _stdDev = new StandardDeviationVolatilityState(maType, resolvedLength, _ => _v2Value);
        _v6Smoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _v7Smoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _wwSmoother = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _maxWindow = new RollingWindowMax(resolvedSmooth);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FireflyOscillator;

    public void Reset()
    {
        _v3Smoother.Reset();
        _stdDev.Reset();
        _v6Smoother.Reset();
        _v7Smoother.Reset();
        _wwSmoother.Reset();
        _maxWindow.Reset();
        _v2Value = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var v2 = (bar.High + bar.Low + (value * 2)) / 4;
        _v2Value = v2;
        var v3 = _v3Smoother.Next(v2, isFinal);
        var v4 = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var v5 = v4 == 0 ? (v2 - v3) * 100 : (v2 - v3) * 100 / v4;
        var v6 = _v6Smoother.Next(v5, isFinal);
        var v7 = _v7Smoother.Next(v6, isFinal);
        var wwZlagEma = _wwSmoother.Next(v7, isFinal);
        var ww = ((wwZlagEma + 100) / 2) - 4;
        var mm = isFinal ? _maxWindow.Add(ww, out _) : _maxWindow.Preview(ww, out _);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fo", ww },
                { "Signal", mm }
            };
        }

        return new StreamingIndicatorStateResult(ww, outputs);
    }

    public void Dispose()
    {
        _v3Smoother.Dispose();
        _stdDev.Dispose();
        _v6Smoother.Dispose();
        _v7Smoother.Dispose();
        _wwSmoother.Dispose();
        _maxWindow.Dispose();
    }
}

public sealed class FisherLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _indexSma;
    private readonly StandardDeviationVolatilityState _stdDevSrc;
    private readonly StandardDeviationVolatilityState _indexStdDev;
    private readonly RollingWindowSum _diffSum;
    private readonly RollingWindowSum _absDiffSum;
    private readonly StreamingInputResolver _input;
    private double _indexValue;
    private double _prevB;
    private bool _hasPrev;
    private int _index;

    public FisherLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _indexSma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDevSrc = new StandardDeviationVolatilityState(maType, _length, inputName);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _diffSum = new RollingWindowSum(_length);
        _absDiffSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FisherLeastSquaresMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _indexSma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDevSrc = new StandardDeviationVolatilityState(maType, _length, selector);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _diffSum = new RollingWindowSum(_length);
        _absDiffSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FisherLeastSquaresMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _indexSma.Reset();
        _stdDevSrc.Reset();
        _indexStdDev.Reset();
        _diffSum.Reset();
        _absDiffSum.Reset();
        _indexValue = 0;
        _prevB = 0;
        _hasPrev = false;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevB = _hasPrev ? _prevB : value;
        var diff = value - prevB;
        var absDiff = Math.Abs(diff);

        var diffSum = isFinal ? _diffSum.Add(diff, out var diffCount) : _diffSum.Preview(diff, out diffCount);
        var absDiffSum = isFinal ? _absDiffSum.Add(absDiff, out var absCount) : _absDiffSum.Preview(absDiff, out absCount);
        var diffAvg = diffCount > 0 ? diffSum / diffCount : 0;
        var absDiffAvg = absCount > 0 ? absDiffSum / absCount : 0;
        var z = absDiffAvg != 0 ? diffAvg / absDiffAvg : 0;
        var expValue = MathHelper.Exp(2 * z);
        var r = expValue + 1 != 0 ? (expValue - 1) / (expValue + 1) : 0;

        _indexValue = _index;
        var sma = _sma.Next(value, isFinal);
        var indexSma = _indexSma.Next(_indexValue, isFinal);
        var stdDevSrc = _stdDevSrc.Update(bar, isFinal, includeOutputs: false).Value;
        var indexStdDev = _indexStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var a = indexStdDev != 0 && r != 0 ? (_indexValue - indexSma) / indexStdDev * r : 0;
        var b = sma + (a * stdDevSrc);

        if (isFinal)
        {
            _prevB = b;
            _hasPrev = true;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Flsma", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _indexSma.Dispose();
        _stdDevSrc.Dispose();
        _indexStdDev.Dispose();
        _diffSum.Dispose();
        _absDiffSum.Dispose();
    }
}

public sealed class FisherTransformStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _wma1;
    private readonly IMovingAverageSmoother _wma2;
    private readonly IMovingAverageSmoother _wma3;
    private readonly IMovingAverageSmoother _wma4;
    private readonly IMovingAverageSmoother _wma5;
    private readonly IMovingAverageSmoother _wma6;
    private readonly IMovingAverageSmoother _wma7;
    private readonly IMovingAverageSmoother _wma8;
    private readonly IMovingAverageSmoother _wma9;
    private readonly IMovingAverageSmoother _wma10;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly RollingWindowSum _numSum;
    private readonly RollingWindowSum _denomSum;
    private readonly StreamingInputResolver _input;

    public FisherTransformStochasticOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 2, int stochLength = 30, int smoothLength = 5, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        _wma1 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma2 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma3 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma4 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma5 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma6 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma7 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma8 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma9 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma10 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _maxWindow = new RollingWindowMax(Math.Max(1, stochLength));
        _minWindow = new RollingWindowMin(Math.Max(1, stochLength));
        _numSum = new RollingWindowSum(Math.Max(1, smoothLength));
        _denomSum = new RollingWindowSum(Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public FisherTransformStochasticOscillatorState(MovingAvgType maType, int length, int stochLength, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        _wma1 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma2 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma3 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma4 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma5 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma6 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma7 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma8 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma9 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _wma10 = MovingAverageSmootherFactory.Create(maType, resolvedLength);
        _maxWindow = new RollingWindowMax(Math.Max(1, stochLength));
        _minWindow = new RollingWindowMin(Math.Max(1, stochLength));
        _numSum = new RollingWindowSum(Math.Max(1, smoothLength));
        _denomSum = new RollingWindowSum(Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FisherTransformStochasticOscillator;

    public void Reset()
    {
        _wma1.Reset();
        _wma2.Reset();
        _wma3.Reset();
        _wma4.Reset();
        _wma5.Reset();
        _wma6.Reset();
        _wma7.Reset();
        _wma8.Reset();
        _wma9.Reset();
        _wma10.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _numSum.Reset();
        _denomSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma1 = _wma1.Next(value, isFinal);
        var wma2 = _wma2.Next(wma1, isFinal);
        var wma3 = _wma3.Next(wma2, isFinal);
        var wma4 = _wma4.Next(wma3, isFinal);
        var wma5 = _wma5.Next(wma4, isFinal);
        var wma6 = _wma6.Next(wma5, isFinal);
        var wma7 = _wma7.Next(wma6, isFinal);
        var wma8 = _wma8.Next(wma7, isFinal);
        var wma9 = _wma9.Next(wma8, isFinal);
        var wma10 = _wma10.Next(wma9, isFinal);

        var rbw = ((wma1 * 5) + (wma2 * 4) + (wma3 * 3) + (wma4 * 2) + wma5 + wma6 + wma7 + wma8 + wma9 + wma10) / 20;
        var highest = isFinal ? _maxWindow.Add(rbw, out _) : _maxWindow.Preview(rbw, out _);
        var lowest = isFinal ? _minWindow.Add(rbw, out _) : _minWindow.Preview(rbw, out _);
        var num = rbw - lowest;
        var denom = highest - lowest;
        var numSum = isFinal ? _numSum.Add(num, out _) : _numSum.Preview(num, out _);
        var denomSum = isFinal ? _denomSum.Add(denom, out _) : _denomSum.Preview(denom, out _);
        var rbws = denomSum + 0.0001 != 0
            ? MathHelper.MinOrMax(numSum / (denomSum + 0.0001) * 100, 100, 0)
            : 0;
        var x = 0.1 * (rbws - 50);
        var expValue = MathHelper.Exp(2 * x);
        var ftso = MathHelper.MinOrMax((((expValue - 1) / (expValue + 1)) + 1) * 50, 100, 0);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ftso", ftso }
            };
        }

        return new StreamingIndicatorStateResult(ftso, outputs);
    }

    public void Dispose()
    {
        _wma1.Dispose();
        _wma2.Dispose();
        _wma3.Dispose();
        _wma4.Dispose();
        _wma5.Dispose();
        _wma6.Dispose();
        _wma7.Dispose();
        _wma8.Dispose();
        _wma9.Dispose();
        _wma10.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _numSum.Dispose();
        _denomSum.Dispose();
    }
}

public sealed class FlaggingBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _aValues;
    private readonly PooledRingBuffer<double> _bValues;
    private readonly StreamingInputResolver _input;
    private double _prevTos;
    private bool _hasPrevTos;

    public FlaggingBandsState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, inputName);
        _aValues = new PooledRingBuffer<double>(3);
        _bValues = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FlaggingBandsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, selector);
        _aValues = new PooledRingBuffer<double>(3);
        _bValues = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FlaggingBands;

    public void Reset()
    {
        _stdDev.Reset();
        _aValues.Clear();
        _bValues.Clear();
        _prevTos = 0;
        _hasPrevTos = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevA1 = _aValues.Count >= 1 ? _aValues[_aValues.Count - 1] : value;
        var prevA2 = _aValues.Count >= 2 ? _aValues[_aValues.Count - 2] : value;
        var prevA3 = _aValues.Count >= 3 ? _aValues[_aValues.Count - 3] : value;
        var prevB1 = _bValues.Count >= 1 ? _bValues[_bValues.Count - 1] : value;
        var prevB2 = _bValues.Count >= 2 ? _bValues[_bValues.Count - 2] : value;
        var prevB3 = _bValues.Count >= 3 ? _bValues[_bValues.Count - 3] : value;
        var l = stdDev != 0 ? (double)1 / _length * stdDev : 0;

        var a = value > prevA1 ? prevA1 + (value - prevA1) : prevA2 == prevA3 ? prevA2 - l : prevA2;
        var b = value < prevB1 ? prevB1 + (value - prevB1) : prevB2 == prevB3 ? prevB2 + l : prevB2;
        var prevTos = _hasPrevTos ? _prevTos : 0;
        var tos = value > prevA2 ? 1 : value < prevB2 ? 0 : prevTos;

        var avg = (a + b) / 2;
        var tavg = tos == 1 ? (a + avg) / 2 : (b + avg) / 2;
        var ts = (tos * b) + ((1 - tos) * a);

        if (isFinal)
        {
            _aValues.TryAdd(a, out _);
            _bValues.TryAdd(b, out _);
            _prevTos = tos;
            _hasPrevTos = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "UpperBand", a },
                { "MiddleBand", tavg },
                { "LowerBand", b },
                { "TrailingStop", ts }
            };
        }

        return new StreamingIndicatorStateResult(tavg, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _aValues.Dispose();
        _bValues.Dispose();
    }
}

public sealed class FloorPivotPointsState : IStreamingIndicatorState
{
    private int _index;

    public IndicatorName Name => IndicatorName.FloorPivotPoints;

    public void Reset()
    {
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var useCurrent = _index >= 1;
        var prevHigh = useCurrent ? bar.High : 0;
        var prevLow = useCurrent ? bar.Low : 0;
        var prevClose = useCurrent ? bar.Close : 0;

        var range = prevHigh - prevLow;
        var pivot = (prevHigh + prevLow + prevClose) / 3;
        var support1 = (pivot * 2) - prevHigh;
        var resistance1 = (pivot * 2) - prevLow;
        var support2 = pivot - range;
        var resistance2 = pivot + range;
        var support3 = support1 - range;
        var resistance3 = resistance1 + range;
        var midpoint1 = (support3 + support2) / 2;
        var midpoint2 = (support2 + support1) / 2;
        var midpoint3 = (support1 + pivot) / 2;
        var midpoint4 = (resistance1 + pivot) / 2;
        var midpoint5 = (resistance2 + resistance1) / 2;
        var midpoint6 = (resistance3 + resistance2) / 2;

        if (isFinal)
        {
            _index++;
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

public sealed class FoldedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RsiState _rsi;
    private readonly RollingWindowSum _absSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public FoldedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _absSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FoldedRelativeStrengthIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _absSum = new RollingWindowSum(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FoldedRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _absSum.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var absRsi = 2 * Math.Abs(rsi - 50);
        var frsi = isFinal ? _absSum.Add(absRsi, out _) : _absSum.Preview(absRsi, out _);
        var signal = _signalSmoother.Next(frsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Frsi", frsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(frsi, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _absSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ForceIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ForceIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ForceIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ForceIndex;

    public void Reset()
    {
        _smoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var rawForce = _hasPrev ? (value - prevValue) * bar.Volume : 0;
        var force = _smoother.Next(rawForce, isFinal);

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
                { "Fi", force }
            };
        }

        return new StreamingIndicatorStateResult(force, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class ForecastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ForecastOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3, InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ForecastOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ForecastOscillator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var pf = value != 0 ? 100 * (_hasPrev ? value - prevValue : 0) / value : 0;
        var signal = _signalSmoother.Next(pf, isFinal);

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
                { "Fo", pf },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pf, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class FractalChaosBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private double _prevUpper;
    private double _prevLower;

    public FractalChaosBandsState()
    {
        _highs = new PooledRingBuffer<double>(3);
        _lows = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.FractalChaosBands;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _prevUpper = 0;
        _prevLower = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh1 = EhlersStreamingWindow.GetOffsetValue(_highs, 1);
        var prevHigh2 = EhlersStreamingWindow.GetOffsetValue(_highs, 2);
        var prevHigh3 = EhlersStreamingWindow.GetOffsetValue(_highs, 3);
        var prevLow1 = EhlersStreamingWindow.GetOffsetValue(_lows, 1);
        var prevLow2 = EhlersStreamingWindow.GetOffsetValue(_lows, 2);
        var prevLow3 = EhlersStreamingWindow.GetOffsetValue(_lows, 3);
        double oklUpper = prevHigh1 < prevHigh2 ? 1 : 0;
        double okrUpper = prevHigh3 < prevHigh2 ? 1 : 0;
        double oklLower = prevLow1 > prevLow2 ? 1 : 0;
        double okrLower = prevLow3 > prevLow2 ? 1 : 0;

        var upper = oklUpper == 1 && okrUpper == 1 ? prevHigh2 : _prevUpper;
        var lower = oklLower == 1 && okrLower == 1 ? prevLow2 : _prevLower;
        var middle = (upper + lower) / 2;

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevUpper = upper;
            _prevLower = lower;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", middle },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

public sealed class FractalChaosOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private double _prevUpper;
    private double _prevLower;

    public FractalChaosOscillatorState()
    {
        _highs = new PooledRingBuffer<double>(3);
        _lows = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.FractalChaosOscillator;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _prevUpper = 0;
        _prevLower = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh1 = EhlersStreamingWindow.GetOffsetValue(_highs, 1);
        var prevHigh2 = EhlersStreamingWindow.GetOffsetValue(_highs, 2);
        var prevHigh3 = EhlersStreamingWindow.GetOffsetValue(_highs, 3);
        var prevLow1 = EhlersStreamingWindow.GetOffsetValue(_lows, 1);
        var prevLow2 = EhlersStreamingWindow.GetOffsetValue(_lows, 2);
        var prevLow3 = EhlersStreamingWindow.GetOffsetValue(_lows, 3);
        double oklUpper = prevHigh1 < prevHigh2 ? 1 : 0;
        double okrUpper = prevHigh3 < prevHigh2 ? 1 : 0;
        double oklLower = prevLow1 > prevLow2 ? 1 : 0;
        double okrLower = prevLow3 > prevLow2 ? 1 : 0;

        var upper = oklUpper == 1 && okrUpper == 1 ? prevHigh2 : _prevUpper;
        var lower = oklLower == 1 && okrLower == 1 ? prevLow2 : _prevLower;
        var fco = upper != _prevUpper ? 1 : lower != _prevLower ? -1 : 0;

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _prevUpper = upper;
            _prevLower = lower;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fco", fco }
            };
        }

        return new StreamingIndicatorStateResult(fco, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

public sealed class FreedomOfMovementState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private readonly StandardDeviationVolatilityState _volumeStdDev;
    private readonly RollingWindowMax _aMoveMax;
    private readonly RollingWindowMin _aMoveMin;
    private readonly RollingWindowMax _relVolMax;
    private readonly RollingWindowMin _relVolMin;
    private readonly RollingWindowSum _vBymSum;
    private readonly StandardDeviationVolatilityState _vBymStdDev;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevDpl;
    private bool _hasPrev;
    private bool _hasPrevDpl;
    private double _vBymValue;

    public FreedomOfMovementState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _volumeStdDev = new StandardDeviationVolatilityState(maType, _length, InputName.Volume);
        _aMoveMax = new RollingWindowMax(_length);
        _aMoveMin = new RollingWindowMin(_length);
        _relVolMax = new RollingWindowMax(_length);
        _relVolMin = new RollingWindowMin(_length);
        _vBymSum = new RollingWindowSum(_length);
        _vBymStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _vBymValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FreedomOfMovementState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _volumeStdDev = new StandardDeviationVolatilityState(maType, _length, InputName.Volume);
        _aMoveMax = new RollingWindowMax(_length);
        _aMoveMin = new RollingWindowMin(_length);
        _relVolMax = new RollingWindowMax(_length);
        _relVolMin = new RollingWindowMin(_length);
        _vBymSum = new RollingWindowSum(_length);
        _vBymStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _vBymValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FreedomOfMovement;

    public void Reset()
    {
        _volumeSmoother.Reset();
        _volumeStdDev.Reset();
        _aMoveMax.Reset();
        _aMoveMin.Reset();
        _relVolMax.Reset();
        _relVolMin.Reset();
        _vBymSum.Reset();
        _vBymStdDev.Reset();
        _prevValue = 0;
        _prevDpl = 0;
        _hasPrev = false;
        _hasPrevDpl = false;
        _vBymValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var avgVolume = _volumeSmoother.Next(bar.Volume, isFinal);
        var sdVolume = _volumeStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var relVol = sdVolume != 0 ? (bar.Volume - avgVolume) / sdVolume : 0;

        var priceChg = _hasPrev ? value - prevValue : 0;
        var aMove = prevValue != 0 ? Math.Abs(priceChg / prevValue) : 0;
        var aMoveMax = isFinal ? _aMoveMax.Add(aMove, out _) : _aMoveMax.Preview(aMove, out _);
        var aMoveMin = isFinal ? _aMoveMin.Add(aMove, out _) : _aMoveMin.Preview(aMove, out _);
        var theMove = aMoveMax - aMoveMin != 0
            ? (1 + ((aMove - aMoveMin) * (10 - 1))) / (aMoveMax - aMoveMin)
            : 0;
        var relVolMax = isFinal ? _relVolMax.Add(relVol, out _) : _relVolMax.Preview(relVol, out _);
        var relVolMin = isFinal ? _relVolMin.Add(relVol, out _) : _relVolMin.Preview(relVol, out _);
        var theVol = relVolMax - relVolMin != 0
            ? (1 + ((relVol - relVolMin) * (10 - 1))) / (relVolMax - relVolMin)
            : 0;
        var vBym = theMove != 0 ? theVol / theMove : 0;
        var vBymSum = isFinal ? _vBymSum.Add(vBym, out var countAfter) : _vBymSum.Preview(vBym, out countAfter);
        var avf = countAfter > 0 ? vBymSum / countAfter : 0;

        _vBymValue = vBym;
        var sdf = _vBymStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var theFom = sdf != 0 ? (vBym - avf) / sdf : 0;
        var prevDpl = _hasPrevDpl ? _prevDpl : 0;
        var dpl = theFom >= 2 ? prevValue : _hasPrevDpl ? prevDpl : value;

        if (isFinal)
        {
            _prevValue = value;
            _prevDpl = dpl;
            _hasPrev = true;
            _hasPrevDpl = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fom", theFom },
                { "Dpl", dpl }
            };
        }

        return new StreamingIndicatorStateResult(theFom, outputs);
    }

    public void Dispose()
    {
        _volumeSmoother.Dispose();
        _volumeStdDev.Dispose();
        _aMoveMax.Dispose();
        _aMoveMin.Dispose();
        _relVolMax.Dispose();
        _relVolMin.Dispose();
        _vBymSum.Dispose();
        _vBymStdDev.Dispose();
    }
}

public sealed class FunctionToCandlesState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsiC;
    private readonly RsiState _rsiO;
    private readonly RsiState _rsiH;
    private readonly RsiState _rsiL;

    public FunctionToCandlesState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14)
    {
        var resolved = Math.Max(1, length);
        _rsiC = new RsiState(maType, resolved);
        _rsiO = new RsiState(maType, resolved);
        _rsiH = new RsiState(maType, resolved);
        _rsiL = new RsiState(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.FunctionToCandles;

    public void Reset()
    {
        _rsiC.Reset();
        _rsiO.Reset();
        _rsiH.Reset();
        _rsiL.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var rsiC = _rsiC.Next(bar.Close, isFinal);
        var rsiO = _rsiO.Next(bar.Open, isFinal);
        var rsiH = _rsiH.Next(bar.High, isFinal);
        var rsiL = _rsiL.Next(bar.Low, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Close", rsiC },
                { "Open", rsiO },
                { "High", rsiH },
                { "Low", rsiL }
            };
        }

        return new StreamingIndicatorStateResult(rsiC, outputs);
    }

    public void Dispose()
    {
        _rsiC.Dispose();
        _rsiO.Dispose();
        _rsiH.Dispose();
        _rsiL.Dispose();
    }
}

public sealed class FXSniperIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly CommodityChannelIndexState _cciState;
    private readonly double _c1;
    private readonly double _c2;
    private readonly double _c3;
    private readonly double _c4;
    private readonly double _w1;
    private readonly double _w2;
    private double _e1;
    private double _e2;
    private double _e3;
    private double _e4;
    private double _e5;
    private double _e6;

    public FXSniperIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int cciLength = 14, int t3Length = 5, double b = MathHelper.InversePhi,
        InputName inputName = InputName.TypicalPrice)
    {
        _cciState = new CommodityChannelIndexState(inputName, maType, Math.Max(1, cciLength), 0.015);
        var b2 = b * b;
        var b3 = b2 * b;
        _c1 = -b3;
        _c2 = 3 * (b2 + b3);
        _c3 = -3 * ((2 * b2) + b + b3);
        _c4 = 1 + (3 * b) + b3 + (3 * b2);
        var nr = 1 + (0.5 * (t3Length - 1));
        _w1 = 2 / (nr + 1);
        _w2 = 1 - _w1;
    }

    public FXSniperIndicatorState(MovingAvgType maType, int cciLength, int t3Length, double b,
        InputName inputName, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _cciState = new CommodityChannelIndexState(inputName, maType, Math.Max(1, cciLength), 0.015, selector);
        var b2 = b * b;
        var b3 = b2 * b;
        _c1 = -b3;
        _c2 = 3 * (b2 + b3);
        _c3 = -3 * ((2 * b2) + b + b3);
        _c4 = 1 + (3 * b) + b3 + (3 * b2);
        var nr = 1 + (0.5 * (t3Length - 1));
        _w1 = 2 / (nr + 1);
        _w2 = 1 - _w1;
    }

    public IndicatorName Name => IndicatorName.FXSniperIndicator;

    public void Reset()
    {
        _cciState.Reset();
        _e1 = 0;
        _e2 = 0;
        _e3 = 0;
        _e4 = 0;
        _e5 = 0;
        _e6 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var cci = _cciState.Update(bar, isFinal, includeOutputs: false).Value;

        var e1 = (_w1 * cci) + (_w2 * _e1);
        var e2 = (_w1 * e1) + (_w2 * _e2);
        var e3 = (_w1 * e2) + (_w2 * _e3);
        var e4 = (_w1 * e3) + (_w2 * _e4);
        var e5 = (_w1 * e4) + (_w2 * _e5);
        var e6 = (_w1 * e5) + (_w2 * _e6);
        var fxsniper = (_c1 * e6) + (_c2 * e5) + (_c3 * e4) + (_c4 * e3);

        if (isFinal)
        {
            _e1 = e1;
            _e2 = e2;
            _e3 = e3;
            _e4 = e4;
            _e5 = e5;
            _e6 = e6;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "FXSniper", fxsniper }
            };
        }

        return new StreamingIndicatorStateResult(fxsniper, outputs);
    }

    public void Dispose()
    {
        _cciState.Dispose();
    }
}

public sealed class GainLossMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _glmaSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public GainLossMovingAverageState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 14, int signalLength = 7, InputName inputName = InputName.Close)
    {
        _glmaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public GainLossMovingAverageState(MovingAvgType maType, int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _glmaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GainLossMovingAverage;

    public void Reset()
    {
        _glmaSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceChg = _hasPrev ? value - prevValue : 0;
        var gainLoss = value + prevValue != 0 ? priceChg / ((value + prevValue) / 2) * 100 : 0;
        var glma = _glmaSmoother.Next(gainLoss, isFinal);
        var signal = _signalSmoother.Next(glma, isFinal);

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
                { "Glma", glma },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(glma, outputs);
    }

    public void Dispose()
    {
        _glmaSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class GannHiLoActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _highMa;
    private readonly IMovingAverageSmoother _lowMa;
    private readonly StreamingInputResolver _input;
    private double _prevHighMa;
    private double _prevLowMa;
    private double _prevGhla;
    private bool _hasPrev;

    public GannHiLoActivatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 3, InputName inputName = InputName.Close)
    {
        _highMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _lowMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public GannHiLoActivatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _highMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _lowMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.GannHiLoActivator;

    public void Reset()
    {
        _highMa.Reset();
        _lowMa.Reset();
        _prevHighMa = 0;
        _prevLowMa = 0;
        _prevGhla = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highMa = _highMa.Next(bar.High, isFinal);
        var lowMa = _lowMa.Next(bar.Low, isFinal);
        var prevHighMa = _hasPrev ? _prevHighMa : 0;
        var prevLowMa = _hasPrev ? _prevLowMa : 0;
        var prevGhla = _hasPrev ? _prevGhla : 0;
        var ghla = value > prevHighMa ? lowMa : value < prevLowMa ? highMa : prevGhla;

        if (isFinal)
        {
            _prevHighMa = highMa;
            _prevLowMa = lowMa;
            _prevGhla = ghla;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ghla", ghla }
            };
        }

        return new StreamingIndicatorStateResult(ghla, outputs);
    }

    public void Dispose()
    {
        _highMa.Dispose();
        _lowMa.Dispose();
    }
}

public sealed class GannSwingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private double _prevGso;
    private bool _hasPrev;

    public GannSwingOscillatorState(int length = 5)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highestValues = new PooledRingBuffer<double>(2);
        _lowestValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.GannSwingOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _prevGso = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest1 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 1);
        var prevHighest2 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 2);
        var prevLowest1 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 1);
        var prevLowest2 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 2);
        var prevGso = _hasPrev ? _prevGso : 0;
        var gso = prevHighest2 > prevHighest1 && highest > prevHighest1 ? 1
            : prevLowest2 < prevLowest1 && lowest < prevLowest1 ? -1
            : prevGso;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _prevGso = gso;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gso", gso }
            };
        }

        return new StreamingIndicatorStateResult(gso, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
    }
}

public sealed class GannTrendOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private double _prevGto;
    private bool _hasPrev;

    public GannTrendOscillatorState(int length = 3)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _highestValues = new PooledRingBuffer<double>(2);
        _lowestValues = new PooledRingBuffer<double>(2);
    }

    public IndicatorName Name => IndicatorName.GannTrendOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _highestValues.Clear();
        _lowestValues.Clear();
        _prevGto = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var prevHighest1 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 1);
        var prevHighest2 = EhlersStreamingWindow.GetOffsetValue(_highestValues, 2);
        var prevLowest1 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 1);
        var prevLowest2 = EhlersStreamingWindow.GetOffsetValue(_lowestValues, 2);
        var prevGto = _hasPrev ? _prevGto : 0;
        var gto = prevHighest2 > prevHighest1 && highest > prevHighest1 ? 1
            : prevLowest2 < prevLowest1 && lowest < prevLowest1 ? -1
            : prevGto;

        if (isFinal)
        {
            _highestValues.TryAdd(highest, out _);
            _lowestValues.TryAdd(lowest, out _);
            _prevGto = gto;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Gto", gto }
            };
        }

        return new StreamingIndicatorStateResult(gto, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
    }
}
