#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using System.Collections.Generic;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Udv")]
public sealed class UpsideDownsideVolumeState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeBalanceWindow _window;
    public UpsideDownsideVolumeState(int length = 50) => _window = new VolumeBalanceWindow(length, VolumeBalanceKind.UpsideDownside);
    public IndicatorName Name => IndicatorName.UpsideDownsideVolume;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Udv", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Upr")]
public sealed class UpsidePotentialRatioState : IStreamingIndicatorState, IDisposable
{
    private readonly TargetReturnWindow _window;
    public UpsidePotentialRatioState(int length = 30, double bmk = .05) => _window = new TargetReturnWindow(length, bmk, true);
    public IndicatorName Name => IndicatorName.UpsidePotentialRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Upr", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("vClose")]
public sealed class ValueChartIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly ValueChartWindow _window;
    private bool _custom;
    public ValueChartIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.ValueChartIndicator;
    void ICustomInputConsumer.ReadCloseAsInput() => _custom = true;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var values = _window.Next(bar, _custom, isFinal);
        return new StreamingIndicatorStateResult(values[0], includeOutputs ? new Dictionary<string, double>
        { { "vClose", values[0] }, { "vOpen", values[1] }, { "vHigh", values[2] }, { "vLow", values[3] } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vabcd")]
public sealed class VanillaABCDPatternState : IStreamingIndicatorState
{
    private readonly StreamingInputResolver _input;
    private double _prevValue1;
    private double _prevValue2;
    private double _prevValue3;
    private double _prevOs;
    private int _index;

    public VanillaABCDPatternState()
    {
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VanillaABCDPattern;

    public void Reset()
    {
        _prevValue1 = 0;
        _prevValue2 = 0;
        _prevValue3 = 0;
        _prevOs = 0;
        _index = 0;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var prevValue1 = _index >= 1 ? _prevValue1 : 0;
        var prevValue2 = _index >= 2 ? _prevValue2 : 0;
        var prevValue3 = _index >= 3 ? _prevValue3 : 0;

        var up = prevValue3 > prevValue2 && prevValue1 > prevValue2 && value < prevValue2 ? 1d : 0d;
        var dn = prevValue3 < prevValue2 && prevValue1 < prevValue2 && value > prevValue2 ? 1d : 0d;

        var prevOs = _index >= 1 ? _prevOs : 0;
        var os = up == 1d ? 1d : dn == 1d ? 0d : prevOs;
        var dos = os - prevOs;

        if (isFinal)
        {
            _prevValue3 = _prevValue2;
            _prevValue2 = _prevValue1;
            _prevValue1 = value;
            _prevOs = os;
            _index++;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vabcd", dos }
            };
        }

        return new StreamingIndicatorStateResult(dos, outputs);
    }
}

[PrimaryOutput("Vo")]
public sealed class VaradiOscillatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VaradiWindow _window;
    public VaradiOscillatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.VaradiOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var point = _window.Next(bar.Close, bar.High, bar.Low, isFinal);
        return new StreamingIndicatorStateResult(point.Value,
            includeOutputs ? new Dictionary<string, double> { { "Vo", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vama")]
public sealed class VariableAdaptiveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VariableAdaptiveWindow _window;
    public VariableAdaptiveMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
        => _window = new(maType, length);
    public IndicatorName Name => IndicatorName.VariableAdaptiveMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var point = _window.Next(bar, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Vama", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vidya")]
public sealed class VariableIndexDynamicAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly ChandeMomentumOscillatorState _cmo;
    private readonly StreamingInputResolver _input;
    private readonly double _alpha;
    private double _prevVidya;
    private bool _hasPrev;

    public VariableIndexDynamicAverageState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        _alpha = 2d / (Math.Max(1, length) + 1d);
        _cmo = new ChandeMomentumOscillatorState(maType, Math.Max(1, length), 3);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VariableIndexDynamicAverage;

    public void Reset()
    {
        _cmo.Reset();
        _prevVidya = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var cmo = _cmo.Update(bar, isFinal, includeOutputs: false).Value;
        var currentCmo = Math.Abs(cmo / 100);
        // Seeded at the first price, as the batch is: alpha * |CMO| is legitimately zero on a series with
        // no momentum, and a recursion multiplied by zero never leaves its seed.
        var prevVidya = _hasPrev ? _prevVidya : value;
        var vidya = VidyaBlend.Compute(prevVidya, value, _alpha * currentCmo);

        if (isFinal)
        {
            _prevVidya = vidya;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vidya", vidya }
            };
        }

        return new StreamingIndicatorStateResult(vidya, outputs);
    }

    public void Dispose()
    {
        _cmo.Dispose();
    }
}

[PrimaryOutput("Vlma")]
public sealed class VariableLengthMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VariableLengthWindow _window;
    public VariableLengthMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int minLength = 5, int maxLength = 50) => _window = new(maType, minLength, maxLength);
    public IndicatorName Name => IndicatorName.VariableLengthMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal);
        return new StreamingIndicatorStateResult(point.Value, includeOutputs ? new Dictionary<string, double> { { "Length", point.Length }, { "Vlma", point.Value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vma")]
public sealed class VariableMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VariableMovingAverageEngine _engine;
    private readonly StreamingInputResolver _input;

    public VariableMovingAverageState(int length = 6)
    {
        _engine = new VariableMovingAverageEngine(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VariableMovingAverage;

    public void Reset()
    {
        _engine.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var vma = _engine.Next(value, isFinal);

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vma", vma }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}

[PrimaryOutput("MiddleBand")]
public sealed class VariableMovingAverageBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly VariableBandWindow _window;
    private readonly StreamingInputResolver _input=new(InputName.Close,null);
    public VariableMovingAverageBandsState(MovingAvgType maType=MovingAvgType.VariableMovingAverage,int length=6,double mult=1.5)
        => _window=new(maType,length,mult);
    public IndicatorName Name => IndicatorName.VariableMovingAverageBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        var value=_input.GetValue(bar); var point=_window.Next(value,bar.High,bar.Low,isFinal);
        return new StreamingIndicatorStateResult(point.Middle,includeOutputs ? new Dictionary<string,double>
            { ["UpperBand"]=point.Upper,["MiddleBand"]=point.Middle,["LowerBand"]=point.Lower } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vhma")]
public sealed class VerticalHorizontalMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VerticalHorizontalAverageWindow _window;
    public VerticalHorizontalMovingAverageState(int length = 50) => _window = new(length);
    public IndicatorName Name => IndicatorName.VerticalHorizontalMovingAverage;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Close, isFinal);
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Vhma", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vhaco")]
public sealed class VervoortHeikenAshiCandlestickOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VervoortCandleWindow? _wide;
    private readonly IMovingAverageSmoother _haMa1;
    private readonly IMovingAverageSmoother _haMa2;
    private readonly IMovingAverageSmoother _medianMa1;
    private readonly IMovingAverageSmoother _medianMa2;
    private StreamingInputResolver _input;
    private double _prevInput;
    private double _prevHao;
    private double _prevHac;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _prevDnKeeping;
    private bool _prevDnKeepAll;
    private bool _prevDnTrend;
    private bool _prevUpKeeping;
    private bool _prevUpKeepAll;
    private bool _prevUpTrend;
    private double _prevHaco;
    private bool _hasPrev;

    public VervoortHeikenAshiCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.ZeroLagTripleExponentialMovingAverage, int length = 34)
    {
        if (VervoortCandleWindow.Supports(maType)) _wide=new(false,maType,length);
        var resolved = Math.Max(1, length);
        _haMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _haMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _medianMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.VervoortHeikenAshiCandlestickOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _wide?.Reset();
        _haMa1.Reset();
        _haMa2.Reset();
        _medianMa1.Reset();
        _medianMa2.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _prevHac = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevDnKeeping = false;
        _prevDnKeepAll = false;
        _prevDnTrend = false;
        _prevUpKeeping = false;
        _prevUpKeepAll = false;
        _prevUpTrend = false;
        _prevHaco = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var inputValue = _input.GetValue(bar);
        if (_wide is not null)
        {var point=_wide.Next(inputValue,bar.High,bar.Low,bar.Open,bar.Close,isFinal);return new(point.Value,includeOutputs?new Dictionary<string,double>{{"Vhaco",point.Value}}:null);}
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;
        var medianPrice = (bar.High + bar.Low) / 2;

        var tma1 = _haMa1.Next(hac, isFinal);
        var tma2 = _haMa2.Next(tma1, isFinal);
        var tma12 = _medianMa1.Next(medianPrice, isFinal);
        var tma22 = _medianMa2.Next(tma12, isFinal);
        var zlHa = tma1 + (tma1 - tma2);
        var zlCl = tma12 + (tma12 - tma22);
        var zlDiff = zlCl - zlHa;

        var prevHac = _hasPrev ? _prevHac : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var dnKeep1 = hac < hao && prevHac < prevHao;
        var dnKeep2 = zlDiff < 0;
        var dnKeep3 = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * 0.35 && bar.Low <= prevHigh;
        var dnKeeping = dnKeep1 || dnKeep2;
        var dnKeepAll = (dnKeeping || _prevDnKeeping) && ((bar.Close < bar.Open) || (bar.Close < prevClose));
        var dnTrend = dnKeepAll || (_prevDnKeepAll && dnKeep3);

        var upKeep1 = hac >= hao && prevHac >= prevHao;
        var upKeep2 = zlDiff >= 0;
        var upKeep3 = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * 0.35 && bar.High >= prevLow;
        var upKeeping = upKeep1 || upKeep2;
        var upKeepAll = (upKeeping || _prevUpKeeping) && ((bar.Close >= bar.Open) || (bar.Close >= prevClose));
        var upTrend = upKeepAll || (_prevUpKeepAll && upKeep3);

        var upw = dnTrend == false && _prevDnTrend && upTrend;
        var dnw = upTrend == false && _prevUpTrend && dnTrend;
        var haco = upw ? 1 : dnw ? -1 : _prevHaco;

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _prevHac = hac;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevDnKeeping = dnKeeping;
            _prevDnKeepAll = dnKeepAll;
            _prevDnTrend = dnTrend;
            _prevUpKeeping = upKeeping;
            _prevUpKeepAll = upKeepAll;
            _prevUpTrend = upTrend;
            _prevHaco = haco;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vhaco", haco }
            };
        }

        return new StreamingIndicatorStateResult(haco, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _haMa1.Dispose();
        _haMa2.Dispose();
        _medianMa1.Dispose();
        _medianMa2.Dispose();
    }
}

[PrimaryOutput("Vhaltco")]
public sealed class VervoortHeikenAshiLongTermCandlestickOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VervoortCandleWindow? _wide;
    private readonly IMovingAverageSmoother _tacMa1;
    private readonly IMovingAverageSmoother _tacMa2;
    private readonly IMovingAverageSmoother _thlMa1;
    private readonly IMovingAverageSmoother _thlMa2;
    private StreamingInputResolver _input;
    private readonly double _factor;
    private double _prevInput;
    private double _prevHao;
    private double _prevHac;
    private double _prevHigh;
    private double _prevLow;
    private double _prevClose;
    private bool _prevKeepN1;
    private bool _prevKeepAll1;
    private bool _prevUtr;
    private bool _prevKeepN2;
    private bool _prevKeepAll2;
    private bool _prevDtr;
    private double _prevHaco;
    private bool _hasPrev;

    public VervoortHeikenAshiLongTermCandlestickOscillatorState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length = 55, double factor = 1.1)
    {
        if (VervoortCandleWindow.Supports(maType)) _wide=new(true,maType,length, factor);
        var resolved = Math.Max(1, length);
        _tacMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _tacMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa1 = MovingAverageSmootherFactory.Create(maType, resolved);
        _thlMa2 = MovingAverageSmootherFactory.Create(maType, resolved);
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
        _factor = factor;
    }

