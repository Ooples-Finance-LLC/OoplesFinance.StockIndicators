#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Evwma")]
public sealed class ElasticVolumeWeightedMovingAverageV1State : IStreamingIndicatorState, IDisposable
{
    private readonly double _mult;
    private readonly StreamingInputResolver _input;
    private readonly IMovingAverageSmoother _volumeSmoother;
    private double _prevEvwma;
    private bool _hasPrev;

    public ElasticVolumeWeightedMovingAverageV1State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 40, double mult = 20)
    {
        _mult = mult;
        _volumeSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ElasticVolumeWeightedMovingAverageV1;

    public void Reset()
    {
        _volumeSmoother.Reset();
        _prevEvwma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var avgVolume = _volumeSmoother.Next(bar.Volume, isFinal);
        var n = avgVolume * _mult;
        var prevEvwma = _hasPrev ? _prevEvwma : value;
        var evwma = n > 0 ? prevEvwma + bar.Volume / n * (value - prevEvwma) : prevEvwma;

        if (isFinal)
        {
            _prevEvwma = evwma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Evwma", evwma }
            };
        }

        return new StreamingIndicatorStateResult(evwma, outputs);
    }

    public void Dispose()
    {
        _volumeSmoother.Dispose();
    }
}

[PrimaryOutput("Evwma")]
public sealed class ElasticVolumeWeightedMovingAverageV2State : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _volumeSum;
    private readonly StreamingInputResolver _input;
    private double _prevEvwma;
    private bool _hasPrev;

    public ElasticVolumeWeightedMovingAverageV2State(int length = 14)
    {
        _volumeSum = new RollingWindowSum(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ElasticVolumeWeightedMovingAverageV2;

    public void Reset()
    {
        _volumeSum.Reset();
        _prevEvwma = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var volumeSum = isFinal ? _volumeSum.Add(volume, out _) : _volumeSum.Preview(volume, out _);
        var prevEvwma = _hasPrev ? _prevEvwma : value;
        var evwma = volumeSum > 0 ? prevEvwma + volume / volumeSum * (value - prevEvwma) : prevEvwma;

        if (isFinal)
        {
            _prevEvwma = evwma;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Evwma", evwma }
            };
        }

        return new StreamingIndicatorStateResult(evwma, outputs);
    }

    public void Dispose()
    {
        _volumeSum.Dispose();
    }
}

[PrimaryOutput("Emt")]
public sealed class ElderMarketThermometerState : IStreamingIndicatorState, IDisposable
{
    private readonly ElderThermometerWindow _window;
    public ElderMarketThermometerState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 22)
        => _window = new ElderThermometerWindow(maType, length);
    public IndicatorName Name => IndicatorName.ElderMarketThermometer;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var (value, signal) = _window.Next(bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Emt", value }, { "Signal", signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Eszs")]
