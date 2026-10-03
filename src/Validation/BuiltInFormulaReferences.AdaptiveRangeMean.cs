using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> AdaptiveRangeMeanOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)=>AdaptiveRangeMeanOutputs(bars,Integer(indicator.CreateOptions(),"Length",14));
    internal static IReadOnlyDictionary<string,double[]> AdaptiveRangeMeanOutputs(IReadOnlyList<Bar> bars,int length,int fast=2,int slow=14)
    {
        var period=Math.Max(1,length)+1L;var fastAlpha=2d/(Math.Max(1,fast)+1L);var slowAlpha=2d/(Math.Max(1,slow)+1L);
        var zero=new ReferenceFraction(0);var previous=zero;var output=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var window=bars.Skip((int)Math.Max(0,i-period+1)).Take((int)Math.Min(i+1L,period)).ToArray();
            var highest=ReferenceFraction.FromDouble(window.Max(b=>b.High));var lowest=ReferenceFraction.FromDouble(window.Min(b=>b.Low));var price=ReferenceFraction.FromDouble(bars[i].Close);
            var distance=new ReferenceFraction(2)*price-highest-lowest;if(distance.Sign<0)distance=zero-distance;
            var multiplier=highest.CompareTo(lowest)==0?0:Math.Max(0,Math.Min(1,(distance/(highest-lowest)).ToDouble()));
            var delta=ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(fastAlpha)-ReferenceFraction.FromDouble(slowAlpha)).ToDouble());
            var product=ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(multiplier)*delta).ToDouble());
            var coefficient=ReferenceFraction.FromDouble((product+ReferenceFraction.FromDouble(slowAlpha)).ToDouble());
            var gain=ReferenceFraction.FromDouble((coefficient*coefficient).ToDouble());
            var value=(previous+gain*(price-previous)).ToDouble();output[i]=value;previous=ReferenceFraction.FromDouble(value);
        }
        return Outputs(("Ama",output));
    }
}
