#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Vfi")]
public sealed class VolumeFlowIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _coef;
    private readonly double _vcoef;
    private readonly RollingWindowSum _vcpSum;

    // The deviation of the window about its own mean, matching the batch calculation; see #190.
    private readonly RollingStandardDeviation _vinter;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _vfiMa;
    private readonly IMovingAverageSmoother _signalMa;
    private StreamingInputResolver _input;
    private double _prevValue;
    private double _prevVave;
    private bool _hasPrev;

    public VolumeFlowIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 130, int length2 = 30,
        int signalLength = 5, int smoothLength = 3, double coef = 0.2, double vcoef = 2.5)
    {
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

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
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
    private readonly int _length;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _atrMa;
    private readonly IMovingAverageSmoother _vpnSmooth;
    private readonly RollingWindowSum _vmpSum;
    private readonly RollingWindowSum _vmnSum;
    private StreamingInputResolver _input;
    private double _prevValue;
    private double _prevClose;
    private bool _hasPrev;

    public VolumePositiveNegativeIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 30, int smoothLength = 3)
    {
        _length = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length);
        _atrMa = MovingAverageSmootherFactory.Create(maType, _length);
        _vpnSmooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _vmpSum = new RollingWindowSum(_length);
        _vmnSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.TypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.VolumePositiveNegativeIndicator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _volumeMa.Reset();
        _atrMa.Reset();
        _vpnSmooth.Reset();
        _vmpSum.Reset();
        _vmnSum.Reset();
        _prevValue = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        // The first bar has no previous close, so its true range is its own high - low, as the batch ATR
        // measures it. A previous close of 0 made it the whole high and inflated the first window's ATR.
        var prevClose = _hasPrev ? _prevClose : bar.Close;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrMa.Next(tr, isFinal);
        var mav = _volumeMa.Next(bar.Volume, isFinal);
        mav = mav > 0 ? mav : 1;
        var mf = value - prevValue;
        var mc = 0.1 * atr;

        var vmp = mf > mc ? bar.Volume : 0;
        var vmn = mf < -mc ? bar.Volume : 0;
        var vp = isFinal ? _vmpSum.Add(vmp, out _) : _vmpSum.Preview(vmp, out _);
        var vn = isFinal ? _vmnSum.Add(vmn, out _) : _vmnSum.Preview(vmn, out _);

        var vpn = mav != 0 ? (vp - vn) / mav / _length * 100 : 0;
        var signal = _vpnSmooth.Next(vpn, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevClose = bar.Close;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vpni", vpn },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(vpn, outputs);
    }

    public void Dispose()
    {
        _volumeMa.Dispose();
        _atrMa.Dispose();
        _vpnSmooth.Dispose();
        _vmpSum.Dispose();
        _vmnSum.Dispose();
    }
}

