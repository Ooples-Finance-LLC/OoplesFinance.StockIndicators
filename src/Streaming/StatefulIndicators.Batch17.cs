using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class MarketFacilitationIndexState : IStreamingIndicatorState
{
    public IndicatorName Name => IndicatorName.MarketFacilitationIndex;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var mfi = bar.Volume != 0 ? (bar.High - bar.Low) / bar.Volume : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mi", mfi }
            };
        }

        return new StreamingIndicatorStateResult(mfi, outputs);
    }
}

public sealed class MarketMeannessIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _medianScratch;
    private readonly IMovingAverageSmoother? _mmiSmoother;
    private readonly NoiseEliminationTechnologyEngine? _mmiNet;
    private readonly StreamingInputResolver _input;

    public MarketMeannessIndexState(MovingAvgType maType = MovingAvgType.EhlersNoiseEliminationTechnology,
        int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        if (maType == MovingAvgType.EhlersNoiseEliminationTechnology)
        {
            _mmiNet = new NoiseEliminationTechnologyEngine(_length);
        }
        else
        {
            _mmiSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        }

        _values = new PooledRingBuffer<double>(_length);
        _medianScratch = new double[_length];
        _input = new StreamingInputResolver(inputName, null);
    }

    public MarketMeannessIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        if (maType == MovingAvgType.EhlersNoiseEliminationTechnology)
        {
            _mmiNet = new NoiseEliminationTechnologyEngine(_length);
        }
        else
        {
            _mmiSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        }

        _values = new PooledRingBuffer<double>(_length);
        _medianScratch = new double[_length];
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MarketMeannessIndex;

    public void Reset()
    {
        _values.Clear();
        _mmiSmoother?.Reset();
        _mmiNet?.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var median = EhlersStreamingWindow.GetMedian(_values, value, _medianScratch);
        int nl = 0;
        int nh = 0;
        for (var j = 1; j < _length; j++)
        {
            var value1 = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            var value2 = EhlersStreamingWindow.GetOffsetValue(_values, value, j);

            if (value1 > median && value1 > value2)
            {
                nl++;
            }
            else if (value1 < median && value1 < value2)
            {
                nh++;
            }
        }

        var mmi = _length != 1 ? 100d * (nl + nh) / (_length - 1) : 0;
        var mmiSmoothed = _mmiNet != null
            ? _mmiNet.Next(mmi, isFinal)
            : _mmiSmoother!.Next(mmi, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mmi", mmi },
                { "MmiSmoothed", mmiSmoothed }
            };
        }

        return new StreamingIndicatorStateResult(mmi, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _mmiSmoother?.Dispose();
        _mmiNet?.Dispose();
    }
}

public sealed class MartinRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly IMovingAverageSmoother _retSmoother;
    private readonly UlcerIndexState _ulcerIndex;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _retValue;

    public MartinRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.02, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var barMin = 60d * 24;
        var minPerYr = 60d * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = MathHelper.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _ulcerIndex = new UlcerIndexState(_length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MartinRatioState(MovingAvgType maType, int length, double bmk, Func<OhlcvBar, double> selector)
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
        _ulcerIndex = new UlcerIndexState(_length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MartinRatio;

    public void Reset()
    {
        _values.Clear();
        _retSmoother.Reset();
        _ulcerIndex.Reset();
        _retValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var ret = priorValue != 0 ? (100 * (value / priorValue)) - 1 - (_bench * 100) : 0;
        _retValue = ret;
        var retSma = _retSmoother.Next(ret, isFinal);
        var ulcer = _ulcerIndex.Update(bar, isFinal, includeOutputs: false).Value;
        var martin = ulcer != 0 ? retSma / ulcer : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mr", martin }
            };
        }

        return new StreamingIndicatorStateResult(martin, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _retSmoother.Dispose();
        _ulcerIndex.Dispose();
    }
}

