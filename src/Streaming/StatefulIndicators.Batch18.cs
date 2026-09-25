#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Maaq")]
public sealed class MovingAverageAdaptiveQState : IStreamingIndicatorState, IDisposable
{
    private readonly EfficiencyRatioState _er;
    private readonly StreamingInputResolver _input;
    private readonly double _fastAlpha;
    private readonly double _slowAlpha;
    private double _prevMaaq;
    private bool _hasPrev;

    public MovingAverageAdaptiveQState(int length = 10, double fastAlpha = 0.667, double slowAlpha = 0.0645)
    {
        var resolved = Math.Max(1, length);
        _er = new EfficiencyRatioState(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
        _fastAlpha = fastAlpha;
        _slowAlpha = slowAlpha;
    }

    public IndicatorName Name => IndicatorName.MovingAverageAdaptiveQ;

    public void Reset()
    {
        _er.Reset();
        _prevMaaq = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevMaaq = _hasPrev ? _prevMaaq : value;
        var er = _er.Next(value, isFinal);
        var temp = (er * _fastAlpha) + _slowAlpha;
        var maaq = prevMaaq + (MathHelper.Pow(temp, 2) * (value - prevMaaq));

        if (isFinal)
        {
            _prevMaaq = maaq;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Maaq", maaq }
            };
        }

        return new StreamingIndicatorStateResult(maaq, outputs);
    }

    public void Dispose()
    {
        _er.Dispose();
    }
}

[PrimaryOutput("Mabw")]
public sealed class MovingAverageBandWidthState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fastSmoother;
    private readonly IMovingAverageSmoother _slowSmoother;
    private readonly RollingWindowSum _sqSum;
    private readonly StreamingInputResolver _input;
    private readonly double _mult;

    public MovingAverageBandWidthState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int fastLength = 10,
        int slowLength = 50, double mult = 1)
    {
        var resolvedFast = Math.Max(1, fastLength);
        var resolvedSlow = Math.Max(1, slowLength);
        _fastSmoother = MovingAverageSmootherFactory.Create(maType, resolvedFast);
        _slowSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSlow);
        _sqSum = new RollingWindowSum(resolvedFast);
        _mult = mult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MovingAverageBandWidth;

    public void Reset()
    {
        _fastSmoother.Reset();
        _slowSmoother.Reset();
        _sqSum.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fast = _fastSmoother.Next(value, isFinal);
        var slow = _slowSmoother.Next(value, isFinal);
        var diff = slow - fast;
        var sq = diff * diff;
        int countAfter;
        var sum = isFinal ? _sqSum.Add(sq, out countAfter) : _sqSum.Preview(sq, out countAfter);
        var dev = MathHelper.Sqrt(countAfter > 0 ? sum / countAfter : 0) * _mult;
        var upper = slow + dev;
        var lower = slow - dev;
        // Divided by the average the bands are centred on, which is the slow one: they are slow +/- dev.
        // This divided by the fast average instead, and agreed with the batch only because the batch read
        // it from MovingAverageBands' MiddleBand, which was publishing the fast average rather than the
        // centre. With that corrected the batch moved and this had to follow, which is the clearest sign
        // the mislabelled band was not merely cosmetic: a second indicator was dividing by it.
        var mabw = slow != 0 ? (upper - lower) / slow * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mabw", mabw }
            };
        }

        return new StreamingIndicatorStateResult(mabw, outputs);
    }

    public void Dispose()
    {
        _fastSmoother.Dispose();
        _slowSmoother.Dispose();
        _sqSum.Dispose();
    }
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
    private readonly IMovingAverageSmoother _ma1;
    private readonly IMovingAverageSmoother _ma2;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha;

    public MovingAverageV3State(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 3)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _ma1 = MovingAverageSmootherFactory.Create(maType, resolved1);
        _ma2 = MovingAverageSmootherFactory.Create(maType, resolved2);
        _input = new StreamingInputResolver(InputName.Close, null);
        var lamdaRatio = (double)resolved1 / resolved2;
        _alpha = resolved1 - lamdaRatio != 0 ? lamdaRatio * (resolved1 - 1) / (resolved1 - lamdaRatio) : 0;
    }

    public IndicatorName Name => IndicatorName.MovingAverageV3;

    public void Reset()
    {
        _ma1.Reset();
        _ma2.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma1 = _ma1.Next(value, isFinal);
        var ma2 = _ma2.Next(value, isFinal);
        var nma = ((1 + _alpha) * ma1) - (_alpha * ma2);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mav3", nma }
            };
        }

        return new StreamingIndicatorStateResult(nma, outputs);
    }

    public void Dispose()
    {
        _ma1.Dispose();
        _ma2.Dispose();
    }
}