[PrimaryOutput("Vpci")]
public sealed class VolumePriceConfirmationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _vwmaFastSum;
    private readonly RollingWindowSum _vwmaSlowSum;
    private readonly IMovingAverageSmoother _vwmaFastVolMa;
    private readonly IMovingAverageSmoother _vwmaSlowVolMa;
    private readonly IMovingAverageSmoother _volumeFastMa;
    private readonly IMovingAverageSmoother _volumeSlowMa;
    private readonly IMovingAverageSmoother _smaFast;
    private readonly IMovingAverageSmoother _smaSlow;
    private readonly IMovingAverageSmoother _vpciMa;
    private readonly StreamingInputResolver _input;

    public VolumePriceConfirmationIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 20, int length = 8)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        var resolved = Math.Max(1, length);
        _vwmaFastSum = new RollingWindowSum(resolvedFast);
        _vwmaSlowSum = new RollingWindowSum(resolvedSlow);
        _vwmaFastVolMa = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedFast);
        _vwmaSlowVolMa = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedSlow);
        _volumeFastMa = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _volumeSlowMa = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _smaFast = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _smaSlow = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _vpciMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumePriceConfirmationIndicator;

    public void Reset()
    {
        _vwmaFastSum.Reset();
        _vwmaSlowSum.Reset();
        _vwmaFastVolMa.Reset();
        _vwmaSlowVolMa.Reset();
        _volumeFastMa.Reset();
        _volumeSlowMa.Reset();
        _smaFast.Reset();
        _smaSlow.Reset();
        _vpciMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumePrice = value * volume;

        var vwmaFastVol = _vwmaFastVolMa.Next(volume, isFinal);
        var vwmaFastSum = isFinal ? _vwmaFastSum.Add(volumePrice, out var fastCount) : _vwmaFastSum.Preview(volumePrice, out fastCount);
        var vwmaFastAvg = fastCount > 0 ? vwmaFastSum / fastCount : 0;
        var vwmaFast = vwmaFastVol != 0 ? vwmaFastAvg / vwmaFastVol : 0;

        var vwmaSlowVol = _vwmaSlowVolMa.Next(volume, isFinal);
        var vwmaSlowSum = isFinal ? _vwmaSlowSum.Add(volumePrice, out var slowCount) : _vwmaSlowSum.Preview(volumePrice, out slowCount);
        var vwmaSlowAvg = slowCount > 0 ? vwmaSlowSum / slowCount : 0;
        var vwmaSlow = vwmaSlowVol != 0 ? vwmaSlowAvg / vwmaSlowVol : 0;

        var volumeSmaFast = _volumeFastMa.Next(volume, isFinal);
        var volumeSmaSlow = _volumeSlowMa.Next(volume, isFinal);
        var smaFast = _smaFast.Next(value, isFinal);
        var smaSlow = _smaSlow.Next(value, isFinal);

        var vpc = vwmaSlow - smaSlow;
        var vpr = smaFast != 0 ? vwmaFast / smaFast : 0;
        var vm = volumeSmaSlow != 0 ? volumeSmaFast / volumeSmaSlow : 0;
        var vpci = vpc * vpr * vm;
        var vpciSma = _vpciMa.Next(vpci, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Vpci", vpci },
                { "Signal", vpciSma }
            };
        }

        return new StreamingIndicatorStateResult(vpci, outputs);
    }

    public void Dispose()
    {
        _vwmaFastSum.Dispose();
        _vwmaSlowSum.Dispose();
        _vwmaFastVolMa.Dispose();
        _vwmaSlowVolMa.Dispose();
        _volumeFastMa.Dispose();
        _volumeSlowMa.Dispose();
        _smaFast.Dispose();
        _smaSlow.Dispose();
        _vpciMa.Dispose();
    }
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
    private readonly RollingVolumeMean? _exactMean;
    private readonly RollingWindowSum _volumePriceSum;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly StreamingInputResolver _input;

    public VolumeWeightedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var resolved = Math.Max(1, length);
        _exactMean = maType == MovingAvgType.SimpleMovingAverage ? new RollingVolumeMean(resolved) : null;
        _volumePriceSum = new RollingWindowSum(resolved);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeWeightedMovingAverage;

    public void Reset()
    {
        _exactMean?.Reset();
        _volumePriceSum.Reset();
        _volumeMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        double vwma;
        if (_exactMean is not null) vwma = _exactMean.Next(value, volume, isFinal);
        else
        {
            var volumePrice = value * volume;
            var volumePriceSum = isFinal ? _volumePriceSum.Add(volumePrice, out var count) : _volumePriceSum.Preview(volumePrice, out count);
            var volumePriceAvg = count > 0 ? volumePriceSum / count : 0;
            var volumeMa = _volumeMa.Next(volume, isFinal);
            vwma = volumeMa != 0 ? volumePriceAvg / volumeMa : 0;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vwma", vwma }
            };
        }

        return new StreamingIndicatorStateResult(vwma, outputs);
    }

    public void Dispose()
    {
        _exactMean?.Dispose();
        _volumePriceSum.Dispose();
        _volumeMa.Dispose();
    }
}

[PrimaryOutput("Vwrsi")]
public sealed class VolumeWeightedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _upMa;
    private readonly IMovingAverageSmoother _downMa;
    private readonly IMovingAverageSmoother _smoothMa;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VolumeWeightedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int smoothLength = 3)
    {
        var resolved = Math.Max(1, length);
        _upMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _downMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _smoothMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolumeWeightedRelativeStrengthIndex;

    public void Reset()
    {
        _upMa.Reset();
        _downMa.Reset();
        _smoothMa.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var max = Math.Max(diff * volume, 0);
        var min = -Math.Min(diff * volume, 0);

        var up = _upMa.Next(max, isFinal);
        var dn = _downMa.Next(min, isFinal);
        var rsiRaw = dn == 0 ? 100 : up == 0 ? 0 : 100 - (100 / (1 + (up / dn)));
        var rsiScale = (rsiRaw * 2) - 100;
        var rsi = _smoothMa.Next(rsiScale, isFinal);

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
                { "Vwrsi", rsi }
            };
        }

        return new StreamingIndicatorStateResult(rsi, outputs);
    }

    public void Dispose()
    {
        _upMa.Dispose();
        _downMa.Dispose();
        _smoothMa.Dispose();
    }
}

