using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class RexOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _roSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public RexOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _roSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RexOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _roSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RexOscillator;

    public void Reset()
    {
        _roSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var tvb = (3 * close) - (bar.Low + bar.Open + bar.High);
        var ro = _roSmoother.Next(tvb, isFinal);
        var signal = _signalSmoother.Next(ro, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ro", ro },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ro, outputs);
    }

    public void Dispose()
    {
        _roSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class RightSidedRickerMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _cumulativeWeights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public RightSidedRickerMovingAverageState(int length = 50, double pctWidth = 60,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var width = pctWidth / 100d * _length;
        _cumulativeWeights = new double[_length];
        double cumulative = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = (1 - MathHelper.Pow(j / width, 2))
                * MathHelper.Exp(-(MathHelper.Pow(j, 2) / (2 * MathHelper.Pow(width, 2))));
            cumulative += weight;
            _cumulativeWeights[j] = cumulative;
        }

        _weightSum = cumulative;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RightSidedRickerMovingAverageState(int length, double pctWidth, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var width = pctWidth / 100d * _length;
        _cumulativeWeights = new double[_length];
        double cumulative = 0;
        for (var j = 0; j < _length; j++)
        {
            var weight = (1 - MathHelper.Pow(j / width, 2))
                * MathHelper.Exp(-(MathHelper.Pow(j, 2) / (2 * MathHelper.Pow(width, 2))));
            cumulative += weight;
            _cumulativeWeights[j] = cumulative;
        }

        _weightSum = cumulative;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RightSidedRickerMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        var count = _values.Count;
        double vw = 0;
        for (var j = 0; j < _length; j++)
        {
            double prevV;
            if (isFinal)
            {
                var idx = count - 1 - j;
                prevV = idx >= 0 ? _values[idx] : 0;
            }
            else
            {
                if (j == 0)
                {
                    prevV = value;
                }
                else
                {
                    var idx = count - j;
                    prevV = idx >= 0 ? _values[idx] : 0;
                }
            }

            vw += prevV * _cumulativeWeights[j];
        }

        var rrma = _weightSum != 0 ? vw / _weightSum : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rsrma", rrma }
            };
        }

        return new StreamingIndicatorStateResult(rrma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class RobustWeightingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowCorrelation _corrWindow;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _indexSma;
    private readonly IMovingAverageSmoother _lSma;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StandardDeviationVolatilityState _indexStdDev;
    private readonly StreamingInputResolver _input;
    private double _indexValue;
    private int _index;

    public RobustWeightingOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(_length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _indexSma = MovingAverageSmootherFactory.Create(maType, _length);
        _lSma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RobustWeightingOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(_length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _indexSma = MovingAverageSmootherFactory.Create(maType, _length);
        _lSma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RobustWeightingOscillator;

    public void Reset()
    {
        _corrWindow.Reset();
        _sma.Reset();
        _indexSma.Reset();
        _lSma.Reset();
        _stdDev.Reset();
        _indexStdDev.Reset();
        _indexValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_index;
        _indexValue = index;

        var corr = isFinal ? _corrWindow.Add(index, value, out _) : _corrWindow.Preview(index, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var sma = _sma.Next(value, isFinal);
        var indexSma = _indexSma.Next(index, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var indexStdDev = _indexStdDev.Update(bar, isFinal, includeOutputs: false).Value;

        var a = indexStdDev != 0 ? corr * (stdDev / indexStdDev) : 0;
        var b = sma - (a * indexSma);
        var l = value - a - (b * value);
        var lSma = _lSma.Next(l, isFinal);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rwo", lSma }
            };
        }

        return new StreamingIndicatorStateResult(lSma, outputs);
    }

    public void Dispose()
    {
        _corrWindow.Dispose();
        _sma.Dispose();
        _indexSma.Dispose();
        _lSma.Dispose();
        _stdDev.Dispose();
        _indexStdDev.Dispose();
    }
}

public sealed class RSINGIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StandardDeviationVolatilityState _rangeStdDev;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _rangeValue;
    private int _index;

    public RSINGIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length);
        _signalMa = MovingAverageSmootherFactory.Create(maType, _length);
        _rangeStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _rangeValue);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RSINGIndicatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length);
        _signalMa = MovingAverageSmootherFactory.Create(maType, _length);
        _rangeStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _rangeValue);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RSINGIndicator;

    public void Reset()
    {
        _volumeMa.Reset();
        _signalMa.Reset();
        _rangeStdDev.Reset();
        _values.Clear();
        _rangeValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var ma = _volumeMa.Next(volume, isFinal);
        var range = bar.High - bar.Low;
        _rangeValue = range;
        var stdev = _rangeStdDev.Update(bar, isFinal, includeOutputs: false).Value;

        var count = _values.Count;
        var prevValue = count >= _length ? _values[count - _length] : 0;
        var vwr = ma != 0 ? volume / ma : 0;
        var blr = stdev != 0 ? range / stdev : 0;
        var pmo = _index >= _length ? value - prevValue : 0;
        var rsing = vwr * blr * pmo;
        var signal = _signalMa.Next(rsing, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Rsing", rsing },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rsing, outputs);
    }

    public void Dispose()
    {
        _volumeMa.Dispose();
        _signalMa.Dispose();
        _rangeStdDev.Dispose();
        _values.Dispose();
    }
}

