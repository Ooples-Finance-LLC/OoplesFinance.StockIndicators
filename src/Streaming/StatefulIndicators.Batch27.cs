using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class VolumeFlowIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _coef;
    private readonly double _vcoef;
    private readonly RollingWindowSum _vcpSum;
    private readonly StandardDeviationVolatilityState _vinter;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _vfiMa;
    private readonly IMovingAverageSmoother _signalMa;
    private readonly StreamingInputResolver _input;
    private double _inter;
    private double _prevValue;
    private double _prevVave;
    private bool _hasPrev;

    public VolumeFlowIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.TypicalPrice, int length1 = 130, int length2 = 30,
        int signalLength = 5, int smoothLength = 3, double coef = 0.2, double vcoef = 2.5)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _coef = coef;
        _vcoef = vcoef;
        _vcpSum = new RollingWindowSum(_length1);
        _vinter = new StandardDeviationVolatilityState(maType, _length2, _ => _inter);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length1);
        _vfiMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _signalMa = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeFlowIndicatorState(MovingAvgType maType, int length1, int length2, int signalLength,
        int smoothLength, double coef, double vcoef, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _coef = coef;
        _vcoef = vcoef;
        _vcpSum = new RollingWindowSum(_length1);
        _vinter = new StandardDeviationVolatilityState(maType, _length2, _ => _inter);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length1);
        _vfiMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _signalMa = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeFlowIndicator;

    public void Reset()
    {
        _vcpSum.Reset();
        _vinter.Reset();
        _volumeMa.Reset();
        _vfiMa.Reset();
        _signalMa.Reset();
        _inter = 0;
        _prevValue = 0;
        _prevVave = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var inter = value > 0 && prevValue > 0 ? Math.Log(value) - Math.Log(prevValue) : 0;
        _inter = inter;
        var vinter = _vinter.Update(bar, isFinal, includeOutputs: false).Value;
        var vave = _volumeMa.Next(bar.Volume, isFinal);
        var prevVave = _hasPrev ? _prevVave : 0;
        var cutoff = bar.Close * vinter * _coef;
        var vmax = prevVave * _vcoef;
        var vc = Math.Min(bar.Volume, vmax);
        var mf = _hasPrev ? value - prevValue : 0;
        var vcp = mf > cutoff ? vc : mf < cutoff * -1 ? vc * -1 : mf > 0 ? vc : mf < 0 ? vc * -1 : 0;
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

public sealed class VolumePositiveNegativeIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly IMovingAverageSmoother _atrMa;
    private readonly IMovingAverageSmoother _vpnSmooth;
    private readonly RollingWindowSum _vmpSum;
    private readonly RollingWindowSum _vmnSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevClose;
    private bool _hasPrev;

    public VolumePositiveNegativeIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        InputName inputName = InputName.TypicalPrice, int length = 30, int smoothLength = 3)
    {
        _length = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length);
        _atrMa = MovingAverageSmootherFactory.Create(maType, _length);
        _vpnSmooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _vmpSum = new RollingWindowSum(_length);
        _vmnSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumePositiveNegativeIndicatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, _length);
        _atrMa = MovingAverageSmootherFactory.Create(maType, _length);
        _vpnSmooth = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _vmpSum = new RollingWindowSum(_length);
        _vmnSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolumePositiveNegativeIndicator;

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
        var prevClose = _hasPrev ? _prevClose : 0;
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
        int slowLength = 20, int length = 8, InputName inputName = InputName.Close)
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
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumePriceConfirmationIndicatorState(MovingAvgType maType, int fastLength, int slowLength, int length,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

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
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class VolumeWeightedAveragePriceState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _cumVolume;
    private double _cumVolumePrice;

    public VolumeWeightedAveragePriceState(InputName inputName = InputName.TypicalPrice)
    {
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeWeightedAveragePriceState(Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeWeightedAveragePrice;

    public void Reset()
    {
        _cumVolume = 0;
        _cumVolumePrice = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumeSum = _cumVolume + volume;
        var volumePriceSum = _cumVolumePrice + (value * volume);
        var vwap = volumeSum != 0 ? volumePriceSum / volumeSum : 0;

        if (isFinal)
        {
            _cumVolume = volumeSum;
            _cumVolumePrice = volumePriceSum;
        }

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

public sealed class VolumeWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumePriceSum;
    private readonly IMovingAverageSmoother _volumeMa;
    private readonly StreamingInputResolver _input;

    public VolumeWeightedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _volumePriceSum = new RollingWindowSum(resolved);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeWeightedMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _volumePriceSum = new RollingWindowSum(resolved);
        _volumeMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.VolumeWeightedMovingAverage;

    public void Reset()
    {
        _volumePriceSum.Reset();
        _volumeMa.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumePrice = value * volume;
        var volumePriceSum = isFinal ? _volumePriceSum.Add(volumePrice, out var count) : _volumePriceSum.Preview(volumePrice, out count);
        var volumePriceAvg = count > 0 ? volumePriceSum / count : 0;
        var volumeMa = _volumeMa.Next(volume, isFinal);
        var vwma = volumeMa != 0 ? volumePriceAvg / volumeMa : 0;

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
        _volumePriceSum.Dispose();
        _volumeMa.Dispose();
    }
}

public sealed class VolumeWeightedRelativeStrengthIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _upMa;
    private readonly IMovingAverageSmoother _downMa;
    private readonly IMovingAverageSmoother _smoothMa;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public VolumeWeightedRelativeStrengthIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int smoothLength = 3, InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _upMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _downMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _smoothMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VolumeWeightedRelativeStrengthIndexState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _upMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _downMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _smoothMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class VortexBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _basisMa;
    private readonly IMovingAverageSmoother _diffMa;
    private readonly StreamingInputResolver _input;

    public VortexBandsState(MovingAvgType maType = MovingAvgType.McNichollMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _basisMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _diffMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public VortexBandsState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _basisMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _diffMa = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var diff = value - basis;
        var diffMa = _diffMa.Next(diff, isFinal);
        var dev = 2 * diffMa;
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

        return new StreamingIndicatorStateResult(basis, outputs);
    }

    public void Dispose()
    {
        _basisMa.Dispose();
        _diffMa.Dispose();
    }
}

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
        int length2 = 100, double level = 8, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _level = level;
        _tempSum = new RollingWindowSum(_length1);
        _rangeSum = new RollingWindowSum(_length1);
        _wma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(inputName, null);
    }

    public VostroIndicatorState(MovingAvgType maType, int length1, int length2, double level,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _level = level;
        _tempSum = new RollingWindowSum(_length1);
        _rangeSum = new RollingWindowSum(_length1);
        _wma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class WaddahAttarExplosionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _bbLength;
    private readonly double _sensitivity;
    private readonly MacdEngine _macd1;
    private readonly MacdEngine _macd2;
    private readonly MacdEngine _macd3;
    private readonly MacdEngine _macd4;
    private readonly RollingWindowStats _bbWindow;
    private readonly StreamingInputResolver _input;
    private readonly PooledRingBuffer<double> _values;

    public WaddahAttarExplosionState(int fastLength = 20, int slowLength = 40, double sensitivity = 150,
        InputName inputName = InputName.Close)
    {
        _bbLength = Math.Max(1, fastLength);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _sensitivity = sensitivity;
        _macd1 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd2 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd3 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd4 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _bbWindow = new RollingWindowStats(_bbLength);
        _input = new StreamingInputResolver(inputName, null);
        _values = new PooledRingBuffer<double>(3);
    }

    public WaddahAttarExplosionState(int fastLength, int slowLength, double sensitivity, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _bbLength = Math.Max(1, fastLength);
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _sensitivity = sensitivity;
        _macd1 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd2 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd3 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _macd4 = new MacdEngine(MovingAvgType.ExponentialMovingAverage, resolvedFast, resolvedSlow, 9);
        _bbWindow = new RollingWindowStats(_bbLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var snapshot = isFinal ? _bbWindow.Add(value) : _bbWindow.Preview(value);
        var middle = snapshot.Count >= _bbLength ? snapshot.Sum / _bbLength : 0;
        var variance = snapshot.Count >= _bbLength ? (snapshot.SumSquares / _bbLength) - (middle * middle) : 0;
        var stdDev = MathHelper.Sqrt(variance);
        var upper = middle + (stdDev * 2);
        var lower = middle - (stdDev * 2);

        var prev1 = EhlersStreamingWindow.GetOffsetValue(_values, value, 1);
        var prev2 = EhlersStreamingWindow.GetOffsetValue(_values, value, 2);
        var prev3 = EhlersStreamingWindow.GetOffsetValue(_values, value, 3);

        var macd1 = _macd1.Next(value, isFinal, out _, out _);
        var macd2 = _macd2.Next(prev1, isFinal, out _, out _);
        var macd3 = _macd3.Next(prev2, isFinal, out _, out _);
        var macd4 = _macd4.Next(prev3, isFinal, out _, out _);

        var t1 = (macd1 - macd2) * _sensitivity;
        var t2 = (macd3 - macd4) * _sensitivity;
        var e1 = upper - lower;
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

public sealed class WamiOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffWma;
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public WamiOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 13,
        int length2 = 4, InputName inputName = InputName.Close)
    {
        _diffWma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length2));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(inputName, null);
    }

    public WamiOscillatorState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _diffWma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length2));
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class WaveTrendOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _esaMa;
    private readonly IMovingAverageSmoother _dMa;
    private readonly IMovingAverageSmoother _tciMa;
    private readonly IMovingAverageSmoother _wt2Ma;
    private readonly StreamingInputResolver _input;

    public WaveTrendOscillatorState(InputName inputName = InputName.FullTypicalPrice,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 21,
        int smoothLength = 4)
    {
        _esaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _tciMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _wt2Ma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public WaveTrendOscillatorState(MovingAvgType maType, int length1, int length2, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _esaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _tciMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _wt2Ma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WaveTrendOscillator;

    public void Reset()
    {
        _esaMa.Reset();
        _dMa.Reset();
        _tciMa.Reset();
        _wt2Ma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var ap = _input.GetValue(bar);
        var esa = _esaMa.Next(ap, isFinal);
        var absApEsa = Math.Abs(ap - esa);
        var d = _dMa.Next(absApEsa, isFinal);
        var ci = d != 0 ? (ap - esa) / (0.015 * d) : 0;
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

public sealed class WellesWilderSummationState : IStreamingIndicatorState
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private double _prevSum;

    public WellesWilderSummationState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WellesWilderSummationState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        int length2 = 21, double factor = 3, InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _factor = factor;
        _atrMa = MovingAverageSmootherFactory.Create(maType, resolved2);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved1);
        _maxWindow = new RollingWindowMax(resolved2);
        _minWindow = new RollingWindowMin(resolved2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WellesWilderVolatilitySystemState(MovingAvgType maType, int length1, int length2, double factor,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _factor = factor;
        _atrMa = MovingAverageSmootherFactory.Create(maType, resolved2);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved1);
        _maxWindow = new RollingWindowMax(resolved2);
        _minWindow = new RollingWindowMin(resolved2);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
        var prevClose = _hasPrev ? _prevClose : 0;
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

    public WellRoundedMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _alpha = 2d / (_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WellRoundedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _alpha = 2d / (_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class WilliamsAccumulationDistributionState : IStreamingIndicatorState
{
    private double _prevClose;
    private double _prevHigh;
    private double _prevLow;
    private double _prevWad;
    private bool _hasPrev;

    public IndicatorName Name => IndicatorName.WilliamsAccumulationDistribution;

    public void Reset()
    {
        _prevClose = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevWad = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = bar.Close;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var wad = close > prevClose ? _prevWad + close - prevLow : close < prevClose ? _prevWad + close - prevHigh : 0;

        if (isFinal)
        {
            _prevClose = close;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
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

public sealed class WilliamsFractalsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _highs;
    private readonly PooledRingBuffer<double> _lows;
    private int _index;

    public WilliamsFractalsState(int length = 2)
    {
        _length = Math.Max(1, length);
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
        var index = _index;
        var prevHigh = index >= _length - 2 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length - 2) : 0;
        var prevHigh1 = index >= _length - 1 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length - 1) : 0;
        var prevHigh2 = index >= _length ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length) : 0;
        var prevHigh3 = index >= _length + 1 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 1) : 0;
        var prevHigh4 = index >= _length + 2 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 2) : 0;
        var prevHigh5 = index >= _length + 3 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 3) : 0;
        var prevHigh6 = index >= _length + 4 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 4) : 0;
        var prevHigh7 = index >= _length + 5 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 5) : 0;
        var prevHigh8 = index >= _length + 8 ? EhlersStreamingWindow.GetOffsetValue(_highs, bar.High, _length + 6) : 0;
        var prevLow = index >= _length - 2 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length - 2) : 0;
        var prevLow1 = index >= _length - 1 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length - 1) : 0;
        var prevLow2 = index >= _length ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length) : 0;
        var prevLow3 = index >= _length + 1 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 1) : 0;
        var prevLow4 = index >= _length + 2 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 2) : 0;
        var prevLow5 = index >= _length + 3 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 3) : 0;
        var prevLow6 = index >= _length + 4 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 4) : 0;
        var prevLow7 = index >= _length + 5 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 5) : 0;
        var prevLow8 = index >= _length + 8 ? EhlersStreamingWindow.GetOffsetValue(_lows, bar.Low, _length + 6) : 0;

        double upFractal = (prevHigh4 < prevHigh2 && prevHigh3 < prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) ||
            (prevHigh5 < prevHigh2 && prevHigh4 < prevHigh2 && prevHigh3 == prevHigh2 && prevHigh1 < prevHigh2) ||
            (prevHigh6 < prevHigh2 && prevHigh5 < prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) || (prevHigh7 < prevHigh2 && prevHigh6 < prevHigh2 && prevHigh5 == prevHigh2 && prevHigh4 == prevHigh2 &&
                prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 && prevHigh < prevHigh2) || (prevHigh8 < prevHigh2 && prevHigh7 < prevHigh2 &&
                prevHigh6 == prevHigh2 && prevHigh5 <= prevHigh2 && prevHigh4 == prevHigh2 && prevHigh3 <= prevHigh2 && prevHigh1 < prevHigh2 &&
                prevHigh < prevHigh2) ? 1d : 0d;
        double dnFractal = (prevLow4 > prevLow2 && prevLow3 > prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ||
            (prevLow5 > prevLow2 && prevLow4 > prevLow2 && prevLow3 == prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) ||
            (prevLow6 > prevLow2 && prevLow5 > prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 &&
                prevLow > prevLow2) || (prevLow7 > prevLow2 && prevLow6 > prevLow2 && prevLow5 == prevLow2 && prevLow4 == prevLow2 &&
                prevLow3 >= prevLow2 && prevLow1 > prevLow2 && prevLow > prevLow2) || (prevLow8 > prevLow2 && prevLow7 > prevLow2 &&
                prevLow6 == prevLow2 && prevLow5 >= prevLow2 && prevLow4 == prevLow2 && prevLow3 >= prevLow2 && prevLow1 > prevLow2 &&
                prevLow > prevLow2) ? 1d : 0d;

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
        double lowerNeutralZone = 45, InputName inputName = InputName.Close)
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
        _input = new StreamingInputResolver(inputName, null);
    }

    public WilsonRelativePriceChannelState(MovingAvgType maType, int length, int smoothLength, double overbought,
        double oversold, double upperNeutralZone, double lowerNeutralZone, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

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
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class WindowedVolumeWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _bartlettWSum;
    private readonly RollingWindowSum _bartlettVWSum;
    private readonly RollingWindowSum _blackmanWSum;
    private readonly RollingWindowSum _blackmanVWSum;
    private readonly RollingWindowSum _hanningWSum;
    private readonly RollingWindowSum _hanningVWSum;
    private readonly StreamingInputResolver _input;
    private int _index;

    public WindowedVolumeWeightedMovingAverageState(int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _bartlettWSum = new RollingWindowSum(_length);
        _bartlettVWSum = new RollingWindowSum(_length);
        _blackmanWSum = new RollingWindowSum(_length);
        _blackmanVWSum = new RollingWindowSum(_length);
        _hanningWSum = new RollingWindowSum(_length);
        _hanningVWSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public WindowedVolumeWeightedMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _bartlettWSum = new RollingWindowSum(_length);
        _bartlettVWSum = new RollingWindowSum(_length);
        _blackmanWSum = new RollingWindowSum(_length);
        _blackmanVWSum = new RollingWindowSum(_length);
        _hanningWSum = new RollingWindowSum(_length);
        _hanningVWSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.WindowedVolumeWeightedMovingAverage;

    public void Reset()
    {
        _bartlettWSum.Reset();
        _bartlettVWSum.Reset();
        _blackmanWSum.Reset();
        _blackmanVWSum.Reset();
        _hanningWSum.Reset();
        _hanningVWSum.Reset();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var iRatio = (double)_index / _length;
        var bartlett = 1 - (2 * Math.Abs(_index - (_length / 2d)) / _length);

        var bartlettW = bartlett * volume;
        var bartlettWSum = isFinal ? _bartlettWSum.Add(bartlettW, out _) : _bartlettWSum.Preview(bartlettW, out _);
        var bartlettVW = value * bartlettW;
        var bartlettVWSum = isFinal ? _bartlettVWSum.Add(bartlettVW, out _) : _bartlettVWSum.Preview(bartlettVW, out _);
        var bartlettWvwma = bartlettWSum != 0 ? bartlettVWSum / bartlettWSum : 0;

        var blackman = 0.42 - (0.5 * Math.Cos(2 * Math.PI * iRatio)) + (0.08 * Math.Cos(4 * Math.PI * iRatio));
        var blackmanW = blackman * volume;
        _ = isFinal ? _blackmanWSum.Add(blackmanW, out _) : _blackmanWSum.Preview(blackmanW, out _);
        var blackmanVW = value * blackmanW;
        _ = isFinal ? _blackmanVWSum.Add(blackmanVW, out _) : _blackmanVWSum.Preview(blackmanVW, out _);

        var hanning = 0.5 - (0.5 * Math.Cos(2 * Math.PI * iRatio));
        var hanningW = hanning * volume;
        _ = isFinal ? _hanningWSum.Add(hanningW, out _) : _hanningWSum.Preview(hanningW, out _);
        var hanningVW = value * hanningW;
        _ = isFinal ? _hanningVWSum.Add(hanningVW, out _) : _hanningVWSum.Preview(hanningVW, out _);

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Wvwma", bartlettWvwma }
            };
        }

        return new StreamingIndicatorStateResult(bartlettWvwma, outputs);
    }

    public void Dispose()
    {
        _bartlettWSum.Dispose();
        _bartlettVWSum.Dispose();
        _blackmanWSum.Dispose();
        _blackmanVWSum.Dispose();
        _hanningWSum.Dispose();
        _hanningVWSum.Dispose();
    }
}

