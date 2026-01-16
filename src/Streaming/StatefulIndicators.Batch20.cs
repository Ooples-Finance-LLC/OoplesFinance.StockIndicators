using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public sealed class PremierStochasticOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ssSmoother;
    private readonly IMovingAverageSmoother _nskSmoother;
    private readonly StochasticOscillatorState _stochastic;

    public PremierStochasticOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 8, int smoothLength = 25, InputName inputName = InputName.Close)
    {
        var resolved = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(smoothLength)));
        _stochastic = new StochasticOscillatorState(maType, Math.Max(1, length), 3, 3, inputName);
        _nskSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _ssSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public PremierStochasticOscillatorState(MovingAvgType maType, int length, int smoothLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(smoothLength)));
        _stochastic = new StochasticOscillatorState(maType, Math.Max(1, length), 3, 3, selector);
        _nskSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _ssSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
    }

    public IndicatorName Name => IndicatorName.PremierStochasticOscillator;

    public void Reset()
    {
        _stochastic.Reset();
        _nskSmoother.Reset();
        _ssSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var sk = _stochastic.Update(bar, isFinal, includeOutputs: false).Value;
        var nsk = 0.1 * (sk - 50);
        var nskEma = _nskSmoother.Next(nsk, isFinal);
        var ss = _ssSmoother.Next(nskEma, isFinal);
        var exp = MathHelper.Exp(ss);
        var pso = exp + 1 != 0 ? MathHelper.MinOrMax((exp - 1) / (exp + 1), 1, -1) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pso", pso }
            };
        }

        return new StreamingIndicatorStateResult(pso, outputs);
    }

    public void Dispose()
    {
        _stochastic.Dispose();
        _nskSmoother.Dispose();
        _ssSmoother.Dispose();
    }
}

public sealed class PrettyGoodOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly AverageTrueRangeSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;

    public PrettyGoodOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = new AverageTrueRangeSmoother(maType, resolved, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PrettyGoodOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _sma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = new AverageTrueRangeSmoother(maType, resolved, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PrettyGoodOscillator;

    public void Reset()
    {
        _sma.Reset();
        _atrSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var atr = _atrSmoother.Next(bar, isFinal);
        var pgo = atr != 0 ? (value - sma) / atr : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pgo", pgo }
            };
        }

        return new StreamingIndicatorStateResult(pgo, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class PriceCycleOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _diffSma;
    private readonly AverageTrueRangeSmoother _atrSmoother;
    private readonly StreamingInputResolver _input;

    public PriceCycleOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 22,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _diffSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = new AverageTrueRangeSmoother(maType, resolved, inputName);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceCycleOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _diffSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _atrSmoother = new AverageTrueRangeSmoother(maType, resolved, selector);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceCycleOscillator;

    public void Reset()
    {
        _diffSma.Reset();
        _atrSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var diff = value - bar.Low;
        var diffSma = _diffSma.Next(diff, isFinal);
        var atr = _atrSmoother.Next(bar, isFinal);
        var pco = atr != 0 ? diffSma / atr * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pco", pco }
            };
        }

        return new StreamingIndicatorStateResult(pco, outputs);
    }

    public void Dispose()
    {
        _diffSma.Dispose();
        _atrSmoother.Dispose();
    }
}

