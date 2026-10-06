#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Ro")]
public sealed class RexOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly RexWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public RexOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) { _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.RexOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var r = _window.Next(_input.GetValue(bar), bar.Open, bar.High, bar.Low, isFinal); return new(r.Value, includeOutputs ? new Dictionary<string, double> { { "Ro", r.Value }, { "Signal", r.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rsrma")]
public sealed class RightSidedRickerMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RickerWindow _window;
    public RightSidedRickerMovingAverageState(int length = 50, double pctWidth = 60) => _window = new(length, pctWidth);
    public IndicatorName Name => IndicatorName.RightSidedRickerMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Rsrma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Rwo")]
public sealed class RobustWeightingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RobustWeightingWindow? _wide;
    private readonly int _length;
    private readonly RollingWindowCorrelation _corrWindow = null!;
    private readonly IMovingAverageSmoother _sma = null!;
    private readonly IMovingAverageSmoother _indexSma = null!;
    private readonly IMovingAverageSmoother _lSma = null!;
    private readonly RollingStandardDeviation _stdDev = null!;
    private readonly RollingStandardDeviation _indexStdDev = null!;
    private readonly StreamingInputResolver _input = default;
    private double _indexValue;
    private int _index;

    public RobustWeightingOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        if (StrengthWindow.Supports(maType)) { _wide = new(maType, length); return; }
        _length = Math.Max(1, length);
        _corrWindow = new RollingWindowCorrelation(_length);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _indexSma = MovingAverageSmootherFactory.Create(maType, _length);
        _lSma = MovingAverageSmootherFactory.Create(maType, _length);
        _stdDev = new RollingStandardDeviation(_length);
        _indexStdDev = new RollingStandardDeviation(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RobustWeightingOscillator;

    public void Reset()
    {
        if (_wide is not null) { _wide.Reset(); return; }
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
        StreamingInputValidation.Validate(bar);
        if (_wide is not null)
        {
            var point = _wide.Next(bar.Close, isFinal);
            return new(point.Value, includeOutputs ? new Dictionary<string,double> { ["Rwo"] = point.Value } : null);
        }
        var value = _input.GetValue(bar);
        var index = (double)_index;
        _indexValue = index;

        var corr = isFinal ? _corrWindow.Add(index, value, out _) : _corrWindow.Preview(index, value, out _);
        corr = MathHelper.IsValueNullOrInfinity(corr) ? 0 : corr;

        var sma = _sma.Next(value, isFinal);
        var indexSma = _indexSma.Next(index, isFinal);
        var stdDev = _stdDev.Next(value, isFinal);
        var indexStdDev = _indexStdDev.Next(_indexValue, isFinal);

        var a = indexStdDev != 0 ? corr * (stdDev / indexStdDev) : 0;
        var b = sma - (a * indexSma);
        var l = value - ((a * index) + b);
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
        if (_wide is not null) { _wide.Dispose(); return; }
        _corrWindow.Dispose();
        _sma.Dispose();
        _indexSma.Dispose();
        _lSma.Dispose();
        _stdDev.Dispose();
        _indexStdDev.Dispose();
    }
}

[PrimaryOutput("Rsing")]
public sealed class RSINGIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => false;
    private readonly RsingWindow _window;
    public RSINGIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.RSINGIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.High, bar.Low, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Rsing", value.Line }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

public sealed class RSMKIndicatorState : IMultiSeriesIndicatorState, IDisposable, Validation.IMultiSeriesInputDomainContract
{
    private readonly PairedSeriesAlignment _alignment = new();
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
    public Validation.IndicatorInputDomain PrimaryInputDomain => Validation.IndicatorInputDomain.PositiveClose;
    public Validation.IndicatorInputDomain BenchmarkInputDomain => Validation.IndicatorInputDomain.PositiveClose;


    public void Reset()
    {
        _alignment.Reset();
        _logDiffEma.Reset();
        _logRatios.Clear();
        _lastMarketValue = 0;
        _hasMarket = false;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (bar is null) throw new ArgumentNullException(nameof(bar));
        if ((series.Equals(_primarySeries) || series.Equals(_marketSeries))
            && (!(bar.Close > 0) || double.IsInfinity(bar.Close)))
            throw new ArgumentOutOfRangeException(nameof(bar), "RSMK requires strictly positive finite primary and benchmark closes.");
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

        var logRatio = Math.Log(bar.Close) - Math.Log(marketValue);
        var count = _logRatios.Count;
        var prevLogRatio = count >= _length ? _logRatios[count - _length] : 0;
        var logDiff = count < _length ? 0 : logRatio - prevLogRatio;
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

        _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(true, rsmk, outputs);
    }

    public void Dispose()
    {
        _logDiffEma.Dispose();
        _logRatios.Dispose();
    }
}

[PrimaryOutput("Req")]
public sealed class RunningEquityState : IStreamingIndicatorState, IDisposable
{
    private readonly RunningEquityWindow _window; private readonly StreamingInputResolver _input;
    public RunningEquityState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100) { _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.RunningEquity;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Req", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Stc")]
public sealed class SchaffTrendCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly SchaffFirstPassWindow _window;
    public SchaffTrendCycleState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 23, int slowLength = 50, int cycleLength = 10) => _window = new(maType, fastLength, slowLength, cycleLength);
    public IndicatorName Name => IndicatorName.SchaffTrendCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Stc", value } } : null); }
    public void Dispose() => _window.Dispose();
}

public sealed class SectorRotationModelState : IMultiSeriesIndicatorState, IDisposable
{
    private readonly PairedSeriesAlignment _alignment = new();
    private readonly SeriesKey _primarySeries;
    private readonly SeriesKey _marketSeries;
    private readonly PairedRoc _primaryRoc1;
    private readonly PairedRoc _primaryRoc2;
    private readonly PairedRoc _marketRoc1;
    private readonly PairedRoc _marketRoc2;
    private readonly PairedAverage _signal;
    private TechnicalRatingValue _lastMarketRoc1;
    private TechnicalRatingValue _lastMarketRoc2;
    private bool _hasMarket;
    private int _index;