[PrimaryOutput("UpperBand")]
public sealed class VortexBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _basisMa;
    private readonly IMovingAverageSmoother _diffMa;
    private readonly StreamingInputResolver _input;

    public VortexBandsState(MovingAvgType maType = MovingAvgType.McNichollMovingAverage, int length = 20)
    {
        var resolved = Math.Max(1, length);
        _basisMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _diffMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VortexBands;

    public void Reset()
    {
        _basisMa.Reset();
        _diffMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisMa.Next(value, isFinal);
        // A band half-width is a distance; see the batch calculation for the full reasoning. Measured
        // signed it turns negative whenever price sits below the basis, inverting the two bands.
        var diff = Math.Abs(value - basis);
        var diffMa = _diffMa.Next(diff, isFinal);
        var dev = 2 * Math.Max(0, diffMa);
        var upper = basis + dev;
        var lower = basis - dev;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", basis },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(upper, outputs);
    }

    public void Dispose()
    {
        _basisMa.Dispose();
        _diffMa.Dispose();
    }
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
    private readonly double _sensitivity;
    private readonly MacdEngine _macd1;
    private readonly MacdEngine _macd2;
    private readonly MacdEngine _macd3;
    private readonly MacdEngine _macd4;
    private readonly RollingStandardDeviation _bbWindow;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public WaddahAttarExplosionState(int fastLength = 20, int slowLength = 40, double sensitivity = 150)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _sensitivity = sensitivity;
        _macd1 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd2 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd3 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd4 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _bbWindow = new RollingStandardDeviation(resolvedFast);
        _input = new StreamingInputResolver(InputName.Close, null);
        _values = new PooledRingBuffer<double>(3);
    }

    public IndicatorName Name => IndicatorName.WaddahAttarExplosion;

    public void Reset()
    {
        _macd1.Reset();
        _macd2.Reset();
        _macd3.Reset();
        _macd4.Reset();
        _bbWindow.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _bbWindow.Next(value, isFinal);

        var prev1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prev2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prev3 = EhlersStreamingWindow.GetOffsetValue(_values, value, 3);

        var macd1 = _macd1.Next(value, isFinal, out _, out _);
        var macd2 = _macd2.Next(prev1, isFinal, out _, out _);
        var macd3 = _macd3.Next(prev2, isFinal, out _, out _);
        var macd4 = _macd4.Next(prev3, isFinal, out _, out _);

        var t1 = (macd1 - macd2) * _sensitivity;
        var t2 = (macd3 - macd4) * _sensitivity;
        var e1 = 4 * stdDev;
        var trendUp = t1 >= 0 ? t1 : 0;
        var trendDn = t1 < 0 ? -t1 : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "T1", t1 },
                { "T2", t2 },
                { "E1", e1 },
                { "TrendUp", trendUp },
                { "TrendDn", trendDn }
            };
        }

        return new StreamingIndicatorStateResult(t1, outputs);
    }

    public void Dispose()
    {
        _macd1.Dispose();
        _macd2.Dispose();
        _macd3.Dispose();
        _macd4.Dispose();
        _bbWindow.Dispose();
        _values.Dispose();
    }
}

[PrimaryOutput("Wami")]
public sealed class WamiOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffWma;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public WamiOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 13,
        int length2 = 4)
    {
        _diffWma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length2));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WamiOscillator;

    public void Reset()
    {
        _diffWma.Reset();
        _ema1.Reset();
        _ema2.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;

        var wma = _diffWma.Next(diff, isFinal);
        var ema1 = _ema1.Next(wma, isFinal);
        var wami = _ema2.Next(ema1, isFinal);

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
        _diffWma.Dispose();
        _ema1.Dispose();
        _ema2.Dispose();
    }
}