public sealed class PriceMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _sc1;
    private readonly double _sc2;
    private double _prevValue;
    private double _prevRocMa;
    private double _prevPmo;
    private bool _hasPrev;

    public PriceMomentumOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 35,
        int length2 = 20, int signalLength = 10, InputName inputName = InputName.Close)
    {
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _sc1 = 2d / resolvedLength1;
        _sc2 = 2d / resolvedLength2;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceMomentumOscillatorState(MovingAvgType maType, int length1, int length2, int signalLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _sc1 = 2d / resolvedLength1;
        _sc2 = 2d / resolvedLength2;
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceMomentumOscillator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevValue = 0;
        _prevRocMa = 0;
        _prevPmo = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var roc = prevValue != 0 ? (value - prevValue) / prevValue * 100 : 0;
        var rocMa = _prevRocMa + ((roc - _prevRocMa) * _sc1);
        var pmo = _prevPmo + (((rocMa * 10) - _prevPmo) * _sc2);
        var signal = _signalSmoother.Next(pmo, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevRocMa = rocMa;
            _prevPmo = pmo;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pmo", pmo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pmo, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class PriceVolumeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly RollingWindowSum _aSum;
    private readonly RollingWindowSum _bSum;
    private readonly RollingWindowSum _absASum;
    private readonly RollingWindowSum _absBSum;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _volumes;
    private readonly StreamingInputResolver _input;

    public PriceVolumeOscillatorState(int length1 = 50, int length2 = 14, InputName inputName = InputName.Close)
    {
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _aSum = new RollingWindowSum(_length1);
        _bSum = new RollingWindowSum(_length2);
        _absASum = new RollingWindowSum(_length1);
        _absBSum = new RollingWindowSum(_length2);
        _values = new PooledRingBuffer<double>(_length1);
        _volumes = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceVolumeOscillatorState(int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _aSum = new RollingWindowSum(_length1);
        _bSum = new RollingWindowSum(_length2);
        _absASum = new RollingWindowSum(_length1);
        _absBSum = new RollingWindowSum(_length2);
        _values = new PooledRingBuffer<double>(_length1);
        _volumes = new PooledRingBuffer<double>(_length2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceVolumeOscillator;

    public void Reset()
    {
        _aSum.Reset();
        _bSum.Reset();
        _absASum.Reset();
        _absBSum.Reset();
        _values.Clear();
        _volumes.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length1);
        var prevVolume = EhlersStreamingWindow.GetOffsetValue(_volumes, volume, _length2);
        var hasValue = _values.Count >= _length1;
        var hasVolume = _volumes.Count >= _length2;
        var a = hasValue ? value - prevValue : 0;
        var b = hasVolume ? volume - prevVolume : 0;
        var absA = Math.Abs(a);
        var absB = Math.Abs(b);
        var aSum = isFinal ? _aSum.Add(a, out _) : _aSum.Preview(a, out _);
        var bSum = isFinal ? _bSum.Add(b, out _) : _bSum.Preview(b, out _);
        var absASum = isFinal ? _absASum.Add(absA, out _) : _absASum.Preview(absA, out _);
        var absBSum = isFinal ? _absBSum.Add(absB, out _) : _absBSum.Preview(absB, out _);
        var oscA = absASum != 0 ? aSum / absASum : 0;
        var oscB = absBSum != 0 ? bSum / absBSum : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _volumes.TryAdd(volume, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Po", oscA },
                { "Vo", oscB }
            };
        }

        return new StreamingIndicatorStateResult(oscA, outputs);
    }

    public void Dispose()
    {
        _aSum.Dispose();
        _bSum.Dispose();
        _absASum.Dispose();
        _absBSum.Dispose();
        _values.Dispose();
        _volumes.Dispose();
    }
}

public sealed class PriceVolumeRankState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevVolume;
    private bool _hasPrev;

    public PriceVolumeRankState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5,
        int slowLength = 10, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceVolumeRankState(MovingAvgType maType, int fastLength, int slowLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceVolumeRank;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _prevValue = 0;
        _prevVolume = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevVolume = _hasPrev ? _prevVolume : 0;

        double pvr = value > prevValue && volume > prevVolume ? 1
            : value > prevValue && volume <= prevVolume ? 2
            : value <= prevValue && volume <= prevVolume ? 3
            : 4;
        var fast = _fastSmoother.Next(pvr, isFinal);
        var slow = _slowSmoother.Next(pvr, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevVolume = volume;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Pvr", pvr },
                { "SlowSignal", slow },
                { "FastSignal", fast }
            };
        }

        return new StreamingIndicatorStateResult(pvr, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

public sealed class PriceVolumeTrendState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevPvt;
    private bool _hasPrev;

    public PriceVolumeTrendState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        InputName inputName = InputName.Close)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceVolumeTrendState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceVolumeTrend;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevValue = 0;
        _prevPvt = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevPvt = _hasPrev ? _prevPvt : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var pvt = prevValue != 0 ? prevPvt + (bar.Volume * (diff / prevValue)) : prevPvt;
        var signal = _signalSmoother.Next(pvt, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _prevPvt = pvt;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Pvt", pvt },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(pvt, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

public sealed class PriceZoneOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _vmaSmoother;
    private readonly IMovingAverageSmoother _dvmaSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public PriceZoneOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _vmaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _dvmaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PriceZoneOscillatorState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _vmaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _dvmaSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PriceZoneOscillator;

    public void Reset()
    {
        _vmaSmoother.Reset();
        _dvmaSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var diff = _hasPrev ? value - prevValue : 0;
        var dvol = Math.Sign(diff) * value;
        var vma = _vmaSmoother.Next(value, isFinal);
        var dvma = _dvmaSmoother.Next(dvol, isFinal);
        var pzo = vma != 0 ? MathHelper.MinOrMax(100 * dvma / vma, 100, -100) : 0;

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
                { "Pzo", pzo }
            };
        }

        return new StreamingIndicatorStateResult(pzo, outputs);
    }

    public void Dispose()
    {
        _vmaSmoother.Dispose();
        _dvmaSmoother.Dispose();
    }
}

public sealed class PrimeNumberBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PrimeNumberOscillatorState _upperPno;
    private readonly PrimeNumberOscillatorState _lowerPno;
    private readonly RollingWindowMax _upperWindow;
    private readonly RollingWindowMin _lowerWindow;
    private readonly StreamingInputResolver _input;

    public PrimeNumberBandsState(int length = 5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _upperPno = new PrimeNumberOscillatorState(_length, bar => bar.High);
        _lowerPno = new PrimeNumberOscillatorState(_length, bar => bar.Low);
        _upperWindow = new RollingWindowMax(_length);
        _lowerWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PrimeNumberBandsState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _upperPno = new PrimeNumberOscillatorState(_length, bar => bar.High);
        _lowerPno = new PrimeNumberOscillatorState(_length, bar => bar.Low);
        _upperWindow = new RollingWindowMax(_length);
        _lowerWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PrimeNumberBands;

    public void Reset()
    {
        _upperPno.Reset();
        _lowerPno.Reset();
        _upperWindow.Reset();
        _lowerWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var upperPno = _upperPno.Update(bar, isFinal, includeOutputs: false).Value;
        var lowerPno = _lowerPno.Update(bar, isFinal, includeOutputs: false).Value;
        var upper = isFinal ? _upperWindow.Add(upperPno, out _) : _upperWindow.Preview(upperPno, out _);
        var lower = isFinal ? _lowerWindow.Add(lowerPno, out _) : _lowerWindow.Preview(lowerPno, out _);

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

    public void Dispose()
    {
        _upperPno.Dispose();
        _lowerPno.Dispose();
        _upperWindow.Dispose();
        _lowerWindow.Dispose();
    }
}

public sealed class PrimeNumberOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly StreamingInputResolver _input;
    private double _prevPno1;
    private double _prevPno2;
    private double _prevPno;
    private bool _hasPrev;

    public PrimeNumberOscillatorState(int length = 5, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PrimeNumberOscillatorState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PrimeNumberOscillator;

    public void Reset()
    {
        _prevPno1 = 0;
        _prevPno2 = 0;
        _prevPno = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ratio = value * _length / 100;
        var convertedValue = (long)Math.Round(value);
        var sqrtValue = value >= 0 ? (long)Math.Round(MathHelper.Sqrt(value)) : 0;
        var maxValue = (long)Math.Round(value + ratio);
        var minValue = (long)Math.Round(value - ratio);

        double pno1 = 0;
        for (var j = convertedValue; j <= maxValue; j++)
        {
            pno1 = j;
            for (var k = 2; k <= sqrtValue; k++)
            {
                pno1 = j % k == 0 ? 0 : j;
                if (pno1 == 0)
                {
                    break;
                }
            }

            if (pno1 > 0)
            {
                break;
            }
        }

        if (pno1 == 0)
        {
            pno1 = _hasPrev ? _prevPno1 : 0;
        }

        double pno2 = 0;
        for (var l = convertedValue; l >= minValue; l--)
        {
            pno2 = l;
            for (var m = 2; m <= sqrtValue; m++)
            {
                pno2 = l % m == 0 ? 0 : l;
                if (pno2 == 0)
                {
                    break;
                }
            }

            if (pno2 > 0)
            {
                break;
            }
        }

        if (pno2 == 0)
        {
            pno2 = _hasPrev ? _prevPno2 : 0;
        }

        var pno = pno1 - value < value - pno2 ? pno1 - value : pno2 - value;
        if (pno == 0)
        {
            pno = _hasPrev ? _prevPno : 0;
        }

        if (isFinal)
        {
            _prevPno1 = pno1;
            _prevPno2 = pno2;
            _prevPno = pno;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Pno", pno }
            };
        }

        return new StreamingIndicatorStateResult(pno, outputs);
    }

    public void Dispose()
    {
    }
}

public sealed class PringSpecialKState : IStreamingIndicatorState, IDisposable
{
    private readonly RateOfChangeState _roc10;
    private readonly RateOfChangeState _roc15;
    private readonly RateOfChangeState _roc20;
    private readonly RateOfChangeState _roc30;
    private readonly RateOfChangeState _roc40;
    private readonly RateOfChangeState _roc65;
    private readonly RateOfChangeState _roc75;
    private readonly RateOfChangeState _roc100;
    private readonly RateOfChangeState _roc195;
    private readonly RateOfChangeState _roc265;
    private readonly RateOfChangeState _roc390;
    private readonly RateOfChangeState _roc530;
    private readonly IMovingAverageSmoother _roc10Sma;
    private readonly IMovingAverageSmoother _roc15Sma;
    private readonly IMovingAverageSmoother _roc20Sma;
    private readonly IMovingAverageSmoother _roc30Sma;
    private readonly IMovingAverageSmoother _roc40Sma;
    private readonly IMovingAverageSmoother _roc65Sma;
    private readonly IMovingAverageSmoother _roc75Sma;
    private readonly IMovingAverageSmoother _roc100Sma;
    private readonly IMovingAverageSmoother _roc195Sma;
    private readonly IMovingAverageSmoother _roc265Sma;
    private readonly IMovingAverageSmoother _roc390Sma;
    private readonly IMovingAverageSmoother _roc530Sma;
    private readonly IMovingAverageSmoother _signalSmoother;

    public PringSpecialKState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 10, int length2 = 15,
        int length3 = 20, int length4 = 30, int length5 = 40, int length6 = 50, int length7 = 65, int length8 = 75,
        int length9 = 100, int length10 = 130, int length11 = 195, int length12 = 265, int length13 = 390, int length14 = 530,
        int smoothLength = 10, InputName inputName = InputName.Close)
    {
        var len1 = Math.Max(1, length1);
        var len2 = Math.Max(1, length2);
        var len3 = Math.Max(1, length3);
        var len4 = Math.Max(1, length4);
        var len5 = Math.Max(1, length5);
        var len6 = Math.Max(1, length6);
        var len7 = Math.Max(1, length7);
        var len8 = Math.Max(1, length8);
        var len9 = Math.Max(1, length9);
        var len10 = Math.Max(1, length10);
        var len11 = Math.Max(1, length11);
        var len12 = Math.Max(1, length12);
        var len13 = Math.Max(1, length13);
        var len14 = Math.Max(1, length14);
        _roc10 = new RateOfChangeState(len1, inputName);
        _roc15 = new RateOfChangeState(len2, inputName);
        _roc20 = new RateOfChangeState(len3, inputName);
        _roc30 = new RateOfChangeState(len4, inputName);
        _roc40 = new RateOfChangeState(len5, inputName);
        _roc65 = new RateOfChangeState(len7, inputName);
        _roc75 = new RateOfChangeState(len8, inputName);
        _roc100 = new RateOfChangeState(len9, inputName);
        _roc195 = new RateOfChangeState(len11, inputName);
        _roc265 = new RateOfChangeState(len12, inputName);
        _roc390 = new RateOfChangeState(len13, inputName);
        _roc530 = new RateOfChangeState(len14, inputName);
        _roc10Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc15Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc20Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc30Sma = MovingAverageSmootherFactory.Create(maType, len2);
        _roc40Sma = MovingAverageSmootherFactory.Create(maType, len6);
        _roc65Sma = MovingAverageSmootherFactory.Create(maType, len7);
        _roc75Sma = MovingAverageSmootherFactory.Create(maType, len8);
        _roc100Sma = MovingAverageSmootherFactory.Create(maType, len9);
        _roc195Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc265Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc390Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc530Sma = MovingAverageSmootherFactory.Create(maType, len11);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
    }

    public PringSpecialKState(MovingAvgType maType, int length1, int length2, int length3, int length4, int length5, int length6,
        int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14,
        int smoothLength, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var len1 = Math.Max(1, length1);
        var len2 = Math.Max(1, length2);
        var len3 = Math.Max(1, length3);
        var len4 = Math.Max(1, length4);
        var len5 = Math.Max(1, length5);
        var len6 = Math.Max(1, length6);
        var len7 = Math.Max(1, length7);
        var len8 = Math.Max(1, length8);
        var len9 = Math.Max(1, length9);
        var len10 = Math.Max(1, length10);
        var len11 = Math.Max(1, length11);
        var len12 = Math.Max(1, length12);
        var len13 = Math.Max(1, length13);
        var len14 = Math.Max(1, length14);
        _roc10 = new RateOfChangeState(len1, selector);
        _roc15 = new RateOfChangeState(len2, selector);
        _roc20 = new RateOfChangeState(len3, selector);
        _roc30 = new RateOfChangeState(len4, selector);
        _roc40 = new RateOfChangeState(len5, selector);
        _roc65 = new RateOfChangeState(len7, selector);
        _roc75 = new RateOfChangeState(len8, selector);
        _roc100 = new RateOfChangeState(len9, selector);
        _roc195 = new RateOfChangeState(len11, selector);
        _roc265 = new RateOfChangeState(len12, selector);
        _roc390 = new RateOfChangeState(len13, selector);
        _roc530 = new RateOfChangeState(len14, selector);
        _roc10Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc15Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc20Sma = MovingAverageSmootherFactory.Create(maType, len1);
        _roc30Sma = MovingAverageSmootherFactory.Create(maType, len2);
        _roc40Sma = MovingAverageSmootherFactory.Create(maType, len6);
        _roc65Sma = MovingAverageSmootherFactory.Create(maType, len7);
        _roc75Sma = MovingAverageSmootherFactory.Create(maType, len8);
        _roc100Sma = MovingAverageSmootherFactory.Create(maType, len9);
        _roc195Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc265Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc390Sma = MovingAverageSmootherFactory.Create(maType, len10);
        _roc530Sma = MovingAverageSmootherFactory.Create(maType, len11);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
    }

    public IndicatorName Name => IndicatorName.PringSpecialK;

    public void Reset()
    {
        _roc10.Reset();
        _roc15.Reset();
        _roc20.Reset();
        _roc30.Reset();
        _roc40.Reset();
        _roc65.Reset();
        _roc75.Reset();
        _roc100.Reset();
        _roc195.Reset();
        _roc265.Reset();
        _roc390.Reset();
        _roc530.Reset();
        _roc10Sma.Reset();
        _roc15Sma.Reset();
        _roc20Sma.Reset();
        _roc30Sma.Reset();
        _roc40Sma.Reset();
        _roc65Sma.Reset();
        _roc75Sma.Reset();
        _roc100Sma.Reset();
        _roc195Sma.Reset();
        _roc265Sma.Reset();
        _roc390Sma.Reset();
        _roc530Sma.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var roc10 = _roc10.Update(bar, isFinal, includeOutputs: false).Value;
        var roc15 = _roc15.Update(bar, isFinal, includeOutputs: false).Value;
        var roc20 = _roc20.Update(bar, isFinal, includeOutputs: false).Value;
        var roc30 = _roc30.Update(bar, isFinal, includeOutputs: false).Value;
        var roc40 = _roc40.Update(bar, isFinal, includeOutputs: false).Value;
        var roc65 = _roc65.Update(bar, isFinal, includeOutputs: false).Value;
        var roc75 = _roc75.Update(bar, isFinal, includeOutputs: false).Value;
        var roc100 = _roc100.Update(bar, isFinal, includeOutputs: false).Value;
        var roc195 = _roc195.Update(bar, isFinal, includeOutputs: false).Value;
        var roc265 = _roc265.Update(bar, isFinal, includeOutputs: false).Value;
        var roc390 = _roc390.Update(bar, isFinal, includeOutputs: false).Value;
        var roc530 = _roc530.Update(bar, isFinal, includeOutputs: false).Value;
        var roc10Sma = _roc10Sma.Next(roc10, isFinal);
        var roc15Sma = _roc15Sma.Next(roc15, isFinal);
        var roc20Sma = _roc20Sma.Next(roc20, isFinal);
        var roc30Sma = _roc30Sma.Next(roc30, isFinal);
        var roc40Sma = _roc40Sma.Next(roc40, isFinal);
        var roc65Sma = _roc65Sma.Next(roc65, isFinal);
        var roc75Sma = _roc75Sma.Next(roc75, isFinal);
        var roc100Sma = _roc100Sma.Next(roc100, isFinal);
        var roc195Sma = _roc195Sma.Next(roc195, isFinal);
        var roc265Sma = _roc265Sma.Next(roc265, isFinal);
        var roc390Sma = _roc390Sma.Next(roc390, isFinal);
        var roc530Sma = _roc530Sma.Next(roc530, isFinal);
        var specialK = (roc10Sma * 1) + (roc15Sma * 2) + (roc20Sma * 3) + (roc30Sma * 4) + (roc40Sma * 1)
            + (roc65Sma * 2) + (roc75Sma * 3) + (roc100Sma * 4) + (roc195Sma * 1) + (roc265Sma * 2)
            + (roc390Sma * 3) + (roc530Sma * 4);
        var signal = _signalSmoother.Next(specialK, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "PringSpecialK", specialK },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(specialK, outputs);
    }

    public void Dispose()
    {
        _roc10.Dispose();
        _roc15.Dispose();
        _roc20.Dispose();
        _roc30.Dispose();
        _roc40.Dispose();
        _roc65.Dispose();
        _roc75.Dispose();
        _roc100.Dispose();
        _roc195.Dispose();
        _roc265.Dispose();
        _roc390.Dispose();
        _roc530.Dispose();
        _roc10Sma.Dispose();
        _roc15Sma.Dispose();
        _roc20Sma.Dispose();
        _roc30Sma.Dispose();
        _roc40Sma.Dispose();
        _roc65Sma.Dispose();
        _roc75Sma.Dispose();
        _roc100Sma.Dispose();
        _roc195Sma.Dispose();
        _roc265Sma.Dispose();
        _roc390Sma.Dispose();
        _roc530Sma.Dispose();
        _signalSmoother.Dispose();
    }
}

