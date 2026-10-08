using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using R=OoplesFinance.StockIndicators.Validation.ReferenceFraction;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> DftSpectrumOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return DftSpectrumOutputs(bars,Integer(o,"Length1",48),Integer(o,"Length2",10));}
    internal static IReadOnlyDictionary<string,double[]> DftSpectrumOutputs(IReadOnlyList<Bar> bars,int upper,int lower,ICollection<Signal>? signals=null,bool rationalQuadrature=false)
    {
        upper=Math.Max(1,upper);lower=Math.Max(1,lower);var roof=new R[bars.Count];RoofingValues(bars,upper,lower,false,roof);var powers=new List<(int Period,R[] Values)>();
        // Common scale keeps every fourth power above the binary64 underflow range;
        // it cancels from the final ratio. Each recurrence has 53 significant bits.
        var scale=new R(BigInteger.One<<2148);
        BigInteger Units(R value){var(n,d)=value.Components;var q=BigInteger.DivRem(n<<1074,d,out var remainder);if(!remainder.IsZero)throw new InvalidOperationException("Expected dyadic observation");return q;}
        var units=roof.Select(Units).ToArray();

        for(long period=lower;period<=upper;period++)
        {
            var trajectory=new R[bars.Count];
            for(var i=0;i<bars.Count;i++)
            {
                R energy;
                if(rationalQuadrature){var real=new R(0);var imaginary=new R(0);for(var lag=0;lag<=Math.Min((long)upper,i);lag++){var angle=2*Math.PI*((double)lag/period);real+=roof[i-lag]*R.FromDouble(Math.Cos(angle))*scale;imaginary+=roof[i-lag]*R.FromDouble(Math.Sin(angle))*scale;}energy=real*real+imaginary*imaginary;}
                else {BigInteger real=0,imaginary=0;for(var lag=0;lag<=Math.Min((long)upper,i);lag++){var angle=2*Math.PI*((double)lag/period);real+=units[i-lag]*Units(R.FromDouble(Math.Cos(angle)));imaginary+=units[i-lag]*Units(R.FromDouble(Math.Sin(angle)));}energy=new R(real*real+imaginary*imaginary);}
                trajectory[i]=(R.FromDouble(.2)*energy*energy+R.FromDouble(.8)*(i==0?new R(0):trajectory[i-1])).RoundExtendedBinary64();
            }
            powers.Add(((int)period,trajectory));
        }
        var output=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var peak=powers.Aggregate(new R(0),(max,p)=>max.CompareTo(p.Values[i])>=0?max:p.Values[i]);var total=new R(0);var weighted=new R(0);
            foreach(var bin in powers)if(peak.Sign>0&&bin.Values[i].CompareTo(peak/new R(2))>=0){total+=bin.Values[i];weighted+=new R(bin.Period)*bin.Values[i];}output[i]=total.Sign==0?0:(weighted/total).ToDouble();
            var slope=roof[i]-(i==0?new R(0):roof[i-1]);var previous=i==0?new R(0):roof[i-1]-(i<2?new R(0):roof[i-2]);signals?.Add(slope.Sign>0&&slope.CompareTo(previous)>0?Signal.StrongBuy:slope.Sign<0&&slope.CompareTo(previous)<0?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("Edftse",output));
    }
}
