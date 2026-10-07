#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Vfi")]
public sealed class VolumeFlowIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VolumeFlowWindow? _safe;
    private bool _selected;
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _coef;
    private readonly double _vcoef;
    private readonly RollingWindowSum _vcpSum = null!;

    // The deviation of the window about its own mean, matching the batch calculation; see #190.
    private readonly RollingStandardDeviation _vinter = null!;
    private readonly IMovingAverageSmoother _volumeMa = null!;
    private readonly IMovingAverageSmoother _vfiMa = null!;
    private readonly IMovingAverageSmoother _signalMa = null!;
    private StreamingInputResolver _input;
    private double _prevValue;
    private double _prevVave;
    private bool _hasPrev;

    public VolumeFlowIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 130, int length2 = 30,
        int signalLength = 5, int smoothLength = 3, double coef = 0.2, double vcoef = 2.5)
    {
        _safe=StrengthWindow.Supports(maType)?new(maType,length1,length2,signalLength,smoothLength,coef,vcoef):null;
        if(_safe is not null)return;
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _coef = coef;
        _vcoef = vcoef;
        _vcpSum = new RollingWindowSum(_length1);
        // No moving-average type, and no selector: the log return is passed to Next directly.
        _vinter = new RollingStandardDeviation(_length2);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length1);
        _vfiMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _signalMa = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.TypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.VolumeFlowIndicator;

    void ICustomInputConsumer.ReadCloseAsInput()
    { _selected=true;_input = new StreamingInputResolver(InputName.Close, null); }

    public void Reset()
    {
        if(_safe is not null){_safe.Reset();return;}
        _vcpSum.Reset();
        _vinter.Reset();
        _volumeMa.Reset();
        _vfiMa.Reset();
        _signalMa.Reset();
        _prevValue = 0;
        _prevVave = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        if(_safe is not null)
        {
            StreamingInputValidation.Validate(bar);var typical=new ExactMeanAccumulator();typical.Add(bar.High);typical.Add(bar.Low);typical.Add(bar.Close);
            var point=_safe.Next(_selected?bar.Close:typical.Mean(3),bar.Close,bar.Volume,isFinal);
            return new(point.Line,includeOutputs?new Dictionary<string,double>{{"Vfi",point.Line},{"Signal",point.Signal},{"Histogram",point.Histogram}}:null);
        }
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var inter = value > 0 && prevValue > 0 ? Math.Log(value) - Math.Log(prevValue) : 0;

        // Fed the log return, which is the series this measures. The first bar has no prior value and is
        // defined as 0 here and in the batch calculation alike, so both engines hold the same window.
        var vinter = _vinter.Next(inter, isFinal);
        var vave = _volumeMa.Next(bar.Volume, isFinal);
        var prevVave = _hasPrev ? _prevVave : 0;
        var cutoff = bar.Close * vinter * _coef;
        var vmax = prevVave * _vcoef;
        var vc = Math.Min(bar.Volume, vmax);
        var mf = _hasPrev ? value - prevValue : 0;
        var vcp = mf > cutoff ? vc : mf < -cutoff ? -vc : 0;
        var vcpSum = isFinal ? _vcpSum.Add(vcp, out _) : _vcpSum.Preview(vcp, out _);
        var vcpVaveSum = vave != 0 ? vcpSum / vave : 0;
        var vfi = _vfiMa.Next(vcpVaveSum, isFinal);
        var signal = _signalMa.Next(vfi, isFinal);
        var histogram = vfi - signal;

        if (isFinal)
        {
            _prevValue = value;
            _prevVave = vave;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Vfi", vfi },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(vfi, outputs);
    }

    public void Dispose()
    {
        if(_safe is not null){_safe.Dispose();return;}
        _vcpSum.Dispose();
        _vinter.Dispose();
        _volumeMa.Dispose();
        _vfiMa.Dispose();
        _signalMa.Dispose();
    }
}

