using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class ImpulsePercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _signalLength;
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly RollingWindowSum _signalSum;
    private readonly StreamingInputResolver _input;

    public ImpulsePercentagePriceOscillatorState(InputName inputName = InputName.TypicalPrice,
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

    public ImpulsePercentagePriceOscillatorState(MovingAvgType maType, int length, int signalLength,
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

    public IndicatorName Name => IndicatorName.ImpulsePercentagePriceOscillator;

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
        var ppo = mi > hi && hi != 0 ? macd / hi * 100 : mi < lo && lo != 0 ? macd / lo * 100 : 0;
        var ppoSum = isFinal ? _signalSum.Add(ppo, out var count) : _signalSum.Preview(ppo, out count);
        var signal = count > 0 ? ppoSum / count : 0;
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
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _signalSum.Dispose();
    }
}

public sealed class InertiaIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityIndexEngine _rviHigh;
    private readonly RelativeVolatilityIndexEngine _rviLow;
    private readonly IMovingAverageSmoother? _smoother;
    private readonly LinearRegressionState? _linreg;
    private double _rviValue;
    private readonly bool _useLinearRegression;

    public InertiaIndicatorState(MovingAvgType maType = MovingAvgType.LinearRegression, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _rviHigh = new RelativeVolatilityIndexEngine(MovingAvgType.WildersSmoothingMethod, 10, 14);
        _rviLow = new RelativeVolatilityIndexEngine(MovingAvgType.WildersSmoothingMethod, 10, 14);
        _useLinearRegression = maType == MovingAvgType.LinearRegression;
        if (_useLinearRegression)
        {
            _linreg = new LinearRegressionState(resolved, _ => _rviValue);
        }
        else
        {
            _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        }
    }

    public IndicatorName Name => IndicatorName.InertiaIndicator;

    public void Reset()
    {
        _rviHigh.Reset();
        _rviLow.Reset();
        _smoother?.Reset();
        _linreg?.Reset();
        _rviValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var rviHigh = _rviHigh.Next(bar.High, bar, isFinal);
        var rviLow = _rviLow.Next(bar.Low, bar, isFinal);
        var rvi = (rviHigh + rviLow) / 2;

        double inertia;
        if (_useLinearRegression)
        {
            _rviValue = rvi;
            inertia = _linreg!.Update(bar, isFinal, includeOutputs: false).Value;
        }
        else
        {
            inertia = _smoother!.Next(rvi, isFinal);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Inertia", inertia }
            };
        }

        return new StreamingIndicatorStateResult(inertia, outputs);
    }

    public void Dispose()
    {
        _rviHigh.Dispose();
        _rviLow.Dispose();
        _smoother?.Dispose();
        _linreg?.Dispose();
    }
}

public sealed class InformationRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly IMovingAverageSmoother _retSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _retValue;

    public InformationRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = 0.05,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _bench = MathHelper.Pow(1 + bmk, _length / 360d) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public InformationRatioState(MovingAvgType maType, int length, double bmk, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _bench = MathHelper.Pow(1 + bmk, _length / 360d) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.InformationRatio;

    public void Reset()
    {
        _retSmoother.Reset();
        _stdDev.Reset();
        _values.Clear();
        _retValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, _length);
        var ret = prevValue != 0 ? (value / prevValue) - 1 : 0;
        _retValue = ret;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var retSma = _retSmoother.Next(ret, isFinal);
        var info = stdDev != 0 ? (retSma - _bench) / stdDev : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ir", info }
            };
        }

        return new StreamingIndicatorStateResult(info, outputs);
    }

    public void Dispose()
    {
        _retSmoother.Dispose();
        _stdDev.Dispose();
        _values.Dispose();
    }
}

