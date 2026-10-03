#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Maaq")]
public sealed class MovingAverageAdaptiveQState : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAverageAdaptiveQWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public MovingAverageAdaptiveQState(int length = 10, double fastAlpha = .667, double slowAlpha = .0645) => _window = new(length, fastAlpha, slowAlpha);
    public IndicatorName Name => IndicatorName.MovingAverageAdaptiveQ;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Maaq", point.Value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Mabw")]
public sealed class MovingAverageBandWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAverageBandWindow _window;
    public MovingAverageBandWidthState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10, int slowLength = 50, double mult = 1) => _window = new(maType, fastLength, slowLength, mult);
    public IndicatorName Name => IndicatorName.MovingAverageBandWidth;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Width, includeOutputs ? new Dictionary<string, double> { { "Mabw", value.Width } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Macd")]
public sealed class MovingAverageConvergenceDivergenceLeaderState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly IMovingAverageSmoother _diffFastSmoother;
    private readonly IMovingAverageSmoother _diffSlowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly bool[] _invalid = new bool[3];

    public MovingAverageConvergenceDivergenceLeaderState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedFast) : MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedSlow) : MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _diffFastSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedFast) : MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _diffSlowSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(resolvedSlow) : MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _signalSmoother = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MovingAverageConvergenceDivergenceLeader;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _diffFastSmoother.Reset();
        _diffSlowSmoother.Reset();
        _signalSmoother.Reset();
        Array.Clear(_invalid, 0, _invalid.Length);
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        double Smooth(IMovingAverageSmoother smoother, double input, int slot)
        {
            var invalid = _invalid[slot] || double.IsNaN(input) || double.IsInfinity(input);
            if (isFinal) _invalid[slot] = invalid;
            return invalid ? double.NaN : smoother.Next(input, isFinal);
        }
        var value = _input.GetValue(bar);
        var emaFast = _fastSmoother.Next(value, isFinal);
        var emaSlow = _slowSmoother.Next(value, isFinal);
        var diffFast = value - emaFast;
        var diffSlow = value - emaSlow;
        var diffFastMa = Smooth(_diffFastSmoother, diffFast, 0);
        var diffSlowMa = Smooth(_diffSlowSmoother, diffSlow, 1);
        var i1 = emaFast + diffFastMa;
        var i2 = emaSlow + diffSlowMa;
        var macd = i1 - i2;
        _ = Smooth(_signalSmoother, macd, 2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Macd", macd },
                { "I1", i1 },
                { "I2", i2 }
            };
        }

        return new StreamingIndicatorStateResult(macd, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _diffFastSmoother.Dispose();
        _diffSlowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Mav3")]
public sealed class MovingAverageV3State : IStreamingIndicatorState, IDisposable
{
    private readonly MovingAverageV3Window _window;
    public MovingAverageV3State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14, int length2 = 3) => _window = new(maType, length1, length2);
    public IndicatorName Name => IndicatorName.MovingAverageV3;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Mav3", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Md2Pole")]
public sealed class MultiDepthZeroLagExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly MultiDepthWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public MultiDepthZeroLagExponentialMovingAverageState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.MultiDepthZeroLagExponentialMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), isFinal);
        return new StreamingIndicatorStateResult(point.Two, includeOutputs ? new Dictionary<string, double>
            { { "Md2Pole", point.Two }, { "Md1Pole", point.One }, { "Md3Pole", point.Three } } : null);
    }
}

[PrimaryOutput("Mli")]
public sealed class MultiLevelIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly MultiLevelWindow _window;
    public MultiLevelIndicatorState(int length = 14, double factor = 10000) { _window = new(length, factor); }
    public IndicatorName Name => IndicatorName.MultiLevelIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Open, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Mli", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Mvo")]
public sealed class MultiVoteOnBalanceVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private double _prevHigh;
    private double _prevLow;
    private double _prevMvo;
    private bool _hasPrev;

    public MultiVoteOnBalanceVolumeState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MultiVoteOnBalanceVolume;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevClose = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevMvo = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var currentHigh = bar.High;
        var currentLow = bar.Low;
        var currentClose = _input.GetValue(bar);
        var currentVolume = bar.Volume / 1_000_000d;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevMvo = _hasPrev ? _prevMvo : 0;
        var highVote = currentHigh > prevHigh ? 1 : currentHigh < prevHigh ? -1 : 0;
        var lowVote = currentLow > prevLow ? 1 : currentLow < prevLow ? -1 : 0;
        var closeVote = currentClose > prevClose ? 1 : currentClose < prevClose ? -1 : 0;
        var totalVotes = highVote + lowVote + closeVote;
        var mvo = prevMvo + (currentVolume * totalVotes);
        var signal = _signalSmoother.Next(mvo, isFinal);

        if (isFinal)
        {
            _prevClose = currentClose;
            _prevHigh = currentHigh;
            _prevLow = currentLow;
            _prevMvo = mvo;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Mvo", mvo },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(mvo, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Nbpf")]
