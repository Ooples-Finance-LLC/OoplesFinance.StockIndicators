using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ZDistanceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return new Dictionary<string, double[]> { ["Zscore"] = ZDistanceValues(bars,
            (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!, Integer(options, "Length", 20)) };
    }
    internal static double[] ZDistanceValues(IReadOnlyList<Bar> bars, MovingAvgType kind = MovingAvgType.VolumeWeightedAveragePrice, int length = 20)
        => ZDistanceRootReferences(bars,kind,length).Select(v=>v.Sign==0?0d:v.Sign*v.Square.SqrtToDouble()).ToArray();
    private static (ReferenceFraction Square,int Sign)[] ZDistanceRootReferences(IReadOnlyList<Bar> bars, MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var code = AverageKind(new { MaType = kind }, 3);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, code);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = kind == MovingAvgType.VolumeWeightedAveragePrice ? prices.Select((price, i) =>
        {
            // Translate the window by the current price, independently of production's product sum.
            var offset = R(0); var volume = R(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            { var weight = R(bars[j].Volume); offset += weight * (prices[j] - price); volume += weight; }
            return volume.Sign == 0 ? R(0) : price + offset / volume;
        }).ToArray() : Mean(prices, length);
        var residuals = prices.Select((value, i) => value - means[i]).ToArray();
        return residuals.Select((residual, i) =>
        {
            if (i + 1 < length) return (R(0),0);
            var sum = R(0);
            for (var j = i - length + 1; j <= i; j++) sum += residuals[j] * residuals[j];
            return sum.Sign == 0 ? (R(0),0) : (residual * residual * R(length) / sum,residual.Sign);
        }).ToArray();
    }
    internal static int ZDistanceReferenceSign(params (ReferenceFraction Square,int Sign)[] terms)
    {
        // Partition by sign, then isolate the singleton radical. This reference
        // never evaluates the production pair-first squared-difference formula.
        var nonzero=terms.Where(v=>v.Sign!=0&&v.Square.Sign!=0).ToArray();
        var positive=nonzero.Where(v=>v.Sign>0).ToArray();var negative=nonzero.Where(v=>v.Sign<0).ToArray();
        if(negative.Length==0)return positive.Length==0?0:1;
        if(positive.Length==0)return -1;
        if(nonzero.Length==2)return positive[0].Square.CompareTo(negative[0].Square);
        var majority=positive.Length==2?positive:negative;var singleton=positive.Length==2?negative[0]:positive[0];var sign=positive.Length==2?1:-1;
        var delta=singleton.Square-majority[0].Square-majority[1].Square;
        if(delta.Sign<=0)return sign;
        var comparison=(delta*delta-new ReferenceFraction(4)*majority[0].Square*majority[1].Square).Sign;
        return -sign*comparison;
    }
    internal static Signal[] ZDistanceSignals(IReadOnlyList<Bar> bars,MovingAvgType kind=MovingAvgType.VolumeWeightedAveragePrice,int length=20)
    {
        var roots=ZDistanceRootReferences(bars,kind,length);var zero=(new ReferenceFraction(0),0);
        return roots.Select((current,i)=>
        {
            var previous=i==0?zero:roots[i-1];var older=i<2?zero:roots[i-2];
            var direction=ZDistanceReferenceSign(current,(previous.Item1,-previous.Item2));
            var change=ZDistanceReferenceSign(current,(previous.Item1*new ReferenceFraction(4),-previous.Item2),older);
            if(direction==0)return Signal.None;
            if(direction>0)return change>0?Signal.StrongBuy:Signal.Buy;
            return change<0?Signal.StrongSell:Signal.Sell;
        }).ToArray();
    }

}