    public SectorRotationModelState(SeriesKey primarySeries, SeriesKey marketSeries,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25, int length2 = 75)
    {
        _primarySeries = primarySeries;
        _marketSeries = marketSeries;
        var resolved1 = Math.Max(1, length1);
        _primaryRoc1 = new PairedRoc(resolved1);
        _primaryRoc2 = new PairedRoc(Math.Max(1, length2));
        _marketRoc1 = new PairedRoc(resolved1);
        _marketRoc2 = new PairedRoc(Math.Max(1, length2));
        _signal = new PairedAverage(maType, resolved1);
    }

    public IndicatorName Name => IndicatorName.SectorRotationModel;

    public void Reset()
    {
        _alignment.Reset();
        _primaryRoc1.Reset();
        _primaryRoc2.Reset();
        _marketRoc1.Reset();
        _marketRoc2.Reset();
        _signal.Reset();
        _lastMarketRoc1 = 0;
        _lastMarketRoc2 = 0;
        _hasMarket = false;
        _index = 0;
    }

    public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
        bool isFinal, bool includeOutputs)
    {
        if (!_alignment.CanUpdate(context, _primarySeries, _marketSeries, series, bar, isFinal))
            return new MultiSeriesIndicatorStateResult(false, 0d, null);

        if (series.Equals(_marketSeries))
        {
            var roc1 = _marketRoc1.Next(bar.Close, isFinal);
            var roc2 = _marketRoc2.Next(bar.Close, isFinal);
            if (isFinal)
            {
                _lastMarketRoc1 = roc1;
                _lastMarketRoc2 = roc2;
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

        var bull1 = _primaryRoc1.Next(bar.Close, false);
        var bull2 = _primaryRoc2.Next(bar.Close, false);

        TechnicalRatingValue bear1;
        TechnicalRatingValue bear2;
        if (_hasMarket)
        {
            bear1 = _lastMarketRoc1;
            bear2 = _lastMarketRoc2;
        }
        else if (context.TryGetLatest(_marketSeries, out var marketBar))
        {
            bear1 = _marketRoc1.Next(marketBar.Close, false);
            bear2 = _marketRoc2.Next(marketBar.Close, false);
        }
        else
        {
            _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(false, 0d, null);
        }

        var bull = (bull1 + bull2) / 2;
        var bear = (bear1 + bear2) / 2;
        var raw = (TechnicalRatingValue)100d * (bull - bear);
        var osc = PairedOutput.Publish(GetType(), 0, _index, raw);
        var signal = PairedOutput.Publish(GetType(), 1, _index, _signal.Next(raw, false));
        if (isFinal) { _primaryRoc1.Next(bar.Close, true); _primaryRoc2.Next(bar.Close, true); _signal.Next(raw, true); _index++; }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Srm", osc },
                { "Signal", signal }
            };
        }

        _alignment.Commit(_primarySeries, _marketSeries, series, bar, isFinal);
            return new MultiSeriesIndicatorStateResult(true, osc, outputs);
    }

