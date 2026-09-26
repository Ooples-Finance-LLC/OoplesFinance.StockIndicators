using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Rrsi")]
public sealed class RapidRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RapidGainLossWindow _window;
    private readonly StrengthAverage? _wideSignal;
    private readonly IMovingAverageSmoother _signal;
    public RapidRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _window = new(length);
        if (StrengthWindow.Supports(maType)) _wideSignal = new StrengthAverage(maType, length);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }
    public IndicatorName Name => IndicatorName.RapidRelativeStrengthIndex;
    public void Reset() { _window.Reset(); _wideSignal?.Reset(); _signal.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        var signal = _wideSignal is null ? _signal.Next(value, isFinal) : _wideSignal.Next(new StrengthValue(value), isFinal).Mantissa;
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rrsi", value }, { "Signal", signal } } : null);
    }
    public void Dispose() { _window.Dispose(); _wideSignal?.Dispose(); _signal.Dispose(); }
}

[PrimaryOutput("Rochla")]
public sealed class RatioOCHLAveragerState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevD;
    private bool _hasPrev;

    public RatioOCHLAveragerState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RatioOCHLAverager;

    public void Reset()
    {
        _prevD = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var range = bar.High - bar.Low;
        var b = range != 0 ? Math.Abs(value - bar.Open) / range : 0;
        var c = b > 1 ? 1 : b;
        var prevD = _hasPrev ? _prevD : value;
        var d = (c * value) + ((1 - c) * prevD);

        if (isFinal)
        {
            _prevD = d;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rochla", d }
            };
        }

        return new StreamingIndicatorStateResult(d, outputs);
    }
}

