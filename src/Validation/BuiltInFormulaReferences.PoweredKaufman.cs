using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PoweredKaufmanOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return PoweredKaufmanOutputs(bars, Math.Max(1, Integer(options, "Length", 100)), Number(options, 3, "Factor", "Multiplier"), indicator.BatchName == IndicatorName.AdaptiveTrailingStop);
    }
    internal static IReadOnlyDictionary<string, double[]> PoweredKaufmanOutputs(IReadOnlyList<Bar> bars, int length, double factor, bool trailing)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? zero - v : v;
        ReferenceFraction Round(ReferenceFraction v) => ReferenceFraction.FromDouble(v.ToDouble());
        var average = zero; var a = zero; var b = zero; var upper = zero; var lower = zero; var side = false;
        var averages = new double[bars.Count]; var powers = new double[bars.Count]; var stops = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var travel = zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += Abs(prices[j] - prices[j - 1]);
            var efficiency = travel.Sign == 0 ? zero : Round(Abs(prices[i] - prices[i - length]) / travel);
            var gain = factor == 2 ? Round(efficiency * efficiency) : factor == 3 ? Round(Round(efficiency * efficiency) * efficiency) : ReferenceFraction.FromDouble(Math.Pow(efficiency.ToDouble(), factor));
            if (i == 0) average = a = b = prices[i];
            average = Round((one - gain) * average + gain * prices[i]);
            var previousA = a; var previousB = b;
            a = Round(((prices[i] - a).Sign >= 0 ? prices[i] : a) - Abs(prices[i] - a) * gain);
            b = Round(((prices[i] - b).Sign <= 0 ? prices[i] : b) + Abs(prices[i] - b) * gain);
            if ((a - previousA).Sign > 0 || (a - previousA).Sign < 0 && (b - previousB).Sign < 0) upper = a;
            if ((b - previousB).Sign < 0 || (b - previousB).Sign > 0 && (a - previousA).Sign > 0) lower = b;
            if ((upper - prices[i]).Sign > 0) side = true; else if ((lower - prices[i]).Sign > 0) side = false;
            averages[i] = average.ToDouble(); powers[i] = gain.ToDouble(); stops[i] = (side ? lower : upper).ToDouble();
        }
        return trailing ? Outputs(("Ts", stops)) : Outputs(("Per", powers), ("Pkama", averages));
    }
}
