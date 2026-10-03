using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> FloorPivotOutputs(IReadOnlyList<Bar> bars)
    {
        var keys=new[]{"Pivot","S1","S2","S3","R1","R2","R3","M1","M2","M3","M4","M5","M6"};var result=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        var groups=bars.Select((bar,index)=>(bar,index)).GroupBy(x=>x.bar.Time.Date).ToArray();
        for(var g=1;g<groups.Length;g++)
        {
            var prior=groups[g-1].Select(x=>x.bar).ToArray();var h=ReferenceFraction.FromDouble(prior.Max(b=>b.High));var l=ReferenceFraction.FromDouble(prior.Min(b=>b.Low));var c=ReferenceFraction.FromDouble(prior[prior.Length-1].Close);
            var p=RoundRocBankStage((h+l+c)/new ReferenceFraction(3));var range=RoundRocBankStage(h-l);
            var s1=RoundRocBankStage(new ReferenceFraction(2)*p-h);var r1=RoundRocBankStage(new ReferenceFraction(2)*p-l);
            var s2=RoundRocBankStage(p-range);var r2=RoundRocBankStage(p+range);var s3=RoundRocBankStage(s1-range);var r3=RoundRocBankStage(r1+range);
            var pairs=new[]{(s3,s2),(s2,s1),(s1,p),(r1,p),(r2,r1),(r3,r2)};
            var values=new[]{p,s1,s2,s3,r1,r2,r3}.Concat(pairs.Select(v=>RoundRocBankStage((v.Item1+v.Item2)/new ReferenceFraction(2)))).ToArray();
            foreach(var item in groups[g])for(var slot=0;slot<keys.Length;slot++)result[keys[slot]][item.index]=values[slot].ToDouble();
        }
        return result;
    }
}