[PrimaryOutput("Md2Pole")]
public sealed class MultiDepthZeroLagExponentialMovingAverageState : IStreamingIndicatorState
{
    private readonly double _a1;
    private readonly double _a2;
    private readonly double _a3;
    private readonly double _b2;
    private readonly double _b3;
    private readonly double _c;
    private readonly StreamingInputResolver _input;
    private double _alpha1;
    private double _alpha2;
    private double _alpha2_2;
    private double _alpha3;
    private double _alpha3_2;
    private double _alpha3_3;
    private double _beta1;
    private double _beta2;
    private double _beta2_2;
    private double _beta3_1;
    private double _beta3_2;
    private double _beta3_3;
    private int _index;

    public MultiDepthZeroLagExponentialMovingAverageState(int length = 50)
    {
        var resolved = Math.Max(1, length);
        _a1 = (double)2 / (resolved + 1);
        _a2 = MathHelper.Exp(-MathHelper.Sqrt(2) * Math.PI / resolved);
        _a3 = MathHelper.Exp(-Math.PI / resolved);
        _b2 = 2 * _a2 * Math.Cos(MathHelper.Sqrt(2) * Math.PI / resolved);
        _b3 = 2 * _a3 * Math.Cos(MathHelper.Sqrt(3) * Math.PI / resolved);
        _c = MathHelper.Exp(-2 * Math.PI / resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MultiDepthZeroLagExponentialMovingAverage;

    public void Reset()
    {
        _alpha1 = 0;
        _alpha2 = 0;
        _alpha2_2 = 0;
        _alpha3 = 0;
        _alpha3_2 = 0;
        _alpha3_3 = 0;
        _beta1 = 0;
        _beta2 = 0;
        _beta2_2 = 0;
        _beta3_1 = 0;
        _beta3_2 = 0;
        _beta3_3 = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevAlpha1 = _index >= 1 ? _alpha1 : value;
        var alpha1 = (_a1 * value) + ((1 - _a1) * prevAlpha1);

        var prevAlpha2 = _index >= 1 ? _alpha2 : value;
        var priorAlpha2 = _index >= 2 ? _alpha2_2 : value;
        var alpha2 = (_b2 * prevAlpha2) - (_a2 * _a2 * priorAlpha2) + ((1 - _b2 + (_a2 * _a2)) * value);

        var prevAlpha3 = _index >= 1 ? _alpha3 : value;
        var prevAlpha3_2 = _index >= 2 ? _alpha3_2 : value;
        var prevAlpha3_3 = _index >= 3 ? _alpha3_3 : value;
        var alpha3 = ((_b3 + _c) * prevAlpha3) - ((_c + (_b3 * _c)) * prevAlpha3_2) +
            (_c * _c * prevAlpha3_3) + ((1 - _b3 + _c) * (1 - _c) * value);

        var detrend1 = value - alpha1;
        var detrend2 = value - alpha2;
        var detrend3 = value - alpha3;

        var prevBeta1 = _index >= 1 ? _beta1 : 0;
        var beta1 = (_a1 * detrend1) + ((1 - _a1) * prevBeta1);

        var prevBeta2 = _index >= 1 ? _beta2 : 0;
        var prevBeta2_2 = _index >= 2 ? _beta2_2 : 0;
        var beta2 = (_b2 * prevBeta2) - (_a2 * _a2 * prevBeta2_2) + ((1 - _b2 + (_a2 * _a2)) * detrend2);

        var prevBeta3_2 = _index >= 2 ? _beta3_2 : 0;
        var prevBeta3_3 = _index >= 3 ? _beta3_3 : 0;
        var beta3 = ((_b3 + _c) * (_index >= 1 ? _beta3_1 : 0)) - ((_c + (_b3 * _c)) * prevBeta3_2) +
            (_c * _c * prevBeta3_3) + ((1 - _b3 + _c) * (1 - _c) * detrend3);

        var mda1 = alpha1 + beta1;
        var mda2 = alpha2 + (0.5 * beta2);
        var mda3 = alpha3 + ((double)1 / 3 * beta3);

        if (isFinal)
        {
            _alpha1 = alpha1;
            _alpha2_2 = _alpha2;
            _alpha2 = alpha2;
            _alpha3_3 = _alpha3_2;
            _alpha3_2 = _alpha3;
            _alpha3 = alpha3;
            _beta1 = beta1;
            _beta2_2 = _beta2;
            _beta2 = beta2;
            _beta3_3 = _beta3_2;
            _beta3_2 = _beta3_1;
            _beta3_1 = beta3;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Md2Pole", mda2 },
                { "Md1Pole", mda1 },
                { "Md3Pole", mda3 }
            };
        }

        return new StreamingIndicatorStateResult(mda2, outputs);
    }
}

[PrimaryOutput("Mli")]
public sealed class MultiLevelIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _factor;
    private readonly PooledRingBuffer<double> _openValues;
    private readonly StreamingInputResolver _input;

