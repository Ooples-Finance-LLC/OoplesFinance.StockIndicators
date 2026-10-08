#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Attributes;
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
        StreamingInputValidation.Validate(bar);
        var value = _selector != null ? _selector(bar) : StreamingInputSelector.GetValue(bar, _inputName);
        StreamingInputValidation.Finite(value, nameof(value));
        return value;
    }
}

[PrimaryOutput("Sma")]
public sealed class SimpleMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _window;
    private readonly StreamingInputResolver _input;

    public SimpleMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new RollingWindowSum(_length, trackMeanRoundoff: true);
        _input = new StreamingInputResolver(InputName.Close, null);
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
        var mean = _window.Average(value, isFinal, out countAfter);
        var sma = countAfter >= _length ? mean : 0;

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

[PrimaryOutput("Ema")]
public sealed class ExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly EmaState _ema;
    private readonly StreamingInputResolver _input;

    public ExponentialMovingAverageState(int length = 14)
    {
        _ema = new EmaState(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ExponentialMovingAverage;

    public void Reset()
    {
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
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

[PrimaryOutput("Wma")]
public sealed class WeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly WmaState _wma;
    private readonly StreamingInputResolver _input;

    public WeightedMovingAverageState(int length = 14)
    {
        _wma = new WmaState(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WeightedMovingAverage;

    public void Reset()
    {
        _wma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var wma = _wma.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wma", wma }
            };
        }

        return new StreamingIndicatorStateResult(wma, outputs);
    }

    public void Dispose()
    {
        _wma.Dispose();
    }
}

[PrimaryOutput("Wwma")]
public sealed class WellesWilderMovingAverageState : IStreamingIndicatorState
{
    private readonly WilderState _wilder;
    private readonly StreamingInputResolver _input;

    public WellesWilderMovingAverageState(int length = 14)
    {
        _wilder = new WilderState(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WellesWilderMovingAverage;

    public void Reset()
    {
        _wilder.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var wwma = _wilder.GetNext(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wwma", wwma }
            };
        }

        return new StreamingIndicatorStateResult(wwma, outputs);
    }
}

[PrimaryOutput("Tma")]
public sealed class TriangularMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _primary;
    private readonly IMovingAverageSmoother _secondary;
    private readonly StreamingInputResolver _input;

    public TriangularMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _primary = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _secondary = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TriangularMovingAverage;

    public void Reset()
    {
        _primary.Reset();
        _secondary.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var first = _primary.Next(value, isFinal);
        var tma = _secondary.Next(first, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tma", tma }
            };
        }

        return new StreamingIndicatorStateResult(tma, outputs);
    }

    public void Dispose()
    {
        _primary.Dispose();
        _secondary.Dispose();
    }
}

[PrimaryOutput("Hma")]
public sealed class HullMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly HullWindow _window;
    public HullMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.HullMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Hma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}


[PrimaryOutput("AveragePrice")]
public sealed class AveragePriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public AveragePriceState()
    {
        _input = new StreamingInputResolver(InputName.AveragePrice, null);
    }

    public IndicatorName Name => IndicatorName.AveragePrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var averagePrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "AveragePrice", averagePrice }
            };
        }

        return new StreamingIndicatorStateResult(averagePrice, outputs);
    }
}

[PrimaryOutput("FullTp")]
public sealed class FullTypicalPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public FullTypicalPriceState()
    {
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.FullTypicalPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var fullTypicalPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "FullTp", fullTypicalPrice }
            };
        }

        return new StreamingIndicatorStateResult(fullTypicalPrice, outputs);
    }
}

[PrimaryOutput("MedianPrice")]
public sealed class MedianPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public MedianPriceState()
    {
        _input = new StreamingInputResolver(InputName.MedianPrice, null);
    }

    public IndicatorName Name => IndicatorName.MedianPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var medianPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "MedianPrice", medianPrice }
            };
        }

        return new StreamingIndicatorStateResult(medianPrice, outputs);
    }
}

[PrimaryOutput("Tp")]
public sealed class TypicalPriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public TypicalPriceState()
    {
        _input = new StreamingInputResolver(InputName.TypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.TypicalPrice;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var typicalPrice = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tp", typicalPrice }
            };
        }

        return new StreamingIndicatorStateResult(typicalPrice, outputs);
    }
}

[PrimaryOutput("WeightedClose")]
public sealed class WeightedCloseState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;

    public WeightedCloseState()
    {
        _input = new StreamingInputResolver(InputName.WeightedClose, null);
    }

    public IndicatorName Name => IndicatorName.WeightedClose;

    public void Reset()
    {
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var weightedClose = _input.GetValue(bar);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "WeightedClose", weightedClose }
            };
        }

        return new StreamingIndicatorStateResult(weightedClose, outputs);
    }
}

[PrimaryOutput("HCLC2")]
public sealed class MidpointState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;

    public MidpointState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(resolved);
        _minWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Midpoint;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        int maxCount;
        int minCount;
        var highest = isFinal ? _maxWindow.Add(value, out maxCount) : _maxWindow.Preview(value, out maxCount);
        var lowest = isFinal ? _minWindow.Add(value, out minCount) : _minWindow.Preview(value, out minCount);
        var midpoint = PriceMean.Of(highest, lowest);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "HCLC2", midpoint }
            };
        }

        return new StreamingIndicatorStateResult(midpoint, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

[PrimaryOutput("HHLL2")]
public sealed class MidpriceState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;

    public MidpriceState(int length = 14)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
    }

    public IndicatorName Name => IndicatorName.Midprice;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        int highCount;
        int lowCount;
        var highest = isFinal ? _highWindow.Add(bar.High, out highCount) : _highWindow.Preview(bar.High, out highCount);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out lowCount) : _lowWindow.Preview(bar.Low, out lowCount);
        var midprice = PriceMean.Of(highest, lowest);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "HHLL2", midprice }
            };
        }

        return new StreamingIndicatorStateResult(midprice, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("UpperBand")]
public sealed class AverageTrueRangeChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly KeltnerWindow _window;
    private readonly double _multiplier;
    public AverageTrueRangeChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double mult = 2.5)
    { _window = new(maType, length, length, maType); _multiplier = mult; }
    public IndicatorName Name => IndicatorName.AverageTrueRangeChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var stages = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        var point = RangeChannelWindow.Output(bar.Close, stages.Middle, stages.Atr, _multiplier, true);
        return new(point.Upper, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } , { "Sma", point.Average } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class UniChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _mean;
    private readonly double _upper, _lower;
    private readonly bool _additive;
    public UniChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10, double ubFac = .02, double lbFac = .02, bool type1 = false)
    {
        UniChannelArithmetic.Validate(ubFac, lbFac); length = Math.Max(1, length); _upper = ubFac; _lower = lbFac; _additive = type1;
        _mean = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(length) : MovingAverageSmootherFactory.Create(maType, length);
    }
    public IndicatorName Name => IndicatorName.UniChannel;
    public void Reset() => _mean.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var middle = _mean.Next(bar.Close, isFinal);
        return new(middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", UniChannelArithmetic.Band(middle, _upper, _additive) }, { "MiddleBand", middle }, { "LowerBand", UniChannelArithmetic.Band(middle, -_lower, _additive) } } : null);
    }
    public void Dispose() => _mean.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class PriceHeadleyAccelerationBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly HeadleyBandWindow _window;
    public PriceHeadleyAccelerationBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double factor = .001) { _window = new(maType, length, factor); }
    public IndicatorName Name => IndicatorName.PriceHeadleyAccelerationBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class PseudoPolynomialChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly PseudoPolynomialWindow _window;
    public PseudoPolynomialChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double morph = .9) { _window = new(maType, length, morph); }
    public IndicatorName Name => IndicatorName.PseudoPolynomialChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", p.Upper }, { "MiddleBand", p.Middle }, { "LowerBand", p.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class ProjectedSupportAndResistanceState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ProjectedLevelsWindow _window;
    public ProjectedSupportAndResistanceState(int length=25)=>_window=new(length);
    public IndicatorName Name=>IndicatorName.ProjectedSupportAndResistance;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var values=_window.Next(bar.High,bar.Low,isFinal);
        IReadOnlyDictionary<string,double>? outputs=null;
        if(includeOutputs){var result=new Dictionary<string,double>(5);for(var i=0;i<values.Length;i++)result[ProjectedLevelsWindow.Keys[i]]=values[i];outputs=result;}
        return new(values[4],outputs);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class ProjectionBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionFamilyKernel _kernel;
    public ProjectionBandsState(int length = 14) => _kernel = new(IndicatorName.ProjectionBands,length,MovingAvgType.WeightedMovingAverage,4);
    public IndicatorName Name => IndicatorName.ProjectionBands;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs) => _kernel.Update(bar,isFinal,includeOutputs);
    public void Dispose() => _kernel.Dispose();
}

[PrimaryOutput("Pbo")]
public sealed class ProjectionOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionFamilyKernel _kernel;
    public ProjectionOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14, int smoothLength = 4) => _kernel = new(IndicatorName.ProjectionOscillator,length,maType,smoothLength);
    public IndicatorName Name => IndicatorName.ProjectionOscillator;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs) => _kernel.Update(bar,isFinal,includeOutputs);
    public void Dispose() => _kernel.Dispose();
}

[PrimaryOutput("Pbw")]
public sealed class ProjectionBandwidthState : IStreamingIndicatorState, IDisposable
{
    private readonly ProjectionFamilyKernel _kernel;
    public ProjectionBandwidthState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14) => _kernel = new(IndicatorName.ProjectionBandwidth,length,maType,length);
    public IndicatorName Name => IndicatorName.ProjectionBandwidth;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs) => _kernel.Update(bar,isFinal,includeOutputs);
    public void Dispose() => _kernel.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class RootMovingAverageSquaredErrorBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly RmseBandWindow _window;
    public RootMovingAverageSquaredErrorBandsState(double stdDevFactor = 1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14) { _window = new(maType, length, stdDevFactor); }
    public IndicatorName Name => IndicatorName.RootMovingAverageSquaredErrorBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("FastMa")]