[PrimaryOutput("Wto")]
public sealed class WaveTrendOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly IMovingAverageSmoother _esaMa;
    private readonly IMovingAverageSmoother _dMa;
    private readonly IMovingAverageSmoother _tciMa;
    private readonly IMovingAverageSmoother _wt2Ma;
    private StreamingInputResolver _input;
    private readonly SeededEmaResidual? _stableResidual;

    public WaveTrendOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 21,
        int smoothLength = 4)
    {
        _stableResidual = maType == MovingAvgType.ExponentialMovingAverage ? new SeededEmaResidual(length1) : null;
        _esaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _tciMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _wt2Ma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.WaveTrendOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _esaMa.Reset();
        _stableResidual?.Reset();
        _dMa.Reset();
        _tciMa.Reset();
        _wt2Ma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var ap = _input.GetValue(bar);
        var esa = _esaMa.Next(ap, isFinal);
        var residual = _stableResidual?.Next(ap, isFinal) ?? ap - esa;
        var absApEsa = Math.Abs(residual);
        var d = _dMa.Next(absApEsa, isFinal);
        var ci = d != 0 ? residual / (0.015 * d) : 0;
        var tci = _tciMa.Next(ci, isFinal);
        var wt2 = _wt2Ma.Next(tci, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Wto", tci },
                { "Signal", wt2 }
            };
        }

        return new StreamingIndicatorStateResult(tci, outputs);
    }

    public void Dispose()
    {
        _esaMa.Dispose();
        _dMa.Dispose();
        _tciMa.Dispose();
        _wt2Ma.Dispose();
    }
}

[PrimaryOutput("Wws")]
public sealed class WellesWilderSummationState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private double _prevSum;

    public WellesWilderSummationState(int length = 14)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WellesWilderSummation;

    public void Reset()
    {
        _prevSum = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sum = _prevSum - (_prevSum / _length) + value;

        if (isFinal)
        {
            _prevSum = sum;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wws", sum }
            };
        }

        return new StreamingIndicatorStateResult(sum, outputs);
    }
}

[PrimaryOutput("Wwvs")]
public sealed class WellesWilderVolatilitySystemState : IStreamingIndicatorState, IDisposable
{
    private readonly double _factor;
    private readonly IMovingAverageSmoother _atrMa;
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public WellesWilderVolatilitySystemState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63,
        int length2 = 21, double factor = 3)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _factor = factor;
        _atrMa = MovingAverageSmootherFactory.Create(maType, resolved2);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved1);
        _maxWindow = new RollingWindowMax(Math.Max(2, resolved2));
        _minWindow = new RollingWindowMin(Math.Max(2, resolved2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WellesWilderVolatilitySystem;

    public void Reset()
    {
        _atrMa.Reset();
        _ema.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        // For TrueRange on first bar, use current close to avoid inflated TR
        var prevClose = _hasPrev ? _prevClose : bar.Close;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevClose);
        var atr = _atrMa.Next(tr, isFinal);
        var ema = _ema.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(value, out _) : _maxWindow.Preview(value, out _);
        var lowest = isFinal ? _minWindow.Add(value, out _) : _minWindow.Preview(value, out _);
        var sic = value > ema ? highest : lowest;
        var vstop = value > ema ? sic - (_factor * atr) : sic + (_factor * atr);

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
                { "Wwvs", vstop }
            };
        }

        return new StreamingIndicatorStateResult(vstop, outputs);
    }

    public void Dispose()
    {
        _atrMa.Dispose();
        _ema.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
    }
}

[PrimaryOutput("Wrma")]
public sealed class WellRoundedMovingAverageState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly StreamingInputResolver _input;
    private double _prevA;
    private double _prevB;
    private double _prevY;
    private double _prevSrcY;
    private double _prevSrcEma;
    private double _prevYEma;
    private bool _hasPrev;

    public WellRoundedMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _alpha = 2d / (_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WellRoundedMovingAverage;

    public void Reset()
    {
        _prevA = 0;
        _prevB = 0;
        _prevY = 0;
        _prevSrcY = 0;
        _prevSrcEma = 0;
        _prevYEma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevSrcY = _hasPrev ? _prevSrcY : 0;
        var prevSrcEma = _hasPrev ? _prevSrcEma : 0;

        var a = _prevA + (_alpha * prevSrcY);
        var b = _prevB + (_alpha * prevSrcEma);
        var ab = a + b;
        var y = CalculationsHelper.CalculateEMA(ab, _prevY, 1);
        var srcY = value - y;
        var yEma = CalculationsHelper.CalculateEMA(y, _prevYEma, _length);
        var srcEma = value - yEma;

        if (isFinal)
        {
            _prevA = a;
            _prevB = b;
            _prevY = y;
            _prevSrcY = srcY;
            _prevSrcEma = srcEma;
            _prevYEma = yEma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wrma", y }
            };
        }

        return new StreamingIndicatorStateResult(y, outputs);
    }
}

