using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
using R=OoplesFinance.StockIndicators.Validation.ReferenceFraction;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static R UltimateBandOffset(R numerator,R variance)
    {
        if(numerator.Sign==0)return new R(50);var square=numerator*numerator/variance;var(n,d)=square.Components;
        BigInteger Root(BigInteger value){if(value.IsZero)return value;var x=BigInteger.One<<((value.ToByteArray().Length*8+1)/2);while(true){var y=(x+value/x)/2;if(y>=x)return x;x=y;}}
        for(var precision=80;;precision=checked(precision*3)){var scaled=n<<(2*precision);var floor=Root(scaled/d);var ceiling=floor*floor*d==scaled?floor:floor+1;var grid=new R(BigInteger.One<<precision);var low=(new R(50)+new R(numerator.Sign>0?floor:-ceiling)/grid).RoundExtendedBinary64();var high=(new R(50)+new R(numerator.Sign>0?ceiling:-floor)/grid).RoundExtendedBinary64();if(low.CompareTo(high)==0)return low;}
    }
    internal static IReadOnlyDictionary<string,double[]> UltimateMomentumOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator,bool selected=false)
    {var o=(UltimateMomentumIndicatorSpecOptions)indicator.CreateOptions();return UltimateMomentumOutputs(bars,AverageKind(o,1),o.Length1,o.Length2,o.Length3,o.Length4,o.Length5,o.StdDevMult,selected);}
    internal static IReadOnlyDictionary<string,double[]> UltimateMomentumOutputs(IReadOnlyList<Bar> bars,int kind,int strength,int fastPeriod,int middle,int slowPeriod,int band,double multiplier,bool selected=false,ICollection<Signal>? signals=null)
    {
        strength=Math.Max(1,strength);fastPeriod=Math.Max(1,fastPeriod);middle=Math.Max(1,middle);slowPeriod=Math.Max(1,slowPeriod);band=Math.Max(1,band);R D(double value)=>R.FromDouble(value);R Round(R value)=>value.RoundExtendedBinary64();
        var prices=bars.Select(b=>D(b.Close)).ToArray();var directions=prices.Select((v,i)=>v.CompareTo(i==0?new R(0):prices[i-1])).ToArray();var advances=prices.Select((_,i)=>directions.Skip(Math.Max(0,i-fastPeriod+1)).Take(Math.Min(i+1,fastPeriod)).Count(v=>v>0)).ToArray();var declines=prices.Select((_,i)=>directions.Skip(Math.Max(0,i-fastPeriod+1)).Take(Math.Min(i+1,fastPeriod)).Count(v=>v<0)).ToArray();
        var net=prices.Select((_,i)=>advances[i]+declines[i]==0?new R(0):Round(new R(1000)*new R(advances[i]-declines[i])/new R(advances[i]+declines[i]))).ToArray();var fast=SmoothRocBankStage(net,fastPeriod,kind,Round);var slow=SmoothRocBankStage(net,slowPeriod,kind,Round);var center=SmoothRocBankStage(prices,band,kind,Round);
        var typical=bars.Select(b=>selected?D(b.Close):D(((D(b.High)+D(b.Low)+D(b.Close))/new R(3)).ToDouble())).ToArray();var positive=bars.Select((b,i)=>i>0&&typical[i].CompareTo(typical[i-1])>0?typical[i]*D(b.Volume):new R(0)).ToArray();var negative=bars.Select((b,i)=>i>0&&typical[i].CompareTo(typical[i-1])<0?typical[i]*D(b.Volume):new R(0)).ToArray();
        R Flow(int period,int i){var start=Math.Max(0,i-period+1);var p=positive.Skip(start).Take(i-start+1).Aggregate(new R(0),(a,v)=>a+v);var n=negative.Skip(start).Take(i-start+1).Aggregate(new R(0),(a,v)=>a+v);var total=p+n;return n.Sign==0?new R(100):p.Sign==0?new R(0):total.Sign==0?new R(p.Sign>0?100:0):D(Math.Max(0,Math.Min(100,(new R(100)*p/total).ToDouble())));}
        var blend=new R[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var position=new R(0);if(i+1>=band&&multiplier!=0){var window=prices.Skip(i-band+1).Take(band).ToArray();var mean=window.Aggregate(new R(0),(a,v)=>a+v)/new R(band);var variance=window.Aggregate(new R(0),(a,v)=>a+(v-mean)*(v-mean))/new R(band);if(variance.Sign>0)position=UltimateBandOffset(new R(50)*(prices[i]-center[i])/D(multiplier),variance);}
            var ratio=declines[i]==0?new R(0):new R(100)*new R(advances[i])/new R(declines[i]);blend[i]=Round(new R(200)*position+ratio+new R(2)*(fast[i]-slow[i])+new R(3)*Flow(fastPeriod,i)+new R(3)*Flow(middle,i)+D(1.5)*Flow(slowPeriod,i));
            if(i>0){var scale=blend[i].Abs().CompareTo(blend[i-1].Abs())>0?blend[i].Abs():blend[i-1].Abs();if((blend[i]-blend[i-1]).Abs().CompareTo(D(1.4210854715202004e-14)*scale)<=0)blend[i]=blend[i-1];}
        }
        var changes=blend.Select((v,i)=>i==0?new R(0):v-blend[i-1]).ToArray();var gains=SmoothRocBankStage(changes.Select(v=>v.Sign>0?Round(v):new R(0)).ToArray(),strength,kind,Round);var losses=SmoothRocBankStage(changes.Select(v=>v.Sign<0?Round(new R(0)-v):new R(0)).ToArray(),strength,kind,Round);var rsi=new R[bars.Count];
        for(var i=0;i<bars.Count;i++){rsi[i]=losses[i].Sign==0?new R(100):gains[i].Sign==0?new R(0):D((new R(100)*gains[i]/(gains[i]+losses[i])).ToDouble());if(i>0&&strength>1&&kind is 3 or 6&&changes[i].Sign==0)rsi[i]=rsi[i-1];}
        var output=SmoothRocBankStage(rsi,strength,kind,Round);for(var i=0;i<bars.Count;i++){var slope=output[i]-(i==0?new R(0):output[i-1]);var old=i==0?new R(0):output[i-1]-(i<2?new R(0):output[i-2]);signals?.Add(slope.Sign>0&&slope.CompareTo(old)>0?Signal.StrongBuy:slope.Sign<0&&slope.CompareTo(old)<0?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None);}
        return Outputs(("Utm",output.Select(v=>v.ToDouble()).ToArray()));
    }
}
