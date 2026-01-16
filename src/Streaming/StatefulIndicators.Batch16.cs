using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class KaufmanAdaptiveLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _srcMa;
    private readonly IMovingAverageSmoother _indexMa;
    private readonly KaufmanAdaptiveCorrelationOscillatorState _kaco;
    private readonly StreamingInputResolver _input;
    private int _index;

    public KaufmanAdaptiveLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.KaufmanAdaptiveMovingAverage,
        int length = 100, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _kaco = new KaufmanAdaptiveCorrelationOscillatorState(maType, resolved, inputName);
        if (maType == MovingAvgType.KaufmanAdaptiveMovingAverage)
        {
            _srcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
        }
        else
        {
            _srcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexMa = MovingAverageSmootherFactory.Create(maType, resolved);
        }
        _input = new StreamingInputResolver(inputName, null);
    }

    public KaufmanAdaptiveLeastSquaresMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _kaco = new KaufmanAdaptiveCorrelationOscillatorState(maType, resolved, selector);
        if (maType == MovingAvgType.KaufmanAdaptiveMovingAverage)
        {
            _srcMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
            _indexMa = new KaufmanAdaptiveMovingAverageEngine(resolved);
        }
        else
        {
            _srcMa = MovingAverageSmootherFactory.Create(maType, resolved);
            _indexMa = MovingAverageSmootherFactory.Create(maType, resolved);
        }
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;

    public void Reset()
    {
        _srcMa.Reset();
        _indexMa.Reset();
        _kaco.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var kacoResult = _kaco.Update(bar, isFinal, includeOutputs: true);
        var kacoOutputs = kacoResult.Outputs!;
        var indexSt = kacoOutputs["IndexSt"];
        var srcSt = kacoOutputs["SrcSt"];
        var r = kacoResult.Value;

        double index = _index;
        var srcMa = _srcMa.Next(value, isFinal);
        var indexMa = _indexMa.Next(index, isFinal);
        var alpha = indexSt != 0 ? srcSt / indexSt * r : 0;
        var beta = srcMa - (alpha * indexMa);
        var kalsma = (alpha * index) + beta;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Kalsma", kalsma }
            };
        }

        return new StreamingIndicatorStateResult(kalsma, outputs);
    }

    public void Dispose()
    {
        _srcMa.Dispose();
        _indexMa.Dispose();
        _kaco.Dispose();
    }
}