[PrimaryOutput("Wad")]
public sealed class WilliamsAccumulationDistributionState : IStreamingIndicatorState
{
    private double _prevClose;
    private double _prevWad;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.WilliamsAccumulationDistribution;

    public void Reset()
    {
        _prevClose = 0;
        _prevWad = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var close = bar.Close;

        // The bar's contribution is measured against the true range high and low, and an unchanged close
        // contributes nothing without discarding the total. See CalculateWilliamsAccumulationDistribution.
        double wad;
        if (!_hasPrev)
        {
            wad = 0;
        }
        else
        {
            var trueRangeHigh = Math.Max(bar.High, _prevClose);
            var trueRangeLow = Math.Min(bar.Low, _prevClose);
            wad = close > _prevClose ? _prevWad + close - trueRangeLow
                : close < _prevClose ? _prevWad + close - trueRangeHigh
                : _prevWad;
        }

        if (isFinal)
        {
            _prevClose = close;
            _prevWad = wad;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wad", wad }
            };
        }

        return new StreamingIndicatorStateResult(wad, outputs);
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
    private readonly double _overbought;
    private readonly double _oversold;
    private readonly double _upperNeutralZone;
    private readonly double _lowerNeutralZone;
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _obSmooth;
    private readonly IMovingAverageSmoother _osSmooth;
    private readonly IMovingAverageSmoother _nzuSmooth;
    private readonly IMovingAverageSmoother _nzlSmooth;
    private readonly StreamingInputResolver _input;

    public WilsonRelativePriceChannelState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 34,
        int smoothLength = 1, double overbought = 70, double oversold = 30, double upperNeutralZone = 55,
        double lowerNeutralZone = 45)
    {
        var resolved = Math.Max(1, length);
        var smooth = Math.Max(1, smoothLength);
        _overbought = overbought;
        _oversold = oversold;
        _upperNeutralZone = upperNeutralZone;
        _lowerNeutralZone = lowerNeutralZone;
        _rsi = new RsiState(maType, resolved);
        _obSmooth = MovingAverageSmootherFactory.Create(maType, smooth);
        _osSmooth = MovingAverageSmootherFactory.Create(maType, smooth);
        _nzuSmooth = MovingAverageSmootherFactory.Create(maType, smooth);
        _nzlSmooth = MovingAverageSmootherFactory.Create(maType, smooth);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.WilsonRelativePriceChannel;

    public void Reset()
    {
        _rsi.Reset();
        _obSmooth.Reset();
        _osSmooth.Reset();
        _nzuSmooth.Reset();
        _nzlSmooth.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var ob = _obSmooth.Next(rsi - _overbought, isFinal);
        var os = _osSmooth.Next(rsi - _oversold, isFinal);
        var nzu = _nzuSmooth.Next(rsi - _upperNeutralZone, isFinal);
        var nzl = _nzlSmooth.Next(rsi - _lowerNeutralZone, isFinal);

        var s1 = value - (value * os / 100);
        var u1 = value - (value * ob / 100);
        var u2 = value - (value * nzu / 100);
        var s2 = value - (value * nzl / 100);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "S1", s1 },
                { "S2", s2 },
                { "U1", u1 },
                { "U2", u2 }
            };
        }

        return new StreamingIndicatorStateResult(s1, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _obSmooth.Dispose();
        _osSmooth.Dispose();
        _nzuSmooth.Dispose();
        _nzlSmooth.Dispose();
    }
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
public sealed class WoodiePivotPointsState : IStreamingIndicatorState
{
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.WoodiePivotPoints;

    public void Reset()
    {
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var range = prevHigh - prevLow;
        var pivot = (prevHigh + prevLow + (prevClose * 2)) / 4;
        var support1 = (pivot * 2) - prevHigh;
        var resistance1 = (pivot * 2) - prevLow;
        var support2 = pivot - range;
        var resistance2 = pivot + range;
        var support3 = prevLow - (2 * (prevHigh - pivot));
        var resistance3 = prevHigh + (2 * (pivot - prevLow));
        var support4 = support3 - range;
        var resistance4 = resistance3 + range;
        var midpoint1 = (support1 + support2) / 2;
        var midpoint2 = (pivot + support1) / 2;
        var midpoint3 = (resistance1 + pivot) / 2;
        var midpoint4 = (resistance1 + resistance2) / 2;

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
            outputs = new Dictionary<string, double>(13)
            {
                { "Pivot", pivot },
                { "S1", support1 },
                { "S2", support2 },
                { "S3", support3 },
                { "S4", support4 },
                { "R1", resistance1 },
                { "R2", resistance2 },
                { "R3", resistance3 },
                { "R4", resistance4 },
                { "M1", midpoint1 },
                { "M2", midpoint2 },
                { "M3", midpoint3 },
                { "M4", midpoint4 }
            };
        }