public sealed class WoodieCommodityChannelIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly CommodityChannelIndexState _slowCci;
    private readonly CommodityChannelIndexState _fastCci;

    public WoodieCommodityChannelIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 6, int slowLength = 14)
    {
        _slowCci = new CommodityChannelIndexState(InputName.TypicalPrice, maType, Math.Max(1, slowLength), 0.015);
        _fastCci = new CommodityChannelIndexState(InputName.TypicalPrice, maType, Math.Max(1, fastLength), 0.015);
    }

    public WoodieCommodityChannelIndexState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _slowCci = new CommodityChannelIndexState(InputName.Close, maType, Math.Max(1, slowLength), 0.015, selector);
        _fastCci = new CommodityChannelIndexState(InputName.Close, maType, Math.Max(1, fastLength), 0.015, selector);
    }

    public IndicatorName Name => IndicatorName.WoodieCommodityChannelIndex;

    public void Reset()
    {
        _slowCci.Reset();
        _fastCci.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
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

public sealed class ZDistanceFromVwapState : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAvgType _maType;
    private readonly int _length;
    private readonly IMovingAverageSmoother? _meanMa;
    private readonly StandardDeviationVolatilityState? _stdDev;
    private readonly StreamingInputResolver _input;
    private readonly StreamingInputResolver _vwapInput;
    private double _inputValue;
    private double _cumVolume;
    private double _cumVolumePrice;

    public ZDistanceFromVwapState(MovingAvgType maType = MovingAvgType.VolumeWeightedAveragePrice, int length = 20,
        InputName inputName = InputName.Close)
    {
        _maType = maType;
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);

        if (_maType == MovingAvgType.VolumeWeightedAveragePrice)
        {
            _vwapInput = new StreamingInputResolver(InputName.TypicalPrice, null);
        }
        else
        {
            _vwapInput = default;
            _meanMa = MovingAverageSmootherFactory.Create(_maType, _length);
            _stdDev = new StandardDeviationVolatilityState(_maType, _length, _ => _inputValue);
        }
    }

    public ZDistanceFromVwapState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _maType = maType;
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);

        if (_maType == MovingAvgType.VolumeWeightedAveragePrice)
        {
            _vwapInput = new StreamingInputResolver(InputName.TypicalPrice, null);
        }
        else
        {
            _vwapInput = default;
            _meanMa = MovingAverageSmootherFactory.Create(_maType, _length);
            _stdDev = new StandardDeviationVolatilityState(_maType, _length, _ => _inputValue);
        }
    }

    public IndicatorName Name => IndicatorName.ZDistanceFromVwap;

    public void Reset()
    {
        _meanMa?.Reset();
        _stdDev?.Reset();
        _inputValue = 0;
        _cumVolume = 0;
        _cumVolumePrice = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _inputValue = value;

        double mean;
        double stdDev;
        if (_maType == MovingAvgType.VolumeWeightedAveragePrice)
        {
            var vwapValue = _vwapInput.GetValue(bar);
            var volume = bar.Volume;
            var volumeSum = _cumVolume + volume;
            var volumePriceSum = _cumVolumePrice + (vwapValue * volume);
            mean = volumeSum != 0 ? volumePriceSum / volumeSum : 0;
            stdDev = mean >= 0 ? MathHelper.Sqrt(mean) : 0;

            if (isFinal)
            {
                _cumVolume = volumeSum;
                _cumVolumePrice = volumePriceSum;
            }
        }
        else
        {
            mean = _meanMa!.Next(value, isFinal);
            stdDev = _stdDev!.Update(bar, isFinal, includeOutputs: false).Value;
        }

        var zscore = stdDev != 0 ? (value - mean) / stdDev : 0;

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
        _stdDev?.Dispose();
    }
}

