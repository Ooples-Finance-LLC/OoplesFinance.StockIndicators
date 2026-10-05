using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> VervoortVolatilityOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return VervoortVolatilityOutputs(bars,AverageKind(o,3),Integer(o,"Length1",8),Integer(o,"Length2",13),Number(o,3.55,"DevMult"),Number(o,.9,"LowBandMult"));}
    internal static IReadOnlyDictionary<string,double[]> VervoortVolatilityOutputs(IReadOnlyList<Bar> bars,int kind,int first,int second,double multiplier,double lowerMultiplier,ICollection<Signal>? signals=null)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value)=>value.RoundExtendedBinary64();
        var input=bars.Select(b=>R(b.Close)).ToArray();var mean=SmoothRocBankStage(input,first,kind,Round);var center=SmoothRocBankStage(mean,first,kind,Round);
        ReferenceFraction[] Observed(ReferenceFraction[] values,int period)=>values.Select((_,i)=>Round(values.Skip(Math.Max(0,i-period+1)).Take(Math.Min(i+1,period)).Aggregate(R(0),(sum,v)=>sum+v)/R(Math.Min(i+1,period)))).ToArray();
        var middle=Observed(mean,first);var range=bars.Select((b,i)=>Round(b.Close>=(i==0?0:bars[i-1].Close)?R(b.Close)-R(i==0?0:bars[i-1].Low):R(i==0?0:bars[i-1].Close)-R(b.Low))).ToArray();
        var width=SmoothRocBankStage(Observed(range,second).Select(v=>Round(R(multiplier)*v)).ToArray(),first,kind,Round);
        var upper=center.Select((v,i)=>Round(v+width[i])).ToArray();var lower=center.Select((v,i)=>Round(v-Round(R(lowerMultiplier)*width[i]))).ToArray();
        var previousSlope=R(0);
        for(var i=0;i<bars.Count;i++)
        {
            var slope=input[i]-middle[i];var previous=i==0?R(0):input[i-1];var previousUpper=i==0?R(0):upper[i-1];var previousLower=i==0?R(0):lower[i-1];
            signals?.Add(slope.Sign>0 && slope.CompareTo(previousSlope)>0?Signal.StrongBuy:slope.Sign<0 && slope.CompareTo(previousSlope)<0?Signal.StrongSell
                :slope.Sign>0 || previous.CompareTo(previousLower)<0 && input[i].CompareTo(lower[i])>0?Signal.Buy
                :slope.Sign<0 || previous.CompareTo(previousUpper)>0 && input[i].CompareTo(upper[i])<0?Signal.Sell:Signal.None);
            previousSlope=slope;
        }
        return Outputs(("UpperBand",upper.Select(v=>v.ToDouble()).ToArray()),("MiddleBand",middle.Select(v=>v.ToDouble()).ToArray()),("LowerBand",lower.Select(v=>v.ToDouble()).ToArray()));
    }
}
