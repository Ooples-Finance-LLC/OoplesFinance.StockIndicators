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
    private readonly RatioOchlWindow _window=new();
    public RatioOCHLAveragerState(){}
    public IndicatorName Name=>IndicatorName.RatioOCHLAverager;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.Open,bar.High,bar.Low,bar.Close,isFinal);
        return new(value,includeOutputs?new Dictionary<string,double>{{"Rochla",value}}:null);
    }
}

[PrimaryOutput("Rsi")]
public sealed class ReallySimpleIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ReallySimpleWindow _window;
    private readonly StreamingInputResolver _input;
    public ReallySimpleIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, int smoothLength = 10)
    { _window = new(maType, length, smoothLength); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.ReallySimpleIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var point = _window.Next(price, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Rsi", point.Line }, { "Signal", point.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rd")]
public sealed class RecursiveDifferenciatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RecursiveDifferenciatorWindow _window;
    public RecursiveDifferenciatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14, double alpha = 0.6)
        => _window = new(maType, length, alpha);
    public IndicatorName Name => IndicatorName.RecursiveDifferenciator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Rd", point.Line } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rmta")]
public sealed class RecursiveMovingTrendAverageState : IStreamingIndicatorState
{
    private readonly RecursiveTrendWindow _window;
    public RecursiveMovingTrendAverageState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.RecursiveMovingTrendAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rmta", value } } : null);
    }
}

[PrimaryOutput("Rrsi")]
public sealed class RecursiveRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RecursiveRsiWindow _window;
    public RecursiveRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
        => _window = new RecursiveRsiWindow(maType, length);
    public IndicatorName Name => IndicatorName.RecursiveRelativeStrengthIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var result = _window.Next(bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Rrsi", result.Value } } : null;
        return new StreamingIndicatorStateResult(result.Value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rsto")]
public sealed class RecursiveStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly RecursiveStochasticWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public RecursiveStochasticState(int length = 200, double alpha = .1) => _window = new(length, alpha);
    public IndicatorName Name => IndicatorName.RecursiveStochastic;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { var price = _input.GetValue(bar); var value = _window.Next(price, isFinal); return new(value, includeOutputs ? new Dictionary<string, double> { { "Rsto", value } } : null); }
    public void Dispose() => _window.Reset();
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
    private readonly RegularizedWindow _window;
    public RegularizedExponentialMovingAverageState(int length = 14, double lambda = .5) => _window = new(length, lambda);
    public IndicatorName Name => IndicatorName.RegularizedExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rema", value } } : null);
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
public sealed class RelativeVigorIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly RelativeVigorWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public RelativeVigorIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14) { _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.RelativeVigorIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var r = _window.Next(_input.GetValue(bar), bar.Open, bar.High, bar.Low, isFinal); return new(r.Value, includeOutputs ? new Dictionary<string, double> { { "Rvi", r.Value }, { "Signal", r.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rvi")]
public sealed class RelativeVolatilityIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly RelativeVolatilityWindow _window;
    private readonly StreamingInputResolver _input;
    internal RelativeVolatilityIndexV1State(MovingAvgType maType, int length, int smoothLength, InputName inputName) { _window = new(maType, length, smoothLength); _input = new(inputName, null); }
    public RelativeVolatilityIndexV1State(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, int smoothLength = 14) : this(maType, length, smoothLength, InputName.Close) { }
    public IndicatorName Name => IndicatorName.RelativeVolatilityIndexV1;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(_input.GetValue(bar), isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Rvi", value } } : null);
    }
    public void Dispose() => _window.Dispose();
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
        var rvi = RelativeVolatilityWindow.Mean(rviHigh, rviLow);

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
    private readonly RelativeVolumeWindow _window;
    public RelativeVolumeIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 60)
        => _window = new RelativeVolumeWindow(maType, length);
    public IndicatorName Name => IndicatorName.RelativeVolumeIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var (score, demand) = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(score, includeOutputs ? new Dictionary<string, double> { { "Rvi", score }, { "Dpl", demand } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Repulse")]
public sealed class RepulseState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly RepulseWindow _window;
    private readonly StreamingInputResolver _input;
    public RepulseState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    { _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.Repulse;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar); var value = _window.Next(bar.Open, bar.High, bar.Low, close, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Repulse", value.Line }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rma")]
public sealed class RepulsionMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RepulsionWindow _window;
    public RepulsionMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.RepulsionMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Rma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
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
public sealed class RetrospectiveCandlestickChartState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly RetrospectiveCandleWindow _window;
    public RetrospectiveCandlestickChartState(int length=100)=>_window=new RetrospectiveCandleWindow(length);
    public IndicatorName Name=>IndicatorName.RetrospectiveCandlestickChart;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.Open,bar.High,bar.Low,bar.Close,isFinal);
        return new StreamingIndicatorStateResult(value,includeOutputs?new Dictionary<string,double>{{"Rcc",value}}:null);
    }
    public void Dispose()=>_window.Reset();
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