public sealed class KaufmanAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private readonly StreamingInputResolver _input;
    private double _prevKama;

    public KaufmanAdaptiveMovingAverageState(int length = 10, int fastLength = 2, int slowLength = 30,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _fastAlpha = 2d / (Math.Max(1, fastLength) + 1);
        _slowAlpha = 2d / (Math.Max(1, slowLength) + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KaufmanAdaptiveMovingAverageState(int length, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _fastAlpha = 2d / (Math.Max(1, fastLength) + 1);
        _slowAlpha = 2d / (Math.Max(1, slowLength) + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaufmanAdaptiveMovingAverage;

    public void Reset()
    {
        _er.Reset();
        _prevKama = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var er = _er.Next(value, isFinal);
        var sc = MathHelper.Pow((er * (_fastAlpha - _slowAlpha)) + _slowAlpha, 2);
        var kama = (sc * value) + ((1 - sc) * _prevKama);

        if (isFinal)
        {
            _prevKama = kama;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Er", er },
                { "Kama", kama }
            };
        }

        return new StreamingIndicatorStateResult(kama, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

public sealed class KaufmanBinaryWaveState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _fastSc;
    private readonly double _slowSc;
    private readonly double _filterPct;
    private double _prevAma;
    private double _prevAmaLow;
    private double _prevAmaHigh;
    private double _diffValue;
    private bool _hasPrev;

    public KaufmanBinaryWaveState(int length = 20, double fastSc = 0.6022, double slowSc = 0.0645,
        double filterPct = 10, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _diffValue);
        _fastSc = fastSc;
        _slowSc = slowSc;
        _filterPct = filterPct;
        _input = new StreamingInputResolver(inputName, null);
    }

    public KaufmanBinaryWaveState(int length, double fastSc, double slowSc, double filterPct,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _stdDev = new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, resolved, _ => _diffValue);
        _fastSc = fastSc;
        _slowSc = slowSc;
        _filterPct = filterPct;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KaufmanBinaryWave;

    public void Reset()
    {
        _er.Reset();
        _stdDev.Reset();
        _prevAma = 0;
        _prevAmaLow = 0;
        _prevAmaHigh = 0;
        _diffValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevAma = _hasPrev ? _prevAma : value;
        var er = _er.Next(value, isFinal);
        var smooth = MathHelper.Pow((er * _fastSc) + _slowSc, 2);
        var ama = prevAma + (smooth * (value - prevAma));
        var diff = ama - prevAma;
        _diffValue = diff;

        var diffStdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var filter = _filterPct / 100d * diffStdDev;
        var amaLow = ama < prevAma ? ama : _prevAmaLow;
        var amaHigh = ama > prevAma ? ama : _prevAmaHigh;
        double bw = ama - amaLow > filter ? 1 : amaHigh - ama > filter ? -1 : 0;

        if (isFinal)
        {
            _prevAma = ama;
            _prevAmaLow = amaLow;
            _prevAmaHigh = amaHigh;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Kbw", bw }
            };
        }

        return new StreamingIndicatorStateResult(bw, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
        _stdDev.Dispose();
    }
}

public sealed class KaufmanStressIndicatorState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly RollingWindowMax _primaryHighWindow;
    private readonly RollingWindowMin _primaryLowWindow;
    private readonly RollingWindowMax _marketHighWindow;
    private readonly RollingWindowMin _marketLowWindow;
    private readonly RollingWindowMax _dMaxWindow;
    private readonly RollingWindowMin _dMinWindow;
    private double _marketHigh;
    private double _marketLow;
    private double _marketClose;
    private bool _hasMarket;

    public KaufmanStressIndicatorState(SeriesKey primarySeries, SeriesKey marketSeries, int length = 60)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        var resolved = Math.Max(1, length);
        _primaryHighWindow = new RollingWindowMax(resolved);
        _primaryLowWindow = new RollingWindowMin(resolved);
        _marketHighWindow = new RollingWindowMax(resolved);
        _marketLowWindow = new RollingWindowMin(resolved);
        _dMaxWindow = new RollingWindowMax(resolved);
        _dMinWindow = new RollingWindowMin(resolved);
    }

    public IndicatorName Name => IndicatorName.KaufmanStressIndicator;

    public void Reset()
    {
        _primaryHighWindow.Reset();
        _primaryLowWindow.Reset();
        _marketHighWindow.Reset();
        _marketLowWindow.Reset();
        _dMaxWindow.Reset();
        _dMinWindow.Reset();
        _marketHigh = 0;
        _marketLow = 0;
        _marketClose = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (series.Equals(_marketSeries))
        {
            var marketHigh = isFinal
                ? _marketHighWindow.Add(bar.High, out _)
                : _marketHighWindow.Preview(bar.High, out _);
            var marketLow = isFinal
                ? _marketLowWindow.Add(bar.Low, out _)
                : _marketLowWindow.Preview(bar.Low, out _);

            if (isFinal)
            {
                _marketHigh = marketHigh;
                _marketLow = marketLow;
                _marketClose = bar.Close;
                _hasMarket = true;
            }

            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        if (!series.Equals(_primarySeries))
        {
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var primaryHigh = isFinal
            ? _primaryHighWindow.Add(bar.High, out _)
            : _primaryHighWindow.Preview(bar.High, out _);
        var primaryLow = isFinal
            ? _primaryLowWindow.Add(bar.Low, out _)
            : _primaryLowWindow.Preview(bar.Low, out _);

        double marketHighValue;
        double marketLowValue;
        double marketCloseValue;
        if (_hasMarket)
        {
            marketHighValue = _marketHigh;
            marketLowValue = _marketLow;
            marketCloseValue = _marketClose;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            marketHighValue = _marketHighWindow.Preview(marketBar.High, out _);
            marketLowValue = _marketLowWindow.Preview(marketBar.Low, out _);
            marketCloseValue = marketBar.Close;
        }
        else
        {
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var r1 = primaryHigh - primaryLow;
        var r2 = marketHighValue - marketLowValue;
        var s1 = r1 != 0 ? (bar.Close - primaryLow) / r1 : 50;
        var s2 = r2 != 0 ? (marketCloseValue - marketLowValue) / r2 : 50;
        var d = s1 - s2;
        var dMax = isFinal ? _dMaxWindow.Add(d, out _) : _dMaxWindow.Preview(d, out _);
        var dMin = isFinal ? _dMinWindow.Add(d, out _) : _dMinWindow.Preview(d, out _);
        var range = dMax - dMin;
        var sv = range != 0 ? MathHelper.MinOrMax(100 * (d - dMin) / range, 100, 0) : 50;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ksi", sv }
            };
        }

        return new MultiSeriesIndicatorStateResult(true, sv, outputs);
    }

    public void Dispose()
    {
        _primaryHighWindow.Dispose();
        _primaryLowWindow.Dispose();
        _marketHighWindow.Dispose();
        _marketLowWindow.Dispose();
        _dMaxWindow.Dispose();
        _dMinWindow.Dispose();
    }
}

public sealed class KendallRankCorrelationCoefficientState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly LinearRegressionState _linReg;
    private readonly RollingWindowCorrelation _correlation;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _linRegValues;
    private readonly StreamingInputResolver _input;

    public KendallRankCorrelationCoefficientState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _linReg = new LinearRegressionState(_length, inputName);
        _correlation = new RollingWindowCorrelation(_length);
        _values = new PooledRingBuffer<double>(_length);
        _linRegValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KendallRankCorrelationCoefficientState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _linReg = new LinearRegressionState(_length, selector);
        _correlation = new RollingWindowCorrelation(_length);
        _values = new PooledRingBuffer<double>(_length);
        _linRegValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KendallRankCorrelationCoefficient;

    public void Reset()
    {
        _linReg.Reset();
        _correlation.Reset();
        _values.Clear();
        _linRegValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var linReg = _linReg.Update(bar, isFinal, includeOutputs: false).Value;
        var corr = isFinal
            ? _correlation.Add(linReg, value, out _)
            : _correlation.Preview(linReg, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var totalPairs = _length * (_length - 1) / 2d;
        double numerator = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var valueJ = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            var linRegJ = EhlersStreamingWindow.GetOffsetValue(_linRegValues, linReg, j);
            for (var k = 0; k <= j; k++)
            {
                var valueK = EhlersStreamingWindow.GetOffsetValue(_values, value, k);
                var linRegK = EhlersStreamingWindow.GetOffsetValue(_linRegValues, linReg, k);
                numerator += Math.Sign(linRegJ - linRegK) * Math.Sign(valueJ - valueK);
            }
        }

        var kendall = totalPairs != 0 ? numerator / totalPairs : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _linRegValues.TryAdd(linReg, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Krcc", kendall }
            };
        }

        return new StreamingIndicatorStateResult(kendall, outputs);
    }

    public void Dispose()
    {
        _linReg.Dispose();
        _correlation.Dispose();
        _values.Dispose();
        _linRegValues.Dispose();
    }
}