[PrimaryOutput("Vpni")]
public sealed class VolumePositiveNegativeIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VolumePositiveNegativeWindow _window;
    private bool _selected;
    public VolumePositiveNegativeIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 30, int smoothLength = 3)
        => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.VolumePositiveNegativeIndicator;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal, _selected);
        return new(point.Line, includeOutputs ? new Dictionary<string, double> { ["Vpni"] = point.Line, ["Signal"] = point.Signal } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vpci")]
public sealed class VolumePriceConfirmationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VpciWindow _window;
    public VolumePriceConfirmationIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 20, int length = 8) => _window = new(maType, fastLength, slowLength, length);
    public IndicatorName Name => IndicatorName.VolumePriceConfirmationIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double>
            { ["Vpci"] = point.Line, ["Signal"] = point.SignalLine } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vwap")]
public sealed class VolumeWeightedAveragePriceState : IStreamingIndicatorState, ICustomInputConsumer
{
    private StreamingInputResolver _input;
    private ExactVolumeMean _mean;
    private bool _customInput;

    public VolumeWeightedAveragePriceState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeWeightedAveragePrice;

    void ICustomInputConsumer.ReadCloseAsInput() => _customInput = true;

    public void Reset()
    {
        _mean = default;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var next = _mean;
        if (_customInput) next.Add(value, bar.Volume);
        else next.AddTypical(bar.High, bar.Low, bar.Close, bar.Volume);
        var vwap = next.Value();
        if (isFinal) _mean = next;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vwap", vwap }
            };
        }

        return new StreamingIndicatorStateResult(vwap, outputs);
    }
}

[PrimaryOutput("Vwma")]
public sealed class VolumeWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeWeightedWindow _window;
    public VolumeWeightedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.VolumeWeightedMovingAverage;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Vwma", value } } : null);
    }
}

[PrimaryOutput("Vwrsi")]
public sealed class VolumeWeightedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeWeightedRsiWindow _window;
    public VolumeWeightedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int smoothLength = 3) => _window = new(maType, length, smoothLength);
    public IndicatorName Name => IndicatorName.VolumeWeightedRelativeStrengthIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { ["Vwrsi"] = value } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("UpperBand")]
public sealed class VortexBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly VortexBandWindow _window;
    public VortexBandsState(MovingAvgType maType = MovingAvgType.McNichollMovingAverage, int length = 20) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.VortexBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Upper, includeOutputs ? new Dictionary<string, double>
            { ["UpperBand"] = point.Upper, ["MiddleBand"] = point.Middle, ["LowerBand"] = point.Lower } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vi")]
public sealed class VostroIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly double _level;
    private readonly RollingWindowSum _tempSum;
    private readonly RollingWindowSum _rangeSum;
    private readonly IMovingAverageSmoother _wma;
    private readonly StreamingInputResolver _input;
    private double _prevIBuff116;
    private double _prevIBuff112;

    public VostroIndicatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 5,
        int length2 = 100, double level = 8)
    {
        _length1 = Math.Max(1, length1);
        _level = level;
        _tempSum = new RollingWindowSum(_length1);
        _rangeSum = new RollingWindowSum(_length1);
        _wma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VostroIndicator;

    public void Reset()
    {
        _tempSum.Reset();
        _rangeSum.Reset();
        _wma.Reset();
        _prevIBuff116 = 0;
        _prevIBuff112 = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var wma = _wma.Next(value, isFinal);

        var sumMedian = isFinal ? _tempSum.Add(value, out _) : _tempSum.Preview(value, out _);
        var range = bar.High - bar.Low;
        var sumRange = isFinal ? _rangeSum.Add(range, out _) : _rangeSum.Preview(range, out _);

        var gd120 = sumMedian;
        var gd128 = gd120 * 0.2;
        var gd121 = sumRange;
        var gd136 = gd121 * 0.2 * 0.2;

        var iBuff116 = gd136 != 0 ? (bar.Low - gd128) / gd136 : 0;
        var iBuff112 = gd136 != 0 ? (bar.High - gd128) / gd136 : 0;

        double iBuff108 = iBuff112 > _level && bar.High > wma ? 90 : iBuff116 < -_level && bar.Low < wma ? -90 : 0;
        var iBuff109 = (iBuff112 > _level && _prevIBuff112 > _level) ||
            (iBuff116 < -_level && _prevIBuff116 < -_level)
            ? 0
            : iBuff108;

        if (isFinal)
        {
            _prevIBuff116 = iBuff116;
            _prevIBuff112 = iBuff112;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vi", iBuff109 }
            };
        }

        return new StreamingIndicatorStateResult(iBuff109, outputs);
    }

    public void Dispose()
    {
        _tempSum.Dispose();
        _rangeSum.Dispose();
        _wma.Dispose();
    }
}