public sealed class MovingAverageBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAverageBandWindow _window;
    public MovingAverageBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10, int slowLength = 50, double mult = 1) => _window = new(maType, fastLength, slowLength, mult);
    public IndicatorName Name => IndicatorName.MovingAverageBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Fast, includeOutputs ? new Dictionary<string, double> { { "UpperBand", value.Upper }, { "MiddleBand", value.Middle }, { "LowerBand", value.Lower }, { "FastMa", value.Fast } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class MovingAverageSupportResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly SupportResistanceWindow _window;
    private readonly double _factor;
    public MovingAverageSupportResistanceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10, double factor = 2)
    {
        HighLowBandsWindow.ValidateShift(factor); _factor = factor; _window = new(maType, length);
    }
    public IndicatorName Name => IndicatorName.MovingAverageSupportResistance;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var middle = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(middle, includeOutputs ? new Dictionary<string, double> {
            { "UpperBand", HighLowBandsWindow.Shift(middle, _factor) }, { "MiddleBand", middle }, { "LowerBand", SupportResistanceWindow.Lower(middle, _factor) } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class MotionToAttractionChannelsState : IStreamingIndicatorState
{
    private readonly MotionAttractionWindow _window;
    public MotionToAttractionChannelsState(int length = 14) { _window = new(length); }
    public IndicatorName Name => IndicatorName.MotionToAttractionChannels;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class MeanAbsoluteErrorBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly StreamingInputResolver _input;
    private readonly ExactCumulativeErrorBands _errors;

    public MeanAbsoluteErrorBandsState(double stdDevFactor = 1,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var resolved = Math.Max(1, length);
        _errors = new ExactCumulativeErrorBands(stdDevFactor);
        _meanSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MeanAbsoluteErrorBands;

    public void Reset()
    {
        _meanSmoother.Reset();
        _errors.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var (upper, lower) = _errors.Next(value, middle, isFinal);

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
        _meanSmoother.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class MeanAbsoluteDeviationBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _meanSmoother;
    private readonly ExactMeanAbsoluteDeviationWindow _deviation;
    private readonly StreamingInputResolver _input;
    private readonly double _stdDevFactor;

    public MeanAbsoluteDeviationBandsState(double stdDevFactor = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _meanSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _deviation = new ExactMeanAbsoluteDeviationWindow(resolved);
        _stdDevFactor = stdDevFactor;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MeanAbsoluteDeviationBands;

    public void Reset()
    {
        _meanSmoother.Reset();
        _deviation.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _meanSmoother.Next(value, isFinal);
        var deviation = _deviation.Next(value, isFinal);
        var upper = BollingerArithmetic.Band(middle, deviation, _stdDevFactor);
        var lower = BollingerArithmetic.Band(middle, deviation, -_stdDevFactor);

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
        _meanSmoother.Dispose();
        _deviation.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class MovingAverageDisplacedEnvelopeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length2;
    private readonly IMovingAverageSmoother _emaSmoother;
    private readonly PooledRingBuffer<double> _emaWindow;
    private readonly StreamingInputResolver _input;
    private readonly double _pct;

    public MovingAverageDisplacedEnvelopeState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 9, int length2 = 13, double pct = 0.5)
    {
        _length2 = Math.Max(1, length2);
        _emaSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _emaWindow = new PooledRingBuffer<double>(_length2);
        _pct = pct;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MovingAverageDisplacedEnvelope;

    public void Reset()
    {
        _emaSmoother.Reset();
        _emaWindow.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _emaSmoother.Next(value, isFinal);
        var prevEma = _emaWindow.Count >= _length2 ? _emaWindow[0] : 0;
        var upper = RoundedPercentageBand.Percent(prevEma, _pct, 1);
        var lower = RoundedPercentageBand.Percent(prevEma, _pct, -1);
        var middle = prevEma;

        if (isFinal)
        {
            _emaWindow.TryAdd(ema, out _);
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
        _emaSmoother.Dispose();
        _emaWindow.Dispose();
    }
}

[PrimaryOutput("Vhf")]
public sealed class VerticalHorizontalFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly VerticalHorizontalWindow _window;
    private readonly RocBankAverage? _signal;
    private readonly IMovingAverageSmoother? _fallback;
    public VerticalHorizontalFilterState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 18, int signalLength = 6)
    {
        _window = new(length);
        if (StrengthWindow.Supports(maType)) _signal = new(maType, Math.Max(1, signalLength), int.MaxValue);
        else _fallback = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }
    public IndicatorName Name => IndicatorName.VerticalHorizontalFilter;
    public void Reset() { _window.Reset(); _signal?.Reset(); _fallback?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        var signal = _signal is null ? _fallback!.Next(value, isFinal) : _signal.Next(new RocBankValue(value), isFinal).Publish();
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Vhf", value }, { "Signal", signal } } : null);
    }
    public void Dispose() { _window.Dispose(); _signal?.Dispose(); _fallback?.Dispose(); }
}

[PrimaryOutput("Ss")]
public sealed class SigmaSpikesState : IStreamingIndicatorState, IDisposable    
{
    private readonly SigmaSpikesWindow _window;
    private readonly StreamingInputResolver _input;
    public SigmaSpikesState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    { _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.SigmaSpikes;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var point = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ss", point.Line }, { "Signal", point.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Sv")]
public sealed class StatisticalVolatilityState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;
    private readonly StatisticalVolatilityWindow _window;
    public StatisticalVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 30, int length2 = 253)
        => _window = new(maType, length1, length2);
    public IndicatorName Name => IndicatorName.StatisticalVolatility;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Sv", point.Line }, { "Signal", point.Signal } } : null;
        return new StreamingIndicatorStateResult(point.Line, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vsi")]
public sealed class VolatilitySwitchIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VolatilitySwitchWindow _window;
    private readonly StreamingInputResolver _input;
    public VolatilitySwitchIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14)
    { _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.VolatilitySwitchIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Value(price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Vsi", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class ExtendedRecursiveBandsState : IStreamingIndicatorState
{
    private readonly ExtendedBandWindow _window;
    public ExtendedRecursiveBandsState(int length = 100) { _window = new(length); }
    public IndicatorName Name => IndicatorName.ExtendedRecursiveBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class StollerAverageRangeChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly KeltnerWindow _window;
    private readonly double _multiplier;
    public StollerAverageRangeChannelsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double atrMult = 2)
    { _window = new(maType, length, length, maType); _multiplier = atrMult; }
    public IndicatorName Name => IndicatorName.StollerAverageRangeChannels;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var stages = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        var point = RangeChannelWindow.Output(bar.Close, stages.Middle, stages.Atr, _multiplier, false);
        return new(point.Average, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower }  } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Dema1")]
public sealed class Dema2LinesState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _fastDema;
    private readonly IMovingAverageSmoother _slowDema;
    private readonly StreamingInputResolver _input;

    public Dema2LinesState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10,
        int slowLength = 40)
    {
        var fast = Math.Max(1, fastLength);
        var slow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, fast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, slow);
        _fastDema = MovingAverageSmootherFactory.Create(maType, fast);
        _slowDema = MovingAverageSmootherFactory.Create(maType, slow);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Dema2Lines;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _fastDema.Reset();
        _slowDema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var dema1 = _fastDema.Next(fast, isFinal);
        var dema2 = _slowDema.Next(slow, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dema1", dema1 },
                { "Dema2", dema2 }
            };
        }

        return new StreamingIndicatorStateResult(dema1, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _fastDema.Dispose();
        _slowDema.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class DynamicSupportAndResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly DynamicSupportWindow _window;
    public DynamicSupportAndResistanceState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 25) { _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.DynamicSupportAndResistance;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "Support", point.Support }, { "Resistance", point.Resistance }, { "MiddleBand", point.Middle } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("UpperBand")]
public sealed class DailyAveragePriceDeltaState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyDeltaWindow _window;
    public DailyAveragePriceDeltaState(MovingAvgType maType=MovingAvgType.SimpleMovingAverage,int length=21)=>_window=new(maType,length);
    public IndicatorName Name=>IndicatorName.DailyAveragePriceDelta;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.High,bar.Low,isFinal);
        return new(value.Upper,includeOutputs?new Dictionary<string,double>{{"UpperBand",value.Upper},{"LowerBand",value.Lower}}:null);
    }
    public void Dispose()=>_window.Dispose();
}
[PrimaryOutput("K")]
public sealed class PeriodicChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly PeriodicChannelWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public PeriodicChannelState(int length1 = 500, int length2 = 2) { _window = new(length1, length2); }
    public IndicatorName Name => IndicatorName.PeriodicChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(_input.GetValue(bar), isFinal);
        return new(point.Values[0], includeOutputs
            ? PeriodicChannelWindow.Keys.Select((key, i) => (key, value: point.Values[i])).ToDictionary(v => v.key, v => v.value) : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class PriceLineChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly PriceDriftWindow _window;
    public PriceLineChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 100) { _window = new(maType, length, false); }
    public IndicatorName Name => IndicatorName.PriceLineChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class PriceCurveChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly PriceDriftWindow _window;
    public PriceCurveChannelState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 100) { _window = new(maType, length, true); }
    public IndicatorName Name => IndicatorName.PriceCurveChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class RangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _stdDevFactor;
    private readonly IMovingAverageSmoother _middleSmoother;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;

    public RangeBandsState(double stdDevFactor = 1, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14)
    {
        _length = Math.Max(1, length);
        _stdDevFactor = stdDevFactor;
        _middleSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(_length) : MovingAverageSmootherFactory.Create(maType, _length);
        var windowLength = _length;
        _maxWindow = new RollingWindowMax(windowLength);
        _minWindow = new RollingWindowMin(windowLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RangeBands;

    public void Reset()
    {
        _middleSmoother.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _middleSmoother.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(middle, out _) : _maxWindow.Preview(middle, out _);
        var lowest = isFinal ? _minWindow.Add(middle, out _) : _minWindow.Preview(middle, out _);
        var upper = RangeBandArithmetic.Band(middle, highest, lowest, _stdDevFactor);
        var lower = RangeBandArithmetic.Band(middle, highest, lowest, -_stdDevFactor);

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
        _middleSmoother.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class RangeIdentifierState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevUp;
    private double _prevDown;
    private bool _hasPrev;

    public RangeIdentifierState(int length = 34)
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RangeIdentifier;

    public void Reset()
    {
        _prevUp = 0;
        _prevDown = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevUp = _hasPrev ? _prevUp : 0;
        var prevDown = _hasPrev ? _prevDown : 0;
        var up = value < prevUp && value > prevDown ? prevUp : bar.High;
        var down = value < prevUp && value > prevDown ? prevDown : bar.Low;
        var middle = PriceMean.Of(up, down);

        if (isFinal)
        {
            _prevUp = up;
            _prevDown = down;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", up },
                { "MiddleBand", middle },
                { "LowerBand", down }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }
}

[PrimaryOutput("LinearRegression")]
public sealed class LinearRegressionState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactLinearFitWindow _regression;
    private readonly StreamingInputResolver _input;

    public LinearRegressionState(int length = 14)
    {
        _regression = new ExactLinearFitWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    internal LinearRegressionState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _regression = new ExactLinearFitWindow(length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.LinearRegression;

    public void Reset()
    {
        _regression.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        // The fit of batch CalculateLinearRegression: the same class, fed the same values.
        var fit = _regression.Next(value, isFinal);
        var slope = fit.Slope;
        // The intercept is reported at the first bar of the stream, as batch reports it at bar 0.
        var intercept = fit.GlobalIntercept;
        var predictedToday = fit.Last;
        var predictedTomorrow = fit.Next;


        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "LinearRegression", predictedToday },
                { "PredictedTomorrow", predictedTomorrow },
                { "Slope", slope },
                { "Intercept", intercept }
            };
        }

        return new StreamingIndicatorStateResult(predictedToday, outputs);
    }

    public void Dispose()
    {
        _regression.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class StandardDeviationChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactRegressionChannelWindow _channel;
    public StandardDeviationChannelState(int length = 40, double stdDevMult = 2)
        => _channel = new ExactRegressionChannelWindow(length, stdDevMult);

    public IndicatorName Name => IndicatorName.StandardDeviationChannel;

    public void Reset()
    {
        _channel.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var bands = _channel.Next(bar.Close, isFinal);
        var upper = bands.Upper; var middle = bands.Middle; var lower = bands.Lower;

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
        _channel.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class TimeSeriesForecastState : IStreamingIndicatorState, IDisposable
{
    private readonly TimeSeriesForecastWindow _window;
    public TimeSeriesForecastState(int length = 500) { _window = new(length); }
    public IndicatorName Name => IndicatorName.TimeSeriesForecast;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", p.Upper }, { "MiddleBand", p.Middle }, { "LowerBand", p.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class SmartEnvelopeState : IStreamingIndicatorState
{
    private readonly SmartEnvelopeWindow _window;
    public SmartEnvelopeState(int length = 14, double factor = 1) { _window = new(length, factor); }
    public IndicatorName Name => IndicatorName.SmartEnvelope;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
}

[PrimaryOutput("Support")]
public sealed class SupportResistanceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevSma;
    private double _prevRes;
    private double _prevSupp;
    private bool _hasPrev;

    public SupportResistanceState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.SupportResistance;

    public void Reset()
    {
        _smoother.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevValue = 0;
        _prevSma = 0;
        _prevRes = 0;
        _prevSupp = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var sma = _smoother.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevSma = _hasPrev ? _prevSma : 0;
        var crossAbove = prevValue < prevSma && value >= prevSma;
        var crossBelow = prevValue > prevSma && value <= prevSma;
        var res = crossBelow ? highest : _hasPrev ? _prevRes : highest;
        var supp = crossAbove ? lowest : _hasPrev ? _prevSupp : lowest;

        if (isFinal)
        {
            _prevValue = value;
            _prevSma = sma;
            _prevRes = res;
            _prevSupp = supp;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Support", supp },
                { "Resistance", res }
            };
        }

        return new StreamingIndicatorStateResult(supp, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("Deviation")]
public sealed class StationaryExtrapolatedLevelsState : IStreamingIndicatorState, IDisposable
{
    private readonly StationaryLevelsWindow _window;
    public StationaryExtrapolatedLevelsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.StationaryExtrapolatedLevels;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Deviation, includeOutputs ? new Dictionary<string, double> { { "UpperBand", value.Upper }, { "MiddleBand", value.Middle }, { "LowerBand", value.Lower }, { "Deviation", value.Deviation } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Scalper")]
public sealed class ScalpersChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly ScalperChannelWindow _window;
    public ScalpersChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 15, int length2 = 20) { _window = new(maType, length1, length2); }
    public IndicatorName Name => IndicatorName.ScalpersChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Scalper, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower }, { "Scalper", point.Scalper } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class SmoothedVolatilityBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly SmoothedVolatilityWindow _window;
    public SmoothedVolatilityBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = .9)
    { _window = new(maType, length1, length2, deviation, bandAdjust); }
    public IndicatorName Name => IndicatorName.SmoothedVolatilityBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class MovingAverageEnvelopeState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;

    public MovingAverageEnvelopeState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double mult = 0.025)
    {
        var resolved = Math.Max(1, length);
        _smoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved)
            : MovingAverageSmootherFactory.Create(maType, resolved);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MovingAverageEnvelope;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _smoother.Next(value, isFinal);
        var upper = RoundedPercentageBand.Of(middle, _mult, 1);
        var lower = RoundedPercentageBand.Of(middle, _mult, -1);

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
    }
}

[PrimaryOutput("UpperBand")]
public sealed class LinearChannelsState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevA2;
    private double _prevUpper;
    private double _prevLower;
    private int _count;

    public LinearChannelsState(int length = 14, double mult = 50)
    {
        _length = Math.Max(1, length);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.LinearChannels;

    public void Reset()
    {
        _prevA = 0;
        _prevA2 = 0;
        _prevUpper = 0;
        _prevLower = 0;
        _count = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var s = (double)1 / _length;
        var prevA = _count >= 1 ? _prevA : value;
        var prevA2 = _count >= 2 ? _prevA2 : value;
        var x = value + ((prevA - prevA2) * _mult);
        var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;

        var up = a + (Math.Abs(a - prevA) * _mult);
        var dn = a - (Math.Abs(a - prevA) * _mult);
        var prevUpper = _count >= 1 ? _prevUpper : 0;
        var prevLower = _count >= 1 ? _prevLower : 0;
        var upper = up == a ? prevUpper : up;
        var lower = dn == a ? prevLower : dn;

        if (isFinal)
        {
            _prevA2 = _prevA;
            _prevA = a;
            _prevUpper = upper;
            _prevLower = lower;
            _count++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpperBand", upper },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(upper, outputs);
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class NarrowSidewaysChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly BollingerBandsState _bands;
    public NarrowSidewaysChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double stdDevMult = 3)
    { HighLowBandsWindow.ValidateShift(stdDevMult); _bands = new(length, stdDevMult, maType); }
    public IndicatorName Name => IndicatorName.NarrowSidewaysChannel;
    public void Reset() => _bands.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { return _bands.Update(bar, isFinal, includeOutputs); }
    public void Dispose() => _bands.Dispose();
}

[PrimaryOutput("Roc")]
public sealed class RateOfChangeBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly RocBandWindow _window;
    public RateOfChangeBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 12, int smoothLength = 3) { _window = new(maType, length, smoothLength); }
    public IndicatorName Name => IndicatorName.RateOfChangeBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Roc, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", 0 }, { "LowerBand", -point.Upper }, { "Roc", point.Roc } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class GChannelsState : IStreamingIndicatorState
{
    private readonly GChannelWindow _window;
    public GChannelsState(int length = 100) { _window = new(length); }
    public IndicatorName Name => IndicatorName.GChannels;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class HighLowMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly HighLowAverageWindow _window;
    public HighLowMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.HighLowMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", value.Upper }, { "MiddleBand", value.Middle }, { "LowerBand", value.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class HighLowBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly HighLowBandsWindow _window;
    private readonly double _pctShift;
    public HighLowBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double pctShift = 1)
    {
        HighLowBandsWindow.ValidateShift(pctShift); _pctShift = pctShift; _window = new(maType, length);
    }
    public IndicatorName Name => IndicatorName.HighLowBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var middle = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(middle, includeOutputs ? new Dictionary<string, double> {
            { "UpperBand", HighLowBandsWindow.Shift(middle, _pctShift) }, { "MiddleBand", middle }, { "LowerBand", HighLowBandsWindow.Shift(middle, -_pctShift) } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class KeltnerChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly KeltnerWindow _window;
    private readonly double _multiplier;
    public KeltnerChannelsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 20, int length2 = 10, double multFactor = 2, MovingAvgType atrMaType = MovingAvgType.WildersSmoothingMethod)
    { _window = new(maType, length1, length2, atrMaType); _multiplier = multFactor; }
    internal bool MiddleOnly { get; set; }
    public IndicatorName Name => IndicatorName.KeltnerChannels;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (MiddleOnly) { var middle = _window.NextMiddle(bar.Close, isFinal).Publish(); return new(middle, includeOutputs ? new Dictionary<string, double> { { "MiddleBand", middle } } : null); }
        var stages = _window.Next(bar.High, bar.Low, bar.Close, isFinal); var point = KeltnerWindow.Bands(stages.Middle, stages.Atr, _multiplier);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class DEnvelopeState : IStreamingIndicatorState
{
    private readonly DEnvelopeWindow _window;
    public DEnvelopeState(int length = 20, double devFactor = 2) { _window = new(length, devFactor); }
    public IndicatorName Name => IndicatorName.DEnvelope;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
}

[PrimaryOutput("MiddleChannel")]
public sealed class PriceChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly double _pct;

    public PriceChannelState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21,
        double pct = 0.06)
    {
        var resolved = Math.Max(1, length);
        _smoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved)
            : MovingAverageSmootherFactory.Create(maType, resolved);
        _pct = pct;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.PriceChannel;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _smoother.Next(value, isFinal);
        var upper = RoundedPercentageBand.Of(ema, _pct, 1);
        var lower = RoundedPercentageBand.Of(ema, _pct, -1);
        var middle = ema;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperChannel", upper },
                { "LowerChannel", lower },
                { "MiddleChannel", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class MovingAverageChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly PriceAverageChannelWindow _window;
    public MovingAverageChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.MovingAverageChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", value.Upper }, { "MiddleBand", value.Middle }, { "LowerBand", value.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}
[IndicatorBounds(0, 100, CanBeNegative = false)]
[IndicatorCategory("Oscillator", SubCategory = "Momentum")]
[HasVariants("Wilder", OtherVariants = "Cutler", Reference = "Wilder 1978")]
[PrimaryOutput("Rsi")]
public sealed class RelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly PriceRsiWindow? _wide;
    private readonly StrengthAverage? _wideSignal;
    private readonly IMovingAverageSmoother _avgGain;
    private readonly IMovingAverageSmoother _avgLoss;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;
    private readonly bool _preserveFlatRatio;
    private double _previousRsi;

    // Wilder's smoothing of the gains and losses, as he defined the index and as the batch indicator defaults to.
    public RelativeStrengthIndexState(int length = 14, int signalLength = 3,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        if (StrengthWindow.Supports(maType))
        {
            _wide = new PriceRsiWindow(maType, length);
            _wideSignal = new StrengthAverage(maType, signalLength);
        }
        var resolved = Math.Max(1, length);
        _preserveFlatRatio = resolved > 1 && (maType == MovingAvgType.WildersSmoothingMethod || maType == MovingAvgType.ExponentialMovingAverage);
        _avgGain = MovingAverageSmootherFactory.Create(maType, resolved);
        _avgLoss = MovingAverageSmootherFactory.Create(maType, resolved);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RelativeStrengthIndex;

    public void Reset()
    {
        _wide?.Reset();
        _wideSignal?.Reset();
        _avgGain.Reset();
        _avgLoss.Reset();
        _signal.Reset();
        _prevClose = 0;
        _hasPrev = false;
        _previousRsi = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_wide is not null)
        {
            var value = _wide.Next(bar.Close, isFinal);
            var mean = _wideSignal!.Next(new StrengthValue(value), isFinal).Mantissa;
            return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double>
                { { "Rsi", value }, { "Signal", mean }, { "Histogram", value - mean } } : null);
        }
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var priceChg = _hasPrev ? currentValue - prevClose : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.Next(gain, isFinal);
        var avgLoss = _avgLoss.Next(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        if (_preserveFlatRatio && _hasPrev && priceChg == 0) rsi = _previousRsi;
        var signal = _signal.Next(rsi, isFinal);
        var histogram = rsi - signal;

        if (isFinal)
        {
            _prevClose = currentValue;
            _previousRsi = rsi;
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

    public void Dispose()
    {
        _wide?.Dispose();
        _wideSignal?.Dispose();
        _avgGain.Dispose();
        _avgLoss.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("FastK")]
public sealed class StochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _fastSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength1))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength2))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, null);
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
        StreamingInputValidation.Validate(bar);
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var fastK = ClampedRangePosition.Percent(close, lowestLow, highestHigh);

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

[PrimaryOutput("Williams%R")]
public sealed class WilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public WilliamsRState(int length = 14)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WilliamsR;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var high = bar.High;
        var low = bar.Low;
        var close = _input.GetValue(bar);

        var highestHigh = isFinal ? _highWindow.Add(high, out _) : _highWindow.Preview(high, out _);
        var lowestLow = isFinal ? _lowWindow.Add(low, out _) : _lowWindow.Preview(low, out _);
        var williamsR = WilliamsRangePosition.Percent(close, lowestLow, highestHigh);

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

[PrimaryOutput("Cci")]
public sealed class CommodityChannelIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly CommodityIndexWindow? _exact;
    private readonly IMovingAverageSmoother? _priceSmoother, _meanDevSmoother;
    private readonly StreamingInputResolver _input;
    private bool _readClose;
    private readonly double _constant;

    public CommodityChannelIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double constant = 0.015)
    {
        CommodityIndexWindow.ValidateConstant(constant);
        if (StrengthWindow.Supports(maType)) _exact = new CommodityIndexWindow(maType, length, constant);
        else
        {
            _priceSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
            _meanDevSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        }
        _input = new StreamingInputResolver(InputName.Close, null);
        _constant = constant;
    }
    public IndicatorName Name => IndicatorName.CommodityChannelIndex;
    void ICustomInputConsumer.ReadCloseAsInput() => _readClose = true;
    public void Reset() { _exact?.Reset(); _priceSmoother?.Reset(); _meanDevSmoother?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var value = _readClose ? close : CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, close);
        double cci;
        if (_exact is not null) cci = _exact.Next(value, isFinal);
        else
        {
            var mean = _priceSmoother!.Next(value, isFinal);
            var deviation = _meanDevSmoother!.Next(Math.Abs(value - mean), isFinal);
            cci = deviation == 0 ? 0 : (value - mean) / (_constant * deviation);
        }
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Cci", cci } } : null;
        return new StreamingIndicatorStateResult(cci, outputs);
    }
    public void Dispose() { _exact?.Dispose(); _priceSmoother?.Dispose(); _meanDevSmoother?.Dispose(); }
}

[PrimaryOutput("StochRsi")]
public sealed class StochasticRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly StrengthAverage? _exactFast, _exactSlow;
    private readonly int _length;
    private readonly RsiState _rsi;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public StochasticRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        int smoothLength1 = 3, int smoothLength2 = 3, int? stochLength = null)
    {
        if (StrengthWindow.Supports(maType))
        {
            _exactFast = new StrengthAverage(maType, smoothLength1);
            _exactSlow = new StrengthAverage(maType, smoothLength2);
        }
        _length = Math.Max(1, length);
        // The stochastic's own lookback over the RSI, as the batch method's stochLength: the RSI's length unless set.
        var stochWindow = Math.Max(1, stochLength ?? length);
        _rsi = new RsiState(maType, _length);
        _maxWindow = new RollingWindowMax(stochWindow);
        _minWindow = new RollingWindowMin(stochWindow);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StochasticRelativeStrengthIndex;

    public void Reset()
    {
        _exactFast?.Reset(); _exactSlow?.Reset();
        _rsi.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(rsi, out _) : _maxWindow.Preview(rsi, out _);
        var lowest = isFinal ? _minWindow.Add(rsi, out _) : _minWindow.Preview(rsi, out _);
        var fastK = ClampedRangePosition.Percent(rsi, lowest, highest);

        var fastD = _exactFast is null ? _fastSmoother.Next(fastK, isFinal) : _exactFast.Next(new StrengthValue(fastK), isFinal).Mantissa;
        var slowD = _exactSlow is null ? _slowSmoother.Next(fastD, isFinal) : _exactSlow.Next(new StrengthValue(fastD), isFinal).Mantissa;

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
        _exactFast?.Dispose(); _exactSlow?.Dispose();
        _rsi.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

[PrimaryOutput("ConnorsRsi")]
public sealed class ConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly RsiState _streakRsi;
    private ReturnOrderStatistic _rocRank;
    private readonly int _rankLength;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _streak;
    private bool _hasPrev;

    public ConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100)
    {
        _rsi = new RsiState(maType, Math.Max(1, length2));
        _streakRsi = new RsiState(maType, Math.Max(1, length1));
        _rankLength = Math.Max(1, length3);
        _rocRank = new ReturnOrderStatistic(_rankLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _streakRsi.Reset();
        _rocRank.Dispose();
        _rocRank = new ReturnOrderStatistic(_rankLength);
        _prevValue = 0;
        _streak = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var rsi = _rsi.Next(currentValue, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;

        // Connors ranks the one-bar rate of change of the price; this used to rank a length3-bar rate of change
        // of the RSI, copying the batch.
        var pctRank = 100d * _rocRank.CountLessThan(currentValue, prevValue) / _rankLength;
        if (isFinal) _rocRank.Add(currentValue, prevValue);

        var prevStreak = _streak;
        var streak = !_hasPrev ? 0 : currentValue > prevValue
            ? prevStreak >= 0 ? prevStreak + 1 : 1
            : currentValue < prevValue
                ? prevStreak <= 0 ? prevStreak - 1 : -1
                : 0;
        var streakRsi = _streakRsi.Next(streak, isFinal);

        var connors = ConnorsValue.Combine(rsi, pctRank, streakRsi);

        if (isFinal)
        {
            _prevValue = currentValue;
            _streak = streak;
            _hasPrev = true;
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
    }
}

[PrimaryOutput("SaRsi")]
public sealed class StochasticConnorsRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StrengthAverage? _exactFast, _exactSlow;
    private readonly ConnorsRelativeStrengthIndexState _connors;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;

    public StochasticConnorsRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100, int smoothLength1 = 3, int smoothLength2 = 3)
    {
        if (StrengthWindow.Supports(maType))
        {
            _exactFast = new StrengthAverage(maType, smoothLength1);
            _exactSlow = new StrengthAverage(maType, smoothLength2);
        }
        _length = Math.Max(1, length2);
        _connors = new ConnorsRelativeStrengthIndexState(maType, length1, length2, length3);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
    }

    public IndicatorName Name => IndicatorName.StochasticConnorsRelativeStrengthIndex;

    public void Reset()
    {
        _exactFast?.Reset(); _exactSlow?.Reset();
        _connors.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var connors = _connors.Update(bar, isFinal, includeOutputs: false).Value;
        var highest = isFinal ? _maxWindow.Add(connors, out _) : _maxWindow.Preview(connors, out _);
        var lowest = isFinal ? _minWindow.Add(connors, out _) : _minWindow.Preview(connors, out _);
        var fastK = ClampedRangePosition.Percent(connors, lowest, highest);
        var fastD = _exactFast is null ? _fastSmoother.Next(fastK, isFinal) : _exactFast.Next(new StrengthValue(fastK), isFinal).Mantissa;
        var slowD = _exactSlow is null ? _slowSmoother.Next(fastD, isFinal) : _exactSlow.Next(new StrengthValue(fastD), isFinal).Mantissa;

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
        _exactFast?.Dispose(); _exactSlow?.Dispose();
        _connors.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

[PrimaryOutput("Smi")]
public sealed class StochasticMomentumIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly StochasticMomentumWindow _window;
    public StochasticMomentumIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2, int length2 = 8, int smoothLength1 = 5, int smoothLength2 = 5)
        => _window = new(maType, length1, length2, smoothLength1, smoothLength2);
    public IndicatorName Name => IndicatorName.StochasticMomentumIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Smi", value.Line }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[IndicatorBounds(double.NegativeInfinity, double.PositiveInfinity)]
[IndicatorCategory("Trend", SubCategory = "Momentum")]
[HasVariants("Standard EMA", OtherVariants = "Wilder EMA", Reference = "Appel")]
[PrimaryOutput("Macd")]
public sealed class MovingAverageConvergenceDivergenceState : IStreamingIndicatorState
{
    private readonly EmaState _fast;
    private readonly EmaState _slow;
    private readonly EmaState _signal;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public MovingAverageConvergenceDivergenceState(int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        _fast = new EmaState(fastLength);
        _slow = new EmaState(slowLength);
        _signal = new EmaState(signalLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MovingAverageConvergenceDivergence;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.GetNext(value, isFinal);
        var slow = _slow.GetNext(value, isFinal);
        var macd = fast - slow;
        var invalidSignal = _signalInvalid || double.IsInfinity(macd);
        var signal = invalidSignal ? double.NaN : _signal.GetNext(macd, isFinal);
        if (isFinal) _signalInvalid = invalidSignal;
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

[PrimaryOutput("Apo")]
public sealed class AbsolutePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly StreamingInputResolver _input;

    public AbsolutePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 10, int slowLength = 20)
    {
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(fastLength)
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(slowLength)
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AbsolutePriceOscillator;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var apo = fast - slow;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Apo", apo }
            };
        }

        return new StreamingIndicatorStateResult(apo, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
    }
}

[PrimaryOutput("Ppo")]
public sealed class PercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public PercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.PercentagePriceOscillator;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var ppo = RoundedPercentageChange.Of(fast, slow);
        var invalidSignal = _signalInvalid || double.IsInfinity(ppo);
        var signal = invalidSignal ? double.NaN : _signal.Next(ppo, isFinal);
        if (isFinal) _signalInvalid = invalidSignal;
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
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Pvo")]
public sealed class PercentageVolumeOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private StreamingInputResolver _input;
    private bool _signalInvalid;

    public PercentageVolumeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        _fast = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Volume, null);
    }

    public IndicatorName Name => IndicatorName.PercentageVolumeOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _signal.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fast.Next(value, isFinal);
        var slow = _slow.Next(value, isFinal);
        var pvo = RoundedPercentageChange.Of(fast, slow);
        var invalidSignal = _signalInvalid || double.IsInfinity(pvo);
        var signal = invalidSignal ? double.NaN : _signal.Next(pvo, isFinal);
        if (isFinal) _signalInvalid = invalidSignal;
        var histogram = pvo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Pvo", pvo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(pvo, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("MiddleChannel")]