public sealed class RSMKIndicatorState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly int _length;
    private readonly IMovingAverageSmoother _logDiffEma;
    private readonly PooledRingBuffer<double> _logRatios;
    private double _lastMarketValue;
    private bool _hasMarket;

    public RSMKIndicatorState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 90, int smoothLength = 3)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        _length = Math.Max(1, length);
        _logDiffEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _logRatios = new PooledRingBuffer<double>(_length + 1);
    }

    public IndicatorName Name => IndicatorName.RSMKIndicator;

    public void Reset()
    {
        _logDiffEma.Reset();
        _logRatios.Clear();
        _lastMarketValue = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (series.Equals(_marketSeries))
        {
            _lastMarketValue = bar.Close;
            _hasMarket = true;
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        if (!series.Equals(_primarySeries))
        {
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
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var logRatio = marketValue != 0 ? bar.Close / marketValue : 0;
        var count = _logRatios.Count;
        var prevLogRatio = count >= _length ? _logRatios[count - _length] : 0;
        var logDiff = logRatio - prevLogRatio;
        var logDiffEma = _logDiffEma.Next(logDiff, isFinal);
        var rsmk = logDiffEma * 100;

        if (isFinal)
        {
            _logRatios.TryAdd(logRatio, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Rsmk", rsmk }
            };
        }

        return new MultiSeriesIndicatorStateResult(true, rsmk, outputs);
    }

    public void Dispose()
    {
        _logDiffEma.Dispose();
        _logRatios.Dispose();
    }
}

public sealed class RunningEquityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly RollingWindowSum _chgXSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevX;
    private bool _hasPrev;

    public RunningEquityState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _chgXSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RunningEquityState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _chgXSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RunningEquity;

    public void Reset()
    {
        _sma.Reset();
        _chgXSum.Reset();
        _prevValue = 0;
        _prevX = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevX = _hasPrev ? _prevX : 0;
        var x = Math.Sign(value - sma);
        var chgX = _hasPrev ? (value - prevValue) * prevX : 0;
        var sum = isFinal ? _chgXSum.Add(chgX, out _) : _chgXSum.Preview(chgX, out _);
        var req = sum;

        if (isFinal)
        {
            _prevValue = value;
            _prevX = x;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Req", req }
            };
        }

        return new StreamingIndicatorStateResult(req, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _chgXSum.Dispose();
    }
}

