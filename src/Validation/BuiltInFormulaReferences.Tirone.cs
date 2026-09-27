using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> TironeOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var keys=new[]{"Tlh","Clh","Blh","Am","Eh","El","Rh","Rl"};var output=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        for(var i=0;i<bars.Count;i++)
        {
            var sample=bars.Skip(Math.Max(0,i-length+1)).Take(Math.Min(i+1,length)).ToArray();
            var h=ReferenceFraction.FromDouble(sample.Max(b=>b.High));var l=ReferenceFraction.FromDouble(sample.Min(b=>b.Low));var c=ReferenceFraction.FromDouble(bars[i].Close);
            var range=RoundRocBankStage(h-l);var third=RoundRocBankStage(range/new ReferenceFraction(3));var half=RoundRocBankStage(range/new ReferenceFraction(2));var mean=RoundRocBankStage((h+l+c)/new ReferenceFraction(3));
            var values=new[]{h-third,l+half,l+third,mean,mean+range,mean-range,new ReferenceFraction(2)*mean-l,new ReferenceFraction(2)*mean-h};
            for(var j=0;j<keys.Length;j++)output[keys[j]][i]=RoundRocBankStage(values[j]).ToDouble();
        }
        return output;
    }
}