    public IndicatorName Name => IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;

    void ICustomInputConsumer.ReadCloseAsInput() =>
        _input = new StreamingInputResolver(InputName.Close, null);

    public void Reset()
    {
        _wide?.Reset();
        _tacMa1.Reset();
        _tacMa2.Reset();
        _thlMa1.Reset();
        _thlMa2.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _prevHac = 0;
        _prevHigh = 0;
        _prevLow = 0;
        _prevClose = 0;
        _prevKeepN1 = false;
        _prevKeepAll1 = false;
        _prevUtr = false;
        _prevKeepN2 = false;
        _prevKeepAll2 = false;
        _prevDtr = false;
        _prevHaco = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var inputValue = _input.GetValue(bar);
        if (_wide is not null)
        {var point=_wide.Next(inputValue,bar.High,bar.Low,bar.Open,bar.Close,isFinal);return new(point.Value,includeOutputs?new Dictionary<string,double>{{"Vhaltco",point.Value}}:null);}
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;
        var medianPrice = (bar.High + bar.Low) / 2;

        var tac = _tacMa1.Next(hac, isFinal);
        var tacTema = _tacMa2.Next(tac, isFinal);
        var thl2 = _thlMa1.Next(medianPrice, isFinal);
        var thl2Tema = _thlMa2.Next(thl2, isFinal);
        var hacSmooth = (2 * tac) - tacTema;
        var hl2Smooth = (2 * thl2) - thl2Tema;

        var prevHac = _hasPrev ? _prevHac : 0;
        var prevHigh = _hasPrev ? _prevHigh : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var prevClose = _hasPrev ? _prevClose : 0;

        var shortCandle = Math.Abs(bar.Close - bar.Open) < (bar.High - bar.Low) * _factor;
        var keepN1 = ((hac >= hao) && (prevHac >= prevHao)) || bar.Close >= hac ||
            bar.High > prevHigh || bar.Low > prevLow || hl2Smooth >= hacSmooth;
        var keepAll1 = keepN1 || (_prevKeepN1 && (bar.Close >= bar.Open || bar.Close >= prevClose));
        var keep13 = shortCandle && bar.High >= prevLow;
        var utr = keepAll1 || (_prevKeepAll1 && keep13);

        var keepN2 = (hac < hao && prevHac < prevHao) || hl2Smooth < hacSmooth;
        var keepAll2 = keepN2 || (_prevKeepN2 && (bar.Close < bar.Open || bar.Close < prevClose));
        var keep23 = shortCandle && bar.Low <= prevHigh;
        var dtr = (keepAll2 || _prevKeepAll2) && keep23;

        var upw = dtr == false && _prevDtr && utr;
        var dnw = utr == false && _prevUtr && dtr;
        var haco = upw ? 1 : dnw ? -1 : _prevHaco;

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _prevHac = hac;
            _prevHigh = bar.High;
            _prevLow = bar.Low;
            _prevClose = bar.Close;
            _prevKeepN1 = keepN1;
            _prevKeepAll1 = keepAll1;
            _prevUtr = utr;
            _prevKeepN2 = keepN2;
            _prevKeepAll2 = keepAll2;
            _prevDtr = dtr;
            _prevHaco = haco;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vhaltco", haco }
            };
        }

        return new StreamingIndicatorStateResult(haco, outputs);
    }

    public void Dispose()
    {
        _wide?.Dispose();
        _tacMa1.Dispose();
        _tacMa2.Dispose();
        _thlMa1.Dispose();
        _thlMa2.Dispose();
    }
}

