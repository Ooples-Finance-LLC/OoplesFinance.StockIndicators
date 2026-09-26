#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Tr")]
public sealed class TechnicalRatingsState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly int _uoLength1;
    private readonly int _uoLength2;
    private readonly int _uoLength3;
    private readonly int _vwmaLength;
    private readonly RollingWindowSum _bpSum1;
    private readonly RollingWindowSum _bpSum2;
    private readonly RollingWindowSum _bpSum3;
    private readonly RollingWindowSum _trSum1;
    private readonly RollingWindowSum _trSum2;
    private readonly RollingWindowSum _trSum3;
    private readonly IMovingAverageSmoother _ma10;
    private readonly IMovingAverageSmoother _ma20;
    private readonly IMovingAverageSmoother _ma30;
    private readonly IMovingAverageSmoother _ma50;
    private readonly IMovingAverageSmoother _ma100;
    private readonly IMovingAverageSmoother _ma200;
    private readonly IMovingAverageSmoother _vwmaVolumeSmoother;
    private readonly RollingWindowSum _vwmaVolumePriceSum;
    private readonly RollingWindowMax _stochHighWindow;
    private readonly RollingWindowMin _stochLowWindow;
    private readonly IMovingAverageSmoother _stochFastSmoother;
    private readonly RollingWindowMax _stochRsiHighWindow;
    private readonly RollingWindowMin _stochRsiLowWindow;
    private readonly IMovingAverageSmoother _stochRsiFastSmoother;
    private readonly RelativeStrengthIndexState _rsi;
    private readonly AwesomeOscillatorState _ao;
    private readonly IMovingAverageSmoother _macdFast;
    private readonly IMovingAverageSmoother _macdSlow;
    private readonly IMovingAverageSmoother _macdSignal;
    private readonly IchimokuCloudState _ichimoku;
    private readonly AverageDirectionalIndexState _adx;
    private readonly CommodityChannelIndexState _cci;
    private readonly ElderRayIndexState _elderRay;
    private readonly HullMovingAverageState _hma;
    private readonly WilliamsRState _williamsR;
    private readonly MomentumOscillatorState _momentum;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevRsi;
    private double _prevKSto;
    private double _prevDSto;
    private double _prevCci;
    private double _prevAdxPlus;
    private double _prevAdxMinus;
    private double _prevAo1;
    private double _prevAo2;
    private double _prevMom;
    private double _prevKStoRsi;
    private double _prevDStoRsi;
    private double _prevWr;
    private double _prevBullPower;
    private double _prevBearPower;
    private bool _hasPrev;

    public TechnicalRatingsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int aoLength1 = 55, int aoLength2 = 34, int rsiLength = 14, int stochLength1 = 14, int stochLength2 = 3,
        int stochLength3 = 3, int ultOscLength1 = 7, int ultOscLength2 = 14, int ultOscLength3 = 28,
        int ichiLength1 = 9, int ichiLength2 = 26, int ichiLength3 = 52, int vwmaLength = 20, int cciLength = 20,
        int adxLength = 14, int momLength = 10, int macdLength1 = 12, int macdLength2 = 26, int macdLength3 = 9,
        int bullBearLength = 13, int williamRLength = 14, int maLength1 = 10, int maLength2 = 20, int maLength3 = 30,
        int maLength4 = 50, int maLength5 = 100, int maLength6 = 200, int hullMaLength = 9)
    {
        _ = stochLength3;
        _uoLength1 = Math.Max(1, ultOscLength1);
        _uoLength2 = Math.Max(1, ultOscLength2);
        _uoLength3 = Math.Max(1, ultOscLength3);
        _vwmaLength = Math.Max(1, vwmaLength);
        var resolvedStoch = Math.Max(1, stochLength1);
        var resolvedStochSmooth = Math.Max(1, stochLength2);

        _bpSum1 = new RollingWindowSum(_uoLength1);
        _bpSum2 = new RollingWindowSum(_uoLength2);
        _bpSum3 = new RollingWindowSum(_uoLength3);
        _trSum1 = new RollingWindowSum(_uoLength1);
        _trSum2 = new RollingWindowSum(_uoLength2);
        _trSum3 = new RollingWindowSum(_uoLength3);
        _ma10 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength1));
        _ma20 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength2));
        _ma30 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength3));
        _ma50 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength4));
        _ma100 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength5));
        _ma200 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, maLength6));
        _vwmaVolumeSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, _vwmaLength);
        _vwmaVolumePriceSum = new RollingWindowSum(_vwmaLength);
        _stochHighWindow = new RollingWindowMax(resolvedStoch);
        _stochLowWindow = new RollingWindowMin(resolvedStoch);
        _stochFastSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedStochSmooth);
        _stochRsiHighWindow = new RollingWindowMax(resolvedStoch);
        _stochRsiLowWindow = new RollingWindowMin(resolvedStoch);
        _stochRsiFastSmoother = MovingAverageSmootherFactory.Create(MovingAvgType.SimpleMovingAverage, resolvedStochSmooth);
        _rsi = new RelativeStrengthIndexState(Math.Max(1, rsiLength), 3);
        _ao = new AwesomeOscillatorState(Math.Max(1, aoLength1), Math.Max(1, aoLength2));
        _macdFast = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, macdLength1));
        _macdSlow = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, macdLength2));
        _macdSignal = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, macdLength3));
        _ichimoku = new IchimokuCloudState(ichiLength1, ichiLength2, ichiLength3);
        _adx = new AverageDirectionalIndexState(Math.Max(1, adxLength));
        _cci = new CommodityChannelIndexState(MovingAvgType.SimpleMovingAverage, Math.Max(1, cciLength), 0.015);
        _elderRay = new ElderRayIndexState(MovingAvgType.ExponentialMovingAverage, Math.Max(1, bullBearLength));
        _hma = new HullMovingAverageState(MovingAvgType.WeightedMovingAverage, Math.Max(1, hullMaLength));
        _williamsR = new WilliamsRState(Math.Max(1, williamRLength));
        _momentum = new MomentumOscillatorState(MovingAvgType.WeightedMovingAverage, Math.Max(1, momLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TechnicalRatings;

    // No resolver of its own: the inner states that default to a typical or median price are the
    // ones that must switch to reading the close when this state is wrapped.
    void ICustomInputConsumer.ReadCloseAsInput()
    {
        ((ICustomInputConsumer)_ao).ReadCloseAsInput();
        ((ICustomInputConsumer)_cci).ReadCloseAsInput();
    }

    public void Reset()
    {
        _bpSum1.Reset();
        _bpSum2.Reset();
        _bpSum3.Reset();
        _trSum1.Reset();
        _trSum2.Reset();
        _trSum3.Reset();
        _ma10.Reset();
        _ma20.Reset();
        _ma30.Reset();
        _ma50.Reset();
        _ma100.Reset();
        _ma200.Reset();
        _vwmaVolumeSmoother.Reset();
        _vwmaVolumePriceSum.Reset();
        _stochHighWindow.Reset();
        _stochLowWindow.Reset();
        _stochFastSmoother.Reset();
        _stochRsiHighWindow.Reset();
        _stochRsiLowWindow.Reset();
        _stochRsiFastSmoother.Reset();
        _rsi.Reset();
        _ao.Reset();
        _macdFast.Reset();
        _macdSlow.Reset();
        _macdSignal.Reset();
        _ichimoku.Reset();
        _adx.Reset();
        _cci.Reset();
        _elderRay.Reset();
        _hma.Reset();
        _williamsR.Reset();
        _momentum.Reset();
        _prevValue = 0;
        _prevRsi = 0;
        _prevKSto = 0;
        _prevDSto = 0;
        _prevCci = 0;
        _prevAdxPlus = 0;
        _prevAdxMinus = 0;
        _prevAo1 = 0;
        _prevAo2 = 0;
        _prevMom = 0;
        _prevKStoRsi = 0;
        _prevDStoRsi = 0;
        _prevWr = 0;
        _prevBullPower = 0;
        _prevBearPower = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var rsi = _rsi.Update(bar, isFinal, includeOutputs: false).Value;
        var prevRsi = _hasPrev ? _prevRsi : 0;

        var ma10 = _ma10.Next(value, isFinal);
        var ma20 = _ma20.Next(value, isFinal);
        var ma30 = _ma30.Next(value, isFinal);
        var ma50 = _ma50.Next(value, isFinal);
        var ma100 = _ma100.Next(value, isFinal);
        var ma200 = _ma200.Next(value, isFinal);
        var hma = _hma.Update(bar, isFinal, includeOutputs: false).Value;

        var volume = bar.Volume;
        var volumeSma = _vwmaVolumeSmoother.Next(volume, isFinal);
        var volumePrice = value * volume;
        int vwmaCount;
        var volumePriceSum = isFinal
            ? _vwmaVolumePriceSum.Add(volumePrice, out vwmaCount)
            : _vwmaVolumePriceSum.Preview(volumePrice, out vwmaCount);
        var volumePriceAvg = vwmaCount > 0 ? volumePriceSum / vwmaCount : 0;
        var vwma = volumeSma != 0 ? volumePriceAvg / volumeSma : 0;

        var stochHigh = isFinal ? _stochHighWindow.Add(bar.High, out _) : _stochHighWindow.Preview(bar.High, out _);
        var stochLow = isFinal ? _stochLowWindow.Add(bar.Low, out _) : _stochLowWindow.Preview(bar.Low, out _);
        var stochRange = stochHigh - stochLow;
        var kSto = stochRange != 0 ? MathHelper.MinOrMax((value - stochLow) / stochRange * 100, 100, 0) : 0;
        var dSto = _stochFastSmoother.Next(kSto, isFinal);
        var prevKSto = _hasPrev ? _prevKSto : 0;
        var prevDSto = _hasPrev ? _prevDSto : 0;

        var stochRsiHigh = isFinal ? _stochRsiHighWindow.Add(rsi, out _) : _stochRsiHighWindow.Preview(rsi, out _);
        var stochRsiLow = isFinal ? _stochRsiLowWindow.Add(rsi, out _) : _stochRsiLowWindow.Preview(rsi, out _);
        var stochRsiRange = stochRsiHigh - stochRsiLow;
        var kStoRsi = stochRsiRange != 0 ? MathHelper.MinOrMax((rsi - stochRsiLow) / stochRsiRange * 100, 100, 0) : 0;
        var dStoRsi = _stochRsiFastSmoother.Next(kStoRsi, isFinal);
        var prevKStoRsi = _hasPrev ? _prevKStoRsi : 0;
        var prevDStoRsi = _hasPrev ? _prevDStoRsi : 0;

        var ao = _ao.Update(bar, isFinal, includeOutputs: false).Value;
        var prevAo1 = _hasPrev ? _prevAo1 : 0;
        var prevAo2 = _hasPrev ? _prevAo2 : 0;

        var macdFast = _macdFast.Next(value, isFinal);
        var macdSlow = _macdSlow.Next(value, isFinal);
        var macd = macdFast - macdSlow;
        var macdSig = _macdSignal.Next(macd, isFinal);

        var previousForPressure = _hasPrev ? prevValue : value;
        var minValue = Math.Min(bar.Low, previousForPressure);
        var maxValue = Math.Max(bar.High, previousForPressure);
        var bp = value - minValue;
        var tr = maxValue - minValue;
        var bpSum1 = isFinal ? _bpSum1.Add(bp, out _) : _bpSum1.Preview(bp, out _);
        var bpSum2 = isFinal ? _bpSum2.Add(bp, out _) : _bpSum2.Preview(bp, out _);
        var bpSum3 = isFinal ? _bpSum3.Add(bp, out _) : _bpSum3.Preview(bp, out _);
        var trSum1 = isFinal ? _trSum1.Add(tr, out _) : _trSum1.Preview(tr, out _);
        var trSum2 = isFinal ? _trSum2.Add(tr, out _) : _trSum2.Preview(tr, out _);
        var trSum3 = isFinal ? _trSum3.Add(tr, out _) : _trSum3.Preview(tr, out _);
        var avg1 = trSum1 != 0 ? bpSum1 / trSum1 : 0;
        var avg2 = trSum2 != 0 ? bpSum2 / trSum2 : 0;
        var avg3 = trSum3 != 0 ? bpSum3 / trSum3 : 0;
        var uo = MathHelper.MinOrMax(100 * (((4 * avg1) + (2 * avg2) + avg3) / 7), 100, 0);

        var ichimokuResult = _ichimoku.Update(bar, isFinal, includeOutputs: true);
        var ichimokuOutputs = ichimokuResult.Outputs!;
        var conLine = ichimokuOutputs["TenkanSen"];
        var baseLine = ichimokuOutputs["KijunSen"];
        var leadLine1 = ichimokuOutputs["SenkouSpanA"];
        var leadLine2 = ichimokuOutputs["SenkouSpanB"];

        var adxResult = _adx.Update(bar, isFinal, includeOutputs: true);
        var adxOutputs = adxResult.Outputs!;
        var adx = adxResult.Value;
        var adxPlus = adxOutputs["DiPlus"];
        var adxMinus = adxOutputs["DiMinus"];
        var prevAdxPlus = _hasPrev ? _prevAdxPlus : 0;
        var prevAdxMinus = _hasPrev ? _prevAdxMinus : 0;

        var cci = _cci.Update(bar, isFinal, includeOutputs: false).Value;
        var prevCci = _hasPrev ? _prevCci : 0;

        var elderResult = _elderRay.Update(bar, isFinal, includeOutputs: true);
        var elderOutputs = elderResult.Outputs!;
        var bullPower = elderOutputs["BullPower"];
        var bearPower = elderOutputs["BearPower"];
        var prevBullPower = _hasPrev ? _prevBullPower : 0;
        var prevBearPower = _hasPrev ? _prevBearPower : 0;

        var wr = _williamsR.Update(bar, isFinal, includeOutputs: false).Value;
        var prevWr = _hasPrev ? _prevWr : 0;

        var mom = _momentum.Update(bar, isFinal, includeOutputs: false).Value;
        var prevMom = _hasPrev ? _prevMom : 0;

        var upTrend = TechnicalRatingComparison.Compare(value, ma50) > 0;
        var dnTrend = TechnicalRatingComparison.Compare(value, ma50) < 0;

        double maRating = 0;
        maRating += TechnicalRatingComparison.Compare(value, ma10);
        maRating += TechnicalRatingComparison.Compare(value, ma20);
        maRating += TechnicalRatingComparison.Compare(value, ma30);
        maRating += TechnicalRatingComparison.Compare(value, ma50);
        maRating += TechnicalRatingComparison.Compare(value, ma100);
        maRating += TechnicalRatingComparison.Compare(value, ma200);
        maRating += TechnicalRatingComparison.Compare(value, hma);
        maRating += TechnicalRatingComparison.Compare(value, vwma);
        maRating += TechnicalRatingComparison.Compare(leadLine1, leadLine2) > 0 && TechnicalRatingComparison.Compare(value, leadLine1) > 0 && TechnicalRatingComparison.Compare(value, baseLine) < 0 && TechnicalRatingComparison.Compare(prevValue, conLine) < 0 &&
            TechnicalRatingComparison.Compare(value, conLine) > 0 ? 1 : TechnicalRatingComparison.Compare(leadLine2, leadLine1) > 0 && TechnicalRatingComparison.Compare(value, leadLine2) < 0 && TechnicalRatingComparison.Compare(value, baseLine) > 0 && TechnicalRatingComparison.Compare(prevValue, conLine) > 0 &&
            TechnicalRatingComparison.Compare(value, conLine) < 0 ? -1 : 0;
        maRating /= 9;

        double oscRating = 0;
        oscRating += TechnicalRatingComparison.Compare(rsi, 30) < 0 && TechnicalRatingComparison.Compare(prevRsi, rsi) < 0 ? 1 : TechnicalRatingComparison.Compare(rsi, 70) > 0 && TechnicalRatingComparison.Compare(prevRsi, rsi) > 0 ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(kSto, 20) < 0 && TechnicalRatingComparison.Compare(dSto, 20) < 0 && TechnicalRatingComparison.Compare(kSto, dSto) > 0 && TechnicalRatingComparison.Compare(prevKSto, prevDSto) < 0 ? 1 :
            TechnicalRatingComparison.Compare(kSto, 80) > 0 && TechnicalRatingComparison.Compare(dSto, 80) > 0 && TechnicalRatingComparison.Compare(kSto, dSto) < 0 && TechnicalRatingComparison.Compare(prevKSto, prevDSto) > 0 ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(cci, -100) < 0 && TechnicalRatingComparison.Compare(cci, prevCci) > 0 ? 1 : TechnicalRatingComparison.Compare(cci, 100) > 0 && TechnicalRatingComparison.Compare(cci, prevCci) < 0 ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(adx, 20) > 0 && TechnicalRatingComparison.Compare(prevAdxPlus, prevAdxMinus) < 0 && TechnicalRatingComparison.Compare(adxPlus, adxMinus) > 0 ? 1 :
            TechnicalRatingComparison.Compare(adx, 20) > 0 && TechnicalRatingComparison.Compare(prevAdxPlus, prevAdxMinus) > 0 && TechnicalRatingComparison.Compare(adxPlus, adxMinus) < 0 ? -1 : 0;
        oscRating += (TechnicalRatingComparison.Compare(ao, 0) > 0 && TechnicalRatingComparison.Compare(prevAo1, 0) < 0) || (TechnicalRatingComparison.Compare(ao, 0) > 0 && TechnicalRatingComparison.Compare(prevAo1, 0) > 0 && TechnicalRatingComparison.Compare(ao, prevAo1) > 0 && TechnicalRatingComparison.Compare(prevAo2, prevAo1) > 0) ? 1 :
            (TechnicalRatingComparison.Compare(ao, 0) < 0 && TechnicalRatingComparison.Compare(prevAo1, 0) > 0) || (TechnicalRatingComparison.Compare(ao, 0) < 0 && TechnicalRatingComparison.Compare(prevAo1, 0) < 0 && TechnicalRatingComparison.Compare(ao, prevAo1) < 0 && TechnicalRatingComparison.Compare(prevAo2, prevAo1) < 0) ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(mom, prevMom);
        oscRating += TechnicalRatingComparison.Compare(macd, macdSig);
        oscRating += dnTrend && TechnicalRatingComparison.Compare(kStoRsi, 20) < 0 && TechnicalRatingComparison.Compare(dStoRsi, 20) < 0 && TechnicalRatingComparison.Compare(kStoRsi, dStoRsi) > 0 && TechnicalRatingComparison.Compare(prevKStoRsi, prevDStoRsi) < 0 ? 1 :
            upTrend && TechnicalRatingComparison.Compare(kStoRsi, 80) > 0 && TechnicalRatingComparison.Compare(dStoRsi, 80) > 0 && TechnicalRatingComparison.Compare(kStoRsi, dStoRsi) < 0 && TechnicalRatingComparison.Compare(prevKStoRsi, prevDStoRsi) > 0 ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(wr, -80) < 0 && TechnicalRatingComparison.Compare(wr, prevWr) > 0 ? 1 : TechnicalRatingComparison.Compare(wr, -20) > 0 && TechnicalRatingComparison.Compare(wr, prevWr) < 0 ? -1 : 0;
        oscRating += upTrend && TechnicalRatingComparison.Compare(bearPower, 0) < 0 && TechnicalRatingComparison.Compare(bearPower, prevBearPower) > 0 ? 1 :
            dnTrend && TechnicalRatingComparison.Compare(bullPower, 0) > 0 && TechnicalRatingComparison.Compare(bullPower, prevBullPower) < 0 ? -1 : 0;
        oscRating += TechnicalRatingComparison.Compare(uo, 70) > 0 ? 1 : TechnicalRatingComparison.Compare(uo, 30) < 0 ? -1 : 0;
        oscRating /= 11;

        var totalRating = (maRating + oscRating) / 2;

        if (isFinal)
        {
            _prevValue = value;
            _prevRsi = rsi;
            _prevKSto = kSto;
            _prevDSto = dSto;
            _prevCci = cci;
            _prevAdxPlus = adxPlus;
            _prevAdxMinus = adxMinus;
            _prevAo2 = _prevAo1;
            _prevAo1 = ao;
            _prevMom = mom;
            _prevKStoRsi = kStoRsi;
            _prevDStoRsi = dStoRsi;
            _prevWr = wr;
            _prevBullPower = bullPower;
            _prevBearPower = bearPower;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "Tr", totalRating },
                { "Or", oscRating },
                { "Mr", maRating }
            };
        }

        return new StreamingIndicatorStateResult(totalRating, outputs);
    }