public sealed class PsychologicalLineState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _condSum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public PsychologicalLineState(int length = 20, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _condSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public PsychologicalLineState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _condSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.PsychologicalLine;

    public void Reset()
    {
        _condSum.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        double cond = value > prevValue ? 1 : 0;
        var condSum = isFinal ? _condSum.Add(cond, out _) : _condSum.Preview(cond, out _);
        var psy = _length != 0 ? condSum / _length * 100 : 0;

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
                { "Pl", psy }
            };
        }

        return new StreamingIndicatorStateResult(psy, outputs);
    }

    public void Dispose()
    {
        _condSum.Dispose();
    }
}

public sealed class QmaSmaDifferenceState : IStreamingIndicatorState, IDisposable
{
    private readonly QuadraticMovingAverageState _qma;
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;

    public QmaSmaDifferenceState(int length = 14, InputName inputName = InputName.Close)
    {
        _qma = new QuadraticMovingAverageState(length, inputName);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public QmaSmaDifferenceState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _qma = new QuadraticMovingAverageState(length, selector);
        _sma = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.QmaSmaDifference;

    public void Reset()
    {
        _qma.Reset();
        _sma.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var qma = _qma.Update(bar, isFinal, includeOutputs: false).Value;
        var sma = _sma.Next(value, isFinal);
        var diff = qma - sma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "QmaSmaDiff", diff }
            };
        }

        return new StreamingIndicatorStateResult(diff, outputs);
    }

    public void Dispose()
    {
        _qma.Dispose();
        _sma.Dispose();
    }
}

public sealed class QuadraticLeastSquaresMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _forecastLength;
    private readonly IMovingAverageSmoother _sma;
    private readonly IMovingAverageSmoother _nMa;
    private readonly IMovingAverageSmoother _n2Ma;
    private readonly IMovingAverageSmoother _nn2Ma;
    private readonly IMovingAverageSmoother _n2vMa;
    private readonly IMovingAverageSmoother _nvMa;
    private readonly StandardDeviationVolatilityState _nStdDev;
    private readonly StandardDeviationVolatilityState _n2StdDev;
    private readonly StreamingInputResolver _input;
    private int _index;
    private double _nValue;
    private double _n2Value;

    public QuadraticLeastSquaresMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 550,
        int forecastLength = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _forecastLength = Math.Max(1, forecastLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _nMa = MovingAverageSmootherFactory.Create(maType, _length);
        _n2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _nn2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _n2vMa = MovingAverageSmootherFactory.Create(maType, _length);
        _nvMa = MovingAverageSmootherFactory.Create(maType, _length);
        _nStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _nValue);
        _n2StdDev = new StandardDeviationVolatilityState(maType, _length, _ => _n2Value);
        _input = new StreamingInputResolver(inputName, null);
    }

    public QuadraticLeastSquaresMovingAverageState(MovingAvgType maType, int length, int forecastLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _forecastLength = Math.Max(1, forecastLength);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _nMa = MovingAverageSmootherFactory.Create(maType, _length);
        _n2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _nn2Ma = MovingAverageSmootherFactory.Create(maType, _length);
        _n2vMa = MovingAverageSmootherFactory.Create(maType, _length);
        _nvMa = MovingAverageSmootherFactory.Create(maType, _length);
        _nStdDev = new StandardDeviationVolatilityState(maType, _length, _ => _nValue);
        _n2StdDev = new StandardDeviationVolatilityState(maType, _length, _ => _n2Value);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.QuadraticLeastSquaresMovingAverage;

    public void Reset()
    {
        _sma.Reset();
        _nMa.Reset();
        _n2Ma.Reset();
        _nn2Ma.Reset();
        _n2vMa.Reset();
        _nvMa.Reset();
        _nStdDev.Reset();
        _n2StdDev.Reset();
        _index = 0;
        _nValue = 0;
        _n2Value = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var n = (double)_index;
        var n2 = n * n;
        _nValue = n;
        _n2Value = n2;
        var nn2 = n * n2;
        var n2v = n2 * value;
        var nv = n * value;
        var nSma = _nMa.Next(n, isFinal);
        var n2Sma = _n2Ma.Next(n2, isFinal);
        var nn2Sma = _nn2Ma.Next(nn2, isFinal);
        var n2vSma = _n2vMa.Next(n2v, isFinal);
        var nvSma = _nvMa.Next(nv, isFinal);
        var sma = _sma.Next(value, isFinal);
        var nn2Cov = nn2Sma - (nSma * n2Sma);
        var n2vCov = n2vSma - (n2Sma * sma);
        var nvCov = nvSma - (nSma * sma);
        var nVariance = _nStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var n2Variance = _n2StdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var norm = (n2Variance * nVariance) - MathHelper.Pow(nn2Cov, 2);
        var a = norm != 0 ? ((n2vCov * nVariance) - (nvCov * nn2Cov)) / norm : 0;
        var b = norm != 0 ? ((nvCov * n2Variance) - (n2vCov * nn2Cov)) / norm : 0;
        var c = sma - (a * n2Sma) - (b * nSma);
        var qlsma = (a * n2) + (b * n) + c;
        var forecast = (a * MathHelper.Pow(n + _forecastLength, 2)) + (b * (n + _forecastLength)) + c;

        if (isFinal)
        {
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Qlma", qlsma },
                { "Forecast", forecast }
            };
        }

        return new StreamingIndicatorStateResult(qlsma, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
        _nMa.Dispose();
        _n2Ma.Dispose();
        _nn2Ma.Dispose();
        _n2vMa.Dispose();
        _nvMa.Dispose();
        _nStdDev.Dispose();
        _n2StdDev.Dispose();
    }
}

