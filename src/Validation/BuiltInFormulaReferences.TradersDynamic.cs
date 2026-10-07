using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> TradersDynamicOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {
        var o=indicator.CreateOptions();return TradersDynamicOutputs(bars,AverageKind(o,1),Integer(o,"Length1",13),Integer(o,"Length2",34),Integer(o,"Length3",2),Integer(o,"Length4",7));
    }
    internal static IReadOnlyDictionary<string,double[]> TradersDynamicOutputs(IReadOnlyList<Bar> bars,int kind,int l1,int l2,int l3,int l4,ICollection<Signal>? signals=null)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value)=>R(value.ToDouble());
        var strength=RoundedPriceRsi(bars,l1,kind);var input=strength.Select(R).ToArray();
        var center=SmoothStrengthStage(input,l2,kind).Select(v=>v.ToDouble()).ToArray();
        var fast=SmoothStrengthStage(input,l3,kind).Select(v=>v.ToDouble()).ToArray();
        var slow=SmoothStrengthStage(input,l4,kind).Select(v=>v.ToDouble()).ToArray();
        var upper=new double[bars.Count];var middle=new double[bars.Count];var lower=new double[bars.Count];var previousSlope=R(0);
        for(var i=0;i<bars.Count;i++)
        {
            double deviation=0;
            if(i+1>=l2)
            {
                var sample=input.Skip(i-l2+1).Take(l2).ToArray();
                var offsets=sample.Select(v=>Round(v-sample[0])).ToArray();
                var mean=Round(offsets.Aggregate(R(0),(sum,value)=>Round(sum+value))/R(l2));
                var residuals=offsets.Select(v=>Round(v-mean)).ToArray();var squares=residuals.Select(v=>Round(v*v)).ToArray();
                var variance=Round(squares.Aggregate(R(0),(sum,value)=>Round(sum+value))/R(l2));
                var lost=residuals.Where((v,j)=>v.Sign!=0 && squares[j].CompareTo(R(2.2250738585072014E-308))<0).Any();
                if(lost || variance.Sign>0 && variance.CompareTo(R(2.2250738585072014E-308))<0)
                {
                    var exactMean=sample.Aggregate(R(0),(sum,value)=>sum+value)/R(l2);
                    variance=sample.Aggregate(R(0),(sum,value)=>sum+(value-exactMean)*(value-exactMean))/R(l2);
                }
                deviation=variance.SqrtToDouble();
            }
            var width=Round(R(1.6185)*R(deviation));upper[i]=Round(R(center[i])+width).ToDouble();lower[i]=Round(R(center[i])-width).ToDouble();
            middle[i]=Round(Round(R(upper[i])+R(lower[i]))/R(2)).ToDouble();
            var slope=R(fast[i])-R(slow[i]);var previous=i==0?0:fast[i-1];var previousUpper=i==0?0:upper[i-1];var previousLower=i==0?0:lower[i-1];
            signals?.Add(slope.Sign>0 && slope.CompareTo(previousSlope)>0?Signal.StrongBuy:slope.Sign<0 && slope.CompareTo(previousSlope)<0?Signal.StrongSell
                :slope.Sign>0 || previous<previousLower && fast[i]>lower[i]?Signal.Buy:slope.Sign<0 || previous>previousUpper && fast[i]<upper[i]?Signal.Sell:Signal.None);
            previousSlope=slope;
        }
        return Outputs(("UpperBand",upper),("MiddleBand",middle),("LowerBand",lower),("Tdi",fast),("Signal",slow));
    }
}