public void Dispose()
{
    _bpSum1.Dispose();
        _bpSum2.Dispose();
        _bpSum3.Dispose();
        _trSum1.Dispose();
        _trSum2.Dispose();
        _trSum3.Dispose();
        _ma10.Dispose();
        _ma20.Dispose();
        _ma30.Dispose();
        _ma50.Dispose();
        _ma100.Dispose();
        _ma200.Dispose();
        _vwmaVolumeSmoother.Dispose();
        _vwmaVolumePriceSum.Dispose();
        _stochHighWindow.Dispose();
        _stochLowWindow.Dispose();
        _stochFastSmoother.Dispose();
        _stochRsiHighWindow.Dispose();
        _stochRsiLowWindow.Dispose();
        _stochRsiFastSmoother.Dispose();
        _ao.Dispose();
        _macdFast.Dispose();
        _macdSlow.Dispose();
        _macdSignal.Dispose();
        _ichimoku.Dispose();
        _cci.Dispose();
        _elderRay.Dispose();
        _hma.Dispose();
    _williamsR.Dispose();
    _momentum.Dispose();
}
}

[PrimaryOutput("TfsMob")]
public sealed class TFSMboIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public TFSMboIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 25,
        int slowLength = 200, int signalLength = 18)
    {
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TFSMboIndicator;

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
        var mob1 = _fast.Next(value, isFinal);
        var mob2 = _slow.Next(value, isFinal);
        var tfsMob = mob1 - mob2;
        var invalid = _signalInvalid || double.IsInfinity(tfsMob) || double.IsNaN(tfsMob);
        var signal = invalid ? double.NaN : _signal.Next(tfsMob, isFinal);
        if (isFinal) _signalInvalid = invalid;
        var histogram = tfsMob - signal;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "TfsMob", tfsMob },
                { "Signal", signal },
                { "Histogram", histogram }
            };
        }

        return new StreamingIndicatorStateResult(tfsMob, outputs);
    }

    public void Dispose()
    {
        _fast.Dispose();
        _slow.Dispose();
        _signal.Dispose();
    }
}