[PrimaryOutput("Rsi")]
public sealed class ReallySimpleIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ma;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public ReallySimpleIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21, int smoothLength = 10)
    {
        _ma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ReallySimpleIndicator;

    public void Reset()
    {
        _ma.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _ma.Next(value, isFinal);
        var rsi = value != 0 ? (bar.Low - ma) / value * 100 : 0;
        var signal = _signal.Next(rsi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Rsi", rsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _ma.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Rd")]
public sealed class RecursiveDifferenciatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly IMovingAverageSmoother _ema;
    private readonly WilderState _avgGain;
    private readonly WilderState _avgLoss;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _bValues;
    private double _prevEma;
    private bool _hasPrevEma;
    private double _prevBChg1;
    private double _prevBChg2;
    private bool _hasPrevBChg;

    public RecursiveDifferenciatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double alpha = 0.6)
    {
        _length = Math.Max(1, length);
        _alpha = alpha;
        _ema = MovingAverageSmootherFactory.Create(maType, _length);
        _avgGain = new WilderState(_length);
        _avgLoss = new WilderState(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _bValues = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.RecursiveDifferenciator;

    public void Reset()
    {
        _ema.Reset();
        _avgGain.Reset();
        _avgLoss.Reset();
        _bValues.Clear();
        _prevEma = 0;
        _hasPrevEma = false;
        _prevBChg1 = 0;
        _prevBChg2 = 0;
        _hasPrevBChg = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var prevEma = _hasPrevEma ? _prevEma : 0;
        var priceChg = _hasPrevEma ? ema - prevEma : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;
        var avgGain = _avgGain.GetNext(gain, isFinal);
        var avgLoss = _avgLoss.GetNext(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        var a = rsi / 100;
        var prevBChg1 = _hasPrevBChg ? _prevBChg1 : a;
        var b = (_alpha * a) + ((1 - _alpha) * prevBChg1);
        var priorB = _bValues.Count >= _length ? _bValues[_bValues.Count - _length] : 0;
        var bChg = b - priorB;

        if (isFinal)
        {
            _bValues.TryAdd(b, out _);
            _prevEma = ema;
            _hasPrevEma = true;
            _prevBChg2 = _hasPrevBChg ? _prevBChg1 : 0;
            _prevBChg1 = bChg;
            _hasPrevBChg = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rd", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _bValues.Dispose();
    }
}

[PrimaryOutput("Rmta")]
public sealed class RecursiveMovingTrendAverageState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevBot;
    private double _prevNRes;
    private bool _hasPrev;

    public RecursiveMovingTrendAverageState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _alpha = 2d / (resolved + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RecursiveMovingTrendAverage;

    public void Reset()
    {
        _prevBot = 0;
        _prevNRes = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevBot = _hasPrev ? _prevBot : value;
        var prevNRes = _hasPrev ? _prevNRes : value;
        var bot = ((1 - _alpha) * prevBot) + value;
        var nRes = ((1 - _alpha) * prevNRes) + (_alpha * (value + bot - prevBot));

        if (isFinal)
        {
            _prevBot = bot;
            _prevNRes = nRes;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rmta", nRes }
            };
        }

        return new StreamingIndicatorStateResult(nRes, outputs);
    }
}

[PrimaryOutput("Rrsi")]
public sealed class RecursiveRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _srcMa;
    private readonly WilderState _avgGain;
    private readonly WilderState _avgLoss;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _bValues;
    private readonly PooledRingBuffer<double> _avgValues;
    private readonly PooledRingBuffer<double> _gainValues;
    private readonly PooledRingBuffer<double> _lossValues;
    private readonly PooledRingBuffer<double> _avgRsiValues;
    private double _avgRsiSum;
    private double _prevSrc;
    private bool _hasPrevSrc;

    public RecursiveRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        _length = Math.Max(1, length);
        _srcMa = MovingAverageSmootherFactory.Create(maType, _length);
        _avgGain = new WilderState(_length);
        _avgLoss = new WilderState(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _values = new PooledRingBuffer<double>(_length);
        _bValues = new PooledRingBuffer<double>(_length);
        _avgValues = new PooledRingBuffer<double>(_length);
        _gainValues = new PooledRingBuffer<double>(_length);
        _lossValues = new PooledRingBuffer<double>(_length);
        _avgRsiValues = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.RecursiveRelativeStrengthIndex;

    public void Reset()
    {
        _srcMa.Reset();
        _avgGain.Reset();
        _avgLoss.Reset();
        _values.Clear();
        _bValues.Clear();
        _avgValues.Clear();
        _gainValues.Clear();
        _lossValues.Clear();
        _avgRsiValues.Clear();
        _avgRsiSum = 0;
        _prevSrc = 0;
        _hasPrevSrc = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var chg = _values.Count >= _length ? value - prevValue : 0;
        var src = _srcMa.Next(chg, isFinal);
        var prevSrc = _hasPrevSrc ? _prevSrc : 0;
        var srcChg = _hasPrevSrc ? src - prevSrc : 0;
        var srcGain = srcChg > 0 ? srcChg : 0;
        var srcLoss = srcChg < 0 ? Math.Abs(srcChg) : 0;
        var avgGain = _avgGain.GetNext(srcGain, isFinal);
        var avgLoss = _avgLoss.GetNext(srcLoss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        double b = 0;
        double avg = 0;
        double gain = 0;
        double loss = 0;
        double avgRsi = 0;
        var useAvg = _avgRsiValues.Count >= _length;
        for (var j = 1; j <= _length; j++)
        {
            var prevB = j <= _bValues.Count ? _bValues[_bValues.Count - j] : src;
            var prevAvg = j <= _avgValues.Count ? _avgValues[_avgValues.Count - j] : 0;
            var prevGain = j <= _gainValues.Count ? _gainValues[_gainValues.Count - j] : 0;
            var prevLoss = j <= _lossValues.Count ? _lossValues[_lossValues.Count - j] : 0;
            var k = (double)j / _length;
            var a = rsi * ((double)_length / j);
            avg = (a + prevB) / 2;
            var avgChg = avg - prevAvg;
            gain = avgChg > 0 ? avgChg : 0;
            loss = avgChg < 0 ? Math.Abs(avgChg) : 0;
            var avgGainRec = (gain * k) + (prevGain * (1 - k));
            var avgLossRec = (loss * k) + (prevLoss * (1 - k));
            var rsRec = avgLossRec != 0 ? avgGainRec / avgLossRec : 0;
            avgRsi = avgLossRec == 0 ? 100 : avgGainRec == 0
                ? 0
                : MathHelper.MinOrMax(100 - (100 / (1 + rsRec)), 1, 0);
            b = useAvg ? _avgRsiSum / _length : avgRsi;
        }

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _bValues.TryAdd(b, out _);
            _avgValues.TryAdd(avg, out _);
            _gainValues.TryAdd(gain, out _);
            _lossValues.TryAdd(loss, out _);
            if (_avgRsiValues.TryAdd(avgRsi, out var removed))
            {
                _avgRsiSum += avgRsi - removed;
            }
            else
            {
                _avgRsiSum += avgRsi;
            }

            _prevSrc = src;
            _hasPrevSrc = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rrsi", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }

    public void Dispose()
    {
        _srcMa.Dispose();
        _values.Dispose();
        _bValues.Dispose();
        _avgValues.Dispose();
        _gainValues.Dispose();
        _lossValues.Dispose();
        _avgRsiValues.Dispose();
    }
}

[PrimaryOutput("Rsto")]
public sealed class RecursiveStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly RollingWindowMax _maHighWindow;
    private readonly RollingWindowMin _maLowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevK;
    private bool _hasPrevK;

    public RecursiveStochasticState(int length = 200, double alpha = 0.1)
    {
        _length = Math.Max(1, length);
        _alpha = alpha;
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _maHighWindow = new RollingWindowMax(_length);
        _maLowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RecursiveStochastic;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _maHighWindow.Reset();
        _maLowWindow.Reset();
        _prevK = 0;
        _hasPrevK = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(value, out _) : _highWindow.Preview(value, out _);
        var lowest = isFinal ? _lowWindow.Add(value, out _) : _lowWindow.Preview(value, out _);
        var stoch = highest - lowest != 0 ? (value - lowest) / (highest - lowest) * 100 : 0;
        var prevK = _hasPrevK ? _prevK : 0;
        var ma = (_alpha * stoch) + ((1 - _alpha) * prevK);
        var highestMa = isFinal ? _maHighWindow.Add(ma, out _) : _maHighWindow.Preview(ma, out _);
        var lowestMa = isFinal ? _maLowWindow.Add(ma, out _) : _maLowWindow.Preview(ma, out _);
        var k = highestMa - lowestMa != 0
            ? MathHelper.MinOrMax((ma - lowestMa) / (highestMa - lowestMa) * 100, 100, 0)
            : 0;

        if (isFinal)
        {
            _prevK = k;
            _hasPrevK = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rsto", k }
            };
        }

        return new StreamingIndicatorStateResult(k, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _maHighWindow.Dispose();
        _maLowWindow.Dispose();
    }
}

[PrimaryOutput("Rosc")]
public sealed class RegressionOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactLinearFitWindow _linReg;
    private readonly StreamingInputResolver _input;

    public RegressionOscillatorState(int length = 63)
    {
        _linReg = new ExactLinearFitWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RegressionOscillator;

    public void Reset()
    {
        _linReg.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _input.GetValue(bar);
        var rosc = _linReg.Next(value, isFinal).PercentFitResidual(value);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rosc", rosc }
            };
        }

        return new StreamingIndicatorStateResult(rosc, outputs);
    }

    public void Dispose()
    {
        _linReg.Dispose();
    }
}

[PrimaryOutput("Rema")]
public sealed class RegularizedExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private readonly double _lambda;
    private readonly StreamingInputResolver _input;
    private double _prevRema1;
    private double _prevRema2;

    public RegularizedExponentialMovingAverageState(int length = 14, double lambda = 0.5)
    {
        var resolved = Math.Max(1, length);
        _alpha = 2d / (resolved + 1);
        _lambda = lambda;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RegularizedExponentialMovingAverage;

    public void Reset()
    {
        _prevRema1 = 0;
        _prevRema2 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rema = (_prevRema1 + (_alpha * (value - _prevRema1)) + (_lambda * ((2 * _prevRema1) - _prevRema2)))
            / (_lambda + 1);

        if (isFinal)
        {
            _prevRema2 = _prevRema1;
            _prevRema1 = rema;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rema", rema }
            };
        }

        return new StreamingIndicatorStateResult(rema, outputs);
    }
}

