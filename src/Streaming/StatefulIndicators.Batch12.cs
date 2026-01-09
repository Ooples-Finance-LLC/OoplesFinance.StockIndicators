using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;

public sealed class ElasticVolumeWeightedMovingAverageV1State : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private double _prevEvwma;
    private bool _hasPrev;

    public ElasticVolumeWeightedMovingAverageV1State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 40, double mult = 20, InputName inputName = InputName.Close)
    {
        _mult = mult;
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ElasticVolumeWeightedMovingAverageV1State(MovingAvgType maType, int length, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _mult = mult;
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ElasticVolumeWeightedMovingAverageV1;

    public void Reset()
    {
        _volumeSmoother.Reset();
        _prevEvwma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var avgVolume = _volumeSmoother.Next(bar.Volume, isFinal);
        var n = avgVolume * _mult;
        var prevEvwma = _hasPrev ? _prevEvwma : value;
        var evwma = n > 0 ? (((n - bar.Volume) * prevEvwma) + (bar.Volume * value)) / n : 0;

        if (isFinal)
        {
            _prevEvwma = evwma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Evwma", evwma }
            };
        }

        return new StreamingIndicatorStateResult(evwma, outputs);
    }

    public void Dispose()
    {
        _volumeSmoother.Dispose();
    }
}