public sealed class SchaffTrendCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _cycleLength;
    private readonly IMovingAverageSmoother _fastEma;
    private readonly IMovingAverageSmoother _slowEma;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;

    public SchaffTrendCycleState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 23, int slowLength = 50, int cycleLength = 10,
        InputName inputName = InputName.Close)
    {
        _cycleLength = Math.Max(1, cycleLength);
        _fastEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _maxWindow = new RollingWindowMax(_cycleLength);
        _minWindow = new RollingWindowMin(_cycleLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SchaffTrendCycleState(MovingAvgType maType, int fastLength, int slowLength, int cycleLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _cycleLength = Math.Max(1, cycleLength);
        _fastEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowEma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _maxWindow = new RollingWindowMax(_cycleLength);
        _minWindow = new RollingWindowMin(_cycleLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SchaffTrendCycle;

    public void Reset()
    {
        _fastEma.Reset();
        _slowEma.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastEma.Next(value, isFinal);
        var slow = _slowEma.Next(value, isFinal);
        var macd = fast - slow;
        var highest = isFinal ? _maxWindow.Add(macd, out _) : _maxWindow.Preview(macd, out _);
        var lowest = isFinal ? _minWindow.Add(macd, out _) : _minWindow.Preview(macd, out _);
        var range = highest - lowest;
        var stc = range != 0 ? MathHelper.MinOrMax((macd - lowest) / range * 100, 100, 0) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Stc", stc }
            };
        }

        return new StreamingIndicatorStateResult(stc, outputs);
    }

    public void Dispose()
    {
        _fastEma.Dispose();
        _slowEma.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

public sealed class SectorRotationModelState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly RateOfChangeState _primaryRoc1;
    private readonly RateOfChangeState _primaryRoc2;
    private readonly RateOfChangeState _marketRoc1;
    private readonly RateOfChangeState _marketRoc2;
    private readonly IMovingAverageSmoother _signal;
    private double _lastMarketRoc1;
    private double _lastMarketRoc2;
    private bool _hasMarket;

    public SectorRotationModelState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25, int length2 = 75)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        var resolved1 = Math.Max(1, length1);
        _primaryRoc1 = new RateOfChangeState(resolved1);
        _primaryRoc2 = new RateOfChangeState(Math.Max(1, length2));
        _marketRoc1 = new RateOfChangeState(resolved1);
        _marketRoc2 = new RateOfChangeState(Math.Max(1, length2));
        _signal = MovingAverageSmootherFactory.Create(maType, resolved1);
    }

    public IndicatorName Name => IndicatorName.SectorRotationModel;

    public void Reset()
    {
        _primaryRoc1.Reset();
        _primaryRoc2.Reset();
        _marketRoc1.Reset();
        _marketRoc2.Reset();
        _signal.Reset();
        _lastMarketRoc1 = 0;
        _lastMarketRoc2 = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (series.Equals(_marketSeries))
        {
            var roc1 = _marketRoc1.Update(bar, isFinal, includeOutputs: false).Value;
            var roc2 = _marketRoc2.Update(bar, isFinal, includeOutputs: false).Value;
            _lastMarketRoc1 = roc1;
            _lastMarketRoc2 = roc2;
            _hasMarket = true;
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        if (!series.Equals(_primarySeries))
        {
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var bull1 = _primaryRoc1.Update(bar, isFinal, includeOutputs: false).Value;
        var bull2 = _primaryRoc2.Update(bar, isFinal, includeOutputs: false).Value;

        double bear1;
        double bear2;
        if (_hasMarket)
        {
            bear1 = _lastMarketRoc1;
            bear2 = _lastMarketRoc2;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            bear1 = _marketRoc1.Update(marketBar, isFinal: false, includeOutputs: false).Value;
            bear2 = _marketRoc2.Update(marketBar, isFinal: false, includeOutputs: false).Value;
        }
        else
        {
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var bull = (bull1 + bull2) / 2;
        var bear = (bear1 + bear2) / 2;
        var osc = 100 * (bull - bear);
        var signal = _signal.Next(osc, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Srm", osc },
                { "Signal", signal }
            };
        }

        return new MultiSeriesIndicatorStateResult(true, osc, outputs);
    }

    public void Dispose()
    {
        _primaryRoc1.Dispose();
        _primaryRoc2.Dispose();
        _marketRoc1.Dispose();
        _marketRoc2.Dispose();
        _signal.Dispose();
    }
}

public sealed class SelfAdjustingRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private double _rsiValue;

    public SelfAdjustingRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothingLength = 21, double mult = 2,
        InputName inputName = InputName.Close)
    {
        _mult = mult;
        var resolvedLength = Math.Max(1, length);
        _rsi = new RsiState(maType, resolvedLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _stdDev = new StandardDeviationVolatilityState(maType, resolvedLength, _ => _rsiValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SelfAdjustingRelativeStrengthIndexState(MovingAvgType maType, int length, int smoothingLength, double mult,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _mult = mult;
        var resolvedLength = Math.Max(1, length);
        _rsi = new RsiState(maType, resolvedLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        _stdDev = new StandardDeviationVolatilityState(maType, resolvedLength, _ => _rsiValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SelfAdjustingRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _signalSmoother.Reset();
        _stdDev.Reset();
        _rsiValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        _rsiValue = rsi;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var signal = _signalSmoother.Next(rsi, isFinal);
        var adjustingStdDev = _mult * stdDev;
        var obLevel = 50 + adjustingStdDev;
        var osLevel = 50 - adjustingStdDev;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "SaRsi", rsi },
                { "Signal", signal },
                { "ObLevel", obLevel },
                { "OsLevel", osLevel }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _signalSmoother.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class SelfWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SelfWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SelfWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(_length * 2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SelfWeightedMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        double weightSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var pValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var weight = EhlersStreamingWindow.GetOffsetValue(_values, value, _length + j);
            weightSum += weight;
            sum += weight * pValue;
        }

        var swma = weightSum != 0 ? sum / weightSum : 0;

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

public sealed class SellGravitationIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sgiSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;

    public SellGravitationIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _sgiSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.SellGravitationIndex;

    public void Reset()
    {
        _sgiSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var v1 = bar.Close - bar.Open;
        var v2 = bar.High - bar.Low;
        var v3 = v2 != 0 ? v1 / v2 : 0;
        var sgi = _sgiSmoother.Next(v3, isFinal);
        var signal = _signalSmoother.Next(sgi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Sgi", sgi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(sgi, outputs);
    }

    public void Dispose()
    {
        _sgiSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class SentimentZoneOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _fastLength;
    private readonly double _factor;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public SentimentZoneOscillatorState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage,
        int fastLength = 14, int slowLength = 30, double factor = 0.95,
        InputName inputName = InputName.Close)
    {
        _fastLength = Math.Max(1, fastLength);
        _factor = factor;
        _maxWindow = new RollingWindowMax(Math.Max(1, slowLength));
        _minWindow = new RollingWindowMin(Math.Max(1, slowLength));
        _smoother = MovingAverageSmootherFactory.Create(maType, _fastLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SentimentZoneOscillatorState(MovingAvgType maType, int fastLength, int slowLength, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _factor = factor;
        _maxWindow = new RollingWindowMax(Math.Max(1, slowLength));
        _minWindow = new RollingWindowMin(Math.Max(1, slowLength));
        _smoother = MovingAverageSmootherFactory.Create(maType, _fastLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SentimentZoneOscillator;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _smoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        double r = value > prevValue ? 1 : -1;
        var sp = _smoother.Next(r, isFinal);
        var szo = _fastLength != 0 ? 100 * sp / _fastLength : 0;
        var highest = isFinal ? _maxWindow.Add(szo, out _) : _maxWindow.Preview(szo, out _);
        var lowest = isFinal ? _minWindow.Add(szo, out _) : _minWindow.Preview(szo, out _);
        var range = highest - lowest;
        _ = lowest + (range * _factor);
        _ = highest - (range * _factor);

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
                { "Szo", szo }
            };
        }

        return new StreamingIndicatorStateResult(szo, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _smoother.Dispose();
    }
}

public sealed class SequentiallyFilteredMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma;
    private readonly RollingWindowSum _signSum;
    private readonly StreamingInputResolver _input;
    private double _prevSma;
    private double _prevSfma;
    private bool _hasPrev;

    public SequentiallyFilteredMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _signSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SequentiallyFilteredMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _signSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SequentiallyFilteredMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _signSum.Reset();
        _prevSma = 0;
        _prevSfma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var prevSma = _hasPrev ? _prevSma : 0;
        var sign = Math.Sign(sma - prevSma);
        var sum = isFinal ? _signSum.Add(sign, out _) : _signSum.Preview(sign, out _);
        var alpha = Math.Abs(sum) == _length ? 1 : 0;
        var prevSfma = _hasPrev ? _prevSfma : sma;
        var sfma = (alpha * sma) + ((1 - alpha) * prevSfma);

        if (isFinal)
        {
            _prevSma = sma;
            _prevSfma = sfma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sfma", sfma }
            };
        }

        return new StreamingIndicatorStateResult(sfma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _signSum.Dispose();
    }
}

public sealed class SettingLessTrendStepFilteringState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _chgSum;
    private int _chgCount;
    private double _prevA;
    private double _prevB;
    private bool _hasPrev;

    public SettingLessTrendStepFilteringState(InputName inputName = InputName.Close)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public SettingLessTrendStepFilteringState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SettingLessTrendStepFiltering;

    public void Reset()
    {
        _chgSum = 0;
        _chgCount = 0;
        _prevA = 0;
        _prevB = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevB = _hasPrev ? _prevB : value;
        var prevA = _hasPrev ? _prevA : 0;
        var diff = Math.Abs(value - prevB);
        var sc = diff + prevA != 0 ? diff / (diff + prevA) : 0;
        var sltsf = (sc * value) + ((1 - sc) * prevB);
        var chg = Math.Abs(sltsf - prevB);
        var chgSum = _chgSum + chg;
        var count = _chgCount + 1;
        var a = count > 0 ? (chgSum / count) * (1 + sc) : 0;
        var b = sltsf > prevB + a ? sltsf : sltsf < prevB - a ? sltsf : prevB;

        if (isFinal)
        {
            _chgSum = chgSum;
            _chgCount = count;
            _prevA = a;
            _prevB = b;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sltsf", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }
}

public sealed class ShapeshiftingMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weightsX;
    private readonly double[] _weightsN;
    private readonly double _weightSumX;
    private readonly double _weightSumN;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public ShapeshiftingMovingAverageState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weightsX = new double[_length];
        _weightsN = new double[_length];
        double sumX = 0;
        double sumN = 0;
        if (_length == 1)
        {
            _weightsX[0] = 1;
            _weightsN[0] = 1;
            sumX = 1;
            sumN = 1;
        }
        else
        {
            for (var j = 0; j < _length; j++)
            {
                var x = (double)j / (_length - 1);
                var n = -1 + (x * 2);
                var wx = 1 - (2 * x / (MathHelper.Pow(x, 4) + 1));
                var wn = 1 - (2 * MathHelper.Pow(n, 2) / (MathHelper.Pow(n, 4) + 1));
                _weightsX[j] = wx;
                _weightsN[j] = wn;
                sumX += wx;
                sumN += wn;
            }
        }

        _weightSumX = sumX;
        _weightSumN = sumN;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ShapeshiftingMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _weightsX = new double[_length];
        _weightsN = new double[_length];
        double sumX = 0;
        double sumN = 0;
        if (_length == 1)
        {
            _weightsX[0] = 1;
            _weightsN[0] = 1;
            sumX = 1;
            sumN = 1;
        }
        else
        {
            for (var j = 0; j < _length; j++)
            {
                var x = (double)j / (_length - 1);
                var n = -1 + (x * 2);
                var wx = 1 - (2 * x / (MathHelper.Pow(x, 4) + 1));
                var wn = 1 - (2 * MathHelper.Pow(n, 2) / (MathHelper.Pow(n, 4) + 1));
                _weightsX[j] = wx;
                _weightsN[j] = wn;
                sumX += wx;
                sumN += wn;
            }
        }

        _weightSumX = sumX;
        _weightSumN = sumN;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.ShapeshiftingMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sumX = 0;
        double sumN = 0;
        for (var j = 0; j < _length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sumX += prevValue * _weightsX[j];
            sumN += prevValue * _weightsN[j];
        }

        var filtX = _weightSumX != 0 ? sumX / _weightSumX : 0;
        _ = _weightSumN != 0 ? sumN / _weightSumN : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sma", filtX }
            };
        }

        return new StreamingIndicatorStateResult(filtX, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class SharpeRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly IMovingAverageSmoother _retSmoother;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private double _retValue;

    public SharpeRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = 0.02,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var barMin = 60d * 24;
        var minPerYr = 60d * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = MathHelper.Pow(1 + bmk, _length / barsPerYr) - 1;
        _retSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SharpeRatioState(MovingAvgType maType, int length, double bmk, Func<OhlcvBar, double> selector)
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
        _stdDev = new StandardDeviationVolatilityState(maType, _length, _ => _retValue);
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SharpeRatio;

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
        var ret = prevValue != 0 ? (value / prevValue) - 1 - _bench : 0;
        _retValue = ret;
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var retSma = _retSmoother.Next(ret, isFinal);
        var sharpe = stdDev != 0 ? retSma / stdDev : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sr", sharpe }
            };
        }

        return new StreamingIndicatorStateResult(sharpe, outputs);
    }

    public void Dispose()
    {
        _retSmoother.Dispose();
        _stdDev.Dispose();
        _values.Dispose();
    }
}

public sealed class SharpModifiedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly IMovingAverageSmoother _sma;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SharpModifiedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        for (var j = 0; j < _length; j++)
        {
            var factor = 1 + (2 * j);
            _weights[j] = (_length - factor) / 2d;
        }

        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SharpModifiedMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _weights = new double[_length];
        for (var j = 0; j < _length; j++)
        {
            var factor = 1 + (2 * j);
            _weights[j] = (_length - factor) / 2d;
        }

        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SharpModifiedMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        double slope = 0;
        for (var j = 0; j < _length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            slope += prevValue * _weights[j];
        }

        var denom = (_length + 1d) * _length;
        var smma = denom != 0 ? sma + (6 * slope / denom) : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Smma", smma }
            };
        }

        return new StreamingIndicatorStateResult(smma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _values.Dispose();
    }
}