    public void Dispose()
    {




        _signal.Dispose();
    }
}

[PrimaryOutput("SaRsi")]
public sealed class SelfAdjustingRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _signalSmoother;

    // The deviation of the window about its own mean, matching the batch calculation; see #190.
    private readonly ExactPopulationWindow _stdDev;
    private readonly StrengthAverage? _exactSignal;
    private readonly StreamingInputResolver _input;

    public SelfAdjustingRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int smoothingLength = 21, double mult = 2)
    {
        _mult = mult;
        var resolvedLength = Math.Max(1, length);
        _rsi = new RsiState(maType, resolvedLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothingLength));
        // No moving-average type, and no selector: the index is passed to Next directly.
        _stdDev = new ExactPopulationWindow(resolvedLength);
        if (StrengthWindow.Supports(maType)) _exactSignal = new StrengthAverage(maType, smoothingLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SelfAdjustingRelativeStrengthIndex;

    public void Reset()
    {
        _exactSignal?.Reset();
        _rsi.Reset();
        _signalSmoother.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);

        // Fed the relative strength index, which is the series this measures.
        var stdDev = _stdDev.Next(rsi, isFinal);
        var signal = _exactSignal is null ? _signalSmoother.Next(rsi, isFinal) : _exactSignal.Next(new StrengthValue(rsi), isFinal).Mantissa;
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
        _exactSignal?.Dispose();
        _rsi.Dispose();
        _signalSmoother.Dispose();
        _stdDev.Dispose();
    }
}

[PrimaryOutput("Swma")]
public sealed class SelfWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SelfWeightedWindow _window;
    public SelfWeightedMovingAverageState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.SelfWeightedMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Swma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sgi")]
public sealed class SellGravitationIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => false;
    private readonly SellGravitationWindow _window;
    public SellGravitationIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.SellGravitationIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var values = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(values.Line, includeOutputs ? new Dictionary<string, double> { { "Sgi", values.Line }, { "Signal", values.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Szo")]
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
        int fastLength = 14, int slowLength = 30, double factor = 0.95)
    {
        _fastLength = Math.Max(1, fastLength);
        _factor = factor;
        _maxWindow = new RollingWindowMax(Math.Max(1, slowLength));
        _minWindow = new RollingWindowMin(Math.Max(1, slowLength));
        _smoother = MovingAverageSmootherFactory.Create(maType, _fastLength);
        _input = new StreamingInputResolver(InputName.Close, null);
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

[PrimaryOutput("Sfma")]
public sealed class SequentiallyFilteredMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly SequentialMeanGate _gate;
    private readonly StreamingInputResolver _input;

    public SequentiallyFilteredMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 50)
    {
        _sma = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(length)
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _gate = new SequentialMeanGate(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SequentiallyFilteredMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _gate.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var sfma = _gate.Next(value, sma, isFinal);

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
        _gate.Dispose();
    }
}

[PrimaryOutput("Sltsf")]
public sealed class SettingLessTrendStepFilteringState : IStreamingIndicatorState
{
    private readonly SettingLessStepWindow _window = new();
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public SettingLessTrendStepFilteringState() { }
    public IndicatorName Name => IndicatorName.SettingLessTrendStepFiltering;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Sltsf", value } } : null);
    }
}

[PrimaryOutput("Sma")]
public sealed class ShapeshiftingMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ShapeshiftingWindow _window;
    public ShapeshiftingMovingAverageState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.ShapeshiftingMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Sma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sr")]
public sealed class SharpeRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly ReturnScoreWindow _window;
    public SharpeRatioState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, double bmk = .02)
        => _window = new ReturnScoreWindow(maType, length, bmk, false);
    public IndicatorName Name => IndicatorName.SharpeRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Sr", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Smma")]
