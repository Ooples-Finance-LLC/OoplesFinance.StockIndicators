using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> VolumeFlowOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator,bool selected=false)
    {var o=indicator.CreateOptions();return VolumeFlowOutputs(bars,AverageKind(o,1),Integer(o,"Length1",130),Integer(o,"Length2",30),Integer(o,"SignalLength",5),Integer(o,"SmoothLength",3),Number(o,.2,"Coef"),Number(o,2.5,"Vcoef"),selected:selected);}
    internal static IReadOnlyDictionary<string,double[]> VolumeFlowOutputs(IReadOnlyList<Bar> bars,int kind,int flow,int variance,int signal,int smooth,double coefficient,double volumeCoefficient,ICollection<Signal>? signals=null,bool selected=false)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);ReferenceFraction Round(ReferenceFraction value)=>value.RoundExtendedBinary64();
        var prices=bars.Select(b=>selected?b.Close:ExactPriceMean(b.High,b.Low,b.Close)).ToArray();
        var returns=prices.Select((p,i)=>R(i==0||p<=0||prices[i-1]<=0?0:KasePeakV2LogReference(p,prices[i-1]))).ToArray();
        var averages=SmoothRocBankStage(bars.Select(b=>R(b.Volume)).ToArray(),flow,kind,Round);var flows=new ReferenceFraction[bars.Count];var normalized=new ReferenceFraction[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var sample=returns.Skip(Math.Max(0,i-variance+1)).Take(Math.Min(variance,i+1)).ToArray();var mean=sample.Aggregate(R(0),(sum,v)=>sum+v)/R(sample.Length);
            var spread=i+1<variance?R(0):sample.Aggregate(R(0),(sum,v)=>sum+(v-mean)*(v-mean))/R(sample.Length);var factor=R(bars[i].Close)*R(coefficient);var change=i==0?R(0):R(prices[i])-R(prices[i-1]);
            int Compare(ReferenceFraction k)
            {
                if(spread.Sign==0||k.Sign==0)return (R(0)-change).Sign;
                if(k.Sign!=change.Sign)return k.Sign.CompareTo(change.Sign);
                return k.Sign*(k*k*spread).CompareTo(change*change);
            }
            var cap=Round((i==0?R(0):averages[i-1])*R(volumeCoefficient));var volume=R(bars[i].Volume);var capped=cap.CompareTo(volume)<0?cap:volume;
            flows[i]=Compare(factor)<0?capped:Compare(R(0)-factor)>0?R(0)-capped:R(0);
            var total=flows.Skip(Math.Max(0,i-flow+1)).Take(Math.Min(flow,i+1)).Aggregate(R(0),(sum,v)=>sum+v);normalized[i]=averages[i].Sign==0?R(0):Round(total/averages[i]);
        }
        var line=SmoothRocBankStage(normalized,smooth,kind,Round);var signalLine=SmoothRocBankStage(line,signal,3,Round);var histogram=line.Select((v,i)=>Round(v-signalLine[i])).ToArray();
        for(var i=0;i<bars.Count;i++){var d=histogram[i];var old=i==0?R(0):histogram[i-1];signals?.Add(d.Sign>0&&d.CompareTo(old)>0?Signal.StrongBuy:d.Sign<0&&d.CompareTo(old)<0?Signal.StrongSell:d.Sign>0?Signal.Buy:d.Sign<0?Signal.Sell:Signal.None);}
        return Outputs(("Vfi",line.Select(v=>v.ToDouble()).ToArray()),("Signal",signalLine.Select(v=>v.ToDouble()).ToArray()),("Histogram",histogram.Select(v=>v.ToDouble()).ToArray()));
    }
}