public sealed class ShinoharaIntensityRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _highSum;
    private readonly RollingWindowSum _lowSum;
    private readonly RollingWindowSum _openSum;
    private readonly RollingWindowSum _prevCloseSum;
    private double _prevClose;
    private bool _hasPrev;

    public ShinoharaIntensityRatioState(int length = 14)
    {
        _length = Math.Max(1, length);
        _highSum = new RollingWindowSum(_length);
        _lowSum = new RollingWindowSum(_length);
        _openSum = new RollingWindowSum(_length);
        _prevCloseSum = new RollingWindowSum(_length);
    }

    public IndicatorName Name => IndicatorName.ShinoharaIntensityRatio;

    public void Reset()
    {
        _highSum.Reset();
        _lowSum.Reset();
        _openSum.Reset();
        _prevCloseSum.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var highSum = isFinal ? _highSum.Add(bar.High, out _) : _highSum.Preview(bar.High, out _);
        var lowSum = isFinal ? _lowSum.Add(bar.Low, out _) : _lowSum.Preview(bar.Low, out _);
        var openSum = isFinal ? _openSum.Add(bar.Open, out _) : _openSum.Preview(bar.Open, out _);
        var prevCloseSum = isFinal ? _prevCloseSum.Add(prevClose, out _) : _prevCloseSum.Preview(prevClose, out _);
        var bullA = highSum - openSum;
        var bearA = openSum - lowSum;
        var bullB = highSum - prevCloseSum;
        var bearB = prevCloseSum - lowSum;
        var ratioA = bearA != 0 ? bullA / bearA * 100 : 0;
        var ratioB = bearB != 0 ? bullB / bearB * 100 : 0;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "ARatio", ratioA },
                { "BRatio", ratioB }
            };
        }

        return new StreamingIndicatorStateResult(ratioA, outputs);
    }

    public void Dispose()
    {
        _highSum.Dispose();
        _lowSum.Dispose();
        _openSum.Dispose();
        _prevCloseSum.Dispose();
    }
}

