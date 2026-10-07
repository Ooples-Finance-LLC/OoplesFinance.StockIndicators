using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> RatioOchlOutputs(IReadOnlyList<Bar> bars)
    {
        var previous=new ReferenceFraction(0);var zero=new ReferenceFraction(0);var output=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var b=bars[i];var price=ReferenceFraction.FromDouble(b.Close);var distance=price-ReferenceFraction.FromDouble(b.Open);if(distance.Sign<0)distance=zero-distance;
            var range=ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low);
            var gain=ReferenceFraction.FromDouble(range.Sign==0?0:Math.Min(1,(distance/range).ToDouble()));
            if(i==0)previous=price;
            output[i]=(previous+gain*(price-previous)).ToDouble();previous=ReferenceFraction.FromDouble(output[i]);
        }
        return Outputs(("Rochla",output));
    }
}