public sealed class ElderSafeZoneStopsState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly RollingWindowSum _dmMinusCountSum;
    private readonly RollingWindowSum _dmPlusCountSum;
    private readonly RollingWindowSum _dmMinusSum;
    private readonly RollingWindowSum _dmPlusSum;
    private readonly RollingWindowMax _safeZMinusMax;
    private readonly RollingWindowMin _safeZPlusMin;
    private readonly double _factor;
    private readonly StreamingInputResolver _input;
    private double _prevLow;
    private double _prevHigh;
    private bool _hasPrev;

    public ElderSafeZoneStopsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 63,
        int length2 = 22, int length3 = 3, double factor = 2.5)
    {
        _ema = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _dmMinusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmMinusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusSum = new RollingWindowSum(Math.Max(1, length2));
        _dmPlusCountSum = new RollingWindowSum(Math.Max(1, length2));
        _safeZMinusMax = new RollingWindowMax(Math.Max(1, length3));
        _safeZPlusMin = new RollingWindowMin(Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, null);
        _factor = factor;
    }

    public IndicatorName Name => IndicatorName.ElderSafeZoneStops;

    public void Reset()
    {
        _ema.Reset();
        _dmMinusSum.Reset();
        _dmMinusCountSum.Reset();
        _dmPlusSum.Reset();
        _dmPlusCountSum.Reset();
        _safeZMinusMax.Reset();
        _safeZPlusMin.Reset();
        _prevLow = 0;
        _prevHigh = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;

        var dmMinus = prevLow > bar.Low ? prevLow - bar.Low : 0;
        var dmMinusCount = prevLow > bar.Low ? 1d : 0d;
        var dmPlus = bar.High > prevHigh ? bar.High - prevHigh : 0;
        var dmPlusCount = bar.High > prevHigh ? 1d : 0d;

        var dmMinusSum = isFinal ? _dmMinusSum.Add(dmMinus, out _) : _dmMinusSum.Preview(dmMinus, out _);
        var dmMinusCountSum = isFinal ? _dmMinusCountSum.Add(dmMinusCount, out _) : _dmMinusCountSum.Preview(dmMinusCount, out _);
        var dmPlusSum = isFinal ? _dmPlusSum.Add(dmPlus, out _) : _dmPlusSum.Preview(dmPlus, out _);
        var dmPlusCountSum = isFinal ? _dmPlusCountSum.Add(dmPlusCount, out _) : _dmPlusCountSum.Preview(dmPlusCount, out _);

        var dmAvgMinus = dmMinusCountSum != 0 ? dmMinusSum / dmMinusCountSum : 0;
        var dmAvgPlus = dmPlusCountSum != 0 ? dmPlusSum / dmPlusCountSum : 0;

        var safeZMinus = prevLow - (_factor * dmAvgMinus);
        var safeZPlus = prevHigh + (_factor * dmAvgPlus);

        var highest = isFinal ? _safeZMinusMax.Add(safeZMinus, out _) : _safeZMinusMax.Preview(safeZMinus, out _);
        var lowest = isFinal ? _safeZPlusMin.Add(safeZPlus, out _) : _safeZPlusMin.Preview(safeZPlus, out _);
        var ema = _ema.Next(value, isFinal);
        var stop = value >= ema ? highest : lowest;

        if (isFinal)
        {
            _prevLow = bar.Low;
            _prevHigh = bar.High;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eszs", stop }
            };
        }

        return new StreamingIndicatorStateResult(stop, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _dmMinusSum.Dispose();
        _dmMinusCountSum.Dispose();
        _dmPlusSum.Dispose();
        _dmPlusCountSum.Dispose();
        _safeZMinusMax.Dispose();
        _safeZPlusMin.Dispose();
    }
}

[PrimaryOutput("Ewo")]
public sealed class ElliottWaveOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public ElliottWaveOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 34)
    {
        _fastSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ElliottWaveOscillator;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _signalSmoother.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var ewo = fast - slow;
        var invalidSignal = _signalInvalid || double.IsInfinity(ewo);
        var signal = invalidSignal ? double.NaN : _signalSmoother.Next(ewo, isFinal);
        if (isFinal) _signalInvalid = invalidSignal;
        var histogram = ewo - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ewo", ewo },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(ewo, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Wa")]