[PrimaryOutput("PercentB")]
public sealed class VervoortModifiedBollingerBandIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VervoortModifiedWindow? _safe;
    private bool _selected;
    private readonly IMovingAverageSmoother _hacMa1 = null!;
    private readonly IMovingAverageSmoother _hacMa2 = null!;
    private readonly IMovingAverageSmoother _zlhaMa = null!;
    private readonly IMovingAverageSmoother _wma = null!;
    // The deviation of each window about its own mean, matching the batch calculation; see #190. One measures
    // the smoothed Heikin-Ashi series over length1 and the other the percent-b series over length2 - two
    // different series over two different windows, which must not be crossed in either respect.
    private readonly RollingStandardDeviation _zlhaStdDev = null!;
    private readonly RollingStandardDeviation _percbStdDev = null!;
    private StreamingInputResolver _input;
    private readonly double _stdDevMult;
    private readonly VervoortModifiedBandPosition _precise = null!;
    private double _prevInput;
    private double _prevHao;
    private bool _hasPrev;

    public VervoortModifiedBollingerBandIndicatorState(MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, int length1 = 18, int length2 = 200,
        int smoothLength = 8, double stdDevMult = 1.6)
    {
        _safe=VervoortModifiedWindow.Supports(maType)?new(maType,length1,length2,smoothLength,stdDevMult):null;
        if(_safe is not null)return;
        _stdDevMult = stdDevMult;
        _precise = new VervoortModifiedBandPosition(maType, length1, smoothLength);
        _hacMa1 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _hacMa2 = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _zlhaMa = MovingAverageSmootherFactory.Create(maType, Math.Max(1, smoothLength));
        _wma = MovingAverageSmootherFactory.Create(MovingAvgType.WeightedMovingAverage, Math.Max(1, length1));
        // No moving-average type, and no selectors: each series is passed to Next directly.
        _zlhaStdDev = new RollingStandardDeviation(Math.Max(1, length1));
        _percbStdDev = new RollingStandardDeviation(Math.Max(1, length2));
        _input = new StreamingInputResolver(InputName.FullTypicalPrice, null);
    }

    public IndicatorName Name => IndicatorName.VervoortModifiedBollingerBandIndicator;

    void ICustomInputConsumer.ReadCloseAsInput()
    { _selected=true;_input = new StreamingInputResolver(InputName.Close, null); }

    public void Reset()
    {
        if(_safe is not null){_safe.Reset();return;}
        _precise.Reset();
        _hacMa1.Reset();
        _hacMa2.Reset();
        _zlhaMa.Reset();
        _wma.Reset();
        _zlhaStdDev.Reset();
        _percbStdDev.Reset();
        _prevInput = 0;
        _prevHao = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        if(_safe is not null)
        {
            StreamingInputValidation.Validate(bar);var mean=new ExactMeanAccumulator();mean.Add(bar.Open);mean.Add(bar.High);mean.Add(bar.Low);mean.Add(bar.Close);
            var point=_safe.Next(_selected?bar.Close:mean.Mean(4),bar.High,bar.Low,isFinal);
            return new(point.Percent,includeOutputs?new Dictionary<string,double>{{"UpperBand",point.Upper},{"MiddleBand",50},{"LowerBand",point.Lower},{"PercentB",point.Percent}}:null);
        }
        var inputValue = _input.GetValue(bar);
        var prevInput = _hasPrev ? _prevInput : 0;
        var prevHao = _hasPrev ? _prevHao : 0;
        var hao = (prevInput + prevHao) / 2;
        var hac = (inputValue + hao + Math.Max(bar.High, hao) + Math.Min(bar.Low, hao)) / 4;

        var tma1 = _hacMa1.Next(hac, isFinal);
        var tma2 = _hacMa2.Next(tma1, isFinal);
        var zlha = tma1 + (tma1 - tma2);
        var zlhaTema = _zlhaMa.Next(zlha, isFinal);

        // Fed the smoothed Heikin-Ashi series, which is the series this measures.
        var zlhaStdDev = _zlhaStdDev.Next(zlhaTema, isFinal);
        var wma = _wma.Next(zlhaTema, isFinal);
        var percb = zlhaStdDev != 0
            ? (zlhaTema + (2 * zlhaStdDev) - wma) / (4 * zlhaStdDev) * 100
            : 0;
        percb = _precise.Next(inputValue, bar.High, bar.Low, isFinal);
        // Fed the percent-b series, over its own window rather than the one above.
        var percbStdDev = _percbStdDev.Next(percb, isFinal);
        var upper = 50 + (_stdDevMult * percbStdDev);
        var lower = 50 - (_stdDevMult * percbStdDev);

        if (isFinal)
        {
            _prevInput = inputValue;
            _prevHao = hao;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            // The bands are 50 plus and minus a multiple of the deviation, so their mean is the centre;
            // %b is the series they are drawn around. See the batch calculation.
            outputs = new Dictionary<string, double>(4)
            {
                { "UpperBand", upper },
                { "MiddleBand", (upper + lower) / 2 },
                { "LowerBand", lower },
                { "PercentB", percb }
            };
        }

        return new StreamingIndicatorStateResult(percb, outputs);
    }

    public void Dispose()
    {
        if(_safe is not null){_safe.Dispose();return;}
        _precise.Dispose();
        _hacMa1.Dispose();
        _hacMa2.Dispose();
        _zlhaMa.Dispose();
        _wma.Dispose();
        _zlhaStdDev.Dispose();
        _percbStdDev.Dispose();
    }
}