public sealed class InsyncIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _smaLength;
    private readonly RelativeStrengthIndexState _rsi;
    private readonly CommodityChannelIndexState _cci;
    private readonly MoneyFlowIndexState _mfi;
    private readonly MovingAverageConvergenceDivergenceState _macd;
    private readonly BollingerBandsPercentBState _pctB;
    private readonly DetrendedPriceOscillatorState _dpo;
    private readonly RateOfChangeState _roc;
    private readonly EaseOfMovementState _eom;
    private readonly RollingWindowSum _emoSum;
    private readonly RollingWindowSum _macdSum;
    private readonly RollingWindowSum _dpoSum;
    private readonly RollingWindowSum _rocSum;
    private readonly RollingWindowMax _stochHigh;
    private readonly RollingWindowMin _stochLow;
    private readonly IMovingAverageSmoother _stochFast;
    private readonly IMovingAverageSmoother _stochSlow;
    private readonly PooledRingBuffer<double> _pdoinsbValues;
    private readonly PooledRingBuffer<double> _pdoinssValues;

    public InsyncIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9, int emoLength = 14, int mfiLength = 20, int bbLength = 20,
        int cciLength = 14, int dpoLength = 18, int rocLength = 10, int rsiLength = 14, int stochLength = 14, int stochKLength = 1,
        int stochDLength = 3, int smaLength = 10, double stdDevMult = 2, double divisor = 10000)
    {
        _smaLength = Math.Max(1, smaLength);
        _rsi = new RelativeStrengthIndexState(Math.Max(1, rsiLength));
        _cci = new CommodityChannelIndexState(length: Math.Max(1, cciLength));
        _mfi = new MoneyFlowIndexState(Math.Max(1, mfiLength));
        _macd = new MovingAverageConvergenceDivergenceState(Math.Max(1, fastLength), Math.Max(1, slowLength), Math.Max(1, signalLength));
        _pctB = new BollingerBandsPercentBState(stdDevMult, MovingAvgType.SimpleMovingAverage, Math.Max(1, bbLength));
        _dpo = new DetrendedPriceOscillatorState(MovingAvgType.SimpleMovingAverage, Math.Max(1, dpoLength));
        _roc = new RateOfChangeState(Math.Max(1, rocLength));
        _eom = new EaseOfMovementState(divisor);
        _emoSum = new RollingWindowSum(_smaLength);
        _macdSum = new RollingWindowSum(_smaLength);
        _dpoSum = new RollingWindowSum(_smaLength);
        _rocSum = new RollingWindowSum(_smaLength);
        var resolvedStochLength = Math.Max(1, stochLength);
        _stochHigh = new RollingWindowMax(resolvedStochLength);
        _stochLow = new RollingWindowMin(resolvedStochLength);
        _stochFast = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, stochKLength));
        _stochSlow = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, stochDLength));
        _pdoinsbValues = new PooledRingBuffer<double>(_smaLength);
        _pdoinssValues = new PooledRingBuffer<double>(_smaLength);
    }

    public IndicatorName Name => IndicatorName.InsyncIndex;

    public void Reset()
    {
        _rsi.Reset();
        _cci.Reset();
        _mfi.Reset();
        _macd.Reset();
        _pctB.Reset();
        _dpo.Reset();
        _roc.Reset();
        _eom.Reset();
        _emoSum.Reset();
        _macdSum.Reset();
        _dpoSum.Reset();
        _rocSum.Reset();
        _stochHigh.Reset();
        _stochLow.Reset();
        _stochFast.Reset();
        _stochSlow.Reset();
        _pdoinsbValues.Clear();
        _pdoinssValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var rsi = _rsi.Update(bar, isFinal, includeOutputs: false).Value;
        var cci = _cci.Update(bar, isFinal, includeOutputs: false).Value;
        var mfi = _mfi.Update(bar, isFinal, includeOutputs: false).Value;
        var macd = _macd.Update(bar, isFinal, includeOutputs: false).Value;
        var bolins2 = _pctB.Update(bar, isFinal, includeOutputs: false).Value;
        var dpo = _dpo.Update(bar, isFinal, includeOutputs: false).Value;
        var roc = _roc.Update(bar, isFinal, includeOutputs: false).Value;
        var emo = _eom.Update(bar, isFinal, includeOutputs: false).Value;

        var prevPdoinss10 = EhlersStreamingWindow.GetOffsetValue(_pdoinssValues, _smaLength);
        var prevPdoinsb10 = EhlersStreamingWindow.GetOffsetValue(_pdoinsbValues, _smaLength);

        double bolinsll = bolins2 < 0.05 ? -5 : bolins2 > 0.95 ? 5 : 0;
        double cciins = cci > 100 ? 5 : cci < -100 ? -5 : 0;

        var emoSum = isFinal ? _emoSum.Add(emo, out var emoCount) : _emoSum.Preview(emo, out emoCount);
        var emoSma = emoCount > 0 ? emoSum / emoCount : 0;
        var emvins2 = emo - emoSma;
        double emvinsb = emvins2 < 0 ? emoSma < 0 ? -5 : 0 : emoSma > 0 ? 5 : 0;

        var macdSum = isFinal ? _macdSum.Add(macd, out var macdCount) : _macdSum.Preview(macd, out macdCount);
        var macdSma = macdCount > 0 ? macdSum / macdCount : 0;
        var macdins2 = macd - macdSma;
        double macdinsb = macdins2 < 0 ? macdSma < 0 ? -5 : 0 : macdSma > 0 ? 5 : 0;
        double mfiins = mfi > 80 ? 5 : mfi < 20 ? -5 : 0;

        var dpoSum = isFinal ? _dpoSum.Add(dpo, out var dpoCount) : _dpoSum.Preview(dpo, out dpoCount);
        var dpoSma = dpoCount > 0 ? dpoSum / dpoCount : 0;
        var pdoins2 = dpo - dpoSma;
        double pdoinsb = pdoins2 < 0 ? dpoSma < 0 ? -5 : 0 : dpoSma > 0 ? 5 : 0;
        double pdoinss = pdoins2 > 0 ? dpoSma > 0 ? 5 : 0 : dpoSma < 0 ? -5 : 0;

        var rocSum = isFinal ? _rocSum.Add(roc, out var rocCount) : _rocSum.Preview(roc, out rocCount);
        var rocSma = rocCount > 0 ? rocSum / rocCount : 0;
        var rocins2 = roc - rocSma;
        double rocinsb = rocins2 < 0 ? rocSma < 0 ? -5 : 0 : rocSma > 0 ? 5 : 0;
        double rsiins = rsi > 70 ? 5 : rsi < 30 ? -5 : 0;

        var highestHigh = isFinal ? _stochHigh.Add(bar.High, out _) : _stochHigh.Preview(bar.High, out _);
        var lowestLow = isFinal ? _stochLow.Add(bar.Low, out _) : _stochLow.Preview(bar.Low, out _);
        var range = highestHigh - lowestLow;
        var fastK = range != 0 ? MathHelper.MinOrMax((bar.Close - lowestLow) / range * 100, 100, 0) : 0;
        var fastD = _stochFast.Next(fastK, isFinal);
        var slowD = _stochSlow.Next(fastD, isFinal);

        double stopdins = slowD > 80 ? 5 : slowD < 20 ? -5 : 0;
        double stopkins = fastD > 80 ? 5 : fastD < 20 ? -5 : 0;

        var iidx = 50 + cciins + bolinsll + rsiins + stopkins + stopdins + mfiins + emvinsb + rocinsb + prevPdoinss10 +
            prevPdoinsb10 + macdinsb;

        if (isFinal)
        {
            _pdoinsbValues.TryAdd(pdoinsb, out _);
            _pdoinssValues.TryAdd(pdoinss, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Iidx", iidx }
            };
        }

        return new StreamingIndicatorStateResult(iidx, outputs);
    }

    public void Dispose()
    {
        _cci.Dispose();
        _mfi.Dispose();
        _pctB.Dispose();
        _dpo.Dispose();
        _roc.Dispose();
        _emoSum.Dispose();
        _macdSum.Dispose();
        _dpoSum.Dispose();
        _rocSum.Dispose();
        _stochHigh.Dispose();
        _stochLow.Dispose();
        _stochFast.Dispose();
        _stochSlow.Dispose();
        _pdoinsbValues.Dispose();
        _pdoinssValues.Dispose();
    }
}

public sealed class InternalBarStrengthIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _smoothLength;
    private readonly RollingWindowSum _ibsSum;
    private readonly StreamingInputResolver _input;
    private double _prevIbsiEma;

    public InternalBarStrengthIndicatorState(int length = 14, int smoothLength = 3, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smoothLength = Math.Max(1, smoothLength);
        _ibsSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public InternalBarStrengthIndicatorState(int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smoothLength = Math.Max(1, smoothLength);
        _ibsSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.InternalBarStrengthIndicator;

    public void Reset()
    {
        _ibsSum.Reset();
        _prevIbsiEma = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;
        var ibs = high - low != 0 ? (close - low) / (high - low) * 100 : 0;
        var ibsSum = isFinal ? _ibsSum.Add(ibs, out var count) : _ibsSum.Preview(ibs, out count);
        var ibsi = count > 0 ? ibsSum / count : 0;
        var ibsiEma = CalculationsHelper.CalculateEMA(ibsi, _prevIbsiEma, _smoothLength);

        if (isFinal)
        {
            _prevIbsiEma = ibsiEma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ibs", ibsi },
                { "Signal", ibsiEma }
            };
        }

        return new StreamingIndicatorStateResult(ibsi, outputs);
    }

    public void Dispose()
    {
        _ibsSum.Dispose();
    }
}