public sealed class KirshenbaumBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly double _stdDevFactor;
    private readonly IMovingAverageSmoother _smoother;
    private readonly LinearRegressionState _linReg;
    private readonly RollingWindowSum _errorSum;
    private readonly StreamingInputResolver _input;

    public KirshenbaumBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 30, int length2 = 20, double stdDevFactor = 1, InputName inputName = InputName.Close)
    {
        _length2 = Math.Max(1, length2);
        _stdDevFactor = stdDevFactor;
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _linReg = new LinearRegressionState(_length2, inputName);
        _errorSum = new RollingWindowSum(_length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KirshenbaumBandsState(MovingAvgType maType, int length1, int length2, double stdDevFactor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length2 = Math.Max(1, length2);
        _stdDevFactor = stdDevFactor;
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _linReg = new LinearRegressionState(_length2, selector);
        _errorSum = new RollingWindowSum(_length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KirshenbaumBands;

    public void Reset()
    {
        _smoother.Reset();
        _linReg.Reset();
        _errorSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _smoother.Next(value, isFinal);
        var linReg = _linReg.Update(bar, isFinal, includeOutputs: false).Value;
        var diff = linReg - value;
        var sum = isFinal ? _errorSum.Add(diff * diff, out var count) : _errorSum.Preview(diff * diff, out count);
        var sampleCount = Math.Min(_length2, count);
        var stdError = sampleCount > 0 ? MathHelper.Sqrt(sum / sampleCount) : 0;
        stdError = MathHelper.IsValueNullOrInfinity(stdError) ? 0 : stdError;
        var ratio = stdError * _stdDevFactor;
        var upper = middle + ratio;
        var lower = middle - ratio;

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
        _smoother.Dispose();
        _linReg.Dispose();
        _errorSum.Dispose();
    }
}

public sealed class KlingerVolumeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevTrend;
    private double _prevDm;
    private double _prevCm;
    private bool _hasPrev;

    public KlingerVolumeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 34, int slowLength = 55, int signalLength = 13, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public KlingerVolumeOscillatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KlingerVolumeOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _prevTrend = 0;
        _prevDm = 0;
        _prevCm = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var mom = _hasPrev ? value - prevValue : 0;
        var trend = mom > 0 ? 1 : mom < 0 ? -1 : _prevTrend;
        var dm = bar.High - bar.Low;
        var cm = trend == _prevTrend ? _prevCm + dm : _prevDm + dm;
        var temp = cm != 0 ? Math.Abs((2 * (dm / cm)) - 1) : -1;
        var vf = bar.Volume * temp * trend * 100;

        var fast = _fastSmoother.Next(vf, isFinal);
        var slow = _slowSmoother.Next(vf, isFinal);
        var kvo = fast - slow;
        var signal = _signalSmoother.Next(kvo, isFinal);
        var histogram = kvo - signal;

        if (isFinal)
        {
            _prevValue = value;
            _prevTrend = trend;
            _prevDm = dm;
            _prevCm = cm;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Kvo", kvo },
                { "KvoSignal", signal },
                { "KvoHistogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(kvo, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class KnowSureThingState : IStreamingIndicatorState, IDisposable
{
    private readonly double _weight1;
    private readonly double _weight2;
    private readonly double _weight3;
    private readonly double _weight4;
    private readonly RateOfChangeState _roc1;
    private readonly RateOfChangeState _roc2;
    private readonly RateOfChangeState _roc3;
    private readonly RateOfChangeState _roc4;
    private readonly IMovingAverageSmoother _roc1Smoother;
    private readonly IMovingAverageSmoother _roc2Smoother;
    private readonly IMovingAverageSmoother _roc3Smoother;
    private readonly IMovingAverageSmoother _roc4Smoother;
    private readonly IMovingAverageSmoother _signalSmoother;

    public KnowSureThingState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10, int length2 = 10,
        int length3 = 10, int length4 = 15, int rocLength1 = 10, int rocLength2 = 15, int rocLength3 = 20,
        int rocLength4 = 30, int signalLength = 9, double weight1 = 1, double weight2 = 2, double weight3 = 3,
        double weight4 = 4, InputName inputName = InputName.Close)
    {
        _weight1 = weight1;
        _weight2 = weight2;
        _weight3 = weight3;
        _weight4 = weight4;
        _roc1 = new RateOfChangeState(Math.Max(1, rocLength1), inputName);
        _roc2 = new RateOfChangeState(Math.Max(1, rocLength2), inputName);
        _roc3 = new RateOfChangeState(Math.Max(1, rocLength3), inputName);
        _roc4 = new RateOfChangeState(Math.Max(1, rocLength4), inputName);
        _roc1Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _roc2Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _roc3Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _roc4Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public KnowSureThingState(MovingAvgType maType, int length1, int length2, int length3, int length4,
        int rocLength1, int rocLength2, int rocLength3, int rocLength4, int signalLength, double weight1,
        double weight2, double weight3, double weight4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _weight1 = weight1;
        _weight2 = weight2;
        _weight3 = weight3;
        _weight4 = weight4;
        _roc1 = new RateOfChangeState(Math.Max(1, rocLength1), selector);
        _roc2 = new RateOfChangeState(Math.Max(1, rocLength2), selector);
        _roc3 = new RateOfChangeState(Math.Max(1, rocLength3), selector);
        _roc4 = new RateOfChangeState(Math.Max(1, rocLength4), selector);
        _roc1Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _roc2Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _roc3Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _roc4Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.KnowSureThing;

    public void Reset()
    {
        _roc1.Reset();
        _roc2.Reset();
        _roc3.Reset();
        _roc4.Reset();
        _roc1Smoother.Reset();
        _roc2Smoother.Reset();
        _roc3Smoother.Reset();
        _roc4Smoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roc1 = _roc1.Update(bar, isFinal, includeOutputs: false).Value;
        var roc2 = _roc2.Update(bar, isFinal, includeOutputs: false).Value;
        var roc3 = _roc3.Update(bar, isFinal, includeOutputs: false).Value;
        var roc4 = _roc4.Update(bar, isFinal, includeOutputs: false).Value;

        var roc1Ma = _roc1Smoother.Next(roc1, isFinal);
        var roc2Ma = _roc2Smoother.Next(roc2, isFinal);
        var roc3Ma = _roc3Smoother.Next(roc3, isFinal);
        var roc4Ma = _roc4Smoother.Next(roc4, isFinal);

        var kst = (roc1Ma * _weight1) + (roc2Ma * _weight2) + (roc3Ma * _weight3) + (roc4Ma * _weight4);
        var signal = _signalSmoother.Next(kst, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Kst", kst },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(kst, outputs);
    }

    public void Dispose()
    {
        _roc1.Dispose();
        _roc2.Dispose();
        _roc3.Dispose();
        _roc4.Dispose();
        _roc1Smoother.Dispose();
        _roc2Smoother.Dispose();
        _roc3Smoother.Dispose();
        _roc4Smoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class KurtosisIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _diffs;
    private readonly StreamingInputResolver _input;
    private int _index;

    public KurtosisIndicatorState(int length1 = 3, int length2 = 1, int fastLength = 3, int slowLength = 65,
        InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _slowSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, slowLength));
        _fastSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, fastLength));
        _values = new PooledRingBuffer<double>(_length1);
        _diffs = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KurtosisIndicatorState(int length1, int length2, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _slowSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, slowLength));
        _fastSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, fastLength));
        _values = new PooledRingBuffer<double>(_length1);
        _diffs = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KurtosisIndicator;

    public void Reset()
    {
        _slowSmoother.Reset();
        _fastSmoother.Reset();
        _values.Clear();
        _diffs.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var diff = _index >= _length1 ? value - priorValue : 0;
        var priorDiff = EhlersStreamingWindow.GetOffsetValue(_diffs, diff, _length2);
        var k = _index >= _length2 ? diff - priorDiff : 0;
        var fk = _slowSmoother.Next(k, isFinal);
        var signal = _fastSmoother.Next(fk, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _diffs.TryAdd(diff, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fk", fk },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fk, outputs);
    }

    public void Dispose()
    {
        _slowSmoother.Dispose();
        _fastSmoother.Dispose();
        _values.Dispose();
        _diffs.Dispose();
    }
}

public sealed class KwanIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _smoothLength;
    private readonly RsiState _rsi;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _vrValues;
    private readonly StreamingInputResolver _input;
    private double _prevSum;

    public KwanIndicatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 9,
        int smoothLength = 2, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _smoothLength = Math.Max(1, smoothLength);
        _rsi = new RsiState(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length);
        _vrValues = new PooledRingBuffer<double>(_smoothLength);
        _input = new StreamingInputResolver(inputName, null);
    }

    public KwanIndicatorState(MovingAvgType maType, int length, int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _smoothLength = Math.Max(1, smoothLength);
        _rsi = new RsiState(maType, _length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _values = new PooledRingBuffer<double>(_length);
        _vrValues = new PooledRingBuffer<double>(_smoothLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.KwanIndicator;

    public void Reset()
    {
        _rsi.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _values.Clear();
        _vrValues.Clear();
        _prevSum = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var hh = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var ll = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var rsi = _rsi.Next(close, isFinal);
        var priorClose = EhlersStreamingWindow.GetOffsetValue(_values, close, _length);
        var mom = priorClose != 0 ? close / priorClose * 100 : 0;
        var sto = hh - ll != 0 ? (close - ll) / (hh - ll) * 100 : 0;
        var vr = mom != 0 ? sto * rsi / mom : 0;
        var prevVr = EhlersStreamingWindow.GetOffsetValue(_vrValues, vr, _smoothLength);
        var sum = _prevSum + prevVr;
        var knrp = sum / _smoothLength;

        if (isFinal)
        {
            _values.TryAdd(close, out _);
            _vrValues.TryAdd(vr, out _);
            _prevSum = sum;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ki", knrp }
            };
        }

        return new StreamingIndicatorStateResult(knrp, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _values.Dispose();
        _vrValues.Dispose();
    }
}

public sealed class LBRPaintBarsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _atrSmoother;
    private readonly double _atrMult;
    private double _prevClose;
    private bool _hasPrev;

    public LBRPaintBarsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 9, int lbLength = 16,
        double atrMult = 2.5)
    {
        _highWindow = new RollingWindowMax(Math.Max(1, lbLength));
        _lowWindow = new RollingWindowMin(Math.Max(1, lbLength));
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _atrMult = atrMult;
    }

    public IndicatorName Name => IndicatorName.LBRPaintBars;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _atrSmoother.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var prevClose = _hasPrev ? _prevClose : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrSmoother.Next(tr, isFinal);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var aatr = _atrMult * atr;
        var upper = highest - aatr;
        var lower = lowest + aatr;

        if (isFinal)
        {
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "LowerBand", lower },
                { "MiddleBand", aatr }
            };
        }

        return new StreamingIndicatorStateResult(aatr, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class LeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _wma;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;

    public LeastSquaresMovingAverageState(int length = 25, InputName inputName = InputName.Close)
    {
        _wma = new WmaState(length);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public LeastSquaresMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wma = new WmaState(length);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LeastSquaresMovingAverage;

    public void Reset()
    {
        _wma.Reset();
        _sma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma = _wma.GetNext(value, isFinal);
        var sma = _sma.Next(value, isFinal);
        var lsma = (3 * wma) - (2 * sma);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Lsma", lsma }
            };
        }

        return new StreamingIndicatorStateResult(lsma, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
        _sma.Dispose();
    }
}

public sealed class LeoMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _wma;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;

    public LeoMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _wma = new WmaState(length);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public LeoMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wma = new WmaState(length);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LeoMovingAverage;

    public void Reset()
    {
        _wma.Reset();
        _sma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma = _wma.GetNext(value, isFinal);
        var sma = _sma.Next(value, isFinal);
        var lma = (2 * wma) - sma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Lma", lma }
            };
        }

        return new StreamingIndicatorStateResult(lma, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
        _sma.Dispose();
    }
}