[PrimaryOutput("Vso")]
public sealed class VervoortSmoothedOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly VervoortSmoothedWindow _window;
    private bool _selected;
    public VervoortSmoothedOscillatorState(int length1=18,int length2=30,int length3=2,int smoothLength=3,double stdDevMult=2)
        =>_window=new(length1,length2,length3,smoothLength,stdDevMult);
    public IndicatorName Name=>IndicatorName.VervoortSmoothedOscillator;
    void ICustomInputConsumer.ReadCloseAsInput()=>_selected=true;
    public void Reset()=>_window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar,bool isFinal,bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);var typical=new ExactMeanAccumulator();typical.Add(bar.High);typical.Add(bar.Low);typical.Add(bar.Close);var point=_window.Next(bar.Close,_selected?bar.Close:typical.Mean(3),bar.High,bar.Low,isFinal);
        return new(point.Line,includeOutputs?new Dictionary<string,double>{{"Vso",point.Line},{"Sk",point.Stochastic}}:null);
    }
    public void Dispose()=>_window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class VervoortVolatilityBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly VervoortVolatilityWindow? _wide;
    private readonly double _devMult;
    private readonly double _lowBandMult;
    private readonly IMovingAverageSmoother _medianAvg = null!;
    private readonly IMovingAverageSmoother _medianAvgEma = null!;
    private readonly IMovingAverageSmoother _devHighMa = null!;
    private readonly RollingWindowSum _medianAvgSum = null!;
    private readonly RollingWindowSum _typicalSum = null!;
    private readonly StreamingInputResolver _input;
    private double _prevValue;
    private double _prevLow;
    private bool _hasPrev;

    public VervoortVolatilityBandsState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 8,
        int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9)
    {
        if (StrengthWindow.Supports(maType)) { _wide=new(maType,length1,length2,devMult,lowBandMult); return; }
        var resolvedLength1 = Math.Max(1, length1);
        var resolvedLength2 = Math.Max(1, length2);
        _devMult = devMult;
        _lowBandMult = lowBandMult;
        _medianAvg = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgEma = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _devHighMa = MovingAverageSmootherFactory.Create(maType, resolvedLength1);
        _medianAvgSum = new RollingWindowSum(resolvedLength1);
        _typicalSum = new RollingWindowSum(resolvedLength2);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VervoortVolatilityBands;

    public void Reset()
    {
        if(_wide is not null){_wide.Reset();return;}
        _medianAvg.Reset();
        _medianAvgEma.Reset();
        _devHighMa.Reset();
        _medianAvgSum.Reset();
        _typicalSum.Reset();
        _prevValue = 0;
        _prevLow = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if(_wide is not null){var point=_wide.Next(bar.Close,bar.Low,isFinal);return new(point.Middle,includeOutputs?new Dictionary<string,double>{{"UpperBand",point.Upper},{"MiddleBand",point.Middle},{"LowerBand",point.Lower}}:null);}
        var value = _input.GetValue(bar);
        var medianAvg = _medianAvg.Next(value, isFinal);
        var medianAvgEma = _medianAvgEma.Next(medianAvg, isFinal);
        int medianCount;
        var medianAvgSum = isFinal ? _medianAvgSum.Add(medianAvg, out medianCount) : _medianAvgSum.Preview(medianAvg, out medianCount);
        var medianAvgSma = medianCount > 0 ? medianAvgSum / medianCount : 0;

        var prevValue = _hasPrev ? _prevValue : 0;
        var prevLow = _hasPrev ? _prevLow : 0;
        var typical = value >= prevValue ? value - prevLow : prevValue - bar.Low;
        int typicalCount;
        var typicalSum = isFinal ? _typicalSum.Add(typical, out typicalCount) : _typicalSum.Preview(typical, out typicalCount);
        var typicalSma = typicalCount > 0 ? typicalSum / typicalCount : 0;
        var deviation = _devMult * typicalSma;
        var devHigh = _devHighMa.Next(deviation, isFinal);
        var devLow = _lowBandMult * devHigh;

        var upper = medianAvgEma + devHigh;
        var lower = medianAvgEma - devLow;

        if (isFinal)
        {
            _prevValue = value;
            _prevLow = bar.Low;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(3)
            {
                { "UpperBand", upper },
                { "MiddleBand", medianAvgSma },
                { "LowerBand", lower }
            };
        }

        return new StreamingIndicatorStateResult(medianAvgSma, outputs);
    }

    public void Dispose()
    {
        if(_wide is not null){_wide.Dispose();return;}
        _medianAvg.Dispose();
        _medianAvgEma.Dispose();
        _devHighMa.Dispose();
        _medianAvgSum.Dispose();
        _typicalSum.Dispose();
    }
}