[PrimaryOutput("Ppo")]
public sealed class TFSMboPercentagePriceOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _fast;
    private readonly IMovingAverageSmoother _slow;
    private readonly IMovingAverageSmoother _signal;
    private readonly StreamingInputResolver _input;
    private bool _signalInvalid;

    public TFSMboPercentagePriceOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 25, int slowLength = 200, int signalLength = 18)
    {
        _fast = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, fastLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, fastLength));
        _slow = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, slowLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, slowLength));
        _signal = maType == MovingAvgType.SimpleMovingAverage ? new RoundedSimpleMovingAverageSmoother(Math.Max(1, signalLength)) : MovingAverageSmootherFactory.Create(maType, Math.Max(1, signalLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TFSMboPercentagePriceOscillator;

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
        var mob1 = _fast.Next(value, isFinal);
        var mob2 = _slow.Next(value, isFinal);
        var ppo = RoundedPercentageChange.Of(mob1, mob2);
        var invalid = _signalInvalid || double.IsInfinity(ppo) || double.IsNaN(ppo);
        var signal = invalid ? double.NaN : _signal.Next(ppo, isFinal);
        if (isFinal) _signalInvalid = invalid;
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

[PrimaryOutput("Tether")]
public sealed class TFSTetherLineIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public TFSTetherLineIndicatorState(int length = 50)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TFSTetherLineIndicator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);
        var tether = PriceMean.Of(highest, lowest);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tether", tether }
            };
        }

        return new StreamingIndicatorStateResult(tether, outputs);
    }