[PrimaryOutput("Rdos")]
public sealed class RelativeDifferenceOfSquaresOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _aSum;
    private readonly RollingWindowSum _dSum;
    private readonly RollingWindowSum _nSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public RelativeDifferenceOfSquaresOscillatorState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _aSum = new RollingWindowSum(resolved);
        _dSum = new RollingWindowSum(resolved);
        _nSum = new RollingWindowSum(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeDifferenceOfSquaresOscillator;

    public void Reset()
    {
        _aSum.Reset();
        _dSum.Reset();
        _nSum.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var a = value > prevValue ? 1 : 0;
        var d = value < prevValue ? 1 : 0;
        var n = value == prevValue ? 1 : 0;

        var aSum = isFinal ? _aSum.Add(a, out _) : _aSum.Preview(a, out _);
        var dSum = isFinal ? _dSum.Add(d, out _) : _dSum.Preview(d, out _);
        var nSum = isFinal ? _nSum.Add(n, out _) : _nSum.Preview(n, out _);
        var total = aSum + dSum + nSum;
        var rdos = total != 0 ? (MathHelper.Pow(aSum, 2) - MathHelper.Pow(dSum, 2)) / MathHelper.Pow(total, 2) : 0;

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
                { "Rdos", rdos }
            };
        }

        return new StreamingIndicatorStateResult(rdos, outputs);
    }

    public void Dispose()
    {
        _aSum.Dispose();
        _dSum.Dispose();
        _nSum.Dispose();
    }
}

[PrimaryOutput("Rmi")]
public sealed class RelativeMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeMomentumWindow? _wide;
    private readonly int _length2;
    private readonly IMovingAverageSmoother _avgGain;
    private readonly IMovingAverageSmoother _avgLoss;
    private readonly IMovingAverageSmoother _signal;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public RelativeMomentumIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 14, int length2 = 3)
    {
        if (StrengthWindow.Supports(maType)) _wide = new RelativeMomentumWindow(maType, length1, length2);
        _length2 = Math.Max(1, length2);
        _avgGain = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _avgLoss = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _values = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeMomentumIndex;

    public void Reset()
    {
        _wide?.Reset();
        _avgGain.Reset();
        _avgLoss.Reset();
        _signal.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null)
        {
            var next = _wide.Next(bar.Close, isFinal);
            return new StreamingIndicatorStateResult(next.Value, includeOutputs ? new Dictionary<string, double>
                { { "Rmi", next.Value }, { "Signal", next.Signal }, { "Histogram", next.Histogram } } : null);
        }
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length2);
        var hasLength = _values.Count >= _length2;
        var priceChg = hasLength ? value - prevValue : 0;
        var gain = hasLength && priceChg > 0 ? priceChg : 0;
        var loss = hasLength && priceChg < 0 ? Math.Abs(priceChg) : 0;
        var avgGain = _avgGain.Next(gain, isFinal);
        var avgLoss = _avgLoss.Next(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rmi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        var signal = _signal.Next(rmi, isFinal);
        var histogram = rmi - signal;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Rmi", rmi },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(rmi, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _avgGain.Dispose();
        _avgLoss.Dispose();
        _signal.Dispose();
        _values.Dispose();
    }
}