    public MultiLevelIndicatorState(int length = 14, double factor = 10000)
    {
        _length = Math.Max(1, length);
        _factor = factor;
        _openValues = new PooledRingBuffer<double>(_length + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MultiLevelIndicator;

    public void Reset()
    {
        _openValues.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var currentOpen = bar.Open;
        var currentClose = _input.GetValue(bar);
        var prevOpen = EhlersStreamingWindow.GetOffsetValue(_openValues, currentOpen, _length);
        var z = (prevOpen - currentOpen) * _factor;

        if (isFinal)
        {
            _openValues.TryAdd(currentOpen, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Mli", z }
            };
        }

        return new StreamingIndicatorStateResult(z, outputs);
    }

    public void Dispose()
    {
        _openValues.Dispose();
    }
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
    private readonly int _length;
    private readonly double[] _weights;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public NarrowBandpassFilterState(int length = 50)
    {
        _length = Math.Max(2, length);
        _weights = BuildWeights(_length);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NarrowBandpassFilter;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        for (var j = 0; j < _length; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * _weights[j];
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
                { "Nbpf", sum }
            };
        }

        return new StreamingIndicatorStateResult(sum, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }

    private static double[] BuildWeights(int length)
    {
        var weights = new double[length];
        for (var j = 0; j < length; j++)
        {
            var x = j / (double)(length - 1);
            var win = 0.42 - (0.5 * Math.Cos(2 * Math.PI * x)) + (0.08 * Math.Cos(4 * Math.PI * x));
            weights[j] = Math.Sin(2 * Math.PI * j / length) * win;
        }

        return weights;
    }
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
            var range = hh - ll;
            var frac = range != 0 ? (c - ll) / range : 0;
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
    private readonly IMovingAverageSmoother _inputSma;
    private readonly IMovingAverageSmoother _nviSma;
    private readonly IMovingAverageSmoother _signalSmoother;
    // The deviation of each window about its own mean, matching the batch calculation; see #190. One measures
    // the resolved input and the other the negative volume index, and they must not be crossed.
    private readonly RollingStandardDeviation _inputStdDev;
    private readonly RollingStandardDeviation _nviStdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _top;
    private readonly double _bottom;
    private double _prevClose;
    private double _prevVolume;
    private double _prevNvi;
    private double _prevNvdi;
    private double _prevBsc;
    private bool _hasPrev;

    public NegativeVolumeDisparityIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33,
        int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        var resolved = Math.Max(1, length);
        _inputSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _nviSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        // No moving-average type, and no selector: RollingStandardDeviation is handed the value itself, so
        // the index no longer has to be smuggled in through a closure over a field.
        _inputStdDev = new RollingStandardDeviation(resolved);
        _nviStdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
        _top = top;
        _bottom = bottom;
    }

    public IndicatorName Name => IndicatorName.NegativeVolumeDisparityIndicator;

    public void Reset()
    {
        _inputSma.Reset();
        _nviSma.Reset();
        _signalSmoother.Reset();
        _inputStdDev.Reset();
        _nviStdDev.Reset();
        _prevClose = 0;
        _prevVolume = 0;
        _prevNvi = 0;
        _prevNvdi = 0;
        _prevBsc = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevVolume = _hasPrev ? _prevVolume : 0;
        var prevNvi = _hasPrev ? _prevNvi : 1000;
        var pctChg = CalculationsHelper.CalculatePercentChange(value, prevClose);
        // Matches the batch CalculateNegativeVolumeIndex: Fosback compounds the rate of change onto the
        // running index (NVI = prevNVI + prevNVI * ROC), and CalculatePercentChange returns that ROC
        // already scaled to a percentage, so the 100 has to come back out.
        var nvi = volume >= prevVolume ? prevNvi : prevNvi + (prevNvi * pctChg / 100);

        var inputSma = _inputSma.Next(value, isFinal);
        var nviSma = _nviSma.Next(nvi, isFinal);
        // Each deviation is fed the series it measures: the resolved input, and the negative volume index.
        var stdDev = _inputStdDev.Next(value, isFinal);
        var nviStdDev = _nviStdDev.Next(nvi, isFinal);

        var aTop = value - (inputSma - (2 * stdDev));
        var aBot = 4 * stdDev;
        var a = aBot != 0 ? aTop / aBot : 0;
        var bTop = nvi - (nviSma - (2 * nviStdDev));
        var bBot = 4 * nviStdDev;
        var b = bBot != 0 ? bTop / bBot : 0;
        var nvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
        var signal = _signalSmoother.Next(nvdi, isFinal);

        var prevNvdi = _hasPrev ? _prevNvdi : 0;
        var prevBsc = _hasPrev ? _prevBsc : 0;
        var bsc = (prevNvdi < _bottom && nvdi > _bottom) || nvdi > signal
            ? 1
            : (prevNvdi > _top && nvdi < _top) || nvdi < _bottom
                ? -1
                : prevBsc;

        if (isFinal)
        {
            _prevClose = value;
            _prevVolume = volume;
            _prevNvi = nvi;
            _prevNvdi = nvdi;
            _prevBsc = bsc;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Nvdi", nvdi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(nvdi, outputs);
    }

    public void Dispose()
    {
        _inputSma.Dispose();
        _nviSma.Dispose();
        _signalSmoother.Dispose();
        _inputStdDev.Dispose();
        _nviStdDev.Dispose();
    }
}

[PrimaryOutput("Nvi")]
public sealed class NegativeVolumeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly int _initialValue;
    private double _prevClose;
    private double _prevVolume;
    private double _prevNvi;
    private bool _hasPrev;

    public NegativeVolumeIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 255,
        int initialValue = 1000)
    {
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
        _initialValue = initialValue;
    }