public sealed class DonchianChannelsState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public DonchianChannelsState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DonchianChannels;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var upper = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lower = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var middle = PriceMean.Of(upper, lower);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperChannel", upper },
                { "LowerChannel", lower },
                { "MiddleChannel", middle }
            };
        }

        return new StreamingIndicatorStateResult(middle, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("Cfdv")]
public sealed class ClosedFormDistanceVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly ClosedFormDistanceWindow _window;
    public ClosedFormDistanceVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.ClosedFormDistanceVolatility;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Cfdv", point.Value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Dcw")]
public sealed class DonchianChannelWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly DonchianWidthWindow _window;
    public DonchianChannelWidthState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, int smoothLength = 22) { _window = new(maType, length, smoothLength); }
    public IndicatorName Name => IndicatorName.DonchianChannelWidth;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Width, includeOutputs ? new Dictionary<string, double> { { "Dcw", point.Width }, { "Signal", point.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Hv")]
public sealed class HistoricalVolatilityState : IStreamingIndicatorState, IDisposable
{
    // The deviation of the log-return window about its own mean, matching the batch calculation; see #190.
    private readonly RollingStandardDeviation _stdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _annualSqrt;
    private double _prevValue;
    private double _logReturn;
    private bool _hasPrev;

    public HistoricalVolatilityState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        _stdDev = new RollingStandardDeviation(length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _annualSqrt = MathHelper.Sqrt(365);
    }

    public IndicatorName Name => IndicatorName.HistoricalVolatility;

    public void Reset()
    {
        _stdDev.Reset();
        _prevValue = 0;
        _logReturn = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        _logReturn = StableLogRatio.OfSameSign(value, prevValue);

        // The first bar's log return is a fabricated zero, so it is kept out of the window entirely rather
        // than counted as an observation. The window then fills one bar later, at index length, which is
        // where the batch publishes its first value too - the two stay aligned because neither counts it.
        var stdDevLog = _hasPrev ? _stdDev.Next(_logReturn, isFinal) : 0;
        var hv = 100 * stdDevLog * _annualSqrt;

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
                { "Hv", hv }
            };
        }

        return new StreamingIndicatorStateResult(hv, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

[PrimaryOutput("Gcv")]
public sealed class GarmanKlassVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _terms;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private readonly double _logCoeff;

    public GarmanKlassVolatilityState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 14,
        int signalLength = 7)
    {
        _length = Math.Max(1, length);
        _terms = new PooledRingBuffer<double>(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
        _logCoeff = (2 * Math.Log(2)) - 1;
    }

    public IndicatorName Name => IndicatorName.GarmanKlassVolatility;

    public void Reset()
    {
        _terms.Clear();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentClose = _input.GetValue(bar);
        var logHl = bar.Low != 0 ? StableLogRatio.OfSameSign(bar.High, bar.Low) : 0;
        var logCo = bar.Open != 0 ? StableLogRatio.OfSameSign(currentClose, bar.Open) : 0;
        var log = (0.5 * MathHelper.Pow(logHl, 2)) - (_logCoeff * MathHelper.Pow(logCo, 2));

        var nextCount = Math.Min(_length, _terms.Count + 1);
        if (isFinal) _terms.TryAdd(log, out _);
        double logSum = 0;
        if (nextCount == _length)
        {
            var start = !isFinal && _terms.Count == _length ? 1 : 0;
            for (var j = start; j < _terms.Count; j++) logSum += _terms[j];
            if (!isFinal) logSum += log;
        }
        var gcv = MathHelper.Sqrt(logSum / _length) * Math.Sqrt(252);
        var signal = _signal.Next(gcv, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Gcv", gcv },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(gcv, outputs);
    }

    public void Dispose()
    {
        _terms.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Gapo")]
public sealed class GopalakrishnanRangeIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly GopalakrishnanWindow _window;
    private readonly RocBankAverage? _exact;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public GopalakrishnanRangeIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 5)
    { length = Math.Max(2, length); _window = new(length); if (StrengthWindow.Supports(maType)) _exact = new(maType, length, int.MaxValue); else _fallback = MovingAverageSmootherFactory.Create(maType, length); }
    public IndicatorName Name => IndicatorName.GopalakrishnanRangeIndex;
    public void Reset() { _window.Reset(); _exact?.Reset(); _fallback?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, isFinal);
        var signal = _exact is null ? _fallback!.Next(value, isFinal) : _exact.Next(new RocBankValue(value), isFinal).Publish();
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Gapo", value }, { "Signal", signal } } : null);
    }
    public void Dispose() { _exact?.Dispose(); _fallback?.Dispose(); }
}

[PrimaryOutput("Hvp")]
public sealed class HistoricalVolatilityPercentileState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _annualLength;
    private readonly double _annualSqrt;
    private readonly RollingWindowSum _tempLogSum;
    private readonly RollingWindowSum _devLogSqSum;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private RollingOrderStatistic _hvOrder;
    private double _prevValue;
    private bool _hasPrev;

    public HistoricalVolatilityPercentileState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21, int annualLength = 252)
    {
        _length = Math.Max(1, length);
        _annualLength = Math.Max(1, annualLength);
        _annualSqrt = MathHelper.Sqrt(_annualLength);
        _tempLogSum = new RollingWindowSum(_length);
        _devLogSqSum = new RollingWindowSum(_length);
        _signal = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _hvOrder = new RollingOrderStatistic(_annualLength);
    }

    public IndicatorName Name => IndicatorName.HistoricalVolatilityPercentile;

    public void Reset()
    {
        _tempLogSum.Reset();
        _devLogSqSum.Reset();
        _signal.Reset();
        _hvOrder.Dispose();
        _hvOrder = new RollingOrderStatistic(_annualLength);
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tempLog = value != 0 && prevValue != 0 && Math.Sign(value) == Math.Sign(prevValue)
            ? StableLogRatio.Of(Math.Abs(value), Math.Abs(prevValue)) : 0;

        int tempCount;
        var tempSum = isFinal ? _tempLogSum.Add(tempLog, out tempCount) : _tempLogSum.Preview(tempLog, out tempCount);
        var avgLog = tempCount > 0 ? tempSum / tempCount : 0;

        var devLogSq = MathHelper.Pow(tempLog - avgLog, 2);
        var devSum = isFinal ? _devLogSqSum.Add(devLogSq, out _) : _devLogSqSum.Preview(devLogSq, out _);
        var devLogSqAvg = _length > 1 ? devSum / (_length - 1) : 0;
        var stdDevLog = devLogSqAvg >= 0 ? MathHelper.Sqrt(devLogSqAvg) : 0;
        var hv = stdDevLog * _annualSqrt;

        if (isFinal)
        {
            _hvOrder.Add(hv);
        }

        var boundary = VolatilityRank.StrictBoundary(hv);
        var count = (double)(isFinal ? _hvOrder.CountLessThan(boundary) : _hvOrder.PreviewCountLessThan(boundary, hv));
        var hvp = count / _annualLength * 100;
        var signal = _signal.Next(hvp, isFinal);

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
                { "Hvp", hvp },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(hvp, outputs);
    }

    public void Dispose()
    {
        _tempLogSum.Dispose();
        _devLogSqSum.Dispose();
        _signal.Dispose();
        _hvOrder.Dispose();
    }
}

[PrimaryOutput("Fzs")]
public sealed class FastZScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly StandardizedScoreWindow _score;
    private readonly StrengthAverage? _exact;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly StreamingInputResolver _input;
    private readonly bool _simple;
    public FastZScoreState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        _score = new StandardizedScoreWindow(length, true);
        if (StrengthWindow.Supports(maType)) _exact = new StrengthAverage(maType, length);
        else _fallback = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _simple = maType == MovingAvgType.SimpleMovingAverage;
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.FastZScore;
    public void Reset() { _score.Reset(); _exact?.Reset(); _fallback?.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mean = _exact is null ? _fallback!.Next(value, isFinal) : _exact.Next(new StrengthValue(value), isFinal).Mantissa;
        var score = _score.Next(value, mean, _simple, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Fzs", score } } : null;
        return new StreamingIndicatorStateResult(score, outputs);
    }
    public void Dispose() { _score.Dispose(); _exact?.Dispose(); _fallback?.Dispose(); }
}

[PrimaryOutput("Ci")]
public sealed class ChoppinessIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ChoppinessWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public ChoppinessIndexState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.ChoppinessIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ci", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Cmo")]
public sealed class ChandeMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumWindow _window;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);

    public ChandeMomentumOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, int signalLength = 3)
    {
        _window = new ChandeMomentumWindow(length);
        _signal = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillator;
    public void Reset() { _window.Reset(); _signal.Reset(); }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var cmo = _window.Next(_input.GetValue(bar), isFinal);
        var signal = _signal.Next(cmo, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(2) { { "Cmo", cmo }, { "Signal", signal } } : null;
        return new StreamingIndicatorStateResult(cmo, outputs);
    }

    public void Dispose() { _window.Dispose(); _signal.Dispose(); }
}

[IndicatorBounds(0, double.PositiveInfinity, CanBeNegative = false)]
[IndicatorCategory("Volatility")]
[HasVariants("Wilder", OtherVariants = "SMA, EMA", Reference = "Wilder 1978")]
[PrimaryOutput("Atr")]
public sealed class AverageTrueRangeState : IStreamingIndicatorState, IDisposable
{
    private readonly KeltnerWindow? _exact;
    private readonly IMovingAverageSmoother _atr;
    private double _prevClose;
    private bool _hasPrev;

    // Wilder's smoothing, as he defined the average true range, and as the batch indicator defaults to; a caller
    // who asks the batch method for another average gets it here too.
    public AverageTrueRangeState(int length = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        if (StrengthWindow.Supports(maType)) _exact = new(maType, 1, length, maType);
        _atr = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.AverageTrueRange;

    public void Reset()
    {
        _exact?.Reset();
        _atr.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var value = _exact.Next(bar.High, bar.Low, bar.Close, isFinal).Atr.Publish();
            return new(value, includeOutputs ? new Dictionary<string, double> { { "Atr", value } } : null);
        }
        // For first bar, use current close (TR = High - Low)
        var prevClose = _hasPrev ? _prevClose : bar.Close;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atr.Next(tr, isFinal);

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

    public void Dispose()
    {
        _exact?.Dispose();
        _atr.Dispose();
    }
}

[PrimaryOutput("Adx")]
public sealed class AverageDirectionalIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly DirectionalIndexWindow? _exact;
    private readonly IMovingAverageSmoother _dmPlus;
    private readonly IMovingAverageSmoother _dmMinus;
    private readonly IMovingAverageSmoother _tr;
    private readonly IMovingAverageSmoother _adx;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    // Wilder's smoothing throughout, as he defined the directional index and as the batch indicator defaults to.
    public AverageDirectionalIndexState(int length = 14, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod)
    {
        var resolved = Math.Max(1, length);
        if (StrengthWindow.Supports(maType)) _exact = new(maType, resolved);
        _dmPlus = MovingAverageSmootherFactory.Create(maType, resolved);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, resolved);
        _tr = MovingAverageSmootherFactory.Create(maType, resolved);
        _adx = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.AverageDirectionalIndex;

    public void Reset()
    {
        _exact?.Reset();
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
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var point = _exact.Next(bar.High, bar.Low, bar.Close, isFinal);
            return new(point.Adx, includeOutputs ? new Dictionary<string, double> { { "DiPlus", point.Plus }, { "DiMinus", point.Minus }, { "Adx", point.Adx } } : null);
        }
        var prevHigh = _hasPrev ? _prevHigh : bar.High;
        var prevLow = _hasPrev ? _prevLow : bar.Low;
        // For TrueRange on first bar, use current close
        var prevCloseForTr = _hasPrev ? _prevClose : bar.Close;

        var highDiff = bar.High - prevHigh;
        var lowDiff = prevLow - bar.Low;