public sealed class SharpModifiedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AffineAverageWindow _window;
    private readonly IMovingAverageSmoother _average;
    private readonly StreamingInputResolver _input;

    public SharpModifiedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _average = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _window = new AffineAverageWindow(length, sharp: true, exactSimple: maType == MovingAvgType.SimpleMovingAverage);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SharpModifiedMovingAverage;
    public void Reset() { _window.Reset(); _average.Reset(); }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var result = _window.Next(value, _average.Next(value, isFinal), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Smma", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose() { _window.Dispose(); _average.Dispose(); }
}

[PrimaryOutput("ARatio")]
public sealed class ShinoharaIntensityRatioState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ShinoharaWindow _window;
    public ShinoharaIntensityRatioState(int length = 14) => _window = new ShinoharaWindow(length);
    public IndicatorName Name => IndicatorName.ShinoharaIntensityRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var (a, b) = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(a, includeOutputs ? new Dictionary<string, double> { { "ARatio", a }, { "BRatio", b } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sc")]
public sealed class SimpleCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly SimpleCycleWindow _window;
    private readonly StreamingInputResolver _input;
    public SimpleCycleState(int length = 50)
    { _window = new(length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.SimpleCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Sc", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Sl")]
public sealed class SimpleLinesState : IStreamingIndicatorState
{
    private readonly SimpleLinesKernel _kernel;
    public SimpleLinesState(int length = 10, double mult = 10) { _kernel = new(length, mult); }
    public IndicatorName Name => IndicatorName.SimpleLines;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _kernel.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Sl", value } } : null);
    }
}

[PrimaryOutput("Slsma")]
public sealed class SimplifiedLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SimplifiedLeastSquaresWindow _window;
    private readonly StreamingInputResolver _input;

    public SimplifiedLeastSquaresMovingAverageState(int length = 14)
    {
        _window = new SimplifiedLeastSquaresWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SimplifiedLeastSquaresMovingAverage;
    public void Reset() => _window.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var result = _window.Next(value, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double> { { "Slsma", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Swma")]
public sealed class SimplifiedWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _weighted;
    private readonly StreamingInputResolver _input;

    public SimplifiedWeightedMovingAverageState(int length = 14)
    {
        _weighted = new WmaState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SimplifiedWeightedMovingAverage;

    public void Reset() => _weighted.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma = _weighted.GetNext(value, isFinal);

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
        _weighted.Dispose();
    }
}

[PrimaryOutput("Swma")]
public sealed class SineWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly SineWindowMean _mean;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);

    public SineWeightedMovingAverageState(int length = 14) => _mean = new SineWindowMean(length);
    public IndicatorName Name => IndicatorName.SineWeightedMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Swma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

[PrimaryOutput("Ssma")]
public sealed class SlowSmoothedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _l1;
    private readonly IMovingAverageSmoother _l2;
    private readonly IMovingAverageSmoother _l3;
    private readonly StreamingInputResolver _input;

    public SlowSmoothedMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 15)
    {
        var resolved = Math.Max(1, length);
        var w2 = Math.Max(1, Math.Min(530, (int)Math.Ceiling(resolved / 3d)));
        var w1 = Math.Max(1, Math.Min(530, (int)Math.Ceiling((resolved - w2) / 2d)));
        var w3 = Math.Max(1, Math.Min(530, (int)Math.Floor((resolved - w2) / 2d)));
        _l1 = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(w1) : MovingAverageSmootherFactory.Create(maType, w1);
        _l2 = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(w2) : MovingAverageSmootherFactory.Create(maType, w2);
        _l3 = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(w3) : MovingAverageSmootherFactory.Create(maType, w3);
        _input = new StreamingInputResolver(InputName.Close, null);
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

[PrimaryOutput("Smi")]
public sealed class SMIErgodicIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly TrueStrengthIndexState _strength;
    public SMIErgodicIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 5, int slowLength = 20, int signalLength = 5)
        => _strength = new(maType, fastLength, slowLength, signalLength);
    public IndicatorName Name => IndicatorName.SMIErgodicIndicator;
    public void Reset() => _strength.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _strength.Update(bar, isFinal, includeOutputs);
        return new(value.Value, includeOutputs ? new Dictionary<string, double> { { "Smi", value.Value }, { "Signal", value.Outputs!["Signal"] } } : null);
    }
    public void Dispose() => _strength.Dispose();
}
