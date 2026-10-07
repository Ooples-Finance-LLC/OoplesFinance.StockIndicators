using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TunedBypassWindow : IDisposable
{
    private readonly TunedCycleWindow _cycle;private readonly double _pole;private readonly Queue<BigInteger> _hp=new();private BigInteger _previous,_smooth,_v1,_v12;private long _index;
    internal TunedBypassWindow(int minimum,int maximum,int cutoff,int median){_cycle=new(minimum,maximum,cutoff,median);var angle=Math.Max(.01,Math.Min(.99,2*Math.PI/Math.Max(1,cutoff)));_pole=Math.Cos(angle)/(1+Math.Sin(angle));}
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    internal (double First,double Second,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var cycle=_cycle.Next(price,final);var value=U(price);var unit=BigInteger.One<<1074;var history=_hp.ToArray();BigInteger H(int lag)=>lag<=history.Length?history[history.Length-lag]:BigInteger.Zero;
        var hp=_index<7?value:RocBankValue.RoundUnits(U(.5*(1+_pole))*(value-_previous)+U(_pole)*H(1),unit);var smooth=_index<7?value-_previous:RocBankValue.RoundUnits(hp+2*H(1)+3*H(2)+3*H(3)+2*H(4)+H(5),12);
        var beta=Math.Cos(Math.Max(.01,Math.Min(.99,2*Math.PI/cycle)));var angle=Math.Max(.01,Math.Min(.99,4*Math.PI*(Math.Max(.5-.015*_index,.15)/cycle)));var alpha=Math.Cos(angle)/(1+Math.Sin(angle));
        var first=RocBankValue.RoundUnits(U(.5*(1-alpha))*(smooth-_smooth)+U(beta*(1+alpha))*_v1-U(alpha)*_v12,unit);var second=RocBankValue.RoundUnits(U(cycle/Math.PI*2)*(first-_v1),unit);var trade=second>first&&second.Sign>=0?Signal.Buy:second<first||second.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_hp.Count==5)_hp.Dequeue();_hp.Enqueue(hp);_previous=value;_smooth=smooth;_v12=_v1;_v1=first;_index++;}return(ExactMeanAccumulator.UnitRatio(first,BigInteger.One),ExactMeanAccumulator.UnitRatio(second,BigInteger.One),trade);
    }
    internal void Reset(){_cycle.Reset();_hp.Clear();_previous=_smooth=_v1=_v12=default;_index=0;}
    public void Dispose()=>Reset();
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,int minimum,int maximum,int cutoff,int median)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new TunedBypassWindow(minimum,maximum,cutoff,median);var output=new Dictionary<string,List<double>>{{"V1",new()},{"V2",new()}};var signals=new List<Signal>(input.Count);foreach(var price in input){var p=state.Next(price,true);output["V1"].Add(p.First);output["V2"].Add(p.Second);signals.Add(p.Trade);}return(output,signals);
    }
}