        var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevCloseForTr);

        var dmPlus14 = _dmPlus.Next(dmPlus, isFinal);
        var dmMinus14 = _dmMinus.Next(dmMinus, isFinal);
        var tr14 = _tr.Next(tr, isFinal);

        var diPlus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmPlus14 / tr14, 100, 0) : 0;
        var diMinus = tr14 != 0 ? MathHelper.MinOrMax(100 * dmMinus14 / tr14, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var dx = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.Next(dx, isFinal);

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

    public void Dispose()
    {
        _exact?.Dispose();
        _dmPlus.Dispose();
        _dmMinus.Dispose();
        _tr.Dispose();
        _adx.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class BollingerBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _middle;
    private readonly ExactPopulationWindow _stdDev;
    private readonly StreamingInputResolver _input;

    public BollingerBandsState(int length = 20, double stdDevMult = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _middle = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new ExactPopulationWindow(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.BollingerBands;

    public void Reset()
    {
        _middle.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var middle = _middle.Next(value, isFinal);
        var stdDev = _stdDev.Next(value, isFinal);
        var upper = BollingerArithmetic.Band(middle, stdDev, _stdDevMult);
        var lower = BollingerArithmetic.Band(middle, stdDev, -_stdDevMult);

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
        _middle.Dispose();
        _stdDev.Dispose();
    }
}

[PrimaryOutput("StdDev")]
public sealed class StandardDeviationVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly ResidualVolatilityWindow _window;
    private readonly StreamingInputResolver _input;
    internal StandardDeviationVolatilityState(MovingAvgType maType, int length, InputName inputName)
    { _window = new(maType, length); _input = new StreamingInputResolver(inputName, null); }
    public StandardDeviationVolatilityState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
        : this(maType, length, InputName.Close) { }
    internal StandardDeviationVolatilityState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        _window = new(maType, length); _input = new StreamingInputResolver(InputName.Close, selector);
    }
    public IndicatorName Name => IndicatorName.StandardDeviationVolatility;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var result = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(result.Deviation, includeOutputs ? new Dictionary<string, double> { { "StdDev", result.Deviation }, { "Variance", result.Variance }, { "Signal", result.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Std")]
public sealed class StandardDeviationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StreamingInputResolver _input;

    private readonly RollingStandardDeviation _population;

    public StandardDeviationState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _length = Math.Max(1, length);
        _signalMa = MovingAverageSmootherFactory.Create(maType, _length);
        _population = new RollingStandardDeviation(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.StandardDeviation;

    public void Reset()
    {
        _signalMa.Reset();
        _population.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var std = _population.Next(value, isFinal);

        var signal = _signalMa.Next(std, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Std", std },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(std, outputs);
    }

    public void Dispose()
    {
        _signalMa.Dispose();
        _population.Dispose();
    }
}


[PrimaryOutput("Uvi")]
public sealed class UltimateVolatilityIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly UltimateVolatilityWindow _window;
    public UltimateVolatilityIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
        => _window = new(length);
    public IndicatorName Name => IndicatorName.UltimateVolatilityIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Open, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Uvi", value } } : null);
    }
    public void Dispose() => _window.Reset();
}

[PrimaryOutput("Vbm")]
public sealed class VolatilityBasedMomentumState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VolatilityMomentumWindow? _exact;
    private bool _selected;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    private readonly int _length1;
    private readonly IMovingAverageSmoother _atrSmoother = null!;
    private readonly IMovingAverageSmoother _signalSmoother = null!;
    private readonly PooledRingBuffer<double> _window = null!;
    private readonly StreamingInputResolver _input = default;
    private double _prevValue;
    private bool _hasPrev;

    public VolatilityBasedMomentumState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 22,
        int length2 = 65)
    {
        _length1 = Math.Max(1, length1);
        if (StrengthWindow.Supports(maType)) { _exact = new(maType, _length1, length2); return; }
        _atrSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _window = new PooledRingBuffer<double>(_length1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolatilityBasedMomentum;

    public void Reset()
    {
        if (_exact is not null) { _exact.Reset(); return; }
        _atrSmoother.Reset();
        _signalSmoother.Reset();
        _window.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var point = _exact.Next(bar.High, bar.Low, bar.Close, isFinal, _selected);
            return new(point.Line, includeOutputs ? new Dictionary<string, double> { ["Vbm"] = point.Line, ["Signal"] = point.Signal } : null);
        }
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : value;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _atrSmoother.Next(tr, isFinal);
        var prevLengthValue = _window.Count >= _length1 ? _window[0] : 0;
        var rateOfChange = _window.Count >= _length1 ? value - prevLengthValue : 0;
        var vbm = atr != 0 ? rateOfChange / atr : 0;
        var signal = _signalSmoother.Next(vbm, isFinal);

        if (isFinal)
        {
            _window.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vbm", vbm },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(vbm, outputs);
    }

    public void Dispose()
    {
        if (_exact is not null) { _exact.Dispose(); return; }
        _atrSmoother.Dispose();
        _signalSmoother.Dispose();
        _window.Dispose();
    }
}

[PrimaryOutput("Vqi")]
public sealed class VolatilityQualityIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VolatilityQualityWindow _window;
    private bool _selected;
    public VolatilityQualityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 9, int slowLength = 200)
        => _window = new(maType, fastLength, slowLength);
    public IndicatorName Name => IndicatorName.VolatilityQualityIndex;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, isFinal, _selected);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double>
            { ["Vqi"] = point.Line, ["FastSignal"] = point.Fast, ["SlowSignal"] = point.Slow } : null);
    }
    public void Dispose() => _window.Dispose();
}
[PrimaryOutput("Obv")]
public sealed class OnBalanceVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private ExactMeanAccumulator _obv;
    private bool _hasPrev;

    public OnBalanceVolumeState(int length = 20) : this(length, MovingAvgType.ExponentialMovingAverage) { }

    public OnBalanceVolumeState(int length, MovingAvgType maType)
    {
        _signal = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolume;

    public void Reset()
    {
        _signal.Reset();
        _prevClose = 0;
        _obv = default;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var total = _obv;
        if (currentValue > prevClose) total.Add(bar.Volume);
        else if (currentValue < prevClose) total.Add(bar.Volume, -1);
        var obv = total.Mean(1);

        var signal = double.IsInfinity(obv) ? double.NaN : _signal.Next(obv, isFinal);

        if (isFinal)
        {
            _prevClose = currentValue;
            _obv = total;
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

    public void Dispose() => _signal.Dispose();
}

[PrimaryOutput("Cmf")]
public sealed class ChaikinMoneyFlowState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ChaikinFlowWindow _window;
    public ChaikinMoneyFlowState(int length=20)=>_window=new(length);
    public IndicatorName Name=>IndicatorName.ChaikinMoneyFlow;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value=_window.Next(bar.High,bar.Low,bar.Close,bar.Volume,isFinal);
        return new(value,includeOutputs?new Dictionary<string,double>{{"Cmf",value}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Mfi")]
public sealed class MoneyFlowIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly RollingMoneyFlowIndex _flow;
    private bool _selectedClose;

    public MoneyFlowIndexState(int length = 14)
    {
        _flow = new RollingMoneyFlowIndex(length);
    }

    public IndicatorName Name => IndicatorName.MoneyFlowIndex;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _selectedClose = true;

    public void Reset() => _flow.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _flow.Next(_selectedClose ? bar.Close : RollingMoneyFlowIndex.TypicalPrice(bar.High, bar.Low, bar.Close), bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Mfi", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _flow.Dispose();
}

[PrimaryOutput("Adl")]
public sealed class AccumulationDistributionLineState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly MoneyFlowAverageWindow _window;
    public AccumulationDistributionLineState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
        => _window = new MoneyFlowAverageWindow(maType, length);
    public IndicatorName Name => IndicatorName.AccumulationDistributionLine;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Adl", value.Line }, { "AdlSignal", value.Signal } } : null;
        return new StreamingIndicatorStateResult(value.Line, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("ChaikinOsc")]
public sealed class ChaikinOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly MoneyFlowAverageWindow _window;
    public ChaikinOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 3, int slowLength = 10)
        => _window = new MoneyFlowAverageWindow(maType, fastLength, slowLength);
    public IndicatorName Name => IndicatorName.ChaikinOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "ChaikinOsc", value.Signal } } : null;
        return new StreamingIndicatorStateResult(value.Signal, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tsi")]
public sealed class TrueStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly StrengthWindow? _strength;
    private readonly StrengthAverage? _stableSignal;
    private readonly IMovingAverageSmoother _pcSmoother1;
    private readonly IMovingAverageSmoother _pcSmoother2;
    private readonly IMovingAverageSmoother _absSmoother1;
    private readonly IMovingAverageSmoother _absSmoother2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public TrueStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 25,
        int length2 = 13, int signalLength = 7)
    {
        if (StrengthWindow.Supports(maType)) _strength = new StrengthWindow(maType, new[] { length1, length2 });
        _pcSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _pcSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        if (StrengthWindow.Supports(maType)) _stableSignal = new StrengthAverage(maType, signalLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrueStrengthIndex;

    public void Reset()
    {
        _strength?.Reset();
        _stableSignal?.Reset();
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
        double tsi;
        if (_strength is not null) tsi = _strength.Next(value, isFinal);
        else
        {
            var prevValue = _hasPrev ? _prevValue : 0;
            var pc = _hasPrev ? value - prevValue : 0;
            var absPc = Math.Abs(pc);

            var pcSmooth1 = _pcSmoother1.Next(pc, isFinal);
            var pcSmooth2 = _pcSmoother2.Next(pcSmooth1, isFinal);
            var absSmooth1 = _absSmoother1.Next(absPc, isFinal);
            var absSmooth2 = _absSmoother2.Next(absSmooth1, isFinal);
            tsi = absSmooth2 != 0 ? MathHelper.MinOrMax(100 * pcSmooth2 / absSmooth2, 100, -100) : 0;
        }
        var signal = _stableSignal is null ? _signalSmoother.Next(tsi, isFinal)
            : _stableSignal.Next(new StrengthValue(tsi), isFinal).Mantissa;

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
        _strength?.Dispose();
        _stableSignal?.Dispose();
        _pcSmoother1.Dispose();
        _pcSmoother2.Dispose();
        _absSmoother1.Dispose();
        _absSmoother2.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("BullPower")]
public sealed class ElderRayIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly StreamingInputResolver _input;

    public ElderRayIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 13)
    {
        _ema = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(length)
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ElderRayIndex;

    public void Reset()
    {
        _ema.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var bullPower = bar.High - ema;
        var bearPower = bar.Low - ema;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "BullPower", bullPower },
                { "BearPower", bearPower }
            };
        }

        return new StreamingIndicatorStateResult(bullPower, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
    }
}

[PrimaryOutput("Asi")]
public sealed class AbsoluteStrengthIndexState : IStreamingIndicatorState
{
    private readonly AbsoluteStrengthWindow _window;
    public AbsoluteStrengthIndexState(int length = 10, int maLength = 21, int signalLength = 34) => _window = new(length, maLength, signalLength);
    public IndicatorName Name => IndicatorName.AbsoluteStrengthIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Asi", value } } : null);
    }
}

[PrimaryOutput("Asi")]
public sealed class AccumulativeSwingIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly AccumulatedSwingWindow _window;
    public AccumulativeSwingIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double limitMove = 0) => _window = new(maType, length, limitMove);
    public IndicatorName Name => IndicatorName.AccumulativeSwingIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        return new(value.Value, includeOutputs ? new Dictionary<string, double> { { "Asi", value.Value }, { "Signal", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Bop")]
public sealed class BalanceOfPowerState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;

    public BalanceOfPowerState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signal = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.BalanceOfPower;

    public void Reset()
    {
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var bop = RoundedBalanceOfPower.Of(bar.Open, bar.High, bar.Low, bar.Close);
        var bopSignal = double.IsInfinity(bop) ? double.NaN : _signal.Next(bop, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Bop", bop },
                { "BopSignal", bopSignal }
            };
        }

        return new StreamingIndicatorStateResult(bop, outputs);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

[PrimaryOutput("Belkhayate")]
public sealed class BelkhayateTimingState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly BelkhayateWindow _window = new();
    public IndicatorName Name => IndicatorName.BelkhayateTiming;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Belkhayate", value } } : null);
    }
}

[PrimaryOutput("Cmvc")]
public sealed class ChartmillValueIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ChartmillWindow _window;
    private StreamingInputResolver _input = new(InputName.MedianPrice, null);
    public ChartmillValueIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.ChartmillValueIndicator;
    void ICustomInputConsumer.ReadCloseAsInput() => _input = new StreamingInputResolver(InputName.Close, null);
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var input = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, bar.Open, bar.Close, input, isFinal);
        return new(value.Close, includeOutputs ? new Dictionary<string, double> { { "Cmvc", value.Close }, { "Cmvo", value.Open }, { "Cmvh", value.High }, { "Cmvl", value.Low } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ca")]
public sealed class ConditionalAccumulatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signal;
    private readonly double _increment;
    private double _value;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public ConditionalAccumulatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double increment = 1)
    {
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _increment = increment;
    }

    public IndicatorName Name => IndicatorName.ConditionalAccumulator;

    public void Reset()
    {
        _signal.Reset();
        _value = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        // Strict, as on the batch side: a gap means this bar's low is above the previous high, not
        // level with it. And the first bar cannot gap at all - there is no previous bar to gap from,
        // and comparing against the 0 sentinel made bar.Low > prevHigh true for any positively
        // priced instrument, counting a gap up on bar 0 and carrying that increment forward.
        var value = _value + GapStep(bar, prevHigh, prevLow);
        var signal = _signal.Next(value, isFinal);

        if (isFinal)
        {
            _value = value;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ca", value },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(value, outputs);
    }

    /// <summary>
    /// How much this bar moves the accumulator: one increment up on a gap up, one down on a gap
    /// down, nothing otherwise.
    /// </summary>
    private double GapStep(OhlcvBar bar, double prevHigh, double prevLow)
    {
        if (!_hasPrev)
        {
            return 0;
        }

        if (bar.Low > prevHigh)
        {
            return _increment;
        }

        if (bar.High < prevLow)
        {
            return -_increment;
        }

        return 0;
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

[PrimaryOutput("Dpo")]
public sealed class DetrendedPriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _window;
    private readonly int _prevPeriods;

    public DetrendedPriceOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _prevPeriods = MathHelper.MinOrMax((int)Math.Ceiling(((double)resolved / 2) + 1));
        _smoother = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(resolved)
            : MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
        _window = new PooledRingBuffer<double>(_prevPeriods);
    }

    public IndicatorName Name => IndicatorName.DetrendedPriceOscillator;

    public void Reset()
    {
        _smoother.Reset();
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var prevValue = _window.Count >= _prevPeriods ? _window[0] : 0;
        var dpo = prevValue - sma;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dpo", dpo }
            };
        }

        return new StreamingIndicatorStateResult(dpo, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _window.Dispose();
    }
}

[PrimaryOutput("Eco")]
public sealed class AdaptiveErgodicCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveCandleWindow _window;
    public AdaptiveErgodicCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int smoothLength = 5, int stochLength = 14, int signalLength = 9)
        => _window = new(maType, smoothLength, stochLength, signalLength);
    public IndicatorName Name => IndicatorName.AdaptiveErgodicCandlestickOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, isFinal); var value = point.Eco.Publish();
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Eco", value }, { "Signal", point.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Bulls")]
public sealed class AbsoluteStrengthMTFIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AbsoluteStrengthMtfWindow _window;
    public AbsoluteStrengthMTFIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50, int smoothLength = 25) => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.AbsoluteStrengthMTFIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value.Bulls, includeOutputs ? new Dictionary<string, double> { { "Bulls", value.Bulls }, { "Bears", value.Bears } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Aroon")]
public sealed class AroonOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public AroonOscillatorState(int length = 25)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AroonOscillator;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var count = _window.Count;
        var willEvict = count == _window.Capacity;
        var startIndex = willEvict ? 1 : 0;
        var max = double.MinValue;
        var min = double.MaxValue;
        var maxIndex = 0;
        var minIndex = 0;
        var virtualIndex = 0;

        for (var i = startIndex; i < count; i++)
        {
            var windowValue = _window[i];
            if (windowValue >= max)
            {
                max = windowValue;
                maxIndex = virtualIndex;
            }

            if (windowValue <= min)
            {
                min = windowValue;
                minIndex = virtualIndex;
            }

            virtualIndex++;
        }

        if (value >= max)
        {
            max = value;
            maxIndex = virtualIndex;
        }

        if (value <= min)
        {
            min = value;
            minIndex = virtualIndex;
        }

        var countAfter = virtualIndex + 1;
        var daysSinceMax = countAfter - 1 - maxIndex;
        var daysSinceMin = countAfter - 1 - minIndex;
        var aroonUp = (double)(_length - daysSinceMax) / _length * 100;
        var aroonDown = (double)(_length - daysSinceMin) / _length * 100;
        var aroon = aroonUp - aroonDown;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Aroon", aroon },
                { "AroonUp", aroonUp },
                { "AroonDown", aroonDown }
            };
        }

        return new StreamingIndicatorStateResult(aroon, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

[PrimaryOutput("BearPower")]
public sealed class BearPowerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly CandlePowerWindow _window;
    public BearPowerIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) { _window = new(false, maType, length); }
    public IndicatorName Name => IndicatorName.BearPowerIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "BearPower", p.Value }, { "Signal", p.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("BullPower")]
public sealed class BullPowerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly CandlePowerWindow _window;
    public BullPowerIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14) { _window = new(true, maType, length); }
    public IndicatorName Name => IndicatorName.BullPowerIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, isFinal);
        return new(p.Value, includeOutputs ? new Dictionary<string, double> { { "BullPower", p.Value }, { "Signal", p.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ch")]
public sealed class ContractHighLowState : IStreamingIndicatorState
{
    private double _conHi;
    private double _conLow;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.ContractHighLow;

    public void Reset()
    {
        _conHi = 0;
        _conLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var conHi = _hasPrev ? Math.Max(_conHi, bar.High) : bar.High;
        var conLow = _hasPrev ? Math.Min(_conLow, bar.Low) : bar.Low;

        if (isFinal)
        {
            _conHi = conHi;
            _conLow = conLow;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ch", conHi },
                { "Cl", conLow }
            };
        }

        return new StreamingIndicatorStateResult(conHi, outputs);
    }
}

[PrimaryOutput("Cz")]
public sealed class ChopZoneState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ChopZoneWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    private bool _selected;
    public ChopZoneState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 30, int length2 = 34) => _window = new(maType, length1, length2);
    public IndicatorName Name => IndicatorName.ChopZone;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, close, _selected, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Cz", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Col")]
public sealed class CenterOfLinearityState : IStreamingIndicatorState, IDisposable
{
    private readonly CenterLinearityWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public CenterOfLinearityState(int length=14)=>_window=new(length);
    public IndicatorName Name=>IndicatorName.CenterOfLinearity;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(_input.GetValue(bar),isFinal);
        return new(value,includeOutputs?new Dictionary<string,double>{{"Col",value}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Cv")]
public sealed class ChaikinVolatilityState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly ChaikinVolatilityWindow _window;
    public ChaikinVolatilityState(MovingAvgType maType=MovingAvgType.ExponentialMovingAverage,int length1=10,int length2=12)=>_window=new(maType,length1,length2);
    public IndicatorName Name=>IndicatorName.ChaikinVolatility;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.High,bar.Low,isFinal);
        return new(value,includeOutputs?new Dictionary<string,double>{{"Cv",value}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Cc")]
public sealed class CoppockCurveState : IStreamingIndicatorState, IDisposable
{
    private readonly RocBankWindow? _wide;
    private readonly RateOfChangeState _rocFast;
    private readonly RateOfChangeState _rocSlow;
    private readonly IMovingAverageSmoother _smoother;

    public CoppockCurveState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10, int fastLength = 11,
        int slowLength = 14)
    {
        if (StrengthWindow.Supports(maType)) _wide = new RocBankWindow(maType, new[] { fastLength, slowLength }, new[] { 1, 1 }, new[] { 1, 1 }, length);
        _rocFast = new RateOfChangeState(Math.Max(1, fastLength));
        _rocSlow = new RateOfChangeState(Math.Max(1, slowLength));
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.CoppockCurve;

    public void Reset()
    {
        _wide?.Reset();
        _rocFast.Reset();
        _rocSlow.Reset();
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        // CalculateCoppockCurve sums two rates of change of the price. This took the slow one of the fast
        // rate of change instead, because the batch indicator used to hand its second component the first
        // one's output through the chained series.
        if (_wide is not null)
        {
            var next = _wide.Next(bar.Close, isFinal).Signal;
            return new StreamingIndicatorStateResult(next, includeOutputs
                ? new Dictionary<string, double> { { "Cc", next } } : null);
        }
        var rocFast = _rocFast.Update(bar, isFinal, includeOutputs: false).Value;
        var rocSlow = _rocSlow.Update(bar, isFinal, includeOutputs: false).Value;
        var rocTotal = rocFast + rocSlow;
        var coppock = _smoother.Next(rocTotal, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cc", coppock }
            };
        }

        return new StreamingIndicatorStateResult(coppock, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _rocFast.Dispose();
        _rocSlow.Dispose();
        _smoother.Dispose();
    }
}

[PrimaryOutput("Csi")]
public sealed class CommoditySelectionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly CommoditySelectionWindow _window;
    public CommoditySelectionIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14, double pointValue = 50, double margin = 3000, double commission = 10) => _window = new(maType, length, pointValue, margin, commission);
    public IndicatorName Name => IndicatorName.CommoditySelectionIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Csi", point.Value }, { "Signal", point.Signal } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Delta")]
public sealed class DeltaMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly OpenCloseAverageWindow _window;
    public DeltaMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10, int length2 = 5) => _window = new(maType, length1, Math.Max(1, length2));
    public IndicatorName Name => IndicatorName.DeltaMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Delta", value.Line }, { "Signal", value.Signal }, { "Histogram", value.Histogram } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Dsp")]
public sealed class DetrendedSyntheticPriceState : IStreamingIndicatorState
{
    private readonly double _alpha;
    private double _ema1;
    private double _ema2;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;
    private bool _hasEma;

    public DetrendedSyntheticPriceState(int length = 14)
    {
        _alpha = length > 2 ? (double)2 / (Math.Max(1, length) + 1d) : 0.67;
    }

    public IndicatorName Name => IndicatorName.DetrendedSyntheticPrice;

    public void Reset()
    {
        _ema1 = 0;
        _ema2 = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
        _hasEma = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var high = Math.Max(bar.High, prevHigh);
        var low = Math.Min(bar.Low, prevLow);
        var price = PriceMean.Of(high, low);
        var prevEma1 = _hasEma ? _ema1 : price;
        var prevEma2 = _hasEma ? _ema2 : price;
        var ema1 = VidyaBlend.Compute(prevEma1, price, _alpha);
        var ema2 = VidyaBlend.Compute(prevEma2, price, _alpha / 2);
        var dsp = ema1 - ema2;

        if (isFinal)
        {
            _ema1 = ema1;
            _ema2 = ema2;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
            _hasEma = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dsp", dsp }
            };
        }

        return new StreamingIndicatorStateResult(dsp, outputs);
    }
}

[PrimaryOutput("Do")]
public sealed class DerivativeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly DerivativeWindow _window;
    public DerivativeOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14, int length2 = 9, int length3 = 5, int length4 = 3) => _window = new(maType, length1, length2, length3, length4);
    public IndicatorName Name => IndicatorName.DerivativeOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Do", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Do")]
public sealed class DemandOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly DemandOscillatorWindow _window;
    public DemandOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 2, int length3 = 20)
        => _window = new(maType, length1, length2, length3);
    public IndicatorName Name => IndicatorName.DemandOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Do", point.Line }, { "Signal", point.SignalLine } } : null);
    }
    public void Dispose() => _window.Dispose();
    }

[PrimaryOutput("Dsm")]
public sealed class DoubleSmoothedMomentaState : IStreamingIndicatorState, IDisposable
{
    private readonly MomentaRangeWindow? _wide;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _topSmoother1;
    private readonly IMovingAverageSmoother _topSmoother2;
    private readonly IMovingAverageSmoother _botSmoother1;
    private readonly IMovingAverageSmoother _botSmoother2;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;