[PrimaryOutput("Vix")]
public sealed class VixTradingSystemState : IStreamingIndicatorState, IDisposable
{
    private readonly IMovingAverageSmoother _sma;
    private readonly StreamingInputResolver _input;
    private double _prevCount;
    private bool _hasPrev;

    public VixTradingSystemState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 50,
        double maxCount = 11, double minCount = -11)
    {
        _sma = MovingAverageSmootherFactory.Create(maType, Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
        _ = maxCount;
        _ = minCount;
    }

    public IndicatorName Name => IndicatorName.VixTradingSystem;

    public void Reset()
    {
        _sma.Reset();
        _prevCount = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var vixts = _sma.Next(value, isFinal);
        var prevCount = _hasPrev ? _prevCount : 0;
        var count = value > vixts && prevCount >= 0 ? prevCount + 1
            : value <= vixts && prevCount <= 0 ? prevCount - 1 : prevCount;

        if (isFinal)
        {
            _prevCount = count;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vix", count }
            };
        }

        return new StreamingIndicatorStateResult(count, outputs);
    }

    public void Dispose()
    {
        _sma.Dispose();
    }
}

[PrimaryOutput("Vida1")]
public sealed class VolatilityIndexDynamicAverageIndicatorState : IStreamingIndicatorState, IDisposable
{
    private readonly VolatilityIndexWindow _window;
    public VolatilityIndexDynamicAverageIndicatorState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, double alpha1 = 0.2, double alpha2 = 0.04) => _window = new(maType, length, alpha1, alpha2);
    public IndicatorName Name => IndicatorName.VolatilityIndexDynamicAverageIndicator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    { StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, isFinal); return new(point.First, includeOutputs ? new Dictionary<string, double> { { "Vida1", point.First }, { "Vida2", point.Second } } : null); }
    public void Dispose() => _window.Dispose();

}