public sealed class ElasticVolumeWeightedMovingAverageV2State : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumeSum;
    private readonly StreamingInputResolver _input;
    private double _prevEvwma;
    private bool _hasPrev;

    public ElasticVolumeWeightedMovingAverageV2State(int length = 14, InputName inputName = InputName.Close)
    {
        _volumeSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ElasticVolumeWeightedMovingAverageV2State(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _volumeSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ElasticVolumeWeightedMovingAverageV2;

    public void Reset()
    {
        _volumeSum.Reset();
        _prevEvwma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumeSum = isFinal ? _volumeSum.Add(volume, out _) : _volumeSum.Preview(volume, out _);
        var prevEvwma = _hasPrev ? _prevEvwma : 0;
        var evwma = volumeSum != 0 ? (((volumeSum - volume) * prevEvwma) + (volume * value)) / volumeSum : 0;

        if (isFinal)
        {
            _prevEvwma = evwma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Evwma", evwma }
            };
        }

        return new StreamingIndicatorStateResult(evwma, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
    }
}

public sealed class ElderMarketThermometerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _prevLow;
    private double _prevHigh;
    private bool _hasPrev;

    public ElderMarketThermometerState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 22)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.ElderMarketThermometer;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevLow = 0;
        _prevHigh = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;

        var emt = bar.High < prevHigh && bar.Low > prevLow
            ? 0
            : bar.High - prevHigh > prevLow - bar.Low
                ? Math.Abs(bar.High - prevHigh)
                : Math.Abs(prevLow - bar.Low);

        var signal = _signalSmoother.Next(emt, isFinal);

        if (isFinal)
        {
            _prevLow = bar.Low;
            _prevHigh = bar.High;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Emt", emt },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(emt, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class ElderSafeZoneStopsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowSum _dmMinusCountSum;
    private readonly RollingWindowSum _dmPlusCountSum;
    private readonly RollingWindowSum _dmMinusSum;
    private readonly RollingWindowSum _dmPlusSum;
    private readonly RollingWindowMax _safeZMinusMax;
    private readonly RollingWindowMin _safeZPlusMin;
    private readonly double _factor;
    private readonly StreamingInputResolver _input;
    private double _prevLow;
    private double _prevHigh;
    private bool _hasPrev;

    public ElderSafeZoneStopsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63,
        int length2 = 22, int length3 = 3, double factor = 2.5, InputName inputName = InputName.Close)
    {
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dmMinusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmMinusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _safeZMinusMax = new RollingWindowMax(Math.Max(1, length3));
        _safeZPlusMin = new RollingWindowMin(Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
        _factor = factor;
    }

    public ElderSafeZoneStopsState(MovingAvgType maType, int length1, int length2, int length3, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dmMinusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmMinusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _safeZMinusMax = new RollingWindowMax(Math.Max(1, length3));
        _safeZPlusMin = new RollingWindowMin(Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _factor = factor;
    }

    public IndicatorName Name => IndicatorName.ElderSafeZoneStops;

    public void Reset()
    {
        _ema.Reset();
        _dmMinusSum.Reset();
        _dmMinusCountSum.Reset();
        _dmPlusSum.Reset();
        _dmPlusCountSum.Reset();
        _safeZMinusMax.Reset();
        _safeZPlusMin.Reset();
        _prevLow = 0;
        _prevHigh = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;

        var dmMinus = prevLow > bar.Low ? prevLow - bar.Low : 0;
        var dmMinusCount = prevLow > bar.Low ? 1d : 0d;
        var dmPlus = bar.High > prevHigh ? bar.High - prevHigh : 0;
        var dmPlusCount = bar.High > prevHigh ? 1d : 0d;

        var dmMinusSum = isFinal ? _dmMinusSum.Add(dmMinus, out _) : _dmMinusSum.Preview(dmMinus, out _);
        var dmMinusCountSum = isFinal ? _dmMinusCountSum.Add(dmMinusCount, out _) : _dmMinusCountSum.Preview(dmMinusCount, out _);
        var dmPlusSum = isFinal ? _dmPlusSum.Add(dmPlus, out _) : _dmPlusSum.Preview(dmPlus, out _);
        var dmPlusCountSum = isFinal ? _dmPlusCountSum.Add(dmPlusCount, out _) : _dmPlusCountSum.Preview(dmPlusCount, out _);

        var dmAvgMinus = dmMinusCountSum != 0 ? dmMinusSum / dmMinusCountSum : 0;
        var dmAvgPlus = dmPlusCountSum != 0 ? dmPlusSum / dmPlusCountSum : 0;

        var safeZMinus = prevLow - (_factor * dmAvgMinus);
        var safeZPlus = prevHigh + (_factor * dmAvgPlus);

        var highest = isFinal ? _safeZMinusMax.Add(safeZMinus, out _) : _safeZMinusMax.Preview(safeZMinus, out _);
        var lowest = isFinal ? _safeZPlusMin.Add(safeZPlus, out _) : _safeZPlusMin.Preview(safeZPlus, out _);
        var ema = _ema.Next(value, isFinal);
        var stop = value >= ema ? highest : lowest;

        if (isFinal)
        {
            _prevLow = bar.Low;
            _prevHigh = bar.High;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eszs", stop }
            };
        }

        return new StreamingIndicatorStateResult(stop, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _dmMinusSum.Dispose();
        _dmMinusCountSum.Dispose();
        _dmPlusSum.Dispose();
        _dmPlusCountSum.Dispose();
        _safeZMinusMax.Dispose();
        _safeZPlusMin.Dispose();
    }
}

public sealed class ElliottWaveOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ElliottWaveOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 34, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ElliottWaveOscillatorState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ElliottWaveOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var ewo = fast - slow;
        var signal = _signalSmoother.Next(ewo, isFinal);
        var histogram = ewo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ewo", ewo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(ewo, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EmaWaveIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _emaA;
    private readonly IMovingAverageSmoother _emaB;
    private readonly IMovingAverageSmoother _emaC;
    private readonly IMovingAverageSmoother _waSmoother;
    private readonly IMovingAverageSmoother _wbSmoother;
    private readonly IMovingAverageSmoother _wcSmoother;
    private readonly StreamingInputResolver _input;

    public EmaWaveIndicatorState(int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4,
        InputName inputName = InputName.Close)
    {
        _emaA = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length1));
        _emaB = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length2));
        _emaC = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length3));
        var resolvedSmooth = Math.Max(1, smoothLength);
        _waSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _wbSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _wcSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EmaWaveIndicatorState(int length1, int length2, int length3, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _emaA = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length1));
        _emaB = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length2));
        _emaC = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length3));
        var resolvedSmooth = Math.Max(1, smoothLength);
        _waSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _wbSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _wcSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSmooth);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EmaWaveIndicator;

    public void Reset()
    {
        _emaA.Reset();
        _emaB.Reset();
        _emaC.Reset();
        _waSmoother.Reset();
        _wbSmoother.Reset();
        _wcSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var emaA = _emaA.Next(value, isFinal);
        var emaB = _emaB.Next(value, isFinal);
        var emaC = _emaC.Next(value, isFinal);
        var emaADiff = value - emaA;
        var emaBDiff = value - emaB;
        var emaCDiff = value - emaC;
        var wa = _waSmoother.Next(emaADiff, isFinal);
        var wb = _wbSmoother.Next(emaBDiff, isFinal);
        var wc = _wcSmoother.Next(emaCDiff, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Wa", wa },
                { "Wb", wb },
                { "Wc", wc }
            };
        }

        return new StreamingIndicatorStateResult(wa, outputs);
    }

    public void Dispose()
    {
        _emaA.Dispose();
        _emaB.Dispose();
        _emaC.Dispose();
        _waSmoother.Dispose();
        _wbSmoother.Dispose();
        _wcSmoother.Dispose();
    }
}

