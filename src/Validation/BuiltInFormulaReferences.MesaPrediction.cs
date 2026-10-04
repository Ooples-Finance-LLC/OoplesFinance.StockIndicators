using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MesaPredictionFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not EhlersMesaPredictIndicatorV1SpecOptions options) return null;
        return new("Predict", new[] { "Ssf", "Predict", "PrePredict" }, bars => MesaPredictionValues(bars,options));
    }
    internal static Dictionary<string,double[]> MesaPredictionValues(IReadOnlyList<Bar> bars, EhlersMesaPredictIndicatorV1SpecOptions options)
    {
        var window=Math.Max(2,options.UpperLength); var orderLimit=Math.Min(Math.Max(1,options.Length2),window-1);
        var smooth=Math.Max(1,options.LowerLength); var horizon=Math.Max(1,options.Length1);
        var zero=new ReferenceFraction(0); var one=new ReferenceFraction(1); var two=new ReferenceFraction(2);
        ReferenceFraction R(ReferenceFraction v)=>RoundRocBankStage(v);
        ReferenceFraction D(double v)=>ReferenceFraction.FromDouble(v);
        ReferenceFraction At(IReadOnlyList<ReferenceFraction> a,int j)=>j<0||j>=a.Count?zero:a[j];
        ReferenceFraction Sparse(Dictionary<int,ReferenceFraction> a,int j)=>a.TryGetValue(j,out var v)?v:zero;
        var highAngle=Math.Sqrt(2)*Math.PI/window;var highPole=Math.Exp(-highAngle);
        var h2=2*highPole*Math.Cos(highAngle);var h3=-highPole*highPole;var h1=(1+h2-h3)/4;
        var lowAngle=Math.Sqrt(2)*Math.PI/smooth;var lowPole=Math.Exp(-lowAngle);
        var l2=2*lowPole*Math.Cos(lowAngle);var l3=-lowPole*lowPole;var l1=1-l2-l3;
        var prices=bars.Select(b=>D(b.Close)).ToArray();var high=new ReferenceFraction[bars.Count];var ssf=new ReferenceFraction[bars.Count];
        var pre=new ReferenceFraction[bars.Count];var predicted=new ReferenceFraction[bars.Count];var coefficients=new List<ReferenceFraction[]>();
        ReferenceFraction? mass=null;
        ReferenceFraction Weight(int lag)=>D(1-Math.Cos(2*Math.PI*(lag+1d)/(smooth+1d)));
        for(var i=0;i<bars.Count;i++)
        {
            high[i]=i<4?zero:R(R(R(D(h1)*R(R(prices[i]-At(prices,i-1))-R(At(prices,i-1)-At(prices,i-2))))+R(D(h2)*At(high,i-1)))+R(D(h3)*At(high,i-2)));
            ssf[i]=R(R(R(R(D(l1)*R(high[i]+At(high,i-1)))/two)+R(D(l2)*At(ssf,i-1)))+R(D(l3)*At(ssf,i-2)));
            var sample=ssf.Skip(Math.Max(0,i-window+1)).Take(Math.Min(window,i+1)).ToArray();
            var scale=sample.Select(v=>v.Abs()).Aggregate(zero,(a,b)=>a.CompareTo(b)>0?a:b);
            var priceScale=prices[i].Abs().CompareTo(At(prices,i-1).Abs())>0?prices[i].Abs():At(prices,i-1).Abs();
            var polynomial=new[]{one};
            if(scale.CompareTo(D(64*2.2204460492503131e-16)*priceScale)>0)
            {
                var forward=new Dictionary<int,ReferenceFraction>();var backward=new Dictionary<int,ReferenceFraction>();
                for(var j=0;j<sample.Length;j++)
                {
                    var position=window-sample.Length+j;var value=R(sample[j]/scale);if(value.Sign==0)continue;
                    if(position>0)forward[position-1]=value;if(position<window-1)backward[position]=value;
                }
                for(var order=1;order<=orderLimit;order++)
                {
                    if(forward.Count==0||backward.Count==0)break;
                    var cross=forward.Aggregate(zero,(sum,p)=>sum+p.Value*Sparse(backward,p.Key));
                    var energy=forward.Values.Concat(backward.Values).Aggregate(zero,(sum,v)=>sum+v*v);
                    var reflection=energy.Sign==0?zero:R(two*cross/energy);
                    if(reflection.CompareTo(one)>0)reflection=one;if(reflection.CompareTo(zero-one)<0)reflection=zero-one;
                    var expanded=polynomial.Concat(new[]{zero}).ToArray();
                    polynomial=expanded.Select((v,k)=>R(v-R(reflection*expanded[order-k]))).ToArray();
                    var keys=forward.Keys.Concat(backward.Keys).Distinct().ToArray();var nextForward=new Dictionary<int,ReferenceFraction>();var nextBackward=new Dictionary<int,ReferenceFraction>();
                    foreach(var k in keys)
                    {
                        if(k>0&&k<window-order){var v=R(Sparse(forward,k)-R(reflection*Sparse(backward,k)));if(v.Sign!=0)nextForward[k-1]=v;}
                        if(k<window-order-1){var v=R(Sparse(backward,k)-R(reflection*Sparse(forward,k)));if(v.Sign!=0)nextBackward[k]=v;}
                    }
                    forward=nextForward;backward=nextBackward;
                }
            }
            var fitted=polynomial.Skip(1).Select(v=>zero-v).ToArray();coefficients.Add(fitted);
            var recent=coefficients.Skip(Math.Max(0,coefficients.Count-smooth)).Reverse().ToArray();
            var count=recent.Max(v=>v.Length);var averaged=new ReferenceFraction[count];
            if(count>0&&mass is null){var sum=zero;for(var lag=0;lag<smooth;lag++)sum+=Weight(lag);mass=sum;}
            for(var k=0;k<count;k++){var sum=zero;for(var lag=0;lag<recent.Length;lag++)sum+=At(recent[lag],k)*Weight(lag);averaged[k]=R(sum/mass!.Value);}
            var forecast=zero;
            if(averaged.Any(v=>v.Sign!=0))
            {
                var path=sample.ToList();
                for(var step=0;step<horizon;step++)
                {var sum=zero;for(var k=0;k<count;k++)sum+=averaged[k]*At(path,path.Count-1-k);forecast=R(sum);path.Add(forecast);}
            }
            pre[i]=forecast;predicted[i]=R((forecast+At(pre,i-1))/two);
        }
        return new(){["Ssf"]=ssf.Select(v=>v.ToDouble()).ToArray(),["Predict"]=predicted.Select(v=>v.ToDouble()).ToArray(),["PrePredict"]=pre.Select(v=>v.ToDouble()).ToArray()};
    }
}