public sealed class NarrowBandpassFilterState : IStreamingIndicatorState, IDisposable
{
    private readonly NarrowBandpassWindow _window;
    public NarrowBandpassFilterState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.NarrowBandpassFilter;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Nbpf", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Nxc")]
public sealed class NaturalDirectionalComboState : IStreamingIndicatorState, IDisposable
{
    private readonly NaturalDirectionalIndexState _ndx;
    private readonly NaturalStochasticIndicatorState _nst;

    public NaturalDirectionalComboState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 40,
        int smoothLength = 20)
    {
        _ndx = new NaturalDirectionalIndexState(maType, length, smoothLength);
        _nst = new NaturalStochasticIndicatorState(maType, length, smoothLength);
    }

    public IndicatorName Name => IndicatorName.NaturalDirectionalCombo;

    public void Reset()
    {
        _ndx.Reset();
        _nst.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var ndx = _ndx.Update(bar, isFinal, includeOutputs: false).Value;
        var nst = _nst.Update(bar, isFinal, includeOutputs: false).Value;
        var v3 = Math.Sign(ndx) != Math.Sign(nst)
            ? ndx * nst
            : ((Math.Abs(ndx) * nst) + (Math.Abs(nst) * ndx)) / 2;
        var nxc = Math.Sign(v3) * MathHelper.Sqrt(Math.Abs(v3));

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nxc", nxc }
            };
        }

        return new StreamingIndicatorStateResult(nxc, outputs);
    }

    public void Dispose()
    {
        _ndx.Dispose();
        _nst.Dispose();
    }
}

[PrimaryOutput("Ndx")]
public sealed class NaturalDirectionalIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _lnValues;
    private readonly StreamingInputResolver _input;

    public NaturalDirectionalIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 40,
        int smoothLength = 20)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _lnValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NaturalDirectionalIndex;

    public void Reset()
    {
        _smoother.Reset();
        _lnValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ln = value > 0 ? Math.Log(value) * 1000 : 0;
        double weightSum = 0;
        double denomSum = 0;
        double absSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var prevLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, j + 1);
            var currLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, j);
            var diff = prevLn - currLn;
            absSum += Math.Abs(diff);
            var frac = absSum != 0 ? (ln - currLn) / absSum : 0;
            var ratio = 1 / MathHelper.Sqrt(j + 1);
            weightSum += frac * ratio;
            denomSum += ratio;
        }

        var rawNdx = denomSum != 0 ? weightSum / denomSum * 100 : 0;
        var ndx = _smoother.Next(rawNdx, isFinal);

        if (isFinal)
        {
            _lnValues.TryAdd(ln, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Ndx", ndx }
            };
        }

        return new StreamingIndicatorStateResult(ndx, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _lnValues.Dispose();
    }
}

[PrimaryOutput("Nmc")]
public sealed class NaturalMarketComboState : IStreamingIndicatorState, IDisposable
{
    private readonly NaturalMarketRiverState _nmr;
    private readonly NaturalMarketMirrorState _nmm;
    private readonly IMovingAverageSmoother _signalSmoother;