    public DoubleSmoothedMomentaState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2, int length2 = 5,
        int length3 = 25)
    {
        if (StrengthWindow.Supports(maType)) _wide = new MomentaRangeWindow(maType, length1, length2, length3);
        var resolved1 = Math.Max(2, length1);
        _maxWindow = new RollingWindowMax(resolved1);
        _minWindow = new RollingWindowMin(resolved1);
        _topSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _topSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _botSmoother1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _botSmoother2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _signal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DoubleSmoothedMomenta;

    public void Reset()
    {
        _wide?.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _topSmoother1.Reset();
        _topSmoother2.Reset();
        _botSmoother1.Reset();
        _botSmoother2.Reset();
        _signal.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var currentValue = _input.GetValue(bar);
        if (_wide is not null)
        {
            var next = _wide.Next(currentValue, isFinal);
            return new StreamingIndicatorStateResult(next.Value, includeOutputs
                ? new Dictionary<string, double> { { "Dsm", next.Value }, { "Signal", next.Signal } } : null);
        }
        var high = isFinal ? _maxWindow.Add(currentValue, out _) : _maxWindow.Preview(currentValue, out _);
        var low = isFinal ? _minWindow.Add(currentValue, out _) : _minWindow.Preview(currentValue, out _);
        var srcLc = currentValue - low;
        var hcLc = high - low;
        var top1 = _topSmoother1.Next(srcLc, isFinal);
        var top2 = _topSmoother2.Next(top1, isFinal);
        var bot1 = _botSmoother1.Next(hcLc, isFinal);
        var bot2 = _botSmoother2.Next(bot1, isFinal);
        var mom = bot2 != 0 ? MathHelper.MinOrMax(100 * top2 / bot2, 100, 0) : 0;
        var signal = _signal.Next(mom, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Dsm", mom },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mom, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _topSmoother1.Dispose();
        _topSmoother2.Dispose();
        _botSmoother1.Dispose();
        _botSmoother2.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Curta")]
public sealed class DidiIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _shortSma;
    private readonly IMovingAverageSmoother _mediumSma;
    private readonly IMovingAverageSmoother _longSma;
    private readonly StreamingInputResolver _input;

    public DidiIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 3, int length2 = 8, int length3 = 20)
    {
        _shortSma = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length1)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _mediumSma = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length2)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _longSma = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length3)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DidiIndex;

    public void Reset()
    {
        _shortSma.Reset();
        _mediumSma.Reset();
        _longSma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mediumSma = _mediumSma.Next(value, isFinal);
        var shortSma = _shortSma.Next(value, isFinal);
        var longSma = _longSma.Next(value, isFinal);
        var curta = mediumSma != 0 ? shortSma / mediumSma : 0;
        var longa = mediumSma != 0 ? longSma / mediumSma : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Curta", curta },
                { "Media", mediumSma == 0 ? 0 : 1 },
                { "Longa", longa }
            };
        }

        return new StreamingIndicatorStateResult(curta, outputs);
    }

    public void Dispose()
    {
        _shortSma.Dispose();
        _mediumSma.Dispose();
        _longSma.Dispose();
    }
}

[PrimaryOutput("Di")]
public sealed class DisparityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;

    public DisparityIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        _smoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(length) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DisparityIndex;

    public void Reset()
    {
        _smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _smoother.Next(value, isFinal);
        var disparity = RoundedPercentageChange.Of(value, sma);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Di", disparity }
            };
        }

        return new StreamingIndicatorStateResult(disparity, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}

[PrimaryOutput("Di")]
public sealed class DampingIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DampingWindow _window;
    public DampingIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5, double threshold = 1.5) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.DampingIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Di", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Dti")]
public sealed class DirectionalTrendIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly DirectionalStrengthWindow? _wide;
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private double _prevHigh;
    private double _prevLow;
    private bool _hasPrev;

    public DirectionalTrendIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14, int length2 = 10, int length3 = 5)
    {
        if (StrengthWindow.Supports(maType)) _wide = new DirectionalStrengthWindow(maType, length1, length2, length3);
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
    }

    public IndicatorName Name => IndicatorName.DirectionalTrendIndex;

    public void Reset()
    {
        _wide?.Reset();
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        double dti;
        if (_wide is not null) dti = _wide.Next(bar.High, bar.Low, isFinal);
        else
        {
            var prevHigh = _hasPrev ? _prevHigh : 0;
            var prevLow = _hasPrev ? _prevLow : 0;
            var hmu = bar.High - prevHigh > 0 ? bar.High - prevHigh : 0;
            var lmd = bar.Low - prevLow < 0 ? (bar.Low - prevLow) * -1 : 0;
            var diff = hmu - lmd;
            var absDiff = Math.Abs(diff);

            var diffEma1 = _diffEma1.Next(diff, isFinal);
            var absDiffEma1 = _absDiffEma1.Next(absDiff, isFinal);
            var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
            var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
            var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
            var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
            dti = absDiffEma3 != 0 ? MathHelper.MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;

        }

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Dti", dti }
            };
        }

        return new StreamingIndicatorStateResult(dti, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
    }
}

[PrimaryOutput("Dto")]
public sealed class DTOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly DtOscillatorWindow _window;
    public DTOscillatorState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 13, int length2 = 8, int length3 = 5, int length4 = 3) => _window = new(maType, length1, length2, length3, length4);
    public IndicatorName Name => IndicatorName.DTOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Dto", point.Line }, { "Signal", point.SignalLine } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Roc")]
public sealed class RateOfChangeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public RateOfChangeState(int length = 12)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
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

        var roc = RoundedPercentageChange.Of(current, prevValue);
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

[PrimaryOutput("Ui")]
public sealed class UlcerIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly DrawdownWindow _window;
    private readonly StreamingInputResolver _input;
    public UlcerIndexState(int length = 14) { _window = new(length); _input = new(InputName.Close, null); }
    internal UlcerIndexState(int length, Func<OhlcvBar, double> selector)
    { if (selector is null) throw new ArgumentNullException(nameof(selector)); _window = new(length); _input = new(InputName.Close, selector); }
    public IndicatorName Name => IndicatorName.UlcerIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Ui", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("ViPlus")]
public sealed class VortexIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly VortexWindow _window;
    public VortexIndicatorState(int length = 14) => _window = new VortexWindow(length);
    public IndicatorName Name => IndicatorName.VortexIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "ViPlus", value.Plus }, { "ViMinus", value.Minus } } : null;
        return new StreamingIndicatorStateResult(value.Plus, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ao")]
public sealed class AwesomeOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private StreamingInputResolver _input;

    // Simple averages of the median price, as Williams defined it and as the batch indicator defaults to.
    public AwesomeOscillatorState(int fastLength = 5, int slowLength = 34,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.MedianPrice, null);
    }

    public IndicatorName Name => IndicatorName.AwesomeOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fastSma = _fast.Next(value, isFinal);
        var slowSma = _slow.Next(value, isFinal);
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
        _fast.Dispose();
        _slow.Dispose();
    }
}

[PrimaryOutput("Ac")]
public sealed class AcceleratorOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _smooth;
    private StreamingInputResolver _input;
    private bool _signalInvalid;

    // The awesome oscillator less its own average, on simple averages as the batch indicator defaults to.
    public AcceleratorOscillatorState(int fastLength = 5, int slowLength = 34, int smoothLength = 5,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage)
    {
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _smooth = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, smoothLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.MedianPrice, null);
    }

    public IndicatorName Name => IndicatorName.AcceleratorOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
        _smooth.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fastSma = _fast.Next(value, isFinal);
        var slowSma = _slow.Next(value, isFinal);
        var ao = fastSma - slowSma;

        var invalid = _signalInvalid || double.IsNaN(ao) || double.IsInfinity(ao);
        var aoSma = invalid ? double.NaN : _smooth.Next(ao, isFinal);
        if (isFinal) _signalInvalid = invalid;
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
        _fast.Dispose();
        _slow.Dispose();
        _smooth.Dispose();
    }
}

[PrimaryOutput("Trix")]
public sealed class TrixState : IStreamingIndicatorState, IDisposable
{
    private readonly TrixWindow _window;
    public TrixState(int length = 15, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.Trix;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal).Publish();
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Trix", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("1lsma")]
public sealed class _1LCLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly OneLcWindow _window;
    public _1LCLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName._1LCLeastSquaresMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "1lsma", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("3hma")]
public sealed class _3HMAState : IStreamingIndicatorState, IDisposable
{
    private readonly ThreeHullWindow _window;
    public _3HMAState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 50) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName._3HMA;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "3hma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Macd1")]
public sealed class _4MovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema8;
    private readonly IMovingAverageSmoother _ema10;
    private readonly IMovingAverageSmoother _ema17;
    private readonly IMovingAverageSmoother _ema14;
    private readonly IMovingAverageSmoother _ema16;
    private readonly IMovingAverageSmoother _signal1;
    private readonly IMovingAverageSmoother _signal2;
    private readonly IMovingAverageSmoother _signal3;
    private readonly IMovingAverageSmoother _signal4;
    private readonly double _blueMult;
    private readonly double _yellowMult;
    private readonly StreamingInputResolver _input;
    private readonly bool[] _signalInvalid = new bool[4];

    public _4MovingAverageConvergenceDivergenceState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5,
        int length2 = 8, int length3 = 10, int length4 = 17, int length5 = 14, int length6 = 16,
        double blueMult = 4.3, double yellowMult = 1.4)
    {
        var resolved1 = Math.Max(1, length1);
        _ema5 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length2)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length3)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length4)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length5)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length6)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName._4MovingAverageConvergenceDivergence;

    public void Reset()
    {
        _ema5.Reset();
        _ema8.Reset();
        _ema10.Reset();
        _ema17.Reset();
        _ema14.Reset();
        _ema16.Reset();
        _signal1.Reset();
        _signal2.Reset();
        _signal3.Reset();
        _signal4.Reset();
        Array.Clear(_signalInvalid, 0, _signalInvalid.Length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema5 = _ema5.Next(value, isFinal);
        var ema8 = _ema8.Next(value, isFinal);
        var ema10 = _ema10.Next(value, isFinal);
        var ema17 = _ema17.Next(value, isFinal);
        var ema14 = _ema14.Next(value, isFinal);
        var ema16 = _ema16.Next(value, isFinal);

        var macd1 = ema17 - ema14;
        var macd2 = ema17 - ema8;
        var macd3 = ema10 - ema16;
        var macd4 = ema5 - ema10;

        var invalid1 = _signalInvalid[0] || double.IsInfinity(macd1);
        var macd1Signal = invalid1 ? double.NaN : _signal1.Next(macd1, isFinal);
        if (isFinal) _signalInvalid[0] = invalid1;
        var invalid2 = _signalInvalid[1] || double.IsInfinity(macd2);
        var macd2Signal = invalid2 ? double.NaN : _signal2.Next(macd2, isFinal);
        if (isFinal) _signalInvalid[1] = invalid2;
        var invalid3 = _signalInvalid[2] || double.IsInfinity(macd3);
        var macd3Signal = invalid3 ? double.NaN : _signal3.Next(macd3, isFinal);
        if (isFinal) _signalInvalid[2] = invalid3;
        var invalid4 = _signalInvalid[3] || double.IsInfinity(macd4);
        var macd4Signal = invalid4 ? double.NaN : _signal4.Next(macd4, isFinal);
        if (isFinal) _signalInvalid[3] = invalid4;

        var macd1Histogram = macd1 - macd1Signal;
        var macd2Histogram = macd2 - macd2Signal;
        var macd3Histogram = macd3 - macd3Signal;
        var macd4Histogram = macd4 - macd4Signal;
        _ = _blueMult * macd1Histogram;
        _ = _yellowMult * macd3Histogram;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Macd1", macd4 },
                { "Signal1", macd4Signal },
                { "Histogram1", macd4Histogram },
                { "Macd2", macd2 },
                { "Signal2", macd2Signal },
                { "Histogram2", macd2Histogram }
            };
        }

        return new StreamingIndicatorStateResult(macd4, outputs);
    }

    public void Dispose()
    {
        _ema5.Dispose();
        _ema8.Dispose();
        _ema10.Dispose();
        _ema17.Dispose();
        _ema14.Dispose();
        _ema16.Dispose();
        _signal1.Dispose();
        _signal2.Dispose();
        _signal3.Dispose();
        _signal4.Dispose();
    }
}

[PrimaryOutput("Ppo1")]
public sealed class _4PercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema5;
    private readonly IMovingAverageSmoother _ema8;
    private readonly IMovingAverageSmoother _ema10;
    private readonly IMovingAverageSmoother _ema17;
    private readonly IMovingAverageSmoother _ema14;
    private readonly IMovingAverageSmoother _ema16;
    private readonly IMovingAverageSmoother _signal1;
    private readonly IMovingAverageSmoother _signal2;
    private readonly IMovingAverageSmoother _signal3;
    private readonly IMovingAverageSmoother _signal4;
    private readonly double _blueMult;
    private readonly double _yellowMult;
    private readonly StreamingInputResolver _input;
    private readonly bool[] _signalInvalid = new bool[4];

    public _4PercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 5,
        int length2 = 8, int length3 = 10, int length4 = 17, int length5 = 14, int length6 = 16,
        double blueMult = 4.3, double yellowMult = 1.4)
    {
        var resolved1 = Math.Max(1, length1);
        _ema5 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _ema8 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length2)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _ema10 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length3)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _ema17 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length4)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _ema14 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length5)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _ema16 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length6)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signal1 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal2 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal3 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _signal4 = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved1) : MovingAverageSmootherFactory.Create(maType, resolved1);
        _blueMult = blueMult;
        _yellowMult = yellowMult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName._4PercentagePriceOscillator;

    public void Reset()
    {
        _ema5.Reset();
        _ema8.Reset();
        _ema10.Reset();
        _ema17.Reset();
        _ema14.Reset();
        _ema16.Reset();
        _signal1.Reset();
        _signal2.Reset();
        _signal3.Reset();
        _signal4.Reset();
        Array.Clear(_signalInvalid, 0, _signalInvalid.Length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema5 = _ema5.Next(value, isFinal);
        var ema8 = _ema8.Next(value, isFinal);
        var ema10 = _ema10.Next(value, isFinal);
        var ema17 = _ema17.Next(value, isFinal);
        var ema14 = _ema14.Next(value, isFinal);
        var ema16 = _ema16.Next(value, isFinal);


        var ppo1 = RoundedPercentageChange.Of(ema17, ema14);
        var ppo2 = RoundedPercentageChange.Of(ema17, ema8);
        var ppo3 = RoundedPercentageChange.Of(ema10, ema16);
        var ppo4 = RoundedPercentageChange.Of(ema5, ema10);

        var invalid1 = _signalInvalid[0] || double.IsInfinity(ppo1);
        var ppo1Signal = invalid1 ? double.NaN : _signal1.Next(ppo1, isFinal);
        if (isFinal) _signalInvalid[0] = invalid1;
        var invalid2 = _signalInvalid[1] || double.IsInfinity(ppo2);
        var ppo2Signal = invalid2 ? double.NaN : _signal2.Next(ppo2, isFinal);
        if (isFinal) _signalInvalid[1] = invalid2;
        var invalid3 = _signalInvalid[2] || double.IsInfinity(ppo3);
        var ppo3Signal = invalid3 ? double.NaN : _signal3.Next(ppo3, isFinal);
        if (isFinal) _signalInvalid[2] = invalid3;
        var invalid4 = _signalInvalid[3] || double.IsInfinity(ppo4);
        var ppo4Signal = invalid4 ? double.NaN : _signal4.Next(ppo4, isFinal);
        if (isFinal) _signalInvalid[3] = invalid4;

        var ppo1Histogram = ppo1 - ppo1Signal;
        var ppo2Histogram = ppo2 - ppo2Signal;
        var ppo3Histogram = ppo3 - ppo3Signal;
        var ppo4Histogram = ppo4 - ppo4Signal;
        _ = _blueMult * ppo1Histogram;
        _ = _yellowMult * ppo3Histogram;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(6)
            {
                { "Ppo1", ppo4 },
                { "Signal1", ppo4Signal },
                { "Histogram1", ppo4Histogram },
                { "Ppo2", ppo2 },
                { "Signal2", ppo2Signal },
                { "Histogram2", ppo2Histogram }
            };
        }

        return new StreamingIndicatorStateResult(ppo4, outputs);
    }

    public void Dispose()
    {
        _ema5.Dispose();
        _ema8.Dispose();
        _ema10.Dispose();
        _ema17.Dispose();
        _ema14.Dispose();
        _ema16.Dispose();
        _signal1.Dispose();
        _signal2.Dispose();
        _signal3.Dispose();
        _signal4.Dispose();
    }
}

[PrimaryOutput("Ama")]
public sealed class AdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly AdaptiveRangeMeanWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public AdaptiveMovingAverageState(int fastLength=2,int slowLength=14,int length=14)=>_window=new(fastLength,slowLength,length);
    public IndicatorName Name=>IndicatorName.AdaptiveMovingAverage;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value=_window.Next(_input.GetValue(bar),bar.High,bar.Low,isFinal);
        return new(value,includeOutputs?new Dictionary<string,double>{{"Ama",value}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Aema")]
public sealed class AdaptiveExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveEmaWindow _window;
    public AdaptiveExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.AdaptiveExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Aema", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Aarma")]
public sealed class AdaptiveAutonomousRecursiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveAutonomousWindow _window;
    public AdaptiveAutonomousRecursiveMovingAverageState(int length = 14, double gamma = 3) => _window = new(length, gamma);
    public IndicatorName Name => IndicatorName.AdaptiveAutonomousRecursiveMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value.Average, includeOutputs ? new Dictionary<string, double> { { "D", value.Deviation }, { "Aarma", value.Average } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Als")]
public sealed class AdaptiveLeastSquaresState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveFitWindow _window;
    public AdaptiveLeastSquaresState(int length = 500, double smooth = 1.5) => _window = new(length, smooth);
    public IndicatorName Name => IndicatorName.AdaptiveLeastSquares;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal); return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Als", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ema")]
public sealed class AlphaDecreasingExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly AlphaDecreasingWindow _window = new();
    public IndicatorName Name => IndicatorName.AlphaDecreasingExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ema", value } } : null);
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class AdaptivePriceZoneIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveZoneWindow _window;
    public AdaptivePriceZoneIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, double pct = 2) => _window = new(maType, length, pct);
    public IndicatorName Name => IndicatorName.AdaptivePriceZoneIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Arsi")]
public sealed class AdaptiveRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly StreamingInputResolver _input;
    private double _prevArsi;
    private bool _hasPrev;

    public AdaptiveRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14)
    {
        _rsi = new RsiState(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AdaptiveRelativeStrengthIndex;

    public void Reset()
    {
        _rsi.Reset();
        _prevArsi = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var prevArsi = _hasPrev ? _prevArsi : 0;
        var arsi = AdaptiveRsiBlend.Next(value, prevArsi, rsi);

        if (isFinal)
        {
            _prevArsi = arsi;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Arsi", arsi }
            };
        }

        return new StreamingIndicatorStateResult(arsi, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
    }
}

[PrimaryOutput("Ast")]
public sealed class AdaptiveStochasticState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveStochasticWindow _window;
    public AdaptiveStochasticState(int length = 50, int fastLength = 50, int slowLength = 200) => _window = new(length, fastLength, slowLength);
    public IndicatorName Name => IndicatorName.AdaptiveStochastic;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Ast", value } } : null); }
    public void Dispose() { }

}

