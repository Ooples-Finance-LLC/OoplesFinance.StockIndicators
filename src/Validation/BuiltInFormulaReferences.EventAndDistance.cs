using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? EventAndDistance(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        switch (indicator.BatchName)
        {
            case IndicatorName.ZDistanceFromVwap:
                var volumeMean = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.VolumeWeightedAveragePrice;
                if (kind == 0 && !volumeMean) return null;
                return new("Zscore", new[] { "Zscore" }, bars =>
                {
                    var period = Integer(options, "Length", 20);
                    var prices = Closes(bars);
                    var mean = volumeMean ? prices.Select((value, i) =>
                    {
                        var window = Window(bars, i, period).ToArray();
                        var volume = window.Sum(b => b.Volume);
                        return volume == 0 ? 0 : value + window.Sum(b => b.Volume * (b.Close - value)) / volume;
                    }).ToArray() : Average(prices, period, kind);
                    var residual = prices.Zip(mean, (v, m) => v - m).ToArray();
                    var variance = Average(residual.Select(v => v * v).ToArray(), period, 1);
                    return Outputs(("Zscore", residual.Select((v, i) => variance[i] == 0 ? 0 : v / Math.Sqrt(variance[i])).ToArray()));
                });
            case IndicatorName.DrunkardWalk:
                return new("UpWalk", new[] { "UpWalk", "DnWalk" }, bars =>
                {
                    var period = Integer(options, "Length1", 80);
                    var ranges = TrueRanges(bars);
                    double[] Walk(bool upward)
                    {
                        var extremes = bars.Select((_, i) => upward ? Window(bars, i, period).Min(b => b.Low)
                            : Window(bars, i, period).Max(b => b.High)).ToArray();
                        var ages = bars.Select((_, i) => i - Enumerable.Range(Math.Max(0, i - period + 1), Math.Min(i + 1, period))
                            .Last(j => (upward ? bars[j].Low : bars[j].High) == extremes[i])).ToArray(); // NOSONAR: S1244 - Locate the actual extremum observation, including exact ties.
                        return bars.Select((b, i) =>
                        {
                            if (ages[i] == 0) return 0;
                            // Expand all observation weights of the variable-gain range average.
                            double average = 0, survival = 1;
                            for (var j = i; j >= 0; j--)
                            {
                                var gain = ages[j] == 0 ? 0 : 1d / ages[j];
                                average += survival * gain * ranges[j];
                                survival *= 1 - gain;
                            }
                            var distance = upward ? b.High - extremes[i] : extremes[i] - b.Low;
                            return distance / (Math.Sqrt(ages[i]) * (average > 0 ? average : 1));
                        }).ToArray();
                    }
                    return Outputs(("UpWalk", Walk(true)), ("DnWalk", Walk(false)));
                });
            case IndicatorName.WellesWilderVolatilitySystem:
                if (kind == 0) return null;
                return new("Wwvs", new[] { "Wwvs" }, bars =>
                {
                    var period = Integer(options, "Length2", 21);
                    var prices = Closes(bars);
                    var baseline = Average(prices, Integer(options, "Length1", 63), kind);
                    var atr = Average(TrueRanges(bars), period, kind);
                    var factor = Number(options, 3, "Factor");
                    return Outputs(("Wwvs", prices.Select((p, i) => p > baseline[i]
                        ? Window(prices, i, Math.Max(2, period)).Max() - factor * atr[i]
                        : Window(prices, i, Math.Max(2, period)).Min() + factor * atr[i]).ToArray()));
                });
            case IndicatorName.UtBotAlerts:
                if (kind == 0) return null;
                return new("TrailingStop", new[] { "TrailingStop", "Position", "Buy", "Sell" }, bars =>
                {
                    var distance = Average(TrueRanges(bars), Integer(options, "Length", 10), kind)
                        .Select(v => v * Number(options, 1, "KeyValue")).ToArray();
                    var stops = new double[bars.Count];
                    var positions = new double[bars.Count];
                    var buy = new double[bars.Count];
                    var sell = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var price = bars[i].Close;
                        var previous = i == 0 ? price : bars[i - 1].Close;
                        var priorStop = i == 0 ? 0 : stops[i - 1];
                        var side = Math.Sign(price - priorStop);
                        var priorSide = Math.Sign(previous - priorStop);
                        var candidate = price + (side > 0 ? -distance[i] : distance[i]);
                        stops[i] = side == priorSide && side != 0
                            ? side > 0 ? Math.Max(priorStop, candidate) : Math.Min(priorStop, candidate) : candidate;
                        positions[i] = side * priorSide < 0 ? side : i == 0 ? 0 : positions[i - 1];
                        buy[i] = i > 0 && priorSide <= 0 && price > stops[i] ? 1 : 0;
                        sell[i] = i > 0 && priorSide >= 0 && price < stops[i] ? 1 : 0;
                    }
                    return Outputs(("TrailingStop", stops), ("Position", positions), ("Buy", buy), ("Sell", sell));
                });
            default: return null;
        }
    }
}