    public IndicatorName Name => IndicatorName.NegativeVolumeIndex;

    public void Reset()
    {
        _signalSmoother.Reset();
        _prevClose = 0;
        _prevVolume = 0;
        _prevNvi = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var volume = bar.Volume;
        var prevClose = _hasPrev ? _prevClose : 0;
        var prevVolume = _hasPrev ? _prevVolume : 0;
        var prevNvi = _hasPrev ? _prevNvi : _initialValue;
        var pctChg = CalculationsHelper.CalculatePercentChange(value, prevClose);
        // Matches the batch CalculateNegativeVolumeIndex: Fosback compounds the rate of change onto the
        // running index (NVI = prevNVI + prevNVI * ROC), and CalculatePercentChange returns that ROC
        // already scaled to a percentage, so the 100 has to come back out.
        var nvi = volume >= prevVolume ? prevNvi : prevNvi + (prevNvi * pctChg / 100);
        var signal = _signalSmoother.Next(nvi, isFinal);

        if (isFinal)
        {
            _prevClose = value;
            _prevVolume = volume;
            _prevNvi = nvi;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Nvi", nvi },
                { "NviSignal", signal }
            };
        }

        return new StreamingIndicatorStateResult(nvi, outputs);
    }

    public void Dispose()
    {
        _signalSmoother.Dispose();
    }
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
public sealed class NormalizedRelativeVigorIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowSum _closeOpenSum;
    private readonly RollingWindowSum _highLowSum;
    private readonly IMovingAverageSmoother _closeOpenSmoother;
    private readonly IMovingAverageSmoother _highLowSmoother;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public NormalizedRelativeVigorIndexState(MovingAvgType maType = MovingAvgType.SymmetricallyWeightedMovingAverage,
        int length = 10)
    {
        var resolved = Math.Max(1, length);
        _closeOpenSum = new RollingWindowSum(resolved);
        _highLowSum = new RollingWindowSum(resolved);
        _closeOpenSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _highLowSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NormalizedRelativeVigorIndex;

    public void Reset()
    {
        _closeOpenSum.Reset();
        _highLowSum.Reset();
        _closeOpenSmoother.Reset();
        _highLowSmoother.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var closeOpen = close - bar.Open;
        var highLow = bar.High - bar.Low;
        var closeOpenSmoothed = _closeOpenSmoother.Next(closeOpen, isFinal);
        var highLowSmoothed = _highLowSmoother.Next(highLow, isFinal);
        var closeOpenSum = isFinal ? _closeOpenSum.Add(closeOpenSmoothed, out _) : _closeOpenSum.Preview(closeOpenSmoothed, out _);
        var highLowSum = isFinal ? _highLowSum.Add(highLowSmoothed, out _) : _highLowSum.Preview(highLowSmoothed, out _);
        var rvgi = highLowSum != 0 ? closeOpenSum / highLowSum * 100 : 0;
        var signal = _signalSmoother.Next(rvgi, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Nrvi", rvgi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(rvgi, outputs);
    }

    public void Dispose()
    {
        _closeOpenSum.Dispose();
        _highLowSum.Dispose();
        _closeOpenSmoother.Dispose();
        _highLowSmoother.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Nodo")]
public sealed class NthOrderDifferencingOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly int _lbLength;
    private readonly PooledRingBuffer<double> _values;
    private readonly StreamingInputResolver _input;

    public NthOrderDifferencingOscillatorState(int length = 14, int lbLength = 2)
    {
        _length = Math.Max(1, length);
        _lbLength = Math.Max(0, lbLength);
        _values = new PooledRingBuffer<double>((_length * (_lbLength + 1)) + 1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NthOrderDifferencingOscillator;

    public void Reset()
    {
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        double sum = 0;
        double w = 1;
        for (var j = 0; j <= _lbLength; j++)
        {
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length * (j + 1));
            var x = Math.Sign(((j + 1) % 2) - 0.5);
            w *= (_lbLength - j) / (double)(j + 1);
            sum += prevValue * w * x;
        }

        var nodo = value - sum;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Nodo", nodo }
            };
        }

        return new StreamingIndicatorStateResult(nodo, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
    }
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
    private readonly IMovingAverageSmoother _openSmoother;
    private readonly IMovingAverageSmoother _closeSmoother;
    private readonly StreamingInputResolver _input;

    public OCHistogramState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10)
    {
        var resolved = Math.Max(1, length);
        _openSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _closeSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.OCHistogram;

    public void Reset()
    {
        _openSmoother.Reset();
        _closeSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var close = _input.GetValue(bar);
        var openEma = _openSmoother.Next(bar.Open, isFinal);
        var closeEma = _closeSmoother.Next(close, isFinal);
        var ocHistogram = closeEma - openEma;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "OcHistogram", ocHistogram }
            };
        }

        return new StreamingIndicatorStateResult(ocHistogram, outputs);
    }

    public void Dispose()
    {
        _openSmoother.Dispose();
        _closeSmoother.Dispose();
    }
}

