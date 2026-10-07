using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> RetrospectiveCandleOutputs(IReadOnlyList<Bar> bars,int length)
    {
        var changes=bars.Select((b,i)=>RoundRocBankStage(ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(i==0?0:bars[i-1].Close)).Abs()).ToArray();
        var result=new double[bars.Count];var previousClose=0d;
        for(var i=0;i<bars.Count;i++)
        {
            var sample=changes.Skip(Math.Max(0,i-Math.Max(1,length)+1)).Take(Math.Min(i+1,Math.Max(1,length))).ToArray();var lowest=sample.Min();var highest=sample.Max();
            var ratio=highest.CompareTo(lowest)==0?0:((changes[i]-lowest)/(highest-lowest)).ToDouble();var weight=ReferenceFraction.FromDouble((ratio*100)/100);
            double Blend(double current,double previous)=>(ReferenceFraction.FromDouble(previous)+weight*(ReferenceFraction.FromDouble(current)-ReferenceFraction.FromDouble(previous))).ToDouble();
            var b=bars[i];var c=Blend(b.Close,i==0?b.Close:previousClose);var h=Blend(b.High,i==0?b.High:previousClose);var l=Blend(b.Low,i==0?b.Low:previousClose);var o=Blend(b.Open,i==0?b.Open:previousClose);
            result[i]=ExactPriceMean(c,h,l,o);previousClose=c;
        }
        return Outputs(("Rcc",result));
    }
}