public sealed class InterquartileRangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _order;

    public InterquartileRangeBandsState(int length = 14, double mult = 1.5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public InterquartileRangeBandsState(int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mult = mult;
        _order = new RollingOrderStatistic(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.InterquartileRangeBands;

    public void Reset()
    {
        _order.Dispose();
        _order = new RollingOrderStatistic(_length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (isFinal)
        {
            _order.Add(value);
        }

        var q1 = _order.PercentileNearestRank(25);
        var q3 = _order.PercentileNearestRank(75);
        var iqr = q3 - q1;
        var upper = q3 + (_mult * iqr);
        var lower = q1 - (_mult * iqr);
        var middle = (upper + lower) / 2;

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
        _order.Dispose();
    }
}

public sealed class InverseDistanceWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public InverseDistanceWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public InverseDistanceWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.InverseDistanceWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            double weight = 0;
            for (var k = 0; k <= _length - 1; k++)
            {
                var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, k);
                weight += Math.Abs(prevValue - prevValue2);
            }

            sum += prevValue * weight;
            weightedSum += weight;
        }

        var idwma = weightedSum != 0 ? sum / weightedSum : 0;
        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Idwma", idwma }
            };
        }

        return new StreamingIndicatorStateResult(idwma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class InverseFisherFastZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly FastZScoreState _fastZScore;

    public InverseFisherFastZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50,
        InputName inputName = InputName.Close)
    {
        _fastZScore = new FastZScoreState(maType, length, inputName);
    }

    public InverseFisherFastZScoreState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastZScore = new FastZScoreState(maType, length, selector);
    }

    public IndicatorName Name => IndicatorName.InverseFisherFastZScore;

    public void Reset()
    {
        _fastZScore.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var fz = _fastZScore.Update(bar, isFinal, includeOutputs: false).Value;
        var exp = MathHelper.Exp(10 * fz);
        var ifz = exp + 1 != 0 ? (exp - 1) / (exp + 1) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Iffzs", ifz }
            };
        }

        return new StreamingIndicatorStateResult(ifz, outputs);
    }

    public void Dispose()
    {
        _fastZScore.Dispose();
    }
}

public sealed class InverseFisherZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;

    public InverseFisherZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public InverseFisherZScoreState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.InverseFisherZScore;

    public void Reset()
    {
        _sma.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var z = stdDev != 0 ? (value - sma) / stdDev : 0;
        var expZ = MathHelper.Exp(2 * z);
        var f = expZ + 1 != 0 ? MathHelper.MinOrMax((((expZ - 1) / (expZ + 1)) + 1) * 50, 100, 0) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ifzs", f }
            };
        }

        return new StreamingIndicatorStateResult(f, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class JapaneseCorrelationCoefficientState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _length1;
    private readonly IMovingAverageSmoother _highSmoother;
    private readonly IMovingAverageSmoother _lowSmoother;
    private readonly IMovingAverageSmoother _closeSmoother;
    private readonly RollingWindowMax _highest;
    private readonly RollingWindowMin _lowest;
    private readonly PooledRingBuffer<double> _closeValues;
    private readonly StreamingInputResolver _input;

    public JapaneseCorrelationCoefficientState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling(_length / 2d));
        _highSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _highest = new RollingWindowMax(_length1);
        _lowest = new RollingWindowMin(_length1);
        _closeValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public JapaneseCorrelationCoefficientState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling(_length / 2d));
        _highSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _lowSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _highest = new RollingWindowMax(_length1);
        _lowest = new RollingWindowMin(_length1);
        _closeValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.JapaneseCorrelationCoefficient;

    public void Reset()
    {
        _highSmoother.Reset();
        _lowSmoother.Reset();
        _closeSmoother.Reset();
        _highest.Reset();
        _lowest.Reset();
        _closeValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highMa = _highSmoother.Next(bar.High, isFinal);
        var lowMa = _lowSmoother.Next(bar.Low, isFinal);
        var closeMa = _closeSmoother.Next(value, isFinal);
        var highest = isFinal ? _highest.Add(highMa, out _) : _highest.Preview(highMa, out _);
        var lowest = isFinal ? _lowest.Add(lowMa, out _) : _lowest.Preview(lowMa, out _);
        var prevC = EhlersStreamingWindow.GetOffsetValue(_closeValues, closeMa, _length);
        var jo = highest - lowest != 0 ? (closeMa - prevC) / (highest - lowest) : 0;

        if (isFinal)
        {
            _closeValues.TryAdd(closeMa, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Jo", jo }
            };
        }

        return new StreamingIndicatorStateResult(jo, outputs);
    }

    public void Dispose()
    {
        _highSmoother.Dispose();
        _lowSmoother.Dispose();
        _closeSmoother.Dispose();
        _highest.Dispose();
        _lowest.Dispose();
        _closeValues.Dispose();
    }
}

public sealed class JmaRsxCloneState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _f18;
    private readonly double _f20;
    private readonly StreamingInputResolver _input;
    private double _f8;
    private double _f28;
    private double _f30;
    private double _f38;
    private double _f40;
    private double _f48;
    private double _f50;
    private double _f58;
    private double _f60;
    private double _f68;
    private double _f70;
    private double _f78;
    private double _f80;
    private double _f90;

    public JmaRsxCloneState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _f18 = 3d / (_length + 2);
        _f20 = 1 - _f18;
        _input = new StreamingInputResolver(inputName, null);
    }

    public JmaRsxCloneState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _f18 = 3d / (_length + 2);
        _f20 = 1 - _f18;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.JmaRsxClone;

    public void Reset()
    {
        _f8 = 0;
        _f28 = 0;
        _f30 = 0;
        _f38 = 0;
        _f40 = 0;
        _f48 = 0;
        _f50 = 0;
        _f58 = 0;
        _f60 = 0;
        _f68 = 0;
        _f70 = 0;
        _f78 = 0;
        _f80 = 0;
        _f90 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevF8 = _f8;
        var f8 = 100 * value;
        var f10 = prevF8;
        var v8 = f8 - f10;

        var f28 = (_f20 * _f28) + (_f18 * v8);
        var f30 = (_f18 * f28) + (_f20 * _f30);
        var vC = (f28 * 1.5) - (f30 * 0.5);
        var f38 = (_f20 * _f38) + (_f18 * vC);
        var f40 = (_f18 * f38) + (_f20 * _f40);
        var v10 = (f38 * 1.5) - (f40 * 0.5);
        var f48 = (_f20 * _f48) + (_f18 * v10);
        var f50 = (_f18 * f48) + (_f20 * _f50);
        var v14 = (f48 * 1.5) - (f50 * 0.5);
        var f58 = (_f20 * _f58) + (_f18 * Math.Abs(v8));
        var f60 = (_f18 * f58) + (_f20 * _f60);
        var v18 = (f58 * 1.5) - (f60 * 0.5);
        var f68 = (_f20 * _f68) + (_f18 * v18);
        var f70 = (_f18 * f68) + (_f20 * _f70);
        var v1C = (f68 * 1.5) - (f70 * 0.5);
        var f78 = (_f20 * _f78) + (_f18 * v1C);
        var f80 = (_f18 * f78) + (_f20 * _f80);
        var v20 = (f78 * 1.5) - (f80 * 0.5);

        var prevF90_ = _f90;
        var prevF88 = 0d;
        var f90_ = prevF90_ == 0 ? 1 : prevF88 <= prevF90_ ? prevF88 + 1 : prevF90_ + 1;

        double f88 = prevF90_ == 0 && _length - 1 >= 5 ? _length - 1 : 5;
        double f0 = f88 >= f90_ && f8 != f10 ? 1 : 0;
        var f90 = f88 == f90_ && f0 == 0 ? 0 : f90_;
        var v4_ = f88 < f90 && v20 > 0 ? MathHelper.MinOrMax(((v14 / v20) + 1) * 50, 100, 0) : 50;
        var rsx = v4_ > 100 ? 100 : v4_ < 0 ? 0 : v4_;

        if (isFinal)
        {
            _f8 = f8;
            _f28 = f28;
            _f30 = f30;
            _f38 = f38;
            _f40 = f40;
            _f48 = f48;
            _f50 = f50;
            _f58 = f58;
            _f60 = f60;
            _f68 = f68;
            _f70 = f70;
            _f78 = f78;
            _f80 = f80;
            _f90 = f90_;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rsx", rsx }
            };
        }

        return new StreamingIndicatorStateResult(rsx, outputs);
    }
}