public sealed class ZeroLagExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly StreamingInputResolver _input;

    public ZeroLagExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolvedType = maType == MovingAvgType.ZeroLagExponentialMovingAverage
            ? MovingAvgType.ExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZeroLagExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedType = maType == MovingAvgType.ZeroLagExponentialMovingAverage
            ? MovingAvgType.ExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

    public ZeroLagSmoothedCycleState(int length = 100, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _linreg = new LinearRegressionState(_length, inputName);
        _linregAx1 = new LinearRegressionState(_length, _ => _ax1Value);
        _linregLx1 = new LinearRegressionState(_length, _ => _lx1Value);
        _linregAx2 = new LinearRegressionState(_length, _ => _ax2Value);
        _linregLx2 = new LinearRegressionState(_length, _ => _lx2Value);
        _linregAx3 = new LinearRegressionState(_length, _ => _ax3Value);
        _lcoSum = new RollingWindowSum(_length1);
        _lcoSmaSum = new RollingWindowSum(_length1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZeroLagSmoothedCycleState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _length1 = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _linreg = new LinearRegressionState(_length, selector);
        _linregAx1 = new LinearRegressionState(_length, _ => _ax1Value);
        _linregLx1 = new LinearRegressionState(_length, _ => _lx1Value);
        _linregAx2 = new LinearRegressionState(_length, _ => _ax2Value);
        _linregLx2 = new LinearRegressionState(_length, _ => _lx2Value);
        _linregAx3 = new LinearRegressionState(_length, _ => _ax3Value);
        _lcoSum = new RollingWindowSum(_length1);
        _lcoSmaSum = new RollingWindowSum(_length1);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

public sealed class ZeroLagTripleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _tma1;
    private readonly IMovingAverageSmoother _tma2;
    private readonly StreamingInputResolver _input;

    public ZeroLagTripleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolvedType = maType == MovingAvgType.ZeroLagTripleExponentialMovingAverage
            ? MovingAvgType.TripleExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _tma1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _tma2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZeroLagTripleExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedType = maType == MovingAvgType.ZeroLagTripleExponentialMovingAverage
            ? MovingAvgType.TripleExponentialMovingAverage
            : maType;
        var resolved = Math.Max(1, length);
        _tma1 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _tma2 = MovingAverageSmootherFactory.Create(resolvedType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
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

    public ZeroLowLagMovingAverageState(int length = 50, double lag = 1.4, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _lbLength = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _lag = lag;
        _aValues = new PooledRingBuffer<double>(_length + 1);
        _bValues = new PooledRingBuffer<double>(_lbLength + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public ZeroLowLagMovingAverageState(int length, double lag, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _lbLength = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 2));
        _lag = lag;
        _aValues = new PooledRingBuffer<double>(_length + 1);
        _bValues = new PooledRingBuffer<double>(_lbLength + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
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
