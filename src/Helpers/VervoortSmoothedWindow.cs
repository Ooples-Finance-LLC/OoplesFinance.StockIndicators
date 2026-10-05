using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
using Radical=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Radical;
using Average=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Average;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VervoortSmoothedWindow : IDisposable
{
    private readonly Average[] _layers,_emas;
    private readonly RocBankAverage[] _rounded;
    private readonly Average _center;
    private readonly int _band,_range,_smooth;
    private readonly double _multiplier;
    private readonly Queue<Number> _filtered=new();
    private readonly Queue<(double High,double Low,BigInteger Blend)> _candles=new();
    private readonly Queue<double> _oscillator=new();
    private Number _sum,_squares;
    private ExactMeanAccumulator _oscillatorSum;
    private double _previousLine,_previousSk;
    internal VervoortSmoothedWindow(int band,int range,int cascade,int smooth,double multiplier)
    {
        StreamingInputValidation.Finite(multiplier,nameof(multiplier));_band=Math.Max(1,band);_range=Math.Max(1,range);_smooth=Math.Max(1,smooth);_multiplier=multiplier;
        _layers=Enumerable.Range(0,10).Select(_=>new Average(MovingAvgType.SimpleMovingAverage,cascade)).ToArray();
        _rounded=Enumerable.Range(0,10).Select(_=>new RocBankAverage(MovingAvgType.SimpleMovingAverage,cascade,1,true)).ToArray();
        _emas=Enumerable.Range(0,5).Select(_=>new Average(MovingAvgType.ExponentialMovingAverage,smooth)).ToArray();
        _center=new(MovingAvgType.WeightedMovingAverage,band);
    }
    private static Number Next(Average average,Number value,bool final)=>average.Next(Radical.Of(value),final).Rational();
    private double Band(Number value,Number center,bool final)
    {
        var full=_filtered.Count==_band;var expired=full?_filtered.Peek():default;
        var sum=_sum+value-expired;var squares=_squares+value*value-expired*expired;double output=0;
        if(_filtered.Count>=_band-1 && _multiplier!=0)
        {
            var variance=(squares.Times(_band)-sum*sum).Divide((long)_band*_band);var maximum=value.Sign<0?value.Times(-1):value;
            foreach(var old in full?_filtered.Skip(1):_filtered)
            {var absolute=old.Sign<0?old.Times(-1):old;if((absolute-maximum).Sign>0)maximum=absolute;}
            var threshold=maximum*Number.Of(1.4210854715202004e-14);
            if((variance-threshold*threshold).Sign>0)
                output=(Radical.Of(Number.Integer(50))+Radical.QuotientRoot((value-center).Times(50).Divide(Number.Of(_multiplier)),variance)).Publish();
        }
        if(final){if(full)_filtered.Dequeue();_filtered.Enqueue(value);_sum=sum;_squares=squares;}return output;
    }
    internal (double Line,double Stochastic,Signal Trade) Next(double close,double typical,double high,double low,bool final)
    {
        foreach(var value in new[]{close,typical,high,low})StreamingInputValidation.Finite(value,nameof(close));
        var exact=Number.Of(close);Number rainbow=default;var rounded=new RocBankValue(close);var roundedSum=new ExactMeanAccumulator();
        for(var i=0;i<10;i++)
        {exact=Next(_layers[i],exact,final);rainbow+=exact.Times(Math.Max(1,5-i));rounded=_rounded[i].Next(rounded,final);rounded.AddTo(ref roundedSum,Math.Max(1,5-i));}
        rainbow=rainbow.Divide(20);var first=Next(_emas[0],rainbow,final);var second=Next(_emas[1],first,final);
        var dema=first.Times(2)-second;var a=Next(_emas[2],dema,final);var b=Next(_emas[3],a,final);var c=Next(_emas[4],b,final);
        var filtered=a.Times(3)-b.Times(3)+c;var center=Next(_center,filtered,final);var line=Band(filtered,center,final);
        var roundedRainbow=RocBankValue.Round(roundedSum,count:20);var blendSum=new ExactMeanAccumulator();roundedRainbow.AddTo(ref blendSum);blendSum.Add(typical);
        var blend=RocBankValue.Round(blendSum,count:2);var blendUnits=ExactVarianceWindow.Units(blend.Mantissa)<<blend.UpperShift;
        var highest=high;var lowest=low;var minimum=blendUnits;
        foreach(var candle in _candles.Count==_range?_candles.Skip(1):_candles)
        {highest=Math.Max(highest,candle.High);lowest=Math.Min(lowest,candle.Low);minimum=BigInteger.Min(minimum,candle.Blend);}
        var numerator=blendUnits-ExactVarianceWindow.Units(lowest);var denominator=ExactVarianceWindow.Units(highest)-minimum;
        var fast=denominator.IsZero?0:ExactMeanAccumulator.UnitRatio((100*numerator*denominator.Sign)<<1074,BigInteger.Abs(denominator));fast=Math.Max(0,Math.Min(100,fast));
        var sum=_oscillatorSum;if(_oscillator.Count==_smooth)sum.Add(_oscillator.Peek(),-1);sum.Add(fast);var stochastic=sum.Mean(Math.Min(_smooth,_oscillator.Count+1L));
        var trade=stochastic>_previousSk && line>_previousLine?Signal.Buy:stochastic<_previousSk && line<_previousLine?Signal.Sell:Signal.None;
        if(final)
        {if(_candles.Count==_range)_candles.Dequeue();_candles.Enqueue((high,low,blendUnits));if(_oscillator.Count==_smooth)_oscillator.Dequeue();_oscillator.Enqueue(fast);_oscillatorSum=sum;_previousLine=line;_previousSk=stochastic;}
        return(line,stochastic,trade);
    }
    internal void Reset()
    {foreach(var layer in _layers)layer.Reset();foreach(var layer in _rounded)layer.Reset();foreach(var stage in _emas)stage.Reset();_center.Reset();_filtered.Clear();_candles.Clear();_oscillator.Clear();_sum=_squares=default;_oscillatorSum=default;_previousLine=_previousSk=0;}
    public void Dispose(){Reset();foreach(var layer in _rounded)layer.Dispose();}
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,int band,int range,int cascade,int smooth,double multiplier)
    {
        var(input,high,low,_,close,_)=CalculationsHelper.GetInputValuesList(InputName.TypicalPrice,data);
        foreach(var values in new[]{input,data.OpenPrices,high,low,close,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new VervoortSmoothedWindow(band,range,cascade,smooth,multiplier);var output=new Dictionary<string,List<double>>{{"Vso",new()},{"Sk",new()}};var signals=new List<Signal>(input.Count);
        for(var i=0;i<input.Count;i++){var typical=new ExactMeanAccumulator();typical.Add(high[i]);typical.Add(low[i]);typical.Add(close[i]);var selected=data.ChainedValues is { Count: > 0 };var point=state.Next(close[i],selected?input[i]:typical.Mean(3),high[i],low[i],true);output["Vso"].Add(point.Line);output["Sk"].Add(point.Stochastic);signals.Add(point.Trade);}return(output,signals);
    }
}
