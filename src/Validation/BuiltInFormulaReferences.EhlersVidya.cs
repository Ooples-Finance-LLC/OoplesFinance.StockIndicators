using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> EhlersVidyaOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
        => EhlersVidyaOutputs(bars,AverageKind(indicator.CreateOptions(),2),9,30);
    internal static IReadOnlyDictionary<string,double[]> EhlersVidyaOutputs(IReadOnlyList<Bar> bars,int kind,int fast,int slow,ICollection<Signal>? signals=null)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);
        var input=bars.Select(b=>R(b.Close)).ToArray();
        var fastMean=SmoothRocBankStage(input,fast,kind,v=>R(v.ToDouble()));var slowMean=SmoothRocBankStage(input,slow,kind,v=>R(v.ToDouble()));
        var fastSquare=input.Select((v,i)=>(v-fastMean[i])*(v-fastMean[i])).ToArray();var slowSquare=input.Select((v,i)=>(v-slowMean[i])*(v-slowMean[i])).ToArray();
        ReferenceFraction Energy(ReferenceFraction[] values,int i,int period)
        {var first=Math.Max(0,i-period+1);return values.Skip(first).Take(i-first+1).Aggregate(R(0),(sum,v)=>sum+v)/R(i-first+1);}
        var result=new double[bars.Count];var previousSlope=R(0);
        for(var i=0;i<bars.Count;i++)
        {
            var denominator=Energy(slowSquare,i,slow);var gain=0d;
            if(denominator.Sign!=0)
            {
                var squared=R(.2)*R(.2)*Energy(fastSquare,i,fast)/denominator;
                gain=squared.CompareTo(R(.01)*R(.01))<=0?.01:squared.CompareTo(R(.99)*R(.99))>=0?.99:squared.SqrtToDouble();
            }
            var previous=i==0?input[i]:R(result[i-1]);result[i]=(previous+R(gain)*(input[i]-previous)).ToDouble();
            var slope=input[i]-R(result[i]);signals?.Add(slope.Sign>0?slope.CompareTo(previousSlope)>0?Signal.StrongBuy:Signal.Buy:slope.Sign<0?slope.CompareTo(previousSlope)<0?Signal.StrongSell:Signal.Sell:Signal.None);previousSlope=slope;
        }
        return Outputs(("Evidya",result));
    }
}