[PrimaryOutput("Vma")]
public sealed class VolatilityMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VolatilityAverageWindow? _exact;
    private readonly int _length;
    private readonly int _lbLength;
    private readonly IMovingAverageSmoother _sma = null!;

    // The deviation of the window about its own mean, not the mean squared residual from the moving
    // average line; see the batch calculation and #190. The band here is sma +/- dev and k divides by
    // its width, so this has to be the one a band at k sigma is defined against.
    private readonly RollingStandardDeviation _stdDev = null!;
    private readonly IMovingAverageSmoother _kSmoother = null!;
    private readonly IMovingAverageSmoother _vmaSmoother = null!;
    private readonly PooledRingBuffer<double> _values = null!;
    private readonly StreamingInputResolver _input = default;

    public VolatilityMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20,
        int lbLength = 10, int smoothLength = 3)
    {
        _length = Math.Max(1, length);
        _lbLength = Math.Max(1, lbLength);
        var resolvedSmooth = Math.Max(1, smoothLength);
        if (StrengthWindow.Supports(maType)) { _exact = new(maType, _length, _lbLength, resolvedSmooth); return; }
        _sma = MovingAverageSmootherFactory.Create(maType, _lbLength);
        _stdDev = new RollingStandardDeviation(_lbLength);
        _kSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _vmaSmoother = MovingAverageSmootherFactory.Create(maType, resolvedSmooth);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolatilityMovingAverage;

    public void Reset()
    {
        if (_exact is not null) { _exact.Reset(); return; }
        _sma.Reset();
        _stdDev.Reset();
        _kSmoother.Reset();
        _vmaSmoother.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var point = _exact.Next(bar.Close, isFinal);
            return new(point.Value, includeOutputs ? new Dictionary<string, double> { ["Vma"] = point.Value } : null);
        }
        var value = _input.GetValue(bar);
        var sma = _sma.Next(value, isFinal);
        var dev = _stdDev.Next(value, isFinal);
        var upper = sma + dev;
        var lower = sma - dev;
        var k = upper - lower != 0 ? (value - sma) / (upper - lower) * 100 * 2 : 0;
        var kMa = _kSmoother.Next(k, isFinal);
        var kNorm = Math.Min(Math.Max(kMa, -100), 100);
        var kAbs = Math.Round(Math.Abs(kNorm) / _lbLength);
        var kRescaled = CalculationsHelper.RescaleValue(kAbs, 10, 0, _length, 0, true);
        var vLength = (int)Math.Round(Math.Max(kRescaled, 1));

        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= vLength - 1; j++)
        {
            var weight = vLength - j;
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var vma1 = weightedSum != 0 ? sum / weightedSum : 0;
        var vma = _vmaSmoother.Next(vma1, isFinal);

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vma", vma }
            };
        }

        return new StreamingIndicatorStateResult(vma, outputs);
    }

    public void Dispose()
    {
        if (_exact is not null) { _exact.Dispose(); return; }
        _sma.Dispose();
        _stdDev.Dispose();
        _kSmoother.Dispose();
        _vmaSmoother.Dispose();
        _values.Dispose();
    }
}

