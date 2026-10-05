using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MesaPredictV2Window : IDisposable
{
    private readonly int _historyLength,_smoothLength,_horizon;private readonly MovingAvgType _kind;private readonly double _c1,_c2,_c3,_s1,_s2,_s3;private readonly Queue<BigInteger> _smooth=new(),_filtered=new();
    private BigInteger _price1,_price2,_hp1,_hp2,_ssf1,_ssf2,_prediction,_slope;private int _count;
    internal static bool Supports(MovingAvgType kind)=>kind is MovingAvgType.EhlersHannMovingAverage or MovingAvgType.WeightedMovingAverage;
    internal MesaPredictV2Window(MovingAvgType kind,int history,int high,int smooth,int horizon)
    {
        _kind=kind;_historyLength=Math.Max(1,history);_smoothLength=Math.Max(1,smooth);_horizon=Math.Min(_historyLength,Math.Max(1,horizon));high=Math.Max(1,high);
        var a=Math.Exp(Math.Max(-.99,Math.Min(-.01,-1.414*Math.PI/high)));_c2=2*a*Math.Cos(Math.Max(.01,Math.Min(.99,1.414*Math.PI/high)));_c3=-a*a;_c1=(1+_c2-_c3)/4;
        a=Math.Exp(Math.Max(-.99,Math.Min(-.01,-1.414*Math.PI/_smoothLength)));_s2=2*a*Math.Cos(Math.Max(.01,Math.Min(.99,1.414*Math.PI/_smoothLength)));_s3=-a*a;_s1=1-_s2-_s3;
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger numerator,BigInteger denominator)=>RocBankValue.RoundUnits(numerator,denominator);
    internal (double Filter,double Predict,double Extrap,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var value=U(price);var unit=BigInteger.One<<1074;
        var hp=_count<4?BigInteger.Zero:Round(U(_c1)*(value-2*_price1+_price2)+U(_c2)*_hp1+U(_c3)*_hp2,unit);
        var ssf=_count<3?hp:Round(U(_s1)*(hp+_hp1)+2*U(_s2)*_ssf1+2*U(_s3)*_ssf2,2*unit);
        var past=_smooth.ToArray();BigInteger sum=0;
        for(var lag=0;lag<Math.Min(_smoothLength,past.Length+1L);lag++)
        {
            var prior=lag==0?ssf:past[past.Length-lag];
            if(_kind==MovingAvgType.EhlersHannMovingAverage){var sine=Math.Sin(Math.PI*((lag+1d)/(_smoothLength+1d)));sum+=prior*U(2*sine*sine);}else sum+=prior*(_smoothLength-lag);
        }
        var filtered=Round(sum,_kind==MovingAvgType.EhlersHannMovingAverage?unit*(_smoothLength+1L):(BigInteger)_smoothLength*(_smoothLength+1L)/2);
        var history=_filtered.ToArray();var forecast=new BigInteger[5];forecast[0]=filtered;for(var lag=1;lag<5&&lag<_historyLength&&lag<=history.Length;lag++)forecast[lag]=history[history.Length-lag];
        var extrap=filtered;var older=forecast[1];var coefficients=new[]{4.525,-8.45,8.145,-4.045,.825};
        if(forecast.Any(v=>!v.IsZero))for(long step=0;step<_horizon;step++)
        {
            BigInteger predicted=0;for(var k=0;k<5;k++)predicted+=forecast[k]*U(coefficients[k]);predicted=Round(predicted,unit);for(var k=4;k>0;k--)forecast[k]=forecast[k-1];forecast[0]=predicted;
            var next=Round(2*extrap-older,BigInteger.One);older=extrap;extrap=next;
        }
        var prediction=forecast[0];var slope=prediction-_prediction;var trade=slope.Sign>0&&slope>_slope?Signal.StrongBuy:slope.Sign<0&&slope<_slope?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_smooth.Count==_smoothLength)_smooth.Dequeue();_smooth.Enqueue(ssf);if(_filtered.Count==Math.Min(4,_historyLength))_filtered.Dequeue();_filtered.Enqueue(filtered);_price2=_price1;_price1=value;_hp2=_hp1;_hp1=hp;_ssf2=_ssf1;_ssf1=ssf;_prediction=prediction;_slope=slope;if(_count<4)_count++;}
        return(ExactMeanAccumulator.UnitRatio(filtered,BigInteger.One),ExactMeanAccumulator.UnitRatio(prediction,BigInteger.One),ExactMeanAccumulator.UnitRatio(extrap,BigInteger.One),trade);
    }
    internal void Reset(){_smooth.Clear();_filtered.Clear();_price1=_price2=_hp1=_hp2=_ssf1=_ssf2=_prediction=_slope=default;_count=0;}
    public void Dispose()=>Reset();
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int history,int high,int smooth,int horizon)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new MesaPredictV2Window(kind,history,high,smooth,horizon);var output=new Dictionary<string,List<double>>{{"Ssf",new()},{"Predict",new()},{"Extrap",new()}};var signals=new List<Signal>(input.Count);foreach(var price in input){var p=state.Next(price,true);output["Ssf"].Add(p.Filter);output["Predict"].Add(p.Predict);output["Extrap"].Add(p.Extrap);signals.Add(p.Trade);}return(output,signals);
    }
}
