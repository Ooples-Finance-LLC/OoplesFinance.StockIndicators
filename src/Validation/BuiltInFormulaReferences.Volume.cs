using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? VolumeAndReturns(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var name = indicator.BatchName;
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, name == IndicatorName.DonchianChannelWidth ? 1 : 3);
        FormulaDefinition Single(string key, Func<IReadOnlyList<Bar>, double[]> compute) =>
            new(key, new[] { key }, bars => Outputs((key, compute(bars))));
        switch (name)
        {
            case IndicatorName.DemandIndex:
                // This library's single-bar buying/selling-volume ratio, with the first bar defined as zero.
                return Single("Di", bars => bars.Select((b, i) => i == 0 || b.Volume == 0 || b.High == b.Close || b.High == b.Low
                    ? 0 : (2 * b.Close - b.Low - b.High) / (b.High - b.Close)).ToArray());
            case IndicatorName.KlingerVolumeOscillator:
                if (kind == 0) return null;
                return new("Kvo", new[] { "Kvo", "KvoSignal", "KvoHistogram" }, bars =>
                {
                    var sums = bars.Select(b => b.High + b.Low + b.Close).ToArray();
                    var directions = new int[bars.Count];
                    for (var i = 1; i < bars.Count; i++)
                        directions[i] = sums[i] == sums[i - 1] ? directions[i - 1] : Math.Sign(sums[i] - sums[i - 1]);
                    // Measure each trend segment directly, including the bar preceding its reversal.
                    var force = bars.Select((b, i) =>
                    {
                        var reversal = Enumerable.Range(1, i).Where(j => directions[j] != directions[j - 1]).DefaultIfEmpty(0).Max();
                        var start = Math.Max(0, reversal - 1);
                        var totalRange = bars.Skip(start).Take(i - start + 1).Sum(v => v.High - v.Low);
                        return totalRange == 0 ? 0 : b.Volume * Math.Abs(2 * ((b.High - b.Low) / totalRange) - 1) * directions[i] * 100;
                    }).ToArray();
                    var fastPeriod = Integer(options, "FastLength", Integer(options, "Length", 34));
                    var slowPeriod = Integer(options, "SlowLength", 55);
                    double[] line;
                    if (kind == 3)
                    {
                        // Subtract the independently expanded observation weights before summing.
                        // Subtracting two large EMA trajectories loses precision near a zero crossing.
                        decimal[] Powers(int period)
                        {
                            var powers = new decimal[force.Length + 1];
                            powers[0] = 1;
                            var decay = 1 - 2m / (period + 1);
                            for (var i = 1; i < powers.Length; i++) powers[i] = powers[i - 1] * decay;
                            return powers;
                        }
                        var fastPowers = Powers(fastPeriod);
                        var slowPowers = Powers(slowPeriod);
                        decimal Weight(int period, decimal[] powers, int end, int observation)
                        {
                            if (end < period) return 1m / (end + 1);
                            return observation < period ? powers[end - period + 1] / period
                                : 2m / (period + 1) * powers[end - observation];
                        }
                        line = force.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j =>
                            force[j] * (double)(Weight(fastPeriod, fastPowers, i, j) - Weight(slowPeriod, slowPowers, i, j)))).ToArray();
                    }
                    else line = Average(force, fastPeriod, kind).Zip(Average(force, slowPeriod, kind), (f, s) => f - s).ToArray();
                    var signal = Average(line, Integer(options, "SignalLength", 13), kind);
                    return Outputs(("Kvo", line), ("KvoSignal", signal), ("KvoHistogram", line.Zip(signal, (v, m) => v - m).ToArray()));
                });
            case IndicatorName.OnBalanceVolumeDisparityIndicator:
            case IndicatorName.NegativeVolumeDisparityIndicator:
                if (kind == 0) return null;
                var negativeDisparity = name == IndicatorName.NegativeVolumeDisparityIndicator;
                var disparityKey = negativeDisparity ? "Nvdi" : "Obvdi";
                return new(disparityKey, new[] { disparityKey, "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var volumeIndex = new double[bars.Count];
                    double value = negativeDisparity ? 1000 : 0;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        if (negativeDisparity)
                        {
                            if (i > 0 && bars[i].Volume < bars[i - 1].Volume && prices[i - 1] != 0)
                                value *= prices[i] / prices[i - 1];
                        }
                        else value += Math.Sign(prices[i] - (i == 0 ? 0 : prices[i - 1])) * bars[i].Volume;
                        volumeIndex[i] = value;
                    }
                    double[] Position(double[] values)
                    {
                        var means = Average(values, length, kind);
                        var variances = PopulationVariance(values, length);
                        return values.Select((v, i) => variances[i] == 0 ? 0 : .5 + (v - means[i]) / (4 * Math.Sqrt(variances[i]))).ToArray();
                    }
                    var pricePosition = Position(prices);
                    var volumePosition = Position(volumeIndex);
                    var line = pricePosition.Zip(volumePosition, (p, v) => v == -1 ? 0 : (1 + p) / (1 + v)).ToArray();
                    return Outputs((disparityKey, line), ("Signal", Average(line, Integer(options, "SignalLength", 4), kind)));
                });
            case IndicatorName.TradeVolumeIndex:
                if (kind == 0) return null;
                return new("Tvi", new[] { "Tvi", "Signal" }, bars =>
                {
                    var increments = bars.Select((b, i) =>
                    {
                        var change = b.Close - (i == 0 ? 0 : bars[i - 1].Close);
                        return Math.Abs(change) <= .5 ? 0 : Math.Sign(change) * b.Volume;
                    }).ToArray();
                    var line = Cumulative(increments);
                    return Outputs(("Tvi", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.VolumeFlowIndicator:
                if (kind == 0) return null;
                return new("Vfi", new[] { "Vfi", "Signal", "Histogram" }, bars =>
                {
                    var flowPeriod = Integer(options, "Length1", 130);
                    var variancePeriod = Integer(options, "Length2", 30);
                    var prices = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var returns = prices.Select((p, i) => i == 0 || p <= 0 || prices[i - 1] <= 0 ? 0 : Math.Log(p / prices[i - 1])).ToArray();
                    var deviations = PopulationVariance(returns, variancePeriod).Select(Math.Sqrt).ToArray();
                    var volume = Average(bars.Select(b => b.Volume).ToArray(), flowPeriod, kind);
                    var cutoffScale = Number(options, .2, "Coef");
                    var volumeCap = Number(options, 2.5, "Vcoef");
                    var flow = prices.Select((p, i) =>
                    {
                        if (i == 0) return 0;
                        var change = p - prices[i - 1];
                        var threshold = bars[i].Close * deviations[i] * cutoffScale;
                        var capped = Math.Min(bars[i].Volume, volumeCap * volume[i - 1]);
                        return change > threshold ? capped : change < -threshold ? -capped : 0;
                    }).ToArray();
                    var normalized = flow.Select((_, i) => volume[i] == 0 ? 0 : Window(flow, i, flowPeriod).Sum() / volume[i]).ToArray();
                    var line = Average(normalized, Integer(options, "SmoothLength", 3), kind);
                    var signal = Average(line, Integer(options, "SignalLength", 5), 3);
                    return Outputs(("Vfi", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, m) => v - m).ToArray()));
                });
            case IndicatorName.UpsideDownsideVolume:
                return Single("Udv", bars =>
                {
                    var signs = bars.Select((b, i) => Math.Sign(b.Close - (i == 0 ? 0 : bars[i - 1].Close)) * b.Volume).ToArray();
                    return signs.Select((_, i) =>
                    {
                        var values = Window(signs, i, length).ToArray();
                        var down = values.Where(v => v < 0).Sum();
                        return down == 0 ? 0 : values.Where(v => v > 0).Sum() / down;
                    }).ToArray();
                });
            case IndicatorName.UltimateVolatilityIndicator:
                return Single("Uvi", bars => bars.Select((_, i) => Window(bars, i, length).Sum(b => Math.Abs(b.Close - b.Open)) / length).ToArray());
            case IndicatorName.VolatilityRatio:
                return Single("Vr", bars =>
                {
                    var ranges = TrueRanges(bars);
                    return bars.Select((_, i) =>
                    {
                        if (i == 0) return 0;
                        var previous = Window(bars, i - 1, Math.Max(1, length - 1)).ToArray();
                        var high = previous.Max(b => b.High);
                        var low = previous.Min(b => b.Low);
                        var extra = i < length + 1 ? 0 : bars[i - length - 1].Close;
                        if (extra != 0) { high = Math.Max(high, extra); low = Math.Min(low, extra); }
                        return high == low ? 0 : ranges[i] / (high - low);
                    }).ToArray();
                });
            case IndicatorName.VolumePriceConfirmationIndicator:
                return new("Vpci", new[] { "Vpci", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var volume = bars.Select(b => b.Volume).ToArray();
                    double[] Weighted(int period) => prices.Select((_, i) =>
                    {
                        if (i + 1 < period) return 0;
                        var window = Window(bars, i, period).ToArray();
                        var total = window.Sum(b => b.Volume);
                        return total == 0 ? 0 : window.Sum(b => b.Close * b.Volume) / total;
                    }).ToArray();
                    var fast = Weighted(5);
                    var slow = Weighted(20);
                    var fastMean = Average(prices, 5, 1);
                    var slowMean = Average(prices, 20, 1);
                    var fastVolume = Average(volume, 5, 1);
                    var slowVolume = Average(volume, 20, 1);
                    var line = prices.Select((_, i) => fastMean[i] == 0 || slowVolume[i] == 0 ? 0
                        : (slow[i] - slowMean[i]) * fast[i] / fastMean[i] * fastVolume[i] / slowVolume[i]).ToArray();
                    return Outputs(("Vpci", line), ("Signal", Average(line, length, 1)));
                });
            case IndicatorName.VolumeAdaptiveBands:
                if (kind == 0) return null;
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var volumes = Average(bars.Select(b => b.Volume).ToArray(), length, kind);
                    double[] Expand(double sign) => prices.Select((_, i) =>
                    {
                        double value = 0, weight = 1;
                        for (var j = i; j >= 0; j--)
                        {
                            value += weight * prices[j];
                            weight *= sign / Math.Max(1, volumes[j]);
                        }
                        return value + weight * prices[0];
                    }).ToArray();
                    var upper = Average(Expand(1), length, kind);
                    var lower = Average(Expand(-1), length, kind);
                    return Outputs(("UpperBand", upper), ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()), ("LowerBand", lower));
                });
            case IndicatorName.VolumeAccumulationPercent:
                return Single("Vapc", bars => bars.Select((_, i) =>
                {
                    var window = Window(bars, i, length).ToArray();
                    var total = window.Sum(b => b.Volume);
                    var balance = window.Sum(b => b.High == b.Low ? 0 : b.Volume * (2 * (b.Close - b.Low) / (b.High - b.Low) - 1));
                    return total == 0 ? 0 : Clamp(100 * balance / total, -100, 100);
                }).ToArray());
            case IndicatorName.HawkeyeVolumeIndicator:
                var hawkeyeDivisor = Number(options, 3.6, "Divisor");
                return new("Up", new[] { "Up", "Dn" }, bars =>
                {
                    double[] Level(double sign) => bars.Select((_, i) => i == 0 ? 0 :
                        (bars[i - 1].High + bars[i - 1].Low) / 2
                        + (hawkeyeDivisor == 0 ? 0 : sign * (bars[i - 1].High - bars[i - 1].Low) / hawkeyeDivisor)).ToArray();
                    return Outputs(("Up", Level(1)), ("Dn", Level(-1)));
                });
            case IndicatorName.BetterVolumeIndicator:
                return Single("Bvi", bars => bars.Select((b, i) =>
                {
                    var previousClose = i == 0 ? b.Close : bars[i - 1].Close;
                    var span = Math.Max(b.High, previousClose) - Math.Min(b.Low, previousClose);
                    var body = b.Close - b.Open;
                    // Up/down allocation adds to the full volume; a doji divides volume equally.
                    if (body == 0) return b.Volume / 2;
                    var directionalShare = span / (2 * span - Math.Abs(body));
                    return b.Volume * (body > 0 ? directionalShare : 1 - directionalShare);
                }).ToArray());
            case IndicatorName.EarningSupportResistanceLevels:
                return Single("Esr", bars => bars.Select((b, i) => .5 * b.High + (i < 2 ? 0 : .5 * bars[i - 2].Low)).ToArray());
            case IndicatorName.VolumePositiveNegativeIndicator:
                if (kind == 0) return null;
                return new("Vpni", new[] { "Vpni", "Signal" }, bars =>
                {
                    var typical = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var volume = Average(bars.Select(b => b.Volume).ToArray(), length, kind);
                    var range = Average(TrueRanges(bars), length, kind);
                    var signedVolume = bars.Select((b, i) =>
                    {
                        var movement = typical[i] - (i == 0 ? 0 : typical[i - 1]);
                        return movement > range[i] / 10 ? b.Volume : movement < -range[i] / 10 ? -b.Volume : 0;
                    }).ToArray();
                    var line = signedVolume.Select((_, i) => 100 * Window(signedVolume, i, length).Sum()
                        / (length * (volume[i] > 0 ? volume[i] : 1))).ToArray();
                    return Outputs(("Vpni", line), ("Signal", Average(line, Integer(options, "SmoothLength", 3), kind)));
                });
            case IndicatorName.WilliamsAccumulationDistribution:
            case IndicatorName.SmoothedWilliamsAccumulationDistribution:
                var smoothedWad = name == IndicatorName.SmoothedWilliamsAccumulationDistribution;
                kind = AverageKind(options, 1);
                if (smoothedWad && kind == 0) return null;
                var wadKey = smoothedWad ? "Swad" : "Wad";
                return new(wadKey, smoothedWad ? new[] { wadKey, "Signal" } : new[] { wadKey }, bars =>
                {
                    var increments = bars.Select((bar, i) =>
                    {
                        if (i == 0 || bar.Close == bars[i - 1].Close) return 0;
                        var change = bar.Close - bars[i - 1].Close;
                        return change > 0 ? Math.Max(change, bar.Close - bar.Low) : Math.Min(change, bar.Close - bar.High);
                    }).ToArray();
                    var line = Cumulative(increments);
                    return smoothedWad ? Outputs((wadKey, line), ("Signal", Average(line, length, kind))) : Outputs((wadKey, line));
                });
            case IndicatorName.TwiggsMoneyFlow:
                if (kind == 0) return null;
                return Single("Tmf", bars =>
                {
                    var contributions = bars.Select((bar, i) =>
                    {
                        var prior = i == 0 ? 0 : bars[i - 1].Close;
                        var upper = Math.Max(bar.High, prior);
                        var lower = Math.Min(bar.Low, prior);
                        return upper == lower ? 0 : bar.Volume * (2 * (bar.Close - lower) / (upper - lower) - 1);
                    }).ToArray();
                    var numerator = Average(contributions, length, kind);
                    var denominator = Average(bars.Select(b => b.Volume).ToArray(), length, kind);
                    return numerator.Select((value, i) => denominator[i] == 0 ? 0 : value / denominator[i]).ToArray();
                });
            case IndicatorName.VolumeAccumulationOscillator:
                return Single("Vao", bars =>
                {
                    var contributions = bars.Select(b => b.Volume * ((b.Close - b.Low) - (b.High - b.Close)) / 2).ToArray();
                    return contributions.Select((_, i) => Window(contributions, i, length).Average()).ToArray();
                });
            case IndicatorName.OnBalanceVolumeModified:
            case IndicatorName.OnBalanceVolumeReflex:
            case IndicatorName.MultiVoteOnBalanceVolume:
            case IndicatorName.ModifiedPriceVolumeTrend:
                var modifiedObv = name == IndicatorName.OnBalanceVolumeModified;
                var reflexObv = name == IndicatorName.OnBalanceVolumeReflex;
                var multiVote = name == IndicatorName.MultiVoteOnBalanceVolume;
                kind = AverageKind(options, modifiedObv || multiVote ? 3 : 1);
                if (kind == 0) return null;
                var obvKey = modifiedObv ? "Obvm" : reflexObv ? "Obvr" : multiVote ? "Mvo" : "Mpvt";
                return new(obvKey, new[] { obvKey, "Signal" }, bars =>
                {
                    var increments = bars.Select((bar, i) =>
                    {
                        var lag = reflexObv ? length : 1;
                        var previous = i < lag ? 0 : bars[i - lag].Close;
                        if (modifiedObv || reflexObv) return Math.Sign(bar.Close - previous) * bar.Volume;
                        if (multiVote) return bar.Volume / 1000000 * (Math.Sign(bar.Close - previous)
                            + Math.Sign(bar.High - (i == 0 ? 0 : bars[i - 1].High))
                            + Math.Sign(bar.Low - (i == 0 ? 0 : bars[i - 1].Low)));
                        return previous == 0 ? 0 : bar.Volume / 50000 * (bar.Close - previous) / previous;
                    }).ToArray();
                    var line = Cumulative(increments);
                    // Modified PVT restarts after an undefined return from a zero price.
                    if (!modifiedObv && !reflexObv && !multiVote)
                    {
                        double offset = 0;
                        for (var i = 1; i < line.Length; i++)
                        {
                            if (bars[i - 1].Close == 0) offset = line[i];
                            line[i] -= offset;
                        }
                    }
                    if (modifiedObv) line = Average(line, Integer(options, "Length1", 7), kind);
                    var signalPeriod = modifiedObv ? Integer(options, "Length2", 10)
                        : reflexObv ? Integer(options, "SignalLength", 14) : length;
                    return Outputs((obvKey, line), ("Signal", Average(line, signalPeriod, kind)));
                });
            case IndicatorName.FiniteVolumeElements:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var volumeThreshold = Number(options, .3, "Factor");
                return Single("Fve", bars =>
                {
                    var volumeAverage = Average(bars.Select(b => b.Volume).ToArray(), length, kind);
                    var increments = bars.Select((b, i) =>
                    {
                        // Expand close-midpoint + typical-price change into a single numerator.
                        var priorTypical = i == 0 ? 0 : (bars[i - 1].High + bars[i - 1].Low + bars[i - 1].Close) / 3;
                        var flow = (8 * b.Close - b.High - b.Low) / 6 - priorTypical;
                        var threshold = volumeThreshold * b.Close / 100;
                        var direction = flow > threshold ? 1 : flow < -threshold ? -1 : 0;
                        return volumeAverage[i] == 0 ? 0 : direction * 100 * b.Volume / (length * volumeAverage[i]);
                    }).ToArray();
                    return Cumulative(increments);
                });
            case IndicatorName.BuffAverage:
                return new("FastBuff", new[] { "FastBuff", "SlowBuff" }, bars =>
                {
                    double[] Weighted(int period) => bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, period).ToArray();
                        var volume = window.Sum(b => b.Volume);
                        return volume == 0 ? 0 : window.Sum(b => b.Close * b.Volume) / volume;
                    }).ToArray();
                    return Outputs(("FastBuff", Weighted(length)), ("SlowBuff", Weighted(20)));
                });
            case IndicatorName.RelativeVolumeIndicator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Rvi", new[] { "Rvi", "Dpl" }, bars =>
                {
                    var volumes = bars.Select(b => (double)b.Volume).ToArray();
                    var center = Average(volumes, length, kind);
                    var variance = PopulationVariance(volumes, length);
                    var score = volumes.Select((v, i) => variance[i] == 0 ? 0 : (v - center[i]) / Math.Sqrt(variance[i])).ToArray();
                    var demand = new double[bars.Count];
                    var lastTrigger = -1;
                    for (var i = 0; i < demand.Length; i++)
                    {
                        if (score[i] >= 2) lastTrigger = i;
                        demand[i] = lastTrigger < 0 ? bars[0].Close : lastTrigger == 0 ? 0 : bars[lastTrigger - 1].Close;
                    }
                    return Outputs(("Rvi", score), ("Dpl", demand));
                });
            case IndicatorName.PriceVolumeRank:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Pvr", new[] { "Pvr", "FastSignal", "SlowSignal" }, bars =>
                {
                    // Rows are price up/not-up, columns are volume up/not-up.
                    var quadrant = new double[,] { { 1, 2 }, { 4, 3 } };
                    var rank = bars.Select((b, i) => quadrant[b.Close > (i == 0 ? 0 : bars[i - 1].Close) ? 0 : 1,
                        b.Volume > (i == 0 ? 0 : bars[i - 1].Volume) ? 0 : 1]).ToArray();
                    return Outputs(("Pvr", rank), ("FastSignal", Average(rank, Integer(options, "FastLength", 5), kind)),
                        ("SlowSignal", Average(rank, Integer(options, "SlowLength", 10), kind)));
                });
            case IndicatorName.AverageMoneyFlowOscillator:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                return Single("Amfo", bars =>
                {
                    var volume = Average(bars.Select(b => (double)b.Volume).ToArray(), length, kind);
                    var movement = Average(bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray(), length, kind);
                    var flow = movement.Select((v, i) => v == 0 || volume[i] == 0 ? 0
                        : Math.Sign(v) * (Math.Log(Math.Abs(v)) + Math.Log(Math.Abs(volume[i])))).ToArray();
                    var position = flow.Select((v, i) =>
                    {
                        var window = Window(flow, i, length).ToArray();
                        var low = window.Min();
                        var high = window.Max();
                        return high == low ? -100 : 200 * (v - low) / (high - low) - 100;
                    }).ToArray();
                    return Average(position, Integer(options, "SmoothLength", 3), kind);
                });
            case IndicatorName.CumulativeVolumeIndex:
                return Single("Cvi", bars =>
                {
                    var result = new double[bars.Count];
                    for (var i = 1; i < result.Length; i++)
                        result[i] = result[i - 1] + Math.Sign(bars[i].Close - bars[i - 1].Close) * (double)bars[i].Volume;
                    return result;
                });
            case IndicatorName.PriceMomentum:
                return Single("Pm", bars => bars.Select((b, i) => i < length ? 0 : b.Close - bars[i - length].Close).ToArray());
            case IndicatorName.PpoMovingAverage:
                return Single("PpoMa", bars =>
                {
                    var fast = Average(Closes(bars), Integer(options, "FastLength", 12), 3);
                    var slow = Average(Closes(bars), Integer(options, "SlowLength", 26), 3);
                    return fast.Select((v, i) => slow[i] == 0 ? 0 : 100 * (v / slow[i] - 1)).ToArray();
                });
            case IndicatorName.HighestHigh:
                return Single("HighestHigh", bars => bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray());
            case IndicatorName.LowestLow:
                return Single("LowestLow", bars => bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray());
            case IndicatorName.VolumeMomentum:
                return Single("VolumeMomentum", bars => bars.Select((b, i) => i < length ? 0
                    : (double)b.Volume - (double)bars[i - length].Volume).ToArray());
            case IndicatorName.NormalizedVolume:
                return Single("NormalizedVolume", bars => bars.Select((b, i) =>
                {
                    if (i + 1 < length) return 0;
                    var total = Window(bars, i, length).Sum(v => (double)v.Volume);
                    return total == 0 ? 0 : length * (double)b.Volume / total;
                }).ToArray());
            case IndicatorName.MarketFacilitationIndex:
                return Single("Mi", bars => bars.Select(b => b.Volume == 0 ? 0 : (b.High - b.Low) / (double)b.Volume).ToArray());
            case IndicatorName.TFSVolumeOscillator:
                return Single("Tfsvo", bars => bars.Select((_, i) =>
                    Window(bars, i, length).Sum(b => Math.Sign(b.Close - b.Open) * (double)b.Volume) / length).ToArray());
            case IndicatorName.VolumeMomentumOscillator:
                var shortPeriod = Integer(options, "ShortLength", 5);
                var longPeriod = Integer(options, "LongLength", 20);
                return Single("Vmo", bars =>
                {
                    // This oscillator seeds at the first volume, unlike the cumulative-mean
                    // initialization used by the general EMA. Expand its exponential weights.
                    double Mean(int end, int period)
                    {
                        var alpha = 2d / (period + 1d);
                        return (double)bars[0].Volume * Math.Pow(1 - alpha, end)
                            + Enumerable.Range(1, end).Sum(j => (double)bars[j].Volume * alpha * Math.Pow(1 - alpha, end - j));
                    }
                    return bars.Select((_, i) =>
                    {
                        var denominator = Mean(i, longPeriod);
                        return denominator == 0 ? 0 : 100 * (Mean(i, shortPeriod) / denominator - 1);
                    }).ToArray();
                });
            case IndicatorName.VolumeZoneOscillator:
            case IndicatorName.PriceZoneOscillator:
                if (name == IndicatorName.VolumeZoneOscillator) kind = 3;
                if (kind == 0) return null;
                var zoneKey = name == IndicatorName.VolumeZoneOscillator ? "Vzo" : "Pzo";
                return Single(zoneKey, bars =>
                {
                    var weights = bars.Select(b => name == IndicatorName.VolumeZoneOscillator ? (double)b.Volume : b.Close).ToArray();
                    var signed = weights.Select((v, i) => i == 0 ? 0 : v * (name == IndicatorName.VolumeZoneOscillator
                        ? bars[i].Close > bars[i - 1].Close ? 1 : -1 : Math.Sign(bars[i].Close - bars[i - 1].Close))).ToArray();
                    var total = Average(weights, length, kind);
                    var directional = Average(signed, length, kind);
                    return directional.Select((v, i) => total[i] == 0 ? 0 : 100 * v / total[i]).ToArray();
                });
            case IndicatorName.ChaikinOscillator:
                if (kind == 0) return null;
                var fast = Integer(options, "FastLength", 3);
                var slow = Integer(options, "SlowLength", 10);
                return Single("ChaikinOsc", bars =>
                {
                    var money = Cumulative(bars.Select(MoneyFlowVolume).ToArray());
                    var fastAverage = Average(money, fast, kind);
                    var slowAverage = Average(money, slow, kind);
                    return fastAverage.Select((v, i) => v - slowAverage[i]).ToArray();
                });
            case IndicatorName.VolumeOscillator:
                return Single("Vo", bars =>
                {
                    var volumes = bars.Select(b => (double)b.Volume).ToArray();
                    var fastAverage = Average(volumes, Integer(options, "FastLength", 5), 1);
                    var slowAverage = Average(volumes, Integer(options, "SlowLength", length), 1);
                    return fastAverage.Select((v, i) => slowAverage[i] == 0 ? 0 : 100 * (v / slowAverage[i] - 1)).ToArray();
                });
            case IndicatorName.CumulativeSum:
                return Single("CumulativeSum", bars => Cumulative(Closes(bars)));
            case IndicatorName.RollingMax:
                return Single("RollingMax", bars => bars.Select((_, i) => Window(bars, i, length).Max(b => b.Close)).ToArray());
            case IndicatorName.RollingMin:
                return Single("RollingMin", bars => bars.Select((_, i) => Window(bars, i, length).Min(b => b.Close)).ToArray());
            case IndicatorName.SimpleReturns:
            case IndicatorName.LogReturns:
                return Single("Returns", bars => bars.Select((b, i) =>
                {
                    if (i < length) return 0;
                    var previous = bars[i - length].Close;
                    return name == IndicatorName.SimpleReturns ? previous == 0 ? 0 : b.Close / previous - 1
                        : previous <= 0 || b.Close <= 0 ? 0 : Math.Log(b.Close) - Math.Log(previous);
                }).ToArray());
            case IndicatorName.NetVolume:
                return Single("NetVolume", bars => bars.Select((b, i) => i == 0 ? 0
                    : Math.Sign(b.Close - bars[i - 1].Close) * (double)b.Volume).ToArray());
            case IndicatorName.VolumeRateOfChange:
                return Single("Vroc", bars => bars.Select((b, i) => i < length || bars[i - length].Volume == 0 ? 0
                    : 100 * ((double)b.Volume / (double)bars[i - length].Volume - 1)).ToArray());
            case IndicatorName.ChaikinMoneyFlow:
                return Single("Cmf", bars => bars.Select((_, i) =>
                {
                    var window = Window(bars, i, length).ToArray();
                    var volume = window.Sum(b => (double)b.Volume);
                    return volume == 0 ? 0 : window.Sum(MoneyFlowVolume) / volume;
                }).ToArray());
            case IndicatorName.VolumeWeightedAveragePrice:
                return Single("Vwap", RoundedVolumeWeightedPrice);
            case IndicatorName.EaseOfMovement:
                var divisor = Number(options, 1000000, "Divisor");
                return Single("Eom", bars => bars.Select((b, i) => i == 0 || b.Volume == 0 ? 0
                    : divisor * ((b.High + b.Low) / 2 - (bars[i - 1].High + bars[i - 1].Low) / 2)
                        * (b.High - b.Low) / (double)b.Volume).ToArray());
            case IndicatorName.ForceIndex:
                if (kind == 0) return null;
                return Single("Fi", bars => Average(bars.Select((b, i) => i == 0 ? 0
                    : (b.Close - bars[i - 1].Close) * (double)b.Volume).ToArray(), length, kind));
            case IndicatorName.PriceVolumeTrend:
                if (kind == 0) return null;
                return new("Pvt", new[] { "Pvt", "Signal" }, bars =>
                {
                    var increments = bars.Select((b, i) => i == 0 || bars[i - 1].Close == 0 ? 0
                        : (b.Close / bars[i - 1].Close - 1) * (double)b.Volume).ToArray();
                    var line = Cumulative(increments);
                    return Outputs(("Pvt", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.NegativeVolumeIndex:
            case IndicatorName.PositiveVolumeIndex:
                if (kind == 0) return null;
                var key = name == IndicatorName.NegativeVolumeIndex ? "Nvi" : "Pvi";
                var initial = Integer(options, "InitialValue", 1000);
                return new(key, new[] { key, key + "Signal" }, bars =>
                {
                    var line = new double[bars.Count];
                    double level = initial;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        if (i > 0 && bars[i - 1].Close != 0 && (name == IndicatorName.NegativeVolumeIndex
                            ? bars[i].Volume < bars[i - 1].Volume : bars[i].Volume > bars[i - 1].Volume))
                            level *= bars[i].Close / bars[i - 1].Close;
                        line[i] = level;
                    }
                    return Outputs((key, line), (key + "Signal", Average(line, length, kind)));
                });
            case IndicatorName.DonchianChannelWidth:
                if (kind == 0) return null;
                var smoothLength = Integer(options, "SmoothLength", 22);
                return new("Dcw", new[] { "Dcw", "Signal" }, bars =>
                {
                    var width = bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        return window.Max(b => b.High) - window.Min(b => b.Low);
                    }).ToArray();
                    return Outputs(("Dcw", width), ("Signal", Average(width, smoothLength, kind)));
                });
            default: return null;
        }
    }

    private static double MoneyFlowVolume(Bar bar) => bar.High == bar.Low ? 0
        : (2 * bar.Close - bar.High - bar.Low) / (bar.High - bar.Low) * (double)bar.Volume;
}