public sealed class LightLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _sma1;
    private readonly IMovingAverageSmoother _sma2;
    private readonly IMovingAverageSmoother _indexMa;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly StandardDeviationVolatilityState _indexStdDev;
    private readonly StreamingInputResolver _input;
    private double _indexValue;
    private int _index;

    public LightLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 250,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        var length1 = Math.Max(1, (int)Math.Ceiling(_length / 2d));
        _sma1 = MovingAverageSmootherFactory.Create(maType, _length);
        _sma2 = MovingAverageSmootherFactory.Create(maType, length1);
        _indexMa = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LightLeastSquaresMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        var length1 = Math.Max(1, (int)Math.Ceiling(_length / 2d));
        _sma1 = MovingAverageSmootherFactory.Create(maType, _length);
        _sma2 = MovingAverageSmootherFactory.Create(maType, length1);
        _indexMa = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _indexStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LightLeastSquaresMovingAverage;

    public void Reset()
    {
        _sma1.Reset();
        _sma2.Reset();
        _indexMa.Reset();
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

        var sma1 = _sma1.Next(value, isFinal);
        var sma2 = _sma2.Next(value, isFinal);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var indexStdDev = _indexStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var indexMa = _indexMa.Next(index, isFinal);

        var c = stdDev != 0 ? (sma2 - sma1) / stdDev : 0;
        var z = indexStdDev != 0 && c != 0 ? (index - indexMa) / indexStdDev * c : 0;
        var y = sma1 + (z * stdDev);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Llsma", y }
            };
        }

        return new StreamingIndicatorStateResult(y, outputs);
    }

    public void Dispose()
    {
        _sma1.Dispose();
        _sma2.Dispose();
        _indexMa.Dispose();
        _stdDev.Dispose();
        _indexStdDev.Dispose();
    }
}