[PrimaryOutput("T1")]
public sealed class WaddahAttarExplosionState : IStreamingIndicatorState, IDisposable
{
    private readonly WaddahWindow _window;
    public WaddahAttarExplosionState(int fastLength = 20, int slowLength = 40, double sensitivity = 150)
        => _window = new(fastLength, slowLength, sensitivity);
    public IndicatorName Name => IndicatorName.WaddahAttarExplosion;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.T1, includeOutputs ? new Dictionary<string, double>
            { ["T1"] = point.T1, ["T2"] = point.T2, ["E1"] = point.E1, ["TrendUp"] = point.Up, ["TrendDn"] = point.Down } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Wami")]
public sealed class WamiOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly WamiWindow? _wide;
    private readonly IMovingAverageSmoother _diffWma;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public WamiOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 13,
        int length2 = 4)
    {
        if (StrengthWindow.Supports(maType)) _wide = new WamiWindow(maType, length1, length2);
        _diffWma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length2));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WamiOscillator;

    public void Reset()
    {
        _wide?.Reset();
        _diffWma.Reset();
        _ema1.Reset();
        _ema2.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double wami;
        if (_wide is not null) wami = _wide.Next(value, isFinal);
        else
        {
            var prevValue = _hasPrev ? _prevValue : 0;
            var diff = _hasPrev ? value - prevValue : 0;

            var wma = _diffWma.Next(diff, isFinal);
            var ema1 = _ema1.Next(wma, isFinal);
            wami = _ema2.Next(ema1, isFinal);

        }

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
                { "Wami", wami }
            };
        }

        return new StreamingIndicatorStateResult(wami, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _diffWma.Dispose();
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

[PrimaryOutput("Wto")]
public sealed class WaveTrendOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly WaveTrendWindow _window;
    private bool _selected;
    public WaveTrendOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 21, int smoothLength = 4)
        => _window = new(maType, length1, length2, smoothLength);
    public IndicatorName Name => IndicatorName.WaveTrendOscillator;
    void ICustomInputConsumer.ReadCloseAsInput() => _selected = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var price = _selected ? MacZWindow.Number.Of(bar.Close) : WaveTrendWindow.Price(bar.Open, bar.High, bar.Low, bar.Close);
        var point = _window.Next(price, isFinal);
        return new StreamingIndicatorStateResult(point.Line, includeOutputs ? new Dictionary<string, double>
            { ["Wto"] = point.Line, ["Signal"] = point.SignalLine } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Wws")]
public sealed class WellesWilderSummationState : IStreamingIndicatorState
{
    private readonly WilderSummationWindow _window;
    public WellesWilderSummationState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.WellesWilderSummation;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Wws", value } } : null);
    }
}