public sealed class RelativeNormalizedVolatilityState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly PairedSeriesAlignment _alignment = new();
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly RollingStandardDeviation _primaryStdDev;
    private readonly RollingStandardDeviation _marketStdDev;
    private readonly IMovingAverageSmoother _primaryAbsSma;
    private readonly IMovingAverageSmoother _marketAbsSma;
    private double _prevValue;
    private bool _hasPrev;
    private double _prevMarketValue;
    private bool _hasMarketPrev;
    private double _latestMarketAbsSma;
    private bool _hasMarketAbsSma;

    public RelativeNormalizedVolatilityState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        var resolved = Math.Max(1, length);
        _primaryStdDev = new RollingStandardDeviation(resolved);
        _marketStdDev = new RollingStandardDeviation(resolved);
        _primaryAbsSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _marketAbsSma = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.RelativeNormalizedVolatility;

    public void Reset()
    {
        _alignment.Reset();
        _primaryStdDev.Reset();
        _marketStdDev.Reset();
        _primaryAbsSma.Reset();
        _marketAbsSma.Reset();
        _prevValue = 0;
        _hasPrev = false;
        _prevMarketValue = 0;
        _hasMarketPrev = false;
        _latestMarketAbsSma = 0;
        _hasMarketAbsSma = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (!_alignment.CanUpdate(context, _primarySeries, _marketSeries, series, bar, isFinal))
            return new MultiSeriesIndicatorStateResult(false, 0d, null);

        if (series.Equals(_marketSeries))
        {
            var stdDev = _marketStdDev.Next(bar.Close, isFinal);
            var sp = _hasMarketPrev ? bar.Close - _prevMarketValue : 0;
            var zsp = stdDev != 0 ? sp / stdDev : 0;
            var absZsp = Math.Abs(zsp);
            var marketAbsZspSma = _marketAbsSma.Next(absZsp, isFinal);

            if (isFinal)
            {
                _prevMarketValue = bar.Close;
                _hasMarketPrev = true;
                _latestMarketAbsSma = marketAbsZspSma;
                _hasMarketAbsSma = true;
            }

            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        if (!series.Equals(_primarySeries))
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var stdDevPrimary = _primaryStdDev.Next(bar.Close, isFinal);
        var d = _hasPrev ? bar.Close - _prevValue : 0;
        var zsrc = stdDevPrimary != 0 ? d / stdDevPrimary : 0;
        var absZsrc = Math.Abs(zsrc);
        var absZsrcSma = _primaryAbsSma.Next(absZsrc, isFinal);

        double absZspSma;
        if (_hasMarketAbsSma)
        {
            absZspSma = _latestMarketAbsSma;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            var marketStdDev = _marketStdDev.Next(marketBar.Close, isFinal: false);
            var sp = _hasMarketPrev ? marketBar.Close - _prevMarketValue : 0;
            var zsp = marketStdDev != 0 ? sp / marketStdDev : 0;
            var absZsp = Math.Abs(zsp);
            absZspSma = _marketAbsSma.Next(absZsp, isFinal: false);
        }
        else
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var rnv = absZspSma != 0 ? absZsrcSma / absZspSma : 0;

        if (isFinal)
        {
            _prevValue = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rnv", rnv }
            };
        }

        _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(true, rnv, outputs);
    }

    public void Dispose()
    {
        _primaryStdDev.Dispose();
        _marketStdDev.Dispose();
        _primaryAbsSma.Dispose();
        _marketAbsSma.Dispose();
    }
}

[PrimaryOutput("Rss")]
public sealed class RelativeSpreadStrengthState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeSpreadKernel _kernel;
    public RelativeSpreadStrengthState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 10, int slowLength = 40, int length = 14, int smoothLength = 5)
    { _kernel = new(maType, fastLength, slowLength, length, smoothLength); }
    public IndicatorName Name => IndicatorName.RelativeSpreadStrength;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _kernel.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rss", value } } : null);
    }
    public void Dispose() => _kernel.Dispose();
}