public void Dispose()
{
    _highWindow.Dispose();
    _lowWindow.Dispose();
}
}

[PrimaryOutput("Tabf")]
public sealed class TopsAndBottomsFinderState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;

    // The deviation of each window about its own mean, matching the batch calculation; see #190. One measures
    // the rises and the other the falls, and they must not be crossed.
    private readonly RollingStandardDeviation _bStdDev;
    private readonly RollingStandardDeviation _cStdDev;
    private readonly StreamingInputResolver _input;
    private double _prevEma;
    private double _prevUp;
    private double _prevDn;
    private bool _hasPrev;

    public TopsAndBottomsFinderState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 50)
    {
        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        // No moving-average type, and no selector: RollingStandardDeviation is handed the value itself, so
        // neither series has to be smuggled in through a closure over a field.
        _bStdDev = new RollingStandardDeviation(resolved);
        _cStdDev = new RollingStandardDeviation(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TopsAndBottomsFinder;

    public void Reset()
    {
        _ema.Reset();
        _bStdDev.Reset();
        _cStdDev.Reset();
        _prevEma = 0;
        _prevUp = 0;
        _prevDn = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ema = _ema.Next(value, isFinal);
        var prevEma = _hasPrev ? _prevEma : 0;
        // Locals, not fields: each is computed and consumed within this one bar, and nothing carries them to
        // the next. Held as fields they would read like the state above them - _prevEma and the rest do carry
        // forward - which is state this indicator does not have. They were fields only because the deviation
        // state they fed resolved its own input and had to be handed a closure over something.
        var bValue = ema > prevEma ? ema : 0;
        var cValue = ema < prevEma ? ema : 0;

        // Each deviation is fed the series it measures: the rises, and the falls.
        var bStd = _bStdDev.Next(bValue, isFinal);
        var cStd = _cStdDev.Next(cValue, isFinal);

        var prevUp = _hasPrev ? _prevUp : 0;
        var prevDn = _hasPrev ? _prevDn : 0;
        var up = ema + bStd != 0 ? ema / (ema + bStd) : 0;
        var dn = ema + cStd != 0 ? ema / (ema + cStd) : 0;
        var os = prevUp == 1 && up != 1 ? 1 : prevDn == 1 && dn != 1 ? -1 : 0;

        if (isFinal)
        {
            _prevEma = ema;
            _prevUp = up;
            _prevDn = dn;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tabf", os }
            };
        }

        return new StreamingIndicatorStateResult(os, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _bStdDev.Dispose();
        _cStdDev.Dispose();
    }
}

[PrimaryOutput("TotalPower")]
public sealed class TotalPowerIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly RollingWindowSum _bullCountSum;
    private readonly RollingWindowSum _bearCountSum;
    private readonly ElderRayIndexState _elderRay;

    public TotalPowerIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 45, int length2 = 10)
    {
        _length1 = Math.Max(1, length1);
        _bullCountSum = new RollingWindowSum(_length1);
        _bearCountSum = new RollingWindowSum(_length1);
        _elderRay = new ElderRayIndexState(maType, Math.Max(1, length2));
    }

    public IndicatorName Name => IndicatorName.TotalPowerIndicator;

    public void Reset()
    {
        _bullCountSum.Reset();
        _bearCountSum.Reset();
        _elderRay.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var elderResult = _elderRay.Update(bar, isFinal, includeOutputs: true);
        var elderOutputs = elderResult.Outputs!;
        var bullPower = elderOutputs["BullPower"];
        var bearPower = elderOutputs["BearPower"];

        var bullCount = bullPower > 0 ? 1 : 0;
        var bearCount = bearPower < 0 ? 1 : 0;
        var bullCountSum = isFinal ? _bullCountSum.Add(bullCount, out _) : _bullCountSum.Preview(bullCount, out _);
        var bearCountSum = isFinal ? _bearCountSum.Add(bearCount, out _) : _bearCountSum.Preview(bearCount, out _);

        var totalPower = _length1 != 0 ? 100 * Math.Abs(bullCountSum - bearCountSum) / _length1 : 0;
        var adjBullCount = _length1 != 0 ? 100 * bullCountSum / _length1 : 0;
        var adjBearCount = _length1 != 0 ? 100 * bearCountSum / _length1 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "TotalPower", totalPower },
                { "BullCount", adjBullCount },
                { "BearCount", adjBearCount }
            };
        }

        return new StreamingIndicatorStateResult(totalPower, outputs);
    }

    public void Dispose()
    {
        _bullCountSum.Dispose();
        _bearCountSum.Dispose();
        _elderRay.Dispose();
    }
}

