using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? NaturalMarkets(IBuiltInIndicator indicator)
    {
        var key = indicator.BatchName switch
        {
            IndicatorName.NaturalDirectionalCombo => "Nxc",
            IndicatorName.NaturalDirectionalIndex => "Ndx",
            IndicatorName.NaturalStochasticIndicator => "Nst",
            IndicatorName.NaturalMarketMirror => "Nmm",
            IndicatorName.NaturalMarketRiver => "Nmr",
            IndicatorName.NaturalMarketCombo => "Nmc",
            IndicatorName.NaturalMovingAverage => "Nma",
            _ => null
        };
        if (key is null) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 40);
        var smooth = Integer(options, "SmoothLength", 20);
        var kind = AverageKind(options, key == "Nmc" ? 2 : 3);
        if (kind == 0 && key != "Nma") return null;
        return new(key, new[] { key }, bars =>
        {
            var logs = bars.Select(b => b.Close > 0 ? 1000 * Math.Log(b.Close) : 0).ToArray();
            double Log(int i) => i < 0 ? 0 : logs[i];
            var changes = logs.Select((v, i) => v - Log(i - 1)).ToArray();
            var weights = Enumerable.Range(1, length).Select(j => 1 / Math.Sqrt(j)).ToArray();
            double[] Directional() => Average(logs.Select((v, i) =>
                100 / weights.Sum() * Enumerable.Range(0, length).Sum(j =>
                {
                    var travel = Enumerable.Range(0, j + 1).Sum(lag => Math.Abs(Log(i - lag) - Log(i - lag - 1)));
                    return travel == 0 ? 0 : weights[j] * (v - Log(i - j)) / travel;
                })).ToArray(), smooth, kind);
            double[] Stochastic()
            {
                var position = bars.Select((b, i) =>
                {
                    var window = Window(bars, i, length).ToArray();
                    var high = window.Max(v => v.High);
                    var low = window.Min(v => v.Low);
                    if (high == low) return 0; // NOSONAR: S1244 - Equal bounds define an exactly zero range.
                    var numerator = ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(low);
                    var denominator = ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low);
                    return (numerator / denominator).ToDouble();
                }).ToArray();
                var raw = bars.Select((_, i) => 200 / weights.Sum() * Enumerable.Range(0, Math.Min(length, i + 1))
                    .Sum(j => weights[j] * position[i - j]) - 100).ToArray();
                return Average(raw, smooth, kind);
            }
            double[] River() => Average(logs.Select((_, i) => Enumerable.Range(0, Math.Min(length, i + 1))
                .Sum(j => changes[i - j] / (Math.Sqrt(j + 1) + Math.Sqrt(j)))).ToArray(), length, kind);
            double[] Mirror() => Average(logs.Select((v, i) => 100d / length * Enumerable.Range(1, length)
                .Sum(j => weights[j - 1] * (v - Log(i - j)))).ToArray(), length, kind);
            // The combination is positive only if both component readings are positive.
            double[] Combine(double[] first, double[] second) => first.Zip(second, (a, b) =>
                (a > 0 && b > 0 ? 1 : -1) * Math.Sqrt(Math.Abs(a * b))).ToArray();
            if (key == "Nma")
            {
                var line = bars.Select((b, i) =>
                {
                    var travel = Window(changes, i, length).Sum(Math.Abs);
                    var ratio = travel == 0 ? 0 : Enumerable.Range(0, Math.Min(length, i + 1))
                        .Sum(j => Math.Abs(changes[i - j]) / (Math.Sqrt(j + 1) + Math.Sqrt(j))) / travel;
                    var priorPrice = i == 0 ? 0 : bars[i - 1].Close;
                    return priorPrice + ratio * (b.Close - priorPrice);
                }).ToArray();
                return Outputs((key, line));
            }
            var result = key switch
            {
                "Ndx" => Directional(), "Nst" => Stochastic(), "Nxc" => Combine(Directional(), Stochastic()),
                "Nmr" => River(), "Nmm" => Mirror(), _ => Combine(River(), Mirror())
            };
            return Outputs((key, result));
        });
    }
}