public sealed class MassIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly RollingWindowSum _ratioSum;
    private readonly IMovingAverageSmoother _signalSmoother;

    public MassIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 21, int length2 = 21, int length3 = 25, int signalLength = 9)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ratioSum = new RollingWindowSum(Math.Max(1, length3));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.MassIndex;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ratioSum.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var highLow = bar.High - bar.Low;
        var ema1 = _ema1.Next(highLow, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var ratio = ema2 != 0 ? ema1 / ema2 : 0;
        var massIndex = isFinal ? _ratioSum.Add(ratio, out _) : _ratioSum.Preview(ratio, out _);
        var signal = _signalSmoother.Next(massIndex, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mi", massIndex },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(massIndex, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _ratioSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MassThrustIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _decSum;
    private readonly RollingWindowSum _advVolSum;
    private readonly RollingWindowSum _decVolSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public MassThrustIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MassThrustIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MassThrustIndicator;

    public void Reset()
    {
        _advSum.Reset();
        _decSum.Reset();
        _advVolSum.Reset();
        _decVolSum.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var adv = _hasPrev && value > prevValue ? value - prevValue : 0;
        var dec = _hasPrev && value < prevValue ? prevValue - value : 0;

        var advSum = isFinal ? _advSum.Add(adv, out _) : _advSum.Preview(adv, out _);
        var decSum = isFinal ? _decSum.Add(dec, out _) : _decSum.Preview(dec, out _);

        var advVol = _hasPrev && value > prevValue && advSum != 0 ? bar.Volume / advSum : 0;
        var decVol = _hasPrev && value < prevValue && decSum != 0 ? bar.Volume / decSum : 0;

        var advVolSum = isFinal ? _advVolSum.Add(advVol, out _) : _advVolSum.Preview(advVol, out _);
        var decVolSum = isFinal ? _decVolSum.Add(decVol, out _) : _decVolSum.Preview(decVol, out _);

        var mti = ((advSum * advVolSum) - (decSum * decVolSum)) / 1000000d;
        var signal = _signalSmoother.Next(mti, isFinal);

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
                { "Mti", mti },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mti, outputs);
    }

    public void Dispose()
    {
        _advSum.Dispose();
        _decSum.Dispose();
        _advVolSum.Dispose();
        _decVolSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MassThrustOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _decSum;
    private readonly RollingWindowSum _advVolSum;
    private readonly RollingWindowSum _decVolSum;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public MassThrustOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MassThrustOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _advSum = new RollingWindowSum(resolved);
        _decSum = new RollingWindowSum(resolved);
        _advVolSum = new RollingWindowSum(resolved);
        _decVolSum = new RollingWindowSum(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MassThrustOscillator;

    public void Reset()
    {
        _advSum.Reset();
        _decSum.Reset();
        _advVolSum.Reset();
        _decVolSum.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var adv = _hasPrev && value > prevValue ? value - prevValue : 0;
        var dec = _hasPrev && value < prevValue ? prevValue - value : 0;

        var advSum = isFinal ? _advSum.Add(adv, out _) : _advSum.Preview(adv, out _);
        var decSum = isFinal ? _decSum.Add(dec, out _) : _decSum.Preview(dec, out _);

        var advVol = _hasPrev && value > prevValue && advSum != 0 ? bar.Volume / advSum : 0;
        var decVol = _hasPrev && value < prevValue && decSum != 0 ? bar.Volume / decSum : 0;

        var advVolSum = isFinal ? _advVolSum.Add(advVol, out _) : _advVolSum.Preview(advVol, out _);
        var decVolSum = isFinal ? _decVolSum.Add(decVol, out _) : _decVolSum.Preview(decVol, out _);

        var top = (advSum * advVolSum) - (decSum * decVolSum);
        var bot = (advSum * advVolSum) + (decSum * decVolSum);
        var mto = bot != 0 ? 100d * top / bot : 0;
        var signal = _signalSmoother.Next(mto, isFinal);

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
                { "Mto", mto },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mto, outputs);
    }

    public void Dispose()
    {
        _advSum.Dispose();
        _decSum.Dispose();
        _advVolSum.Dispose();
        _decVolSum.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MayerMultipleState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public MayerMultipleState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 200, double threshold = 2.4, InputName inputName = InputName.Close)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
        _ = threshold;
    }

    public MayerMultipleState(MovingAvgType maType, int length, double threshold, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _ = threshold;
    }

    public IndicatorName Name => IndicatorName.MayerMultiple;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var mm = sma != 0 ? value / sma : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mm", mm }
            };
        }

        return new StreamingIndicatorStateResult(mm, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

public sealed class McClellanOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _advSum;
    private readonly RollingWindowSum _decSum;
    private readonly MacdEngine _macd;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;
    private double _prevValue;
    private bool _hasPrev;

    public McClellanOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 19, int slowLength = 39, int signalLength = 9, double mult = 1000,
        InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        _advSum = new RollingWindowSum(resolvedFast);
        _decSum = new RollingWindowSum(resolvedFast);
        _macd = new MacdEngine(maType, resolvedFast, Math.Max(1, slowLength), Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
        _mult = mult;
    }

    public McClellanOscillatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        _advSum = new RollingWindowSum(resolvedFast);
        _decSum = new RollingWindowSum(resolvedFast);
        _macd = new MacdEngine(maType, resolvedFast, Math.Max(1, slowLength), Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _mult = mult;
    }

    public IndicatorName Name => IndicatorName.McClellanOscillator;

    public void Reset()
    {
        _advSum.Reset();
        _decSum.Reset();
        _macd.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var advance = _hasPrev && value > prevValue ? 1d : 0d;
        var decline = _hasPrev && value < prevValue ? 1d : 0d;

        var advanceSum = isFinal ? _advSum.Add(advance, out _) : _advSum.Preview(advance, out _);
        var declineSum = isFinal ? _decSum.Add(decline, out _) : _decSum.Preview(decline, out _);
        var rana = advanceSum + declineSum != 0 ? _mult * (advanceSum - declineSum) / (advanceSum + declineSum) : 0;

        var macd = _macd.Next(rana, isFinal, out var signal, out var histogram);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "AdvSum", advanceSum },
                { "DecSum", declineSum },
                { "Mo", macd },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _advSum.Dispose();
        _decSum.Dispose();
        _macd.Dispose();
    }
}