[PrimaryOutput("Tpx")]
public sealed class TraderPressureIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly TraderPressureWindow _window;
    public TraderPressureIndexState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 7, int length2 = 2, int smoothLength = 3) =>
        _window = new TraderPressureWindow(maType, length1, length2, smoothLength);
    public IndicatorName Name => IndicatorName.TraderPressureIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Tpx", value.Net }, { "Bulls", value.Bulls }, { "Bears", value.Bears } } : null;
        return new StreamingIndicatorStateResult(value.Net, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tdi")]
public sealed class TradersDynamicIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly RsiState _rsi;
    private readonly IMovingAverageSmoother _rsiSignal;

    // The deviation of the window about its own mean, matching the batch calculation; see #190.
    private readonly RollingStandardDeviation _stdDev;
    private readonly IMovingAverageSmoother _mabSmoother;
    private readonly IMovingAverageSmoother _mbbSmoother;
    private readonly StreamingInputResolver _input;

    public TradersDynamicIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 13, int length2 = 34, int length3 = 2, int length4 = 7)
    {
        _rsi = new RsiState(maType, Math.Max(1, length1));
        _rsiSignal = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length2));
        // No moving-average type, and no selector: the index is passed to Next directly.
        _stdDev = new RollingStandardDeviation(Math.Max(1, length2));
        _mabSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length3));
        _mbbSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length4));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TradersDynamicIndex;

    public void Reset()
    {
        _rsi.Reset();
        _rsiSignal.Reset();
        _stdDev.Reset();
        _mabSmoother.Reset();
        _mbbSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);
        var rsiSignal = _rsiSignal.Next(rsi, isFinal);

        // Fed the relative strength index, which is the series this measures, matching the batch calculation.
        var stdDev = _stdDev.Next(rsi, isFinal);
        var mab = _mabSmoother.Next(rsi, isFinal);
        var mbb = _mbbSmoother.Next(rsi, isFinal);
        var offs = 1.6185 * stdDev;
        var up = rsiSignal + offs;
        var dn = rsiSignal - offs;
        var mid = (up + dn) / 2;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(5)
            {
                { "UpperBand", up },
                { "MiddleBand", mid },
                { "LowerBand", dn },
                { "Tdi", mab },
                { "Signal", mbb }
            };
        }

        return new StreamingIndicatorStateResult(mab, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _rsiSignal.Dispose();
        _stdDev.Dispose();
        _mabSmoother.Dispose();
        _mbbSmoother.Dispose();
    }
}

[PrimaryOutput("Tvi")]
public sealed class TradeVolumeIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly TradeVolumeWindow _window;
    public TradeVolumeIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double minTickValue = 0.5)
        => _window = new TradeVolumeWindow(maType, length, minTickValue);
    public IndicatorName Name => IndicatorName.TradeVolumeIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Tvi", value.Line }, { "Signal", value.Signal } } : null;
        return new StreamingIndicatorStateResult(value.Line, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tmmso")]
public sealed class TradingMadeMoreSimplerOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly double _threshold;
    private readonly double _limit;
    private readonly RsiState _rsi;
    private readonly RollingWindowMax _stoch1High;
    private readonly RollingWindowMin _stoch1Low;
    private readonly RollingWindowMax _stoch2High;
    private readonly RollingWindowMin _stoch2Low;
    private readonly IMovingAverageSmoother _stoch1Smoother;
    private readonly IMovingAverageSmoother _stoch2Smoother;
    private readonly StreamingInputResolver _input;

    public TradingMadeMoreSimplerOscillatorState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length1 = 14, int length2 = 8, int length3 = 12, int smoothLength = 3,
        double threshold = 50, double limit = 0)
    {
        _ = length3;
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _threshold = threshold;
        _limit = limit;
        _rsi = new RsiState(maType, _length1);
        _stoch1High = new RollingWindowMax(_length2);
        _stoch1Low = new RollingWindowMin(_length2);
        _stoch2High = new RollingWindowMax(_length1);
        _stoch2Low = new RollingWindowMin(_length1);
        _stoch1Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _stoch2Smoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TradingMadeMoreSimplerOscillator;

    public void Reset()
    {
        _rsi.Reset();
        _stoch1High.Reset();
        _stoch1Low.Reset();
        _stoch2High.Reset();
        _stoch2Low.Reset();
        _stoch1Smoother.Reset();
        _stoch2Smoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var rsi = _rsi.Next(value, isFinal);

        var stoch1High = isFinal ? _stoch1High.Add(bar.High, out _) : _stoch1High.Preview(bar.High, out _);
        var stoch1Low = isFinal ? _stoch1Low.Add(bar.Low, out _) : _stoch1Low.Preview(bar.Low, out _);
        var stoch1Range = stoch1High - stoch1Low;
        var stoch1 = stoch1Range != 0 ? MathHelper.MinOrMax((value - stoch1Low) / stoch1Range * 100, 100, 0) : 0;
        var stoch1FastD = _stoch1Smoother.Next(stoch1, isFinal);

        var stoch2High = isFinal ? _stoch2High.Add(bar.High, out _) : _stoch2High.Preview(bar.High, out _);
        var stoch2Low = isFinal ? _stoch2Low.Add(bar.Low, out _) : _stoch2Low.Preview(bar.Low, out _);
        var stoch2Range = stoch2High - stoch2Low;
        var stoch2 = stoch2Range != 0 ? MathHelper.MinOrMax((value - stoch2Low) / stoch2Range * 100, 100, 0) : 0;
        var stoch2FastD = _stoch2Smoother.Next(stoch2, isFinal);

        var bufRsi = rsi - _threshold;
        var bufStoch1 = stoch1FastD - _threshold;
        var bufStoch2 = stoch2FastD - _threshold;
        var bufHistUp = bufRsi > _limit && bufStoch1 > _limit && bufStoch2 > _limit ? bufStoch2 : 0;
        var bufHistDn = bufRsi < _limit && bufStoch1 < _limit && bufStoch2 < _limit ? bufStoch2 : 0;
        var tmmso = bufHistUp - bufHistDn;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tmmso", tmmso }
            };
        }

        return new StreamingIndicatorStateResult(tmmso, outputs);
    }

    public void Dispose()
    {
        _rsi.Dispose();
        _stoch1High.Dispose();
        _stoch1Low.Dispose();
        _stoch2High.Dispose();
        _stoch2Low.Dispose();
        _stoch1Smoother.Dispose();
        _stoch2Smoother.Dispose();
    }
}

[PrimaryOutput("Tfsvo")]
public sealed class TFSVolumeOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeBalanceWindow _window;
    public TFSVolumeOscillatorState(int length = 7) => _window = new VolumeBalanceWindow(length, VolumeBalanceKind.Tfs);
    public IndicatorName Name => IndicatorName.TFSVolumeOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Tfsvo", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tri")]