public sealed class RelativeStrength3DIndicatorState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly PairedSeriesAlignment _alignment = new();
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly int _length4;
    private readonly IMovingAverageSmoother _fastMa;
    private readonly IMovingAverageSmoother _medMa;
    private readonly IMovingAverageSmoother _slowMa;
    private readonly IMovingAverageSmoother _vSlowMa;
    private readonly IMovingAverageSmoother _rs2Ma;
    private readonly RollingWindowSum _xSum;
    private double _prevR1;
    private bool _hasPrevR1;
    private double _lastMarketValue;
    private bool _hasMarket;

    public RelativeStrength3DIndicatorState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 4, int length2 = 7,
        int length3 = 10, int length4 = 15, int length5 = 30)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        _length4 = Math.Max(1, length4);
        _fastMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _medMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _slowMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _vSlowMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _rs2Ma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xSum = new RollingWindowSum(_length4);
    }

    public IndicatorName Name => IndicatorName.RelativeStrength3DIndicator;

    public void Reset()
    {
        _alignment.Reset();
        _fastMa.Reset();
        _medMa.Reset();
        _slowMa.Reset();
        _vSlowMa.Reset();
        _rs2Ma.Reset();
        _xSum.Reset();
        _prevR1 = 0;
        _hasPrevR1 = false;
        _lastMarketValue = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (!_alignment.CanUpdate(context, _primarySeries, _marketSeries, series, bar, isFinal))
            return new MultiSeriesIndicatorStateResult(false, 0d, null);

        if (series.Equals(_marketSeries))
        {
            if (isFinal)
            {
                _lastMarketValue = bar.Close;
                _hasMarket = true;
            }
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        if (!series.Equals(_primarySeries))
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        double marketValue;
        if (_hasMarket)
        {
            marketValue = _lastMarketValue;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            marketValue = marketBar.Close;
        }
        else
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var r1 = marketValue != 0 ? bar.Close / marketValue * 100 : _hasPrevR1 ? _prevR1 : 0;
        var fastMa = _fastMa.Next(r1, isFinal);
        var medMa = _medMa.Next(fastMa, isFinal);
        var slowMa = _slowMa.Next(fastMa, isFinal);
        var vSlowMa = _vSlowMa.Next(slowMa, isFinal);
        double t1 = TechnicalRatingComparison.Compare(fastMa, medMa) >= 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) >= 0 ? 10 : 0;
        double t2 = TechnicalRatingComparison.Compare(fastMa, medMa) >= 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) < 0 ? 9 : 0;
        double t3 = TechnicalRatingComparison.Compare(fastMa, medMa) < 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) >= 0 ? 9 : 0;
        double t4 = TechnicalRatingComparison.Compare(fastMa, medMa) < 0 && TechnicalRatingComparison.Compare(medMa, slowMa) >= 0 && TechnicalRatingComparison.Compare(slowMa, vSlowMa) < 0 ? 5 : 0;
        var rs2 = t1 + t2 + t3 + t4;
        var rs2Ma = _rs2Ma.Next(rs2, isFinal);
        var x = rs2 >= 5 ? 1 : 0;
        var xSum = isFinal ? _xSum.Add(x, out _) : _xSum.Preview(x, out _);
        var rs3 = rs2 >= 5 || TechnicalRatingComparison.Compare(rs2, rs2Ma) > 0 ? xSum / _length4 * 100 : 0;

        if (isFinal)
        {
            _prevR1 = r1;
            _hasPrevR1 = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rs3d", rs3 }
            };
        }

        _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(true, rs3, outputs);
    }

    public void Dispose()
    {
        _fastMa.Dispose();
        _medMa.Dispose();
        _slowMa.Dispose();
        _vSlowMa.Dispose();
        _rs2Ma.Dispose();
        _xSum.Dispose();
    }
}

[PrimaryOutput("Rvi")]
public sealed class RelativeVigorIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _numeratorMa;
    private readonly IMovingAverageSmoother _denominatorMa;
    private readonly PooledRingBuffer<double> _openValues;
    private readonly PooledRingBuffer<double> _closeValues;
    private readonly PooledRingBuffer<double> _rangeValues;
    private readonly StreamingInputResolver _input;
    private double _prevRvi1;
    private double _prevRvi2;
    private double _prevRvi3;

    public RelativeVigorIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var resolved = Math.Max(1, length);
        _numeratorMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _denominatorMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _openValues = new PooledRingBuffer<double>(3);
        _closeValues = new PooledRingBuffer<double>(3);
        _rangeValues = new PooledRingBuffer<double>(3);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeVigorIndex;

    public void Reset()
    {
        _numeratorMa.Reset();
        _denominatorMa.Reset();
        _openValues.Clear();
        _closeValues.Clear();
        _rangeValues.Clear();
        _prevRvi1 = 0;
        _prevRvi2 = 0;
        _prevRvi3 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var open = bar.Open;
        var high = bar.High;
        var low = bar.Low;
        var prevOpen1 = _openValues.Count >= 1 ? _openValues[_openValues.Count - 1] : 0;
        var prevOpen2 = _openValues.Count >= 2 ? _openValues[_openValues.Count - 2] : 0;
        var prevOpen3 = _openValues.Count >= 3 ? _openValues[_openValues.Count - 3] : 0;
        var prevClose1 = _closeValues.Count >= 1 ? _closeValues[_closeValues.Count - 1] : 0;
        var prevClose2 = _closeValues.Count >= 2 ? _closeValues[_closeValues.Count - 2] : 0;
        var prevClose3 = _closeValues.Count >= 3 ? _closeValues[_closeValues.Count - 3] : 0;
        var prevRange1 = _rangeValues.Count >= 1 ? _rangeValues[_rangeValues.Count - 1] : 0;
        var prevRange2 = _rangeValues.Count >= 2 ? _rangeValues[_rangeValues.Count - 2] : 0;
        var prevRange3 = _rangeValues.Count >= 3 ? _rangeValues[_rangeValues.Count - 3] : 0;

        var a = close - open;
        var b = prevClose1 - prevOpen1;
        var c = prevClose2 - prevOpen2;
        var d = prevClose3 - prevOpen3;
        var e = high - low;
        var f = prevRange1;
        var g = prevRange2;
        var h = prevRange3;

        var numerator = (a + (2 * b) + (2 * c) + d) / 6;
        var denominator = (e + (2 * f) + (2 * g) + h) / 6;
        var numeratorAvg = _numeratorMa.Next(numerator, isFinal);
        var denominatorAvg = _denominatorMa.Next(denominator, isFinal);
        var rvi = denominatorAvg != 0 ? numeratorAvg / denominatorAvg : 0;
        var signal = (rvi + (2 * _prevRvi1) + (2 * _prevRvi2) + _prevRvi3) / 6;

        if (isFinal)
        {
            _openValues.TryAdd(open, out _);
            _closeValues.TryAdd(close, out _);
            _rangeValues.TryAdd(high - low, out _);
            _prevRvi3 = _prevRvi2;
            _prevRvi2 = _prevRvi1;
            _prevRvi1 = rvi;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Rvi", rvi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rvi, outputs);
    }

    public void Dispose()
    {
        _numeratorMa.Dispose();
        _denominatorMa.Dispose();
        _openValues.Dispose();
        _closeValues.Dispose();
        _rangeValues.Dispose();
    }
}

