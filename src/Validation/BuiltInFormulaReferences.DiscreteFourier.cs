using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? DiscreteFourierFormula(IBuiltInIndicator indicator)
        =>indicator.CreateOptions() is EhlersDiscreteFourierTransformSpecOptions?new("Edft",new[]{"Edft"},bars=>DiscreteFourierOutputs(bars,indicator)):null;
    internal static IReadOnlyDictionary<string,double[]> DiscreteFourierOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return DiscreteFourierOutputs(bars,Integer(o,"MinLength",8),Integer(o,"MaxLength",50),Integer(o,"Length",40));}
    internal static IReadOnlyDictionary<string,double[]> DiscreteFourierOutputs(IReadOnlyList<Bar> bars,int minimum,int maximum,int cutoff,ICollection<Signal>? signals=null,bool rationalQuadrature=false)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);ReferenceFraction Round(ReferenceFraction value)=>value.RoundExtendedBinary64();minimum=Math.Max(3,minimum);maximum=Math.Max(minimum,maximum);
        var angle=Math.Max(.01,Math.Min(.99,2*Math.PI/Math.Max(1,cutoff)));var pole=Math.Cos(angle)/(1+Math.Sin(angle));var drive=R((1+pole)/2);var hp=new ReferenceFraction[bars.Count];var clean=new ReferenceFraction[bars.Count];var output=new double[bars.Count];var taps=new[]{1,2,3,3,2,1};
        for(var i=0;i<bars.Count;i++)
        {
            var price=R(bars[i].Close);hp[i]=i<6?price:Round(R(pole)*hp[i-1]+drive*(price-R(bars[i-1].Close)));clean[i]=i<6?hp[i]:Round(taps.Select((w,lag)=>R(w)*hp[i-lag]).Aggregate(R(0),(sum,v)=>sum+v)/R(12));
            var first=Math.Max(0,i-maximum+1);var count=i-first+1;var mass=clean.Skip(first).Take(count).Select(v=>v.Abs()).Max();var scale=bars.Skip(first).Take(count).Select(b=>R(b.Close).Abs()).Max();
            if(mass.CompareTo(R(1.4210854715202004e-14)*scale)>0)
            {
                BigInteger Units(ReferenceFraction value){var(n,d)=value.Components;var scaled=n<<1074;var quotient=BigInteger.DivRem(scaled,d,out var remainder);if(!remainder.IsZero)throw new InvalidOperationException("Expected dyadic observation");return quotient;}
                var samples=clean.Skip(first).Take(count).Select(Units).Reverse().ToArray();
                var powers=new List<(int Period,ReferenceFraction Power)>();var peak=R(0);
                for(long period=minimum;period<=maximum;period++)
                {
                    ReferenceFraction power;
                    if(rationalQuadrature){var real=R(0);var imaginary=R(0);for(var lag=0;lag<count;lag++){var phase=2*Math.PI*lag/period;real+=clean[i-lag]*R(Math.Cos(phase));imaginary+=clean[i-lag]*R(Math.Sin(phase));}power=real*real+imaginary*imaginary;}
                    else {BigInteger real=0,imaginary=0;for(var lag=0;lag<count;lag++){var phase=2*Math.PI*lag/period;real+=samples[lag]*Units(R(Math.Cos(phase)));imaginary+=samples[lag]*Units(R(Math.Sin(phase)));}power=new ReferenceFraction(real*real+imaginary*imaginary);}
                    powers.Add(((int)period,power));if(power.CompareTo(peak)>0)peak=power;
                }
                var total=R(0);var weighted=R(0);if(peak.Sign>0)foreach(var bin in powers){var argument=(R(100)-R(99)*bin.Power/peak).ToDouble();var weight=R(Math.Max(0,3-10*Math.Log10(argument)));total+=weight;weighted+=R(bin.Period)*weight;}output[i]=total.Sign==0?0:(weighted/total).ToDouble();
            }
            var previous=i==0?R(0):hp[i-1];signals?.Add(hp[i].Sign>0&&hp[i].CompareTo(previous)>0?Signal.StrongBuy:hp[i].Sign<0&&hp[i].CompareTo(previous)<0?Signal.StrongSell:hp[i].Sign>0?Signal.Buy:hp[i].Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("Edft",output));
    }
}