public sealed class McGinleyDynamicIndicatorState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _k;
    private readonly StreamingInputResolver _input;
    private double _prevMdi;
    private bool _hasPrev;

    public McGinleyDynamicIndicatorState(int length = 14, double k = 0.6, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _k = k;
        _input = new StreamingInputResolver(inputName, null);
    }

    public McGinleyDynamicIndicatorState(int length, double k, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _k = k;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.McGinleyDynamicIndicator;

    public void Reset()
    {
        _prevMdi = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevMdi = _hasPrev ? _prevMdi : value;
        var ratio = prevMdi != 0 ? value / prevMdi : 0;
        var bottom = _k * _length * MathHelper.Pow(ratio, 4);
        var mdi = bottom != 0 ? prevMdi + ((value - prevMdi) / Math.Max(bottom, 1)) : value;

        if (isFinal)
        {
            _prevMdi = mdi;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mdi", mdi }
            };
        }

        return new StreamingIndicatorStateResult(mdi, outputs);
    }
}

public sealed class McNichollMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;

    public McNichollMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 20, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _alpha = 2d / (resolved + 1);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public McNichollMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _alpha = 2d / (resolved + 1);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.McNichollMovingAverage;

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
        var denom = 1 - _alpha;
        var mnma = denom != 0 ? (((2 - _alpha) * ema1) - ema2) / denom : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mnma", mnma }
            };
        }

        return new StreamingIndicatorStateResult(mnma, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

public sealed class MiddleHighLowMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly MidpointState _midpoint;

    public MiddleHighLowMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 14, int length2 = 10, InputName inputName = InputName.Close)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _midpoint = new MidpointState(Math.Max(1, length2), inputName);
    }

    public MiddleHighLowMovingAverageState(MovingAvgType maType, int length1, int length2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _midpoint = new MidpointState(Math.Max(1, length2), selector);
    }

    public IndicatorName Name => IndicatorName.MiddleHighLowMovingAverage;

    public void Reset()
    {
        _smoother.Reset();
        _midpoint.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var midpoint = _midpoint.Update(bar, isFinal, includeOutputs: false).Value;
        var mhlma = _smoother.Next(midpoint, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mhlma", mhlma }
            };
        }

        return new StreamingIndicatorStateResult(mhlma, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _midpoint.Dispose();
    }
}