    public NaturalMarketComboState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 40,
        int smoothLength = 20)
    {
        _nmr = new NaturalMarketRiverState(maType, length);
        _nmm = new NaturalMarketMirrorState(maType, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
    }

    public IndicatorName Name => IndicatorName.NaturalMarketCombo;

    public void Reset()
    {
        _nmr.Reset();
        _nmm.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var nmr = _nmr.Update(bar, isFinal, includeOutputs: false).Value;
        var nmm = _nmm.Update(bar, isFinal, includeOutputs: false).Value;
        var v3 = Math.Sign(nmm) != Math.Sign(nmr)
            ? nmm * nmr
            : ((Math.Abs(nmm) * nmr) + (Math.Abs(nmr) * nmm)) / 2;
        var nmc = Math.Sign(v3) * MathHelper.Sqrt(Math.Abs(v3));
        _ = _signalSmoother.Next(nmc, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nmc", nmc }
            };
        }

        return new StreamingIndicatorStateResult(nmc, outputs);
    }

    public void Dispose()
    {
        _nmr.Dispose();
        _nmm.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Nmm")]
public sealed class NaturalMarketMirrorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _lnValues;
    private readonly StreamingInputResolver _input;

    public NaturalMarketMirrorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 40)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _lnValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NaturalMarketMirror;

    public void Reset()
    {
        _smoother.Reset();
        _lnValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ln = value > 0 ? Math.Log(value) * 1000 : 0;
        double oiSum = 0;
        for (var j = 1; j <= _length; j++)
        {
            var prevLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, j);
            oiSum += (ln - prevLn) / MathHelper.Sqrt(j) * 100;
        }

        var oiAvg = oiSum / _length;
        var nmm = _smoother.Next(oiAvg, isFinal);

        if (isFinal)
        {
            _lnValues.TryAdd(ln, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nmm", nmm }
            };
        }

        return new StreamingIndicatorStateResult(nmm, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _lnValues.Dispose();
    }
}

[PrimaryOutput("Nmr")]
public sealed class NaturalMarketRiverState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _lnValues;
    private readonly StreamingInputResolver _input;

    public NaturalMarketRiverState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 40)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, _length);
        _lnValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NaturalMarketRiver;

    public void Reset()
    {
        _smoother.Reset();
        _lnValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ln = value > 0 ? Math.Log(value) * 1000 : 0;
        double oiSum = 0;
        for (var j = 0; j < _length; j++)
        {
            var currLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, j);
            var prevLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, j + 1);
            oiSum += (prevLn - currLn) * (MathHelper.Sqrt(j) - MathHelper.Sqrt(j + 1));
        }

        var nmr = _smoother.Next(oiSum, isFinal);

        if (isFinal)
        {
            _lnValues.TryAdd(ln, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nmr", nmr }
            };
        }

        return new StreamingIndicatorStateResult(nmr, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _lnValues.Dispose();
    }
}

[PrimaryOutput("Nms")]
public sealed class NaturalMarketSlopeState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly LinearRegressionState _regression;
    private readonly StreamingInputResolver _input;
    private double _regressionInput;
    private double _prevLinReg;
    private bool _hasPrev;

    public NaturalMarketSlopeState(int length = 40)
    {
        _length = Math.Max(1, length);
        _regression = new LinearRegressionState(_length, _ => _regressionInput);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NaturalMarketSlope;

    public void Reset()
    {
        _regression.Reset();
        _regressionInput = 0;
        _prevLinReg = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        _regressionInput = value > 0 ? Math.Log(value) * 1000 : 0;
        var linReg = _regression.Update(bar, isFinal, includeOutputs: false).Value;
        var prevLinReg = _hasPrev ? _prevLinReg : 0;
        var nms = (linReg - prevLinReg) * Math.Log(_length);

        if (isFinal)
        {
            _prevLinReg = linReg;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nms", nms }
            };
        }

        return new StreamingIndicatorStateResult(nms, outputs);
    }

    public void Dispose()
    {
        _regression.Dispose();
    }
}