public sealed class JrcFractalDimensionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _smoothLength;
    private readonly int _wind1;
    private readonly int _wind2;
    private readonly double _nLog;
    private readonly RollingWindowMax _highest1;
    private readonly RollingWindowMin _lowest1;
    private readonly RollingWindowMax _highest2;
    private readonly RollingWindowMin _lowest2;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _smallRanges;
    private readonly IMovingAverageSmoother _fdSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevSmallSum;
    private int _index;

    public JrcFractalDimensionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 20, int length2 = 5, int smoothLength = 5, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _smoothLength = Math.Max(1, smoothLength);
        _wind1 = MathHelper.MinOrMax((_length2 - 1) * _length1);
        _wind2 = MathHelper.MinOrMax(_length2 * _length1);
        _nLog = Math.Log(_length2);
        _highest1 = new RollingWindowMax(_length1);
        _lowest1 = new RollingWindowMin(_length1);
        _highest2 = new RollingWindowMax(_wind2);
        _lowest2 = new RollingWindowMin(_wind2);
        var capacity = Math.Max(_wind2, _length1);
        _values = new PooledRingBuffer<double>(capacity);
        _smallRanges = new PooledRingBuffer<double>(_wind1);
        _fdSmoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public JrcFractalDimensionState(MovingAvgType maType, int length1, int length2, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _smoothLength = Math.Max(1, smoothLength);
        _wind1 = MathHelper.MinOrMax((_length2 - 1) * _length1);
        _wind2 = MathHelper.MinOrMax(_length2 * _length1);
        _nLog = Math.Log(_length2);
        _highest1 = new RollingWindowMax(_length1);
        _lowest1 = new RollingWindowMin(_length1);
        _highest2 = new RollingWindowMax(_wind2);
        _lowest2 = new RollingWindowMin(_wind2);
        var capacity = Math.Max(_wind2, _length1);
        _values = new PooledRingBuffer<double>(capacity);
        _smallRanges = new PooledRingBuffer<double>(_wind1);
        _fdSmoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.JrcFractalDimension;

    public void Reset()
    {
        _highest1.Reset();
        _lowest1.Reset();
        _highest2.Reset();
        _lowest2.Reset();
        _values.Clear();
        _smallRanges.Clear();
        _fdSmoother.Reset();
        _signalSmoother.Reset();
        _prevSmallSum = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest1 = isFinal ? _highest1.Add(bar.High, out _) : _highest1.Preview(bar.High, out _);
        var lowest1 = isFinal ? _lowest1.Add(bar.Low, out _) : _lowest1.Preview(bar.Low, out _);
        var highest2 = isFinal ? _highest2.Add(bar.High, out _) : _highest2.Preview(bar.High, out _);
        var lowest2 = isFinal ? _lowest2.Add(bar.Low, out _) : _lowest2.Preview(bar.Low, out _);

        var prevValue1 = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var prevValue2 = EhlersStreamingWindow.GetOffsetValue(_values, value, _wind2);
        var bigRange = Math.Max(prevValue2, highest2) - Math.Min(prevValue2, lowest2);

        var prevSmallRange = EhlersStreamingWindow.GetOffsetValue(_smallRanges, _wind1);
        var smallRange = Math.Max(prevValue1, highest1) - Math.Min(prevValue1, lowest1);
        var prevSmallSum = _index >= 1 ? _prevSmallSum : smallRange;
        var smallSum = prevSmallSum + smallRange - prevSmallRange;

        var value1 = _wind1 != 0 ? smallSum / _wind1 : 0;
        var value2 = value1 != 0 ? bigRange / value1 : 0;
        var temp = value2 > 0 ? Math.Log(value2) : 0;
        var fd = _nLog != 0 ? 2 - (temp / _nLog) : 0;

        var jrcfd = _fdSmoother.Next(fd, isFinal);
        var signal = _signalSmoother.Next(jrcfd, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _smallRanges.TryAdd(smallRange, out _);
            _prevSmallSum = smallSum;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Jrcfd", jrcfd },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(jrcfd, outputs);
    }

    public void Dispose()
    {
        _highest1.Dispose();
        _lowest1.Dispose();
        _highest2.Dispose();
        _lowest2.Dispose();
        _values.Dispose();
        _smallRanges.Dispose();
        _fdSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class JsaMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public JsaMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public JsaMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.JsaMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var jma = (value + priorValue) / 2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Jma", jma }
            };
        }

        return new StreamingIndicatorStateResult(jma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class JurikMovingAverageState : IStreamingIndicatorState
{
    private readonly double _phaseRatio;
    private readonly double _alpha;
    private readonly double _beta;
    private readonly StreamingInputResolver _input;
    private double _e0;
    private double _e1;
    private double _e2;
    private double _jma;

    public JurikMovingAverageState(int length = 7, double phase = 50, double power = 2,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : (phase / 100) + 1.5;
        var ratio = 0.45 * (resolved - 1);
        _beta = ratio / (ratio + 2);
        _alpha = MathHelper.Pow(_beta, power);
        _input = new StreamingInputResolver(inputName, null);
    }

    public JurikMovingAverageState(int length, double phase, double power, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : (phase / 100) + 1.5;
        var ratio = 0.45 * (resolved - 1);
        _beta = ratio / (ratio + 2);
        _alpha = MathHelper.Pow(_beta, power);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.JurikMovingAverage;

    public void Reset()
    {
        _e0 = 0;
        _e1 = 0;
        _e2 = 0;
        _jma = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevJma = _jma;
        var e0 = ((1 - _alpha) * value) + (_alpha * _e0);
        var e1 = ((value - e0) * (1 - _beta)) + (_beta * _e1);
        var e2 = ((e0 + (_phaseRatio * e1) - prevJma) * MathHelper.Pow(1 - _alpha, 2)) +
                 (MathHelper.Pow(_alpha, 2) * _e2);
        var jma = e2 + prevJma;

        if (isFinal)
        {
            _e0 = e0;
            _e1 = e1;
            _e2 = e2;
            _jma = jma;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Jma", jma }
            };
        }

        return new StreamingIndicatorStateResult(jma, outputs);
    }
}

public sealed class KalmanSmootherState : IStreamingIndicatorState
{
    private readonly double _smoothFactor;
    private readonly double _veloFactor;
    private readonly StreamingInputResolver _input;
    private double _prevVelo;
    private double _prevKf;
    private bool _hasPrev;

    public KalmanSmootherState(int length = 200, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _smoothFactor = MathHelper.Sqrt((resolved / 10000d) * 2);
        _veloFactor = resolved / 10000d;
        _input = new StreamingInputResolver(inputName, null);
    }

    public KalmanSmootherState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _smoothFactor = MathHelper.Sqrt((resolved / 10000d) * 2);
        _veloFactor = resolved / 10000d;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KalmanSmoother;

    public void Reset()
    {
        _prevVelo = 0;
        _prevKf = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevKf = _hasPrev ? _prevKf : value;
        var dk = value - prevKf;
        var smooth = prevKf + (dk * _smoothFactor);
        var velo = _prevVelo + (_veloFactor * dk);
        var kf = smooth + velo;

        if (isFinal)
        {
            _prevVelo = velo;
            _prevKf = kf;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ks", kf }
            };
        }

        return new StreamingIndicatorStateResult(kf, outputs);
    }
}

public sealed class KarobeinOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly IMovingAverageSmoother _aSmoother;
    private readonly IMovingAverageSmoother _bSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevEma;
    private bool _hasPrev;

    public KarobeinOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 50, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        _aSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KarobeinOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        _aSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _bSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KarobeinOscillator;

    public void Reset()
    {
        _ema.Reset();
        _aSmoother.Reset();
        _bSmoother.Reset();
        _prevEma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var prevEma = _hasPrev ? _prevEma : 0;
        var aRaw = ema < prevEma && prevEma != 0 ? ema / prevEma : 0;
        var bRaw = ema > prevEma && prevEma != 0 ? ema / prevEma : 0;
        var a = _aSmoother.Next(aRaw, isFinal);
        var b = _bSmoother.Next(bRaw, isFinal);
        var ratio = prevEma != 0 && ema != 0 ? ema / prevEma : 0;
        var c = prevEma != 0 && ema != 0 ? MathHelper.MinOrMax(ratio / (ratio + b), 1, 0) : 0;
        var d = prevEma != 0 && ema != 0
            ? MathHelper.MinOrMax((2 * (ratio / (ratio + (c * a)))) - 1, 1, 0)
            : 0;

        if (isFinal)
        {
            _prevEma = ema;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ko", d }
            };
        }

        return new StreamingIndicatorStateResult(d, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _aSmoother.Dispose();
        _bSmoother.Dispose();
    }
}