public sealed class LindaRaschke3_10OscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _macdSignalSmoother;
    private readonly IMovingAverageSmoother _ppoSignalSmoother;
    private readonly StreamingInputResolver _input;

    public LindaRaschke3_10OscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 3, int slowLength = 10, int smoothLength = 16, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _macdSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _ppoSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public LindaRaschke3_10OscillatorState(MovingAvgType maType, int fastLength, int slowLength, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _macdSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _ppoSignalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LindaRaschke3_10Oscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _macdSignalSmoother.Reset();
        _ppoSignalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var ppo = slow != 0 ? (fast - slow) / slow * 100 : 0;
        var macd = fast - slow;
        var macdSignal = _macdSignalSmoother.Next(macd, isFinal);
        var ppoSignal = _ppoSignalSmoother.Next(ppo, isFinal);
        var macdHistogram = macd - macdSignal;
        var ppoHistogram = ppo - ppoSignal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "LindaMacd", macd },
                { "LindaMacdSignal", macdSignal },
                { "LindaMacdHistogram", macdHistogram },
                { "LindaPpo", ppo },
                { "LindaPpoSignal", ppoSignal },
                { "LindaPpoHistogram", ppoHistogram }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _macdSignalSmoother.Dispose();
        _ppoSignalSmoother.Dispose();
    }
}