public sealed class QuadraticMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowSum _sqSum;
    private readonly StreamingInputResolver _input;

    public QuadraticMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sqSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public QuadraticMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sqSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.QuadraticMovingAverage;

    public void Reset()
    {
        _sqSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var pow = MathHelper.Pow(value, 2);
        var sum = isFinal ? _sqSum.Add(pow, out var countAfter) : _sqSum.Preview(pow, out countAfter);
        var avg = countAfter > 0 ? sum / countAfter : 0;
        var qma = avg >= 0 ? MathHelper.Sqrt(avg) : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Qma", qma }
            };
        }

        return new StreamingIndicatorStateResult(qma, outputs);
    }

    public void Dispose()
    {
        _sqSum.Dispose();
    }
}

public sealed class QuadraticRegressionState : IStreamingIndicatorState, IDisposable
{
    private readonly QuadraticRegressionEngine _engine;

    public QuadraticRegressionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 500,
        InputName inputName = InputName.Close)
    {
        _engine = new QuadraticRegressionEngine(maType, length, inputName);
    }

    public QuadraticRegressionState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        _engine = new QuadraticRegressionEngine(maType, length, selector);
    }

    public IndicatorName Name => IndicatorName.QuadraticRegression;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var quadReg = _engine.Next(bar, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "QuadReg", quadReg }
            };
        }

        return new StreamingIndicatorStateResult(quadReg, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

public sealed class QuadrupleExponentialMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema1;
    private readonly IMovingAverageSmoother _ema2;
    private readonly IMovingAverageSmoother _ema3;
    private readonly IMovingAverageSmoother _ema4;
    private readonly IMovingAverageSmoother _ema5;
    private readonly StreamingInputResolver _input;

    public QuadrupleExponentialMovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        InputName inputName = InputName.Close)
    {
        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema4 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(inputName, null);
    }

    public QuadrupleExponentialMovingAverageState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved = Math.Max(1, length);
        _ema1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema3 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema4 = MovingAverageSmootherFactory.Create(maType, resolved);
        _ema5 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.QuadrupleExponentialMovingAverage;

    public void Reset()
    {
        _ema1.Reset();
        _ema2.Reset();
        _ema3.Reset();
        _ema4.Reset();
        _ema5.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema1 = _ema1.Next(value, isFinal);
        var ema2 = _ema2.Next(ema1, isFinal);
        var ema3 = _ema3.Next(ema2, isFinal);
        var ema4 = _ema4.Next(ema3, isFinal);
        var ema5 = _ema5.Next(ema4, isFinal);
        var qema = (5 * ema1) - (10 * ema2) + (10 * ema3) - (5 * ema4) + ema5;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Qema", qema }
            };
        }

        return new StreamingIndicatorStateResult(qema, outputs);
    }

    public void Dispose()
    {
        _ema1.Dispose();
        _ema2.Dispose();
        _ema3.Dispose();
        _ema4.Dispose();
        _ema5.Dispose();
    }
}