        return new StreamingIndicatorStateResult(pivot, outputs);
    }
}

[PrimaryOutput("Zscore")]
public sealed class ZDistanceFromVwapState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly IMovingAverageSmoother? _meanMa;
    private readonly RollingVolumeWeightedMean _vwapMean;
    private readonly RollingZScore _zScore;
    private StreamingInputResolver _input;

    public ZDistanceFromVwapState(MovingAvgType maType = MovingAvgType.VolumeWeightedAveragePrice, int length = 20)
        : this(maType, length, new StreamingInputResolver(InputName.Close, null))
    {
    }

    private ZDistanceFromVwapState(MovingAvgType maType, int length, StreamingInputResolver input)
    {
        var resolved = Math.Max(1, length);
        // LazyBear's calc_zvwap: a rolling volume-weighted mean for the VWAP type, the chosen average otherwise.
        _meanMa = maType == MovingAvgType.VolumeWeightedAveragePrice ? null : MovingAverageSmootherFactory.Create(maType, resolved);
        _vwapMean = new RollingVolumeWeightedMean(resolved);
        _zScore = new RollingZScore(resolved);
        _input = input;
    }

    public IndicatorName Name => IndicatorName.ZDistanceFromVwap;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _meanMa?.Reset();
        _vwapMean.Reset();
        _zScore.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var mean = _meanMa is null ? _vwapMean.Next(value, bar.Volume, isFinal) : _meanMa.Next(value, isFinal);
        var zscore = _zScore.Next(value, mean, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zscore", zscore }
            };
        }

        return new StreamingIndicatorStateResult(zscore, outputs);
    }

    public void Dispose()
    {
        _meanMa?.Dispose();
        _vwapMean.Dispose();
        _zScore.Dispose();
    }
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
        var zema = ema1 + (ema1 - ema2);

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
    private readonly int _length;
    private readonly int _length1;
    private readonly LinearRegressionState _linreg;
    private readonly LinearRegressionState _linregAx1;
    private readonly LinearRegressionState _linregLx1;
    private readonly LinearRegressionState _linregAx2;
    private readonly LinearRegressionState _linregLx2;
    private readonly LinearRegressionState _linregAx3;
    private readonly RollingWindowSum _lcoSum;
    private readonly RollingWindowSum _lcoSmaSum;
    private readonly StreamingInputResolver _input;
    private double _ax1Value;
    private double _lx1Value;
    private double _ax2Value;
    private double _lx2Value;
    private double _ax3Value;

    public ZeroLagSmoothedCycleState(int length = 100)
    {
        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _linreg = new LinearRegressionState(_length);
        _linregAx1 = new LinearRegressionState(_length, _ => _ax1Value);
        _linregLx1 = new LinearRegressionState(_length, _ => _lx1Value);
        _linregAx2 = new LinearRegressionState(_length, _ => _ax2Value);
        _linregLx2 = new LinearRegressionState(_length, _ => _lx2Value);
        _linregAx3 = new LinearRegressionState(_length, _ => _ax3Value);
        _lcoSum = new RollingWindowSum(_length1);
        _lcoSmaSum = new RollingWindowSum(_length1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ZeroLagSmoothedCycle;

    public void Reset()
    {
        _linreg.Reset();
        _linregAx1.Reset();
        _linregLx1.Reset();
        _linregAx2.Reset();
        _linregLx2.Reset();
        _linregAx3.Reset();
        _lcoSum.Reset();
        _lcoSmaSum.Reset();
        _ax1Value = 0;
        _lx1Value = 0;
        _ax2Value = 0;
        _lx2Value = 0;
        _ax3Value = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var linreg = _linreg.Update(bar, isFinal, includeOutputs: false).Value;
        _ax1Value = value - linreg;
        var ax1Linreg = _linregAx1.Update(bar, isFinal, includeOutputs: false).Value;
        _lx1Value = _ax1Value + (_ax1Value - ax1Linreg);
        var lx1Linreg = _linregLx1.Update(bar, isFinal, includeOutputs: false).Value;
        _ax2Value = _lx1Value - lx1Linreg;
        var ax2Linreg = _linregAx2.Update(bar, isFinal, includeOutputs: false).Value;
        _lx2Value = _ax2Value + (_ax2Value - ax2Linreg);
        var lx2Linreg = _linregLx2.Update(bar, isFinal, includeOutputs: false).Value;
        _ax3Value = _lx2Value - lx2Linreg;
        var ax3Linreg = _linregAx3.Update(bar, isFinal, includeOutputs: false).Value;
        var lco = _ax3Value + (_ax3Value - ax3Linreg);
        var lcoSum = isFinal ? _lcoSum.Add(lco, out var count1) : _lcoSum.Preview(lco, out count1);
        var lcoSma1 = count1 > 0 ? lcoSum / count1 : 0;
        var lcoSmaSum = isFinal ? _lcoSmaSum.Add(lcoSma1, out var count2) : _lcoSmaSum.Preview(lcoSma1, out count2);
        var lcoSma2 = count2 > 0 ? lcoSmaSum / count2 : 0;
        var filter = -lcoSma2 * 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Lco", lco },
                { "Filter", filter }
            };
        }

        return new StreamingIndicatorStateResult(filter, outputs);
    }

    public void Dispose()
    {
        _linreg.Dispose();
        _linregAx1.Dispose();
        _linregLx1.Dispose();
        _linregAx2.Dispose();
        _linregLx2.Dispose();
        _linregAx3.Dispose();
        _lcoSum.Dispose();
        _lcoSmaSum.Dispose();
    }
}

