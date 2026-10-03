using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> DemarkRangeOutputs(IReadOnlyList<Bar> bars,int length)
    {
        double At(int index,Func<Bar,double> select)=>index<0?0:select(bars[index]);
        var changes=new ReferenceFraction[bars.Count];var included=new ReferenceFraction[bars.Count];var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var bar=bars[i];var high=RoundRocBankStage(ReferenceFraction.FromDouble(bar.High)-ReferenceFraction.FromDouble(At(i-2,b=>b.High)));
            var low=RoundRocBankStage(ReferenceFraction.FromDouble(bar.Low)-ReferenceFraction.FromDouble(At(i-2,b=>b.Low)));changes[i]=RoundRocBankStage(high+low);
            var overlap=bar.High>=Math.Min(At(i-5,b=>b.Low),At(i-6,b=>b.Low))&&bar.Low<=Math.Max(At(i-5,b=>b.High),At(i-6,b=>b.High));
            var previousOverlap=At(i-2,b=>b.High)>=At(i-8,b=>b.Close)&&At(i-2,b=>b.Low)<=Math.Max(At(i-7,b=>b.Close),At(i-8,b=>b.Close));
            included[i]=overlap||previousOverlap?new ReferenceFraction(0):changes[i];
            var numerator=new ReferenceFraction(0);var denominator=new ReferenceFraction(0);
            for(var j=Math.Max(0,i-Math.Max(1,length)+1);j<=i;j++){numerator+=included[j];denominator+=changes[j].Abs();}
            result[i]=denominator.Sign==0?0:(numerator*new ReferenceFraction(100)/denominator).ToDouble();
        }
        return Outputs(("Drei",result));
    }
}