[PrimaryOutput("Or")]
public sealed class OmegaRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _bench;
    private readonly PooledRingBuffer<double> _values;
    private readonly PooledRingBuffer<double> _returns;
    private readonly StreamingInputResolver _input;

    public OmegaRatioState(int length = 30, double bmk = 0.05)
    {
        _length = Math.Max(1, length);
        var barMin = 60d * 24;
        var minPerYr = 60d * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        _bench = MathHelper.Pow(1 + bmk, _length / barsPerYr) - 1;
        _values = new PooledRingBuffer<double>(_length + 1);
        _returns = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.OmegaRatio;

    public void Reset()
    {
        _values.Clear();
        _returns.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, _length);
        var ret = prevValue != 0 ? (value / prevValue) - 1 : 0;
        double downSide = 0;
        double upSide = 0;
        for (var j = 0; j < _length; j++)
        {
            var iValue = EhlersStreamingWindow.GetOffsetValue(_returns, ret, j);
            downSide += iValue < _bench ? _bench - iValue : 0;
            upSide += iValue > _bench ? iValue - _bench : 0;
        }

        var omega = downSide != 0 ? upSide / downSide : 0;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
            _returns.TryAdd(ret, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Or", omega }
            };
        }

        return new StreamingIndicatorStateResult(omega, outputs);
    }

    public void Dispose()
    {
        _values.Dispose();
        _returns.Dispose();
    }
}

