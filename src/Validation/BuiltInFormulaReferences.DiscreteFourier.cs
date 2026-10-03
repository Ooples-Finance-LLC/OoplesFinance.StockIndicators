using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? DiscreteFourierFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not EhlersDiscreteFourierTransformSpecOptions options) return null;
        return new("Edft", new[] { "Edft" }, bars =>
        {
            var prices = Closes(bars);
            var angle = Clamp(2*Math.PI/options.Length, .01, .99);
            var pole = Math.Cos(angle)/(1+Math.Sin(angle));
            var exact = prices.Select(v => (decimal)v).ToArray();
            var exactPole = (decimal)pole;
            var hp = exact.Select((v, i) =>
            {
                if (i < 6) return v;
                decimal result = 0, weight = 1;
                for (var j = i; j >= 6; j--)
                {
                    result += weight*(1+exactPole)/2*(exact[j]-exact[j-1]);
                    weight *= exactPole;
                }
                return result+weight*exact[5];
            }).ToArray();
            var taps = new[] { 1m, 2, 3, 3, 2, 1 };
            var cleaned = exact.Select((v, i) => i < 6 ? v : taps.Select((w, lag) => w*hp[i-lag]).Sum()/12).ToArray();
            return Outputs(("Edft", prices.Select((_, i) =>
            {
                var mass = (double)Window(cleaned, i, options.MaxLength).Max(Math.Abs);
                var scale = Window(prices, i, options.MaxLength).Max(Math.Abs);
                if (mass <= 64*Math.Pow(2, -52)*scale) return 0;
                var spectrum = Enumerable.Range(options.MinLength, options.MaxLength-options.MinLength+1).Select(period =>
                {
                    var coefficient = Enumerable.Range(0, Math.Min(i+1, options.MaxLength)).Aggregate(Complex.Zero,
                        (sum, lag) => sum+((double)cleaned[i-lag]/mass)*Complex.FromPolarCoordinates(1, 2*Math.PI*lag/period));
                    return (Period: period, Power: coefficient.Real*coefficient.Real+coefficient.Imaginary*coefficient.Imaginary);
                }).ToArray();
                var peak = spectrum.Max(bin => bin.Power);
                if (peak == 0) return 0;
                var weights = spectrum.Select(bin => (bin.Period, Weight: Math.Max(0,
                    3-10*Math.Log10(1+99*(1-bin.Power/peak))))).ToArray();
                return weights.Sum(bin => bin.Period*bin.Weight)/weights.Sum(bin => bin.Weight);
            }).ToArray()));
        });
    }
}