public sealed class KaseConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly KasePeakOscillatorV1Engine _engine;
    private readonly IMovingAverageSmoother _pkSmoother;

    public KaseConvergenceDivergenceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 30, int length2 = 3, int length3 = 8)
    {
        _engine = new KasePeakOscillatorV1Engine(Math.Max(1, length1), Math.Max(1, length2));
        _pkSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
    }

    public IndicatorName Name => IndicatorName.KaseConvergenceDivergence;

    public void Reset()
    {
        _engine.Reset();
        _pkSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var pk = _engine.Next(bar, isFinal, out _, out _);
        var pkSma = _pkSmoother.Next(pk, isFinal);
        var kcd = pk - pkSma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Kcd", kcd }
            };
        }

        return new StreamingIndicatorStateResult(kcd, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _pkSmoother.Dispose();
    }
}

public sealed class KaseDevStopV1State : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _dtrAvg;
    private readonly StandardDeviationVolatilityState _dtrStd;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly PooledRingBuffer<double> _closeValues;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDev1;
    private readonly double _stdDev2;
    private readonly double _stdDev3;
    private readonly double _stdDev4;
    private double _dtrValue;

    public KaseDevStopV1State(InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 21, int length = 20,
        double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolved = Math.Max(1, length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _dtrAvg = MovingAverageSmootherFactory.Create(maType, resolved);
        _dtrStd = new StandardDeviationVolatilityState(maType, resolved, _ => _dtrValue);
        _lowValues = new PooledRingBuffer<double>(2);
        _closeValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(inputName, null);
        _stdDev1 = stdDev1;
        _stdDev2 = stdDev2;
        _stdDev3 = stdDev3;
        _stdDev4 = stdDev4;
    }

    public KaseDevStopV1State(MovingAvgType maType, int fastLength, int slowLength, int length,
        double stdDev1, double stdDev2, double stdDev3, double stdDev4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolved = Math.Max(1, length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _dtrAvg = MovingAverageSmootherFactory.Create(maType, resolved);
        _dtrStd = new StandardDeviationVolatilityState(maType, resolved, _ => _dtrValue);
        _lowValues = new PooledRingBuffer<double>(2);
        _closeValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _stdDev1 = stdDev1;
        _stdDev2 = stdDev2;
        _stdDev3 = stdDev3;
        _stdDev4 = stdDev4;
    }

    public IndicatorName Name => IndicatorName.KaseDevStopV1;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _dtrAvg.Reset();
        _dtrStd.Reset();
        _lowValues.Clear();
        _closeValues.Clear();
        _dtrValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = EhlersStreamingWindow.GetOffsetValue(_closeValues, 2);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, 2);
        var dtr = Math.Max(Math.Max(bar.High - prevLow, Math.Abs(bar.High - prevClose)), Math.Abs(bar.Low - prevClose));
        _dtrValue = dtr;
        var dtrAvg = _dtrAvg.Next(dtr, isFinal);
        var dtrStd = _dtrStd.Update(bar, isFinal, includeOutputs: false).Value;
        var maFast = _fastSmoother.Next(value, isFinal);
        var maSlow = _slowSmoother.Next(value, isFinal);

        var warningLine = maFast < maSlow
            ? value + dtrAvg + (_stdDev1 * dtrStd)
            : value - dtrAvg - (_stdDev1 * dtrStd);
        var dev1 = maFast < maSlow
            ? value + dtrAvg + (_stdDev2 * dtrStd)
            : value - dtrAvg - (_stdDev2 * dtrStd);
        var dev2 = maFast < maSlow
            ? value + dtrAvg + (_stdDev3 * dtrStd)
            : value - dtrAvg - (_stdDev3 * dtrStd);
        var dev3 = maFast < maSlow
            ? value + dtrAvg + (_stdDev4 * dtrStd)
            : value - dtrAvg - (_stdDev4 * dtrStd);

        if (isFinal)
        {
            _lowValues.TryAdd(bar.Low, out _);
            _closeValues.TryAdd(bar.Close, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Dev1", dev1 },
                { "Dev2", dev2 },
                { "Dev3", dev3 },
                { "WarningLine", warningLine }
            };
        }

        return new StreamingIndicatorStateResult(dev1, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _dtrAvg.Dispose();
        _dtrStd.Dispose();
        _lowValues.Dispose();
        _closeValues.Dispose();
    }
}

