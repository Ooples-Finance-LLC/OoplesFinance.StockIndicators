using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

internal readonly struct StreamingInputResolver
{
    private readonly InputName _inputName;
    private readonly Func<OhlcvBar, double>? _selector;

    public StreamingInputResolver(InputName inputName, Func<OhlcvBar, double>? selector)
    {
        _inputName = inputName;
        _selector = selector;
    }

    public double GetValue(OhlcvBar bar)
    {
        return _selector != null ? _selector(bar) : StreamingInputSelector.GetValue(bar, _inputName);
    }
}

public sealed class SimpleMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _window;
    private readonly StreamingInputResolver _input;

    public SimpleMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimpleMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimpleMovingAverage;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int countAfter;
        var sum = isFinal ? _window.Add(value, out countAfter) : _window.Preview(value, out countAfter);
        var sma = countAfter >= _length ? sum / _length : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sma", sma }
            };
        }

        return new StreamingIndicatorStateResult(sma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class ExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly EmaState _ema;
    private readonly StreamingInputResolver _input;

    public ExponentialMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _ema = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ExponentialMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ExponentialMovingAverage;

    public void Reset()
    {
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var ema = _ema.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ema", ema }
            };
        }

        return new StreamingIndicatorStateResult(ema, outputs);
    }
}