[PrimaryOutput("Ztema")]
public sealed class ZeroLagTripleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _tma1;
    private readonly IMovingAverageSmoother _tma2;
    private readonly StreamingInputResolver _input;

    public ZeroLagTripleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 14)
    {
        var resolvedType = maType == MovingAvgType.ZeroLagTripleExponentialMovingAverage
            ? MovingAvgType.TripleExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _tma1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _tma2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ZeroLagTripleExponentialMovingAverage;

    public void Reset()
    {
        _tma1.Reset();
        _tma2.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var tma1 = _tma1.Next(value, isFinal);
        var tma2 = _tma2.Next(tma1, isFinal);
        var zltema = tma1 + (tma1 - tma2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ztema", zltema }
            };
        }

        return new StreamingIndicatorStateResult(zltema, outputs);
    }

    public void Dispose()
    {
        _tma1.Dispose();
        _tma2.Dispose();
    }
}

[PrimaryOutput("Zllma")]
public sealed class ZeroLowLagMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _lbLength;
    private readonly double _lag;
    private readonly PooledRingBuffer<double> _aValues;
    private readonly PooledRingBuffer<double> _bValues;
    private readonly StreamingInputResolver _input;
    private int _index;
    private double _prevA;

    public ZeroLowLagMovingAverageState(int length = 50, double lag = 1.4)
    {
        _length = Math.Max(1, length);
        _lbLength = Math.Max(1, Math.Min(530, (int)Math.Ceiling((double)_length / 2)));
        _lag = lag;
        _aValues = new PooledRingBuffer<double>(_length + 1);
        _bValues = new PooledRingBuffer<double>(_lbLength + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ZeroLowLagMovingAverage;

    public void Reset()
    {
        _aValues.Clear();
        _bValues.Clear();
        _index = 0;
        _prevA = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var priorB = _index >= _lbLength ? EhlersStreamingWindow.GetOffsetValue(_bValues, 0, _lbLength) : value;
        var priorA = _index >= _length ? EhlersStreamingWindow.GetOffsetValue(_aValues, 0, _length) : 0;
        var prevA = _index >= 1 ? _prevA : 0;
        var a = (_lag * value) + ((1 - _lag) * priorB) + prevA;
        var b = _length != 0 ? (a - priorA) / _length : 0;

        if (isFinal)
        {
            _aValues.TryAdd(a, out _);
            _bValues.TryAdd(b, out _);
            _prevA = a;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Zllma", b }
            };
        }

        return new StreamingIndicatorStateResult(b, outputs);
    }

    public void Dispose()
    {
        _aValues.Dispose();
        _bValues.Dispose();
    }
}
