using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> FibonacciPivotOutputs(IReadOnlyList<Bar> bars)
    {
        var keys=new[]{"Pivot","S1","S2","S3","R1","R2","R3","M1","M2","M3","M4","M5","M6"};var result=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        var groups=bars.Select((bar,index)=>(bar,index)).GroupBy(x=>x.bar.Time.Date).ToArray();
        for(var g=1;g<groups.Length;g++)
        {
            var prior=groups[g-1].Select(x=>x.bar).ToArray();var h=ReferenceFraction.FromDouble(prior.Max(b=>b.High));var l=ReferenceFraction.FromDouble(prior.Min(b=>b.Low));var c=ReferenceFraction.FromDouble(prior[prior.Length-1].Close);
            var p=RoundRocBankStage((h+l+c)/new ReferenceFraction(3));var range=RoundRocBankStage(h-l);
            var widths=new[]{0.382,0.61803398874989484820458683436,1d}.Select(f=>RoundRocBankStage(range*ReferenceFraction.FromDouble(f))).ToArray();
            var support=widths.Select(w=>RoundRocBankStage(p-w)).ToArray();var resistance=widths.Select(w=>RoundRocBankStage(p+w)).ToArray();
            var pairs=new[]{(support[2],support[1]),(support[1],support[0]),(support[0],p),(resistance[0],p),(resistance[1],resistance[0]),(resistance[2],resistance[1])};
            var values=new[]{p}.Concat(support).Concat(resistance).Concat(pairs.Select(v=>RoundRocBankStage((v.Item1+v.Item2)/new ReferenceFraction(2)))).ToArray();
            foreach(var item in groups[g])for(var slot=0;slot<keys.Length;slot++)result[keys[slot]][item.index]=values[slot].ToDouble();
        }
        return result;
    }
}