public sealed class EmaWaveIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ResidualAverageWindow _a, _b, _c;
    private readonly StreamingInputResolver _input;
    public EmaWaveIndicatorState(int length1 = 5, int length2 = 25, int length3 = 50, int smoothLength = 4)
    {
        _a = new ResidualAverageWindow(MovingAvgType.ExponentialMovingAverage, length1, MovingAvgType.SimpleMovingAverage, smoothLength);
        _b = new ResidualAverageWindow(MovingAvgType.ExponentialMovingAverage, length2, MovingAvgType.SimpleMovingAverage, smoothLength);
        _c = new ResidualAverageWindow(MovingAvgType.ExponentialMovingAverage, length3, MovingAvgType.SimpleMovingAverage, smoothLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.EmaWaveIndicator;
    public void Reset() { _a.Reset(); _b.Reset(); _c.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var a = _a.Next(value, isFinal).Value; var b = _b.Next(value, isFinal).Value; var c = _c.Next(value, isFinal).Value;
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Wa", a }, { "Wb", b }, { "Wc", c } } : null;
        return new StreamingIndicatorStateResult(a, outputs);
    }
    public void Dispose() { _a.Dispose(); _b.Dispose(); _c.Dispose(); }
}

[PrimaryOutput("Epma")]
public sealed class EndPointMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly AffineAverageWindow _window;
    private readonly StreamingInputResolver _input;

    public EndPointMovingAverageState(int length = 11, int offset = 4)
    {
        _window = new AffineAverageWindow(length, offset);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EndPointMovingAverage;
    public void Reset() { _window.Reset(); }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var result = _window.Next(value, isFinal: isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Epma", result } } : null;
        return new StreamingIndicatorStateResult(result, outputs);
    }

    public void Dispose() { _window.Dispose(); }
}

[PrimaryOutput("Ei")]
public sealed class EnhancedIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _smaSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public EnhancedIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int signalLength = 8)
    {
        var resolved = Math.Max(1, length);
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _smaSmoother = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EnhancedIndex;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _smaSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var dnm = highest - lowest;
        var sma = _smaSmoother.Next(value, isFinal);
        var closewr = dnm != 0 ? 2 * (value - sma) / dnm : 0;
        var signal = _signalSmoother.Next(closewr, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ei", closewr },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(closewr, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _smaSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Ewr")]
public sealed class EnhancedWilliamsRState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _srcMax;
    private readonly RollingWindowMin _srcMin;
    private readonly RollingWindowMax _volMax;
    private readonly RollingWindowMin _volMin;
    private readonly IMovingAverageSmoother _srcSma;
    private readonly IMovingAverageSmoother _volSma;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _af;
    private double _prevValue;
    private bool _hasPrev;

    public EnhancedWilliamsRState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        int signalLength = 5)
    {
        var resolved = Math.Max(2, length);
        _srcMax = new RollingWindowMax(resolved);
        _srcMin = new RollingWindowMin(resolved);
        _volMax = new RollingWindowMax(resolved);
        _volMin = new RollingWindowMin(resolved);
        _af = length < 10 ? 0.25 : ((double)length / 32) - 0.0625;
        var smaLength = MathHelper.MinOrMax((int)Math.Ceiling((double)length / 2));
        _srcSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _volSma = MovingAverageSmootherFactory.Create(maType, smaLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EnhancedWilliamsR;

    public void Reset()
    {
        _srcMax.Reset();
        _srcMin.Reset();
        _volMax.Reset();
        _volMin.Reset();
        _srcSma.Reset();
        _volSma.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = _hasPrev ? _prevValue : 0;

        var maxVol = isFinal ? _volMax.Add(volume, out _) : _volMax.Preview(volume, out _);
        var minVol = isFinal ? _volMin.Add(volume, out _) : _volMin.Preview(volume, out _);
        var maxSrc = isFinal ? _srcMax.Add(value, out _) : _srcMax.Preview(value, out _);
        var minSrc = isFinal ? _srcMin.Add(value, out _) : _srcMin.Preview(value, out _);
        var srcSma = _srcSma.Next(value, isFinal);
        var volSma = _volSma.Next(volume, isFinal);
        var volWr = maxVol - minVol != 0 ? 2 * ((volume - volSma) / (maxVol - minVol)) : 0;
        var srcWr = maxSrc - minSrc != 0 ? 2 * ((value - srcSma) / (maxSrc - minSrc)) : 0;
        var priceDiff = _hasPrev ? value - prevValue : 0;
        var srcSwr = maxSrc - minSrc != 0 ? 2 * (priceDiff / (maxSrc - minSrc)) : 0;

        var ewr = ((volWr > 0 && srcWr > 0 && value > prevValue) ||
            ((volWr > 0 && srcWr < 0 && value < prevValue)) && srcSwr + _af != 0
            ? ((50 * (srcWr * (srcSwr + _af) * volWr)) + srcSwr + _af) / (srcSwr + _af)
            : 25 * ((srcWr * (volWr + 1)) + 2));
        var signal = _signalSmoother.Next(ewr, isFinal);

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
                { "Ewr", ewr },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ewr, outputs);
    }

    public void Dispose()
    {
        _srcMax.Dispose();
        _srcMin.Dispose();
        _volMax.Dispose();
        _volMin.Dispose();
        _srcSma.Dispose();
        _volSma.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Eqma")]
public sealed class EquityMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _chgXSum;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevEqma;
    private double _prevX;
    private double _chgXCumSum;
    private bool _hasPrev;

    public EquityMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        var resolved = Math.Max(1, length);
        _chgXSum = new RollingWindowSum(resolved);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.EquityMovingAverage;

    public void Reset()
    {
        _chgXSum.Reset();
        _sma.Reset();
        _prevValue = 0;
        _prevEqma = 0;
        _prevX = 0;
        _chgXCumSum = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var sma = _sma.Next(value, isFinal);
        var prevEqma = _hasPrev ? _prevEqma : value;
        var prevX = _prevX;
        var x = Math.Sign(value - sma);
        var priceDiff = _hasPrev ? value - prevValue : 0;

        var chgX = priceDiff * prevX;
        var req = isFinal ? _chgXSum.Add(chgX, out _) : _chgXSum.Preview(chgX, out _);
        var chgXCum = priceDiff * x;
        var opteq = _chgXCumSum + chgXCum;
        var alpha = opteq != 0 ? MathHelper.MinOrMax(req / opteq, 0.99, 0.01) : 0.99;
        var eqma = (alpha * value) + ((1 - alpha) * prevEqma);

        if (isFinal)
        {
            _prevValue = value;
            _prevEqma = eqma;
            _prevX = x;
            _chgXCumSum = opteq;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Eqma", eqma }
            };
        }

        return new StreamingIndicatorStateResult(eqma, outputs);
    }

    public void Dispose()
    {
        _chgXSum.Dispose();
        _sma.Dispose();
    }
}