[PrimaryOutput("Rvi")]
public sealed class RelativeVolatilityIndexV1State : IStreamingIndicatorState, IDisposable
{
    // The deviation of the window about its own mean, matching the batch calculation; see #190. It is fed
    // the resolved input rather than resolving one of its own, so a composed reading - the V2 indicator
    // builds one of these on the high and one on the low - still measures the series it was composed on.
    private readonly RollingStandardDeviation _stdDev;
    private readonly IMovingAverageSmoother _upAvg;
    private readonly IMovingAverageSmoother _downAvg;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    // Internal: the library composes this state on a named input other than its own default -
    // a volatility of volume, a relative volatility of the high and of the low. Every parameter is
    // required, so this can never be chosen in place of the public constructor.
    internal RelativeVolatilityIndexV1State(MovingAvgType maType, int length, int smoothLength, InputName inputName)
    {
        _stdDev = new RollingStandardDeviation(Math.Max(1, length));
        _upAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _downAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public RelativeVolatilityIndexV1State(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 10, int smoothLength = 14)
    {
        _stdDev = new RollingStandardDeviation(Math.Max(1, length));
        _upAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _downAvg = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeVolatilityIndexV1;

    public void Reset()
    {
        _stdDev.Reset();
        _upAvg.Reset();
        _downAvg.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var stdDev = _stdDev.Next(value, isFinal);
        var up = value > prevValue ? stdDev : 0;
        var down = value < prevValue ? stdDev : 0;
        var avgUp = _upAvg.Next(up, isFinal);
        var avgDown = _downAvg.Next(down, isFinal);
        var rs = avgDown != 0 ? avgUp / avgDown : 0;
        var rvi = avgDown == 0 ? 100 : avgUp == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

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
                { "Rvi", rvi }
            };
        }

        return new StreamingIndicatorStateResult(rvi, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _upAvg.Dispose();
        _downAvg.Dispose();
    }
}

[PrimaryOutput("Rvi")]
public sealed class RelativeVolatilityIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityIndexV1State _rviHigh;
    private readonly RelativeVolatilityIndexV1State _rviLow;

    public RelativeVolatilityIndexV2State(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 10, int smoothLength = 14)
    {
        _rviHigh = new RelativeVolatilityIndexV1State(maType, length, smoothLength, InputName.High);
        _rviLow = new RelativeVolatilityIndexV1State(maType, length, smoothLength, InputName.Low);
    }

    public IndicatorName Name => IndicatorName.RelativeVolatilityIndexV2;

    public void Reset()
    {
        _rviHigh.Reset();
        _rviLow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var rviHigh = _rviHigh.Update(bar, isFinal, includeOutputs: false).Value;
        var rviLow = _rviLow.Update(bar, isFinal, includeOutputs: false).Value;
        var rvi = (rviHigh + rviLow) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rvi", rvi }
            };
        }

        return new StreamingIndicatorStateResult(rvi, outputs);
    }

    public void Dispose()
    {
        _rviHigh.Dispose();
        _rviLow.Dispose();
    }
}

[PrimaryOutput("Rvi")]
public sealed class RelativeVolumeIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly RollingStandardDeviation _volumeStdDev;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevDpl;
    private bool _hasPrev;
    private bool _hasPrevDpl;

    public RelativeVolumeIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        var resolved = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _volumeStdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeVolumeIndicator;

    public void Reset()
    {
        _volumeMa.Reset();
        _volumeStdDev.Reset();
        _prevValue = 0;
        _prevDpl = 0;
        _hasPrev = false;
        _hasPrevDpl = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var avgVolume = _volumeMa.Next(bar.Volume, isFinal);
        var sdVolume = _volumeStdDev.Next(bar.Volume, isFinal);
        var relVol = sdVolume != 0 ? (bar.Volume - avgVolume) / sdVolume : 0;
        var prevDpl = _hasPrevDpl ? _prevDpl : 0;
        var dpl = relVol >= 2 ? prevValue : _hasPrevDpl ? prevDpl : value;

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
                { "Rvi", relVol },
                { "Dpl", dpl }
            };
        }

        return new StreamingIndicatorStateResult(relVol, outputs);
    }

    public void Dispose()
    {
        _volumeMa.Dispose();
        _volumeStdDev.Dispose();
    }
}

