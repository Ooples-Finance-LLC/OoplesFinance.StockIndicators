using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? TunedBypassFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not EhlersDominantCycleTunedBypassFilterSpecOptions options) return null;
        return new("V2", new[] { "V1", "V2" }, bars =>
        {
            var prices = Closes(bars);
            var cycles = SpectrumCycles(bars, options.MinLength, options.MaxLength, options.Length1, options.Length2);
            var angle = Clamp(2*Math.PI/options.Length1, .01, .99);
            var pole = Math.Cos(angle)/(1+Math.Sin(angle));
            var hp = prices.Select((v, i) => i < 7 ? v : Math.Pow(pole, i-6)*prices[6]
                + Enumerable.Range(7, i-6).Sum(j => (1+pole)/2*Math.Pow(pole, i-j)*(prices[j]-prices[j-1]))).ToArray();
            var taps = new[] { 1d, 2, 3, 3, 2, 1 };
            var smoothed = prices.Select((v, i) => i < 7 ? v-(i == 0 ? 0 : prices[i-1])
                : taps.Select((w, lag) => w*hp[i-lag]).Sum()/12).ToArray();
            var v1 = new double[bars.Count]; var v2 = new double[bars.Count];
            double previous = 0, older = 0;
            for (var i = 0; i < bars.Count; i++)
            {
                var frequency = Clamp(2*Math.PI/cycles[i], .01, .99);
                var bandwidth = Clamp(4*Math.PI*Math.Max(.15, .5-.015*i)/cycles[i], .01, .99);
                var damping = Math.Cos(bandwidth)/(1+Math.Sin(bandwidth));
                var forcing = (1-damping)/2*(smoothed[i]-(i == 0 ? 0 : smoothed[i-1]));
                v1[i] = forcing+Math.Cos(frequency)*(1+damping)*previous-damping*older;
                v2[i] = 2*cycles[i]/Math.PI*(v1[i]-previous);
                older = previous; previous = v1[i];
            }
            return Outputs(("V1", v1), ("V2", v2));
        });
    }
}