public sealed class SimpleCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly PooledRingBuffer<double> _srcValues;
    private readonly StreamingInputResolver _input;
    private double _prevCema;
    private double _prevC;
    private int _count;

    public SimpleCycleState(int length = 50, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _alpha = 1d / _length;
        _srcValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimpleCycleState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _alpha = 1d / _length;
        _srcValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimpleCycle;

    public void Reset()
    {
        _srcValues.Clear();
        _prevCema = 0;
        _prevC = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevC = _count >= 1 ? _prevC : 0;
        var prevSrc = EhlersStreamingWindow.GetOffsetValue(_srcValues, _length);
        var src = value + prevC;
        var cEma = CalculationsHelper.CalculateEMA(prevC, _prevCema, _length);
        var b = prevC - cEma;
        var c = (_alpha * (src - prevSrc)) + ((1 - _alpha) * b);

        if (isFinal)
        {
            _srcValues.TryAdd(src, out _);
            _prevCema = cEma;
            _prevC = c;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sc", c }
            };
        }

        return new StreamingIndicatorStateResult(c, outputs);
    }

    public void Dispose()
    {
        _srcValues.Dispose();
    }
}

public sealed class SimpleLinesState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _mult;
    private readonly double _s;
    private readonly StreamingInputResolver _input;
    private double _prevA1;
    private double _prevA2;
    private int _count;

    public SimpleLinesState(int length = 10, double mult = 10, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _s = 0.01 * 100 * (1d / _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimpleLinesState(int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _mult = mult;
        _s = 0.01 * 100 * (1d / _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimpleLines;

    public void Reset()
    {
        _prevA1 = 0;
        _prevA2 = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevA = _count >= 1 ? _prevA1 : value;
        var prevA2 = _count >= 2 ? _prevA2 : value;
        var x = value + ((prevA - prevA2) * _mult);
        prevA = _count >= 1 ? _prevA1 : x;
        var a = x > prevA + _s ? prevA + _s : x < prevA - _s ? prevA - _s : prevA;

        if (isFinal)
        {
            _prevA2 = _prevA1;
            _prevA1 = a;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Sl", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }
}

public sealed class SimplifiedLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _cmlValues;
    private readonly PooledRingBuffer<double> _cmlSumValues;
    private readonly StreamingInputResolver _input;
    private double _tempSum;
    private double _cmlSumTotal;
    private double _prevSum;

    public SimplifiedLeastSquaresMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _cmlValues = new PooledRingBuffer<double>(_length + 1);
        _cmlSumValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimplifiedLeastSquaresMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _cmlValues = new PooledRingBuffer<double>(_length + 1);
        _cmlSumValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimplifiedLeastSquaresMovingAverage;

    public void Reset()
    {
        _cmlValues.Clear();
        _cmlSumValues.Clear();
        _tempSum = 0;
        _cmlSumTotal = 0;
        _prevSum = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var tempSum = _tempSum + value;
        var cml = tempSum;
        var prevCml = EhlersStreamingWindow.GetOffsetValue(_cmlValues, _length);
        var prevCmlSum = EhlersStreamingWindow.GetOffsetValue(_cmlSumValues, _length);
        var cmlSumTotal = _cmlSumTotal + cml;
        var cmlSum = cmlSumTotal;
        var sum = cmlSum - prevCmlSum;
        var denom = _length * (_length + 1d) / 2;
        var wma = denom != 0 ? ((_length * cml) - _prevSum) / denom : 0;
        var lsma = _length != 0 ? (3 * wma) - (2 * (cml - prevCml) / _length) : 0;

        if (isFinal)
        {
            _tempSum = tempSum;
            _cmlSumTotal = cmlSumTotal;
            _prevSum = sum;
            _cmlValues.TryAdd(cml, out _);
            _cmlSumValues.TryAdd(cmlSum, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Slsma", lsma }
            };
        }

        return new StreamingIndicatorStateResult(lsma, outputs);
    }

    public void Dispose()
    {
        _cmlValues.Dispose();
        _cmlSumValues.Dispose();
    }
}

public sealed class SimplifiedWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _cmlSumValues;
    private readonly StreamingInputResolver _input;
    private double _tempSum;
    private double _cmlSumTotal;
    private double _prevSum;

    public SimplifiedWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _cmlSumValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SimplifiedWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _cmlSumValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SimplifiedWeightedMovingAverage;

    public void Reset()
    {
        _cmlSumValues.Clear();
        _tempSum = 0;
        _cmlSumTotal = 0;
        _prevSum = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var tempSum = _tempSum + value;
        var cml = tempSum;
        var prevCmlSum = EhlersStreamingWindow.GetOffsetValue(_cmlSumValues, _length);
        var cmlSumTotal = _cmlSumTotal + cml;
        var cmlSum = cmlSumTotal;
        var sum = cmlSum - prevCmlSum;
        var denom = _length * (_length + 1d) / 2;
        var wma = denom != 0 ? ((_length * cml) - _prevSum) / denom : 0;

        if (isFinal)
        {
            _tempSum = tempSum;
            _cmlSumTotal = cmlSumTotal;
            _prevSum = sum;
            _cmlSumValues.TryAdd(cmlSum, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Swma", wma }
            };
        }

        return new StreamingIndicatorStateResult(wma, outputs);
    }

    public void Dispose()
    {
        _cmlSumValues.Dispose();
    }
}

public sealed class SineWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double[] _weights;
    private readonly double _weightSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public SineWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _weights = new double[_length];
        double sum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = Math.Sin((j + 1) * Math.PI / (_length + 1));
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SineWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
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
            var weight = Math.Sin((j + 1) * Math.PI / (_length + 1));
            _weights[j] = weight;
            sum += weight;
        }

        _weightSum = sum;
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SineWeightedMovingAverage;

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

public sealed class SlowSmoothedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _l1;
    private readonly IMovingAverageSmoother _l2;
    private readonly IMovingAverageSmoother _l3;
    private readonly StreamingInputResolver _input;

    public SlowSmoothedMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 15,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        var w2 = MathHelper.MinOrMax((int)Math.Ceiling(resolved / 3d));
        var w1 = MathHelper.MinOrMax((int)Math.Ceiling((resolved - w2) / 2d));
        var w3 = MathHelper.MinOrMax((int)Math.Floor((resolved - w2) / 2d));
        _l1 = MovingAverageSmootherFactory.Create(maType, w1);
        _l2 = MovingAverageSmootherFactory.Create(maType, w2);
        _l3 = MovingAverageSmootherFactory.Create(maType, w3);
        _input = new StreamingInputResolver(inputName, null);
    }

    public SlowSmoothedMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        var w2 = MathHelper.MinOrMax((int)Math.Ceiling(resolved / 3d));
        var w1 = MathHelper.MinOrMax((int)Math.Ceiling((resolved - w2) / 2d));
        var w3 = MathHelper.MinOrMax((int)Math.Floor((resolved - w2) / 2d));
        _l1 = MovingAverageSmootherFactory.Create(maType, w1);
        _l2 = MovingAverageSmootherFactory.Create(maType, w2);
        _l3 = MovingAverageSmootherFactory.Create(maType, w3);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SlowSmoothedMovingAverage;

    public void Reset()
    {
        _l1.Reset();
        _l2.Reset();
        _l3.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var l1 = _l1.Next(value, isFinal);
        var l2 = _l2.Next(l1, isFinal);
        var l3 = _l3.Next(l2, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ssma", l3 }
            };
        }

        return new StreamingIndicatorStateResult(l3, outputs);
    }

    public void Dispose()
    {
        _l1.Dispose();
        _l2.Dispose();
        _l3.Dispose();
    }
}

