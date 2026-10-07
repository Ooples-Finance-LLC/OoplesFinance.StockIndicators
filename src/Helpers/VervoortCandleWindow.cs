using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VervoortCandleWindow : IDisposable
{
    private readonly Smooth _haFirst,_haSecond,_midFirst,_midSecond;
    private readonly bool _longTerm;
    private readonly BigInteger _factor;
    private BigInteger _input,_open,_close;
    private double _high,_low,_lastClose,_line;
    private bool _upSeed,_downSeed,_upKeep,_downKeep,_upTrend,_downTrend;
    internal static bool Supports(MovingAvgType kind)=>kind is MovingAvgType.TripleExponentialMovingAverage or MovingAvgType.ZeroLagTripleExponentialMovingAverage;
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger value,long divisor=1)=>RocBankValue.RoundUnits(value,divisor);
    internal VervoortCandleWindow(bool longTerm,MovingAvgType kind,int length,double factor=1.1)
    {
        StreamingInputValidation.Finite(factor,nameof(factor));_longTerm=longTerm;_factor=U(longTerm?factor:.35);
        _haFirst=new(kind,length);_haSecond=new(kind,length);_midFirst=new(kind,length);_midSecond=new(kind,length);
    }
    private sealed class Smooth : IDisposable
    {
        private readonly RocBankAverage[] _stages;
        private readonly bool _zeroLag;
        internal Smooth(MovingAvgType kind,int length)
        {_zeroLag=kind==MovingAvgType.ZeroLagTripleExponentialMovingAverage;_stages=Enumerable.Range(0,_zeroLag?6:3).Select(_=>new RocBankAverage(MovingAvgType.ExponentialMovingAverage,length,1,true)).ToArray();}
        private BigInteger Tema(BigInteger input,int offset,bool final)
        {
            var sum=new ExactMeanAccumulator();sum.Add(double.Epsilon,input);var first=_stages[offset].Next(RocBankValue.Round(sum),final);
            var second=_stages[offset+1].Next(first,final);var third=_stages[offset+2].Next(second,final);
            sum=new();first.AddTo(ref sum,3);second.AddTo(ref sum,-3);third.AddTo(ref sum);var result=RocBankValue.Round(sum);
            return U(result.Mantissa)<<result.UpperShift;
        }
        internal BigInteger Next(BigInteger input,bool final)
        {var first=Tema(input,0,final);return _zeroLag?Round(2*first-Tema(first,3,final)):first;}
        internal void Reset(){foreach(var stage in _stages)stage.Reset();}
        public void Dispose(){foreach(var stage in _stages)stage.Dispose();}
    }
    internal (double Value,Signal Trade) Next(double input,double high,double low,double open,double close,bool final)
    {
        foreach(var value in new[]{input,high,low,open,close})StreamingInputValidation.Finite(value,nameof(input));
        var haOpen=Round(_input+_open,2);var haClose=Round(U(input)+haOpen+BigInteger.Max(U(high),haOpen)+BigInteger.Min(U(low),haOpen),4);
        var midpoint=Round(U(high)+U(low),2);
        var first=_haFirst.Next(haClose,final);var second=_haSecond.Next(first,final);
        var midFirst=_midFirst.Next(midpoint,final);var midSecond=_midSecond.Next(midFirst,final);
        var projectedHa=Round(2*first-second);var projectedMid=Round(2*midFirst-midSecond);
        var narrow=(BigInteger.Abs(U(close)-U(open))<<1074)<(U(high)-U(low))*_factor;
        var upSeed=haClose>=haOpen && _close>=_open || projectedMid>=projectedHa
            || _longTerm && (U(close)>=haClose || high>_high || low>_low);
        var downSeed=haClose<haOpen && _close<_open || projectedMid<projectedHa;
        var rising=close>=open || close>=_lastClose;var falling=close<open || close<_lastClose;
        var upKeep=_longTerm?upSeed || _upSeed && rising:(upSeed || _upSeed) && rising;
        var downKeep=_longTerm?downSeed || _downSeed && falling:(downSeed || _downSeed) && falling;
        var upTrend=upKeep || _upKeep && narrow && high>=_low;
        var downTrend=_longTerm?(downKeep || _downKeep) && narrow && low<=_high:downKeep || _downKeep && narrow && low<=_high;
        var up = !downTrend && _downTrend && upTrend;var down = !upTrend && _upTrend && downTrend;
        var line=up?1:down?-1:_line;
        var trade=line>0?line>_line?Signal.StrongBuy:Signal.Buy:line<0?line<_line?Signal.StrongSell:Signal.Sell:Signal.None;
        if(final)
        {
            _input=U(input);_open=haOpen;_close=haClose;_high=high;_low=low;_lastClose=close;
            _upSeed=upSeed;_downSeed=downSeed;_upKeep=upKeep;_downKeep=downKeep;_upTrend=upTrend;_downTrend=downTrend;_line=line;
        }
        return(line,trade);
    }
    internal void Reset()
    {
        _haFirst.Reset();_haSecond.Reset();_midFirst.Reset();_midSecond.Reset();_input=_open=_close=default;_high=_low=_lastClose=_line=0;
        _upSeed=_downSeed=_upKeep=_downKeep=_upTrend=_downTrend=false;
    }
    public void Dispose(){_haFirst.Dispose();_haSecond.Dispose();_midFirst.Dispose();_midSecond.Dispose();}
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,bool longTerm,MovingAvgType kind,int length,double factor=1.1)
    {
        var(input,high,low,open,close,_)=CalculationsHelper.GetInputValuesList(InputName.FullTypicalPrice,data);
        foreach(var values in new[]{input,high,low,open,close,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new VervoortCandleWindow(longTerm,kind,length,factor);var valuesOut=new List<double>(input.Count);var signals=new List<Signal>(input.Count);
        for(var i=0;i<input.Count;i++){var point=state.Next(input[i],high[i],low[i],open[i],close[i],true);valuesOut.Add(point.Value);signals.Add(point.Trade);}
        return(valuesOut,signals);
    }
}