public sealed class KaseDevStopV2State : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _rangeAvg;
    private readonly StandardDeviationVolatilityState _rangeStd;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly PooledRingBuffer<double> _inputValues;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDev1;
    private readonly double _stdDev2;
    private readonly double _stdDev3;
    private readonly double _stdDev4;
    private double _rangeValue;

    public KaseDevStopV2State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 10, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1,
        double stdDev3 = 2.2, double stdDev4 = 3.6, InputName inputName = InputName.Close)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolved = Math.Max(1, length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _rangeAvg = MovingAverageSmootherFactory.Create(maType, resolved);
        _rangeStd = new StandardDeviationVolatilityState(maType, resolved, _ => _rangeValue);
        _highValues = new PooledRingBuffer<double>(2);
        _lowValues = new PooledRingBuffer<double>(2);
        _inputValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(inputName, null);
        _stdDev1 = stdDev1;
        _stdDev2 = stdDev2;
        _stdDev3 = stdDev3;
        _stdDev4 = stdDev4;
    }

    public KaseDevStopV2State(MovingAvgType maType, int fastLength, int slowLength, int length, double stdDev1,
        double stdDev2, double stdDev3, double stdDev4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolved = Math.Max(1, length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _rangeAvg = MovingAverageSmootherFactory.Create(maType, resolved);
        _rangeStd = new StandardDeviationVolatilityState(maType, resolved, _ => _rangeValue);
        _highValues = new PooledRingBuffer<double>(2);
        _lowValues = new PooledRingBuffer<double>(2);
        _inputValues = new PooledRingBuffer<double>(2);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _stdDev1 = stdDev1;
        _stdDev2 = stdDev2;
        _stdDev3 = stdDev3;
        _stdDev4 = stdDev4;
    }

    public IndicatorName Name => IndicatorName.KaseDevStopV2;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _rangeAvg.Reset();
        _rangeStd.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _inputValues.Clear();
        _rangeValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var maFast = _fastSmoother.Next(value, isFinal);
        var maSlow = _slowSmoother.Next(value, isFinal);
        double trend = maFast > maSlow ? 1 : -1;
        var price = trend == 1 ? bar.High : bar.Low;
        price = trend > 0 ? Math.Max(price, bar.High) : Math.Min(price, bar.Low);

        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, 1);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, 1);
        var prevClose = EhlersStreamingWindow.GetOffsetValue(_inputValues, 2);
        var mmax = Math.Max(Math.Max(bar.High, prevHigh), prevClose);
        var mmin = Math.Min(Math.Min(bar.Low, prevLow), prevClose);
        var rrange = mmax - mmin;
        _rangeValue = rrange;
        var avg = _rangeAvg.Next(rrange, isFinal);
        var dev = _rangeStd.Update(bar, isFinal, includeOutputs: false).Value;

        var val = (price + (-1 * trend)) * (avg + (_stdDev1 * dev));
        var val1 = (price + (-1 * trend)) * (avg + (_stdDev2 * dev));
        var val2 = (price + (-1 * trend)) * (avg + (_stdDev3 * dev));
        var val3 = (price + (-1 * trend)) * (avg + (_stdDev4 * dev));

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _inputValues.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Dev1", val },
                { "Dev2", val1 },
                { "Dev3", val2 },
                { "Dev4", val3 }
            };
        }

        return new StreamingIndicatorStateResult(val, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _rangeAvg.Dispose();
        _rangeStd.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
        _inputValues.Dispose();
    }
}