public sealed class EndPointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public EndPointMovingAverageState(int length = 11, int offset = 4, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var weight = resolved - j - offset;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EndPointMovingAverageState(int length, int offset, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _weights = new double[resolved];
        double weightSum = 0;
        for (var j = 0; j < resolved; j++)
        {
            var weight = resolved - j - offset;
            _weights[j] = weight;
            weightSum += weight;
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EndPointMovingAverage;

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

        var epma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Epma", epma }
            };
        }

        return new StreamingIndicatorStateResult(epma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class EnhancedIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public EnhancedIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int signalLength = 8, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EnhancedIndexState(MovingAvgType maType, int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EnhancedIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _smaSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var dnm = highest - lowest;
        var sma = _smaSmoother.Next(value, isFinal);
        var closewr = dnm != 0 ? 2 * (value - sma) / dnm : 0;
        var signal = _signalSmoother.Next(closewr, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ei", closewr },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(closewr, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _smaSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EnhancedWilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _srcMax;
    private readonly RollingWindowMin _srcMin;
    private readonly RollingWindowMax _volMax;
    private readonly RollingWindowMin _volMin;
    private readonly IMovingAverageSmoother _srcSma;
    private readonly IMovingAverageSmoother _volSma;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _af;
    private double _prevValue;
    private bool _hasPrev;

    public EnhancedWilliamsRState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int signalLength = 5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(2, length);
        _srcMax = new RollingWindowMax(resolved);
        _srcMin = new RollingWindowMin(resolved);
        _volMax = new RollingWindowMax(resolved);
        _volMin = new RollingWindowMin(resolved);
        _af = length < 10 ? 0.25 : ((double)length / 32) - 0.0625;
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _srcSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _volSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public EnhancedWilliamsRState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(2, length);
        _srcMax = new RollingWindowMax(resolved);
        _srcMin = new RollingWindowMin(resolved);
        _volMax = new RollingWindowMax(resolved);
        _volMin = new RollingWindowMin(resolved);
        _af = length < 10 ? 0.25 : ((double)length / 32) - 0.0625;
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _srcSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _volSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EnhancedWilliamsR;

    public void Reset()
    {
        _srcMax.Reset();
        _srcMin.Reset();
        _volMax.Reset();
        _volMin.Reset();
        _srcSma.Reset();
        _volSma.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = _hasPrev ? _prevValue : 0;

        var maxVol = isFinal ? _volMax.Add(volume, out _) : _volMax.Preview(volume, out _);
        var minVol = isFinal ? _volMin.Add(volume, out _) : _volMin.Preview(volume, out _);
        var maxSrc = isFinal ? _srcMax.Add(value, out _) : _srcMax.Preview(value, out _);
        var minSrc = isFinal ? _srcMin.Add(value, out _) : _srcMin.Preview(value, out _);
        var srcSma = _srcSma.Next(value, isFinal);
        var volSma = _volSma.Next(volume, isFinal);
        var volWr = maxVol - minVol != 0 ? 2 * ((volume - volSma) / (maxVol - minVol)) : 0;
        var srcWr = maxSrc - minSrc != 0 ? 2 * ((value - srcSma) / (maxSrc - minSrc)) : 0;
        var priceDiff = _hasPrev ? value - prevValue : 0;
        var srcSwr = maxSrc - minSrc != 0 ? 2 * (priceDiff / (maxSrc - minSrc)) : 0;

        var ewr = ((volWr > 0 && srcWr > 0 && value > prevValue) ||
            ((volWr > 0 && srcWr < 0 && value < prevValue)) && srcSwr + _af != 0
            ? ((50 * (srcWr * (srcSwr + _af) * volWr)) + srcSwr + _af) / (srcSwr + _af)
            : 25 * ((srcWr * (volWr + 1)) + 2));
        var signal = _signalSmoother.Next(ewr, isFinal);

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
                { "Ewr", ewr },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ewr, outputs);
    }

    public void Dispose()
    {
        _srcMax.Dispose();
        _srcMin.Dispose();
        _volMax.Dispose();
        _volMin.Dispose();
        _srcSma.Dispose();
        _volSma.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class EquityMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _chgXSum;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevEqma;
    private double _prevX;
    private double _chgXCumSum;
    private bool _hasPrev;

    public EquityMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _chgXSum = new RollingWindowSum(resolved);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public EquityMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _chgXSum = new RollingWindowSum(resolved);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.EquityMovingAverage;

    public void Reset()
    {
        _chgXSum.Reset();
        _sma.Reset();
        _prevValue = 0;
        _prevEqma = 0;
        _prevX = 0;
        _chgXCumSum = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var sma = _sma.Next(value, isFinal);
        var prevEqma = _hasPrev ? _prevEqma : value;
        var prevX = _prevX;
        var x = Math.Sign(value - sma);
        var priceDiff = _hasPrev ? value - prevValue : 0;

        var chgX = priceDiff * prevX;
        var req = isFinal ? _chgXSum.Add(chgX, out _) : _chgXSum.Preview(chgX, out _);
        var chgXCum = priceDiff * x;
        var opteq = _chgXCumSum + chgXCum;
        var alpha = opteq != 0 ? MathHelper.MinOrMax(req / opteq, 0.99, 0.01) : 0.99;
        var eqma = (alpha * value) + ((1 - alpha) * prevEqma);

        if (isFinal)
        {
            _prevValue = value;
            _prevEqma = eqma;
            _prevX = x;
            _chgXCumSum = opteq;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eqma", eqma }
            };
        }

        return new StreamingIndicatorStateResult(eqma, outputs);
    }

    public void Dispose()
    {
        _chgXSum.Dispose();
        _sma.Dispose();
    }
}

public sealed class ErgodicCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _xcoEma1;
    private readonly IMovingAverageSmoother _xcoEma2;
    private readonly IMovingAverageSmoother _xhlEma1;
    private readonly IMovingAverageSmoother _xhlEma2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ErgodicCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 32, int length2 = 12, InputName inputName = InputName.Close)
    {
        _xcoEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xcoEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _xhlEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xhlEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicCandlestickOscillatorState(MovingAvgType maType, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _xcoEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xcoEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _xhlEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xhlEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicCandlestickOscillator;

    public void Reset()
    {
        _xcoEma1.Reset();
        _xcoEma2.Reset();
        _xhlEma1.Reset();
        _xhlEma2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var xco = close - bar.Open;
        var xhl = bar.High - bar.Low;
        var xcoEma1 = _xcoEma1.Next(xco, isFinal);
        var xcoEma2 = _xcoEma2.Next(xcoEma1, isFinal);
        var xhlEma1 = _xhlEma1.Next(xhl, isFinal);
        var xhlEma2 = _xhlEma2.Next(xhlEma1, isFinal);
        var eco = xhlEma2 != 0 ? 100 * xcoEma2 / xhlEma2 : 0;
        var signal = _signalSmoother.Next(eco, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eco", eco },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(eco, outputs);
    }

    public void Dispose()
    {
        _xcoEma1.Dispose();
        _xcoEma2.Dispose();
        _xhlEma1.Dispose();
        _xhlEma2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicCommoditySelectionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly IMovingAverageSmoother _dmPlus;
    private readonly IMovingAverageSmoother _dmMinus;
    private readonly IMovingAverageSmoother _tr;
    private readonly IMovingAverageSmoother _adx;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevHigh;
    private double _prevLow;
    private double _prevValue;
    private double _prevAdx;
    private bool _hasPrev;

    public ErgodicCommoditySelectionIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 32, int smoothLength = 5, double pointValue = 1, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _k = 100 * (pointValue / MathHelper.Sqrt(_length) / (150 + smoothLength));
        _dmPlus = MovingAverageSmootherFactory.Create(maType, _length);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, _length);
        _tr = MovingAverageSmootherFactory.Create(maType, _length);
        _adx = MovingAverageSmootherFactory.Create(maType, _length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicCommoditySelectionIndexState(MovingAvgType maType, int length, int smoothLength, double pointValue,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _k = 100 * (pointValue / MathHelper.Sqrt(_length) / (150 + smoothLength));
        _dmPlus = MovingAverageSmootherFactory.Create(maType, _length);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, _length);
        _tr = MovingAverageSmootherFactory.Create(maType, _length);
        _adx = MovingAverageSmootherFactory.Create(maType, _length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicCommoditySelectionIndex;

    public void Reset()
    {
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _adx.Reset();
        _signalSmoother.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevValue = 0;
        _prevAdx = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevAdx = _hasPrev ? _prevAdx : 0;

        var highDiff = bar.High - prevHigh;
        var lowDiff = prevLow - bar.Low;
        var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);

        var dmPlusMa = _dmPlus.Next(dmPlus, isFinal);
        var dmMinusMa = _dmMinus.Next(dmMinus, isFinal);
        var trMa = _tr.Next(tr, isFinal);
        var diPlus = trMa != 0 ? MathHelper.MinOrMax(100 * dmPlusMa / trMa, 100, 0) : 0;
        var diMinus = trMa != 0 ? MathHelper.MinOrMax(100 * dmMinusMa / trMa, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var dx = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.Next(dx, isFinal);
        var adxR = (adx + prevAdx) * 0.5;

        var csi = _length + tr > 0 ? _k * adxR * tr / _length : 0;
        var ergodicCsi = value > 0 ? csi / value : 0;
        var signal = _signalSmoother.Next(ergodicCsi, isFinal);

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevValue = value;
            _prevAdx = adx;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ecsi", ergodicCsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ergodicCsi, outputs);
    }

    public void Dispose()
    {
        _dmPlus.Dispose();
        _dmMinus.Dispose();
        _tr.Dispose();
        _adx.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicMeanDeviationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly IMovingAverageSmoother _ma1Ema;
    private readonly IMovingAverageSmoother _emdi;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ErgodicMeanDeviationIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 55,
        InputName inputName = InputName.Close)
    {
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ma1Ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _emdi = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicMeanDeviationIndicatorState(MovingAvgType maType, int length1, int length2, int length3,
        int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ma1Ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _emdi = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicMeanDeviationIndicator;

    public void Reset()
    {
        _ema.Reset();
        _ma1Ema.Reset();
        _emdi.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var ma1 = value - ema;
        var ma1Ema = _ma1Ema.Next(ma1, isFinal);
        var emdi = _emdi.Next(ma1Ema, isFinal);
        var signal = _signalSmoother.Next(emdi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Emdi", emdi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(emdi, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _ma1Ema.Dispose();
        _emdi.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ErgodicMovingAverageConvergenceDivergenceState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 32, int length2 = 5,
        int length3 = 5, InputName inputName = InputName.Close)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicMovingAverageConvergenceDivergenceState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(value, isFinal);
        var macd = ema1 - ema2;
        var signal = _signalSmoother.Next(macd, isFinal);
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
        _ema1.Dispose();
        _ema2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicPercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ErgodicPercentagePriceOscillatorState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 32, int length2 = 5,
        int length3 = 5, InputName inputName = InputName.Close)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicPercentagePriceOscillatorState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicPercentagePriceOscillator;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(value, isFinal);
        var macd = ema1 - ema2;
        var ppo = ema2 != 0 ? macd / ema2 * 100 : 0;
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
        _ema1.Dispose();
        _ema2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicTrueStrengthIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ErgodicTrueStrengthIndexV1State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3,
        InputName inputName = InputName.Close)
    {
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicTrueStrengthIndexV1State(MovingAvgType maType, int length1, int length2, int length3,
        int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicTrueStrengthIndexV1;

    public void Reset()
    {
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceDiff = _hasPrev ? value - prevValue : 0;
        var absPriceDiff = Math.Abs(priceDiff);

        var diffEma1 = _diffEma1.Next(priceDiff, isFinal);
        var absDiffEma1 = _absDiffEma1.Next(absPriceDiff, isFinal);
        var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
        var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
        var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
        var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
        var etsi = absDiffEma3 != 0 ? MathHelper.MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
        var signal = _signalSmoother.Next(etsi, isFinal);

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
                { "Etsi", etsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(etsi, outputs);
    }

    public void Dispose()
    {
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class ErgodicTrueStrengthIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private readonly IMovingAverageSmoother _diffEma4;
    private readonly IMovingAverageSmoother _absDiffEma4;
    private readonly IMovingAverageSmoother _diffEma5;
    private readonly IMovingAverageSmoother _absDiffEma5;
    private readonly IMovingAverageSmoother _diffEma6;
    private readonly IMovingAverageSmoother _absDiffEma6;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ErgodicTrueStrengthIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6,
        int length6 = 2, int signalLength = 2, InputName inputName = InputName.Close)
    {
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _diffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _absDiffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _diffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _absDiffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _diffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _absDiffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ErgodicTrueStrengthIndexV2State(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int length5, int length6, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _diffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _absDiffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _diffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _absDiffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _diffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _absDiffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ErgodicTrueStrengthIndexV2;

    public void Reset()
    {
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _diffEma4.Reset();
        _absDiffEma4.Reset();
        _diffEma5.Reset();
        _absDiffEma5.Reset();
        _diffEma6.Reset();
        _absDiffEma6.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceDiff = _hasPrev ? value - prevValue : 0;
        var absPriceDiff = Math.Abs(priceDiff);

        var diffEma1 = _diffEma1.Next(priceDiff, isFinal);
        var absDiffEma1 = _absDiffEma1.Next(absPriceDiff, isFinal);
        var diffEma4 = _diffEma4.Next(priceDiff, isFinal);
        var absDiffEma4 = _absDiffEma4.Next(absPriceDiff, isFinal);
        var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
        var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
        var diffEma5 = _diffEma5.Next(diffEma4, isFinal);
        var absDiffEma5 = _absDiffEma5.Next(absDiffEma4, isFinal);
        var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
        var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
        var diffEma6 = _diffEma6.Next(diffEma5, isFinal);
        var absDiffEma6 = _absDiffEma6.Next(absDiffEma5, isFinal);
        var etsi1 = absDiffEma3 != 0 ? MathHelper.MinOrMax(diffEma3 / absDiffEma3 * 100, 100, -100) : 0;
        var etsi2 = absDiffEma6 != 0 ? MathHelper.MinOrMax(diffEma6 / absDiffEma6 * 100, 100, -100) : 0;
        var signal = _signalSmoother.Next(etsi2, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Etsi1", etsi1 },
                { "Etsi2", etsi2 },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(etsi2, outputs);
    }

    public void Dispose()
    {
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
        _diffEma4.Dispose();
        _absDiffEma4.Dispose();
        _diffEma5.Dispose();
        _absDiffEma5.Dispose();
        _diffEma6.Dispose();
        _absDiffEma6.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class FallingRisingFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly RollingWindowMax _tempMax;
    private readonly RollingWindowMin _tempMin;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevA;
    private double _prevError;
    private bool _hasPrev;

    public FallingRisingFilterState(int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _alpha = (double)2 / (resolved + 1);
        _tempMax = new RollingWindowMax(resolved);
        _tempMin = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FallingRisingFilterState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _alpha = (double)2 / (resolved + 1);
        _tempMax = new RollingWindowMax(resolved);
        _tempMin = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FallingRisingFilter;

    public void Reset()
    {
        _tempMax.Reset();
        _tempMin.Reset();
        _prevValue = 0;
        _prevA = 0;
        _prevError = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevA = _hasPrev ? _prevA : 0;
        var prevError = _hasPrev ? _prevError : 0;
        var maxPrev = isFinal ? _tempMax.Add(prevValue, out _) : _tempMax.Preview(prevValue, out _);
        var minPrev = isFinal ? _tempMin.Add(prevValue, out _) : _tempMin.Preview(prevValue, out _);
        var beta = value > maxPrev || value < minPrev ? 1 : _alpha;
        var a = prevA + (_alpha * prevError) + (beta * prevError);
        var error = value - a;

        if (isFinal)
        {
            _prevValue = value;
            _prevA = a;
            _prevError = error;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Frf", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _tempMax.Dispose();
        _tempMin.Dispose();
    }
}

public sealed class FareySequenceWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public FareySequenceWeightedMovingAverageState(int length = 5, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var array = new double[4] { 0, 1, 1, resolved };
        List<double> resList = new();

        while (array[2] <= resolved)
        {
            var a = array[0];
            var b = array[1];
            var c = array[2];
            var d = array[3];
            var k = Math.Floor((resolved + b) / array[3]);

            array[0] = c;
            array[1] = d;
            array[2] = (k * c) - a;
            array[3] = (k * d) - b;

            var res = array[1] != 0 ? Math.Round(array[0] / array[1], 3) : 0;
            resList.Insert(0, res);
        }

        _weights = resList.ToArray();
        double weightSum = 0;
        for (var i = 0; i < _weights.Length; i++)
        {
            weightSum += _weights[i];
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(_weights.Length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FareySequenceWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var array = new double[4] { 0, 1, 1, resolved };
        List<double> resList = new();

        while (array[2] <= resolved)
        {
            var a = array[0];
            var b = array[1];
            var c = array[2];
            var d = array[3];
            var k = Math.Floor((resolved + b) / array[3]);

            array[0] = c;
            array[1] = d;
            array[2] = (k * c) - a;
            array[3] = (k * d) - b;

            var res = array[1] != 0 ? Math.Round(array[0] / array[1], 3) : 0;
            resList.Insert(0, res);
        }

        _weights = resList.ToArray();
        double weightSum = 0;
        for (var i = 0; i < _weights.Length; i++)
        {
            weightSum += _weights[i];
        }

        _weightSum = weightSum;
        _values = new PooledRingBuffer<double>(_weights.Length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FareySequenceWeightedMovingAverage;

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

        var fswma = _weightSum != 0 ? sum / _weightSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fswma", fswma }
            };
        }

        return new StreamingIndicatorStateResult(fswma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class FastandSlowKurtosisOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _ratio;
    private readonly PooledRingBuffer<double> _values;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevMomentum;
    private double _prevFsk;

    public FastandSlowKurtosisOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 3, double ratio = 0.03, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _ratio = ratio;
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public FastandSlowKurtosisOscillatorState(MovingAvgType maType, int length, double ratio,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _ratio = ratio;
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.FastandSlowKurtosisOscillator;

    public void Reset()
    {
        _values.Clear();
        _signalSmoother.Reset();
        _prevMomentum = 0;
        _prevFsk = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hasMomentum = _values.Count >= _length;
        var prevValue = hasMomentum ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var momentum = hasMomentum ? value - prevValue : 0;
        var fsk = (_ratio * (momentum - _prevMomentum)) + ((1 - _ratio) * _prevFsk);
        var signal = _signalSmoother.Next(fsk, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevMomentum = momentum;
            _prevFsk = fsk;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fsk", fsk },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fsk, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _signalSmoother.Dispose();
    }
}