[PrimaryOutput("Eco")]
public sealed class ErgodicCandlestickOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _xcoEma1;
    private readonly IMovingAverageSmoother _xcoEma2;
    private readonly IMovingAverageSmoother _xhlEma1;
    private readonly IMovingAverageSmoother _xhlEma2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public ErgodicCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 32, int length2 = 12)
    {
        _xcoEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xcoEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _xhlEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _xhlEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicCandlestickOscillator;

    public void Reset()
    {
        _xcoEma1.Reset();
        _xcoEma2.Reset();
        _xhlEma1.Reset();
        _xhlEma2.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var xco = close - bar.Open;
        var xhl = bar.High - bar.Low;
        var xcoEma1 = _xcoEma1.Next(xco, isFinal);
        var xcoEma2 = _xcoEma2.Next(xcoEma1, isFinal);
        var xhlEma1 = _xhlEma1.Next(xhl, isFinal);
        var xhlEma2 = _xhlEma2.Next(xhlEma1, isFinal);
        var eco = xhlEma2 != 0 ? 100 * xcoEma2 / xhlEma2 : 0;
        var signal = _signalSmoother.Next(eco, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Eco", eco },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(eco, outputs);
    }

    public void Dispose()
    {
        _xcoEma1.Dispose();
        _xcoEma2.Dispose();
        _xhlEma1.Dispose();
        _xhlEma2.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Ecsi")]
public sealed class ErgodicCommoditySelectionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly IMovingAverageSmoother _dmPlus;
    private readonly IMovingAverageSmoother _dmMinus;
    private readonly IMovingAverageSmoother _tr;
    private readonly IMovingAverageSmoother _adx;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevHigh;
    private double _prevLow;
    private double _prevValue;
    private double _prevAdx;
    private bool _hasPrev;

    public ErgodicCommoditySelectionIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length = 32, int smoothLength = 5, double pointValue = 1)
    {
        _length = Math.Max(1, length);
        _k = 100 * (pointValue / MathHelper.Sqrt(_length) / (150 + smoothLength));
        _dmPlus = MovingAverageSmootherFactory.Create(maType, _length);
        _dmMinus = MovingAverageSmootherFactory.Create(maType, _length);
        _tr = MovingAverageSmootherFactory.Create(maType, _length);
        _adx = MovingAverageSmootherFactory.Create(maType, _length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicCommoditySelectionIndex;

    public void Reset()
    {
        _dmPlus.Reset();
        _dmMinus.Reset();
        _tr.Reset();
        _adx.Reset();
        _signalSmoother.Reset();
        _prevHigh = 0;
        _prevLow = 0;
        _prevValue = 0;
        _prevAdx = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        // Use 0 for prevHigh/prevLow on first bar to match batch ADX behavior
        var prevHigh = _hasPrev ? _prevHigh : bar.High;
        var prevLow = _hasPrev ? _prevLow : bar.Low;
        // For TrueRange, use current value on first bar to avoid inflated TR
        var prevValue = _hasPrev ? _prevValue : value;
        var prevAdx = _hasPrev ? _prevAdx : 0;

        var highDiff = bar.High - prevHigh;
        var lowDiff = prevLow - bar.Low;
        var dmPlus = highDiff > lowDiff ? Math.Max(highDiff, 0) : 0;
        var dmMinus = highDiff < lowDiff ? Math.Max(lowDiff, 0) : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);

        var dmPlusMa = _dmPlus.Next(dmPlus, isFinal);
        var dmMinusMa = _dmMinus.Next(dmMinus, isFinal);
        var trMa = _tr.Next(tr, isFinal);
        var diPlus = trMa != 0 ? MathHelper.MinOrMax(100 * dmPlusMa / trMa, 100, 0) : 0;
        var diMinus = trMa != 0 ? MathHelper.MinOrMax(100 * dmMinusMa / trMa, 100, 0) : 0;
        var diDiff = Math.Abs(diPlus - diMinus);
        var diSum = diPlus + diMinus;
        var dx = diSum != 0 ? MathHelper.MinOrMax(100 * diDiff / diSum, 100, 0) : 0;
        var adx = _adx.Next(dx, isFinal);
        var adxR = (adx + prevAdx) * 0.5;

        var csi = _length + tr > 0 ? _k * adxR * tr / _length : 0;
        var ergodicCsi = value > 0 ? csi / value : 0;
        var signal = _signalSmoother.Next(ergodicCsi, isFinal);

        if (isFinal)
        {
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevValue = value;
            _prevAdx = adx;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Ecsi", ergodicCsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(ergodicCsi, outputs);
    }

    public void Dispose()
    {
        _dmPlus.Dispose();
        _dmMinus.Dispose();
        _tr.Dispose();
        _adx.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Emdi")]
public sealed class ErgodicMeanDeviationIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly ResidualAverageWindow? _exact;
    private readonly IMovingAverageSmoother[]? _fallback;
    private readonly StreamingInputResolver _input;
    public ErgodicMeanDeviationIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 32, int length2 = 5, int length3 = 5, int signalLength = 5)
    {
        if (StrengthWindow.Supports(maType)) _exact = new ResidualAverageWindow(maType, length1, maType, length2, length3, signalLength);
        else _fallback = new[] { length1, length2, length3, signalLength }.Select(p => MovingAverageSmootherFactory.Create(maType, Math.Max(1, p))).ToArray();
        _input = new StreamingInputResolver(InputName.Close, null);
    }
    public IndicatorName Name => IndicatorName.ErgodicMeanDeviationIndicator;
    public void Reset() { _exact?.Reset(); if (_fallback is not null) foreach (var stage in _fallback) stage.Reset(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar); double line, signal;
        if (_exact is not null) (line, signal) = _exact.Next(value, isFinal);
        else
        {
            var mean = _fallback![0].Next(value, isFinal);
            var first = _fallback[1].Next(value - mean, isFinal); line = _fallback[2].Next(first, isFinal); signal = _fallback[3].Next(line, isFinal);
        }
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Emdi", line }, { "Signal", signal } } : null;
        return new StreamingIndicatorStateResult(line, outputs);
    }
    public void Dispose() { _exact?.Dispose(); if (_fallback is not null) foreach (var stage in _fallback) stage.Dispose(); }
}

[PrimaryOutput("Macd")]
public sealed class ErgodicMovingAverageConvergenceDivergenceState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public ErgodicMovingAverageConvergenceDivergenceState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 32, int length2 = 5,
        int length3 = 5)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicMovingAverageConvergenceDivergence;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _signalSmoother.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(value, isFinal);
        var macd = ema1 - ema2;
        var invalidSignal = _signalInvalid || double.IsInfinity(macd);
        var signal = invalidSignal ? double.NaN : _signalSmoother.Next(macd, isFinal);
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

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Ppo")]
public sealed class ErgodicPercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public ErgodicPercentagePriceOscillatorState(
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 32, int length2 = 5,
        int length3 = 5)
    {
        _ema1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _ema2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicPercentagePriceOscillator;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _signalSmoother.Reset();
        _signalInvalid = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(value, isFinal);
        var ppo = RoundedPercentageChange.Of(ema1, ema2);
        var invalidSignal = _signalInvalid || double.IsInfinity(ppo);
        var signal = invalidSignal ? double.NaN : _signalSmoother.Next(ppo, isFinal);
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
        _ema1.Dispose();
        _ema2.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Etsi")]
public sealed class ErgodicTrueStrengthIndexV1State : IStreamingIndicatorState, IDisposable
{
    private readonly StrengthWindow? _strength;
    private readonly StrengthAverage? _stableSignal;
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ErgodicTrueStrengthIndexV1State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 4, int length2 = 8, int length3 = 6, int signalLength = 3)
    {
        if (StrengthWindow.Supports(maType)) _strength = new StrengthWindow(maType, new[] { length1, length2, length3 });
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        if (StrengthWindow.Supports(maType)) _stableSignal = new StrengthAverage(maType, signalLength);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicTrueStrengthIndexV1;

    public void Reset()
    {
        _strength?.Reset();
        _stableSignal?.Reset();
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double etsi;
        if (_strength is not null) etsi = _strength.Next(value, isFinal);
        else
        {
            var prevValue = _hasPrev ? _prevValue : 0;
            var priceDiff = _hasPrev ? value - prevValue : 0;
            var absPriceDiff = Math.Abs(priceDiff);

            var diffEma1 = _diffEma1.Next(priceDiff, isFinal);
            var absDiffEma1 = _absDiffEma1.Next(absPriceDiff, isFinal);
            var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
            var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
            var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
            var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
            etsi = absDiffEma3 != 0 ? MathHelper.MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
        }
        var signal = _stableSignal is null ? _signalSmoother.Next(etsi, isFinal)
            : _stableSignal.Next(new StrengthValue(etsi), isFinal).Mantissa;

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
                { "Etsi", etsi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(etsi, outputs);
    }

    public void Dispose()
    {
        _strength?.Dispose();
        _stableSignal?.Dispose();
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Etsi2")]
public sealed class ErgodicTrueStrengthIndexV2State : IStreamingIndicatorState, IDisposable
{
    private readonly StrengthWindow? _strength1, _strength2;
    private readonly StrengthAverage? _stableSignal;
    private readonly IMovingAverageSmoother _diffEma1;
    private readonly IMovingAverageSmoother _absDiffEma1;
    private readonly IMovingAverageSmoother _diffEma2;
    private readonly IMovingAverageSmoother _absDiffEma2;
    private readonly IMovingAverageSmoother _diffEma3;
    private readonly IMovingAverageSmoother _absDiffEma3;
    private readonly IMovingAverageSmoother _diffEma4;
    private readonly IMovingAverageSmoother _absDiffEma4;
    private readonly IMovingAverageSmoother _diffEma5;
    private readonly IMovingAverageSmoother _absDiffEma5;
    private readonly IMovingAverageSmoother _diffEma6;
    private readonly IMovingAverageSmoother _absDiffEma6;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public ErgodicTrueStrengthIndexV2State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 21, int length2 = 9, int length3 = 9, int length4 = 17, int length5 = 6,
        int length6 = 2, int signalLength = 2)
    {
        if (StrengthWindow.Supports(maType))
        {
            _strength1 = new StrengthWindow(maType, new[] { length1, length2, length3 });
            _strength2 = new StrengthWindow(maType, new[] { length4, length5, length6 });
            _stableSignal = new StrengthAverage(maType, signalLength);
        }
        _diffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _absDiffEma1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length1));
        _diffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _absDiffEma2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        _diffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _absDiffEma3 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _diffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _absDiffEma4 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _diffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _absDiffEma5 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length5));
        _diffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _absDiffEma6 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length6));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ErgodicTrueStrengthIndexV2;

    public void Reset()
    {
        _strength1?.Reset(); _strength2?.Reset(); _stableSignal?.Reset();
        _diffEma1.Reset();
        _absDiffEma1.Reset();
        _diffEma2.Reset();
        _absDiffEma2.Reset();
        _diffEma3.Reset();
        _absDiffEma3.Reset();
        _diffEma4.Reset();
        _absDiffEma4.Reset();
        _diffEma5.Reset();
        _absDiffEma5.Reset();
        _diffEma6.Reset();
        _absDiffEma6.Reset();
        _signalSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double etsi1, etsi2;
        if (_strength1 is not null && _strength2 is not null)
        {
            etsi1 = _strength1.Next(value, isFinal);
            etsi2 = _strength2.Next(value, isFinal);
        }
        else
        {
            var prevValue = _hasPrev ? _prevValue : 0;
            var priceDiff = _hasPrev ? value - prevValue : 0;
            var absPriceDiff = Math.Abs(priceDiff);

            var diffEma1 = _diffEma1.Next(priceDiff, isFinal);
            var absDiffEma1 = _absDiffEma1.Next(absPriceDiff, isFinal);
            var diffEma4 = _diffEma4.Next(priceDiff, isFinal);
            var absDiffEma4 = _absDiffEma4.Next(absPriceDiff, isFinal);
            var diffEma2 = _diffEma2.Next(diffEma1, isFinal);
            var absDiffEma2 = _absDiffEma2.Next(absDiffEma1, isFinal);
            var diffEma5 = _diffEma5.Next(diffEma4, isFinal);
            var absDiffEma5 = _absDiffEma5.Next(absDiffEma4, isFinal);
            var diffEma3 = _diffEma3.Next(diffEma2, isFinal);
            var absDiffEma3 = _absDiffEma3.Next(absDiffEma2, isFinal);
            var diffEma6 = _diffEma6.Next(diffEma5, isFinal);
            var absDiffEma6 = _absDiffEma6.Next(absDiffEma5, isFinal);
            etsi1 = absDiffEma3 != 0 ? MathHelper.MinOrMax(diffEma3 / absDiffEma3 * 100, 100, -100) : 0;
            etsi2 = absDiffEma6 != 0 ? MathHelper.MinOrMax(diffEma6 / absDiffEma6 * 100, 100, -100) : 0;
        }
        var signal = _stableSignal is null ? _signalSmoother.Next(etsi2, isFinal)
            : _stableSignal.Next(new StrengthValue(etsi2), isFinal).Mantissa;

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Etsi1", etsi1 },
                { "Etsi2", etsi2 },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(etsi2, outputs);
    }

    public void Dispose()
    {
        _strength1?.Dispose(); _strength2?.Dispose(); _stableSignal?.Dispose();
        _diffEma1.Dispose();
        _absDiffEma1.Dispose();
        _diffEma2.Dispose();
        _absDiffEma2.Dispose();
        _diffEma3.Dispose();
        _absDiffEma3.Dispose();
        _diffEma4.Dispose();
        _absDiffEma4.Dispose();
        _diffEma5.Dispose();
        _absDiffEma5.Dispose();
        _diffEma6.Dispose();
        _absDiffEma6.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Frf")]
public sealed class FallingRisingFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly double _alpha;
    private readonly RollingWindowMax _tempMax;
    private readonly RollingWindowMin _tempMin;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevA;
    private double _prevError;
    private bool _hasPrev;

    public FallingRisingFilterState(int length = 14)
    {
        var resolved = Math.Max(2, length);
        _alpha = (double)2 / (resolved + 1);
        _tempMax = new RollingWindowMax(resolved);
        _tempMin = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FallingRisingFilter;

    public void Reset()
    {
        _tempMax.Reset();
        _tempMin.Reset();
        _prevValue = 0;
        _prevA = 0;
        _prevError = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevA = _hasPrev ? _prevA : 0;
        var prevError = _hasPrev ? _prevError : 0;
        var maxPrev = isFinal ? _tempMax.Add(prevValue, out _) : _tempMax.Preview(prevValue, out _);
        var minPrev = isFinal ? _tempMin.Add(prevValue, out _) : _tempMin.Preview(prevValue, out _);
        var beta = value > maxPrev || value < minPrev ? 1 : _alpha;
        var a = prevA + (_alpha * prevError) + (beta * prevError);
        var error = value - a;

        if (isFinal)
        {
            _prevValue = value;
            _prevA = a;
            _prevError = error;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Frf", a }
            };
        }

        return new StreamingIndicatorStateResult(a, outputs);
    }

    public void Dispose()
    {
        _tempMax.Dispose();
        _tempMin.Dispose();
    }
}

[PrimaryOutput("Fswma")]
public sealed class FareySequenceWeightedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly FareyWindowMean _mean;
    private readonly StreamingInputResolver _input;

    public FareySequenceWeightedMovingAverageState(int length = 5)
    {
        _mean = new FareyWindowMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FareySequenceWeightedMovingAverage;

    public void Reset()
    {
        _mean.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fswma = _mean.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Fswma", fswma }
            };
        }

        return new StreamingIndicatorStateResult(fswma, outputs);
    }

    public void Dispose()
    {
        _mean.Dispose();
    }
}

[PrimaryOutput("Fsk")]
public sealed class FastandSlowKurtosisOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _ratio;
    private readonly PooledRingBuffer<double> _values;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevMomentum;
    private double _prevFsk;

    public FastandSlowKurtosisOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 3, double ratio = 0.03)
    {
        _length = Math.Max(1, length);
        _ratio = ratio;
        _values = new PooledRingBuffer<double>(_length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.FastandSlowKurtosisOscillator;

    public void Reset()
    {
        _values.Clear();
        _signalSmoother.Reset();
        _prevMomentum = 0;
        _prevFsk = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var hasMomentum = _values.Count >= _length;
        var prevValue = hasMomentum ? EhlersStreamingWindow.GetOffsetValue(_values, value, _length) : 0;
        var momentum = hasMomentum ? value - prevValue : 0;
        var fsk = (_ratio * (momentum - _prevMomentum)) + ((1 - _ratio) * _prevFsk);
        var signal = _signalSmoother.Next(fsk, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _prevMomentum = momentum;
            _prevFsk = fsk;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Fsk", fsk },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(fsk, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _signalSmoother.Dispose();
    }
}
