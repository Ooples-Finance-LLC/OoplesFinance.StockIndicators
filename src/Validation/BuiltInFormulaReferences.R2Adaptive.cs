using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static ReferenceFraction R2Offset(ReferenceFraction center,ReferenceFraction numerator,ReferenceFraction variance)
    {
        if(numerator.Sign==0||variance.Sign==0)return center.RoundExtendedBinary64();
        var square=numerator*numerator/variance;var (n,d)=square.Components;
        BigInteger Root(BigInteger value){if(value.IsZero)return value;var estimate=BigInteger.One<<((value.ToByteArray().Length*8+1)/2);while(true){var next=(estimate+value/estimate)/2;if(next>=estimate)return estimate;estimate=next;}}
        for(var precision=80;;precision=checked(precision*3))
        {
            var scaled=n<<(2*precision);var root=Root(scaled/d);var ceiling=root*root*d==scaled?root:root+1;var grid=new ReferenceFraction(BigInteger.One<<precision);
            var low=(center+new ReferenceFraction(numerator.Sign>0?root:-ceiling)/grid).RoundExtendedBinary64();var high=(center+new ReferenceFraction(numerator.Sign>0?ceiling:-root)/grid).RoundExtendedBinary64();if(low.CompareTo(high)==0)return low;
        }
    }
    private static ReferenceFraction[] R2Linear(IReadOnlyList<Bar> bars,int period)
    {
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);var result=new ReferenceFraction[bars.Count];
        for(var i=0;i<bars.Count;i++){var first=Math.Max(0,i-period+1);var count=i-first+1;var center=R(count-1)/R(2);var sum=R(0);var xy=R(0);var xx=R(0);
            for(var j=first;j<=i;j++){var x=R(j-first)-center;sum+=R(bars[j].Close);xy+=x*R(bars[j].Close);xx+=x*x;}result[i]=(sum/R(count)+(xx.Sign==0?R(0):xy/xx*center)).RoundExtendedBinary64();}
        return result;
    }
    internal static IReadOnlyDictionary<string,double[]> R2AdaptiveOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return R2AdaptiveOutputs(bars,Integer(o,"Length",100),AverageKind(o,1));}
    internal static IReadOnlyDictionary<string,double[]> R2AdaptiveOutputs(IReadOnlyList<Bar> bars,int period,int kind,ICollection<Signal>? signals=null)
    {
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);ReferenceFraction Round(ReferenceFraction v)=>v.RoundExtendedBinary64();
        var prices=bars.Select(b=>R(b.Close)).ToArray();var linear=R2Linear(bars,period);var average=SmoothRocBankStage(prices,period,kind,Round);var lagged=new ReferenceFraction[bars.Count];var adaptive=new ReferenceFraction[bars.Count];var errors=new ReferenceFraction[bars.Count];var result=new ReferenceFraction[bars.Count];
        (ReferenceFraction Cov,ReferenceFraction Xvar,ReferenceFraction Weight,ReferenceFraction Mean) Correlation(ReferenceFraction[] source,int i)
        {
            var first=Math.Max(0,i-period+1);var count=i-first+1;var x=source.Skip(first).Take(count).ToArray();var y=prices.Skip(first).Take(count).ToArray();var mx=x.Aggregate(R(0),(sum,v)=>sum+v)/R(count);var my=y.Aggregate(R(0),(sum,v)=>sum+v)/R(count);
            var xx=R(0);var yy=R(0);var xy=R(0);for(var j=0;j<count;j++){xx+=(x[j]-mx)*(x[j]-mx);yy+=(y[j]-my)*(y[j]-my);xy+=(x[j]-mx)*(y[j]-my);}
            return(xy/R(count),xx/R(count),xx.Sign==0||yy.Sign==0?R(0):xy*xy/(xx*yy),mx);
        }
        for(var i=0;i<bars.Count;i++)
        {
            lagged[i]=i==0?prices[i]:result[i-1];var lag=Correlation(lagged,i);var deviation=lagged[i]-lag.Mean;errors[i]=deviation*deviation;
            var first=Math.Max(0,i-period+1);var count=i-first+1;var energy=errors.Skip(first).Take(count).Aggregate(R(0),(sum,v)=>sum+v)/R(count);
            adaptive[i]=R2Offset(average[i],i+1<period?R(0):lag.Cov*deviation,lag.Xvar*energy);
            var w1=Correlation(linear,i).Weight;var w2=Correlation(adaptive,i).Weight;result[i]=Round(w1*linear[i]+w2*adaptive[i]+(R(1)-w1-w2)*lagged[i]);
            var slope=prices[i]-result[i];var old=i==0?R(0):prices[i-1]-result[i-1];signals?.Add(slope.Sign>0&&slope.CompareTo(old)>0?Signal.StrongBuy:slope.Sign<0&&slope.CompareTo(old)<0?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("R2ar",result.Select(v=>v.ToDouble()).ToArray()));
    }
    internal static double[] R2CoreReference(IReadOnlyList<Bar> bars,int period)
    {
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);var linear=R2Linear(bars,period);var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++){var first=Math.Max(0,i-period+1);var count=i-first+1;var mean=bars.Skip(first).Take(count).Aggregate(R(0),(sum,b)=>sum+R(b.Close))/R(count);var total=R(0);var errors=R(0);
            for(var j=first;j<=i;j++){var price=R(bars[j].Close);total+=(price-mean)*(price-mean);errors+=(price-linear[j])*(price-linear[j]);}var weight=total.Sign==0?R(0):R(1)-errors/total;if(weight.Sign<0)weight=R(0);result[i]=(weight*linear[i]+(R(1)-weight)*R(bars[i].Close)).ToDouble();}return result;
    }
}