[PrimaryOutput("Repulse")]
public sealed class RepulseState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _bullMa;
    private readonly IMovingAverageSmoother _bearMa;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevOpen;
    private bool _hasPrevOpen;

    public RepulseState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _bullMa = MovingAverageSmootherFactory.Create(maType, resolved * 5);
        _bearMa = MovingAverageSmootherFactory.Create(maType, resolved * 5);
        _signal = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Repulse;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _bullMa.Reset();
        _bearMa.Reset();
        _signal.Reset();
        _prevOpen = 0;
        _hasPrevOpen = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var lowestLow = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var highestHigh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var prevOpen = _hasPrevOpen ? _prevOpen : 0;
        var bullPower = close != 0 ? 100 * ((3 * close) - (2 * lowestLow) - prevOpen) / close : 0;
        var bearPower = close != 0 ? 100 * (prevOpen + (2 * highestHigh) - (3 * close)) / close : 0;
        var bullMa = _bullMa.Next(bullPower, isFinal);
        var bearMa = _bearMa.Next(bearPower, isFinal);
        var repulse = bullMa - bearMa;
        var signal = _signal.Next(repulse, isFinal);

        if (isFinal)
        {
            _prevOpen = bar.Open;
            _hasPrevOpen = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Repulse", repulse },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(repulse, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _bullMa.Dispose();
        _bearMa.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Rma")]
public sealed class RepulsionMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma1;
    private readonly IMovingAverageSmoother _sma2;
    private readonly IMovingAverageSmoother _sma3;
    private readonly StreamingInputResolver _input;

    public RepulsionMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100)
    {
        var resolved = Math.Max(1, length);
        _sma1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _sma2 = MovingAverageSmootherFactory.Create(maType, resolved * 2);
        _sma3 = MovingAverageSmootherFactory.Create(maType, resolved * 3);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RepulsionMovingAverage;

    public void Reset()
    {
        _sma1.Reset();
        _sma2.Reset();
        _sma3.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma1 = _sma1.Next(value, isFinal);
        var sma2 = _sma2.Next(value, isFinal);
        var sma3 = _sma3.Next(value, isFinal);
        var ma = sma3 + sma2 - sma1;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rma", ma }
            };
        }

        return new StreamingIndicatorStateResult(ma, outputs);
    }

    public void Dispose()
    {
        _sma1.Dispose();
        _sma2.Dispose();
        _sma3.Dispose();
    }
}

[PrimaryOutput("Raf")]
public sealed class RetentionAccelerationFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow1;
    private readonly RollingWindowMin _lowWindow1;
    private readonly RollingWindowMax _highWindow2;
    private readonly RollingWindowMin _lowWindow2;
    private readonly StreamingInputResolver _input;
    private double _prevAltma;
    private bool _hasPrev;

    public RetentionAccelerationFilterState(int length = 50)
    {
        _length = Math.Max(1, length);
        _highWindow1 = new RollingWindowMax(_length);
        _lowWindow1 = new RollingWindowMin(_length);
        _highWindow2 = new RollingWindowMax(_length * 2);
        _lowWindow2 = new RollingWindowMin(_length * 2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RetentionAccelerationFilter;

    public void Reset()
    {
        _highWindow1.Reset();
        _lowWindow1.Reset();
        _highWindow2.Reset();
        _lowWindow2.Reset();
        _prevAltma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest1 = isFinal ? _highWindow1.Add(bar.High, out _) : _highWindow1.Preview(bar.High, out _);
        var lowest1 = isFinal ? _lowWindow1.Add(bar.Low, out _) : _lowWindow1.Preview(bar.Low, out _);
        var highest2 = isFinal ? _highWindow2.Add(bar.High, out _) : _highWindow2.Preview(bar.High, out _);
        var lowest2 = isFinal ? _lowWindow2.Add(bar.Low, out _) : _lowWindow2.Preview(bar.Low, out _);
        var ar = 2 * (highest1 - lowest1);
        var br = 2 * (highest2 - lowest2);
        var k1 = ar != 0 ? (1 - ar) / ar : 0;
        var k2 = br != 0 ? (1 - br) / br : 0;
        var alpha = k1 != 0 ? k2 / k1 : 0;
        var r1 = alpha != 0 && highest1 >= 0
            ? MathHelper.Sqrt(highest1) / 4 * ((alpha - 1) / alpha) * (k2 / (k2 + 1))
            : 0;
        var r2 = highest2 >= 0 ? MathHelper.Sqrt(highest2) / 4 * (alpha - 1) * (k1 / (k1 + 1)) : 0;
        var factor = r1 != 0 ? r2 / r1 : 0;
        var altk = MathHelper.Pow(factor >= 1 ? 1 : factor, MathHelper.Sqrt(_length)) * ((double)1 / _length);
        var prevAltma = _hasPrev ? _prevAltma : value;
        var altma = (altk * value) + ((1 - altk) * prevAltma);

        if (isFinal)
        {
            _prevAltma = altma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Raf", altma }
            };
        }

        return new StreamingIndicatorStateResult(altma, outputs);
    }

    public void Dispose()
    {
        _highWindow1.Dispose();
        _lowWindow1.Dispose();
        _highWindow2.Dispose();
        _lowWindow2.Dispose();
    }
}