[PrimaryOutput("Wwvs")]
public sealed class WellesWilderVolatilitySystemState : IStreamingIndicatorState, IDisposable
{
    private readonly WilderVolatilityWindow _window;
    public WellesWilderVolatilitySystemState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63, int length2 = 21, double factor = 3) => _window = new(maType, length1, length2, factor);
    public IndicatorName Name => IndicatorName.WellesWilderVolatilitySystem;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        return new(point.Value, includeOutputs ? new Dictionary<string, double> { { "Wwvs", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Wrma")]
public sealed class WellRoundedMovingAverageState : IStreamingIndicatorState
{
    private readonly WellRoundedWindow _window;
    public WellRoundedMovingAverageState(int length = 14) => _window = new(length);
    public IndicatorName Name => IndicatorName.WellRoundedMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Wrma", value } } : null);
    }
}

[PrimaryOutput("Wad")]
public sealed class WilliamsAccumulationDistributionState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly WilliamsAccumulationWindow _window = new();
    public IndicatorName Name => IndicatorName.WilliamsAccumulationDistribution;
    public void Reset() { _window.Reset();  }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var wide = _window.Next(bar.High, bar.Low, bar.Close, isFinal);
        var value = wide.Publish();
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Wad", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
}

[PrimaryOutput("UpFractal")]
public sealed class WilliamsFractalsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private int _index;

    public WilliamsFractalsState(int length = 2)
    {
        _length = Math.Max(2, length);
        _highs = new PooledRingBuffer<double>(_length + 8);
        _lows = new PooledRingBuffer<double>(_length + 8);
    }

    public IndicatorName Name => IndicatorName.WilliamsFractals;

    public void Reset()
    {
        _highs.Clear();
        _lows.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var older = Math.Min(6, _index - _length);
        double upFractal = 0, dnFractal = 0;
        if (older >= 2)
        {
            Span<double> highs = stackalloc double[9];
            Span<double> lows = stackalloc double[9];
            for (var j = 0; j < older + 3; j++)
            {
                var offset = _length + older - j;
                highs[j] = EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, offset);
                lows[j] = EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, offset);
            }
            upFractal = WilliamsFractalPattern.IsFractal(highs.Slice(0, older + 3), upper: true) ? 1 : 0;
            dnFractal = WilliamsFractalPattern.IsFractal(lows.Slice(0, older + 3), upper: false) ? 1 : 0;
        }

        if (isFinal)
        {
            _highs.TryAdd(bar.High, out _);
            _lows.TryAdd(bar.Low, out _);
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "UpFractal", upFractal },
                { "DnFractal", dnFractal }
            };
        }

        return new StreamingIndicatorStateResult(upFractal, outputs);
    }

    public void Dispose()
    {
        _highs.Dispose();
        _lows.Dispose();
    }
}

[PrimaryOutput("S1")]
public sealed class WilsonRelativePriceChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly WilsonWindow _window;
    public WilsonRelativePriceChannelState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 34,
        int smoothLength = 1, double overbought = 70, double oversold = 30, double upperNeutralZone = 55, double lowerNeutralZone = 45)
        => _window = new(maType, length, smoothLength, oversold, lowerNeutralZone, overbought, upperNeutralZone);
    public IndicatorName Name => IndicatorName.WilsonRelativePriceChannel;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Lines[0], includeOutputs ? new Dictionary<string, double>
            { ["S1"] = point.Lines[0], ["S2"] = point.Lines[1], ["U1"] = point.Lines[2], ["U2"] = point.Lines[3] } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Wvwma")]