[PrimaryOutput("Vr")]
public sealed class VolatilityRatioState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly VolatilityRatioWindow _window; private readonly StreamingInputResolver _input;
    public VolatilityRatioState(MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14, double breakoutLevel = .5)
    { _window = new(length); _input = new StreamingInputResolver(InputName.Close, null); }
    public IndicatorName Name => IndicatorName.VolatilityRatio;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var price = _input.GetValue(bar); var value = _window.Next(bar.High, bar.Low, price, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Vr", value } } : null);
    }
    public void Dispose() { }
}

[PrimaryOutput("Vwma")]
public sealed class VolatilityWaveMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VolatilityWaveWindow? _exact;
    private readonly int _length;
    private readonly double _kf;

    // The deviation of the window about its own mean, matching the batch calculation; see #190.
    private readonly RollingStandardDeviation _stdDev = null!;
    private readonly IMovingAverageSmoother _wmap1 = null!;
    private readonly IMovingAverageSmoother _wmap2 = null!;
    private readonly PooledRingBuffer<double> _values = null!;
    private readonly StreamingInputResolver _input = default;

    public VolatilityWaveMovingAverageState(MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 20,
        double kf = 2.5)
    {
        _length = Math.Max(1, length);
        StreamingInputValidation.Finite(kf, nameof(kf));
        _kf = kf;
        if (StrengthWindow.Supports(maType)) { _exact = new(maType, _length, kf); return; }
        var s = MathHelper.MinOrMax((int)Math.Ceiling(MathHelper.Sqrt(_length)));
        // No moving-average type: a windowed deviation is taken about the window's own mean. maType still
        // selects the averages that smooth the weighted mean, below.
        _stdDev = new RollingStandardDeviation(_length);
        _wmap1 = MovingAverageSmootherFactory.Create(maType, s);
        _wmap2 = MovingAverageSmootherFactory.Create(maType, s);
        _values = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.VolatilityWaveMovingAverage;

    public void Reset()
    {
        if (_exact is not null) { _exact.Reset(); return; }
        _stdDev.Reset();
        _wmap1.Reset();
        _wmap2.Reset();
        _values.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        if (_exact is not null)
        {
            var point = _exact.Next(bar.Close, isFinal);
            return new(point, includeOutputs ? new Dictionary<string, double> { ["Vwma"] = point } : null);
        }
        var value = _input.GetValue(bar);
        // Fed the resolved input rather than the bar, matching the batch calculation.
        var stdDev = _stdDev.Next(value, isFinal);
        var sdPct = value != 0 ? stdDev / value * 100 : 0;
        var p = sdPct >= 0 ? MathHelper.MinOrMax(MathHelper.Sqrt(sdPct) * _kf, 4, 1) : 1;

        double sum = 0;
        double weightedSum = 0;
        for (var j = 0; j <= _length - 1; j++)
        {
            var weight = MathHelper.Pow(_length - j, p);
            var prevValue = EhlersStreamingWindow.GetOffsetValue(_values, value, j);
            sum += prevValue * weight;
            weightedSum += weight;
        }

        var pma = weightedSum != 0 ? sum / weightedSum : 0;
        var wmap1 = _wmap1.Next(pma, isFinal);
        var wmap2 = _wmap2.Next(wmap1, isFinal);
        var zlmap = (2 * wmap1) - wmap2;

        if (isFinal)
        {
            _values.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1)
            {
                { "Vwma", zlmap }
            };
        }

        return new StreamingIndicatorStateResult(zlmap, outputs);
    }

    public void Dispose()
    {
        if (_exact is not null) { _exact.Dispose(); return; }
        _stdDev.Dispose();
        _wmap1.Dispose();
        _wmap2.Dispose();
        _values.Dispose();
    }
}

