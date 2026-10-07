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
    private readonly ExactPopulationWindow _primaryStdDev;
    private readonly ExactPopulationWindow _marketStdDev;
    private readonly PairedAverage _primaryAbsSma;
    private readonly PairedAverage _marketAbsSma;
    private double _prevValue;
    private bool _hasPrev;
    private double _prevMarketValue;
    private bool _hasMarketPrev;
    private TechnicalRatingValue _latestMarketAbsSma;
    private int _index;
    private bool _hasMarketAbsSma;

    public RelativeNormalizedVolatilityState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        var resolved = Math.Max(1, length);
        _primaryStdDev = new ExactPopulationWindow(resolved);
        _marketStdDev = new ExactPopulationWindow(resolved);
        _primaryAbsSma = new PairedAverage(maType, resolved);
        _marketAbsSma = new PairedAverage(maType, resolved);
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
        _index = 0;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (!_alignment.CanUpdate(context, _primarySeries, _marketSeries, series, bar, isFinal))
            return new MultiSeriesIndicatorStateResult(false, 0d, null);

        if (series.Equals(_marketSeries))
        {
            var stdDev = _marketStdDev.Next(bar.Close, isFinal);
            var absZsp = NormalizedChange(bar.Close, _prevMarketValue, _hasMarketPrev, stdDev);
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

        var stdDevPrimary = _primaryStdDev.Next(bar.Close, false);
        var absZsrc = NormalizedChange(bar.Close, _prevValue, _hasPrev, stdDevPrimary);
        var absZsrcSma = _primaryAbsSma.Next(absZsrc, false);

        TechnicalRatingValue absZspSma;
        if (_hasMarketAbsSma)
        {
            absZspSma = _latestMarketAbsSma;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            var marketStdDev = _marketStdDev.Next(marketBar.Close, false);
            var absZsp = NormalizedChange(marketBar.Close, _prevMarketValue, _hasMarketPrev, marketStdDev);
            absZspSma = _marketAbsSma.Next(absZsp, false);
        }
        else
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var rnv = PairedOutput.Publish(GetType(), 0, _index, absZsrcSma / absZspSma);

        if (isFinal)
        {
            _primaryStdDev.Next(bar.Close, true);
            _primaryAbsSma.Next(absZsrc, true);
            _prevValue = bar.Close;
            _hasPrev = true;
            _index++;
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

    private static TechnicalRatingValue NormalizedChange(double value, double previous, bool hasPrevious, double deviation)
    {
        if (!hasPrevious || deviation == 0) return default;
        var change = (TechnicalRatingValue)value - previous;
        return new TechnicalRatingValue(System.Numerics.BigInteger.Abs(change.Units)) / deviation;
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
    private readonly PairedAverage _fastMa;
    private readonly PairedAverage _medMa;
    private readonly PairedAverage _slowMa;
    private readonly PairedAverage _vSlowMa;
    private readonly PairedAverage _rs2Ma;
    private readonly RollingWindowSum _xSum;
    private TechnicalRatingValue _prevR1;
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
        _fastMa = new PairedAverage(maType, Math.Max(1, length3));
        _medMa = new PairedAverage(maType, Math.Max(1, length2));
        _slowMa = new PairedAverage(maType, Math.Max(1, length4));
        _vSlowMa = new PairedAverage(maType, Math.Max(1, length5));
        _rs2Ma = new PairedAverage(maType, Math.Max(1, length1));
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

        var r1 = marketValue != 0 ? (TechnicalRatingValue)bar.Close / marketValue * 100d : _hasPrevR1 ? _prevR1 : 0;
        var fastMa = _fastMa.Next(r1, isFinal);
        var medMa = _medMa.Next(fastMa, isFinal);
        var slowMa = _slowMa.Next(fastMa, isFinal);
        var vSlowMa = _vSlowMa.Next(slowMa, isFinal);
        double t1 = fastMa.Units.CompareTo(medMa.Units) >= 0 && medMa.Units.CompareTo(slowMa.Units) >= 0 && slowMa.Units.CompareTo(vSlowMa.Units) >= 0 ? 10 : 0;
        double t2 = fastMa.Units.CompareTo(medMa.Units) >= 0 && medMa.Units.CompareTo(slowMa.Units) >= 0 && slowMa.Units.CompareTo(vSlowMa.Units) < 0 ? 9 : 0;
        double t3 = fastMa.Units.CompareTo(medMa.Units) < 0 && medMa.Units.CompareTo(slowMa.Units) >= 0 && slowMa.Units.CompareTo(vSlowMa.Units) >= 0 ? 9 : 0;
        double t4 = fastMa.Units.CompareTo(medMa.Units) < 0 && medMa.Units.CompareTo(slowMa.Units) >= 0 && slowMa.Units.CompareTo(vSlowMa.Units) < 0 ? 5 : 0;
        var rs2 = t1 + t2 + t3 + t4;
        var rs2Ma = _rs2Ma.Next(rs2, isFinal).Publish();
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
public sealed class RetentionAccelerationFilterState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => false;
    private readonly RetentionAccelerationWindow _window;
    public RetentionAccelerationFilterState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.RetentionAccelerationFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var next = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(next.Value, includeOutputs ? new Dictionary<string, double> { { "Raf", next.Value } } : null);
    }
    public void Dispose() => Reset();
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
    private readonly ReverseRsiWindow _window;
    public ReverseEngineeringRelativeStrengthIndexState(int length = 14, double rsiLevel = 50) => _window = new(length, rsiLevel);
    public IndicatorName Name => IndicatorName.ReverseEngineeringRelativeStrengthIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string,double> { ["Rersi"] = point.Value } : null);
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