public sealed class MidpointOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MidpointOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 26, int signalLength = 9, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public MidpointOscillatorState(MovingAvgType maType, int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MidpointOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var range = highest - lowest;
        var mo = range != 0
            ? MathHelper.MinOrMax(100 * ((2 * value) - highest - lowest) / range, 100, -100)
            : 0;
        var signal = _signalSmoother.Next(mo, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", mo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mo, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MirroredMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _openMa;
    private readonly IMovingAverageSmoother _closeMa;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly IMovingAverageSmoother _mirrorSignalSmoother;

    public MirroredMovingAverageConvergenceDivergenceState(MovingAvgType maType =
        MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _openMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _closeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _mirrorSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.MirroredMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _openMa.Reset();
        _closeMa.Reset();
        _signalSmoother.Reset();
        _mirrorSignalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var emaOpen = _openMa.Next(bar.Open, isFinal);
        var emaClose = _closeMa.Next(bar.Close, isFinal);
        var macd = emaClose - emaOpen;
        var mirrorMacd = emaOpen - emaClose;
        var signal = _signalSmoother.Next(macd, isFinal);
        var mirrorSignal = _mirrorSignalSmoother.Next(mirrorMacd, isFinal);
        var histogram = macd - signal;
        var mirrorHistogram = mirrorMacd - mirrorSignal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Macd", macd },
                { "Signal", signal },
                { "Histogram", histogram },
                { "MirrorMacd", mirrorMacd },
                { "MirrorSignal", mirrorSignal },
                { "MirrorHistogram", mirrorHistogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _openMa.Dispose();
        _closeMa.Dispose();
        _signalSmoother.Dispose();
        _mirrorSignalSmoother.Dispose();
    }
}

public sealed class MirroredPercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _openMa;
    private readonly IMovingAverageSmoother _closeMa;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly IMovingAverageSmoother _mirrorSignalSmoother;

    public MirroredPercentagePriceOscillatorState(MovingAvgType maType =
        MovingAvgType.ExponentialMovingAverage, int length = 20, int signalLength = 9)
    {
        var resolved = Math.Max(1, length);
        _openMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _closeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _mirrorSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.MirroredPercentagePriceOscillator;

    public void Reset()
    {
        _openMa.Reset();
        _closeMa.Reset();
        _signalSmoother.Reset();
        _mirrorSignalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var emaOpen = _openMa.Next(bar.Open, isFinal);
        var emaClose = _closeMa.Next(bar.Close, isFinal);
        var macd = emaClose - emaOpen;
        var mirrorMacd = emaOpen - emaClose;
        var ppo = emaOpen != 0 ? macd / emaOpen * 100 : 0;
        var mirrorPpo = emaClose != 0 ? mirrorMacd / emaClose * 100 : 0;
        var signal = _signalSmoother.Next(ppo, isFinal);
        var mirrorSignal = _mirrorSignalSmoother.Next(mirrorPpo, isFinal);
        var histogram = ppo - signal;
        var mirrorHistogram = mirrorPpo - mirrorSignal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Ppo", ppo },
                { "Signal", signal },
                { "Histogram", histogram },
                { "MirrorPpo", mirrorPpo },
                { "MirrorSignal", mirrorSignal },
                { "MirrorHistogram", mirrorHistogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo, outputs);
    }

    public void Dispose()
    {
        _openMa.Dispose();
        _closeMa.Dispose();
        _signalSmoother.Dispose();
        _mirrorSignalSmoother.Dispose();
    }
}

public sealed class MobilityOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _moSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public MobilityOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 10, int length2 = 14, int signalLength = 7, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _highWindow = new RollingWindowMax(_length2);
        _lowWindow = new RollingWindowMin(_length2);
        _moSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _highValues = new PooledRingBuffer<double>(_length1 + _length2 + 1);
        _lowValues = new PooledRingBuffer<double>(_length1 + _length2 + 1);
        _values = new PooledRingBuffer<double>(_length2 + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MobilityOscillatorState(MovingAvgType maType, int length1, int length2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _highWindow = new RollingWindowMax(_length2);
        _lowWindow = new RollingWindowMin(_length2);
        _moSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _highValues = new PooledRingBuffer<double>(_length1 + _length2 + 1);
        _lowValues = new PooledRingBuffer<double>(_length1 + _length2 + 1);
        _values = new PooledRingBuffer<double>(_length2 + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MobilityOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _moSmoother.Reset();
        _signalSmoother.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var hMax = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lMin = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var prevC = EhlersStreamingWindow.GetOffsetValue(_values, value, _length2);
        var rx = _length1 != 0 ? (hMax - lMin) / _length1 : 0;

        var imx = 1;
        double pdfmx = 0;
        double pdfc = 0;
        for (var j = 1; j <= _length1; j++)
        {
            var bu = lMin + (j * rx);
            var bl = bu - rx;

            var currHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, high, j);
            var currLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, j);
            double hMax1 = currHigh;
            double lMin1 = currLow;
            for (var k = 2; k < _length2; k++)
            {
                var highValue = EhlersStreamingWindow.GetOffsetValue(_highValues, high, j + k);
                var lowValue = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, j + k);
                hMax1 = Math.Max(highValue, hMax1);
                lMin1 = Math.Min(lowValue, lMin1);
            }

            var rx1 = _length1 != 0 ? (hMax1 - lMin1) / _length1 : 0;
            var bl1 = lMin1 + ((j - 1) * rx1);
            var bu1 = lMin1 + (j * rx1);

            double pdf = 0;
            for (var k = 1; k <= _length2; k++)
            {
                var highValue = EhlersStreamingWindow.GetOffsetValue(_highValues, high, j + k);
                var lowValue = EhlersStreamingWindow.GetOffsetValue(_lowValues, low, j + k);

                if (highValue <= bu1)
                {
                    pdf += 1;
                }
                if (highValue <= bu1 || lowValue >= bu1)
                {
                    if (highValue <= bl1)
                    {
                        pdf -= 1;
                    }
                    if (highValue <= bl || lowValue >= bl1)
                    {
                        continue;
                    }
                    else
                    {
                        pdf -= highValue - lowValue != 0 ? (bl1 - lowValue) / (highValue - lowValue) : 0;
                    }
                }
                else
                {
                    pdf += highValue - lowValue != 0 ? (bu1 - lowValue) / (highValue - lowValue) : 0;
                }
            }

            pdf = _length2 != 0 ? pdf / _length2 : 0;
            if (j == 1)
            {
                pdfmx = pdf;
                pdfc = pdf;
                imx = j;
            }

            pdfmx = Math.Max(pdf, pdfmx);
            if (prevC > bl && prevC <= bu)
            {
                pdfc = pdf;
            }
        }

        var pmo = lMin + ((imx - 0.5) * rx);
        var mo = pdfmx != 0 ? 100 * (1 - (pdfc / pdfmx)) : 0;
        mo = prevC < pmo ? -mo : mo;
        var moValue = -mo;

        var moSmoothed = _moSmoother.Next(moValue, isFinal);
        var signal = _signalSmoother.Next(moSmoothed, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _highValues.TryAdd(high, out _);
            _lowValues.TryAdd(low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", moSmoothed },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(moSmoothed, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _moSmoother.Dispose();
        _signalSmoother.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
        _values.Dispose();
    }
}

public sealed class ModifiedGannHiloActivatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _cSmoother;
    private readonly IMovingAverageSmoother _dSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;
    private double _prevG;
    private bool _hasPrev;

    public ModifiedGannHiloActivatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, double mult = 1, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _cSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _dSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _mult = mult;
    }

    public ModifiedGannHiloActivatorState(MovingAvgType maType, int length, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _cSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _dSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _mult = mult;
    }

    public IndicatorName Name => IndicatorName.ModifiedGannHiloActivator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _cSmoother.Reset();
        _dSmoother.Reset();
        _prevG = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var open = bar.Open;
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        var max = Math.Max(close, open);
        var min = Math.Min(close, open);
        var a = highestHigh - max;
        var b = min - lowestLow;
        var c = max + (a * _mult);
        var d = min - (b * _mult);
        var e = _cSmoother.Next(c, isFinal);
        var f = _dSmoother.Next(d, isFinal);

        var prevG = _hasPrev ? _prevG : 0;
        var g = close > e ? 1 : close > f ? 0 : prevG;
        var gannHilo = (g * f) + ((1 - g) * e);

        if (isFinal)
        {
            _prevG = g;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ghla", gannHilo }
            };
        }

        return new StreamingIndicatorStateResult(gannHilo, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _cSmoother.Dispose();
        _dSmoother.Dispose();
    }
}

public sealed class ModifiedPriceVolumeTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevMpvt;
    private bool _hasPrev;

    public ModifiedPriceVolumeTrendState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 23, InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public ModifiedPriceVolumeTrendState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ModifiedPriceVolumeTrend;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevValue = 0;
        _prevMpvt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var rv = bar.Volume / 50000d;
        var diff = _hasPrev ? value - prevValue : 0;
        var mpvt = prevValue != 0 ? _prevMpvt + (rv * diff / prevValue) : 0;
        var signal = _signalSmoother.Next(mpvt, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevMpvt = mpvt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mpvt", mpvt },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mpvt, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class ModularFilterState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _beta;
    private readonly StreamingInputResolver _input;
    private double _prevB2;
    private double _prevC2;
    private double _prevOs2;
    private bool _hasPrev;

    public ModularFilterState(int length = 200, double beta = 0.8, double z = 0.5,
        InputName inputName = InputName.Close)
    {
        _alpha = 2d / (Math.Max(1, length) + 1);
        _beta = beta;
        _input = new StreamingInputResolver(inputName, null);
        _ = z;
    }

    public ModularFilterState(int length, double beta, double z, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _alpha = 2d / (Math.Max(1, length) + 1);
        _beta = beta;
        _input = new StreamingInputResolver(InputName.Close, selector);
        _ = z;
    }

    public IndicatorName Name => IndicatorName.ModularFilter;

    public void Reset()
    {
        _prevB2 = 0;
        _prevC2 = 0;
        _prevOs2 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevB2 = _hasPrev ? _prevB2 : value;
        var prevC2 = _hasPrev ? _prevC2 : value;
        var b2Base = (_alpha * value) + ((1 - _alpha) * prevB2);
        var c2Base = (_alpha * value) + ((1 - _alpha) * prevC2);
        var b2 = value > b2Base ? value : b2Base;
        var c2 = value < c2Base ? value : c2Base;
        var prevOs2 = _hasPrev ? _prevOs2 : 0;
        var os2 = value == b2 ? 1 : value == c2 ? 0 : prevOs2;
        var upper2 = (_beta * b2) + ((1 - _beta) * c2);
        var lower2 = (_beta * c2) + ((1 - _beta) * b2);
        var ts2 = (os2 * upper2) + ((1 - os2) * lower2);

        if (isFinal)
        {
            _prevB2 = b2;
            _prevC2 = c2;
            _prevOs2 = os2;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mf", ts2 }
            };
        }

        return new StreamingIndicatorStateResult(ts2, outputs);
    }
}

