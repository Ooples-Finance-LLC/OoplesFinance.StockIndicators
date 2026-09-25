using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MomentumWindows(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        switch (indicator.BatchName)
        {
            case IndicatorName.SimplePriceZone:
                return new("Spz", new[] { "Spz" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var line = bars.Select((b, i) =>
                    {
                        var distance = Window(changes, i, length).Sum(Math.Abs);
                        var origin = bars[Math.Max(0, i - length)].Close;
                        return distance == 0 ? 0 : 100 * (b.Close - origin) / distance;
                    }).ToArray();
                    return Outputs(("Spz", line));
                });
            case IndicatorName.WaveTrendOscillator:
                return new("Wto", new[] { "Wto", "Signal" }, bars =>
                {
                    var prices = bars.Select(b => (b.Open + b.High + b.Low + b.Close) / 4).ToArray();
                    // Expand residual weights over price increments; no subtraction of near-equal price levels.
                    var displacement = prices.Select((_, i) => Enumerable.Range(1, i).Sum(j =>
                    {
                        var weight = i < length ? (double)j / (i + 1) : j < length
                            ? (double)j / length * Math.Pow(1 - 2d / (length + 1), i - length + 1)
                            : Math.Pow(1 - 2d / (length + 1), i - j + 1);
                        return weight * (prices[j] - prices[j - 1]);
                    })).ToArray();
                    var deviation = Average(displacement.Select(Math.Abs).ToArray(), length, 3);
                    var channel = displacement.Select((d, i) => deviation[i] == 0 ? 0 : d / deviation[i] / .015).ToArray();
                    var line = Average(channel, 21, 3);
                    return Outputs(("Wto", line), ("Signal", Average(line, 4, 3)));
                });
            case IndicatorName.WamiOscillator:
                var wamiKind = AverageKind(options, 3);
                if (wamiKind == 0) return null;
                if (wamiKind is 1 or 2 or 3 or 6) return new("Wami", new[] { "Wami" }, bars => WamiOutputs(bars, indicator));
                return new("Wami", new[] { "Wami" }, bars =>
                {
                    var differences = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var weighted = Average(differences, 4, 2);
                    return Outputs(("Wami", Average(Average(weighted, length, wamiKind), length, wamiKind)));
                });
            case IndicatorName.VolumeWeightedRelativeStrengthIndex:
                var volumeRsiKind = AverageKind(options, 2);
                if (volumeRsiKind == 0) return null;
                return new("Vwrsi", new[] { "Vwrsi" }, bars =>
                {
                    var signed = bars.Select((b, i) => i == 0 ? 0 : b.Volume * (b.Close - bars[i - 1].Close)).ToArray();
                    var net = Average(signed, length, volumeRsiKind);
                    var total = Average(signed.Select(Math.Abs).ToArray(), length, volumeRsiKind);
                    // Signed/absolute flow ratio is the centered RSI; no movement retains the published +100 convention.
                    var centered = net.Select((v, i) => total[i] == 0 ? 100 : 100 * v / total[i]).ToArray();
                    return Outputs(("Vwrsi", Average(centered, Integer(options, "SmoothLength", 3), volumeRsiKind)));
                });
            case IndicatorName.PolarizedFractalEfficiency:
                var efficiencyKind = AverageKind(options, 3);
                if (efficiencyKind == 0) return null;
                return new("Pfe", new[] { "Pfe" }, bars =>
                {
                    var positions = bars.Select((b, i) => new System.Numerics.Complex(i, b.Close)).ToArray();
                    var line = positions.Select((position, i) =>
                    {
                        if (i < length) return 0;
                        var displacement = position - positions[i - length];
                        var path = Enumerable.Range(i - length + 1, length)
                            .Sum(j => (positions[j] - positions[j - 1]).Magnitude);
                        // Triangle inequality bounds the unsmoothed signed distance ratio by one.
                        return 100 * Math.Sign(displacement.Imaginary) * displacement.Magnitude / path;
                    }).ToArray();
                    return Outputs(("Pfe", Average(line, Integer(options, "SmoothLength", 5), efficiencyKind)));
                });
            case IndicatorName.NthOrderDifferencingOscillator:
                return new("Nodo", new[] { "Nodo" }, bars =>
                {
                    // (1 - z^-length)^order applied as repeated differences, without binomial weights.
                    var line = Closes(bars);
                    for (var order = 0; order < Integer(options, "LbLength", 2); order++)
                        line = line.Select((v, i) => v - (i < length ? 0 : line[i - length])).ToArray();
                    return Outputs(("Nodo", line));
                });
            case IndicatorName.RelativeDifferenceOfSquaresOscillator:
                return new("Rdos", new[] { "Rdos" }, bars =>
                {
                    var directions = bars.Select((b, i) => Math.Sign(b.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var line = directions.Select((_, i) =>
                    {
                        var window = Window(directions, i, length).ToArray();
                        // P(up)^2-P(down)^2 = E(direction)*P(nonzero direction).
                        return window.Average() * window.Count(d => d != 0) / window.Length;
                    }).ToArray();
                    return Outputs(("Rdos", line));
                });
            case IndicatorName.PriceVolumeOscillator:
                return new("Po", new[] { "Po", "Vo" }, bars =>
                {
                    double[] Leg(double[] values, int period)
                    {
                        var changes = values.Select((v, i) => i < period ? 0 : v - values[i - period]).ToArray();
                        return changes.Select((_, i) =>
                        {
                            var window = Window(changes, i, period).ToArray();
                            var mass = window.Sum(Math.Abs);
                            return mass == 0 ? 0 : window.Sum() / mass;
                        }).ToArray();
                    }
                    return Outputs(("Po", Leg(Closes(bars), Integer(options, "Length1", length))),
                        ("Vo", Leg(bars.Select(b => b.Volume).ToArray(), Integer(options, "Length2", 14))));
                });
            case IndicatorName.SmoothedRateOfChange:
                var rocKind = AverageKind(options, 3);
                if (rocKind == 0) return null;
                return new("Sroc", new[] { "Sroc" }, bars =>
                {
                    var smoothed = Average(Closes(bars), Integer(options, "SmoothLength", 13), rocKind);
                    var lag = Integer(options, "RocLength", length);
                    return Outputs(("Sroc", smoothed.Select((v, i) => i < lag || smoothed[i - lag] == 0
                        ? 100 : 100 * (v / smoothed[i - lag] - 1)).ToArray()));
                });
            case IndicatorName.OCHistogram:
                var histogramKind = AverageKind(options, 3);
                if (histogramKind == 0) return null;
                // Linearity lets us average candle bodies instead of subtracting two price averages.
                return new("OcHistogram", new[] { "OcHistogram" }, bars => Outputs(("OcHistogram",
                    Average(bars.Select(b => b.Close - b.Open).ToArray(), length, histogramKind))));
            case IndicatorName.OscOscillator:
                var oscKind = AverageKind(options, 1);
                if (oscKind == 0) return null;
                return new("Osc", new[] { "Osc" }, bars =>
                {
                    var prices = Closes(bars);
                    var slow = Average(prices, length, oscKind);
                    var fast = Average(prices, Math.Max(1, length / 2), oscKind);
                    return Outputs(("Osc", slow.Zip(fast, (s, f) => s - f).ToArray()));
                });
            case IndicatorName.RangeActionVerificationIndex:
                var raviKind = AverageKind(options, 1);
                if (raviKind == 0) return null;
                return new("Ravi", new[] { "Ravi" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = Average(prices, Integer(options, "FastLength", 7), raviKind);
                    var slow = Average(prices, Integer(options, "SlowLength", 65), raviKind);
                    return Outputs(("Ravi", fast.Zip(slow, (f, s) => s == 0 ? 0 : 100 * (f / s - 1)).ToArray()));
                });
            case IndicatorName.TickLineMomentumOscillator:
                var tickKind = AverageKind(options, 3);
                if (tickKind == 0) return null;
                return new("Tlmo", new[] { "Tlmo" }, bars =>
                {
                    var baseline = Average(Closes(bars), length, tickKind);
                    var votes = bars.Select((b, i) => Math.Sign(b.Close - (i == 0 ? 0 : baseline[i - 1]))).ToArray();
                    var score = votes.Select((_, i) => (double)votes.Take(i + 1).Sum()).ToArray();
                    var period = Integer(options, "SmoothLength", 5);
                    return Outputs(("Tlmo", Average(PercentageReturn(score, period), period, tickKind)));
                });
            case IndicatorName.SmoothedDeltaRatioOscillator:
                var deltaKind = AverageKind(options, 1);
                if (deltaKind == 0) return null;
                return new("Sdro", new[] { "Sdro" }, bars =>
                {
                    var prices = Closes(bars);
                    var baseline = Average(prices, length, deltaKind);
                    var changes = prices.Select((v, i) => i < length ? 0 : Math.Abs(v - prices[i - length])).ToArray();
                    var travel = Average(changes, length, deltaKind);
                    var line = prices.Select((_, i) => i < length || travel[i] == 0 ? 0
                        : Math.Max(0, Math.Min(1, (baseline[i] - baseline[i - length]) / travel[i]))).ToArray();
                    return Outputs(("Sdro", line));
                });
            default: return null;
        }
    }
}
