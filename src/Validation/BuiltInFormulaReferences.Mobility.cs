using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MobilityFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not MobilityOscillatorSpecOptions options) return null;
        var kind = AverageKind(options, 2);
        if (kind == 0) return null;
        return new("Mo", new[] { "Mo", "Signal" }, bars =>
        {
            var raw = bars.Select((bar, i) =>
            {
                if (i < options.Length) return 0d;
                var sample = Window(bars, i, options.Length).ToArray();
                var low = sample.Min(b => b.Low); var high = sample.Max(b => b.High);
                if (high == low) return 0d; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                var width = (high-low)/10;
                var edges = Enumerable.Range(0, 11).Select(k => k == 10 ? high : low+k*width).ToArray();
                // Difference of candle CDFs integrates the mass in each common bin.
                var masses = Enumerable.Range(0, 10).Select(k => sample.Average(b =>
                {
                    if (b.High == b.Low) return b.Low >= edges[k] && (b.Low < edges[k+1] || k == 9) ? 1d : 0; // NOSONAR: S1244 - Equal candle bounds are a point mass, not a narrow interval.
                    double Cdf(double x) => Math.Max(0, Math.Min(1, (x-b.Low)/(b.High-b.Low)));
                    return Cdf(edges[k+1])-Cdf(edges[k]);
                })).ToArray();
                var maximum = masses.Max(); var mode = Array.FindIndex(masses, mass => maximum-mass <= 1e-12);
                var comparison = bars[i-options.Length].Close;
                var priceBin = Enumerable.Range(0, 10).Where(k => comparison >= edges[k] &&
                    (comparison < edges[k+1] || k == 9 && comparison <= high)).DefaultIfEmpty(-1).First();
                var density = priceBin < 0 ? 0 : masses[priceBin];
                return (comparison < low+(mode+0.5)*width ? 1 : -1)*100*Math.Max(0, 1-density/masses[mode]);
            }).ToArray();
            var line = Average(raw, 7, kind);
            return Outputs(("Mo", line), ("Signal", Average(line, 7, kind)));
        });
    }
}