public sealed class SMIErgodicIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _pcSmoothFast;
    private readonly IMovingAverageSmoother _pcSmoothSlow;
    private readonly IMovingAverageSmoother _absSmoothFast;
    private readonly IMovingAverageSmoother _absSmoothSlow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public SMIErgodicIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 5,
        int slowLength = 20, int signalLength = 5, InputName inputName = InputName.Close)
    {
        _pcSmoothFast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _pcSmoothSlow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _absSmoothFast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _absSmoothSlow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public SMIErgodicIndicatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _pcSmoothFast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _pcSmoothSlow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _absSmoothFast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _absSmoothSlow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.SMIErgodicIndicator;

    public void Reset()
    {
        _pcSmoothFast.Reset();
        _pcSmoothSlow.Reset();
        _absSmoothFast.Reset();
        _absSmoothSlow.Reset();
        _signal.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var pc = _hasPrev ? value - prevValue : 0;
        var absPc = Math.Abs(pc);
        var pcSmooth1 = _pcSmoothFast.Next(pc, isFinal);
        var pcSmooth2 = _pcSmoothSlow.Next(pcSmooth1, isFinal);
        var absSmooth1 = _absSmoothFast.Next(absPc, isFinal);
        var absSmooth2 = _absSmoothSlow.Next(absSmooth1, isFinal);
        var smi = absSmooth2 != 0 ? MathHelper.MinOrMax(100 * pcSmooth2 / absSmooth2, 100, -100) : 0;
        var signal = _signal.Next(smi, isFinal);

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
                { "Smi", smi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(smi, outputs);
    }

    public void Dispose()
    {
        _pcSmoothFast.Dispose();
        _pcSmoothSlow.Dispose();
        _absSmoothFast.Dispose();
        _absSmoothSlow.Dispose();
        _signal.Dispose();
    }
}