[PrimaryOutput("Ts")]
public sealed class AdaptiveTrailingStopState : IStreamingIndicatorState, IDisposable
{
    private readonly PoweredKaufmanWindow _window;
    public AdaptiveTrailingStopState(int length = 100, double factor = 3) => _window = new(length, factor);
    public IndicatorName Name => IndicatorName.AdaptiveTrailingStop;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new(value.Stop, includeOutputs ? new Dictionary<string, double> { { "Ts", value.Stop } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ts")]
public sealed class AdaptiveAutonomousRecursiveTrailingStopState : IStreamingIndicatorState, IDisposable
{
    private readonly AdaptiveAutonomousWindow _window;
    public AdaptiveAutonomousRecursiveTrailingStopState(int length = 14, double gamma = 3) => _window = new(length, gamma);
    public IndicatorName Name => IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Stop;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ts", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ahma")]
public sealed class AhrensMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AhrensWindow _window;
    public AhrensMovingAverageState(int length = 9) => _window = new(length);
    public IndicatorName Name => IndicatorName.AhrensMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ahma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Alma")]
public sealed class ArnaudLegouxMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AlmaWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public ArnaudLegouxMovingAverageState(int length = 9, double offset = 0.85, int sigma = 6)
    {
        _mean = new AlmaWindowMean(length, offset, sigma);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ArnaudLegouxMovingAverage;

    public void Reset()
    {
        _mean.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var alma = _mean.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Alma", alma }
            };
        }

        return new StreamingIndicatorStateResult(alma, outputs);
    }

    public void Dispose()
    {
        _mean.Dispose();
    }
}

[PrimaryOutput("Lips")]
public sealed class AlligatorIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly AlligatorLineWindow _jaw, _teeth, _lips;
    private StreamingInputResolver _input = new(InputName.MedianPrice, null);
    public AlligatorIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int jawLength = 13,
        int jawOffset = 8, int teethLength = 8, int teethOffset = 5, int lipsLength = 5, int lipsOffset = 3)
    {
        _jaw = new(maType, jawLength, jawOffset); _teeth = new(maType, teethLength, teethOffset); _lips = new(maType, lipsLength, lipsOffset);
    }
    public IndicatorName Name => IndicatorName.AlligatorIndex;
    void ICustomInputConsumer.ReadCloseAsInput() => _input = new StreamingInputResolver(InputName.Close, null);
    public void Reset() { _jaw.Reset(); _teeth.Reset(); _lips.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var price = _input.GetValue(bar); var jaw = _jaw.Next(price, isFinal); var teeth = _teeth.Next(price, isFinal); var lips = _lips.Next(price, isFinal);
        return new StreamingIndicatorStateResult(lips, includeOutputs ? new Dictionary<string, double> { { "Jaws", jaw }, { "Teeth", teeth }, { "Lips", lips } } : null);
    }
    public void Dispose() { _jaw.Dispose(); _teeth.Dispose(); _lips.Dispose(); }
}

[PrimaryOutput("Amom")]
public sealed class AnchoredMomentumState : IStreamingIndicatorState, IDisposable
{
    private readonly AnchoredMomentumWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public AnchoredMomentumState(MovingAvgType maType=MovingAvgType.ExponentialMovingAverage,int smoothLength=7,int signalLength=8,int momentumLength=10){_window=new(maType,smoothLength,signalLength,momentumLength);}
    public IndicatorName Name=>IndicatorName.AnchoredMomentum;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {StreamingInputValidation.Validate(bar);var r=_window.Next(_input.GetValue(bar),isFinal);return new(r.Value,includeOutputs?new Dictionary<string,double>{{"Amom",r.Value},{"Signal",r.Signal}}:null);}
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("Asrsi")]
public sealed class ApirineSlowRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly ApirineRsiWindow? _wide;
    private readonly IMovingAverageSmoother _smooth;
    private readonly IMovingAverageSmoother _gain;
    private readonly IMovingAverageSmoother _loss;
    private readonly StreamingInputResolver _input;
    private readonly bool _stableWilder;
    private readonly double _retention;
    private double _residual;
    private double _previousPrice;

    public ApirineSlowRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        int smoothLength = 6)
    {
        if (StrengthWindow.Supports(maType)) _wide = new ApirineRsiWindow(maType, length, smoothLength);
        _stableWilder = maType == MovingAvgType.WildersSmoothingMethod;
        _retention = 1 - 1d / Math.Max(1, smoothLength);
        _smooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _gain = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _loss = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ApirineSlowRelativeStrengthIndex;

    public void Reset()
    {
        _wide?.Reset();
        _residual = 0;
        _previousPrice = 0;
        _smooth.Reset();
        _gain.Reset();
        _loss.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        if (_wide is not null)
        {
            var wideValue = _wide.Next(value, isFinal);
            return new StreamingIndicatorStateResult(wideValue, includeOutputs ? new Dictionary<string, double> { { "Asrsi", wideValue } } : null);
        }
        var r1 = _smooth.Next(value, isFinal);
        var residual = _stableWilder ? _retention * (_residual + (value - _previousPrice)) : value - r1;
        if (isFinal)
        {
            _residual = residual;
            _previousPrice = value;
        }
        var r2 = Math.Max(residual, 0);
        var r3 = Math.Max(-residual, 0);
        var r4 = _gain.Next(r2, isFinal);
        var r5 = _loss.Next(r3, isFinal);
        var rs = r5 != 0 ? r4 / r5 : 0;
        var rr = r5 == 0 ? 100 : r4 == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Asrsi", rr }
            };
        }

        return new StreamingIndicatorStateResult(rr, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _smooth.Dispose();
        _gain.Dispose();
        _loss.Dispose();
    }
}

[PrimaryOutput("Arsi")]
public sealed class AsymmetricalRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly AsymmetricGainLossWindow _window;
    public AsymmetricalRelativeStrengthIndexState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.AsymmetricalRelativeStrengthIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Arsi", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Afp")]
public sealed class AtrFilteredExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AtrFilterWindow _window;
    public AtrFilteredExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 45, int atrLength = 20, int stdDevLength = 10, int lbLength = 20, double min = 5) => _window = new(maType, length, atrLength, stdDevLength, lbLength, min);
    public IndicatorName Name => IndicatorName.AtrFilteredExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Afp", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class AutoDispersionBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly AutoDispersionWindow _window;
    public AutoDispersionBandsState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 90, int smoothLength = 140) { _window = new(maType, length, smoothLength); }
    public IndicatorName Name => IndicatorName.AutoDispersionBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var p = _window.Next(bar.Close, isFinal);
        return new(p.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", p.Upper }, { "MiddleBand", p.Middle }, { "LowerBand", p.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Af")]
public sealed class AutoFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly AutoFilterWindow _window;
    public AutoFilterState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.AutoFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Line; return new(value, includeOutputs ? new Dictionary<string, double> { { "Af", value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Al")]
public sealed class AutoLineState : IStreamingIndicatorState, IDisposable
{
    // The deviation of the window about its own mean, matching the batch calculation; see #190. The two have
    // to move together or the engines band the line at different widths over the same prices.
    private readonly RollingStandardDeviation _stdDev;
    private readonly StreamingInputResolver _input;
    private double _prevX;
    private bool _hasPrev;

    public AutoLineState(int length = 500)
    {
        var resolved = Math.Max(1, length);

        // No moving-average type: a windowed deviation is taken about the window's own mean.
        _stdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.AutoLine;

    public void Reset()
    {
        _stdDev.Reset();
        _prevX = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        // Fed the resolved input rather than the bar, so a composed reading measures the series it was
        // composed on rather than resolving a close of its own.
        var dev = _stdDev.Next(value, isFinal);
        var prevX = _hasPrev ? _prevX : value;
        var x = value > prevX + dev ? value : value < prevX - dev ? value : prevX;

        if (isFinal)
        {
            _prevX = x;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Al", x }
            };
        }

        return new StreamingIndicatorStateResult(x, outputs);
    }

    public void Dispose()
    {
        _stdDev.Dispose();
    }
}

[PrimaryOutput("Alwd")]
public sealed class AutoLineWithDriftState : IStreamingIndicatorState, IDisposable
{
    private readonly AutoDriftWindow _window;
    public AutoLineWithDriftState(int length=500)=>_window=new AutoDriftWindow(length);
    public IndicatorName Name=>IndicatorName.AutoLineWithDrift;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.Close,isFinal);
        return new StreamingIndicatorStateResult(value,includeOutputs?new Dictionary<string,double>{{"Alwd",value}}:null);
    }
    public void Dispose()=>_window.Reset();
}

[PrimaryOutput("Arma")]
public sealed class AutonomousRecursiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AutonomousRecursiveWindow _window;
    public AutonomousRecursiveMovingAverageState(int length = 14, int momLength = 7, double gamma = 3) => _window = new(length, momLength, gamma);
    public IndicatorName Name => IndicatorName.AutonomousRecursiveMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Arma", value } } : null); }
    public void Dispose() { }

}

[PrimaryOutput("Aaen")]
public sealed class AverageAbsoluteErrorNormalizationState : IStreamingIndicatorState, IDisposable
{
    private readonly AbsoluteErrorWindow _window;
    public AverageAbsoluteErrorNormalizationState(int length=14)=>_window=new AbsoluteErrorWindow(length);
    public IndicatorName Name=>IndicatorName.AverageAbsoluteErrorNormalization;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var value=_window.Next(bar.Close,isFinal);
        return new StreamingIndicatorStateResult(value,includeOutputs?new Dictionary<string,double>{{"Aaen",value}}:null);
    }
    public void Dispose()=>_window.Reset();
}

[PrimaryOutput("Amfo")]
public sealed class AverageMoneyFlowOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly AverageMoneyFlowWindow _window;
    public AverageMoneyFlowOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 5, int smoothLength = 3) => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.AverageMoneyFlowOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.Volume, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Amfo", value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Atrts")]
public sealed class AverageTrueRangeTrailingStopsState : IStreamingIndicatorState, IDisposable
{
    private readonly AtrTrailingWindow _window;
    public AverageTrueRangeTrailingStopsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63, int length2 = 21, double factor = 3)
    { _window = new(maType, length1, length2, factor); }
    public IndicatorName Name => IndicatorName.AverageTrueRangeTrailingStops;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Atrts", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("ProbPrime")]
public sealed class BayesianOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly BayesianWindow _window;
    public BayesianOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20, double stdDevMult = 2.5, double lowerThreshold = 15) => _window = new(maType, length, stdDevMult, lowerThreshold);
    public IndicatorName Name => IndicatorName.BayesianOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Prime, includeOutputs ? new Dictionary<string, double> { { "SigmaProbsDown", point.Down }, { "SigmaProbsUp", point.Up }, { "ProbPrime", point.Prime } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Bvi")]
public sealed class BetterVolumeIndicatorState : IStreamingIndicatorState
{
    private readonly BetterVolumeWindow _window;
    public BetterVolumeIndicatorState(int length = 8, int lbLength = 2) => _window = new(length, lbLength);
    public IndicatorName Name => IndicatorName.BetterVolumeIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Bvi", value } } : null); }

}

[PrimaryOutput("Bso")]
public sealed class BilateralStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly BilateralStochasticWindow _window;
    public BilateralStochasticOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100, int signalLength = 20) => _window = new(maType, length, signalLength);
    public IndicatorName Name => IndicatorName.BilateralStochasticOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Bso, includeOutputs ? new Dictionary<string, double> { { "Bull", point.Bull }, { "Bear", point.Bear }, { "Bso", point.Bso }, { "Signal", point.SignalLine } } : null); }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("AtrDev")]
public sealed class BollingerBandsAverageTrueRangeState : IStreamingIndicatorState, IDisposable
{
    private readonly BollingerAtrWindow _window;
    public BollingerBandsAverageTrueRangeState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int atrLength = 22, int length = 55, double stdDevMult = 2)
    { _window = new(maType, atrLength, length, stdDevMult); }
    public IndicatorName Name => IndicatorName.BollingerBandsAverageTrueRange;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.High, bar.Low, bar.Close, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "AtrDev", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class BollingerBandsFibonacciRatiosState : IStreamingIndicatorState, IDisposable
{
    private readonly StollerAverageRangeChannelsState _channel;
    public BollingerBandsFibonacciRatiosState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        double fibRatio1 = MathHelper.Phi, double fibRatio2 = MathHelper.Phi + 1, double fibRatio3 = (2 * MathHelper.Phi) + 1)
    { _channel = new(maType, length, fibRatio3); }
    public IndicatorName Name => IndicatorName.BollingerBandsFibonacciRatios;
    public void Reset() => _channel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs) => _channel.Update(bar, isFinal, includeOutputs);
    public void Dispose() => _channel.Dispose();
}

[PrimaryOutput("PctB")]
public sealed class BollingerBandsPercentBState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly ExactPopulationWindow _stdDev;
    private readonly StreamingInputResolver _input;

    public BollingerBandsPercentBState(double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new ExactPopulationWindow(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsPercentB;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        var stdDev = _stdDev.Next(value, isFinal);
        var pctB = BollingerArithmetic.Percent(value, basis, stdDev, _stdDevMult);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "PctB", pctB }
            };
        }

        return new StreamingIndicatorStateResult(pctB, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
    }
}

[PrimaryOutput("BbWidth")]
public sealed class BollingerBandsWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly double _stdDevMult;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly ExactPopulationWindow _stdDev;
    private readonly StreamingInputResolver _input;

    public BollingerBandsWidthState(double stdDevMult = 2, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        var resolved = Math.Max(1, length);
        _stdDevMult = stdDevMult;
        _basisSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolved) : MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new ExactPopulationWindow(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.BollingerBandsWidth;

    public void Reset()
    {
        _basisSmoother.Reset();
        _stdDev.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        var stdDev = _stdDev.Next(value, isFinal);
        var bbWidth = BollingerArithmetic.Width(basis, stdDev, _stdDevMult);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "BbWidth", bbWidth }
            };
        }

        return new StreamingIndicatorStateResult(bbWidth, outputs);
    }

    public void Dispose()
    {
        _basisSmoother.Dispose();
        _stdDev.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class BollingerBandsWithAtrPctState : IStreamingIndicatorState, IDisposable
{
    private readonly AtrPercentBandWindow _window;
    public BollingerBandsWithAtrPctState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, int bbLength = 20, double stdDevMult = 2)
    { _window = new(maType, length, bbLength, stdDevMult); }
    public IndicatorName Name => IndicatorName.BollingerBandsWithAtrPct;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Brsi")]
public sealed class BreakoutRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly BreakoutRsiWindow _window; private bool _selected; private CustomInputRange _range;
    public BreakoutRelativeStrengthIndexState(int length = 14, int lbLength = 2) => _window = new(length, lbLength);
    public IndicatorName Name => IndicatorName.BreakoutRelativeStrengthIndex;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public void Reset() { _window.Reset(); _range.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var price = _selected ? bar.Close : BreakoutRsiWindow.Price(bar.Open, bar.High, bar.Low, bar.Close); var candle = _selected ? bar : _range.Next(bar, price, isFinal);
        var value = _window.Next(price, bar.Open, candle.High, candle.Low, bar.Close, bar.Volume, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Brsi", value } } : null);
    }
    public void Dispose() { }

}

[PrimaryOutput("Bama")]
public sealed class BryantAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly BryantWindow _window;
    public BryantAdaptiveMovingAverageState(int length = 14, int maxLength = 100, double trend = -1) => _window = new(length, maxLength, trend);
    public IndicatorName Name => IndicatorName.BryantAdaptiveMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Bama", value } } : null); }
    public void Dispose() { }

}

[PrimaryOutput("FastBuff")]
public sealed class BuffAverageState : IStreamingIndicatorState, IDisposable    
{
    private readonly BuffWindow _fast, _slow;
    public BuffAverageState(int fastLength = 5, int slowLength = 20) { _fast = new(fastLength); _slow = new(slowLength); }
    public IndicatorName Name => IndicatorName.BuffAverage;
    public void Reset() { _fast.Reset(); _slow.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var fast = _fast.Next(bar.Close, bar.Volume, isFinal); var slow = _slow.Next(bar.Close, bar.Volume, isFinal);
        return new(fast, includeOutputs ? new Dictionary<string, double> { { "FastBuff", fast }, { "SlowBuff", slow } } : null);
    }
    public void Dispose() => Reset();
}

[PrimaryOutput("Cr")]
public sealed class CalmarRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly CalmarWindow _window;
    public CalmarRatioState(int length = 30) => _window = new(length);
    public IndicatorName Name => IndicatorName.CalmarRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Cr", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Pivot")]
public sealed class CamarillaPivotPointsState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyPivotLevels _daily = new(false, camarilla: true);
    public CamarillaPivotPointsState() { }
    public IndicatorName Name => IndicatorName.CamarillaPivotPoints;
    public void Reset() => _daily.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var levels = _daily.Next(bar.StartTime, bar.Open, bar.High, bar.Low, bar.Close, isFinal);
        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            var values = new Dictionary<string, double>(levels.Length);
            for (var i = 0; i < levels.Length; i++) values[_daily.Keys[i]] = levels[i];
            outputs = values;
        }
        return new StreamingIndicatorStateResult(levels[0], outputs);
    }
}