public sealed class WindowedVolumeWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _prices;
    private readonly PooledRingBuffer<double> _volumes;
    private readonly StreamingInputResolver _input;

    public WindowedVolumeWeightedMovingAverageState(int length = 100)
    {
        _length = Math.Max(1, length);
        _prices = new PooledRingBuffer<double>(_length);
        _volumes = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WindowedVolumeWeightedMovingAverage;

    public void Reset()
    {
        _prices.Clear();
        _volumes.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mean = new ExactVolumeMean();
        for (var lag = 0; lag < _length && lag <= _prices.Count; lag++)
        {
            var price = lag == 0 ? value : _prices[_prices.Count - lag];
            var volume = lag == 0 ? bar.Volume : _volumes[_volumes.Count - lag];
            var taper = _length == 1 ? 1 : Math.Min(lag, _length - lag);
            mean.Add(price, volume, taper);
        }
        var result = mean.Value();
        if (isFinal)
        {
            _prices.TryAdd(value, out _);
            _volumes.TryAdd(bar.Volume, out _);
        }
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Wvwma", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose()
    {
        _prices.Dispose();
        _volumes.Dispose();
    }
}

[PrimaryOutput("FastCci")]
public sealed class WoodieCommodityChannelIndexState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly CommodityChannelIndexState _slowCci;
    private readonly CommodityChannelIndexState _fastCci;

    public WoodieCommodityChannelIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 6, int slowLength = 14)
    {
        _slowCci = new CommodityChannelIndexState(maType, Math.Max(1, slowLength), 0.015);
        _fastCci = new CommodityChannelIndexState(maType, Math.Max(1, fastLength), 0.015);
    }

    public IndicatorName Name => IndicatorName.WoodieCommodityChannelIndex;

    // No resolver of its own: the inner states that default to a typical or median price are the
    // ones that must switch to reading the close when this state is wrapped.
    void ICustomInputConsumer.ReadCloseAsInput()
    {
        ((ICustomInputConsumer)_slowCci).ReadCloseAsInput();
        ((ICustomInputConsumer)_fastCci).ReadCloseAsInput();
    }

    public void Reset()
    {
        _slowCci.Reset();
        _fastCci.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var slow = _slowCci.Update(bar, isFinal, includeOutputs: false).Value;
        var fast = _fastCci.Update(bar, isFinal, includeOutputs: false).Value;
        var histogram = fast - slow;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "FastCci", fast },
                { "SlowCci", slow },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(fast, outputs);
    }

    public void Dispose()
    {
        _slowCci.Dispose();
        _fastCci.Dispose();
    }
}

[PrimaryOutput("Pivot")]
public sealed class WoodiePivotPointsState : IStreamingIndicatorState, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly DailyPivotLevels _daily = new(false, woodie: true);
    public WoodiePivotPointsState() { }
    public IndicatorName Name => IndicatorName.WoodiePivotPoints;
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

[PrimaryOutput("Zscore")]
public sealed class ZDistanceFromVwapState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly ZDistanceWindow _window;
    public ZDistanceFromVwapState(MovingAvgType maType = MovingAvgType.VolumeWeightedAveragePrice, int length = 20)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.ZDistanceFromVwap;
    void ICustomInputConsumer.ReadCloseAsInput() { }
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { ["Zscore"] = value } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Zema")]
public sealed class ZeroLagExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;

    public ZeroLagExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        var resolvedType = maType == MovingAvgType.ZeroLagExponentialMovingAverage
            ? MovingAvgType.ExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ZeroLagExponentialMovingAverage;

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
        var zema = ExponentialExtrapolation.Double(ema1, ema2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zema", zema }
            };
        }

        return new StreamingIndicatorStateResult(zema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

[PrimaryOutput("Filter")]
public sealed class ZeroLagSmoothedCycleState : IStreamingIndicatorState, IDisposable
{
    private readonly ZeroLagCycleWindow _window;
    public ZeroLagSmoothedCycleState(int length = 100) => _window = new(length);
    public IndicatorName Name => IndicatorName.ZeroLagSmoothedCycle;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.Filter, includeOutputs ? new Dictionary<string, double> { { "Lco", point.Line }, { "Filter", point.Filter } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Ztema")]
public sealed class ZeroLagTripleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ZeroLagTripleWindow _window;
    public ZeroLagTripleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 14) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.ZeroLagTripleExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ztema", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Zllma")]
public sealed class ZeroLowLagMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ZeroLowLagWindow _window;
    public ZeroLowLagMovingAverageState(int length = 50, double lag = 1.4) => _window = new(length, lag);
    public IndicatorName Name => IndicatorName.ZeroLowLagMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Zllma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}