public sealed class RelativeStrengthIndexState : IStreamingIndicatorState
{
    private readonly WilderState _avgGain;
    private readonly WilderState _avgLoss;
    private readonly WilderState _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public RelativeStrengthIndexState(int length = 14, int signalLength = 3, InputName inputName = InputName.Close)
    {
        _avgGain = new WilderState(length);
        _avgLoss = new WilderState(length);
        _signal = new WilderState(signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RelativeStrengthIndexState(int length, int signalLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _avgGain = new WilderState(length);
        _avgLoss = new WilderState(length);
        _signal = new WilderState(signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RelativeStrengthIndex;

    public void Reset()
    {
        _avgGain.Reset();
        _avgLoss.Reset();
        _signal.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var priceChg = _hasPrev ? currentValue - prevClose : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.GetNext(gain, isFinal);
        var avgLoss = _avgLoss.GetNext(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        var signal = _signal.GetNext(rsi, isFinal);
        var histogram = rsi - signal;

        if (isFinal)
        {
            _prevClose = currentValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Rsi", rsi },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }
}

public sealed class StochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticOscillatorState(MovingAvgType maType, int length, int smoothLength1, int smoothLength2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticOscillator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((close - lowestLow) / range * 100, 100, 0) : 0;

        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "FastK", fastK },
                { "FastD", fastD },
                { "SlowD", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastK, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class WilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public WilliamsRState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WilliamsRState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WilliamsR;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var range = highestHigh - lowestLow;
        var williamsR = range != 0 ? -100 * (highestHigh - close) / range : -100;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Williams%R", williamsR }
            };
        }

        return new StreamingIndicatorStateResult(williamsR, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class CommodityChannelIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _priceSmoother;
    private readonly IMovingAverageSmoother _meanDevSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _constant;

    public CommodityChannelIndexState(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double constant = 0.015)
    {
        _priceSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _meanDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
        _constant = constant;
    }

    public CommodityChannelIndexState(InputName inputName, MovingAvgType maType, int length, double constant,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _priceSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _meanDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, selector);
        _constant = constant;
    }

    public IndicatorName Name => IndicatorName.CommodityChannelIndex;

    public void Reset()
    {
        _priceSmoother.Reset();
        _meanDevSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _priceSmoother.Next(value, isFinal);
        var meanDev = _meanDevSmoother.Next(Math.Abs(value - sma), isFinal);
        var cci = meanDev != 0 ? (value - sma) / (_constant * meanDev) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cci", cci }
            };
        }

        return new StreamingIndicatorStateResult(cci, outputs);
    }

    public void Dispose()
    {
        _priceSmoother.Dispose();
        _meanDevSmoother.Dispose();
    }
}

public sealed class StochasticRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RsiState _rsi;
    private readonly RollingWindowMax _inputHighWindow;
    private readonly RollingWindowMin _inputLowWindow;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticRelativeStrengthIndexState(MovingAvgType maType, int length, int smoothLength1, int smoothLength2,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _rsi = new RsiState(maType, _length);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _inputHighWindow.Reset();
        _inputLowWindow.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var inputHigh = isFinal ? _inputHighWindow.Add(rsi, out _) : _inputHighWindow.Preview(rsi, out _);
        var inputLow = isFinal ? _inputLowWindow.Add(rsi, out _) : _inputLowWindow.Preview(rsi, out _);
        var highest = isFinal ? _maxWindow.Add(inputHigh, out _) : _maxWindow.Preview(inputHigh, out _);
        var lowest = isFinal ? _minWindow.Add(inputLow, out _) : _minWindow.Preview(inputLow, out _);
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((rsi - lowest) / range * 100, 100, 0) : 0;

        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "StochRsi", fastD },
                { "Signal", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastD, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _inputHighWindow.Dispose();
        _inputLowWindow.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class ConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _rocLength;
    private readonly RsiState _rsi;
    private readonly RsiState _streakRsi;
    private readonly RollingPercentRank _rocRank;
    private readonly PooledRingBuffer<double> _rocWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _streak;
    private bool _hasPrev;

    public ConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100, InputName inputName = InputName.Close)
    {
        _rocLength = Math.Max(1, length3);
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _streakRsi = new RsiState(maType, Math.Max(1, length1));
        _rocRank = new RollingPercentRank(_rocLength);
        _rocWindow = new PooledRingBuffer<double>(_rocLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ConnorsRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _rocLength = Math.Max(1, length3);
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _streakRsi = new RsiState(maType, Math.Max(1, length1));
        _rocRank = new RollingPercentRank(_rocLength);
        _rocWindow = new PooledRingBuffer<double>(_rocLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _streakRsi.Reset();
        _rocRank.Reset();
        _rocWindow.Clear();
        _prevValue = 0;
        _streak = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var rsi = _rsi.Next(currentValue, isFinal);
        var rocPrev = _rocWindow.Count >= _rocLength ? _rocWindow[0] : 0;
        var roc = rocPrev != 0 ? (rsi - rocPrev) / rocPrev * 100 : 0;
        var pctRank = isFinal ? _rocRank.Add(roc) : _rocRank.Preview(roc);

        var prevValue = _hasPrev ? _prevValue : 0;
        var prevStreak = _streak;
        var streak = currentValue > prevValue
            ? prevStreak >= 0 ? prevStreak + 1 : 1
            : currentValue < prevValue
                ? prevStreak <= 0 ? prevStreak - 1 : -1
                : 0;
        var streakRsi = _streakRsi.Next(streak, isFinal);

        var connors = MathHelper.MinOrMax((rsi + pctRank + streakRsi) / 3, 100, 0);

        if (isFinal)
        {
            _prevValue = currentValue;
            _streak = streak;
            _hasPrev = true;
            _rocWindow.TryAdd(rsi, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Rsi", rsi },
                { "PctRank", pctRank },
                { "StreakRsi", streakRsi },
                { "ConnorsRsi", connors }
            };
        }

        return new StreamingIndicatorStateResult(connors, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _streakRsi.Dispose();
        _rocRank.Dispose();
        _rocWindow.Dispose();
    }
}

public sealed class StochasticConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly ConnorsRelativeStrengthIndexState _connors;
    private readonly RollingWindowMax _inputHighWindow;
    private readonly RollingWindowMin _inputLowWindow;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;

    public StochasticConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length2);
        _connors = new ConnorsRelativeStrengthIndexState(maType, length1, length2, length3, inputName);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
    }

    public StochasticConnorsRelativeStrengthIndexState(MovingAvgType maType, int length1, int length2, int length3,
        int smoothLength1, int smoothLength2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length2);
        _connors = new ConnorsRelativeStrengthIndexState(maType, length1, length2, length3, selector);
        _inputHighWindow = new RollingWindowMax(2);
        _inputLowWindow = new RollingWindowMin(2);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
    }

    public IndicatorName Name => IndicatorName.StochasticConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _connors.Reset();
        _inputHighWindow.Reset();
        _inputLowWindow.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var connors = _connors.Update(bar, isFinal, includeOutputs: false).Value;
        var inputHigh = isFinal ? _inputHighWindow.Add(connors, out _) : _inputHighWindow.Preview(connors, out _);
        var inputLow = isFinal ? _inputLowWindow.Add(connors, out _) : _inputLowWindow.Preview(connors, out _);
        var highest = isFinal ? _maxWindow.Add(inputHigh, out _) : _maxWindow.Preview(inputHigh, out _);
        var lowest = isFinal ? _minWindow.Add(inputLow, out _) : _minWindow.Preview(inputLow, out _);
        var range = highest - lowest;
        var fastK = range != 0 ? MathHelper.MinOrMax((connors - lowest) / range * 100, 100, 0) : 0;
        var fastD = _fastSmoother.Next(fastK, isFinal);
        var slowD = _slowSmoother.Next(fastD, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "SaRsi", fastD },
                { "Signal", slowD }
            };
        }

        return new StreamingIndicatorStateResult(fastD, outputs);
    }

    public void Dispose()
    {
        _connors.Dispose();
        _inputHighWindow.Dispose();
        _inputLowWindow.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class StochasticMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _diffSmoother1;
    private readonly IMovingAverageSmoother _diffSmoother2;
    private readonly IMovingAverageSmoother _rangeSmoother1;
    private readonly IMovingAverageSmoother _rangeSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticMomentumIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _diffSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _rangeSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _rangeSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public StochasticMomentumIndexState(MovingAvgType maType, int length1, int length2, int smoothLength1,
        int smoothLength2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length1);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _diffSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _rangeSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _rangeSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.StochasticMomentumIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _diffSmoother1.Reset();
        _diffSmoother2.Reset();
        _rangeSmoother1.Reset();
        _rangeSmoother2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var median = (highest + lowest) / 2;
        var diff = value - median;
        var range = highest - lowest;

        var diffEma = _diffSmoother1.Next(diff, isFinal);
        var rangeEma = _rangeSmoother1.Next(range, isFinal);
        var diffSmooth = _diffSmoother2.Next(diffEma, isFinal);
        var rangeSmooth = _rangeSmoother2.Next(rangeEma, isFinal);
        var halfRange = rangeSmooth / 2;
        var smi = halfRange != 0 ? MathHelper.MinOrMax(100 * diffSmooth / halfRange, 100, -100) : 0;
        var signal = _signalSmoother.Next(smi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Smi", smi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(smi, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _diffSmoother1.Dispose();
        _diffSmoother2.Dispose();
        _rangeSmoother1.Dispose();
        _rangeSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MovingAverageConvergenceDivergenceState : IStreamingIndicatorState
{
    private readonly EmaState _fast;
    private readonly EmaState _slow;
    private readonly EmaState _signal;
    private readonly StreamingInputResolver _input;

    public MovingAverageConvergenceDivergenceState(int fastLength = 12, int slowLength = 26, int signalLength = 9,
        InputName inputName = InputName.Close)
    {
        _fast = new EmaState(fastLength);
        _slow = new EmaState(slowLength);
        _signal = new EmaState(signalLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MovingAverageConvergenceDivergenceState(int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fast = new EmaState(fastLength);
        _slow = new EmaState(slowLength);
        _signal = new EmaState(signalLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MovingAverageConvergenceDivergence;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.GetNext(value, isFinal);
        var slow = _slow.GetNext(value, isFinal);
        var macd = fast - slow;
        var signal = _signal.GetNext(macd, isFinal);
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
}

public sealed class AverageTrueRangeState : IStreamingIndicatorState
{
    private readonly WilderState _atr;
    private double _prevClose;
    private bool _hasPrev;

    public AverageTrueRangeState(int length = 14)
    {
        _atr = new WilderState(length);
    }

    public IndicatorName Name => IndicatorName.AverageTrueRange;

    public void Reset()
    {
        _atr.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atr.GetNext(tr, isFinal);

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
                { "Atr", atr }
            };
        }

        return new StreamingIndicatorStateResult(atr, outputs);
    }
}

public sealed class AverageDirectionalIndexState : IStreamingIndicatorState
{
    private readonly WilderState _dmPlus;
    private readonly WilderState _dmMinus;
    private readonly WilderState _tr;
    private readonly WilderState _adx;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public AverageDirectionalIndexState(int length = 14)
    {
        _dmPlus = new WilderState(length);
        _dmMinus = new WilderState(length);
        _tr = new WilderState(length);
        _adx = new WilderState(length);
    }

    public IndicatorName Name => IndicatorName.AverageDirectionalIndex;

    public void Reset()
    {
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _adx.Reset();
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

        var dmPlus14 = _dmPlus.GetNext(dmPlus, isFinal);
        var dmMinus14 = _dmMinus.GetNext(dmMinus, isFinal);
        var tr14 = _tr.GetNext(tr, isFinal);

        var diPlus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmPlus14 / tr14, 100, 0) : 0;
        var diMinus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmMinus14 / tr14, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var dx = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.GetNext(dx, isFinal);

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
                { "DiPlus", diPlus },
                { "DiMinus", diMinus },
                { "Adx", adx }
            };
        }

        return new StreamingIndicatorStateResult(adx, outputs);
    }
}

public sealed class BollingerBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _stdDevMult;
    private readonly RollingWindowStats _window;
    private readonly StreamingInputResolver _input;

    public BollingerBandsState(int length = 20, double stdDevMult = 2, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _window = new RollingWindowStats(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public BollingerBandsState(int length, double stdDevMult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _window = new RollingWindowStats(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.BollingerBands;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var snapshot = isFinal ? _window.Add(value) : _window.Preview(value);
        var middle = snapshot.Count >= _length ? snapshot.Sum / _length : 0;
        var variance = snapshot.Count >= _length ? (snapshot.SumSquares / _length) - (middle * middle) : 0;
        var stdDev = MathHelper.Sqrt(variance);
        var upper = middle + (stdDev * _stdDevMult);
        var lower = middle - (stdDev * _stdDevMult);

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
        _window.Dispose();
    }
}

public sealed class OnBalanceVolumeState : IStreamingIndicatorState
{
    private readonly EmaState _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private double _obv;
    private bool _hasPrev;

    public OnBalanceVolumeState(int length = 20, InputName inputName = InputName.Close)
    {
        _signal = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public OnBalanceVolumeState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signal = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolume;

    public void Reset()
    {
        _signal.Reset();
        _prevClose = 0;
        _obv = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevObv = _obv;
        var obv = currentValue > prevClose ? prevObv + bar.Volume
            : currentValue < prevClose ? prevObv - bar.Volume
            : prevObv;

        var signal = _signal.GetNext(obv, isFinal);

        if (isFinal)
        {
            _prevClose = currentValue;
            _obv = obv;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Obv", obv },
                { "ObvSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(obv, outputs);
    }
}

public sealed class ChaikinMoneyFlowState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumeSum;
    private readonly RollingWindowSum _mfVolumeSum;

    public ChaikinMoneyFlowState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _volumeSum = new RollingWindowSum(resolved);
        _mfVolumeSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.ChaikinMoneyFlow;

    public void Reset()
    {
        _volumeSum.Reset();
        _mfVolumeSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var mfVolume = multiplier * volume;

        var volumeSum = isFinal ? _volumeSum.Add(volume, out _) : _volumeSum.Preview(volume, out _);
        var mfVolumeSum = isFinal ? _mfVolumeSum.Add(mfVolume, out _) : _mfVolumeSum.Preview(mfVolume, out _);
        var cmf = volumeSum != 0 ? mfVolumeSum / volumeSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cmf", cmf }
            };
        }

        return new StreamingIndicatorStateResult(cmf, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
        _mfVolumeSum.Dispose();
    }
}

public sealed class MoneyFlowIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _posSum;
    private readonly RollingWindowSum _negSum;
    private readonly StreamingInputResolver _input;
    private double _prevTypical;
    private bool _hasPrev;

    public MoneyFlowIndexState(int length = 14, InputName inputName = InputName.TypicalPrice)
    {
        var resolved = Math.Max(1, length);
        _posSum = new RollingWindowSum(resolved);
        _negSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public MoneyFlowIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _posSum = new RollingWindowSum(resolved);
        _negSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.TypicalPrice, selector);
    }

    public IndicatorName Name => IndicatorName.MoneyFlowIndex;

    public void Reset()
    {
        _posSum.Reset();
        _negSum.Reset();
        _prevTypical = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var typical = _input.GetValue(bar);
        var rawFlow = typical * bar.Volume;
        var posFlow = _hasPrev && typical > _prevTypical ? rawFlow : 0;
        var negFlow = _hasPrev && typical < _prevTypical ? rawFlow : 0;

        int _;
        var posSum = isFinal ? _posSum.Add(posFlow, out _) : _posSum.Preview(posFlow, out _);
        var negSum = isFinal ? _negSum.Add(negFlow, out _) : _negSum.Preview(negFlow, out _);

        var ratio = negSum != 0 ? posSum / negSum : 0;
        var mfi = negSum == 0 ? 100 : posSum == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + ratio)), 100, 0);

        if (isFinal)
        {
            _prevTypical = typical;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mfi", mfi }
            };
        }

        return new StreamingIndicatorStateResult(mfi, outputs);
    }

    public void Dispose()
    {
        _posSum.Dispose();
        _negSum.Dispose();
    }
}

