using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static double VervoortModifiedRootOffset(ReferenceFraction numerator,ReferenceFraction variance)
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
    internal static IReadOnlyDictionary<string,double[]> VervoortModifiedOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator,bool selected=false)
    {var o=indicator.CreateOptions();return VervoortModifiedOutputs(bars,AverageKind(o,5),Integer(o,"Length1",18),Integer(o,"Length2",200),Integer(o,"SmoothLength",8),Number(o,1.6,"StdDevMult"),selected:selected);}
    internal static IReadOnlyDictionary<string,double[]> VervoortModifiedOutputs(IReadOnlyList<Bar> bars,int kind,int band,int outer,int smooth,double multiplier,ICollection<Signal>? signals=null,bool selected=false)
    {
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);
        var source=bars.Select(b=>selected?R(b.Close):R(((R(b.Open)+R(b.High)+R(b.Low)+R(b.Close))/R(4)).ToDouble())).ToArray();var close=new ReferenceFraction[bars.Count];var previous=R(0);
        for(var i=0;i<bars.Count;i++){var open=(previous+(i==0?R(0):source[i-1]))/R(2);var high=R(bars[i].High);var low=R(bars[i].Low);close[i]=(source[i]+open+(high.CompareTo(open)>0?high:open)+(low.CompareTo(open)<0?low:open))/R(4);previous=open;}
        ReferenceFraction[] Average(ReferenceFraction[] values)
        {
            if(kind is not (4 or 5))return RationalAverage(values,smooth,kind);
            var a=RationalAverage(values,smooth,3);var b=RationalAverage(a,smooth,3);var c=kind==5?RationalAverage(b,smooth,3):a;
            return a.Select((v,i)=>kind==4?R(2)*v-b[i]:R(3)*v-R(3)*b[i]+c[i]).ToArray();
        }
        var first=Average(close);var second=Average(first);var filtered=Average(first.Select((v,i)=>R(2)*v-second[i]).ToArray());var center=RationalAverage(filtered,band,2);
        var variances=new ReferenceFraction[bars.Count];var percent=new double[bars.Count];var upper=new double[bars.Count];var lower=new double[bars.Count];
        ReferenceFraction Variance(ReferenceFraction[] sample){var mean=sample.Aggregate(R(0),(sum,v)=>sum+v)/R(sample.Length);return sample.Aggregate(R(0),(sum,v)=>sum+(v-mean)*(v-mean))/R(sample.Length);}
        for(var i=0;i<bars.Count;i++)
        {
            if(i+1>=band){var sample=filtered.Skip(i-band+1).Take(band).ToArray();var variance=Variance(sample);var maximum=sample.Select(v=>v.Abs()).Max();var cutoff=R(1.4210854715202004e-14)*maximum;
                if(variance.CompareTo(cutoff*cutoff)>0)percent[i]=VervoortModifiedRootOffset(R(25)*(filtered[i]-center[i]),variance);}
            var outerVariance=i+1<outer?R(0):Variance(percent.Skip(i-outer+1).Take(outer).Select(R).ToArray());
            variances[i]=outerVariance;
            upper[i]=outerVariance.Sign==0?50:VervoortModifiedRootOffset(R(multiplier)*outerVariance,outerVariance);lower[i]=outerVariance.Sign==0?50:VervoortModifiedRootOffset(R(-multiplier)*outerVariance,outerVariance);
            var slope=R(percent[i])-R(50);var old=i==0?R(0):R(percent[i-1])-R(50);var previousPercent=i==0?0:percent[i-1];
            int CompareBand(int index,double price,int direction)
            {
                if(index<0)return R(0).CompareTo(R(price));
                var coefficient=R(multiplier)*R(direction);var delta=R(price)-R(50);var v=variances[index];
                if(v.Sign==0 || coefficient.Sign==0)return (R(50)-R(price)).Sign;
                if(coefficient.Sign!=delta.Sign)return coefficient.Sign.CompareTo(delta.Sign);
                return coefficient.Sign*(coefficient*coefficient*v).CompareTo(delta*delta);
            }
            signals?.Add(slope.Sign>0 && slope.CompareTo(old)>0?Signal.StrongBuy:slope.Sign<0 && slope.CompareTo(old)<0?Signal.StrongSell:slope.Sign>0 || CompareBand(i-1,previousPercent,-1)>0 && CompareBand(i,percent[i],-1)<0?Signal.Buy:slope.Sign<0 || CompareBand(i-1,previousPercent,1)<0 && CompareBand(i,percent[i],1)>0?Signal.Sell:Signal.None);
        }
        return Outputs(("UpperBand",upper),("MiddleBand",Enumerable.Repeat(50d,bars.Count).ToArray()),("LowerBand",lower),("PercentB",percent));
    }
}
