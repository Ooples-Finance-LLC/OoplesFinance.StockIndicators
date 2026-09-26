using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? RiskRatios(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name == IndicatorName.CalmarRatio)
        {
            var length = Integer(indicator.CreateOptions(), "Length", 30);
            return new("Cr", new[] { "Cr" }, bars =>
            {
                var drawdowns = bars.Select((b, i) =>
                {
                    var peak = Window(bars, i, Math.Max(2, length)).Max(v => v.Close);
                    return peak == 0 ? 0 : (b.Close - peak) / peak;
                }).ToArray();
                var values = bars.Select((b, i) =>
                {
                    if (i < length || bars[i - length].Close == 0) return 0d;
                    var ratio = b.Close / bars[i - length].Close;
                    // Existing contract annualizes on 24 periods per year, independently of timestamps.
                    var annual = ratio < 0 ? 0 : Math.Pow(ratio, 24d / length) - 1;
                    var depth = Math.Abs(Window(drawdowns, i, length).Min());
                    return depth == 0 ? 0 : annual / depth;
                }).ToArray();
                return Outputs(("Cr", values));
            });
        }
        if (name == IndicatorName.OmegaRatio || name == IndicatorName.UpsidePotentialRatio || name == IndicatorName.TreynorRatio)
            return TargetReturnRatios(indicator);
        if (name != IndicatorName.SharpeRatio && name != IndicatorName.InformationRatio &&
            name != IndicatorName.SortinoRatio && name != IndicatorName.MartinRatio) return null;
        var options = indicator.CreateOptions();
        var period = Integer(options, "Length");
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        var benchmark = Number(options, name == IndicatorName.InformationRatio ? .05 : .02, "Bmk");
        var target = Math.Pow(1 + benchmark, period / 360d) - 1;
        var key = name == IndicatorName.InformationRatio ? "Ir" : name == IndicatorName.MartinRatio ? "Mr" : "Sr";
        return new(key, new[] { key }, bars =>
        {
            // Contract uses overlapping period returns and a benchmark compounded on a 360-bar year.
            var returns = bars.Select((b, i) => i < period || bars[i - period].Close == 0 ? 0
                : b.Close / bars[i - period].Close - 1 - (name == IndicatorName.InformationRatio ? 0 : target)).ToArray();
            var mean = Average(returns, period, kind);
            double[] risk;
            if (name == IndicatorName.MartinRatio)
            {
                var squared = bars.Select((b, i) =>
                {
                    var high = Window(bars, i, period).Max(v => v.Close);
                    return high == 0 ? 0 : Math.Pow(b.Close / high - 1, 2);
                }).ToArray();
                risk = squared.Select((_, i) => Math.Sqrt(Window(squared, i, period).Average())).ToArray();
            }
            else if (name == IndicatorName.SortinoRatio)
                risk = Average(returns.Select(r => Math.Pow(Math.Min(r, 0), 2)).ToArray(), period, kind).Select(Math.Sqrt).ToArray();
            else risk = PopulationVariance(returns, period).Select(Math.Sqrt).ToArray();
            return Outputs((key, mean.Select((m, i) => risk[i] == 0 ? 0
                : (m - (name == IndicatorName.InformationRatio ? target : 0)) / risk[i]).ToArray()));
        });
    }
    private static FormulaDefinition TargetReturnRatios(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var period = Integer(options, "Length");
        var target = Math.Pow(1 + Number(options, .05, "Bmk"), period / 360d) - 1;
        var name = indicator.BatchName;
        var key = name == IndicatorName.OmegaRatio ? "Or" : name == IndicatorName.UpsidePotentialRatio ? "Upr" : "Tr";
        return new(key, new[] { key }, bars =>
        {
            var returns = bars.Select((b, i) => i < period || bars[i - period].Close == 0 ? 0 : b.Close / bars[i - period].Close - 1).ToArray();
            var line = returns.Select((_, i) =>
            {
                var window = Window(returns, i, period).ToArray();
                if (name == IndicatorName.TreynorRatio)
                {
                    var beta = Number(options, 1, "Beta");
                    return beta == 0 ? 0 : (window.Average() - target) / beta;
                }
                var padded = Enumerable.Repeat(0d, period - window.Length).Concat(window).Select(v => v - target).ToArray();
                var upside = padded.Sum(v => Math.Max(v, 0));
                var downside = name == IndicatorName.OmegaRatio ? padded.Sum(v => Math.Max(-v, 0))
                    : Math.Sqrt(period * padded.Sum(v => Math.Pow(Math.Min(v, 0), 2)));
                return downside == 0 ? 0 : upside / downside;
            }).ToArray();
            return Outputs((key, line));
        });
    }

}