public sealed class AccumulationDistributionLineState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private double _adl;

    public AccumulationDistributionLineState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.AccumulationDistributionLine;

    public void Reset()
    {
        _signalSmoother.Reset();
        _adl = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var moneyFlowVolume = multiplier * volume;

        var adl = _adl + moneyFlowVolume;
        var signal = _signalSmoother.Next(adl, isFinal);

        if (isFinal)
        {
            _adl = adl;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Adl", adl },
                { "AdlSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(adl, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class ChaikinOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private double _adl;

    public ChaikinOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 3, int slowLength = 10)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
    }

    public IndicatorName Name => IndicatorName.ChaikinOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _adl = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var high = bar.High;
        var low = bar.Low;
        var close = bar.Close;
        var volume = bar.Volume;
        var multiplier = high - low != 0
            ? (close - low - (high - close)) / (high - low)
            : 0;
        var moneyFlowVolume = multiplier * volume;
        var adl = _adl + moneyFlowVolume;

        var fast = _fastSmoother.Next(adl, isFinal);
        var slow = _slowSmoother.Next(adl, isFinal);
        var chaikinOsc = fast - slow;

        if (isFinal)
        {
            _adl = adl;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "ChaikinOsc", chaikinOsc }
            };
        }

        return new StreamingIndicatorStateResult(chaikinOsc, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class TrueStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _pcSmoother1;
    private readonly IMovingAverageSmoother _pcSmoother2;
    private readonly IMovingAverageSmoother _absSmoother1;
    private readonly IMovingAverageSmoother _absSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public TrueStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25,
        int length2 = 13, int signalLength = 7, InputName inputName = InputName.Close)
    {
        _pcSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _pcSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrueStrengthIndexState(MovingAvgType maType, int length1, int length2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _pcSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _pcSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.TrueStrengthIndex;

    public void Reset()
    {
        _pcSmoother1.Reset();
        _pcSmoother2.Reset();
        _absSmoother1.Reset();
        _absSmoother2.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var pc = _hasPrev ? value - prevValue : 0;
        var absPc = Math.Abs(pc);

        var pcSmooth1 = _pcSmoother1.Next(pc, isFinal);
        var pcSmooth2 = _pcSmoother2.Next(pcSmooth1, isFinal);
        var absSmooth1 = _absSmoother1.Next(absPc, isFinal);
        var absSmooth2 = _absSmoother2.Next(absSmooth1, isFinal);
        var tsi = absSmooth2 != 0 ? MathHelper.MinOrMax(100 * pcSmooth2 / absSmooth2, 100, -100) : 0;
        var signal = _signalSmoother.Next(tsi, isFinal);

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
                { "Tsi", tsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(tsi, outputs);
    }

    public void Dispose()
    {
        _pcSmoother1.Dispose();
        _pcSmoother2.Dispose();
        _absSmoother1.Dispose();
        _absSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class RateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public RateOfChangeState(int length = 12, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RateOfChangeState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RateOfChange;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var current = _input.GetValue(bar);
        double prevValue = 0;
        if (_window.Count >= _length)
        {
            prevValue = _window[0];
        }

        var roc = prevValue != 0 ? (current - prevValue) / prevValue * 100 : 0;
        if (isFinal)
        {
            _window.TryAdd(current, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Roc", roc }
            };
        }

        return new StreamingIndicatorStateResult(roc, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

public sealed class UlcerIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowSum _drawdownSum;
    private readonly StreamingInputResolver _input;

    public UlcerIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _drawdownSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public UlcerIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _drawdownSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.UlcerIndex;

    public void Reset()
    {
        _maxWindow.Reset();
        _drawdownSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var maxValue = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);

        var pctDrawdownSquared = maxValue != 0 ? MathHelper.Pow((value - maxValue) / maxValue * 100, 2) : 0;

        int sumCount;
        var sum = isFinal ? _drawdownSum.Add(pctDrawdownSquared, out sumCount)
            : _drawdownSum.Preview(pctDrawdownSquared, out sumCount);
        var denom = Math.Min(sumCount, _length);
        var average = denom > 0 ? sum / denom : 0;
        var ulcer = MathHelper.Sqrt(average);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ui", ulcer }
            };
        }

        return new StreamingIndicatorStateResult(ulcer, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _drawdownSum.Dispose();
    }
}

public sealed class VortexIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _vmPlusSum;
    private readonly RollingWindowSum _vmMinusSum;
    private readonly RollingWindowSum _trSum;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public VortexIndicatorState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _vmPlusSum = new RollingWindowSum(resolved);
        _vmMinusSum = new RollingWindowSum(resolved);
        _trSum = new RollingWindowSum(resolved);
    }

    public IndicatorName Name => IndicatorName.VortexIndicator;

    public void Reset()
    {
        _vmPlusSum.Reset();
        _vmMinusSum.Reset();
        _trSum.Reset();
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

        var vmPlus = Math.Abs(bar.High - prevLow);
        var vmMinus = Math.Abs(bar.Low - prevHigh);
        var trueRange = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);

        int _;
        var vmPlusTotal = isFinal ? _vmPlusSum.Add(vmPlus, out _) : _vmPlusSum.Preview(vmPlus, out _);
        var vmMinusTotal = isFinal ? _vmMinusSum.Add(vmMinus, out _) : _vmMinusSum.Preview(vmMinus, out _);
        var trueRangeTotal = isFinal ? _trSum.Add(trueRange, out _) : _trSum.Preview(trueRange, out _);

        var viPlus = trueRangeTotal != 0 ? vmPlusTotal / trueRangeTotal : 0;
        var viMinus = trueRangeTotal != 0 ? vmMinusTotal / trueRangeTotal : 0;

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
            outputs = new Dictionary<string, double>(2)
            {
                { "ViPlus", viPlus },
                { "ViMinus", viMinus }
            };
        }

        return new StreamingIndicatorStateResult(viPlus, outputs);
    }

    public void Dispose()
    {
        _vmPlusSum.Dispose();
        _vmMinusSum.Dispose();
        _trSum.Dispose();
    }
}

