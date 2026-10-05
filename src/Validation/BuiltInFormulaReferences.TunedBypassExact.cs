using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using R=OoplesFinance.StockIndicators.Validation.ReferenceFraction;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] TunedCycles(IReadOnlyList<Bar> bars,int minimum,int maximum,int cutoff,int median)
    {
        minimum=Math.Max(3,minimum);maximum=Math.Max(minimum,maximum);median=Math.Max(1,median);R D(double value)=>R.FromDouble(value);R Round(R value)=>value.RoundExtendedBinary64();R At(R[] v,int i)=>i<0?new R(0):v[i];
        var scale=new R(BigInteger.One<<1074);var prices=bars.Select(b=>D(b.Close)*scale).ToArray();var hp=new R[bars.Count];var smooth=new R[bars.Count];var pole=Math.Tan(Math.PI/4-Math.PI/Math.Max(3,cutoff));var taps=new[]{1,2,3,3,2,1};
        for(var i=0;i<bars.Count;i++){hp[i]=i<7?prices[i]:Round(D(.5*(1+pole))*(prices[i]-At(prices,i-1))+D(pole)*At(hp,i-1));smooth[i]=i<7?prices[i]-At(prices,i-1):Round(taps.Select((w,lag)=>new R(w)*hp[i-lag]).Aggregate(new R(0),(s,v)=>s+v)/new R(12));}
        var powers=new List<(int Period,R[] Values)>();
        for(long period=minimum;period<=maximum;period++)
        {
            var real=new R[bars.Count];var imaginary=new R[bars.Count];var energy=new R[bars.Count];var quadrature=new R[bars.Count];var qScale=D(period/(2*Math.PI));
            for(var i=0;i<bars.Count;i++)
            {
                var width=4*Math.PI*Math.Max(.5-.015*i,.15)/period;var alpha=Math.Cos(width)/(1+Math.Abs(Math.Sin(width)));var gain=D(.5*(1-alpha));var feedback=D(Math.Cos(2*Math.PI/period)*(1+alpha));var decay=D(alpha);quadrature[i]=Round(qScale*(smooth[i]-At(smooth,i-1)));
                real[i]=Round(gain*(smooth[i]-At(smooth,i-2))+feedback*At(real,i-1)-decay*At(real,i-2));imaginary[i]=Round(gain*(quadrature[i]-At(quadrature,i-2))+feedback*At(imaginary,i-1)-decay*At(imaginary,i-2));energy[i]=real[i]*real[i]+imaginary[i]*imaginary[i];
            }
            powers.Add(((int)period,energy));
        }
        var cycles=new double[bars.Count];var output=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var peak=powers.Aggregate(new R(0),(max,p)=>max.CompareTo(p.Values[i])>=0?max:p.Values[i]);var total=new R(0);var weighted=new R(0);
            if(peak.Sign>0)foreach(var bin in powers){var ratio=(new R(100)-new R(99)*bin.Values[i]/peak).ToDouble();var db=10*Math.Log10(ratio);if(db<=3){var weight=D(maximum-db);total+=weight;weighted+=new R(bin.Period)*weight;}}
            cycles[i]=total.Sign==0?minimum:Math.Max(minimum,Math.Min(maximum,(weighted/total).ToDouble()));var window=cycles.Skip(Math.Max(0,i-median+1)).Take(Math.Min(i+1,median)).OrderBy(v=>v).ToArray();output[i]=((D(window[(window.Length-1)/2])+D(window[window.Length/2]))/new R(2)).ToDouble();
        }
        return output;
    }
    internal static IReadOnlyDictionary<string,double[]> TunedBypassOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return TunedBypassOutputs(bars,Integer(o,"MinLength",8),Integer(o,"MaxLength",50),Integer(o,"Length1",40),Integer(o,"Length2",10));}
    internal static IReadOnlyDictionary<string,double[]> TunedBypassOutputs(IReadOnlyList<Bar> bars,int minimum,int maximum,int cutoff,int median,ICollection<Signal>? signals=null)
    {
        R D(double value)=>R.FromDouble(value);R Round(R value)=>value.RoundExtendedBinary64();R At(R[] v,int i)=>i<0?new R(0):v[i];var cycles=TunedCycles(bars,minimum,maximum,cutoff,median);var prices=bars.Select(b=>D(b.Close)).ToArray();var hp=new R[bars.Count];var smooth=new R[bars.Count];var first=new R[bars.Count];var second=new R[bars.Count];var taps=new[]{1,2,3,3,2,1};var angle=Math.Max(.01,Math.Min(.99,2*Math.PI/Math.Max(1,cutoff)));var pole=Math.Cos(angle)/(1+Math.Sin(angle));
        for(var i=0;i<bars.Count;i++)
        {
            hp[i]=i<7?prices[i]:Round(D(.5*(1+pole))*(prices[i]-At(prices,i-1))+D(pole)*At(hp,i-1));smooth[i]=i<7?prices[i]-At(prices,i-1):Round(taps.Select((w,lag)=>new R(w)*hp[i-lag]).Aggregate(new R(0),(s,v)=>s+v)/new R(12));
            var beta=Math.Cos(Math.Max(.01,Math.Min(.99,2*Math.PI/cycles[i])));var width=Math.Max(.01,Math.Min(.99,4*Math.PI*(Math.Max(.5-.015*i,.15)/cycles[i])));var alpha=Math.Cos(width)/(1+Math.Sin(width));
            first[i]=Round(D(.5*(1-alpha))*(smooth[i]-At(smooth,i-1))+D(beta*(1+alpha))*At(first,i-1)-D(alpha)*At(first,i-2));second[i]=Round(D(cycles[i]/Math.PI*2)*(first[i]-At(first,i-1)));signals?.Add(second[i].CompareTo(first[i])>0&&second[i].Sign>=0?Signal.Buy:second[i].CompareTo(first[i])<0||second[i].Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("V1",first.Select(v=>v.ToDouble()).ToArray()),("V2",second.Select(v=>v.ToDouble()).ToArray()));
    }
}