public sealed class MomentaRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _topSmoother;
    private readonly IMovingAverageSmoother _botSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MomentaRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 14, InputName inputName = InputName.Close)
    {
        _maxWindow = new RollingWindowMax(Math.Max(1, length1));
        _minWindow = new RollingWindowMin(Math.Max(1, length1));
        _topSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public MomentaRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _maxWindow = new RollingWindowMax(Math.Max(1, length1));
        _minWindow = new RollingWindowMin(Math.Max(1, length1));
        _topSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MomentaRelativeStrengthIndex;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _topSmoother.Reset();
        _botSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var srcLc = value - lowest;
        var hcSrc = highest - value;
        var top = _topSmoother.Next(srcLc, isFinal);
        var bot = _botSmoother.Next(hcSrc, isFinal);
        var rs = bot != 0 ? MathHelper.MinOrMax(top / bot, 1, 0) : 0;
        var rsi = bot == 0 ? 100 : top == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        var signal = _signalSmoother.Next(rsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mrsi", rsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _topSmoother.Dispose();
        _botSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public MomentumOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MomentumOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MomentumOscillator;

    public void Reset()
    {
        _values.Clear();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var mo = prevValue != 0 ? value / prevValue * 100 : 0;
        var signal = _signalSmoother.Next(mo, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mo", mo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mo, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MorphedSineWaveState : IStreamingIndicatorState
{
    private readonly double _power;
    private readonly double _p;
    private readonly StreamingInputResolver _input;
    private int _index;

    public MorphedSineWaveState(int length = 14, double power = 100, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _power = power;
        _p = resolved / (2 * Math.PI);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MorphedSineWaveState(int length, double power, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _power = power;
        _p = resolved / (2 * Math.PI);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MorphedSineWave;

    public void Reset()
    {
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var s = ((value * _power) + Math.Sin(_index / _p)) / _power;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Msw", s }
            };
        }

        return new StreamingIndicatorStateResult(s, outputs);
    }
}

public sealed class MotionSmoothnessIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StandardDeviationVolatilityState _chgStdDev;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _chgValue;
    private bool _hasPrev;

    public MotionSmoothnessIndexState(int length = 50, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, inputName);
        _chgStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _chgValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MotionSmoothnessIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, selector);
        _chgStdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _chgValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MotionSmoothnessIndex;

    public void Reset()
    {
        _stdDev.Reset();
        _chgStdDev.Reset();
        _prevValue = 0;
        _chgValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevValue = _hasPrev ? _prevValue : 0;
        var chg = _hasPrev ? value - prevValue : 0;
        _chgValue = chg;
        var chgStdDev = _chgStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var msi = stdDev != 0 ? chgStdDev / stdDev : 0;

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
                { "Msi", msi }
            };
        }

        return new StreamingIndicatorStateResult(msi, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _chgStdDev.Dispose();
    }
}

public sealed class MotionToAttractionTrailingStopState : IStreamingIndicatorState
{
    private readonly MotionToAttractionChannelsState _channels;
    private readonly StreamingInputResolver _input;
    private double _prevUpper;
    private double _prevLower;
    private double _prevOs;
    private bool _hasPrev;

    public MotionToAttractionTrailingStopState(int length = 14, InputName inputName = InputName.Close)
    {
        _channels = new MotionToAttractionChannelsState(length, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MotionToAttractionTrailingStopState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _channels = new MotionToAttractionChannelsState(length, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MotionToAttractionTrailingStop;

    public void Reset()
    {
        _channels.Reset();
        _prevUpper = 0;
        _prevLower = 0;
        _prevOs = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var channelResult = _channels.Update(bar, isFinal, includeOutputs: true);
        var channelOutputs = channelResult.Outputs!;
        var upper = channelOutputs["UpperBand"];
        var lower = channelOutputs["LowerBand"];
        var prevUpper = _hasPrev ? _prevUpper : value;
        var prevLower = _hasPrev ? _prevLower : value;
        var prevOs = _hasPrev ? _prevOs : 0;
        var os = value > prevUpper ? 1 : value < prevLower ? 0 : prevOs;
        var ts = (os * lower) + ((1 - os) * upper);

        if (isFinal)
        {
            _prevUpper = upper;
            _prevLower = lower;
            _prevOs = os;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ts", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }
}

public sealed class MoveTrackerState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevMt;
    private bool _hasPrev;

    public MoveTrackerState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public MoveTrackerState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MoveTracker;

    public void Reset()
    {
        _prevValue = 0;
        _prevMt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevMt = _hasPrev ? _prevMt : 0;
        var mt = _hasPrev ? value - prevValue : 0;
        var mtSignal = mt - prevMt;

        if (isFinal)
        {
            _prevValue = value;
            _prevMt = mt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mt", mt },
                { "Signal", mtSignal }
            };
        }

        return new StreamingIndicatorStateResult(mt, outputs);
    }
}

public sealed class MovingAverageAdaptiveFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _filter;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private double _prevAma;
    private double _amaDiff;
    private bool _hasPrev;

    public MovingAverageAdaptiveFilterState(int length = 10, double filter = 0.15,
        double fastAlpha = 0.667, double slowAlpha = 0.0645, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _amaDiff);
        _input = new StreamingInputResolver(inputName, null);
        _filter = filter;
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
    }

    public MovingAverageAdaptiveFilterState(int length, double filter, double fastAlpha, double slowAlpha,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _amaDiff);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _filter = filter;
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
    }

    public IndicatorName Name => IndicatorName.MovingAverageAdaptiveFilter;

    public void Reset()
    {
        _er.Reset();
        _stdDev.Reset();
        _prevAma = 0;
        _amaDiff = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevAma = _hasPrev ? _prevAma : value;
        var er = _er.Next(value, isFinal);
        var smooth = MathHelper.Pow((er * (_fastAlpha - _slowAlpha)) + _slowAlpha, 2);
        var ama = prevAma + (smooth * (value - prevAma));
        _amaDiff = ama - prevAma;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var maaf = stdDev * _filter;

        if (isFinal)
        {
            _prevAma = ama;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Maaf", maaf }
            };
        }

        return new StreamingIndicatorStateResult(maaf, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
        _stdDev.Dispose();
    }
}

internal sealed class MacdEngine : IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;

    public MacdEngine(MovingAvgType maType, int fastLength, int slowLength, int signalLength)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public double Next(double value, bool isFinal, out double signal, out double histogram)
    {
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var macd = fast - slow;
        signal = _signalSmoother.Next(macd, isFinal);
        histogram = macd - signal;
        return macd;
    }

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

internal sealed class NoiseEliminationTechnologyEngine : IDisposable
{
    private readonly int _length;
    private readonly double _denom;
    private readonly PooledRingBuffer<double> _values;
    private readonly double[] _scratch;

    public NoiseEliminationTechnologyEngine(int length)
    {
        _length = Math.Max(1, length);
        _denom = 0.5 * _length * (_length - 1);
        _values = new PooledRingBuffer<double>(_length);
        _scratch = new double[_length + 1];
    }

    public double Next(double value, bool isFinal)
    {
        for (var j = 1; j <= _length; j++)
        {
            _scratch[j] = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
        }

        double num = 0;
        for (var j = 2; j <= _length; j++)
        {
            var xj = _scratch[j];
            for (var k = 1; k <= j - 1; k++)
            {
                num -= Math.Sign(xj - _scratch[k]);
            }
        }

        var net = _denom != 0 ? num / _denom : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        return net;
    }

    public void Reset()
    {
        _values.Clear();
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}