public sealed class LinearExtrapolationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;
    private int _index;

    public LinearExtrapolationState(int length = 500, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(Math.Max(1, _length * 2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearExtrapolationState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _values = new PooledRingBuffer<double>(Math.Max(1, _length * 2));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearExtrapolation;

    public void Reset()
    {
        _values.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var index = (double)_index;
        var priorY = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var priorY2 = EhlersStreamingWindow.GetOffsetValue(_values, value, _length * 2);
        var priorX = _index >= _length ? index - _length : 0;
        var priorX2 = _index >= _length * 2 ? index - (_length * 2) : 0;
        var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0
            ? priorY + ((index - priorX) / (priorX2 - priorX) * (priorY2 - priorY))
            : priorY;

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
                { "LinExt", ext }
            };
        }

        return new StreamingIndicatorStateResult(ext, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class LinearQuadraticConvergenceDivergenceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly LinearRegressionState _linReg;
    private readonly QuadraticRegressionEngine _quadReg;
    private readonly IMovingAverageSmoother _signalSmoother;

    public LinearQuadraticConvergenceDivergenceOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50, int signalLength = 25, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _linReg = new LinearRegressionState(resolved, inputName);
        _quadReg = new QuadraticRegressionEngine(maType, resolved, inputName);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public LinearQuadraticConvergenceDivergenceOscillatorState(MovingAvgType maType, int length, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _linReg = new LinearRegressionState(resolved, selector);
        _quadReg = new QuadraticRegressionEngine(maType, resolved, selector);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.LinearQuadraticConvergenceDivergenceOscillator;

    public void Reset()
    {
        _linReg.Reset();
        _quadReg.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var linReg = _linReg.Update(bar, isFinal, includeOutputs: false).Value;
        var quadReg = _quadReg.Next(bar, isFinal);
        var lqcd = quadReg - linReg;
        var signal = _signalSmoother.Next(lqcd, isFinal);
        var osc = lqcd - signal;
        var hist = osc - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Lqcdo", hist }
            };
        }

        return new StreamingIndicatorStateResult(hist, outputs);
    }

    public void Dispose()
    {
        _linReg.Dispose();
        _quadReg.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class LinearRegressionLineState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowCorrelation _correlation;
    private readonly IMovingAverageSmoother _yMa;
    private readonly IMovingAverageSmoother _xMa;
    private readonly StandardDeviationVolatilityState _yStdDev;
    private readonly StandardDeviationVolatilityState _xStdDev;
    private readonly StreamingInputResolver _input;
    private double _indexValue;
    private int _index;

    public LinearRegressionLineState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _correlation = new RollingWindowCorrelation(_length);
        _yMa = MovingAverageSmootherFactory.Create(maType, _length);
        _xMa = MovingAverageSmootherFactory.Create(maType, _length);
        _yStdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _xStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearRegressionLineState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _correlation = new RollingWindowCorrelation(_length);
        _yMa = MovingAverageSmootherFactory.Create(maType, _length);
        _xMa = MovingAverageSmootherFactory.Create(maType, _length);
        _yStdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _xStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _indexValue);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearRegressionLine;

    public void Reset()
    {
        _correlation.Reset();
        _yMa.Reset();
        _xMa.Reset();
        _yStdDev.Reset();
        _xStdDev.Reset();
        _indexValue = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var x = (double)_index;
        _indexValue = x;

        var corr = isFinal
            ? _correlation.Add(value, x, out _)
            : _correlation.Preview(value, x, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;
        var yMa = _yMa.Next(value, isFinal);
        var xMa = _xMa.Next(x, isFinal);
        var my = _yStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var mx = _xStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var slope = mx != 0 ? corr * (my / mx) : 0;
        var inter = yMa - (slope * xMa);
        var reg = (x * slope) + inter;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "LinReg", reg }
            };
        }

        return new StreamingIndicatorStateResult(reg, outputs);
    }

    public void Dispose()
    {
        _correlation.Dispose();
        _yMa.Dispose();
        _xMa.Dispose();
        _yStdDev.Dispose();
        _xStdDev.Dispose();
    }
}

public sealed class LinearTrailingStopState : IStreamingIndicatorState
{
    private readonly double _s;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevA2;
    private double _prevUpper;
    private double _prevLower;
    private double _prevOs;
    private bool _hasPrevA;
    private bool _hasPrevA2;

    public LinearTrailingStopState(int length = 14, double mult = 28, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _s = 1d / resolved;
        _mult = mult;
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearTrailingStopState(int length, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _s = 1d / resolved;
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearTrailingStop;

    public void Reset()
    {
        _prevA = 0;
        _prevA2 = 0;
        _prevUpper = 0;
        _prevLower = 0;
        _prevOs = 0;
        _hasPrevA = false;
        _hasPrevA2 = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevA = _hasPrevA ? _prevA : value;
        var prevA2 = _hasPrevA2 ? _prevA2 : value;
        var x = value + ((prevA - prevA2) * _mult);
        var a = x > prevA + _s ? prevA + _s : x < prevA - _s ? prevA - _s : prevA;
        var up = a + (Math.Abs(a - prevA) * _mult);
        var dn = a - (Math.Abs(a - prevA) * _mult);
        var upper = up == a ? _prevUpper : up;
        var lower = dn == a ? _prevLower : dn;
        var os = value > upper ? 1 : value > lower ? 0 : _prevOs;
        var ts = (os * lower) + ((1 - os) * upper);

        if (isFinal)
        {
            _prevA2 = _prevA;
            _prevA = a;
            _hasPrevA2 = _hasPrevA;
            _hasPrevA = true;
            _prevUpper = upper;
            _prevLower = lower;
            _prevOs = os;
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

public sealed class LinearWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _wma;
    private readonly StreamingInputResolver _input;

    public LinearWeightedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _wma = new WmaState(length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LinearWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _wma = new WmaState(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearWeightedMovingAverage;

    public void Reset()
    {
        _wma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var lwma = _wma.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Lwma", lwma }
            };
        }

        return new StreamingIndicatorStateResult(lwma, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
    }
}

public sealed class LiquidRelativeStrengthIndexState : IStreamingIndicatorState
{
    private readonly double _k;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevVolume;
    private double _numEma;
    private double _denEma;
    private bool _hasPrev;

    public LiquidRelativeStrengthIndexState(int length = 14, InputName inputName = InputName.Close)
    {
        _k = 1d / Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LiquidRelativeStrengthIndexState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _k = 1d / Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LiquidRelativeStrengthIndex;

    public void Reset()
    {
        _prevValue = 0;
        _prevVolume = 0;
        _numEma = 0;
        _denEma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevVolume = _hasPrev ? _prevVolume : 0;
        var a = _hasPrev ? value - prevValue : 0;
        var b = _hasPrev ? bar.Volume - prevVolume : 0;
        var num = Math.Max(a, 0) * Math.Max(b, 0);
        var den = Math.Abs(a) * Math.Abs(b);
        var numEma = (num * _k) + (_numEma * (1 - _k));
        var denEma = (den * _k) + (_denEma * (1 - _k));
        var lrsi = denEma != 0 ? MathHelper.MinOrMax(100 * numEma / denEma, 100, 0) : 0;

        if (isFinal)
        {
            _prevValue = value;
            _prevVolume = bar.Volume;
            _numEma = numEma;
            _denEma = denEma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Lrsi", lrsi }
            };
        }

        return new StreamingIndicatorStateResult(lrsi, outputs);
    }
}

public sealed class LogisticCorrelationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly RollingWindowCorrelation _correlation;
    private readonly StreamingInputResolver _input;
    private int _index;

    public LogisticCorrelationState(int length = 100, double k = 10, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _k = k;
        _correlation = new RollingWindowCorrelation(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public LogisticCorrelationState(int length, double k, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _k = k;
        _correlation = new RollingWindowCorrelation(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LogisticCorrelation;

    public void Reset()
    {
        _correlation.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var x = (double)_index;
        var corr = isFinal
            ? _correlation.Add(x, value, out _)
            : _correlation.Preview(x, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;
        var log = 1 / (1 + MathHelper.Exp(_k * -corr));

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "LogCorr", log }
            };
        }

        return new StreamingIndicatorStateResult(log, outputs);
    }

    public void Dispose()
    {
        _correlation.Dispose();
    }
}

public sealed class MacZIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly IMovingAverageSmoother _wilderSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;

    public MacZIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 12,
        int slowLength = 25, int signalLength = 9, int length = 25, double gamma = 0.02, double mult = 1,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, inputName);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _wilderSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WildersSmoothingMethod, resolved);
        _input = new StreamingInputResolver(inputName, null);
        _mult = mult;
        _ = gamma;
    }

    public MacZIndicatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength, int length,
        double gamma, double mult, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _stdDev = new StandardDeviationVolatilityState(maType, resolved, selector);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _wilderSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.WildersSmoothingMethod, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _mult = mult;
        _ = gamma;
    }

    public IndicatorName Name => IndicatorName.MacZIndicator;

    public void Reset()
    {
        _stdDev.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
        _wilderSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var fastMa = _fastSmoother.Next(value, isFinal);
        var slowMa = _slowSmoother.Next(value, isFinal);
        var wima = _wilderSmoother.Next(value, isFinal);
        var zscore = stdev != 0 ? (value - wima) / stdev : 0;
        var macd = fastMa - slowMa;
        var macz = stdev != 0 ? (zscore * _mult) + (_mult * macd / stdev) : zscore;
        var signal = _signalSmoother.Next(macz, isFinal);
        var histogram = macz - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Macz", macz },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macz, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
        _wilderSmoother.Dispose();
    }
}

public sealed class MacZVwapIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly StreamingInputResolver _vwapInput;
    private readonly double _gamma;
    private double _volSum;
    private double _volPriceSum;
    private double _l0;
    private double _l1;
    private double _l2;
    private double _l3;
    private bool _hasPrev;

