using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> ChaikinFlowOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)=>ChaikinFlowOutputs(bars,Integer(indicator.CreateOptions(),"Length",20));
    internal static IReadOnlyDictionary<string,double[]> ChaikinFlowOutputs(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var zero=new ReferenceFraction(0);
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        var flows=bars.Select(b=>{var high=ReferenceFraction.FromDouble(b.High);var low=ReferenceFraction.FromDouble(b.Low);return b.High==b.Low?zero:RoundRocBankStage((new ReferenceFraction(2)*ReferenceFraction.FromDouble(b.Close)-high-low)*ReferenceFraction.FromDouble(b.Volume)/(high-low));}).ToArray();
#pragma warning restore S1244
        return Outputs(("Cmf",bars.Select((b,i)=>{var flow=zero;var volume=zero;for(var j=Math.Max(0,i-length+1);j<=i;j++){flow+=flows[j];volume+=ReferenceFraction.FromDouble(bars[j].Volume);}return volume.Sign==0?0:(flow/volume).ToDouble();}).ToArray()));
    }
}
