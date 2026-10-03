using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DistanceCoefficientValues(IReadOnlyList<Bar> bars,int length)
    {
        length=Math.Max(1,length);var prices=bars.Select(b=>ReferenceFraction.FromDouble(b.Close)).ToArray();var weights=new ReferenceFraction[bars.Count];var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var present=Math.Min(length-1,i);var weight=prices[i]*prices[i]*new ReferenceFraction(length-1L-present);
            for(var lag=1;lag<=present;lag++){var difference=prices[i]-prices[i-lag];weight+=difference*difference;}
            weights[i]=weight;var mass=new ReferenceFraction(0);var sum=new ReferenceFraction(0);
            for(var j=Math.Max(0,i-length+1);j<=i;j++){mass+=weights[j];sum+=weights[j]*prices[j];}
            result[i]=mass.Sign==0?bars[i].Close:(sum/mass).ToDouble();
        }
        return result;
    }
}