public sealed class TheRangeIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _triSmoother;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private bool _hasPrev;

    public TheRangeIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 10,
        int smoothLength = 3)
    {
        _ = smoothLength;
        _length = Math.Max(1, length);
        _maxWindow = new RollingWindowMax(_length);
        _minWindow = new RollingWindowMin(_length);
        _triSmoother = MovingAverageSmootherFactory.Create(maType, _length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TheRangeIndicator;

    public void Reset()
    {
        _maxWindow.Reset();
        _minWindow.Reset();
        _triSmoother.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        // For TrueRange on first bar, use current close to avoid inflated TR
        var prevValue = _hasPrev ? _prevValue : value;
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, prevValue);
        var v1 = _hasPrev && value > prevValue ? tr / (value - prevValue) : tr;
        var v2 = isFinal ? _minWindow.Add(v1, out _) : _minWindow.Preview(v1, out _);
        var v3 = isFinal ? _maxWindow.Add(v1, out _) : _maxWindow.Preview(v1, out _);
        var stoch = v3 - v2 != 0
            ? MathHelper.MinOrMax(100 * (v1 - v2) / (v3 - v2), 100, 0)
            : MathHelper.MinOrMax(100 * (v1 - v2), 100, 0);
        var tri = _triSmoother.Next(stoch, isFinal);

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
                { "Tri", tri }
            };
        }

        return new StreamingIndicatorStateResult(tri, outputs);
    }

    public void Dispose()
    {
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _triSmoother.Dispose();
    }
}

[PrimaryOutput("Tlmo")]
public sealed class TickLineMomentumOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _smoothLength;
    private readonly IMovingAverageSmoother _maSmoother;
    private readonly IMovingAverageSmoother _rocSmoother;
    private readonly PooledRingBuffer<double> _cumoValues;
    private readonly StreamingInputResolver _input;
    private double _cumoSum;
    private double _prevMa;
    private int _index;
    private bool _hasPrev;

    public TickLineMomentumOscillatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 10, int smoothLength = 5)
    {
        _smoothLength = Math.Max(1, smoothLength);
        _maSmoother = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _rocSmoother = MovingAverageSmootherFactory.Create(maType, _smoothLength);
        _cumoValues = new PooledRingBuffer<double>(_smoothLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TickLineMomentumOscillator;

    public void Reset()
    {
        _maSmoother.Reset();
        _rocSmoother.Reset();
        _cumoValues.Clear();
        _cumoSum = 0;
        _prevMa = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var ma = _maSmoother.Next(value, isFinal);
        var prevMa = _hasPrev ? _prevMa : 0;
        var cumo = value > prevMa ? 1 : value < prevMa ? -1 : 0;
        var cumoSum = _cumoSum + cumo;
        var prevCumoSum = _index >= _smoothLength
            ? EhlersStreamingWindow.GetOffsetValue(_cumoValues, cumoSum, _smoothLength)
            : 0;
        var roc = RoundedPercentageChange.Of(cumoSum, prevCumoSum);
        var tlmo = _rocSmoother.Next(roc, isFinal);

        if (isFinal)
        {
            _cumoSum = cumoSum;
            _prevMa = ma;
            _cumoValues.TryAdd(cumoSum, out _);
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Tlmo", tlmo }
            };
        }

        return new StreamingIndicatorStateResult(tlmo, outputs);
    }

    public void Dispose()
    {
        _maSmoother.Dispose();
        _rocSmoother.Dispose();
        _cumoValues.Dispose();
    }
}

[PrimaryOutput("Ie2")]
public sealed class TillsonIE2State : IStreamingIndicatorState, IDisposable
{
    private readonly TillsonIe2Window _window;
    public TillsonIE2State(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 15) => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.TillsonIE2;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Ie2", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("T3")]
public sealed class TillsonT3MovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly TillsonWindow _window;
    public TillsonT3MovingAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5, double vFactor = 0.7) => _window = new(maType, length, vFactor);
    public IndicatorName Name => IndicatorName.TillsonT3MovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "T3", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Median")]
public sealed class TimeAndMoneyChannelState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length1;
    private readonly int _length2;
    private readonly int _halfLength;
    private readonly IMovingAverageSmoother _basisSmoother;
    private readonly IMovingAverageSmoother _avyomSmoother;
    private readonly IMovingAverageSmoother _yomSquaredSmoother;
    private readonly IMovingAverageSmoother _sigomSmoother;
    private readonly PooledRingBuffer<double> _basisValues;
    private readonly PooledRingBuffer<double> _varyomValues;
    private readonly StreamingInputResolver _input;
    private int _index;
    private readonly RollingStandardDeviation? _centeredDeviation;

    public TimeAndMoneyChannelState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 41,
        int length2 = 82)
    {
        _centeredDeviation = maType == MovingAvgType.SimpleMovingAverage ? new RollingStandardDeviation(length2) : null;
        _length1 = Math.Max(1, length1);
        _length2 = Math.Max(1, length2);
        _halfLength = MathHelper.MinOrMax((int)Math.Ceiling((double)_length1 / 2));
        _basisSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _avyomSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
        _yomSquaredSmoother = MovingAverageSmootherFactory.Create(maType, _length2);
        _sigomSmoother = MovingAverageSmootherFactory.Create(maType, _length1);
        _basisValues = new PooledRingBuffer<double>(_halfLength);
        _varyomValues = new PooledRingBuffer<double>(_halfLength);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TimeAndMoneyChannel;

    public void Reset()
    {
        _centeredDeviation?.Reset();
        _basisSmoother.Reset();
        _avyomSmoother.Reset();
        _yomSquaredSmoother.Reset();
        _sigomSmoother.Reset();
        _basisValues.Clear();
        _varyomValues.Clear();
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var basis = _basisSmoother.Next(value, isFinal);
        var prevBasis = _index >= _halfLength ? EhlersStreamingWindow.GetOffsetValue(_basisValues, basis, _halfLength) : 0;
        var yom = prevBasis != 0 ? 100 * (value - prevBasis) / prevBasis : 0;
        var yomSquared = yom * yom;
        var avyom = _avyomSmoother.Next(yom, isFinal);
        var yomSquaredSma = _yomSquaredSmoother.Next(yomSquared, isFinal);
        var deviation = _centeredDeviation?.Next(yom, isFinal);
        var varyom = deviation.HasValue ? deviation.Value * deviation.Value : yomSquaredSma - (avyom * avyom);
        var prevVaryom = _index >= _halfLength ? EhlersStreamingWindow.GetOffsetValue(_varyomValues, varyom, _halfLength) : 0;
        var som = prevVaryom >= 0 ? MathHelper.Sqrt(prevVaryom) : 0;
        var sigom = _sigomSmoother.Next(som, isFinal);

        if (isFinal)
        {
            _basisValues.TryAdd(basis, out _);
            _varyomValues.TryAdd(varyom, out _);
            _index++;
        }

        var chPlus1 = basis * (1 + (0.01 * sigom));
        var chMinus1 = basis * (1 - (0.01 * sigom));
        var chPlus2 = basis * (1 + (0.02 * sigom));
        var chMinus2 = basis * (1 - (0.02 * sigom));
        var chPlus3 = basis * (1 + (0.03 * sigom));
        var chMinus3 = basis * (1 - (0.03 * sigom));

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(7)
            {
                { "Ch+1", chPlus1 },
                { "Ch-1", chMinus1 },
                { "Ch+2", chPlus2 },
                { "Ch-2", chMinus2 },
                { "Ch+3", chPlus3 },
                { "Ch-3", chMinus3 },
                { "Median", sigom }
            };
        }

        return new StreamingIndicatorStateResult(sigom, outputs);
    }

    public void Dispose()
    {
        _centeredDeviation?.Dispose();
        _basisSmoother.Dispose();
        _avyomSmoother.Dispose();
        _yomSquaredSmoother.Dispose();
        _sigomSmoother.Dispose();
        _basisValues.Dispose();
        _varyomValues.Dispose();
    }
}