public sealed class AwesomeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly RollingWindowSum _fastSum;
    private readonly RollingWindowSum _slowSum;
    private readonly StreamingInputResolver _input;

    public AwesomeOscillatorState(int fastLength = 5, int slowLength = 34, InputName inputName = InputName.MedianPrice)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AwesomeOscillatorState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _input = new StreamingInputResolver(InputName.MedianPrice, selector);
    }

    public IndicatorName Name => IndicatorName.AwesomeOscillator;

    public void Reset()
    {
        _fastSum.Reset();
        _slowSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int fastCount;
        int slowCount;
        var fastSum = isFinal ? _fastSum.Add(value, out fastCount) : _fastSum.Preview(value, out fastCount);
        var slowSum = isFinal ? _slowSum.Add(value, out slowCount) : _slowSum.Preview(value, out slowCount);
        var fastSma = fastCount >= _fastLength ? fastSum / _fastLength : 0;
        var slowSma = slowCount >= _slowLength ? slowSum / _slowLength : 0;
        var ao = fastSma - slowSma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ao", ao }
            };
        }

        return new StreamingIndicatorStateResult(ao, outputs);
    }

    public void Dispose()
    {
        _fastSum.Dispose();
        _slowSum.Dispose();
    }
}

public sealed class AcceleratorOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly int _smoothLength;
    private readonly RollingWindowSum _fastSum;
    private readonly RollingWindowSum _slowSum;
    private readonly RollingWindowSum _smoothSum;
    private readonly StreamingInputResolver _input;

    public AcceleratorOscillatorState(int fastLength = 5, int slowLength = 34, int smoothLength = 5,
        InputName inputName = InputName.MedianPrice)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _smoothLength = Math.Max(1, smoothLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _smoothSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public AcceleratorOscillatorState(int fastLength, int slowLength, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _smoothLength = Math.Max(1, smoothLength);
        _fastSum = new RollingWindowSum(_fastLength);
        _slowSum = new RollingWindowSum(_slowLength);
        _smoothSum = new RollingWindowSum(_smoothLength);
        _input = new StreamingInputResolver(InputName.MedianPrice, selector);
    }

    public IndicatorName Name => IndicatorName.AcceleratorOscillator;

    public void Reset()
    {
        _fastSum.Reset();
        _slowSum.Reset();
        _smoothSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int fastCount;
        int slowCount;
        var fastSum = isFinal ? _fastSum.Add(value, out fastCount) : _fastSum.Preview(value, out fastCount);
        var slowSum = isFinal ? _slowSum.Add(value, out slowCount) : _slowSum.Preview(value, out slowCount);
        var fastSma = fastCount >= _fastLength ? fastSum / _fastLength : 0;
        var slowSma = slowCount >= _slowLength ? slowSum / _slowLength : 0;
        var ao = fastSma - slowSma;

        int smoothCount;
        var smoothSum = isFinal ? _smoothSum.Add(ao, out smoothCount) : _smoothSum.Preview(ao, out smoothCount);
        var aoSma = smoothCount >= _smoothLength ? smoothSum / _smoothLength : 0;
        var ac = ao - aoSma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ac", ac }
            };
        }

        return new StreamingIndicatorStateResult(ac, outputs);
    }

    public void Dispose()
    {
        _fastSum.Dispose();
        _slowSum.Dispose();
        _smoothSum.Dispose();
    }
}

