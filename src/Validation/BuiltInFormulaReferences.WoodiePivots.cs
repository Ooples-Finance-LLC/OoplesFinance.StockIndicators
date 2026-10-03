using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> WoodiePivotOutputs(IReadOnlyList<Bar> bars)
    {
        var keys=new[]{"Pivot","S1","S2","S3","S4","R1","R2","R3","R4","M1","M2","M3","M4"};
        var result=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        var groups=bars.Select((bar,index)=>(bar,index)).GroupBy(x=>x.bar.Time.Date).ToArray();
        for(var g=1;g<groups.Length;g++)
        {
            var prior=groups[g-1].Select(x=>x.bar).ToArray();var h=ReferenceFraction.FromDouble(prior.Max(b=>b.High));var l=ReferenceFraction.FromDouble(prior.Min(b=>b.Low));var c=ReferenceFraction.FromDouble(prior[prior.Length-1].Close);var two=new ReferenceFraction(2);
            var p=RoundRocBankStage((h+l+two*c)/new ReferenceFraction(4));
            var s1=RoundRocBankStage(two*p-h);var r1=RoundRocBankStage(two*p-l);
            var range=RoundRocBankStage(h-l);var s2=RoundRocBankStage(p-range);var r2=RoundRocBankStage(p+range);
            var s3=RoundRocBankStage(l-two*RoundRocBankStage(h-p));var r3=RoundRocBankStage(h+two*RoundRocBankStage(p-l));
            var s4=RoundRocBankStage(s3-range);var r4=RoundRocBankStage(r3+range);
            var pairs=new[]{(s1,s2),(p,s1),(r1,p),(r1,r2)};
            var values=new[]{p,s1,s2,s3,s4,r1,r2,r3,r4}.Concat(pairs.Select(v=>RoundRocBankStage((v.Item1+v.Item2)/two))).ToArray();
            foreach(var item in groups[g])for(var slot=0;slot<keys.Length;slot++)result[keys[slot]][item.index]=values[slot].ToDouble();
        }
        return result;
    }
}
