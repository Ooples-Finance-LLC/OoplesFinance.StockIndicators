using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> DemarkPivotOutputs(IReadOnlyList<Bar> bars)
    {
        var keys=new[]{"Pivot","S1","R1"};var result=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        var groups=bars.Select((bar,index)=>(bar,index)).GroupBy(x=>x.bar.Time.Date).ToArray();
        for(var g=1;g<groups.Length;g++)
        {
            var prior=groups[g-1].Select(x=>x.bar).ToArray();var h=ReferenceFraction.FromDouble(prior.Max(b=>b.High));var l=ReferenceFraction.FromDouble(prior.Min(b=>b.Low));var c=ReferenceFraction.FromDouble(prior[prior.Length-1].Close);var o=ReferenceFraction.FromDouble(prior[0].Open);var two=new ReferenceFraction(2);
            var total=c.CompareTo(o)<0?h+two*l+c:c.CompareTo(o)>0?two*h+l+c:h+l+two*c;
            var pivot=RoundRocBankStage(total/new ReferenceFraction(4));var half=RoundRocBankStage(total/two);
            var values=new[]{pivot,RoundRocBankStage(half-h),RoundRocBankStage(half-l)};
            foreach(var item in groups[g])for(var slot=0;slot<keys.Length;slot++)result[keys[slot]][item.index]=values[slot].ToDouble();
        }
        return result;
    }
}
