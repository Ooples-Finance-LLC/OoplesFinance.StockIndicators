using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> ProjectedLevelsOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var keys=new[]{"Support1","Support2","Resistance1","Resistance2","MiddleBand"};var output=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        for(var i=0;i<bars.Count;i++)
        {
            var sample=bars.Skip(Math.Max(0,i-length+1)).Take(Math.Min(i+1,length)).ToArray();var h=ReferenceFraction.FromDouble(sample.Max(b=>b.High));var l=ReferenceFraction.FromDouble(sample.Min(b=>b.Low));
            var range=RoundRocBankStage(h-l);var quarter=RoundRocBankStage(range/new ReferenceFraction(4));var half=RoundRocBankStage(range/new ReferenceFraction(2));
            var levels=new[]{l-quarter,l-half,h+quarter,h+half}.Select(RoundRocBankStage).ToArray();var total=levels.Aggregate(new ReferenceFraction(0),(sum,v)=>sum+v);
            var values=levels.Concat(new[]{RoundRocBankStage(total/new ReferenceFraction(4))}).ToArray();for(var j=0;j<keys.Length;j++)output[keys[j]][i]=values[j].ToDouble();
        }
        return output;
    }
}
