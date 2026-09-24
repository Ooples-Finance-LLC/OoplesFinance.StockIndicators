using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] DeviationAverageReference(double[] prices, int fast, int slow)
    {
        var changes = prices.Select((v, i) => i < 2 ? 0 : v - prices[i - 2]).ToArray();
        var drive = changes.Select((v, i) => (v + (i == 0 ? 0 : changes[i - 1])) / 2).ToArray();
        var filtered = PoleTrajectory(drive, fast, 2, 1, 1, 0);
        var weights = new double[prices.Length];
        double scaled = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i >= slow - 1)
            {
                var samples = Window(filtered, i, slow).ToArray();
                var mean = samples.Average();
                var deviation = Math.Sqrt(samples.Sum(v => (v - mean) * (v - mean)) / slow);
                if (deviation != 0) scaled = filtered[i] / deviation;
            }
            weights[i] = Clamp(5 * Math.Abs(scaled) / slow, .01, .99);
        }
        // Expand the time-varying exponential average into its weighted price history.
        return prices.Select((_, i) =>
        {
            double survival = 1, value = 0;
            for (var j = i; j >= 0; j--)
            {
                value += survival * weights[j] * prices[j];
                survival *= 1 - weights[j];
            }
            return value;
        }).ToArray();
    }

    private static FormulaDefinition? EhlersLinear(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        switch (indicator.BatchName)
        {
            case IndicatorName.EhlersConvolutionIndicator:
                return new("Eci", new[] { "Eci", "Slope" }, bars =>
                {
                    var prices = Closes(bars); var first = Integer(options, "Length1");
                    var second = Integer(options, "Length2"); var windowLength = Integer(options, "Length3");
                    var angle = Math.Min(.99, Math.Sqrt(2)*Math.PI/first);
                    var pole = Math.Cos(angle)/(1+Math.Sin(angle));
                    var forcing = prices.Select((v, i) => v-2*(i == 0 ? 0 : prices[i-1])+(i < 2 ? 0 : prices[i-2])).ToArray();
                    var high = prices.Select((_, i) => Enumerable.Range(0, i+1).Sum(j => forcing[j]*(i-j+1)*Math.Pow(pole, i-j)*Math.Pow((1+pole)/2, 2))).ToArray();
                    var lowAngle = Math.Sqrt(2)*Math.PI/second;
                    var roof = HilbertLowPass(high, Math.Exp(-lowAngle), lowAngle);
                    var values = new double[bars.Count]; var slope = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var n = Math.Min(i+1, windowLength);
                        var x = Enumerable.Range(0, n).Select(j => roof[i-j]).ToArray();
                        var y = Enumerable.Range(0, n).Select(j => i > j ? roof[i-j-1] : 0).ToArray();
                        var mx = x.Average(); var my = y.Average();
                        var energy = x.Sum(v => (v-mx)*(v-mx))*y.Sum(v => (v-my)*(v-my));
                        var correlation = energy == 0 ? 0 : x.Select((v, j) => (v-mx)*(y[j]-my)).Sum()/Math.Sqrt(energy);
                        values[i] = .25*(1+Math.Tanh(1.5*correlation));
                        var lag = (n+1)/2; var previous = i < lag ? 0 : roof[i-lag];
                        slope[i] = roof[i]-previous > 1e-12*Math.Max(1, Math.Max(Math.Abs(roof[i]), Math.Abs(previous))) ? -1 : 1;
                    }
                    return Outputs(("Eci", values), ("Slope", slope));
                });

            case IndicatorName.EhlersCombFilterSpectralEstimate:
                return new("Ecfse", new[] { "Ecfse" }, bars =>
                {
                    var high = Integer(options, "Length1", 48); var low = Integer(options, "Length2", 10);
                    var bandwidth = Number(options, .3, "Bw");
                    var roof = HilbertRoofingTrajectory(Closes(bars), high, low);
                    var drive = roof.Select((v, i) => v - (i < 2 ? 0 : roof[i - 2])).ToArray();
                    var periods = Enumerable.Range(low, Math.Max(0, high - low + 1)).ToArray();
                    var powers = periods.Select(period =>
                    {
                        var angle = 2 * Math.PI * bandwidth / period; var cosine = Math.Cos(angle);
                        var decay = Clamp(cosine <= 0 ? .01 : cosine / (1 + Math.Abs(Math.Sin(angle))), .01, .99);
                        var first = Math.Cos(2 * Math.PI / period) * (1 + decay);
                        var discriminant = Complex.Sqrt(new Complex(first * first - 4 * decay, 0));
                        var pole1 = (first + discriminant) / 2; var pole2 = (first - discriminant) / 2;
                        var impulse = Enumerable.Range(0, roof.Length).Select(lag => discriminant.Magnitude < 1e-12
                            ? (lag + 1) * Complex.Pow(pole1, lag).Real
                            : ((Complex.Pow(pole1, lag + 1) - Complex.Pow(pole2, lag + 1)) / discriminant).Real).ToArray();
                        var band = roof.Select((_, i) => .5 * (1 - decay) * Enumerable.Range(0, i + 1).Sum(j => impulse[i - j] * drive[j])).ToArray();
                        return band.Select((_, i) => Enumerable.Range(Math.Max(0, i - period), Math.Min(i, period))
                            .Sum(j => Math.Pow(band[j] / period, 2))).ToArray();
                    }).ToArray();
                    return Outputs(("Ecfse", roof.Select((_, i) =>
                    {
                        var maximum = powers.Length == 0 ? 0 : powers.Max(p => p[i]);
                        var retained = Enumerable.Range(0, periods.Length).Where(j => maximum > 0 && powers[j][i] >= maximum / 2).ToArray();
                        var total = retained.Sum(j => powers[j][i]);
                        return total == 0 ? 0 : retained.Sum(j => periods[j] * powers[j][i]) / total;
                    }).ToArray()));
                });
            case IndicatorName.EhlersDiscreteFourierTransformSpectralEstimate:
                return new("Edftse", new[] { "Edftse" }, bars =>
                {
                    var high = Integer(options, "Length1", 48); var low = Integer(options, "Length2", 10);
                    var roof = HilbertRoofingTrajectory(Closes(bars), high, low);
                    var periods = Enumerable.Range(low, Math.Max(0, high - low + 1)).ToArray();
                    var powers = periods.Select(period =>
                    {
                        var energy = roof.Select((_, i) =>
                        {
                            var vector = Enumerable.Range(0, Math.Min(high + 1, i + 1))
                                .Aggregate(Complex.Zero, (sum, lag) => sum + roof[i - lag] * Complex.FromPolarCoordinates(1, 2 * Math.PI * lag / period));
                            return Math.Pow(vector.Magnitude, 4);
                        }).ToArray();
                        return energy.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => .2 * Math.Pow(.8, i - j) * energy[j])).ToArray();
                    }).ToArray();
                    return Outputs(("Edftse", roof.Select((_, i) =>
                    {
                        var maximum = powers.Length == 0 ? 0 : powers.Max(p => p[i]);
                        var retained = Enumerable.Range(0, periods.Length).Where(j => maximum > 0 && powers[j][i] >= maximum / 2).ToArray();
                        var total = retained.Sum(j => powers[j][i]);
                        return total == 0 ? 0 : retained.Sum(j => periods[j] * powers[j][i]) / total;
                    }).ToArray()));
                });
            case IndicatorName.EhlersMesaPredictIndicatorV2:
                var mesaKind = AverageKind(options, 0);
                var mesaHann = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.EhlersHannMovingAverage;
                if (mesaKind == 0 && !mesaHann) return null;
                var history = Integer(options, "Length1", 5); var highPeriod = Integer(options, "Length2", 135);
                var lowPeriod = Integer(options, "Length3", 12); var horizon = Math.Min(history, Integer(options, "Length4", 4));
                return new("Predict", new[] { "Ssf", "Predict", "Extrap" }, bars =>
                {
                    var prices = Closes(bars);
                    double[] PoleFilter(double[] input, int period, bool highPass)
                    {
                        var angle = Clamp(1.414 * Math.PI / period, .01, .99);
                        var radius = Math.Exp(-angle); var b = 2 * radius * Math.Cos(angle);
                        var gain = highPass ? (1 + b + radius * radius) / 4 : 1 - b + radius * radius;
                        var drive = input.Select((v, i) => highPass ? i < 4 ? 0 : v - 2 * input[i - 1] + input[i - 2]
                            : (v + (i == 0 ? 0 : input[i - 1])) / 2).ToArray();
                        var impulse = Enumerable.Range(0, input.Length).Select(lag => Math.Pow(radius, lag)
                            * Math.Sin((lag + 1) * angle) / Math.Sin(angle)).ToArray();
                        return input.Select((_, i) => gain * Enumerable.Range(0, i + 1).Sum(j => drive[j] * impulse[i - j])).ToArray();
                    }
                    var roof = PoleFilter(PoleFilter(prices, highPeriod, true), lowPeriod, false);
                    var hannWeights = Enumerable.Range(1, lowPeriod).Select(j => Math.Pow(Math.Sin(Math.PI * j / (lowPeriod + 1)), 2)).ToArray();
                    var filtered = mesaHann ? roof.Select((_, i) => Enumerable.Range(0, Math.Min(lowPeriod, i + 1))
                        .Sum(lag => hannWeights[lag] * roof[i - lag]) / hannWeights.Sum()).ToArray() : Average(roof, lowPeriod, mesaKind);
                    // Raise the five-state companion matrix to the forecast horizon. This independently
                    // specifies which historical observation each forecast coefficient multiplies.
                    double[,] Multiply(double[,] left, double[,] right)
                    {
                        var result = new double[5, 5];
                        for (var row = 0; row < 5; row++)
                            for (var col = 0; col < 5; col++)
                                for (var k = 0; k < 5; k++) result[row, col] += left[row, k] * right[k, col];
                        return result;
                    }
                    var transition = new double[5, 5]; var forecast = new double[5, 5];
                    var coefficients = new[] { 4.525, -8.45, 8.145, -4.045, .825 };
                    for (var j = 0; j < 5; j++) { transition[0, j] = coefficients[j]; forecast[j, j] = 1; if (j > 0) transition[j, j - 1] = 1; }
                    for (var exponent = horizon; exponent > 0; exponent >>= 1)
                    {
                        if ((exponent & 1) != 0) forecast = Multiply(forecast, transition);
                        transition = Multiply(transition, transition);
                    }
                    var prediction = filtered.Select((_, i) => Enumerable.Range(0, Math.Min(5, Math.Min(history, i + 1)))
                        .Sum(lag => forecast[0, lag] * filtered[i - lag])).ToArray();
                    var extrapolation = filtered.Select((v, i) => (horizon + 1) * v - horizon * (history < 2 || i == 0 ? 0 : filtered[i - 1])).ToArray();
                    return Outputs(("Ssf", filtered), ("Predict", prediction), ("Extrap", extrapolation));
                });
            case IndicatorName.EhlersSmoothedAdaptiveMomentumIndicator:
                var adaptiveMomentumKind = AverageKind(options, 3);
                if (adaptiveMomentumKind == 0) return null;
                return new("Esam", new[] { "Esam", "Signal" }, bars =>
                {
                    var prices = Closes(bars); var first = Integer(options, "Length1", 5);
                    var second = Math.Max(2, Integer(options, "Length2", 8));
                    var periods = AdaptiveCyberPeriods(prices, first, .07);
                    var momentum = prices.Select((v, i) =>
                    {
                        var lag = (int)Math.Ceiling(Math.Abs(periods[i] - 1));
                        return i < lag ? 0 : v - prices[i - lag];
                    }).ToArray();
                    var line = PoleTrajectory(momentum, second, 3, 1, 0, 0);
                    return Outputs(("Esam", line), ("Signal", Average(line, second, adaptiveMomentumKind)));
                });
            case IndicatorName.EhlersZeroMeanRoofingFilter:
                return new("Ezmrf", new[] { "Ezmrf" }, bars =>
                {
                    var first = Integer(options, "Length1", 48); var second = Integer(options, "Length2", 10);
                    var roof = EhlersLinear(new EhlersHpLpRoofingFilter(first, second))!.Compute(bars)["Ehplprf"];
                    var angle = Math.Min(2 * Math.PI / first, .99);
                    var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
                    var difference = roof.Select((v, i) => v - (i == 0 ? 0 : roof[i - 1])).ToArray();
                    return Outputs(("Ezmrf", roof.Select((_, i) => (1 + pole) / 2 * Enumerable.Range(0, i + 1)
                        .Sum(j => difference[j] * Math.Pow(pole, i - j))).ToArray()));
                });
            case IndicatorName.EhlersRocketRelativeStrengthIndex:
                var rocketKind = AverageKind(options, 0);
                var rocketSuper = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.Ehlers2PoleSuperSmootherFilterV2;
                if (!rocketSuper && rocketKind == 0) return null;
                return new("Errsi", new[] { "Errsi" }, bars =>
                {
                    var first = Integer(options, "Length1", 10); var second = Integer(options, "Length2", 8);
                    var momentum = bars.Select((b, i) => i < first - 1 ? 0 : b.Close - bars[i - first + 1].Close).ToArray();
                    var drive = momentum.Select((v, i) => (v + (i == 0 ? 0 : momentum[i - 1])) / 2).ToArray();
                    var smooth = rocketSuper ? Array.Empty<double>() : Average(drive, second, rocketKind);
                    var changes = rocketSuper
                        ? PoleTrajectory(drive.Select((v, i) => v - (i == 0 ? 0 : drive[i - 1])).ToArray(), second, 2, 1, 1, 0)
                        : smooth.Select((v, i) => v - (i == 0 ? 0 : smooth[i - 1])).ToArray();
                    double previous = 0;
                    return Outputs(("Errsi", changes.Select((_, i) =>
                    {
                        var sample = Window(changes, i, first).ToArray(); var total = sample.Sum(Math.Abs);
                        if (total != 0) previous = Math.Max(-.999, Math.Min(.999, sample.Sum() / total));
                        return .5 * (Math.Log(1 + previous) - Math.Log(1 - previous)) * Number(options, 1, "Mult");
                    }).ToArray()));
                });
            case IndicatorName.EhlersDeviationScaledSuperSmoother:
                var deviationKind = AverageKind(options, 0);
                var deviationHann = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.EhlersHannMovingAverage;
                if (deviationKind == 0 && !deviationHann) return null;
                return new("Edsss", new[] { "Edsss" }, bars =>
                {
                    var period = Integer(options, "Length", 12);
                    var smoothing = (int)Math.Ceiling(period / 1.4m);
                    var prices = Closes(bars);
                    var momentum = prices.Select((v, i) => v - (i < period ? 0 : prices[i - period])).ToArray();
                    double[] filtered;
                    if (deviationHann)
                    {
                        var weights = Enumerable.Range(1, smoothing).Select(j => Math.Pow(Math.Sin(Math.PI * j / (smoothing + 1)), 2)).ToArray();
                        filtered = momentum.Select((_, i) => Enumerable.Range(0, Math.Min(smoothing, i + 1))
                            .Sum(j => momentum[i - j] * weights[j]) / weights.Sum()).ToArray();
                    }
                    else filtered = Average(momentum, smoothing, deviationKind);
                    var result = new double[prices.Length];
                    // Propagate each input impulse separately through the time-varying filter.
                    var previous = new double[prices.Length];
                    var older = new double[prices.Length];
                    for (var i = 0; i < prices.Length; i++)
                    {
                        var rms = Math.Sqrt(Window(filtered, i, 50).Average(v => v * v));
                        var magnitude = rms == 0 || filtered[i] == 0 ? 1 : Math.Abs(filtered[i] / rms);
                        var pole = Complex.FromPolarCoordinates(Math.Exp(-Math.Sqrt(2) * Math.PI * magnitude / period),
                            Math.Sqrt(2) * Math.PI * magnitude / period);
                        var gain = ((1 - pole) * (1 - Complex.Conjugate(pole))).Real;
                        for (var j = 0; j <= i; j++)
                        {
                            var impulse = j == i ? gain * (prices[i] + (i == 0 ? 0 : prices[i - 1])) / 2
                                : 2 * pole.Real * previous[j] - pole.Magnitude * pole.Magnitude * older[j];
                            older[j] = previous[j]; previous[j] = impulse;
                            result[i] += impulse;
                        }
                    }
                    return Outputs(("Edsss", result));
                });
            case IndicatorName.EhlersDeviationScaledMovingAverage:
            case IndicatorName.EhlersFisherizedDeviationScaledOscillator:
                var fisherDeviation = indicator.BatchName == IndicatorName.EhlersFisherizedDeviationScaledOscillator;
                var deviationKey = fisherDeviation ? "Efdso" : "Edsma";
                return new(deviationKey, new[] { deviationKey }, bars =>
                {
                    var fast = Integer(options, "Length", 20);
                    var line = DeviationAverageReference(Closes(bars), fast, fisherDeviation ? 40 : fast * 2);
                    if (!fisherDeviation) return Outputs((deviationKey, line));
                    double held = 0;
                    return Outputs((deviationKey, line.Select(v => held = Math.Abs(v) < 2 ? (Math.Log(2 + v) - Math.Log(2 - v)) / 2 : held).ToArray()));
                });
            case IndicatorName.EhlersRecursiveMedianOscillator:
                return new("Ermo", new[] { "Ermo" }, bars =>
                {
                    var prices = Closes(bars);
                    var median = prices.Select((_, i) =>
                    {
                        var sorted = Window(prices, i, Integer(options, "Length1", 5)).OrderBy(v => v).ToArray();
                        return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
                    }).ToArray();
                    var angle1 = Clamp(2 * Math.PI / Integer(options, "Length2", 12), .01, .99);
                    var angle2 = Clamp(Math.Sqrt(2) * Math.PI / Integer(options, "Length3", 30), .01, .99);
                    var pole1 = Math.Cos(angle1) / (1 + Math.Sin(angle1));
                    var pole2 = Math.Cos(angle2) / (1 + Math.Sin(angle2));
                    var smoothed = median.Select((_, i) => (1 - pole1) * Enumerable.Range(0, i + 1)
                        .Sum(j => median[j] * Math.Pow(pole1, i - j))).ToArray();
                    var changes = smoothed.Select((v, i) => v - 2 * (i < 1 ? 0 : smoothed[i - 1]) + (i < 2 ? 0 : smoothed[i - 2])).ToArray();
                    var gain = Math.Pow((1 + pole2) / 2, 2);
                    return Outputs(("Ermo", changes.Select((_, i) => gain * Enumerable.Range(0, i + 1)
                        .Sum(j => changes[j] * (i - j + 1) * Math.Pow(pole2, i - j))).ToArray()));
                });
            case IndicatorName.EhlersHurstCoefficient:
                return new("Ehc", new[] { "Ehc" }, bars =>
                {
                    var prices = Closes(bars);
                    var period = Integer(options, "Length1", 30);
                    var half = (period + 1) / 2;
                    double dimension = 0;
                    var raw = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var full = Window(prices, i, Math.Max(2, period)).ToArray();
                        var recent = Window(prices, i, Math.Max(2, half)).ToArray();
                        var older = Enumerable.Range(half, period - half).Select(lag => i < lag ? 0 : prices[i - lag])
                            .Append(i < half ? prices[i] : prices[i - half]).ToArray();
                        var totalRange = (full.Max() - full.Min()) / period;
                        var halves = (recent.Max() - recent.Min() + older.Max() - older.Min()) / half;
                        if (halves > 0 && totalRange > 0) dimension = (Math.Log(halves / totalRange) / Math.Log(2) + dimension) / 2;
                        raw[i] = 2 - dimension;
                    }
                    var angle = Math.Sqrt(2) * Math.PI / Integer(options, "Length2", 20);
                    return Outputs(("Ehc", HilbertLowPass(raw, Math.Exp(-angle), Math.Min(angle, .99))));
                });
            case IndicatorName.EhlersFMDemodulatorIndicator:
                var demodulatorKind = AverageKind(options, 0);
                var demodulatorSuper = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.Ehlers2PoleSuperSmootherFilterV2;
                if (!demodulatorSuper && demodulatorKind == 0) return null;
                return new("Efmd", new[] { "Efmd" }, bars =>
                {
                    var source = bars.Select(b => Clamp(Integer(options, "FastLength", 10) * (b.Close - b.Open), -1, 1)).ToArray();
                    var period = Integer(options, "SlowLength", 30);
                    return Outputs(("Efmd", demodulatorSuper ? PoleTrajectory(source, period, 2, 1, 1, 0) : Average(source, period, demodulatorKind)));
                });
            case IndicatorName.EhlersEvenBetterSineWaveIndicator:
                return new("Ebsi", new[] { "Ebsi" }, bars =>
                {
                    var highAngle = Clamp(2 * Math.PI / Integer(options, "Length1", 40), .01, .99);
                    var pole = Math.Cos(highAngle) / (1 + Math.Sin(highAngle));
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var drive = changes.Select((v, i) => (v + (i == 0 ? 0 : changes[i - 1])) / 2).ToArray();
                    var high = drive.Select((_, i) => (1 + pole) / 2 * Enumerable.Range(0, i + 1)
                        .Sum(j => drive[j] * Math.Pow(pole, i - j))).ToArray();
                    var angle = Clamp(1.414 * Math.PI / Integer(options, "Length2", 10), .01, .99);
                    var filtered = HilbertLowPass(high, Math.Exp(-angle), angle, averageInput: false);
                    return Outputs(("Ebsi", filtered.Select((_, i) =>
                    {
                        var samples = Enumerable.Range(0, 3).Select(lag => i < lag ? 0 : filtered[i - lag]).ToArray();
                        var scale = samples.Max(Math.Abs);
                        if (scale == 0) return 0;
                        var normalized = samples.Select(v => v / scale).ToArray();
                        return normalized.Sum() / Math.Sqrt(3 * normalized.Sum(v => v * v));
                    }).ToArray()));
                });
            case IndicatorName.EhlersModifiedRelativeStrengthIndex:
                return new("Emrsi", new[] { "Emrsi", "Signal" }, bars =>
                {
                    var lower = Integer(options, "Length2", 10);
                    var lookback = Integer(options, "Length3", 10);
                    var roof = HilbertRoofingTrajectory(Closes(bars), Integer(options, "Length1", 48), lower);
                    var changes = roof.Select((v, i) => v - (i == 0 ? 0 : roof[i - 1])).ToArray();
                    var total = changes.Select((_, i) => Window(changes, i, lookback).Sum(Math.Abs)).ToArray();
                    var ratio = changes.Select((_, i) => total[i] == 0 ? 0 : Window(changes, i, lookback).Sum(v => Math.Max(0, v)) / total[i]).ToArray();
                    var angle = Math.Min(Math.Sqrt(2) * Math.PI / lower, .99);
                    var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / lower);
                    var line = HilbertLowPass(ratio, radius, angle);
                    var impulse = Enumerable.Range(0, bars.Count).Select(lag => Math.Pow(radius, lag) * Math.Sin((lag + 1) * angle) / Math.Sin(angle)).ToArray();
                    // The definition resets a sample with an undefined adjacent ratio. Inject a
                    // correcting impulse, preserving its effect on subsequent filter history.
                    for (var i = 0; i < line.Length; i++)
                        if (total[i] == 0 || i == 0 || total[i - 1] == 0)
                        {
                            var correction = -line[i];
                            for (var j = i; j < line.Length; j++) line[j] += correction * impulse[j - i];
                        }
                    return Outputs(("Emrsi", line), ("Signal", HilbertLowPass(line, radius, angle)));
                });
            case IndicatorName.EhlersStochasticCenterOfGravityOscillator:
                return new("Escog", new[] { "Escog" }, bars =>
                {
                    var center = bars.Select((_, i) =>
                    {
                        var count = Math.Min(length, i + 1);
                        var mass = Enumerable.Range(0, count).Sum(j => bars[i - j].Close);
                        return mass == 0 ? 0 : Enumerable.Range(0, count)
                            .Sum(j => ((length - 1d) / 2 - j) * bars[i - j].Close) / mass;
                    }).ToArray();
                    var rank = center.Select((v, i) =>
                    {
                        var window = Window(center, i, Math.Max(2, length)).ToArray();
                        var low = window.Min(); var high = window.Max();
                        return high == low ? 0 : (v - low) / (high - low);
                    }).ToArray();
                    var weighted = Average(rank, 4, 2).Select(v => 2 * v - 1).ToArray();
                    return Outputs(("Escog", weighted.Select((_, i) => Clamp(.96 * ((i == 0 ? 0 : weighted[i - 1]) + .02), 0, 1)).ToArray()));
                });
            case IndicatorName.EhlersImpulseReaction:
                return new("Eir", new[] { "Eir" }, bars =>
                {
                    var lag = Integer(options, "Length1", 2);
                    var pole = Complex.FromPolarCoordinates(Number(options, .9, "Q"), 2 * Math.PI / Integer(options, "Length2", 20));
                    var gain = (1 - pole.Magnitude * pole.Magnitude) / 2;
                    Complex first = 0, second = 0;
                    var result = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        first = gain * (bars[i].Close - (i < lag ? 0 : bars[i - lag].Close)) + pole * first;
                        second = first + Complex.Conjugate(pole) * second;
                        result[i] = bars[i].Close == 0 ? 0 : 100 * second.Real / bars[i].Close;
                    }
                    return Outputs(("Eir", result));
                });
            case IndicatorName.EhlersAdaptiveBandPassFilter:
                return new("Eabpf", new[] { "Eabpf", "Signal" }, bars =>
                {
                    var upper = Integer(options, "Length1", 48); var lower = Integer(options, "Length2", 10);
                    var firstLag = Integer(options, "Length3", 3); var width = Number(options, .3, "Bw");
                    var roof = HilbertRoofingTrajectory(Closes(bars), upper, lower);
                    var cycles = AutocorrelationSpectrum(AutocorrelationTrajectory(roof, upper), upper, lower, firstLag);
                    var periods = cycles.Select(v => Math.Min(upper, Math.Max(firstLag, v))).ToArray();
                    var decay = periods.Select(p =>
                    {
                        var angle = 2 * Math.PI * width / (.9 * p);
                        var cosine = Math.Cos(angle);
                        return cosine <= 0 ? .01 : Clamp(cosine / (1 + Math.Abs(Math.Sin(angle))), .01, .99);
                    }).ToArray();
                    var feedback = periods.Select((p, i) => Math.Cos(2 * Math.PI / (.9 * p)) * (1 + decay[i])).ToArray();
                    var contributions = Enumerable.Range(0, bars.Count).Select(_ => new double[bars.Count]).ToArray();
                    // Propagate each forcing impulse independently through the time-varying denominator.
                    for (var source = 3; source < bars.Count; source++)
                    {
                        contributions[source][source] = (1 - decay[source]) * (roof[source] - roof[source - 2]) / 2;
                        for (var target = source + 1; target < bars.Count; target++)
                            contributions[source][target] = feedback[target] * contributions[source][target - 1]
                                - (target > source + 1 ? decay[target] * contributions[source][target - 2] : 0);
                    }
                    var band = bars.Select((_, i) => contributions.Sum(c => c[i])).ToArray();
                    var line = band.Select((v, i) =>
                    {
                        var peak = Enumerable.Range(0, i + 1).Max(j => Math.Abs(band[j]) * Math.Pow(.991, i - j));
                        return peak == 0 ? 0 : v / peak;
                    }).ToArray();
                    return Outputs(("Eabpf", line), ("Signal", line.Select((_, i) => i == 0 ? 0 : .9 * line[i - 1]).ToArray()));
                });
            case IndicatorName.EhlersZeroCrossingsDominantCycle:
                return new("Ezcdc", new[] { "Ezcdc" }, bars =>
                {
                    // Compose the independent analytic bandpass reference; no production engine is used.
                    var band = EhlersLinear(new EhlersBandPassFilterV1(length, Number(options, .7, "Bw")))!.Compute(bars)["Ebpf"];
                    var crossings = Enumerable.Range(0, band.Length).Where(i => band[i] != 0 &&
                        (i == 0 || Math.Sign(band[i]) != Math.Sign(band[i - 1]))).ToHashSet();
                    var result = new double[bars.Count]; var previousCrossing = -1;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var prior = i == 0 ? 0 : result[i - 1];
                        result[i] = Math.Max(6, prior);
                        if (!crossings.Contains(i)) continue;
                        var measured = 2d * (i - previousCrossing);
                        result[i] = Math.Min(1.25 * prior, Math.Max(.8 * prior, measured));
                        previousCrossing = i;
                    }
                    return Outputs(("Ezcdc", result));
                });
            case IndicatorName.EhlersBandPassFilterV1:
                return new("Ebpf", new[] { "Ebpf", "Signal" }, bars =>
                {
                    var width = Number(options, .3, "Bw");
                    double Pole(double frequency) => Math.Cos(frequency) / (1 + Math.Sin(frequency));
                    var hpPole = Pole(Clamp(.25 * width * 2 * Math.PI / length, .01, .99));
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var high = changes.Select((_, i) => (3 - hpPole) / 2 * Enumerable.Range(0, i + 1)
                        .Sum(j => changes[j] * Math.Pow(hpPole, i - j))).ToArray();
                    var decay = Pole(Clamp(width * 2 * Math.PI / length, .01, .99));
                    var sum = Math.Cos(Clamp(2 * Math.PI / length, .01, .99)) * (1 + decay);
                    var gap = Complex.Sqrt(sum * sum - 4 * decay);
                    var firstPole = (sum + gap) / 2; var secondPole = (sum - gap) / 2;
                    Complex first = 0, second = 0;
                    var band = new double[bars.Count];
                    for (var i = 3; i < band.Length; i++)
                    {
                        first = (1 - decay) / 2 * (high[i] - high[i - 2]) + firstPole * first;
                        second = first + secondPole * second;
                        band[i] = second.Real;
                    }
                    var line = band.Select((v, i) =>
                    {
                        var peak = Enumerable.Range(0, i + 1).Max(j => Math.Abs(band[j]) * Math.Pow(.991, i - j));
                        return peak == 0 ? 0 : v / peak;
                    }).ToArray();
                    var triggerPole = Pole(Clamp(1.5 * width * 2 * Math.PI / length, .01, .99));
                    var trigger = line.Select((_, i) => (3 - triggerPole) / 2 * Enumerable.Range(0, i + 1)
                        .Sum(j => (line[j] - (j == 0 ? 0 : line[j - 1])) * Math.Pow(triggerPole, i - j))).ToArray();
                    return Outputs(("Ebpf", line), ("Signal", trigger));
                });
            case IndicatorName.EhlersStochasticCyberCycle:
                return new("Escc", new[] { "Escc", "Signal" }, bars =>
                {
                    var cycle = CyberCycleReference(Closes(bars), Number(options, .7, "Alpha"));
                    var rank = cycle.Select((v, i) =>
                    {
                        var window = Window(cycle, i, Math.Max(2, length)).ToArray();
                        var low = window.Min(); var high = window.Max();
                        return high == low ? 0 : (v - low) / (high - low);
                    }).ToArray();
                    var line = Average(rank, 4, 2).Select(v => 2 * v - 1).ToArray();
                    var trigger = line.Select((_, i) => Clamp(.96 * ((i == 0 ? 0 : line[i - 1]) + .02), -1, 1)).ToArray();
                    return Outputs(("Escc", line), ("Signal", trigger));
                });
            case IndicatorName.EhlersRoofingFilterIndicator:
                return new("Erfi", new[] { "Erfi" }, bars =>
                {
                    var angle = Math.Min(Math.Sqrt(2) * Math.PI / Integer(options, "Length1", 80), .99);
                    var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
                    var prices = Closes(bars);
                    var forcing = prices.Select((v, i) => v - 2 * (i == 0 ? 0 : prices[i - 1]) + (i < 2 ? 0 : prices[i - 2])).ToArray();
                    // Repeated real pole: lag n has impulse coefficient (n + 1) * pole^n.
                    var high = prices.Select((_, i) => Math.Pow((1 + pole) / 2, 2) * Enumerable.Range(0, i + 1)
                        .Sum(j => forcing[j] * (i - j + 1) * Math.Pow(pole, i - j))).ToArray();
                    var lowAngle = Math.Sqrt(2) * Math.PI / Integer(options, "Length2", 40);
                    return Outputs(("Erfi", HilbertLowPass(high, Math.Exp(-lowAngle), Math.Min(lowAngle, .99))));
                });
            case IndicatorName.EhlersEarlyOnsetTrendIndicator:
                return new("Eoti", new[] { "Eoti" }, bars =>
                {
                    var high = HighPassV1Trajectory(Closes(bars), Integer(options, "Length2", 100), 1);
                    var angle = Clamp(Math.Sqrt(2) * Math.PI / Integer(options, "Length1", 30), .01, .99);
                    var filtered = HilbertLowPass(high, Math.Exp(-angle), angle);
                    var k = Number(options, .85, "K");
                    return Outputs(("Eoti", filtered.Select((v, i) =>
                    {
                        var peak = Enumerable.Range(0, i + 1).Max(j => Math.Abs(filtered[j]) * Math.Pow(.991, i - j));
                        var ratio = peak == 0 ? 0 : v / peak;
                        return 1 + k * ratio == 0 ? 0 : (ratio + k) / (1 + k * ratio);
                    }).ToArray()));
                });
            case IndicatorName.EhlersModifiedStochasticIndicator:
            case IndicatorName.EhlersRoofingFilterV1:
            case IndicatorName.EhlersStochastic:
                var smoother = options.GetType().GetProperty("MaType")?.GetValue(options);
                var superSmoother = smoother is null || smoother is MovingAvgType.Ehlers2PoleSuperSmootherFilterV1;
                var roofKind = AverageKind(options, 0);
                if (!superSmoother && roofKind == 0) return null;
                var modifiedStoch = indicator.BatchName == IndicatorName.EhlersModifiedStochasticIndicator;
                var stochastic = modifiedStoch || indicator.BatchName == IndicatorName.EhlersStochastic;
                var roofKey = modifiedStoch ? "Emsi" : stochastic ? "Es" : "Erf";
                return new(roofKey, new[] { roofKey }, bars =>
                {
                    var highPeriod = Integer(options, "Length1", Integer(options, "HpLength", 48));
                    var lowPeriod = stochastic && !modifiedStoch ? 10 : Integer(options, "Length2", Integer(options, "LpLength", 10));
                    double[] Smooth(double[] values, int period) => superSmoother
                        ? PoleTrajectory(values, period, 2, 1, 0, 3) : Average(values, period, roofKind);
                    var source = RoofingInputReference(Closes(bars), highPeriod);
                    var roofing = Smooth(source, lowPeriod);
                    if (!stochastic) return Outputs((roofKey, roofing));
                    var normalized = roofing.Select((v, i) =>
                    {
                        var window = Window(roofing, i, Math.Max(2, modifiedStoch ? Integer(options, "Length3", 20) : length)).ToArray();
                        var low = window.Min(); var highValue = window.Max();
                        return highValue == low ? 0 : Math.Max(0, Math.Min(1, (v - low) / (highValue - low)));
                    }).ToArray();
                    if (modifiedStoch)
                    {
                        var angle = Math.Sqrt(2) * Math.PI / highPeriod;
                        return Outputs((roofKey, HilbertLowPass(normalized.Select(v => 100 * v).ToArray(), Math.Exp(-angle), Math.Min(angle, .99))));
                    }
                    var pairs = normalized.Select((v, i) => (v + (i == 0 ? 0 : normalized[i - 1])) / 2).ToArray();
                    return Outputs((roofKey, Smooth(pairs, length)));
                });
            case IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV1:
                return new("Erema", new[] { "Erema" }, bars => Outputs(("Erema",
                    ReverseEmaPolynomial(Closes(bars), Number(options, .1, "Alpha")))));
            case IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV2:
                return new("EremaCycle", new[] { "EremaCycle", "EremaTrend" }, bars => Outputs(
                    ("EremaCycle", ReverseEmaPolynomial(Closes(bars), Number(options, .3, "CycleAlpha"))),
                    ("EremaTrend", ReverseEmaPolynomial(Closes(bars), Number(options, .05, "TrendAlpha")))));
            case IndicatorName.EhlersHpLpRoofingFilter:
                return new("Ehplprf", new[] { "Ehplprf" }, bars =>
                {
                    var highPeriod = Integer(options, "Length1", 48);
                    var lowPeriod = Integer(options, "Length2", 10);
                    var angle = Math.Min(2 * Math.PI / highPeriod, .99);
                    var alpha = (Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle);
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var high = changes.Select((_, i) => (1 - alpha / 2) * Enumerable.Range(0, i + 1)
                        .Sum(j => changes[j] * Math.Pow(1 - alpha, i - j))).ToArray();
                    var lowAngle = Math.Sqrt(2) * Math.PI / lowPeriod;
                    return Outputs(("Ehplprf", HilbertLowPass(high, Math.Exp(-lowAngle), Math.Min(lowAngle, .99))));
                });
            case IndicatorName.EhlersPhaseCalculation:
                var phaseKind = AverageKind(options, 3);
                if (phaseKind == 0) return null;
                return new("Phase", new[] { "Phase", "Signal" }, bars =>
                {
                    var period = Math.Max(2, length);
                    var phase = bars.Select((_, i) =>
                    {
                        // Center a complete window on its mean; a nonzero Fourier bin rejects DC.
                        var baseline = i + 1 < period ? 0 : Window(bars, i, period).Average(b => b.Close);
                        var observations = Enumerable.Range(0, period).Select(j => (i < j ? 0 : bars[i - j].Close) - baseline).ToArray();
                        var vector = observations.Select((v, j) => v * Complex.FromPolarCoordinates(1, 2 * Math.PI * j / period))
                            .Aggregate(Complex.Zero, (sum, v) => sum + v);
                        if (vector.Magnitude <= 1e-12 * observations.Sum(Math.Abs)) return 90d;
                        var angle = (vector.Phase * 180 / Math.PI + 450) % 360;
                        return angle < 1e-10 || angle > 360 - 1e-10 ? 0 : angle;
                    }).ToArray();
                    return Outputs(("Phase", phase), ("Signal", Average(phase, period, phaseKind)));
                });
            case IndicatorName.EhlersEmpiricalModeDecomposition:
            case IndicatorName.EhlersTrendExtraction:
            case IndicatorName.EhlersUniversalTradingFilter:
            case IndicatorName.EhlersSnakeUniversalTradingFilter:
                var decomposition = indicator.BatchName == IndicatorName.EhlersEmpiricalModeDecomposition;
                var extraction = decomposition || indicator.BatchName == IndicatorName.EhlersTrendExtraction;
                var snake = indicator.BatchName == IndicatorName.EhlersSnakeUniversalTradingFilter;
                var filterKind = AverageKind(options, 1);
                var hann = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.EhlersHannMovingAverage;
                if (filterKind == 0 && !hann) return null;
                var primary = extraction ? "Trend" : snake ? "Erf" : "Eutf";
                return new(primary, decomposition ? new[] { "Trend", "Peak", "Valley" } : extraction ? new[] { "Trend", "Bp" } : new[] { primary, "UpperBand", "LowerBand" }, bars =>
                {
                    var period = decomposition ? Integer(options, "Length1", 20) : extraction ? length : Integer(options, "Length1", snake ? 23 : 16);
                    var prices = Closes(bars);
                    double[] source;
                    if (extraction || snake)
                    {
                        var frequency = Clamp((extraction ? 2 : 1) * Math.PI / period, .01, .99);
                        var width = Clamp((extraction ? 4 * Number(options, .1, "Delta") : Number(options, 1.4, "Bw")) * Math.PI / period, .01, .99);
                        var decay = Math.Cos(width) / (1 + Math.Sin(width));
                        if (extraction) decay = Clamp(decay, .01, .99);
                        var sum = Math.Cos(frequency) * (1 + decay);
                        var gap = Complex.Sqrt(sum * sum - 4 * decay);
                        var firstPole = (sum + gap) / 2;
                        var secondPole = (sum - gap) / 2;
                        Complex first = 0, second = 0;
                        source = new double[prices.Length];
                        for (var i = snake ? 3 : 2; i < prices.Length; i++)
                        {
                            first = (1 - decay) / 2 * (prices[i] - prices[i - 2]) + firstPole * first;
                            second = first + secondPole * second;
                            source[i] = second.Real;
                        }
                    }
                    else
                    {
                        var lag = (int)Math.Ceiling(Number(options, 2, "Mult") * period);
                        source = prices.Select((v, i) => v - (i < lag ? 0 : prices[i - lag])).ToArray();
                    }
                    var smoothingPeriod = extraction ? 2 * period : period;
                    double[] filtered;
                    if (hann)
                    {
                        var weights = Enumerable.Range(1, smoothingPeriod).Select(j => Math.Pow(Math.Sin(Math.PI * j / (smoothingPeriod + 1)), 2)).ToArray();
                        var mass = weights.Sum();
                        filtered = source.Select((_, i) => Enumerable.Range(0, Math.Min(smoothingPeriod, i + 1))
                            .Sum(j => weights[j] * source[i - j]) / mass).ToArray();
                    }
                    else filtered = Average(source, smoothingPeriod, filterKind);
                    if (decomposition)
                    {
                        var extrema = new[] { new double[bars.Count], new double[bars.Count] };
                        for (var i = 1; i < bars.Count; i++)
                        {
                            var prior = source[i - 1]; var older = i < 2 ? 0 : source[i - 2];
                            extrema[0][i] = prior > source[i] && prior > older ? prior : extrema[0][i - 1];
                            extrema[1][i] = prior < source[i] && prior < older ? prior : extrema[1][i - 1];
                        }
                        var fraction = Number(options, .1, "Fraction");
                        var smooth = Integer(options, "Length2", 50);
                        return Outputs(("Trend", filtered),
                            ("Peak", Average(extrema[0], smooth, filterKind).Select(v => fraction * v).ToArray()),
                            ("Valley", Average(extrema[1], smooth, filterKind).Select(v => fraction * v).ToArray()));
                    }
                    if (extraction) return Outputs(("Trend", filtered), ("Bp", source));
                    var energy = filtered.Select(v => v * v).ToArray();
                    var rms = filtered.Select((_, i) => Math.Sqrt(Window(energy, i, Integer(options, "Length2", 50)).Average())).ToArray();
                    return Outputs((primary, filtered), ("UpperBand", rms), ("LowerBand", rms.Select(v => -v).ToArray()));
                });
            case IndicatorName.EhlersSuperPassbandFilter:
                return new("Espf", new[] { "Espf", "UpperBand", "LowerBand" }, bars =>
                {
                    var numerator = Integer(options, "Length1", 5);
                    var fastGain = Clamp((double)numerator / Integer(options, "FastLength", 40), .01, .99);
                    var slowGain = Clamp((double)numerator / Integer(options, "SlowLength", 60), .01, .99);
                    // Difference of two zero-seeded exponential kernels, not the production second-order recurrence.
                    var line = bars.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => bars[j].Close
                        * (fastGain * Math.Pow(1 - fastGain, i - j) - slowGain * Math.Pow(1 - slowGain, i - j)))).ToArray();
                    var power = line.Select(v => v * v).ToArray();
                    var rms = line.Select((_, i) => Math.Sqrt(Window(power, i, Integer(options, "Length2", 50)).Average())).ToArray();
                    return Outputs(("Espf", line), ("UpperBand", rms), ("LowerBand", rms.Select(v => -v).ToArray()));
                });
            case IndicatorName.EhlersUniversalOscillator:
                var signalKind = AverageKind(options, 3);
                if (signalKind == 0) return null;
                return new("Euo", new[] { "Euo", "Signal" }, bars =>
                {
                    var noise = bars.Select((b, i) => i < 2 ? 0 : (b.Close - bars[i - 2].Close) / 2).ToArray();
                    var angle = 1.414 * Math.PI / length;
                    var filtered = HilbertLowPass(noise, Math.Exp(-Clamp(angle, .01, .99)), angle);
                    var line = filtered.Select((v, i) =>
                    {
                        var peak = Enumerable.Range(0, i + 1).Max(j => Math.Pow(.991, i - j) * Math.Abs(filtered[j]));
                        return peak == 0 ? 0 : v / peak;
                    }).ToArray();
                    return Outputs(("Euo", line), ("Signal", Average(line, 9, signalKind)));
                });
            case IndicatorName.EhlersTripleDelayLineDetrender:
                var delayKind = AverageKind(options, 0);
                var elliptic = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.EhlersModifiedOptimumEllipticFilter;
                if (delayKind == 0 && !elliptic) return null;
                return new("Etdld", new[] { "Etdld", "Signal" }, bars =>
                {
                    // H(z)=(1-z^-6)^3 / ((1-.088z^-6)(1-1.2z^-6+.7z^-12)).
                    // Factor the poles and evaluate six interleaved first-order sections.
                    var pole = new Complex(.6, Math.Sqrt(.34));
                    var states1 = new Complex[6];
                    var states2 = new Complex[6];
                    var states3 = new Complex[6];
                    var raw = new double[bars.Count];
                    double Price(int i) => i < 0 ? 0 : bars[i].Close;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var lane = i % 6;
                        var drive = Price(i) - 3 * Price(i - 6) + 3 * Price(i - 12) - Price(i - 18);
                        states1[lane] = drive + .088 * states1[lane];
                        states2[lane] = states1[lane] + pole * states2[lane];
                        states3[lane] = states2[lane] + Complex.Conjugate(pole) * states3[lane];
                        raw[i] = states3[lane].Real;
                    }
                    double[] Smooth(double[] values) => elliptic ? EllipticTrajectory(values, true) : Average(values, length, delayKind);
                    var line = Smooth(raw);
                    return Outputs(("Etdld", line), ("Signal", Smooth(line)));
                });
            case IndicatorName.EhlersAnticipateIndicator:
            case IndicatorName.EhlersImpulseResponse:
            case IndicatorName.EhlersBandPassFilterV2:
            case IndicatorName.EhlersCycleBandPassFilter:
            case IndicatorName.EhlersCycleAmplitude:
                var anticipate = indicator.BatchName == IndicatorName.EhlersAnticipateIndicator;
                var impulseResponse = anticipate || indicator.BatchName == IndicatorName.EhlersImpulseResponse;
                var responseKind = AverageKind(options, 0);
                var responseHann = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.EhlersHannMovingAverage;
                if (impulseResponse && responseKind == 0 && !responseHann) return null;
                var bandVariant = impulseResponse || indicator.BatchName == IndicatorName.EhlersBandPassFilterV2;
                var amplitude = indicator.BatchName == IndicatorName.EhlersCycleAmplitude;
                var bandwidth = bandVariant ? Number(options, impulseResponse ? 1 : .3, "Bw") : 2 * Number(options, .1, "Delta");
                var bandKey = anticipate ? "Predict" : impulseResponse ? "Eir" : amplitude ? "Eca" : bandVariant ? "Ebpf" : "Ecbpf";
                return new(bandKey, new[] { bandKey }, bars =>
                {
                    var angle = Clamp(2 * Math.PI * bandwidth / length, .01, .99);
                    var decay = Math.Cos(angle) / (1 + Math.Sin(angle));
                    var coefficient = Math.Cos(Clamp(2 * Math.PI / length, .01, .99)) * (1 + decay);
                    var rootGap = Complex.Sqrt(coefficient * coefficient - 4 * decay);
                    var root1 = (coefficient + rootGap) / 2;
                    var root2 = (coefficient - rootGap) / 2;
                    var response = Enumerable.Range(0, bars.Count).Select(j => rootGap.Magnitude < 1e-12
                        ? ((j + 1) * Complex.Pow(root1, j)).Real
                        : ((Complex.Pow(root1, j + 1) - Complex.Pow(root2, j + 1)) / rootGap).Real).ToArray();
                    var start = bandVariant ? 3 : 2;
                    var band = bars.Select((_, i) => Enumerable.Range(start, Math.Max(0, i - start + 1))
                        .Sum(j => (1 - decay) / 2 * (bars[j].Close - bars[j - 2].Close) * response[i - j])).ToArray();
                    if (impulseResponse)
                    {
                        var period = Clamp((int)Math.Ceiling(length / 1.4), 2, 530);
                        var weights = Enumerable.Range(1, period).Select(j => Math.Pow(Math.Sin(Math.PI * j / (period + 1)), 2)).ToArray();
                        var smoothed = responseHann ? band.Select((_, i) => Enumerable.Range(0, Math.Min(period, i + 1))
                            .Sum(j => weights[j] * band[i - j]) / weights.Sum()).ToArray() : Average(band, period, responseKind);
                        return Outputs((bandKey, anticipate ? AnticipateReference(smoothed, length) : smoothed));
                    }
                    if (!amplitude) return Outputs((bandKey, band));
                    var lag = (int)Math.Ceiling(length / 4d);
                    var energy = band.Select((v, i) => v * v + (i < lag ? 0 : band[i - lag] * band[i - lag])).ToArray();
                    return Outputs((bandKey, energy.Select((_, i) => 2 * 1.414 * Math.Sqrt(Window(energy, i, length).Sum() / length)).ToArray()));
                });
            case IndicatorName.EhlersCyberCycle:
            case IndicatorName.EhlersSimpleCycleIndicator:
                var simpleCycle = indicator.BatchName == IndicatorName.EhlersSimpleCycleIndicator;
                var cycleAlpha = Number(options, simpleCycle ? .07 : 2d / (length + 1), "Alpha");
                var cycleKey = simpleCycle ? "Esci" : "Ecc";
                return new(cycleKey, new[] { cycleKey }, bars =>
                {
                    var line = CyberCycleReference(Closes(bars), cycleAlpha, simpleCycle);
                    return Outputs((cycleKey, line));
                });
            case IndicatorName.EhlersDetrendedLeadingIndicator:
                return new("Deli", new[] { "Dsp", "Deli" }, bars =>
                {
                    var midpoints = bars.Select((b, i) => (Math.Max(b.High, i == 0 ? 0 : bars[i - 1].High)
                        + Math.Min(b.Low, i == 0 ? 0 : bars[i - 1].Low)) / 2).ToArray();
                    var alpha = length > 2 ? 2d / (length + 1) : .67;
                    double[] Smooth(double[] values, double gain, bool seed) => values.Select((_, i) =>
                        (seed ? Math.Pow(1 - gain, i + 1) * values[0] : 0)
                        + Enumerable.Range(0, i + 1).Sum(j => gain * Math.Pow(1 - gain, i - j) * values[j])).ToArray();
                    var spread = Smooth(midpoints, alpha, true).Zip(Smooth(midpoints, alpha / 2, true), (fast, slow) => fast - slow).ToArray();
                    return Outputs(("Dsp", spread), ("Deli", spread.Zip(Smooth(spread, alpha, false), (v, mean) => v - mean).ToArray()));
                });
            case IndicatorName.EhlersFilter:
                return new("Ef", new[] { "Ef" }, bars =>
                {
                    var prices = Closes(bars);
                    var weights = prices.Select((p, i) => Math.Abs(p - (i < 5 ? 0 : prices[i - 5]))).ToArray();
                    var products = prices.Zip(weights, (p, w) => p * w).ToArray();
                    return Outputs(("Ef", prices.Select((p, i) => Window(weights, i, length).Sum() is var mass && mass > 0
                        ? Window(products, i, length).Sum() / mass : p).ToArray()));
                });
            case IndicatorName.EhlersRecursiveMedianFilter:
                return new("Ermf", new[] { "Ermf" }, bars =>
                {
                    var prices = Closes(bars);
                    var medians = prices.Select((_, i) =>
                    {
                        var sorted = Window(prices, i, length).OrderBy(v => v).ToArray();
                        return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
                    }).ToArray();
                    // The published second period is fixed at 12. Half-angle form of its coefficient.
                    var tangent = Math.Tan(Math.PI / 12);
                    var alpha = 2 * tangent / (1 + tangent);
                    return Outputs(("Ermf", medians.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => alpha * Math.Pow(1 - alpha, i - j) * medians[j])).ToArray()));
                });
            case IndicatorName.EhlersBetterExponentialMovingAverage:
                return new("Ebema", new[] { "Ebema" }, bars =>
                {
                    var angle = 2 * Math.PI / length;
                    var trig = Math.Sqrt(2) * Math.Cos(angle - Math.PI / 4);
                    var alpha = trig == 0 ? .01 : Clamp(1 - 1 / trig, .01, .99);
                    var prices = Closes(bars);
                    // Plain EMA impulse response, minus half the current first difference.
                    var line = prices.Select((p, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => alpha * Math.Pow(1 - alpha, i - j) * prices[j])
                        - alpha / 2 * (p - (i == 0 ? 0 : prices[i - 1]))).ToArray();
                    return Outputs(("Ebema", line));
                });
            case IndicatorName.EhlersSuperSmootherFilter:
            case IndicatorName.EhlersAverageErrorFilter:
                var errorFilter = indicator.BatchName == IndicatorName.EhlersAverageErrorFilter;
                var key = errorFilter ? "Eaef" : "Essf";
                return new(key, new[] { key }, bars =>
                {
                    var prices = Closes(bars);
                    // Preserve this legacy variant's documented coefficient saturation.
                    var angle = Clamp(Math.Sqrt(2) * Math.PI / length, .01, .99);
                    var pole = Complex.FromPolarCoordinates(Math.Exp(-angle), angle);
                    var gain = ((1 - pole) * (1 - Complex.Conjugate(pole))).Real;
                    var impulse = Enumerable.Range(0, prices.Length).Select(lag =>
                        ((Complex.Pow(pole, lag + 1) - Complex.Pow(Complex.Conjugate(pole), lag + 1))
                            / (pole - Complex.Conjugate(pole))).Real).ToArray();
                    var forcing = prices.Select((p, i) => gain / 2 * (p + (i == 0 ? 0 : prices[i - 1]))).ToArray();
                    var smooth = prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => impulse[i - j] * forcing[j])).ToArray();
                    if (errorFilter)
                    {
                        for (var i = 0; i < Math.Min(3, prices.Length); i++)
                        {
                            var correction = prices[i] - smooth[i];
                            for (var j = i; j < prices.Length; j++) smooth[j] += correction * impulse[j - i];
                        }
                        var residual = prices.Zip(smooth, (p, s) => p - s).ToArray();
                        return Outputs((key, smooth.Select((s, i) => s + Enumerable.Range(3, Math.Max(0, i - 2))
                            .Sum(j => gain * impulse[i - j] * residual[j])).ToArray()));
                    }
                    return Outputs((key, smooth));
                });
            default: return null;
        }
    }
    private static double[] ReverseEmaPolynomial(double[] prices, double alpha)
    {
        alpha = Clamp(alpha, .01, .99);
        var decay = 1 - alpha;
        // Eight reverse sections form a finite polynomial product (decay^(2^k) + z^-1).
        // Expand it, then convolve against the analytical zero-seeded EMA impulse response.
        var polynomial = new[] { 1d };
        for (var stage = 0; stage < 8; stage++)
        {
            var next = new double[polynomial.Length + 1];
            var weight = Math.Pow(decay, 1 << stage);
            for (var j = 0; j < polynomial.Length; j++)
            { next[j] += polynomial[j] * weight; next[j + 1] += polynomial[j]; }
            polynomial = next;
        }
        var impulse = Enumerable.Range(0, prices.Length).Select(lag => alpha * Math.Pow(decay, lag) - alpha * alpha *
            Enumerable.Range(0, Math.Min(lag + 1, polynomial.Length)).Sum(j => polynomial[j] * Math.Pow(decay, lag - j))).ToArray();
        return prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => prices[j] * impulse[i - j])).ToArray();
    }

    private static double[] CyberCycleReference(double[] prices, double cycleAlpha, bool simpleCycle = false)
    {
        double Price(int i) => i < 0 ? 0 : prices[i];
        var smooth = prices.Select((_, i) => (Price(i) + 2 * Price(i - 1) + 2 * Price(i - 2) + Price(i - 3)) / 6).ToArray();
        double Seed(int i) => (Price(i) - 2 * Price(i - 1) + Price(i - 2)) / 4;
        var forcing = smooth.Select((v, i) => Math.Pow(1 - cycleAlpha / 2, 2)
            * (v - (i > 0 ? 2 * smooth[i - 1] : 0) + (i > 1 ? smooth[i - 2] : 0))).ToArray();
        var pole = 1 - cycleAlpha;
        var start = simpleCycle ? 0 : 7;
        var line = prices.Select((_, i) =>
        {
            if (i < 7) return Seed(i);
            var driven = Enumerable.Range(start, i - start + 1).Sum(j => (i - j + 1) * Math.Pow(pole, i - j) * forcing[j]);
            if (simpleCycle) return driven;
            var age = i - 6;
            return driven + ((age + 1) * Seed(6) - age * pole * Seed(5)) * Math.Pow(pole, age);
        }).ToArray();
        return line;
    }

    private static double[] RoofingInputReference(double[] prices, int length)
    {
        var period = Math.Sqrt(2) * length;
        if (period <= 2) return new double[prices.Length];
        var angle = 2 * Math.PI / period;
        var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        var drive = prices.Select((v, i) => ((v - (i < 1 ? 0 : prices[i - 1]))
            - ((i < 2 ? 0 : prices[i - 2]) - (i < 3 ? 0 : prices[i - 3]))) / 2).ToArray();
        return prices.Select((_, i) => Math.Pow((1 + pole) / 2, 2) * Enumerable.Range(0, i + 1)
            .Sum(j => drive[j] * (i - j + 1) * Math.Pow(pole, i - j))).ToArray();
    }

}
