using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> VervoortSmoothedOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator,bool selected=false)
    {var o=indicator.CreateOptions();return VervoortSmoothedOutputs(bars,Integer(o,"Length1",18),Integer(o,"Length2",30),Integer(o,"Length3",2),Integer(o,"SmoothLength",3),Number(o,2,"StdDevMult"),selected:selected);}
    private static double VervoortRootOffset(ReferenceFraction numerator,ReferenceFraction variance)
    {
        if(numerator.Sign==0)return 50;
        var (n,d)=(numerator*numerator/variance).Components;
        BigInteger Root(BigInteger value)
        {if(value.IsZero)return value;var estimate=BigInteger.One<<((value.ToByteArray().Length*8+1)/2);while(true){var next=(estimate+value/estimate)/2;if(next>=estimate)return estimate;estimate=next;}}
        for(var precision=80;;precision=checked(precision*3))
        {
            var scaled=n<<(2*precision);var floor=Root(scaled/d);var ceiling=floor*floor*d==scaled?floor:floor+1;var grid=new ReferenceFraction(BigInteger.One<<precision);
            var low=new ReferenceFraction(numerator.Sign>0?floor:-ceiling)/grid+new ReferenceFraction(50);
            var high=new ReferenceFraction(numerator.Sign>0?ceiling:-floor)/grid+new ReferenceFraction(50);
            var left=low.ToDouble();var right=high.ToDouble();
#pragma warning disable S1244 // Equal rounded interval endpoints certify the exact output.
            if(left==right)return left;
#pragma warning restore S1244
        }
    }
    internal static IReadOnlyDictionary<string,double[]> VervoortSmoothedOutputs(IReadOnlyList<Bar> bars,int band,int range,int cascade,int smooth,double multiplier,ICollection<Signal>? signals=null,bool selected=false)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value)=>value.RoundExtendedBinary64();
        var input=bars.Select(b=>R(b.Close)).ToArray();var precise=input;var rounded=input;var rainbow=new ReferenceFraction[bars.Count];var roundedRainbow=new ReferenceFraction[bars.Count];
        for(var i=0;i<bars.Count;i++)rainbow[i]=roundedRainbow[i]=R(0);
        for(var depth=0;depth<10;depth++)
        {
            precise=RationalAverage(precise,cascade,1);rounded=SmoothRocBankStage(rounded,cascade,1,Round);
            for(var i=0;i<bars.Count;i++){rainbow[i]+=R(Math.Max(1,5-depth))*precise[i];roundedRainbow[i]+=R(Math.Max(1,5-depth))*rounded[i];}
        }
        for(var i=0;i<bars.Count;i++){rainbow[i]/=R(20);roundedRainbow[i]=Round(roundedRainbow[i]/R(20));}
        var ema1=RationalAverage(rainbow,smooth,3);var ema2=RationalAverage(ema1,smooth,3);var dema=ema1.Select((v,i)=>R(2)*v-ema2[i]).ToArray();
        var a=RationalAverage(dema,smooth,3);var b=RationalAverage(a,smooth,3);var c=RationalAverage(b,smooth,3);
        var filtered=a.Select((v,i)=>R(3)*v-R(3)*b[i]+c[i]).ToArray();var center=RationalAverage(filtered,band,2);
        var blend=bars.Select((bar,i)=>Round((roundedRainbow[i]+(selected?R(bar.Close):Round((R(bar.High)+R(bar.Low)+R(bar.Close))/R(3))))/R(2))).ToArray();
        var line=new double[bars.Count];var fast=new double[bars.Count];var stochastic=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            if(i+1>=band && multiplier!=0)
            {
                var sample=filtered.Skip(i-band+1).Take(band).ToArray();var mean=sample.Aggregate(R(0),(sum,v)=>sum+v)/R(band);
                var variance=sample.Aggregate(R(0),(sum,v)=>sum+(v-mean)*(v-mean))/R(band);var max=sample.Select(v=>v.Sign<0?R(0)-v:v).Aggregate(R(0),(m,v)=>v.CompareTo(m)>0?v:m);
                var cutoff=R(1.4210854715202004e-14)*max;
                if(variance.CompareTo(cutoff*cutoff)>0)line[i]=VervoortRootOffset(R(50)*(filtered[i]-center[i])/R(multiplier),variance);
            }
            var first=Math.Max(0,i-range+1);var candles=bars.Skip(first).Take(i-first+1).ToArray();var minimum=blend.Skip(first).Take(i-first+1).Aggregate(blend[i],(m,v)=>v.CompareTo(m)<0?v:m);
            var denominator=R(candles.Max(v=>v.High))-minimum;var numerator=blend[i]-R(candles.Min(v=>v.Low));
            fast[i]=denominator.Sign==0?0:Math.Max(0,Math.Min(100,(R(100)*numerator/denominator).ToDouble()));
            var start=Math.Max(0,i-smooth+1);stochastic[i]=(fast.Skip(start).Take(i-start+1).Aggregate(R(0),(sum,v)=>sum+R(v))/R(i-start+1)).ToDouble();
            signals?.Add(stochastic[i]>(i==0?0:stochastic[i-1]) && line[i]>(i==0?0:line[i-1])?Signal.Buy:stochastic[i]<(i==0?0:stochastic[i-1]) && line[i]<(i==0?0:line[i-1])?Signal.Sell:Signal.None);
        }
        return Outputs(("Vso",line),("Sk",stochastic));
    }
}