public sealed class QuantitativeQualitativeEstimationState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _rsiSignal;
    private readonly IMovingAverageSmoother _atrRsiEma;
    private readonly IMovingAverageSmoother _atrRsiSmooth;
    private readonly StreamingInputResolver _input;
    private readonly double _fastFactor;
    private readonly double _slowFactor;
    private double _prevRsiSignal;
    private bool _hasPrev;

    public QuantitativeQualitativeEstimationState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        int smoothLength = 5, double fastFactor = 2.618, double slowFactor = 4.236, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        var wildersLength = Math.Max(1, (resolvedLength * 2) - 1);
        _rsi = new RsiState(maType, resolvedLength);
        _rsiSignal = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _atrRsiEma = MovingAverageSmootherFactory.Create(maType, wildersLength);
        _atrRsiSmooth = MovingAverageSmootherFactory.Create(maType, wildersLength);
        _input = new StreamingInputResolver(inputName, null);
        _fastFactor = fastFactor;
        _slowFactor = slowFactor;
    }

    public QuantitativeQualitativeEstimationState(MovingAvgType maType, int length, int smoothLength, double fastFactor,
        double slowFactor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedSmooth = Math.Max(1, smoothLength);
        var wildersLength = Math.Max(1, (resolvedLength * 2) - 1);
        _rsi = new RsiState(maType, resolvedLength);
        _rsiSignal = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _atrRsiEma = MovingAverageSmootherFactory.Create(maType, wildersLength);
        _atrRsiSmooth = MovingAverageSmootherFactory.Create(maType, wildersLength);
        _input = new StreamingInputResolver(InputName.Close, selector);
        _fastFactor = fastFactor;
        _slowFactor = slowFactor;
    }

    public IndicatorName Name => IndicatorName.QuantitativeQualitativeEstimation;

    public void Reset()
    {
        _rsi.Reset();
        _rsiSignal.Reset();
        _atrRsiEma.Reset();
        _atrRsiSmooth.Reset();
        _prevRsiSignal = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var rsiSignal = _rsiSignal.Next(rsi, isFinal);
        var prevRsiSignal = _hasPrev ? _prevRsiSignal : 0;
        var atrRsi = Math.Abs(rsiSignal - prevRsiSignal);
        var atrRsiEma = _atrRsiEma.Next(atrRsi, isFinal);
        var atrRsiSmooth = _atrRsiSmooth.Next(atrRsiEma, isFinal);
        var fastAtrRsi = atrRsiSmooth * _fastFactor;
        var slowAtrRsi = atrRsiSmooth * _slowFactor;

        if (isFinal)
        {
            _prevRsiSignal = rsiSignal;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "FastAtrRsi", fastAtrRsi },
                { "SlowAtrRsi", slowAtrRsi }
            };
        }

        return new StreamingIndicatorStateResult(fastAtrRsi, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _rsiSignal.Dispose();
        _atrRsiEma.Dispose();
        _atrRsiSmooth.Dispose();
    }
}

public sealed class QuasiWhiteNoiseState : IStreamingIndicatorState, IDisposable
{
    private readonly ConnorsRelativeStrengthIndexState _connors;
    private readonly IMovingAverageSmoother _whiteNoiseSma;
    private readonly StandardDeviationVolatilityState _whiteNoiseStdDev;
    private readonly double _divisor;
    private double _whiteNoiseValue;

    public QuasiWhiteNoiseState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 20, int noiseLength = 500,
        double divisor = 40, InputName inputName = InputName.Close)
    {
        var resolvedLength = Math.Max(1, length);
        var resolvedNoise = Math.Max(1, noiseLength);
        _connors = new ConnorsRelativeStrengthIndexState(maType, resolvedNoise, resolvedNoise, resolvedLength, inputName);
        _whiteNoiseSma = MovingAverageSmootherFactory.Create(maType, resolvedNoise);
        _whiteNoiseStdDev = new StandardDeviationVolatilityState(maType, resolvedNoise, _ => _whiteNoiseValue);
        _divisor = divisor;
    }

    public QuasiWhiteNoiseState(MovingAvgType maType, int length, int noiseLength, double divisor, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolvedLength = Math.Max(1, length);
        var resolvedNoise = Math.Max(1, noiseLength);
        _connors = new ConnorsRelativeStrengthIndexState(maType, resolvedNoise, resolvedNoise, resolvedLength, selector);
        _whiteNoiseSma = MovingAverageSmootherFactory.Create(maType, resolvedNoise);
        _whiteNoiseStdDev = new StandardDeviationVolatilityState(maType, resolvedNoise, _ => _whiteNoiseValue);
        _divisor = divisor;
    }

    public IndicatorName Name => IndicatorName.QuasiWhiteNoise;

    public void Reset()
    {
        _connors.Reset();
        _whiteNoiseSma.Reset();
        _whiteNoiseStdDev.Reset();
        _whiteNoiseValue = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var connors = _connors.Update(bar, isFinal, includeOutputs: false).Value;
        _whiteNoiseValue = (connors - 50) * (1d / _divisor);
        var whiteNoiseMa = _whiteNoiseSma.Next(_whiteNoiseValue, isFinal);
        var whiteNoiseStdDev = _whiteNoiseStdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var whiteNoiseVariance = MathHelper.Pow(whiteNoiseStdDev, 2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "WhiteNoise", _whiteNoiseValue },
                { "WhiteNoiseMa", whiteNoiseMa },
                { "WhiteNoiseStdDev", whiteNoiseStdDev },
                { "WhiteNoiseVariance", whiteNoiseVariance }
            };
        }

        return new StreamingIndicatorStateResult(_whiteNoiseValue, outputs);
    }

    public void Dispose()
    {
        _connors.Dispose();
        _whiteNoiseSma.Dispose();
        _whiteNoiseStdDev.Dispose();
    }
}

public sealed class QuickMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _peak;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public QuickMovingAverageState(int length = 14, InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _peak = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 3));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(inputName, null);
    }

    public QuickMovingAverageState(int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _peak = MathHelper.MinOrMax((int)Math.Ceiling((double)_length / 3));
        _values = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.QuickMovingAverage;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double num = 0;
        double denom = 0;
        for (var j = 1; j <= _length + 1; j++)
        {
            var mult = j <= _peak ? (double)j / _peak : (double)(_length + 1 - j) / (_length + 1 - _peak);
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j - 1);
            num += prevValue * mult;
            denom += mult;
        }

        var qma = denom != 0 ? num / denom : 0;
        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Qma", qma }
            };
        }

        return new StreamingIndicatorStateResult(qma, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
}