public sealed class KaseIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _volumeSma;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly double _sqrtLength;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private double _prevKUp;
    private double _prevKDown;
    private bool _hasPrev;

    public KaseIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        var resolved = Math.Max(1, length);
        _volumeSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _sqrtLength = MathHelper.Sqrt(resolved);
    }

    public IndicatorName Name => IndicatorName.KaseIndicator;

    public void Reset()
    {
        _volumeSma.Reset();
        _atrSmoother.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevKUp = 0;
        _prevKDown = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var volumeSma = _volumeSma.Next(bar.Volume, isFinal);
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);

        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var ratio = volumeSma * _sqrtLength;
        var kUp = atr > 0 && ratio != 0 && bar.Low != 0 ? prevHigh / bar.Low / ratio : _prevKUp;
        var kDown = atr > 0 && ratio != 0 && prevLow != 0 ? bar.High / prevLow / ratio : _prevKDown;

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevKUp = kUp;
            _prevKDown = kDown;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "KaseUp", kUp },
                { "KaseDn", kDown }
            };
        }

        return new StreamingIndicatorStateResult(kUp, outputs);
    }

    public void Dispose()
    {
        _volumeSma.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class KasePeakOscillatorV1State : IStreamingIndicatorState, IDisposable
{
    private readonly KasePeakOscillatorV1Engine _engine;
    private double _prevPk;

    public KasePeakOscillatorV1State(int length = 30, int smoothLength = 3)
    {
        _engine = new KasePeakOscillatorV1Engine(Math.Max(1, length), Math.Max(1, smoothLength));
    }

    public IndicatorName Name => IndicatorName.KasePeakOscillatorV1;

    public void Reset()
    {
        _engine.Reset();
        _prevPk = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var pk = _engine.Next(bar, isFinal, out var mn, out var sd);
        var v1 = mn + (1.33 * sd) > 2.08 ? mn + (1.33 * sd) : 2.08;
        var v2 = mn - (1.33 * sd) < -1.92 ? mn - (1.33 * sd) : -1.92;
        var prevPk = _prevPk;
        var ln = prevPk >= 0 && pk > 0 ? v1 : prevPk <= 0 && pk < 0 ? v2 : 0;

        if (isFinal)
        {
            _prevPk = pk;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Kpo", ln },
                { "Pk", pk }
            };
        }

        return new StreamingIndicatorStateResult(ln, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class KasePeakOscillatorV2State : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly double _sensitivity;
    private readonly StandardDeviationVolatilityState _ccDev;
    private readonly IMovingAverageSmoother _ccDevAvg;
    private readonly RollingWindowSum _x1Sum;
    private readonly RollingWindowSum _x2Sum;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly StreamingInputResolver _input;
    private double _ccLogValue;
    private double _prevValue;
    private bool _hasPrev;

    public KasePeakOscillatorV2State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 8, int slowLength = 65, int length1 = 9, int length2 = 30, int length3 = 50, int smoothLength = 3,
        double devFactor = 2, double sensitivity = 40, InputName inputName = InputName.Close)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _sensitivity = sensitivity;
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _ccDev = new StandardDeviationVolatilityState(maType, resolvedLength1, _ => _ccLogValue);
        _ccDevAvg = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
        _x1Sum = new RollingWindowSum(resolvedSmooth);
        _x2Sum = new RollingWindowSum(resolvedSmooth);
        _highValues = new PooledRingBuffer<double>(_slowLength);
        _lowValues = new PooledRingBuffer<double>(_slowLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KasePeakOscillatorV2State(MovingAvgType maType, int fastLength, int slowLength, int length1, int length2, int length3,
        int smoothLength, double devFactor, double sensitivity, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _sensitivity = sensitivity;
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _ccDev = new StandardDeviationVolatilityState(maType, resolvedLength1, _ => _ccLogValue);
        _ccDevAvg = MovingAverageSmootherFactory.Create(maType, resolvedLength2);
        _x1Sum = new RollingWindowSum(resolvedSmooth);
        _x2Sum = new RollingWindowSum(resolvedSmooth);
        _highValues = new PooledRingBuffer<double>(_slowLength);
        _lowValues = new PooledRingBuffer<double>(_slowLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KasePeakOscillatorV2;

    public void Reset()
    {
        _ccDev.Reset();
        _ccDevAvg.Reset();
        _x1Sum.Reset();
        _x2Sum.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _ccLogValue = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var temp = prevValue != 0 ? value / prevValue : 0;
        _ccLogValue = temp > 0 ? Math.Log(temp) : 0;
        var ccDev = _ccDev.Update(bar, isFinal, includeOutputs: false).Value;
        var ccDevAvg = _ccDevAvg.Next(ccDev, isFinal);

        double max1 = 0;
        double max2 = 0;
        for (var j = _fastLength; j < _slowLength; j++)
        {
            var sqrtK = MathHelper.Sqrt(j);
            var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, j);
            var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, j);
            var temp1 = prevLow != 0 ? bar.High / prevLow : 0;
            var log1 = temp1 > 0 ? Math.Log(temp1) : 0;
            max1 = Math.Max(log1 / sqrtK, max1);
            var temp2 = bar.Low != 0 ? prevHigh / bar.Low : 0;
            var log2 = temp2 > 0 ? Math.Log(temp2) : 0;
            max2 = Math.Max(log2 / sqrtK, max2);
        }

        var x1 = ccDevAvg != 0 ? max1 / ccDevAvg : 0;
        var x1Sum = isFinal ? _x1Sum.Add(x1, out var x1Count) : _x1Sum.Preview(x1, out x1Count);
        var x1Avg = x1Count > 0 ? x1Sum / x1Count : 0;
        var x2 = ccDevAvg != 0 ? max2 / ccDevAvg : 0;
        var x2Sum = isFinal ? _x2Sum.Add(x2, out var x2Count) : _x2Sum.Preview(x2, out x2Count);
        var x2Avg = x2Count > 0 ? x2Sum / x2Count : 0;
        var xp = _sensitivity * (x1Avg - x2Avg);

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Kpo", xp }
            };
        }

        return new StreamingIndicatorStateResult(xp, outputs);
    }

    public void Dispose()
    {
        _ccDev.Dispose();
        _ccDevAvg.Dispose();
        _x1Sum.Dispose();
        _x2Sum.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class KaseSerialDependencyIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly StreamingInputResolver _input;
    private double _tempLog;
    private double _prevValue;
    private bool _hasPrev;

    public KaseSerialDependencyIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, _ => _tempLog);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KaseSerialDependencyIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, _ => _tempLog);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaseSerialDependencyIndex;

    public void Reset()
    {
        _stdDev.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _tempLog = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var temp = prevValue != 0 ? value / prevValue : 0;
        _tempLog = temp > 0 ? Math.Log(temp) : 0;
        var volatility = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;

        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, _length);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, _length);
        var ksdiUpTemp = prevLow != 0 ? bar.High / prevLow : 0;
        var ksdiDownTemp = prevHigh != 0 ? bar.Low / prevHigh : 0;
        var ksdiUpLog = ksdiUpTemp > 0 ? Math.Log(ksdiUpTemp) : 0;
        var ksdiDownLog = ksdiDownTemp > 0 ? Math.Log(ksdiDownTemp) : 0;
        var ksdiUp = volatility != 0 ? ksdiUpLog / volatility : 0;
        var ksdiDown = volatility != 0 ? ksdiDownLog / volatility : 0;

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "KsdiUp", ksdiUp },
                { "KsdiDn", ksdiDown }
            };
        }

        return new StreamingIndicatorStateResult(ksdiUp, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class KaufmanAdaptiveBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;
    private double _prevMiddle;
    private double _prevPowMa;

    public KaufmanAdaptiveBandsState(int length = 100, double stdDevFactor = 3, InputName inputName = InputName.Close)
    {
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(inputName, null);
    }

    public KaufmanAdaptiveBandsState(int length, double stdDevFactor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _er = new EfficiencyRatioState(Math.Max(1, length));
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveBands;

    public void Reset()
    {
        _er.Reset();
        _prevMiddle = 0;
        _prevPowMa = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var erPow = MathHelper.Pow(er, _stdDevFactor);
        var middle = (value * erPow) + ((1 - erPow) * _prevMiddle);
        var powMa = (MathHelper.Pow(value, 2) * erPow) + ((1 - erPow) * _prevPowMa);
        var middleSq = middle * middle;
        var dev = powMa - middleSq >= 0 ? MathHelper.Sqrt(powMa - middleSq) : 0;
        var upper = middle + dev;
        var lower = middle - dev;

        if (isFinal)
        {
            _prevMiddle = middle;
            _prevPowMa = powMa;
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
        _er.Dispose();
    }
}

public sealed class KaufmanAdaptiveCorrelationOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _srcMa;
    private readonly IMovingAverageSmoother _indexMa;
    private readonly IMovingAverageSmoother _indexSrcMa;
    private readonly IMovingAverageSmoother _index2Ma;
    private readonly IMovingAverageSmoother _src2Ma;
    private readonly StreamingInputResolver _input;
    private int _index;

    public KaufmanAdaptiveCorrelationOscillatorState(MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage,
        int length = 14, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        if (maType == MovingAvgType.KaufmanAdaptiveMovingAverage)
        {
            _srcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexSrcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _index2Ma = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _src2Ma = new KaufmanAdaptiveMovingAverageEngine(resolved);
        }
        else
        {
            _srcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexSrcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _index2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
            _src2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
        }

        _input = new StreamingInputResolver(inputName, null);
    }

    public KaufmanAdaptiveCorrelationOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        if (maType == MovingAvgType.KaufmanAdaptiveMovingAverage)
        {
            _srcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexSrcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _index2Ma = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _src2Ma = new KaufmanAdaptiveMovingAverageEngine(resolved);
        }
        else
        {
            _srcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexSrcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _index2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
            _src2Ma = MovingAverageSmootherFactory.Create(maType, resolved);
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveCorrelationOscillator;

    public void Reset()
    {
        _srcMa.Reset();
        _indexMa.Reset();
        _indexSrcMa.Reset();
        _index2Ma.Reset();
        _src2Ma.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double index = _index;
        var indexSrc = index * value;
        var src2 = value * value;
        var index2 = index * index;

        var srcMa = _srcMa.Next(value, isFinal);
        var indexMa = _indexMa.Next(index, isFinal);
        var indexSrcMa = _indexSrcMa.Next(indexSrc, isFinal);
        var index2Ma = _index2Ma.Next(index2, isFinal);
        var src2Ma = _src2Ma.Next(src2, isFinal);

        var indexSqrt = index2Ma - (indexMa * indexMa);
        var indexSt = indexSqrt >= 0 ? MathHelper.Sqrt(indexSqrt) : 0;
        var srcSqrt = src2Ma - (srcMa * srcMa);
        var srcSt = srcSqrt >= 0 ? MathHelper.Sqrt(srcSqrt) : 0;
        var denom = indexSt * srcSt;
        var r = denom != 0 ? (indexSrcMa - (indexMa * srcMa)) / denom : 0;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "IndexSt", indexSt },
                { "SrcSt", srcSt },
                { "Kaco", r }
            };
        }

        return new StreamingIndicatorStateResult(r, outputs);
    }

    public void Dispose()
    {
        _srcMa.Dispose();
        _indexMa.Dispose();
        _indexSrcMa.Dispose();
        _index2Ma.Dispose();
        _src2Ma.Dispose();
    }
}