[PrimaryOutput("Vao")]
public sealed class VolumeAccumulationOscillatorState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly VolumeBalanceWindow _window;
    public VolumeAccumulationOscillatorState(int length = 14) => _window = new VolumeBalanceWindow(length, VolumeBalanceKind.Accumulation);
    public IndicatorName Name => IndicatorName.VolumeAccumulationOscillator;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Vao", value } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vapc")]
public sealed class VolumeAccumulationPercentState : IStreamingIndicatorState, IDisposable, ICustomInputRangePolicy
{
    bool ICustomInputRangePolicy.PreserveOriginalRange => true;

    private readonly MoneyFlowPercentWindow _window;
    public VolumeAccumulationPercentState(int length = 10)
        => _window = new MoneyFlowPercentWindow(length);
    public IndicatorName Name => IndicatorName.VolumeAccumulationPercent;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar);
        var value = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs ? new Dictionary<string, double> { { "Vapc", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("MiddleBand")]
public sealed class VolumeAdaptiveBandsState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeAdaptiveBandWindow _window;
    public VolumeAdaptiveBandsState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 100) { _window = new(maType, length); }
    public IndicatorName Name => IndicatorName.VolumeAdaptiveBands;
    public void Reset() => _window.Reset();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var point = _window.Next(bar.Close, bar.Volume, isFinal);
        return new(point.Middle, includeOutputs ? new Dictionary<string, double> { { "UpperBand", point.Upper }, { "MiddleBand", point.Middle }, { "LowerBand", point.Lower } } : null);
    }
    public void Dispose() => _window.Dispose();
}

[PrimaryOutput("Vama")]
public sealed class VolumeAdjustedMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly VolumeAdjustedWindow _window;
    public VolumeAdjustedMovingAverageState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14, double factor = .67) => _window = new(maType, length, factor);
    public IndicatorName Name => IndicatorName.VolumeAdjustedMovingAverage;
    public void Reset() => _window.Reset();
    public void Dispose() => _window.Dispose();
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        StreamingInputValidation.Validate(bar); var value = _window.Next(bar.Close, bar.Volume, isFinal);
        return new StreamingIndicatorStateResult(value, includeOutputs ? new Dictionary<string, double> { { "Vama", value } } : null);
    }
}

internal sealed class VariableMovingAverageEngine : IDisposable
{
    private readonly ExactVariableMovingAverageEngine _engine;
    public VariableMovingAverageEngine(int length) => _engine=new(length);
    public double Next(double value,bool isFinal) => _engine.Next(value,isFinal);
    public void Reset() => _engine.Reset();
    public void Dispose() => _engine.Dispose();
}