public sealed class R2AdaptiveRegressionState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly LinearRegressionState _linreg;
    private readonly StandardDeviationVolatilityState _stdDev;
    private readonly IMovingAverageSmoother _sma;
    private readonly RollingWindowCorrelation _x2Correlation;
    private readonly RollingWindowCorrelation _y1Correlation;
    private readonly RollingWindowCorrelation _y2Correlation;
    private readonly RollingWindowSum _x2Sum;
    private readonly RollingWindowSum _x2PowSum;
    private readonly StreamingInputResolver _input;
    private double _prevOut;
    private bool _hasPrev;

    public R2AdaptiveRegressionState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _linreg = new LinearRegressionState(_length, inputName);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, inputName);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _x2Correlation = new RollingWindowCorrelation(_length);
        _y1Correlation = new RollingWindowCorrelation(_length);
        _y2Correlation = new RollingWindowCorrelation(_length);
        _x2Sum = new RollingWindowSum(_length);
        _x2PowSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(inputName, null);
    }

    public R2AdaptiveRegressionState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _linreg = new LinearRegressionState(_length, selector);
        _stdDev = new StandardDeviationVolatilityState(maType, _length, selector);
        _sma = MovingAverageSmootherFactory.Create(maType, _length);
        _x2Correlation = new RollingWindowCorrelation(_length);
        _y1Correlation = new RollingWindowCorrelation(_length);
        _y2Correlation = new RollingWindowCorrelation(_length);
        _x2Sum = new RollingWindowSum(_length);
        _x2PowSum = new RollingWindowSum(_length);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.R2AdaptiveRegression;

    public void Reset()
    {
        _linreg.Reset();
        _stdDev.Reset();
        _sma.Reset();
        _x2Correlation.Reset();
        _y1Correlation.Reset();
        _y2Correlation.Reset();
        _x2Sum.Reset();
        _x2PowSum.Reset();
        _prevOut = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var stdDev = _stdDev.Update(bar, isFinal, includeOutputs: false).Value;
        var sma = _sma.Next(value, isFinal);
        var y1 = _linreg.Update(bar, isFinal, includeOutputs: false).Value;
        var x2 = _hasPrev ? _prevOut : value;
        var x2Sum = isFinal ? _x2Sum.Add(x2, out var x2Count) : _x2Sum.Preview(x2, out x2Count);
        var x2Avg = x2Count != 0 ? x2Sum / x2Count : 0;
        var x2Dev = x2 - x2Avg;
        var x2Pow = MathHelper.Pow(x2Dev, 2);
        var x2PowSum = isFinal ? _x2PowSum.Add(x2Pow, out var x2PowCount) : _x2PowSum.Preview(x2Pow, out x2PowCount);
        var x2PowAvg = x2PowCount != 0 ? x2PowSum / x2PowCount : 0;
        var x2StdDev = x2PowAvg >= 0 ? MathHelper.Sqrt(x2PowAvg) : 0;
        var r2x2 = isFinal ? _x2Correlation.Add(x2, value, out _) : _x2Correlation.Preview(x2, value, out _);
        r2x2 = MathHelper.IsValueNullOrInfinity(r2x2) ? 0 : r2x2;
        var a = x2StdDev != 0 ? stdDev * r2x2 / x2StdDev : 0;
        var b = sma - (a * x2Avg);
        var y2 = (a * x2) + b;
        var ry1 = isFinal ? _y1Correlation.Add(y1, value, out _) : _y1Correlation.Preview(y1, value, out _);
        ry1 = MathHelper.IsValueNullOrInfinity(ry1) ? 0 : MathHelper.Pow(ry1, 2);
        var ry2 = isFinal ? _y2Correlation.Add(y2, value, out _) : _y2Correlation.Preview(y2, value, out _);
        ry2 = MathHelper.IsValueNullOrInfinity(ry2) ? 0 : MathHelper.Pow(ry2, 2);
        var outValue = (ry1 * y1) + (ry2 * y2) + ((1 - (ry1 + ry2)) * x2);

        if (isFinal)
        {
            _prevOut = outValue;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "R2ar", outValue }
            };
        }

        return new StreamingIndicatorStateResult(outValue, outputs);
    }

    public void Dispose()
    {
        _linreg.Dispose();
        _stdDev.Dispose();
        _sma.Dispose();
        _x2Correlation.Dispose();
        _y1Correlation.Dispose();
        _y2Correlation.Dispose();
        _x2Sum.Dispose();
        _x2PowSum.Dispose();
    }
}

public sealed class RahulMohindarOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _r1;
    private readonly IMovingAverageSmoother _r2;
    private readonly IMovingAverageSmoother _r3;
    private readonly IMovingAverageSmoother _r4;
    private readonly IMovingAverageSmoother _r5;
    private readonly IMovingAverageSmoother _r6;
    private readonly IMovingAverageSmoother _r7;
    private readonly IMovingAverageSmoother _r8;
    private readonly IMovingAverageSmoother _r9;
    private readonly IMovingAverageSmoother _r10;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly IMovingAverageSmoother _swing2Smoother;
    private readonly IMovingAverageSmoother _swing3Smoother;
    private readonly IMovingAverageSmoother _rmoSmoother;
    private readonly StreamingInputResolver _input;

    public RahulMohindarOscillatorState(int length1 = 2, int length2 = 10, int length3 = 30, int length4 = 81,
        InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var resolved4 = Math.Max(1, length4);
        _r1 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r2 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r3 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r4 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r5 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r6 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r7 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r8 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r9 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r10 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _swing2Smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved3);
        _swing3Smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved3);
        _rmoSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved4);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RahulMohindarOscillatorState(int length1, int length2, int length3, int length4, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        var resolved3 = Math.Max(1, length3);
        var resolved4 = Math.Max(1, length4);
        _r1 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r2 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r3 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r4 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r5 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r6 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r7 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r8 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r9 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _r10 = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolved1);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _swing2Smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved3);
        _swing3Smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved3);
        _rmoSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, resolved4);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RahulMohindarOscillator;

    public void Reset()
    {
        _r1.Reset();
        _r2.Reset();
        _r3.Reset();
        _r4.Reset();
        _r5.Reset();
        _r6.Reset();
        _r7.Reset();
        _r8.Reset();
        _r9.Reset();
        _r10.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
        _swing2Smoother.Reset();
        _swing3Smoother.Reset();
        _rmoSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(value, out _) : _highWindow.Preview(value, out _);
        var lowest = isFinal ? _lowWindow.Add(value, out _) : _lowWindow.Preview(value, out _);
        var r1 = _r1.Next(value, isFinal);
        var r2 = _r2.Next(r1, isFinal);
        var r3 = _r3.Next(r2, isFinal);
        var r4 = _r4.Next(r3, isFinal);
        var r5 = _r5.Next(r4, isFinal);
        var r6 = _r6.Next(r5, isFinal);
        var r7 = _r7.Next(r6, isFinal);
        var r8 = _r8.Next(r7, isFinal);
        var r9 = _r9.Next(r8, isFinal);
        var r10 = _r10.Next(r9, isFinal);
        var avg = (r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10;
        var swingTrd1 = highest - lowest != 0 ? 100 * (value - avg) / (highest - lowest) : 0;
        var swingTrd2 = _swing2Smoother.Next(swingTrd1, isFinal);
        var swingTrd3 = _swing3Smoother.Next(swingTrd2, isFinal);
        var rmo = _rmoSmoother.Next(swingTrd1, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(4)
            {
                { "Rmo", rmo },
                { "SwingTrade1", swingTrd1 },
                { "SwingTrade2", swingTrd2 },
                { "SwingTrade3", swingTrd3 }
            };
        }

        return new StreamingIndicatorStateResult(rmo, outputs);
    }

    public void Dispose()
    {
        _r1.Dispose();
        _r2.Dispose();
        _r3.Dispose();
        _r4.Dispose();
        _r5.Dispose();
        _r6.Dispose();
        _r7.Dispose();
        _r8.Dispose();
        _r9.Dispose();
        _r10.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
        _swing2Smoother.Dispose();
        _swing3Smoother.Dispose();
        _rmoSmoother.Dispose();
    }
}