[PrimaryOutput("UpperBand")]
public sealed class TimePriceIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;
    private double _prevHighest;
    private double _prevLowest;
    private int _lastRisingIndex;
    private int _lastFallingIndex;
    private int _index;
    private bool _hasPrev;

    public TimePriceIndicatorState(int length = 50)
    {
        _length = Math.Max(1, length);
        _highWindow = new RollingWindowMax(_length);
        _lowWindow = new RollingWindowMin(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
        _lastRisingIndex = -1;
        _lastFallingIndex = -1;
    }

    public IndicatorName Name => IndicatorName.TimePriceIndicator;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
        _prevHighest = 0;
        _prevLowest = 0;
        _lastRisingIndex = -1;
        _lastFallingIndex = -1;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var prevLowest = _hasPrev ? _prevLowest : 0;
        var rising = bar.High > prevHighest ? 1 : 0;
        var falling = bar.Low < prevLowest ? 1 : 0;
        var lastRisingIndex = rising == 1 ? _index : _lastRisingIndex;
        var lastFallingIndex = falling == 1 ? _index : _lastFallingIndex;

        var a = _index - lastRisingIndex;
        var b = _index - lastFallingIndex;
        var upper = _length != 0 ? ((a > _length ? _length : a) / (double)_length) - 0.5 : 0;
        var lower = _length != 0 ? ((b > _length ? _length : b) / (double)_length) - 0.5 : 0;

        if (isFinal)
        {
            _prevHighest = _highWindow.Add(bar.High, out _);
            _prevLowest = _lowWindow.Add(bar.Low, out _);
            _lastRisingIndex = lastRisingIndex;
            _lastFallingIndex = lastFallingIndex;
            _index++;
            _hasPrev = true;
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

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("Am")]
public sealed class TironeLevelsState : IStreamingIndicatorState, IDisposable   
{
    private readonly RollingWindowMax _highWindow;
    private readonly RollingWindowMin _lowWindow;
    private readonly StreamingInputResolver _input;

    public TironeLevelsState(int length = 20)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(resolved);
        _lowWindow = new RollingWindowMin(resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TironeLevels;

    public void Reset()
    {
        _highWindow.Reset();
        _lowWindow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var highest = isFinal ? _highWindow.Add(bar.High, out _) : _highWindow.Preview(bar.High, out _);
        var lowest = isFinal ? _lowWindow.Add(bar.Low, out _) : _lowWindow.Preview(bar.Low, out _);

        var tlh = highest - ((highest - lowest) / 3);
        var clh = lowest + ((highest - lowest) / 2);
        var blh = lowest + ((highest - lowest) / 3);
        var am = (highest + lowest + value) / 3;
        var eh = am + (highest - lowest);
        var el = am - (highest - lowest);
        var rh = (2 * am) - lowest;
        var rl = (2 * am) - highest;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(8)
            {
                { "Tlh", tlh },
                { "Clh", clh },
                { "Blh", blh },
                { "Am", am },
                { "Eh", eh },
                { "El", el },
                { "Rh", rh },
                { "Rl", rl }
            };
        }

        return new StreamingIndicatorStateResult(am, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _lowWindow.Dispose();
    }
}

[PrimaryOutput("Tai")]
public sealed class TrendAnalysisIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _inputSmoother;
    private readonly RollingWindowMax _maxWindow;
    private readonly RollingWindowMin _minWindow;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public TrendAnalysisIndexState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 28, int length2 = 5)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _inputSmoother = MovingAverageSmootherFactory.Create(maType, resolved1);
        _maxWindow = new RollingWindowMax(resolved2);
        _minWindow = new RollingWindowMin(resolved2);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrendAnalysisIndex;

    public void Reset()
    {
        _inputSmoother.Reset();
        _maxWindow.Reset();
        _minWindow.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var sma = _inputSmoother.Next(value, isFinal);
        var highest = isFinal ? _maxWindow.Add(sma, out _) : _maxWindow.Preview(sma, out _);
        var lowest = isFinal ? _minWindow.Add(sma, out _) : _minWindow.Preview(sma, out _);
        var tai = value != 0 ? (highest - lowest) * 100 / value : 0;
        var signal = _signalSmoother.Next(tai, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tai", tai },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(tai, outputs);
    }

    public void Dispose()
    {
        _inputSmoother.Dispose();
        _maxWindow.Dispose();
        _minWindow.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("Tai")]
public sealed class TrendAnalysisIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _slowMa;
    private readonly IMovingAverageSmoother _fastMa;
    // The deviation of the window about its own mean, matching the batch calculation; see #190. This state
    // publishes the deviation itself as Tai, so the conversion lands directly in its output.
    private readonly RollingStandardDeviation _stdDev;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;

    public TrendAnalysisIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 21, int length2 = 4)
    {
        var resolved1 = Math.Max(1, length1);
        var resolved2 = Math.Max(1, length2);
        _slowMa = MovingAverageSmootherFactory.Create(maType, resolved1);
        _fastMa = MovingAverageSmootherFactory.Create(maType, resolved2);
        // No moving-average type, and no selector: the slow average is passed to Next directly.
        _stdDev = new RollingStandardDeviation(resolved2);
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved1);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TrendAnalysisIndicator;

    public void Reset()
    {
        _slowMa.Reset();
        _fastMa.Reset();
        _stdDev.Reset();
        _signalSmoother.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        // The index is the deviation of the slow average, as the batch computes it: it chains the slow MA
        // into the deviation on purpose, and the average is now passed to Next rather than held in a field.
        var slowValue = _slowMa.Next(value, isFinal);
        _ = _fastMa.Next(value, isFinal);
        var tai = _stdDev.Next(slowValue, isFinal);
        var signal = _signalSmoother.Next(tai, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tai", tai },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(tai, outputs);
    }

    public void Dispose()
    {
        _slowMa.Dispose();
        _fastMa.Dispose();
        _stdDev.Dispose();
        _signalSmoother.Dispose();
    }
}

[PrimaryOutput("TcfPlus")]
public sealed class TrendContinuationFactorState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendContinuationWindow _window;
    public TrendContinuationFactorState(int length = 35) => _window = new(length);
    public IndicatorName Name => IndicatorName.TrendContinuationFactor;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Plus, includeOutputs ? new Dictionary<string, double> { { "TcfPlus", value.Plus }, { "TcfMinus", value.Minus } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tdi")]
public sealed class TrendDetectionIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendDetectionWindow _window;
    public TrendDetectionIndexState(int length1 = 20, int length2 = 40) => _window = new(length1, length2);
    public IndicatorName Name => IndicatorName.TrendDetectionIndex;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value.Line, includeOutputs ? new Dictionary<string, double> { { "Tdi", value.Line }, { "TdiDirection", value.Direction } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Tdfi")]
public sealed class TrendDirectionForceIndexState : IStreamingIndicatorState, IDisposable
{
    private readonly TrendForceKernel _kernel;
    public TrendDirectionForceIndexState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 10, int length2 = 30) => _kernel = new(maType, length1, length2);
    public IndicatorName Name => IndicatorName.TrendDirectionForceIndex;
    public void Reset() => _kernel.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _kernel.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { ["Tdfi"] = value } : null);
    }
    public void Dispose() => _kernel.Dispose();
}