[PrimaryOutput("Rcc")]
public sealed class RetrospectiveCandlestickChartState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _absMax;
    private readonly RollingWindowMin _absMin;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevC;
    private bool _hasPrev;
    private bool _hasPrevC;

    public RetrospectiveCandlestickChartState(int length = 100)
    {
        var resolved = Math.Max(1, length);
        _absMax = new RollingWindowMax(resolved);
        _absMin = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RetrospectiveCandlestickChart;

    public void Reset()
    {
        _absMax.Reset();
        _absMin.Reset();
        _prevValue = 0;
        _prevC = 0;
        _hasPrev = false;
        _hasPrevC = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevValue : 0;
        var absChg = Math.Abs(close - prevClose);
        var highest = isFinal ? _absMax.Add(absChg, out _) : _absMax.Preview(absChg, out _);
        var lowest = isFinal ? _absMin.Add(absChg, out _) : _absMin.Preview(absChg, out _);
        var s = highest - lowest != 0 ? (absChg - lowest) / (highest - lowest) * 100 : 0;
        var weight = s / 100;

        var prevC = _hasPrevC ? _prevC : close;
        var c = (weight * close) + ((1 - weight) * prevC);
        var prevH = _hasPrevC ? prevC : bar.High;
        var h = (weight * bar.High) + ((1 - weight) * prevH);
        var prevL = _hasPrevC ? prevC : bar.Low;
        var l = (weight * bar.Low) + ((1 - weight) * prevL);
        var prevO = _hasPrevC ? prevC : bar.Open;
        var o = (weight * bar.Open) + ((1 - weight) * prevO);
        var k = (c + h + l + o) / 4;

        if (isFinal)
        {
            _prevValue = close;
            _prevC = c;
            _hasPrev = true;
            _hasPrevC = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rcc", k }
            };
        }

        return new StreamingIndicatorStateResult(k, outputs);
    }

    public void Dispose()
    {
        _absMax.Dispose();
        _absMin.Dispose();
    }
}

[PrimaryOutput("Rp")]
public sealed class ReversalPointsState : IStreamingIndicatorState, IDisposable
{
    private readonly ReversalPointsWindow _window;
    public ReversalPointsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 100)
        => _window = new ReversalPointsWindow(maType, length);
    public IndicatorName Name => IndicatorName.ReversalPoints;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rp", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rersi")]
public sealed class ReverseEngineeringRelativeStrengthIndexState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _rsiLevel;
    private readonly double _k;
    private readonly StreamingInputResolver _input;
    private double _prevAuc;
    private double _prevAdc;
    private double _prevValue;
    private bool _hasPrev;

    public ReverseEngineeringRelativeStrengthIndexState(int length = 14, double rsiLevel = 50)
    {
        _length = Math.Max(1, length);
        _rsiLevel = rsiLevel;
        var expPeriod = (2 * _length) - 1;
        _k = 2d / (expPeriod + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
        _prevAuc = 1;
        _prevAdc = 1;
    }

    public IndicatorName Name => IndicatorName.ReverseEngineeringRelativeStrengthIndex;

    public void Reset()
    {
        _prevAuc = 1;
        _prevAdc = 1;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diffUp = _hasPrev ? value - prevValue : 0;
        var diffDown = _hasPrev ? prevValue - value : 0;
        var auc = value > prevValue ? (_k * diffUp) + ((1 - _k) * _prevAuc) : (1 - _k) * _prevAuc;
        var adc = value > prevValue ? ((1 - _k) * _prevAdc) : (_k * diffDown) + ((1 - _k) * _prevAdc);
        var rsiValue = (_length - 1) * ((adc * _rsiLevel / (100 - _rsiLevel)) - auc);
        var revRsi = rsiValue >= 0 ? value + rsiValue : value + (rsiValue * (100 - _rsiLevel) / _rsiLevel);

        if (isFinal)
        {
            _prevValue = value;
            _prevAuc = auc;
            _prevAdc = adc;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rersi", revRsi }
            };
        }

        return new StreamingIndicatorStateResult(revRsi, outputs);
    }
}

[PrimaryOutput("Rmacd")]
public sealed class ReverseMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly IMovingAverageSmoother _fastMa;
    private readonly IMovingAverageSmoother _slowMa;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StreamingInputResolver _input;
    private double _prevFastMa;
    private double _prevSlowMa;
    private bool _hasPrev;
    private bool _signalInvalid;

    public ReverseMovingAverageConvergenceDivergenceState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 12, int slowLength = 26,
        int signalLength = 9, double macdLevel = 0)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastAlpha = 2d / (1d + resolvedFast);
        _slowAlpha = 2d / (1d + resolvedSlow);
        _fastMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedFast) : MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedSlow) : MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _signalMa = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ReverseMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _fastMa.Reset();
        _slowMa.Reset();
        _signalMa.Reset();
        _prevFastMa = 0;
        _prevSlowMa = 0;
        _hasPrev = false;
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var prevFast = _hasPrev ? _prevFastMa : 0;
        var prevSlow = _hasPrev ? _prevSlowMa : 0;
        var pMacdEq = RoundedReverseMacd.Equilibrium(prevFast, prevSlow, _fastAlpha, _slowAlpha);
        var invalid = _signalInvalid || double.IsNaN(pMacdEq) || double.IsInfinity(pMacdEq);
        var signal = invalid ? double.NaN : _signalMa.Next(pMacdEq, isFinal);
        if (isFinal) _signalInvalid = invalid;
        var histogram = pMacdEq - signal;

        var value = _input.GetValue(bar);
        var fastMa = _fastMa.Next(value, isFinal);
        var slowMa = _slowMa.Next(value, isFinal);

        if (isFinal)
        {
            _prevFastMa = fastMa;
            _prevSlowMa = slowMa;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Rmacd", pMacdEq },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(pMacdEq, outputs);
    }

    public void Dispose()
    {
        _fastMa.Dispose();
        _slowMa.Dispose();
        _signalMa.Dispose();
    }
}