[PrimaryOutput("Nma")]
public sealed class NaturalMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly NaturalWindowMean _mean;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);

    public NaturalMovingAverageState(int length = 40) => _mean = new NaturalWindowMean(length);
    public IndicatorName Name => IndicatorName.NaturalMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var nma = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Nma", nma } } : null;
        return new StreamingIndicatorStateResult(nma, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

[PrimaryOutput("Nst")]
public sealed class NaturalStochasticIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _smoother;
    private readonly PooledRingBuffer<double> _highValues;
    private readonly PooledRingBuffer<double> _lowValues;
    private readonly PooledRingBuffer<double> _highestValues;
    private readonly PooledRingBuffer<double> _lowestValues;
    private readonly PooledRingBuffer<double> _inputValues;
    private readonly StreamingInputResolver _input;

    public NaturalStochasticIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20,
        int smoothLength = 10)
    {
        _length = Math.Max(1, length);
        _smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _highValues = new PooledRingBuffer<double>(_length);
        _lowValues = new PooledRingBuffer<double>(_length);
        _highestValues = new PooledRingBuffer<double>(_length);
        _lowestValues = new PooledRingBuffer<double>(_length);
        _inputValues = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NaturalStochasticIndicator;

    public void Reset()
    {
        _smoother.Reset();
        _highValues.Clear();
        _lowValues.Clear();
        _highestValues.Clear();
        _lowestValues.Clear();
        _inputValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var high = bar.High;
        var low = bar.Low;

        // Compute rolling max/min for current bar (same as GetMaxAndMinValuesList)
        // This is what batch stores in highestList[i] / lowestList[i]
        var pendingHighest = ComputeRollingMax(high);
        var pendingLowest = ComputeRollingMin(low);

        double weightSum = 0;
        double denomSum = 0;
        for (var j = 0; j < _length; j++)
        {
            // Batch uses highestList[i - j] which is the rolling max computed at position (i - j)
            var hh = EhlersStreamingWindow.GetOffsetValue(_highestValues, pendingHighest, j);
            var ll = EhlersStreamingWindow.GetOffsetValue(_lowestValues, pendingLowest, j);
            var c = EhlersStreamingWindow.GetOffsetValue(_inputValues, close, j);
            var frac = ExactRangePosition.Fraction(c, ll, hh);
            var ratio = 1 / MathHelper.Sqrt(j + 1);
            weightSum += frac * ratio;
            denomSum += ratio;
        }

        var rawNst = denomSum != 0 ? (200 * weightSum / denomSum) - 100 : 0;
        var nst = _smoother.Next(rawNst, isFinal);

        if (isFinal)
        {
            // Store raw high/low for rolling window computation
            _highValues.TryAdd(high, out _);
            _lowValues.TryAdd(low, out _);
            // Store the pre-computed rolling max/min (like batch's highestList/lowestList)
            _highestValues.TryAdd(pendingHighest, out _);
            _lowestValues.TryAdd(pendingLowest, out _);
            _inputValues.TryAdd(close, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nst", nst }
            };
        }

        return new StreamingIndicatorStateResult(nst, outputs);
    }

    public void Dispose()
    {
        _smoother.Dispose();
        _highValues.Dispose();
        _lowValues.Dispose();
        _highestValues.Dispose();
        _lowestValues.Dispose();
        _inputValues.Dispose();
    }

    private double ComputeRollingMax(double pendingValue)
    {
        var max = pendingValue;
        var count = Math.Min(_length - 1, _highValues.Count);
        for (var i = 0; i < count; i++)
        {
            var value = _highValues[_highValues.Count - 1 - i];
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }

    private double ComputeRollingMin(double pendingValue)
    {
        var min = pendingValue;
        var count = Math.Min(_length - 1, _lowValues.Count);
        for (var i = 0; i < count; i++)
        {
            var value = _lowValues[_lowValues.Count - 1 - i];
            if (value < min)
            {
                min = value;
            }
        }

        return min;
    }
}

[PrimaryOutput("Nvdi")]
public sealed class NegativeVolumeDisparityIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly NegativeVolumeDisparityWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public NegativeVolumeDisparityIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33,
        int signalLength = 4, double top = 1.1, double bottom = .9) => _window = new(maType, length, signalLength, top, bottom);
    public IndicatorName Name => IndicatorName.NegativeVolumeDisparityIndicator;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Line.Publish(), includeOutputs ? new Dictionary<string, double>
            { { "Nvdi", point.Line.Publish() }, { "Signal", point.SignalLine.Publish() } } : null);
    }
}

