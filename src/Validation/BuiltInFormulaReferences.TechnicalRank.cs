using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> TechnicalRankOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return TechnicalRankOutputs(bars, Enumerable.Range(1,9).Select(i => Math.Max(1,Integer(options,"Length"+i))).ToArray());
    }
    internal static IReadOnlyDictionary<string,double[]> TechnicalRankOutputs(IReadOnlyList<Bar> bars, int[] periods, ICollection<Signal>? signals = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var longMean = SmoothRocBankStage(prices,periods[0],1,Round);
        var mediumMean = SmoothRocBankStage(prices,periods[2],1,Round);
        var fast = SmoothRocBankStage(prices,periods[4],3,Round);
        var slow = SmoothRocBankStage(prices,periods[5],3,Round);
        ReferenceFraction Percent(ReferenceFraction value,ReferenceFraction basis) => basis.Sign == 0 ? R(0) : Round(R(100)*(value-basis)/basis);
        var ppo = prices.Select((_,i) => Percent(fast[i],slow[i])).ToArray();
        var smooth = SmoothRocBankStage(ppo,periods[6],3,Round);
        var histogram = ppo.Select((value,i) => Round(value-smooth[i])).ToArray();
        var strength = RoundedPriceRsi(bars,periods[8],6);
        var output = new double[bars.Count]; var previous = R(0); var previousSlope = R(0);
        for(var i=0;i<bars.Count;i++)
        {
            ReferenceFraction MeanTerm(ReferenceFraction mean,int weight) => mean.Sign == 0 ? R(0) : Round(Round(R(weight)*Round(prices[i]-mean))/mean);
            ReferenceFraction Return(int period) => i<period ? R(0) : Percent(prices[i],prices[i-period]);
            var slope = i<periods[7] ? R(0) : Round(Round(histogram[i]-histogram[i-periods[7]])/R(periods[7]));
            var terms = new[] { MeanTerm(longMean[i],30), Round(R(.3)*Return(periods[1])), MeanTerm(mediumMean[i],15),
                Round(R(.15)*Return(periods[3])), Round(R(5)*slope), Round(R(.05)*R(strength[i])) };
            var rank=terms[0]; foreach(var term in terms.Skip(1)) rank=Round(rank+term);
            output[i] = rank.Sign<0 ? 0 : rank.CompareTo(R(100))>0 ? 100 : rank.ToDouble();
            var value=R(output[i]); var change=value-previous;
            signals?.Add(change.Sign>0 ? change.CompareTo(previousSlope)>0 ? Signal.StrongBuy : Signal.Buy
                : change.Sign<0 ? change.CompareTo(previousSlope)<0 ? Signal.StrongSell : Signal.Sell : Signal.None);
            previous=value; previousSlope=change;
        }
        return Outputs(("Tr",output));
    }
}