[PrimaryOutput("Type1")]
public sealed class CCTStochRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly StrengthAverage[]? _exactMeans;
    private readonly RsiState _rsi5;
    private readonly RsiState _rsi8;
    private readonly RsiState _rsi13;
    private readonly RsiState _rsi14;
    private readonly RsiState _rsi21;
    private readonly RollingWindowMin _rsi21Len2Min;
    private readonly RollingWindowMax _rsi21Len2Max;
    private readonly RollingWindowMin _rsi21Len3Min;
    private readonly RollingWindowMax _rsi21Len3Max;
    private readonly RollingWindowMin _rsi21Len5Min;
    private readonly RollingWindowMax _rsi21Len5Max;
    private readonly RollingWindowMin _rsi14Len4Min;
    private readonly RollingWindowMax _rsi14Len4Max;
    private readonly RollingWindowMin _rsi5Len1Min;
    private readonly RollingWindowMax _rsi5Len1Max;
    private readonly RollingWindowMin _rsi13Len3Min;
    private readonly RollingWindowMax _rsi13Len3Max;
    private readonly RollingWindowMin _rsi8Len2Min;
    private readonly RollingWindowMax _rsi8Len2Max;
    private readonly IMovingAverageSmoother _type4Smoother;
    private readonly IMovingAverageSmoother _type5Smoother;
    private readonly IMovingAverageSmoother _type6Smoother;
    private readonly IMovingAverageSmoother _customSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public CCTStochRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 5, int length2 = 8, int length3 = 13, int length4 = 14, int length5 = 21,
        int smoothLength1 = 3, int smoothLength2 = 8, int signalLength = 9)
    {
        if (StrengthWindow.Supports(maType)) _exactMeans = new[] { smoothLength2, smoothLength1, smoothLength1, smoothLength1, signalLength }
            .Select(n => new StrengthAverage(maType, n)).ToArray();
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var resolved4 = Math.Max(1, length4);
        var resolved5 = Math.Max(1, length5);
        _rsi5 = new RsiState(maType, resolved1);
        _rsi8 = new RsiState(maType, resolved2);
        _rsi13 = new RsiState(maType, resolved3);
        _rsi14 = new RsiState(maType, resolved4);
        _rsi21 = new RsiState(maType, resolved5);
        _rsi21Len2Min = new RollingWindowMin(resolved2);
        _rsi21Len2Max = new RollingWindowMax(resolved2);
        _rsi21Len3Min = new RollingWindowMin(resolved3);
        _rsi21Len3Max = new RollingWindowMax(resolved3);
        _rsi21Len5Min = new RollingWindowMin(resolved5);
        _rsi21Len5Max = new RollingWindowMax(resolved5);
        _rsi14Len4Min = new RollingWindowMin(resolved4);
        _rsi14Len4Max = new RollingWindowMax(resolved4);
        _rsi5Len1Min = new RollingWindowMin(resolved1);
        _rsi5Len1Max = new RollingWindowMax(resolved1);
        _rsi13Len3Min = new RollingWindowMin(resolved3);
        _rsi13Len3Max = new RollingWindowMax(resolved3);
        _rsi8Len2Min = new RollingWindowMin(resolved2);
        _rsi8Len2Max = new RollingWindowMax(resolved2);
        _type4Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength2));
        _type5Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _type6Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _customSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength1));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CCTStochRelativeStrengthIndex;

    public void Reset()
    {
        if (_exactMeans is not null) foreach (var mean in _exactMeans) mean.Reset();
        _rsi5.Reset();
        _rsi8.Reset();
        _rsi13.Reset();
        _rsi14.Reset();
        _rsi21.Reset();
        _rsi21Len2Min.Reset();
        _rsi21Len2Max.Reset();
        _rsi21Len3Min.Reset();
        _rsi21Len3Max.Reset();
        _rsi21Len5Min.Reset();
        _rsi21Len5Max.Reset();
        _rsi14Len4Min.Reset();
        _rsi14Len4Max.Reset();
        _rsi5Len1Min.Reset();
        _rsi5Len1Max.Reset();
        _rsi13Len3Min.Reset();
        _rsi13Len3Max.Reset();
        _rsi8Len2Min.Reset();
        _rsi8Len2Max.Reset();
        _type4Smoother.Reset();
        _type5Smoother.Reset();
        _type6Smoother.Reset();
        _customSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        // Five RSIs of the source, one per length. Each used to take the previous one's output, copying a batch
        // that read its own last result back as the input: the 21-bar RSI was an RSI of an RSI four deep.
        var value = _input.GetValue(bar);
        var rsi5 = _rsi5.Next(value, isFinal);
        var rsi8 = _rsi8.Next(value, isFinal);
        var rsi13 = _rsi13.Next(value, isFinal);
        var rsi14 = _rsi14.Next(value, isFinal);
        var rsi21 = _rsi21.Next(value, isFinal);

        var rsi21Len2Min = isFinal ? _rsi21Len2Min.Add(rsi21, out _) : _rsi21Len2Min.Preview(rsi21, out _);
        var rsi21Len2Max = isFinal ? _rsi21Len2Max.Add(rsi21, out _) : _rsi21Len2Max.Preview(rsi21, out _);
        var rsi21Len3Min = isFinal ? _rsi21Len3Min.Add(rsi21, out _) : _rsi21Len3Min.Preview(rsi21, out _);
        var rsi21Len3Max = isFinal ? _rsi21Len3Max.Add(rsi21, out _) : _rsi21Len3Max.Preview(rsi21, out _);
        var rsi21Len5Min = isFinal ? _rsi21Len5Min.Add(rsi21, out _) : _rsi21Len5Min.Preview(rsi21, out _);
        var rsi21Len5Max = isFinal ? _rsi21Len5Max.Add(rsi21, out _) : _rsi21Len5Max.Preview(rsi21, out _);
        var rsi14Len4Min = isFinal ? _rsi14Len4Min.Add(rsi14, out _) : _rsi14Len4Min.Preview(rsi14, out _);
        var rsi14Len4Max = isFinal ? _rsi14Len4Max.Add(rsi14, out _) : _rsi14Len4Max.Preview(rsi14, out _);
        var rsi5Len1Min = isFinal ? _rsi5Len1Min.Add(rsi5, out _) : _rsi5Len1Min.Preview(rsi5, out _);
        var rsi5Len1Max = isFinal ? _rsi5Len1Max.Add(rsi5, out _) : _rsi5Len1Max.Preview(rsi5, out _);
        var rsi13Len3Min = isFinal ? _rsi13Len3Min.Add(rsi13, out _) : _rsi13Len3Min.Preview(rsi13, out _);
        var rsi13Len3Max = isFinal ? _rsi13Len3Max.Add(rsi13, out _) : _rsi13Len3Max.Preview(rsi13, out _);
        var rsi8Len2Min = isFinal ? _rsi8Len2Min.Add(rsi8, out _) : _rsi8Len2Min.Preview(rsi8, out _);
        var rsi8Len2Max = isFinal ? _rsi8Len2Max.Add(rsi8, out _) : _rsi8Len2Max.Preview(rsi8, out _);

        var type1 = CctRsiRatio.Percent(rsi21, rsi21Len2Min, rsi21Len3Min, rsi21Len3Max);
        var type2 = CctRsiRatio.Percent(rsi21, rsi21Len5Min, rsi21Len5Min, rsi21Len5Max);
        var type3 = CctRsiRatio.Percent(rsi14, rsi14Len4Min, rsi14Len4Min, rsi14Len4Max);
        var type4Raw = CctRsiRatio.Percent(rsi21, rsi21Len3Min, rsi21Len3Min, rsi21Len2Max);
        var type5Raw = CctRsiRatio.Percent(rsi5, rsi5Len1Min, rsi5Len1Min, rsi5Len1Max);
        var type6Raw = CctRsiRatio.Percent(rsi13, rsi13Len3Min, rsi13Len3Min, rsi13Len3Max);
        var customRaw = CctRsiRatio.Percent(rsi8, rsi8Len2Min, rsi8Len2Min, rsi8Len2Max);

        var type4 = _exactMeans is null ? _type4Smoother.Next(type4Raw, isFinal) : _exactMeans[0].Next(new StrengthValue(type4Raw), isFinal).Mantissa;
        var type5 = _exactMeans is null ? _type5Smoother.Next(type5Raw, isFinal) : _exactMeans[1].Next(new StrengthValue(type5Raw), isFinal).Mantissa;
        var type6 = _exactMeans is null ? _type6Smoother.Next(type6Raw, isFinal) : _exactMeans[2].Next(new StrengthValue(type6Raw), isFinal).Mantissa;
        var typeCustom = _exactMeans is null ? _customSmoother.Next(customRaw, isFinal) : _exactMeans[3].Next(new StrengthValue(customRaw), isFinal).Mantissa;
        var signal = _exactMeans is null ? _signalSmoother.Next(type1, isFinal) : _exactMeans[4].Next(new StrengthValue(type1), isFinal).Mantissa;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "Type1", type1 },
                { "Type2", type2 },
                { "Type3", type3 },
                { "Type4", type4 },
                { "Type5", type5 },
                { "Type6", type6 },
                { "TypeCustom", typeCustom },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(type1, outputs);
    }

    public void Dispose()
    {
        if (_exactMeans is not null) foreach (var mean in _exactMeans) mean.Dispose();
        _rsi5.Dispose();
        _rsi8.Dispose();
        _rsi13.Dispose();
        _rsi14.Dispose();
        _rsi21.Dispose();
        _rsi21Len2Min.Dispose();
        _rsi21Len2Max.Dispose();
        _rsi21Len3Min.Dispose();
        _rsi21Len3Max.Dispose();
        _rsi21Len5Min.Dispose();
        _rsi21Len5Max.Dispose();
        _rsi14Len4Min.Dispose();
        _rsi14Len4Max.Dispose();
        _rsi5Len1Min.Dispose();
        _rsi5Len1Max.Dispose();
        _rsi13Len3Min.Dispose();
        _rsi13Len3Max.Dispose();
        _rsi8Len2Min.Dispose();
        _rsi8Len2Max.Dispose();
        _type4Smoother.Dispose();
        _type5Smoother.Dispose();
        _type6Smoother.Dispose();
        _customSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Ccmi")]
public sealed class ChandeCompositeMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeCompositeWindow _window;
    public ChandeCompositeMomentumIndexState(MovingAvgType maType = MovingAvgType.DoubleExponentialMovingAverage, int length1 = 5, int length2 = 10, int length3 = 20, int smoothLength = 3) => _window = new(maType, length1, length2, length3, smoothLength);
    public IndicatorName Name => IndicatorName.ChandeCompositeMomentumIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Ccmi", point.Line }, { "Signal", point.SignalLine } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Cfo")]
public sealed class ChandeForecastOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactLinearFitWindow _regression;
    private readonly StreamingInputResolver _input;

    public ChandeForecastOscillatorState(int length = 14)
    {
        _regression = new ExactLinearFitWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ChandeForecastOscillator;

    public void Reset()
    {
        _regression.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var pf = _regression.Next(value, isFinal).PercentResidual(value);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cfo", pf }
            };
        }

        return new StreamingIndicatorStateResult(pf, outputs);
    }

    public void Dispose()
    {
        _regression.Dispose();
    }
}

[PrimaryOutput("Cimi")]
public sealed class ChandeIntradayMomentumIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IntradayGainLossWindow _window;
    public ChandeIntradayMomentumIndexState(int length = 14) => _window = new(length);

    public IndicatorName Name => IndicatorName.ChandeIntradayMomentumIndex;

    public void Reset() => _window.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var imi = _window.Next(bar.Close, bar.Open, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cimi", imi }
            };
        }

        return new StreamingIndicatorStateResult(imi, outputs);
    }

    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ckrsi")]
public sealed class ChandeKrollRSquaredIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeKrollWindow _window;
    public ChandeKrollRSquaredIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, int smoothLength = 3) => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.ChandeKrollRSquaredIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Ckrsi", value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("ExitLong")]
public sealed class ChandelierExitState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandelierWindow _window;
    internal bool ShortOnly { get; set; }
    public ChandelierExitState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22, double mult = 3) => _window = new(maType, length, mult);
    public IndicatorName Name => IndicatorName.ChandelierExit;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(ShortOnly ? point.Short : point.Long, includeOutputs ? new Dictionary<string, double> { { "ExitLong", point.Long }, { "ExitShort", point.Short } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Cmoa")]
public sealed class ChandeMomentumOscillatorAbsoluteState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly ChandeMomentumWindow _momentum;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    private int _seen;

    public ChandeMomentumOscillatorAbsoluteState(int length = 9)
    {
        _length = Math.Max(1, length);
        _momentum = new ChandeMomentumWindow(_length);
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAbsolute;
    public void Reset() { _momentum.Reset(); _seen = 0; }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var momentum = _momentum.Next(_input.GetValue(bar), isFinal);
        var value = _seen < _length ? 0 : Math.Abs(momentum);
        if (isFinal && _seen < _length) _seen++;
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Cmoa", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _momentum.Dispose();
}

[PrimaryOutput("Cmoa")]
public sealed class ChandeMomentumOscillatorAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumAverageWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public ChandeMomentumOscillatorAverageState(int length1 = 5, int length2 = 10, int length3 = 20)
        => _window = new(length1, length2, length3);
    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _window.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Cmoa", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Cmoaa")]
public sealed class ChandeMomentumOscillatorAbsoluteAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumAverageWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public ChandeMomentumOscillatorAbsoluteAverageState(int length1 = 5, int length2 = 10, int length3 = 20)
        => _window = new(length1, length2, length3);
    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAbsoluteAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = Math.Abs(_window.Next(_input.GetValue(bar), isFinal));
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Cmoaa", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Cmoadi")]
public sealed class ChandeMomentumOscillatorAverageDisparityIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeDisparityWindow _window;
    public ChandeMomentumOscillatorAverageDisparityIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 200, int length2 = 50, int length3 = 20) => _window = new(maType, length1, length2, length3);
    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorAverageDisparityIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Cmoadi", value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Cmof")]
public sealed class ChandeMomentumOscillatorFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumWindow _window;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);

    public ChandeMomentumOscillatorFilterState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 9, double filter = 3)
    {
        _window = new ChandeMomentumWindow(length, filter);
        _signal = maType == MovingAvgType.SimpleMovingAverage
            ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, length))
            : MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
    }

    public IndicatorName Name => IndicatorName.ChandeMomentumOscillatorFilter;
    public void Reset() { _window.Reset(); _signal.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _window.Next(_input.GetValue(bar), isFinal);
        var signal = _signal.Next(value, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(2) { { "Cmof", value }, { "Signal", signal } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() { _window.Dispose(); _signal.Dispose(); }
}

[PrimaryOutput("Cqs")]
public sealed class ChandeQuickStickState : IStreamingIndicatorState, IDisposable
{
    private readonly OpenCloseAverageWindow _window;
    public ChandeQuickStickState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14) => _window = new(maType, length, 0);
    public IndicatorName Name => IndicatorName.ChandeQuickStick;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Signal, includeOutputs ? new Dictionary<string, double> { { "Cqs", value.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Cts")]
public sealed class ChandeTrendScoreState : IStreamingIndicatorState, IDisposable
{
    private readonly int _startLength;
    private readonly int _endLength;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public ChandeTrendScoreState(int startLength = 11, int endLength = 20)
    {
        _startLength = Math.Max(1, startLength);
        _endLength = Math.Max(_startLength, endLength);
        _values = new PooledRingBuffer<double>(_endLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ChandeTrendScore;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double ts = 0;
        for (var j = _startLength; j <= _endLength; j++)
        {
            var prevValue = _values.Count >= j ? _values[_values.Count - j] : 0;
            ts += value >= prevValue ? 1 : -1;
        }

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Cts", ts }
            };
        }

        return new StreamingIndicatorStateResult(ts, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

[PrimaryOutput("Cvida1")]
public sealed class ChandeVolatilityIndexDynamicAverageIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VolatilityIndexWindow _window;
    public ChandeVolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, double alpha1 = 0.2, double alpha2 = 0.04) => _window = new(maType, length, alpha1, alpha2);
    public IndicatorName Name => IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.First, includeOutputs ? new Dictionary<string, double> { { "Cvida1", point.First }, { "Cvida2", point.Second } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Crma")]
public sealed class CompoundRatioMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly CompoundRatioWindow _window;
    public CompoundRatioMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.CompoundRatioMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Crma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Cbci")]
public sealed class ConstanceBrownCompositeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly BrownCompositeWindow _window;
    public ConstanceBrownCompositeIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3) => _window = new(maType, fastLength, slowLength, length1, length2, smoothLength);
    public IndicatorName Name => IndicatorName.ConstanceBrownCompositeIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Line, includeOutputs ? new Dictionary<string, double> { { "Cbci", point.Line }, { "FastSignal", point.Fast }, { "SlowSignal", point.Slow } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Cma")]
public sealed class CorrectedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly CorrectedAverageWindow _window;
    public CorrectedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 35) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.CorrectedMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal).Value; return new(value, includeOutputs ? new Dictionary<string, double> { { "Cma", value } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Cwma")]
public sealed class CubedWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IntegerPowerWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public CubedWeightedMovingAverageState(int length = 14)
    {
        _mean = new IntegerPowerWindowMean(length, 3);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CubedWeightedMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Cwma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
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
    private int _sinceRebuild;
    private readonly bool _trackMeanRoundoff;
    private double _meanRoundoff, _lastMeanRoundoff;
    private bool _averageRequiresExact, _lastAverageRequiresExact;

    public RollingWindowSum(int length, bool trackMeanRoundoff = false)
    {
        _trackMeanRoundoff = trackMeanRoundoff;
        _window = new PooledRingBuffer<double>(length);
    }

    internal double Average(double value, bool isFinal, out int countAfter)
    {
        if (!_trackMeanRoundoff) throw new InvalidOperationException("Mean evaluation requires roundoff tracking.");
        var added = _sum + value;
        var previewRoundoff = MeanRoundoff.AfterAddition(_meanRoundoff, added);
        if (_window.Count == _window.Capacity)
            previewRoundoff = MeanRoundoff.AfterAddition(previewRoundoff, added - _window[0]);
        var previewRequiresExact = _averageRequiresExact || ExactMeanAccumulator.SevereCancellation(_sum, value, added)
            || _window.Count == _window.Capacity && ExactMeanAccumulator.SevereCancellation(added, -_window[0], added - _window[0]);
        previewRequiresExact |= _window.Count == _window.Capacity
            && Math.Abs(added - _window[0]) <= 1e-4 * Math.Max(Math.Abs(_window[0]), Math.Abs(value));
        var sum = isFinal ? Add(value, out countAfter) : Preview(value, out countAfter);
        var requiresExact = isFinal ? _lastAverageRequiresExact : previewRequiresExact;
        var roundoff = isFinal ? _lastMeanRoundoff : previewRoundoff;
        if (!requiresExact && !MeanRoundoff.RequiresExact(sum, countAfter, roundoff)) return sum / countAfter;
        var exact = new ExactMeanAccumulator();
        var start = !isFinal && _window.Count == _window.Capacity ? 1 : 0;
        for (var i = start; i < _window.Count; i++) exact.Add(_window[i]);
        if (!isFinal) exact.Add(value);
        return exact.Mean(countAfter);
    }

    public double Preview(double value, out int countAfter)
    {
        if (_window.Count < _window.Capacity)
        {
            countAfter = _window.Count + 1;
            return _sum + value;
        }

        countAfter = _window.Capacity;
        var sum = _sum + value - _window[0];
        if (Math.Abs(sum) <= 1e-4 * Math.Max(Math.Abs(_window[0]), Math.Abs(value)))
        {
            sum = 0;
            for (var i = 1; i < _window.Count; i++) sum += _window[i];
            sum += value;
        }
        return sum;
    }

    public double Add(double value, out int countAfter)
    {
        // Grouped as MovingAverageCore.SimpleMovingAverage groups it, and as Preview does.
        var previousSum = _sum;
        _sum += value;
        if (_trackMeanRoundoff) _meanRoundoff = MeanRoundoff.AfterAddition(_meanRoundoff, _sum);
        _averageRequiresExact |= ExactMeanAccumulator.SevereCancellation(previousSum, value, _sum);
        if (_window.TryAdd(value, out var removed))
        {
            previousSum = _sum;
            _sum -= removed;
            if (_trackMeanRoundoff) _meanRoundoff = MeanRoundoff.AfterAddition(_meanRoundoff, _sum);
            _averageRequiresExact |= ExactMeanAccumulator.SevereCancellation(previousSum, -removed, _sum);
            if (Math.Abs(_sum) <= 1e-4 * Math.Max(Math.Abs(removed), Math.Abs(value)))
            {
                _sum = 0;
                _meanRoundoff = 0;
                _averageRequiresExact = true;
                for (var i = 0; i < _window.Count; i++)
                {
                    _sum += _window[i];
                    if (_trackMeanRoundoff) _meanRoundoff = MeanRoundoff.AfterAddition(_meanRoundoff, _sum);
                }
            }
        }

        countAfter = _window.Count;
        var sum = _sum;
        _lastAverageRequiresExact = _averageRequiresExact;
        _lastMeanRoundoff = _meanRoundoff;

        // Rebuilt from the window every Capacity values, on the bars MovingAverageCore.SimpleMovingAverage
        // rebuilds on, and after the running sum is reported so a preview still equals its final bar. A running
        // sum otherwise keeps the rounding error of every value it has ever held.
        if (++_sinceRebuild == _window.Capacity)
        {
            _sinceRebuild = 0;
            _sum = 0;
            _averageRequiresExact = false;
            _meanRoundoff = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                previousSum = _sum;
                _sum += _window[i];
                if (_trackMeanRoundoff) _meanRoundoff = MeanRoundoff.AfterAddition(_meanRoundoff, _sum);
                _averageRequiresExact |= ExactMeanAccumulator.SevereCancellation(previousSum, _window[i], _sum);
            }
        }

        return sum;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = 0;
        _sinceRebuild = 0;
        _meanRoundoff = _lastMeanRoundoff = 0;
        _averageRequiresExact = _lastAverageRequiresExact = false;
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

internal sealed class RollingCumulativeSum
{
    // Prefix sums as high + low pairs, as batch RollingSum keeps them: see CompensatedSum.
    private readonly List<double> _high = new();
    private readonly List<double> _low = new();

    public double Preview(double value, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        var (endHigh, endLow) = Extend(value);
        return Window(endHigh, endLow, _high.Count - length);
    }

    public double Add(double value, int length)
    {
        var (endHigh, endLow) = Extend(value);
        _high.Add(endHigh);
        _low.Add(endLow);

        if (length <= 0)
        {
            return 0;
        }

        return Window(endHigh, endLow, _high.Count - length - 1);
    }

    public void Reset()
    {
        _high.Clear();
        _low.Clear();
    }

    private (double High, double Low) Extend(double value)
    {
        var last = _high.Count - 1;
        return CompensatedSum.Add(last >= 0 ? _high[last] : 0, last >= 0 ? _low[last] : 0, value);
    }

    private double Window(double endHigh, double endLow, int startIndex) => startIndex >= 0
        ? CompensatedSum.Difference(endHigh, endLow, _high[startIndex], _low[startIndex])
        : endHigh + endLow;
}

internal sealed class RollingWindowCorrelation : IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _x;
    private readonly PooledRingBuffer<double> _y;
    private readonly double[] _xWindow;
    private readonly double[] _yWindow;

    public RollingWindowCorrelation(int length)
    {
        _length = Math.Max(1, length);
        _x = new PooledRingBuffer<double>(_length);
        _y = new PooledRingBuffer<double>(_length);
        _xWindow = new double[_length];
        _yWindow = new double[_length];
    }

    public double Preview(double x, double y, out int countAfter)
    {
        // A preview replaces the oldest pair only if the window is already full.
        var first = _x.Count == _length ? 1 : 0;
        var n = 0;
        for (var i = first; i < _x.Count; i++, n++)
        {
            _xWindow[n] = _x[i];
            _yWindow[n] = _y[i];
        }

        _xWindow[n] = x;
        _yWindow[n] = y;
        countAfter = n + 1;
        return WindowCorrelation.Pearson(new ReadOnlySpan<double>(_xWindow, 0, countAfter), new ReadOnlySpan<double>(_yWindow, 0, countAfter));
    }

    public double Add(double x, double y, out int countAfter)
    {
        _x.TryAdd(x, out _);
        _y.TryAdd(y, out _);
        countAfter = _x.Count;
        for (var i = 0; i < countAfter; i++)
        {
            _xWindow[i] = _x[i];
            _yWindow[i] = _y[i];
        }

        // The routine and window order of batch RollingCorrelation, so the two engines agree to the last bit.
        return WindowCorrelation.Pearson(new ReadOnlySpan<double>(_xWindow, 0, countAfter), new ReadOnlySpan<double>(_yWindow, 0, countAfter));
    }

    public void Reset()
    {
        _x.Clear();
        _y.Clear();
    }

    public void Dispose()
    {
        _x.Dispose();
        _y.Dispose();
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

internal sealed class WmaState : IDisposable
{
    private readonly int _length;
    private readonly long _denominator;
    private readonly PooledRingBuffer<double> _window;
    private ExactMeanAccumulator _sum;
    private ExactMeanAccumulator _numerator;

    public WmaState(int length)
    {
        _length = Math.Max(1, length);
        _denominator = (long)_length * (_length + 1L) / 2;
        _window = new PooledRingBuffer<double>(_length);
    }

    public double GetNext(double value, bool commit)
    {
        // Each retained observation loses one unit of weight; the new one gets N.
        // Missing startup observations have zero weight contributions, as in the batch formula.
        var numerator = _numerator;
        numerator.Subtract(_sum);
        numerator.Add(value, _length);
        var result = numerator.Mean(_denominator);
        if (commit)
        {
            var sum = _sum;
            sum.Add(value);
            if (_window.Count == _length) sum.Add(_window[0], -1);
            _window.TryAdd(value, out _);
            _sum = sum;
            _numerator = numerator;
        }
        return result;
    }

    public void Reset()
    {
        _window.Clear();
        _sum = default;
        _numerator = default;
    }

    public void Dispose() => _window.Dispose();
}

internal sealed class EmaState
{
    private readonly int _length;
    private int _count;
    private ExactMeanAccumulator _sum;
    private double _prevEma;

    public EmaState(int length)
    {
        _length = Math.Max(1, length);
    }

    public double GetNext(double value, bool commit)
    {
        StreamingInputValidation.Finite(value, nameof(value));
        if (_count < _length)
        {
            var sum = _sum;
            sum.Add(value);
            var ema = sum.Mean(_count + 1);
            if (commit)
            {
                _sum = sum;
                _count++;
                _prevEma = ema;
            }

            return ema;
        }

        // Round the complete convex combination once. Separate products can erase
        // subnormals and cancellation residues even when the final value is representable.
        var updated = value;
        if (_length != 1 && value != _prevEma) // NOSONAR: S1244 - The recurrence distinguishes an unchanged state from any representable change.
        {
            var weighted = new ExactMeanAccumulator();
            weighted.Add(value, 2);
            weighted.Add(_prevEma, _length - 1);
            updated = weighted.Mean((long)_length + 1);
        }
        if (commit)
        {
            _prevEma = updated;
            // The count is only needed during initialization; saturate to avoid overflow.
        }

        return updated;
    }

    public void Reset()
    {
        _count = 0;
        _sum = default;
        _prevEma = 0;
    }
}

internal sealed class WilderState
{
    private readonly int _length;
    private double _prev;

    public WilderState(int length)
    {
        _length = Math.Max(1, length);
    }

    public double GetNext(double value, bool commit)
    {
        StreamingInputValidation.Finite(value, nameof(value));
        var wwma = RoundedWilder.Next(value, _prev, _length);
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
        _window = new RollingWindowSum(_length, trackMeanRoundoff: true);
    }

    public double Next(double value, bool isFinal)
    {
        if (_length == 1) return value;
        int countAfter;
        var mean = _window.Average(value, isFinal, out countAfter);
        return countAfter >= _length ? mean : 0;
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

internal sealed class ExactSimpleMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private double _sum;

    public ExactSimpleMovingAverageSmoother(int length)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
    }

    public double Next(double value, bool isFinal)
    {
        if (_length == 1) return value;
        int countAfter;
        var sum = isFinal ? Add(value, out countAfter) : Preview(value, out countAfter);
        return countAfter >= _length ? sum / _length : 0;
    }

    private double Preview(double value, out int countAfter)
    {
        if (_window.Count < _window.Capacity)
        {
            countAfter = _window.Count + 1;
            return _sum + value;
        }

        countAfter = _window.Capacity;
        return _sum + value - _window[0];
    }

    private double Add(double value, out int countAfter)
    {
        if (_window.TryAdd(value, out var removed))
        {
            _sum += value;
            _sum -= removed;
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

internal sealed class DoubleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;

    public DoubleExponentialMovingAverageSmoother(int length)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        return ExponentialExtrapolation.Double(ema1, ema2);
    }

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class ZeroLagExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;

    public ZeroLagExponentialMovingAverageSmoother(int length)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        return ExponentialExtrapolation.Double(ema1, ema2);
    }

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class TripleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EmaState _ema1;
    private readonly EmaState _ema2;
    private readonly EmaState _ema3;

    public TripleExponentialMovingAverageSmoother(int length)
    {
        _ema1 = new EmaState(length);
        _ema2 = new EmaState(length);
        _ema3 = new EmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        var ema1 = _ema1.GetNext(value, isFinal);
        var ema2 = _ema2.GetNext(ema1, isFinal);
        var ema3 = _ema3.GetNext(ema2, isFinal);
        return ExponentialExtrapolation.Triple(ema1, ema2, ema3);
    }

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
    }

    public void Dispose()
    {
    }
}

internal sealed class EhlersZeroLagMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly EhlersZeroLagWindow _window;
    public EhlersZeroLagMovingAverageSmoother(int length) => _window = new(MovingAvgType.ExponentialMovingAverage, length);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class ZeroLowLagMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly ZeroLowLagWindow _window;
    public ZeroLowLagMovingAverageSmoother(int length) => _window = new(length, 1.4);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class ZeroLagTripleExponentialMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly ZeroLagTripleWindow _window;
    public ZeroLagTripleExponentialMovingAverageSmoother(int length) => _window = new(MovingAvgType.TripleExponentialMovingAverage, length);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class McNichollMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly McNichollWindow _window;
    public McNichollMovingAverageSmoother(int length) => _window = new(MovingAvgType.ExponentialMovingAverage, length);
    public double Next(double value, bool isFinal) => _window.Next(value, isFinal);
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class WeightedMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly WmaState _wma;

    public WeightedMovingAverageSmoother(int length)
    {
        _wma = new WmaState(length);
    }

    public double Next(double value, bool isFinal)
    {
        return _wma.GetNext(value, isFinal);
    }

    public void Reset()
    {
        _wma.Reset();
    }

    public void Dispose()
    {
        _wma.Dispose();
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

internal sealed class EhlersHannMovingAverageSmoother : IMovingAverageSmoother, IDisposable
{
    private readonly HannWindowMean _mean;
    public EhlersHannMovingAverageSmoother(int length) => _mean = new HannWindowMean(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class SymmetricallyWeightedMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly SymmetricWindowMean _mean;
    public SymmetricallyWeightedMovingAverageSmoother(int length) => _mean = new SymmetricWindowMean(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

/// <summary>
/// Linear regression smoother that matches MovingAverageCore.LinearRegression behavior.
/// Uses actual sample count n = Min(index+1, length) instead of always using length.
/// </summary>
internal sealed class LinearRegressionCoreSmoother : IMovingAverageSmoother
{
    private readonly ExactLinearFitWindow _regression;

    public LinearRegressionCoreSmoother(int length)
    {
        _regression = new ExactLinearFitWindow(length);
    }

    // The fit of MovingAverageCore.LinearRegression: the same class, fed the same values.
    public double Next(double value, bool isFinal) => _regression.Next(value, isFinal).Last;

    public void Reset() => _regression.Reset();

    public void Dispose() => _regression.Dispose();
}

internal sealed class FibonacciWeightedMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly FibonacciWindowMean _mean;
    public FibonacciWeightedMovingAverageSmoother(int length) => _mean = new FibonacciWindowMean(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class SquareRootWeightedMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly SquareRootWindowMean _mean;
    public SquareRootWeightedMovingAverageSmoother(int length) => _mean = new SquareRootWindowMean(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class IntegerPowerMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly IntegerPowerWindowMean _mean;
    public IntegerPowerMovingAverageSmoother(int length, int power) => _mean = new IntegerPowerWindowMean(length, power);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class QuickMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly QuickWindowMean _mean;
    public QuickMovingAverageSmoother(int length) => _mean = new QuickWindowMean(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class JsaMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly PooledRingBuffer<double> _values;
    public JsaMovingAverageSmoother(int length) => _values = new PooledRingBuffer<double>(Math.Max(1, length));
    public double Next(double value, bool isFinal)
    {
        var prior = _values.Count == _values.Capacity ? _values[0] : 0;
        var result = PriceMean.Of(value, prior);
        if (isFinal) _values.TryAdd(value, out _);
        return result;
    }
    public void Reset() => _values.Clear();
    public void Dispose() => _values.Dispose();
}

internal sealed class RootMeanSquareSmoother : IMovingAverageSmoother
{
    private readonly RollingRootMeanSquare _mean;
    public RootMeanSquareSmoother(int length) => _mean = new RollingRootMeanSquare(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal);
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal sealed class KaufmanMovingAverageSmoother : IMovingAverageSmoother
{
    private readonly RoundedKaufmanWindow _mean;
    public KaufmanMovingAverageSmoother(int length) => _mean = new RoundedKaufmanWindow(length);
    public double Next(double value, bool isFinal) => _mean.Next(value, isFinal).Average;
    public void Reset() => _mean.Reset();
    public void Dispose() => _mean.Dispose();
}

internal static class MovingAverageSmootherFactory
{
    public static IMovingAverageSmoother Create(MovingAvgType maType, int length)
    {
        return maType switch
        {
            MovingAvgType.SimpleMovingAverage => new SimpleMovingAverageSmoother(length),
            MovingAvgType.ExponentialMovingAverage => new ExponentialMovingAverageSmoother(length),
            MovingAvgType.DoubleExponentialMovingAverage => new DoubleExponentialMovingAverageSmoother(length),
            MovingAvgType.ZeroLagExponentialMovingAverage => new ZeroLagExponentialMovingAverageSmoother(length),
            MovingAvgType.TripleExponentialMovingAverage => new TripleExponentialMovingAverageSmoother(length),
            MovingAvgType.ZeroLagTripleExponentialMovingAverage => new ZeroLagTripleExponentialMovingAverageSmoother(length),
            MovingAvgType.ZeroLowLagMovingAverage => new ZeroLowLagMovingAverageSmoother(length),
            MovingAvgType.EhlersZeroLagExponentialMovingAverage => new EhlersZeroLagMovingAverageSmoother(length),
            MovingAvgType.McNichollMovingAverage => new McNichollMovingAverageSmoother(length),
            MovingAvgType.WeightedMovingAverage => new WeightedMovingAverageSmoother(length),
            MovingAvgType.WildersSmoothingMethod => new WilderMovingAverageSmoother(length),
            MovingAvgType.EhlersHannMovingAverage => new EhlersHannMovingAverageSmoother(length),
            MovingAvgType.EhlersHammingMovingAverage => new EhlersHammingMovingAverageSmoother(length),
            MovingAvgType.Ehlers2PoleSuperSmootherFilterV1 => new Ehlers2PoleSuperSmootherFilterV1Smoother(length),
            MovingAvgType.Ehlers2PoleSuperSmootherFilterV2 => new Ehlers2PoleSuperSmootherFilterV2Smoother(length),
            MovingAvgType.EhlersTriangleMovingAverage => new EhlersTriangleMovingAverageSmoother(length),
            MovingAvgType.EhlersModifiedOptimumEllipticFilter => new EhlersModifiedOptimumEllipticFilterSmoother(length),
            MovingAvgType.SymmetricallyWeightedMovingAverage => new SymmetricallyWeightedMovingAverageSmoother(length),
            MovingAvgType.LinearRegression => new LinearRegressionCoreSmoother(length),
            MovingAvgType.FibonacciWeightedMovingAverage => new FibonacciWeightedMovingAverageSmoother(length),
            MovingAvgType.SquareRootWeightedMovingAverage => new SquareRootWeightedMovingAverageSmoother(length),
            MovingAvgType.ParabolicWeightedMovingAverage => new IntegerPowerMovingAverageSmoother(length, 2),
            MovingAvgType.CubedWeightedMovingAverage => new IntegerPowerMovingAverageSmoother(length, 3),
            MovingAvgType.QuickMovingAverage => new QuickMovingAverageSmoother(length),
            MovingAvgType.JsaMovingAverage => new JsaMovingAverageSmoother(length),
            MovingAvgType.QuadraticMovingAverage => new RootMeanSquareSmoother(length),
            MovingAvgType.KaufmanAdaptiveMovingAverage => new KaufmanMovingAverageSmoother(length),
            MovingAvgType.SineWeightedMovingAverage => new SineMovingAverageSmoother(length),
            MovingAvgType.ArnaudLegouxMovingAverage => new AlmaMovingAverageSmoother(length),
            MovingAvgType.NaturalMovingAverage => new NaturalMovingAverageSmoother(length),
            MovingAvgType.VariableIndexDynamicAverage => new VariableIndexDynamicAverageEngine(length),
            _ => throw new NotSupportedException($"MovingAvgType {maType} is not supported in streaming stateful indicators.")
        };
    }
}

internal sealed class EfficiencyRatioState : IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _volatilitySum;
    private readonly PooledRingBuffer<double> _values;
    private double _prevValue;
    private bool _hasPrev;

    public EfficiencyRatioState(int length)
    {
        _length = Math.Max(1, length);
        _volatilitySum = new RollingWindowSum(_length);
        _values = new PooledRingBuffer<double>(_length + 1);
    }

    public double Next(double value, bool isFinal)
    {
        var prevValue = _hasPrev ? _prevValue : 0;
        var volatility = _hasPrev ? Math.Abs(value - prevValue) : 0;
        var volatilitySum = isFinal ? _volatilitySum.Add(volatility, out _) : _volatilitySum.Preview(volatility, out _);
        var priorIndex = _values.Count - _length;
        var momentum = priorIndex >= 0 ? Math.Abs(value - _values[priorIndex]) : 0;
        var er = volatilitySum != 0 ? momentum / volatilitySum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        return er;
    }

    public void Reset()
    {
        _volatilitySum.Reset();
        _values.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _volatilitySum.Dispose();
        _values.Dispose();
    }
}

internal sealed class AdaptiveAutonomousRecursiveMovingAverageEngine : IDisposable
{
    private readonly AdaptiveAutonomousWindow _window;
    public AdaptiveAutonomousRecursiveMovingAverageEngine(int length, double gamma) => _window = new(length, gamma);
    public double Next(double value, bool isFinal, out double d) { var result = _window.Next(value, isFinal); d = result.Deviation; return result.Average; }
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
}

internal sealed class RsiState : IDisposable
{
    private readonly PriceRsiWindow? _wide;
    private readonly IMovingAverageSmoother _avgGain;
    private readonly IMovingAverageSmoother _avgLoss;
    private double _prevValue;
    private bool _hasPrev;
    private readonly bool _preserveFlatRatio;
    private double _previousRsi;

    public RsiState(MovingAvgType maType, int length)
    {
        if (StrengthWindow.Supports(maType))
        {
            _wide = new PriceRsiWindow(maType, length);
        }
        var resolved = Math.Max(1, length);
        _preserveFlatRatio = resolved > 1 && (maType == MovingAvgType.WildersSmoothingMethod || maType == MovingAvgType.ExponentialMovingAverage);
        _avgGain = MovingAverageSmootherFactory.Create(maType, resolved);
        _avgLoss = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public double Next(double value, bool isFinal)
    {
        if (_wide is not null) return _wide.Next(value, isFinal);
        var prevValue = _hasPrev ? _prevValue : 0;
        var priceChg = _hasPrev ? value - prevValue : 0;
        var gain = priceChg > 0 ? priceChg : 0;
        var loss = priceChg < 0 ? Math.Abs(priceChg) : 0;

        var avgGain = _avgGain.Next(gain, isFinal);
        var avgLoss = _avgLoss.Next(loss, isFinal);
        var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
        var rsi = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : MathHelper.MinOrMax(100 - (100 / (1 + rs)), 100, 0);
        if (_preserveFlatRatio && _hasPrev && priceChg == 0) rsi = _previousRsi;

        if (isFinal)
        {
            _prevValue = value;
            _previousRsi = rsi;
            _hasPrev = true;
        }

        return rsi;
    }

    public void Reset()
    {
        _wide?.Reset();
        _avgGain.Reset();
        _avgLoss.Reset();
        _prevValue = 0;
        _hasPrev = false;
        _previousRsi = 0;
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _avgGain.Dispose();
        _avgLoss.Dispose();
    }
}

internal sealed class RollingPercentRank : IDisposable
{
    private readonly int _length;
    private readonly bool _useLinear;
    private readonly PooledRingBuffer<double> _window;
    private OrderStatisticTree _tree = new();
    private bool _disposed;

    public RollingPercentRank(int length)
    {
        _length = Math.Max(1, length);
        _useLinear = _length <= RollingWindowSettings.SmallWindowThreshold;
        _window = new PooledRingBuffer<double>(_length);
    }

    public double Add(double value)
    {
        var count = AddAndCountLessThanOrEqual(value);
        return MathHelper.MinOrMax((double)count / _length * 100, 100, 0);
    }

    /// <summary>The rank <see cref="Add"/> would return for the value, without adding it.</summary>
    /// <remarks>
    /// Add ranks the value against the window it leaves behind, which no longer holds the oldest value once
    /// the window is full. Counting the oldest here made a preview disagree with its own commit whenever
    /// the value about to leave was at or below the new one.
    /// </remarks>
    public double Preview(double value)
    {
        var count = CountLessThanOrEqual(value);
        if (_window.Count == _length && _window[0].CompareTo(value) <= 0)
        {
            count--;
        }

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
                if (_window[i].CompareTo(value) <= 0)
                {
                    count++;
                }
            }

            return Math.Max(0, count - 1);
        }

        if (_window.TryAdd(value, out var removed))
        {
            _tree.Remove(removed);
        }

        _tree.Insert(value);
        return Math.Max(0, _tree.CountLessThanOrEqual(value) - 1);
    }

    private int CountLessThanOrEqual(double value)
    {
        if (_useLinear)
        {
            var count = 0;
            for (var i = 0; i < _window.Count; i++)
            {
                if (_window[i].CompareTo(value) <= 0)
                {
                    count++;
                }
            }

            return count;
        }

        return _tree.CountLessThanOrEqual(value);
    }
}