[PrimaryOutput("Obvdi")]
public sealed class OnBalanceVolumeDisparityIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _inputSma;
    private readonly IMovingAverageSmoother _obvSma;
    private readonly IMovingAverageSmoother _signalSmoother;
    // The deviation of each window about its own mean, matching the batch calculation; see #190. One measures
    // the resolved input and the other the on balance volume, and they must not be crossed.
    private readonly RollingStandardDeviation _inputStdDev;
    private readonly RollingStandardDeviation _obvStdDev;
    private readonly StreamingInputResolver _input;
    private readonly double _top;
    private readonly double _bottom;
    private double _prevClose;
    private ExactMeanAccumulator _obvTotal;
    private double _prevObvdi;
    private double _prevBsc;
    private bool _hasPrev;

    public OnBalanceVolumeDisparityIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33,
        int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        var resolved = Math.Max(1, length);
        _inputSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _obvSma = MovingAverageSmootherFactory.Create(maType, resolved);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        // No moving-average type, and no selector: RollingStandardDeviation is handed the value itself.
        _inputStdDev = new RollingStandardDeviation(resolved);
        _obvStdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
        _top = top;
        _bottom = bottom;
    }

    public IndicatorName Name => IndicatorName.OnBalanceVolumeDisparityIndicator;

    public void Reset()
    {
        _inputSma.Reset();
        _obvSma.Reset();
        _signalSmoother.Reset();
        _inputStdDev.Reset();
        _obvStdDev.Reset();
        _prevClose = 0;
        _obvTotal = default;
        _prevObvdi = 0;
        _prevBsc = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevClose = _hasPrev ? _prevClose : 0;
        var total = _obvTotal;
        if (value > prevClose) total.Add(bar.Volume);
        else if (value < prevClose) total.Add(bar.Volume, -1);
        var obv = total.Mean(1);

        var inputSma = _inputSma.Next(value, isFinal);
        var obvSma = _obvSma.Next(obv, isFinal);
        // Each deviation is fed the series it measures: the resolved input, and the on balance volume.
        var stdDev = _inputStdDev.Next(value, isFinal);
        var obvStdDev = _obvStdDev.Next(obv, isFinal);

        var aTop = value - (inputSma - (2 * stdDev));
        var aBot = 4 * stdDev;
        var a = aBot != 0 ? aTop / aBot : 0;
        var bTop = obv - (obvSma - (2 * obvStdDev));
        var bBot = 4 * obvStdDev;
        var b = bBot != 0 ? bTop / bBot : 0;
        var obvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
        var signal = _signalSmoother.Next(obvdi, isFinal);

        var prevObvdi = _hasPrev ? _prevObvdi : 0;
        var prevBsc = _hasPrev ? _prevBsc : 0;
        var bsc = (prevObvdi < _bottom && obvdi > _bottom) || obvdi > signal
            ? 1
            : (prevObvdi > _top && obvdi < _top) || obvdi < _bottom
                ? -1
                : prevBsc;

        if (isFinal)
        {
            _prevClose = value;
            _obvTotal = total;
            _prevObvdi = obvdi;
            _prevBsc = bsc;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Obvdi", obvdi },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(obvdi, outputs);
    }

    public void Dispose()
    {
        _inputSma.Dispose();
        _obvSma.Dispose();
        _signalSmoother.Dispose();
        _inputStdDev.Dispose();
        _obvStdDev.Dispose();
    }
}