internal sealed class RelativeVolatilityIndexEngine : IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _upSmoother;
    private readonly IMovingAverageSmoother _downSmoother;
    private double _inputValue;
    private double _prevValue;
    private bool _hasPrev;

    public RelativeVolatilityIndexEngine(MovingAvgType maType, int length, int smoothLength)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        _stdDev = new StandardDeviationVolatilityState(maType, resolvedLength, _ => _inputValue);
        _upSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _downSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
    }

    public double Next(double value, OhlcvBar bar, bool isFinal)
    {
        _inputValue = value;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var prevValue = _hasPrev ? _prevValue : 0;
        var up = value > prevValue ? stdDev : 0;
        var down = value < prevValue ? stdDev : 0;
        var avgUp = _upSmoother.Next(up, isFinal);
        var avgDown = _downSmoother.Next(down, isFinal);
        var rs = avgDown != 0 ? avgUp / avgDown : 0;
        var rvi = avgDown == 0 ? 100 : avgUp == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        return rvi;
    }

    public void Reset()
    {
        _stdDev.Reset();
        _upSmoother.Reset();
        _downSmoother.Reset();
        _inputValue = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _upSmoother.Dispose();
        _downSmoother.Dispose();
    }
}

internal sealed class KasePeakOscillatorV1Engine : IDisposable
{
    private readonly int _length;
    private readonly double _sqrtLength;
    private readonly WilderState _atr;
    private readonly IMovingAverageSmoother _pkSmoother;
    private readonly IMovingAverageSmoother _mnSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private double _prevClose;
    private bool _hasPrev;
    private double _pkValue;

    public KasePeakOscillatorV1Engine(int length, int smoothLength)
    {
        _length = Math.Max(1, length);
        _sqrtLength = MathHelper.Sqrt(_length);
        _atr = new WilderState(_length);
        _pkSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, smoothLength));
        _mnSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, _length);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, _length, _ => _pkValue);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
    }

    public double Next(OhlcvBar bar, bool isFinal, out double mn, out double stdDev)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atr.GetNext(tr, isFinal);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, _length);
        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, _length);
        var rwh = atr != 0 ? (bar.High - prevLow) / atr * _sqrtLength : 0;
        var rwl = atr != 0 ? (prevHigh - bar.Low) / atr * _sqrtLength : 0;
        var diff = rwh - rwl;
        var pk = _pkSmoother.Next(diff, isFinal);
        _pkValue = pk;
        stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        mn = _mnSmoother.Next(pk, isFinal);

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        return pk;
    }

    public void Reset()
    {
        _atr.Reset();
        _pkSmoother.Reset();
        _mnSmoother.Reset();
        _stdDev.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _prevClose = 0;
        _hasPrev = false;
        _pkValue = 0;
    }

    public void Dispose()
    {
        _pkSmoother.Dispose();
        _mnSmoother.Dispose();
        _stdDev.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

internal sealed class KaufmanAdaptiveMovingAverageEngine : IMovingAverageSmoother
{
    private readonly EfficiencyRatioState _er;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private double _prevKama;

    public KaufmanAdaptiveMovingAverageEngine(int length, int fastLength = 2, int slowLength = 30)
    {
        _er = new EfficiencyRatioState(Math.Max(1, length));
        _fastAlpha = 2d / (fastLength + 1);
        _slowAlpha = 2d / (slowLength + 1);
    }

    public double Next(double value, bool isFinal)
    {
        var er = _er.Next(value, isFinal);
        var sc = MathHelper.Pow((er * (_fastAlpha - _slowAlpha)) + _slowAlpha, 2);
        var kama = (sc * value) + ((1 - sc) * _prevKama);
        if (isFinal)
        {
            _prevKama = kama;
        }

        return kama;
    }

    public void Reset()
    {
        _er.Reset();
        _prevKama = 0;
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}