[PrimaryOutput("Trender")]
public sealed class TrenderState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _ema;
    private readonly IMovingAverageSmoother _atr;
    private readonly RollingStandardDeviation _stdDev;
    private readonly IMovingAverageSmoother _adSmoother;
    private readonly SpreadAverage? _preciseAd;
    private readonly StreamingInputResolver _input;
    private readonly double _atrMult;
    private double _prevValue;
    private double _atrValue;
    private double _prevEma;
    private double _prevAdm;
    private double _prevTrndDn;
    private double _prevTrndUp;
    private double _prevTrndr;
    private double _prevHigh1;
    private double _prevHigh2;
    private double _prevLow1;
    private double _prevLow2;
    private int _index;
    private bool _hasPrev;

    public TrenderState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        double atrMult = 2)
    {
        var resolved = Math.Max(1, length);
        _ema = MovingAverageSmootherFactory.Create(maType, resolved);
        _atr = MovingAverageSmootherFactory.Create(maType, resolved);
        _stdDev = new RollingStandardDeviation(resolved);
        _adSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _preciseAd = maType == MovingAvgType.WeightedMovingAverage ? new SpreadAverage(maType, resolved) : null;
        _atrMult = atrMult;
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Trender;

    public void Reset()
    {
        _ema.Reset();
        _atr.Reset();
        _stdDev.Reset();
        _adSmoother.Reset();
        _preciseAd?.Reset();
        _prevValue = 0;
        _atrValue = 0;
        _prevEma = 0;
        _prevAdm = 0;
        _prevTrndDn = 0;
        _prevTrndUp = 0;
        _prevTrndr = 0;
        _prevHigh1 = 0;
        _prevHigh2 = 0;
        _prevLow1 = 0;
        _prevLow2 = 0;
        _index = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var ema = _ema.Next(value, isFinal);
        // The first bar has no previous close, so its true range is its own high - low, as the batch ATR
        // measures it. A previous close of 0 made it the whole high and inflated the first window's ATR.
        var tr = CalculationsHelper.CalculateTrueRange(bar.High, bar.Low, _hasPrev ? _prevValue : value);
        var atr = _atr.Next(tr, isFinal);
        // The batch takes the band's standard deviation of the ATR, not of the price: it chains the ATR
        // into StandardDeviationVolatility on purpose.
        _atrValue = atr;
        var ad = value > prevValue ? ema + (atr / 2) : value < prevValue ? ema - (atr / 2) : ema;
        var adm = _preciseAd is null ? _adSmoother.Next(ad, isFinal) : _preciseAd.Next(new(ad), isFinal).Value;
        var prevAdm = _hasPrev ? _prevAdm : 0;
        var prevEma = _hasPrev ? _prevEma : 0;
        var prevHigh = _index >= 2 ? _prevHigh2 : 0;
        var prevLow = _index >= 2 ? _prevLow2 : 0;
        var stdDev = _stdDev.Next(_atrValue, isFinal);

        var prevTrndDn = _hasPrev ? _prevTrndDn : 0;
        var trndDn = adm < ema && prevAdm > prevEma ? prevHigh
            : value < prevValue ? value + (stdDev * _atrMult) : prevTrndDn;
        var prevTrndUp = _hasPrev ? _prevTrndUp : 0;
        var trndUp = adm > ema && prevAdm < prevEma ? prevLow
            : value > prevValue ? value - (stdDev * _atrMult) : prevTrndUp;
        var prevTrndr = _hasPrev ? _prevTrndr : 0;
        var trndr = adm < ema ? trndDn : adm > ema ? trndUp : prevTrndr;

        if (isFinal)
        {
            _prevValue = value;
            _prevEma = ema;
            _prevAdm = adm;
            _prevTrndDn = trndDn;
            _prevTrndUp = trndUp;
            _prevTrndr = trndr;
            _prevHigh2 = _prevHigh1;
            _prevHigh1 = bar.High;
            _prevLow2 = _prevLow1;
            _prevLow1 = bar.Low;
            _index++;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "TrendUp", trndUp },
                { "TrendDn", trndDn },
                { "Trender", trndr }
            };
        }

        return new StreamingIndicatorStateResult(trndr, outputs);
    }

    public void Dispose()
    {
        _ema.Dispose();
        _atr.Dispose();
        _stdDev.Dispose();
        _adSmoother.Dispose();
        _preciseAd?.Dispose();
    }
}

[PrimaryOutput("Tei")]
public sealed class TrendExhaustionIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingWindowMax _highWindow;
    private readonly IMovingAverageSmoother _signalSmoother;
    private readonly StreamingInputResolver _input;
    private readonly double _sc;
    private double _aCountSum;
    private double _hCountSum;
    private double _prevTei;
    private double _prevValue;
    private double _prevHighest;
    private bool _hasPrev;

    public TrendExhaustionIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        var resolved = Math.Max(1, length);
        _highWindow = new RollingWindowMax(Math.Max(2, resolved));
        _signalSmoother = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.Close, null);
        _sc = (double)2 / (resolved + 1);
    }

    public IndicatorName Name => IndicatorName.TrendExhaustionIndicator;

    public void Reset()
    {
        _highWindow.Reset();
        _signalSmoother.Reset();
        _aCountSum = 0;
        _hCountSum = 0;
        _prevTei = 0;
        _prevValue = 0;
        _prevHighest = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue = _hasPrev ? _prevValue : 0;
        var prevHighest = _hasPrev ? _prevHighest : 0;
        var a = value > prevValue ? 1 : 0;
        var h = bar.High > prevHighest ? 1 : 0;
        var aCountSum = _aCountSum + a;
        var hCountSum = _hCountSum + h;
        var haRatio = aCountSum != 0 ? hCountSum / aCountSum : 0;
        var prevTei = _hasPrev ? _prevTei : 0;
        var tei = prevTei + (_sc * (haRatio - prevTei));
        var signal = _signalSmoother.Next(tei, isFinal);

        if (isFinal)
        {
            _aCountSum = aCountSum;
            _hCountSum = hCountSum;
            _prevTei = tei;
            _prevValue = value;
            _prevHighest = _highWindow.Add(bar.High, out _);
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(2)
            {
                { "Tei", tei },
                { "Signal", signal }
            };
        }

        return new StreamingIndicatorStateResult(tei, outputs);
    }

    public void Dispose()
    {
        _highWindow.Dispose();
        _signalSmoother.Dispose();
    }
}