public sealed class RainbowOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _r1;
    private readonly IMovingAverageSmoother _r2;
    private readonly IMovingAverageSmoother _r3;
    private readonly IMovingAverageSmoother _r4;
    private readonly IMovingAverageSmoother _r5;
    private readonly IMovingAverageSmoother _r6;
    private readonly IMovingAverageSmoother _r7;
    private readonly IMovingAverageSmoother _r8;
    private readonly IMovingAverageSmoother _r9;
    private readonly IMovingAverageSmoother _r10;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public RainbowOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 2, int length2 = 10,
        InputName inputName = InputName.Close)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _r1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r6 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r7 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r8 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r9 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r10 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _input = new StreamingInputResolver(inputName, null);
    }

    public RainbowOscillatorState(MovingAvgType maType, int length1, int length2, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _r1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r2 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r3 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r4 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r5 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r6 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r7 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r8 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r9 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _r10 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _highWindow = new RollingWindowMax(resolved2);
        _lowWindow = new RollingWindowMin(resolved2);
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RainbowOscillator;

    public void Reset()
    {
        _r1.Reset();
        _r2.Reset();
        _r3.Reset();
        _r4.Reset();
        _r5.Reset();
        _r6.Reset();
        _r7.Reset();
        _r8.Reset();
        _r9.Reset();
        _r10.Reset();
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(value, out _) : _highWindow.Preview(value, out _);
        var lowest = isFinal ? _lowWindow.Add(value, out _) : _lowWindow.Preview(value, out _);
        var r1 = _r1.Next(value, isFinal);
        var r2 = _r2.Next(r1, isFinal);
        var r3 = _r3.Next(r2, isFinal);
        var r4 = _r4.Next(r3, isFinal);
        var r5 = _r5.Next(r4, isFinal);
        var r6 = _r6.Next(r5, isFinal);
        var r7 = _r7.Next(r6, isFinal);
        var r8 = _r8.Next(r7, isFinal);
        var r9 = _r9.Next(r8, isFinal);
        var r10 = _r10.Next(r9, isFinal);
        var highestRainbow = Math.Max(r1, Math.Max(r2, Math.Max(r3, Math.Max(r4, Math.Max(r5, Math.Max(r6, Math.Max(r7,
            Math.Max(r8, Math.Max(r9, r10)))))))));
        var lowestRainbow = Math.Min(r1, Math.Min(r2, Math.Min(r3, Math.Min(r4, Math.Min(r5, Math.Min(r6, Math.Min(r7,
            Math.Min(r8, Math.Min(r9, r10)))))))));
        var avg = (r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10) / 10;
        var ro = highest - lowest != 0 ? 100 * (value - avg) / (highest - lowest) : 0;
        var upper = highest - lowest != 0 ? 100 * ((highestRainbow - lowestRainbow) / (highest - lowest)) : 0;
        var lower = -upper;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Ro", ro },
                { "UpperBand", upper },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(ro, outputs);
    }

    public void Dispose()
    {
        _r1.Dispose();
        _r2.Dispose();
        _r3.Dispose();
        _r4.Dispose();
        _r5.Dispose();
        _r6.Dispose();
        _r7.Dispose();
        _r8.Dispose();
        _r9.Dispose();
        _r10.Dispose();
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

public sealed class RandomWalkIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _sqrtLength;
    private readonly AverageTrueRangeSmoother _atrSmoother;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;

    public RandomWalkIndexState(MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 14,
        InputName inputName = InputName.Close)
    {
        _length = Math.Max(1, length);
        _sqrtLength = MathHelper.Sqrt(_length);
        _atrSmoother = new AverageTrueRangeSmoother(maType, _length, inputName);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
    }

    public RandomWalkIndexState(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _length = Math.Max(1, length);
        _sqrtLength = MathHelper.Sqrt(_length);
        _atrSmoother = new AverageTrueRangeSmoother(maType, _length, selector);
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
    }

    public IndicatorName Name => IndicatorName.RandomWalkIndex;

    public void Reset()
    {
        _atrSmoother.Reset();
        _highValues.Clear();
        _lowValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var atr = _atrSmoother.Next(bar, isFinal);
        var prevHigh = EhlersStreamingWindow.GetOffsetValue(_highValues, bar.High, _length);
        var prevLow = EhlersStreamingWindow.GetOffsetValue(_lowValues, bar.Low, _length);
        var bottom = atr * _sqrtLength;
        var rwiLow = bottom != 0 ? (prevHigh - bar.Low) / bottom : 0;
        var rwiHigh = bottom != 0 ? (bar.High - prevLow) / bottom : 0;

        if (isFinal)
        {
            _highValues.TryAdd(bar.High, out _);
            _lowValues.TryAdd(bar.Low, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "RwiHigh", rwiHigh },
                { "RwiLow", rwiLow }
            };
        }

        return new StreamingIndicatorStateResult(rwiHigh, outputs);
    }

    public void Dispose()
    {
        _atrSmoother.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
    }
}

public sealed class RangeActionVerificationIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly StreamingInputResolver _input;

    public RangeActionVerificationIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 7,
        int slowLength = 65, InputName inputName = InputName.Close)
    {
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(inputName, null);
    }

    public RangeActionVerificationIndexState(MovingAvgType maType, int fastLength, int slowLength,
        Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _fastSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public IndicatorName Name => IndicatorName.RangeActionVerificationIndex;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var ravi = slow != 0 ? (fast - slow) / slow * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ravi", ravi }
            };
        }

        return new StreamingIndicatorStateResult(ravi, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
    }
}

internal sealed class AverageTrueRangeSmoother : IDisposable
{
    private readonly IMovingAverageSmoother _smoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public AverageTrueRangeSmoother(MovingAvgType maType, int length, InputName inputName)
    {
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(inputName, null);
    }

    public AverageTrueRangeSmoother(MovingAvgType maType, int length, Func<OhlcvBar, double> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, selector);
    }

    public double Next(OhlcvBar bar, bool isFinal)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var atr = _smoother.Next(tr, isFinal);

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        return atr;
    }

    public void Reset()
    {
        _smoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public void Dispose()
    {
        _smoother.Dispose();
    }
}