[PrimaryOutput("Nvi")]
public sealed class NegativeVolumeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeIndexWindow _window;
    public NegativeVolumeIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 255, int initialValue = 1000)
        => _window = new VolumeIndexWindow(maType, length, false, initialValue);
    public IndicatorName Name => IndicatorName.NegativeVolumeIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Nvi", value.Line }, { "NviSignal", value.Signal } } : null;
        return new StreamingIndicatorStateResult(value.Line, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Nrtr")]
public sealed class NickRypockTrailingReverseState : IStreamingIndicatorState
{
    private readonly double _pct;
    private readonly StreamingInputResolver _input;
    private double _trend;
    private double _hp;
    private double _lp;
    private bool _hasPrev;

    public NickRypockTrailingReverseState(int length = 2)
    {
        _pct = Math.Max(1, length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NickRypockTrailingReverse;

    public void Reset()
    {
        _trend = 0;
        _hp = 0;
        _lp = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevTrend = _hasPrev ? _trend : 0;
        // Batch uses GetLastOrDefault which returns 0 for empty lists
        var prevHp = _hasPrev ? _hp : 0;
        var prevLp = _hasPrev ? _lp : 0;
        // Batch initializes trend=0, hp=0, lp=0 each iteration
        double nrtr;
        double trend = prevTrend;
        double hp = 0;
        double lp = 0;

        if (prevTrend >= 0)
        {
            hp = value > prevHp ? value : prevHp;
            nrtr = RoundedPercentageBand.Percent(hp, _pct, -1);
            // Only set trend=-1 when value <= nrtr; otherwise preserve the current trend
            if (value <= nrtr)
            {
                trend = -1;
                lp = value;
                nrtr = RoundedPercentageBand.Percent(lp, _pct, 1);
            }
            // Note: lp stays 0 when value > nrtr (matching batch behavior)
        }
        else
        {
            lp = value < prevLp ? value : prevLp;
            nrtr = RoundedPercentageBand.Percent(lp, _pct, 1);
            // Only set trend=1 when value > nrtr; otherwise preserve the current trend
            if (value > nrtr)
            {
                trend = 1;
                hp = value;
                nrtr = RoundedPercentageBand.Percent(hp, _pct, -1);
            }
            // Note: hp stays 0 when value <= nrtr (matching batch behavior)
        }

        if (isFinal)
        {
            _trend = trend;
            _hp = hp;
            _lp = lp;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nrtr", nrtr }
            };
        }

        return new StreamingIndicatorStateResult(nrtr, outputs);
    }
}

[PrimaryOutput("Nrvi")]
public sealed class NormalizedRelativeVigorIndexState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly NormalizedVigorWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public NormalizedRelativeVigorIndexState(MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage, int length = 10) { _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.NormalizedRelativeVigorIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var r = _window.Next(_input.GetValue(bar), bar.Open, bar.High, bar.Low, isFinal); return new(r.Value, includeOutputs ? new Dictionary<string, double> { { "Nrvi", r.Value }, { "Signal", r.Signal } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Nodo")]
public sealed class NthOrderDifferencingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly NthDifferenceWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public NthOrderDifferencingOscillatorState(int length = 14, int lbLength = 2) { _window = new(length, lbLength); }
    public IndicatorName Name => IndicatorName.NthOrderDifferencingOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(_input.GetValue(bar), isFinal); return new(value, includeOutputs ? new Dictionary<string, double> { { "Nodo", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Oi")]
public sealed class OceanIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly PooledRingBuffer<double> _lnValues;
    private readonly StreamingInputResolver _input;

    public OceanIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _length = Math.Max(1, length);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _lnValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.OceanIndicator;

    public void Reset()
    {
        _signalSmoother.Reset();
        _lnValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ln = value > 0 ? Math.Log(value) * 1000 : 0;
        var prevLn = EhlersStreamingWindow.GetOffsetValue(_lnValues, ln, _length);
        var oi = (ln - prevLn) / MathHelper.Sqrt(_length) * 100;
        var signal = _signalSmoother.Next(oi, isFinal);

        if (isFinal)
        {
            _lnValues.TryAdd(ln, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Oi", oi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(oi, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
        _lnValues.Dispose();
    }
}

[PrimaryOutput("OcHistogram")]
public sealed class OCHistogramState : IStreamingIndicatorState, IDisposable
{
    private readonly OpenCloseHistogramWindow _window;
    public OCHistogramState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.OCHistogram;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "OcHistogram", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Or")]
public sealed class OmegaRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly TargetReturnWindow _window;
    public OmegaRatioState(int length = 30, double bmk = .05) => _window = new TargetReturnWindow(length, bmk, false);
    public IndicatorName Name => IndicatorName.OmegaRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Or", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Obvdi")]
public sealed class OnBalanceVolumeDisparityIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly OnBalanceVolumeDisparityWindow _window;
    private readonly StreamingInputResolver _input = new(InputName.Close, null);
    public OnBalanceVolumeDisparityIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33,
        int signalLength = 4, double top = 1.1, double bottom = .9) => _window = new(maType, length, signalLength, top, bottom);
    public IndicatorName Name => IndicatorName.OnBalanceVolumeDisparityIndicator;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(_input.GetValue(bar), bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(point.Line.Publish(), includeOutputs ? new Dictionary<string, double>
            { { "Obvdi", point.Line.Publish() }, { "Signal", point.SignalLine.Publish() } } : null);
    }
}