    public MacZVwapIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 12,
        int slowLength = 25, int signalLength = 9, int length1 = 20, int length2 = 25, double gamma = 0.02,
        InputName inputName = InputName.Close)
    {
        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length2), inputName);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
        _vwapInput = new StreamingInputResolver(InputName.TypicalPrice, null);
        _gamma = gamma;
        _ = length1;
    }

    public MacZVwapIndicatorState(MovingAvgType maType, int fastLength, int slowLength, int signalLength, int length1,
        int length2, double gamma, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _stdDev = new StandardDeviationVolatilityState(maType, Math.Max(1, length2), selector);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
        _vwapInput = new StreamingInputResolver(InputName.TypicalPrice, null);
        _gamma = gamma;
        _ = length1;
    }

    public IndicatorName Name => IndicatorName.MacZVwapIndicator;

    public void Reset()
    {
        _stdDev.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
        _volSum = 0;
        _volPriceSum = 0;
        _l0 = 0;
        _l1 = 0;
        _l2 = 0;
        _l3 = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var fastMa = _fastSmoother.Next(value, isFinal);
        var slowMa = _slowSmoother.Next(value, isFinal);
        var vwapValue = _vwapInput.GetValue(bar);

        var volSum = _volSum + bar.Volume;
        var volPriceSum = _volPriceSum + (vwapValue * bar.Volume);
        var vwap = volSum != 0 ? volPriceSum / volSum : 0;
        var vwapSd = MathHelper.Sqrt(vwap);
        var zscore = vwapSd != 0 ? (value - vwap) / vwapSd : 0;

        var macd = fastMa - slowMa;
        var maczt = stdev != 0 ? zscore + (macd / stdev) : zscore;

        var prevL0 = _hasPrev ? _l0 : maczt;
        var prevL1 = _hasPrev ? _l1 : maczt;
        var prevL2 = _hasPrev ? _l2 : maczt;
        var prevL3 = _hasPrev ? _l3 : maczt;

        var l0 = ((1 - _gamma) * maczt) + (_gamma * prevL0);
        var l1 = (-1 * _gamma * l0) + prevL0 + (_gamma * prevL1);
        var l2 = (-1 * _gamma * l1) + prevL1 + (_gamma * prevL2);
        var l3 = (-1 * _gamma * l2) + prevL2 + (_gamma * prevL3);
        var macz = (l0 + (2 * l1) + (2 * l2) + l3) / 6;
        var signal = _signalSmoother.Next(macz, isFinal);
        var histogram = macz - signal;

        if (isFinal)
        {
            _volSum = volSum;
            _volPriceSum = volPriceSum;
            _l0 = l0;
            _l1 = l1;
            _l2 = l2;
            _l3 = l3;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Macz", macz },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(macz, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class MarketDirectionIndicatorState : IStreamingIndicatorState
{
    private readonly int _fastLength;
    private readonly int _slowLength;
    private readonly RollingCumulativeSum _sumWindow;
    private readonly StreamingInputResolver _input;
    private double _prevCp2;
    private double _prevValue;
    private bool _hasPrev;

    public MarketDirectionIndicatorState(int fastLength = 13, int slowLength = 55, InputName inputName = InputName.Close)
    {
        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _sumWindow = new RollingCumulativeSum();
        _input = new StreamingInputResolver(inputName, null);
    }

    public MarketDirectionIndicatorState(int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastLength = Math.Max(1, fastLength);
        _slowLength = Math.Max(1, slowLength);
        _sumWindow = new RollingCumulativeSum();
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.MarketDirectionIndicator;

    public void Reset()
    {
        _sumWindow.Reset();
        _prevCp2 = 0;
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var len1Sum = _sumWindow.Preview(value, _fastLength - 1);
        var len2Sum = _sumWindow.Preview(value, _slowLength - 1);
        var denom = _slowLength - _fastLength;
        var cp2 = denom != 0 ? ((_fastLength * len2Sum) - (_slowLength * len1Sum)) / denom : 0;
        var prevValue = _hasPrev ? _prevValue : 0;
        var mdi = value + prevValue != 0
            ? 100 * (_prevCp2 - cp2) / ((value + prevValue) / 2)
            : 0;

        if (isFinal)
        {
            _sumWindow.Add(value, 0);
            _prevCp2 = cp2;
            _prevValue = value;
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

internal sealed class QuadraticRegressionEngine : IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _ySum;
    private readonly RollingWindowSum _x1Sum;
    private readonly RollingWindowSum _x2Sum;
    private readonly RollingWindowSum _x1x2Sum;
    private readonly RollingWindowSum _yx1Sum;
    private readonly RollingWindowSum _yx2Sum;
    private readonly RollingWindowSum _x2PowSum;
    private readonly IMovingAverageSmoother _x1Ma;
    private readonly IMovingAverageSmoother _x2Ma;
    private readonly IMovingAverageSmoother _yMa;
    private readonly StreamingInputResolver _input;
    private int _index;

    public QuadraticRegressionEngine(MovingAvgType maType, int length, InputName inputName)
    {
        _length = Math.Max(1, length);
        _ySum = new RollingWindowSum(_length);
        _x1Sum = new RollingWindowSum(_length);
        _x2Sum = new RollingWindowSum(_length);
        _x1x2Sum = new RollingWindowSum(_length);
        _yx1Sum = new RollingWindowSum(_length);
        _yx2Sum = new RollingWindowSum(_length);
        _x2PowSum = new RollingWindowSum(_length);
        _x1Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _x2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _yMa = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public QuadraticRegressionEngine(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _ySum = new RollingWindowSum(_length);
        _x1Sum = new RollingWindowSum(_length);
        _x2Sum = new RollingWindowSum(_length);
        _x1x2Sum = new RollingWindowSum(_length);
        _yx1Sum = new RollingWindowSum(_length);
        _yx2Sum = new RollingWindowSum(_length);
        _x2PowSum = new RollingWindowSum(_length);
        _x1Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _x2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _yMa = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public double Next(OhlcvBar bar, bool isFinal)
    {
        var y = _input.GetValue(bar);
        var x1 = (double)_index;
        var x2 = x1 * x1;
        var x1x2 = x1 * x2;
        var yx1 = y * x1;
        var yx2 = y * x2;
        var x2Pow = x2 * x2;

        var ySum = isFinal ? _ySum.Add(y, out _) : _ySum.Preview(y, out _);
        var x1Sum = isFinal ? _x1Sum.Add(x1, out _) : _x1Sum.Preview(x1, out _);
        var x2Sum = isFinal ? _x2Sum.Add(x2, out _) : _x2Sum.Preview(x2, out _);
        var x1x2Sum = isFinal ? _x1x2Sum.Add(x1x2, out _) : _x1x2Sum.Preview(x1x2, out _);
        var yx1Sum = isFinal ? _yx1Sum.Add(yx1, out _) : _yx1Sum.Preview(yx1, out _);
        var yx2Sum = isFinal ? _yx2Sum.Add(yx2, out _) : _yx2Sum.Preview(yx2, out _);
        var x2PowSum = isFinal ? _x2PowSum.Add(x2Pow, out _) : _x2PowSum.Preview(x2Pow, out _);

        var max1 = _x1Ma.Next(x1, isFinal);
        var max2 = _x2Ma.Next(x2, isFinal);
        var may = _yMa.Next(y, isFinal);

        var s11 = x2Sum - ((x1Sum * x1Sum) / _length);
        var s12 = x1x2Sum - ((x1Sum * x2Sum) / _length);
        var s22 = x2PowSum - ((x2Sum * x2Sum) / _length);
        var sy1 = yx1Sum - ((ySum * x1Sum) / _length);
        var sy2 = yx2Sum - ((ySum * x2Sum) / _length);
        var bot = (s22 * s11) - (s12 * s12);
        var b2 = bot != 0 ? ((sy1 * s22) - (sy2 * s12)) / bot : 0;
        var b3 = bot != 0 ? ((sy2 * s11) - (sy1 * s12)) / bot : 0;
        var b1 = may - (b2 * max1) - (b3 * max2);
        var result = b1 + (b2 * x1) + (b3 * x2);

        if (isFinal)
        {
            _index++;
        }

        return result;
    }

    public void Reset()
    {
        _ySum.Reset();
        _x1Sum.Reset();
        _x2Sum.Reset();
        _x1x2Sum.Reset();
        _yx1Sum.Reset();
        _yx2Sum.Reset();
        _x2PowSum.Reset();
        _x1Ma.Reset();
        _x2Ma.Reset();
        _yMa.Reset();
        _index = 0;
    }

    public void Dispose()
    {
        _ySum.Dispose();
        _x1Sum.Dispose();
        _x2Sum.Dispose();
        _x1x2Sum.Dispose();
        _yx1Sum.Dispose();
        _yx2Sum.Dispose();
        _x2PowSum.Dispose();
        _x1Ma.Dispose();
        _x2Ma.Dispose();
        _yMa.Dispose();
    }
}