public sealed class TrixState : IStreamingIndicatorState
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly EmaState _ema3;
    private readonly StreamingInputResolver _input;
    private double _prevEma3;
    private bool _hasPrev;

    public TrixState(int length = 15, InputName inputName = InputName.Close)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public TrixState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.Trix;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
        _prevEma3 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        var ema3 = _ema3.GetNext(ema2, isFinal);
        var prevEma3 = _hasPrev ? _prevEma3 : 0;
        var trix = CalculationsHelper.CalculatePercentChange(ema3, prevEma3);

        if (isFinal)
        {
            _prevEma3 = ema3;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Trix", trix }
            };
        }

        return new StreamingIndicatorStateResult(trix, outputs);
    }
}

internal readonly struct RollingWindowSnapshot
{
    public RollingWindowSnapshot(double sum, double sumSquares, int count)
    {
        Sum = sum;
        SumSquares = sumSquares;
        Count = count;
    }

    public double Sum { get; }
    public double SumSquares { get; }
    public int Count { get; }
}

internal sealed class RollingWindowSum : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private double _sum;

    public RollingWindowSum(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        if (_window.Count < _window.Capacity)
        {
            countAfter = _window.Count + 1;
            return _sum + value;
        }

        countAfter = _window.Capacity;
        return _sum + value - _window[0];
    }

    public double Add(double value, out int countAfter)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value - removed;
        }
        else
        {
            _sum += value;
        }

        countAfter = _window.Count;
        return _sum;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingWindowMax : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly LinkedList<(double value, int index)> _deque = new();
    private int _index;

    public RollingWindowMax(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        var capacity = _window.Capacity;
        var expireIndex = _index - capacity;
        countAfter = _window.Count < capacity ? _window.Count + 1 : capacity;

        var node = _deque.First;
        while (node != null && node.Value.index <= expireIndex)
        {
            node = node.Next;
        }

        var max = node != null ? node.Value.value : value;
        if (value > max)
        {
            max = value;
        }

        return max;
    }

    public double Add(double value, out int countAfter)
    {
        _window.TryAdd(value, out _);

        while (_deque.Last != null && _deque.Last.Value.value <= value)
        {
            _deque.RemoveLast();
        }

        _deque.AddLast((value, _index));

        var expireIndex = _index - _window.Capacity;
        while (_deque.First != null && _deque.First.Value.index <= expireIndex)
        {
            _deque.RemoveFirst();
        }

        _index++;
        countAfter = _window.Count;
        return _deque.First != null ? _deque.First.Value.value : value;
    }

    public void Reset()
    {
        _window.Clear();
        _deque.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingWindowMin : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private readonly LinkedList<(double value, int index)> _deque = new();
    private int _index;

    public RollingWindowMin(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public double Preview(double value, out int countAfter)
    {
        var capacity = _window.Capacity;
        var expireIndex = _index - capacity;
        countAfter = _window.Count < capacity ? _window.Count + 1 : capacity;

        var node = _deque.First;
        while (node != null && node.Value.index <= expireIndex)
        {
            node = node.Next;
        }

        var min = node != null ? node.Value.value : value;
        if (value < min)
        {
            min = value;
        }

        return min;
    }

    public double Add(double value, out int countAfter)
    {
        _window.TryAdd(value, out _);

        while (_deque.Last != null && _deque.Last.Value.value >= value)
        {
            _deque.RemoveLast();
        }

        _deque.AddLast((value, _index));

        var expireIndex = _index - _window.Capacity;
        while (_deque.First != null && _deque.First.Value.index <= expireIndex)
        {
            _deque.RemoveFirst();
        }

        _index++;
        countAfter = _window.Count;
        return _deque.First != null ? _deque.First.Value.value : value;
    }

    public void Reset()
    {
        _window.Clear();
        _deque.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingWindowStats : IDisposable
{
    private readonly PooledRingBuffer<double> _window;
    private double _sum;
    private double _sumSquares;

    public RollingWindowStats(int length)
    {
        _window = new PooledRingBuffer<double>(length);
    }

    public RollingWindowSnapshot Preview(double value)
    {
        var removed = _window.Count >= _window.Capacity ? _window[0] : 0;
        var sum = _sum + value - removed;
        var sumSquares = _sumSquares + (value * value) - (removed * removed);
        var count = _window.Count < _window.Capacity ? _window.Count + 1 : _window.Capacity;
        return new RollingWindowSnapshot(sum, sumSquares, count);
    }

    public RollingWindowSnapshot Add(double value)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value - removed;
            _sumSquares += (value * value) - (removed * removed);
        }
        else
        {
            _sum += value;
            _sumSquares += value * value;
        }

        return new RollingWindowSnapshot(_sum, _sumSquares, _window.Count);
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
        _sumSquares = 0;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class EmaState
{
    private readonly int _length;
    private readonly double _k;
    private int _count;
    private double _sum;
    private double _prevEma;

    public EmaState(int length)
    {
        _length = Math.Max(1, length);
        _k = Math.Min(Math.Max((double)2 / (_length + 1), 0.01), 0.99);
    }

    public double GetNext(double value, bool commit)
    {
        if (_count < _length)
        {
            var sum = _sum + value;
            var ema = sum / (_count + 1);
            if (commit)
            {
                _sum = sum;
                _count++;
                _prevEma = ema;
            }

            return ema;
        }

        var updated = (value * _k) + (_prevEma * (1 - _k));
        if (commit)
        {
            _prevEma = updated;
            _count++;
        }

        return updated;
    }

    public void Reset()
    {
        _count = 0;
        _sum = 0;
        _prevEma = 0;
    }
}

internal sealed class WilderState
{
    private readonly double _k;
    private double _prev;

    public WilderState(int length)
    {
        var resolved = Math.Max(1, length);
        _k = (double)1 / resolved;
    }

    public double GetNext(double value, bool commit)
    {
        var wwma = (value * _k) + (_prev * (1 - _k));
        if (commit)
        {
            _prev = wwma;
        }

        return wwma;
    }

    public void Reset()
    {
        _prev = 0;
    }
}

internal interface IMovingAverageSmoother : IDisposable
{
    double Next(double value, bool isFinal);
    void Reset();
}

internal sealed class SimpleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly RollingWindowSum _window;

    public SimpleMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length);
    }

    public double Next(double value, bool isFinal)
    {
        int countAfter;
        var sum = isFinal ? _window.Add(value, out countAfter) : _window.Preview(value, out countAfter);
        return countAfter >= _length ? sum / _length : 0;
    }

    public void Reset()
    {
        _window.Reset();
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class ExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema;

    public ExponentialMovingAverageSmoother(int length)
    {
        _ema = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _ema.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _ema.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class WilderMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly WilderState _wilder;

    public WilderMovingAverageSmoother(int length)
    {
        _wilder = new WilderState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _wilder.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _wilder.Reset();
    }

    public void Dispose()
    {
    }
}

internal static class MovingAverageSmootherFactory
{
    public static IMovingAverageSmoother Create(MovingAvgType maType, int length)
    {
        return maType switch
        {
            MovingAvgType.SimpleMovingAverage => new SimpleMovingAverageSmoother(length),
            MovingAvgType.ExponentialMovingAverage => new ExponentialMovingAverageSmoother(length),
            MovingAvgType.WildersSmoothingMethod => new WilderMovingAverageSmoother(length),
            _ => throw new NotSupportedException($"MovingAvgType {maType} is not supported in streaming stateful indicators.")
        };
    }
}

internal sealed class RsiState : IDisposable
{
    private readonly IMovingAverageSmoother _avgGain;
    private readonly IMovingAverageSmoother _avgLoss;
    private double _prevValue;
    private bool _hasPrev;

    public RsiState(MovingAvgType maType, int length)
    {
        var resolved = Math.Max(1, length);
        _avgGain = MovingAverageSmootherFactory.Create(maType, resolved);
        _avgLoss = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceChg = _hasPrev ? value - prevValue : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.Next(gain, isFinal);
        var avgLoss = _avgLoss.Next(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        return rsi;
    }

    public void Reset()
    {
        _avgGain.Reset();
        _avgLoss.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _avgGain.Dispose();
        _avgLoss.Dispose();
    }
}

internal sealed class RollingPercentRank : IDisposable
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly PooledRingBuffer<double> _window;
    private OrderStatisticTree? _tree;
    private bool _disposed;

    public RollingPercentRank(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        _window = new PooledRingBuffer<double>(_length);
        if (!_useLinear)
        {
            _tree = new OrderStatisticTree();
        }
    }

    public double Add(double value)
    {
        var count = AddAndCountLessThanOrEqual(value);
        return MathHelper.MinOrMax((double)count / _length * 100, 100, 0);
    }

    public double Preview(double value)
    {
        var count = CountLessThanOrEqual(value);
        return MathHelper.MinOrMax((double)count / _length * 100, 100, 0);
    }

    public void Reset()
    {
        _window.Clear();
        if (!_useLinear)
        {
            _tree = new OrderStatisticTree();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Dispose();
        _disposed = true;
    }

    private int AddAndCountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            _window.TryAdd(value, out _);
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] <= value)
                {
                    count++;
                }
            }

            return Math.Max(0, count - 1);
        }

        if (_window.TryAdd(value, out var removed))
        {
            _tree!.Remove(removed);
        }

        _tree!.Insert(value);
        return Math.Max(0, _tree.CountLessThanOrEqual(value) - 1);
    }

    private int CountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i] <= value)
                {
                    count++;
                }
            }

            return count;
        }

        return _tree!.CountLessThanOrEqual(value);
    }
}
