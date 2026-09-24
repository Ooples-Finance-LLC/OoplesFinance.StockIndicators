using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? CompositeMomentum(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 1);
        switch (indicator.BatchName)
        {
            case IndicatorName.TechnicalRank:
                return new("Tr", new[] { "Tr" }, bars =>
                {
                    int Period(int n) => Integer(options, "Length"+n);
                    var prices = Closes(bars);
                    var longMean = Average(prices, Period(1), 1);
                    var mediumMean = Average(prices, Period(3), 1);
                    var fast = Average(prices, Period(5), 3); var slow = Average(prices, Period(6), 3);
                    var ppo = fast.Select((v, i) => slow[i] == 0 ? 0 : 100*(v/slow[i]-1)).ToArray();
                    var ppoSignal = Average(ppo, Period(7), 3);
                    var histogram = ppo.Select((v, i) => v-ppoSignal[i]).ToArray();
                    var strength = MotionRsi(prices, Period(9));
                    double Return(int i, int period) => i < period || prices[i-period] == 0 ? 0 : prices[i]/prices[i-period]-1;
                    return Outputs(("Tr", prices.Select((v, i) =>
                    {
                        // Returns are dimensionless here: convert each weighted return
                        // to percentage points exactly once.
                        var longTerm = 30*(Return(i, Period(2))+(longMean[i] == 0 ? 0 : v/longMean[i]-1));
                        var mediumTerm = 15*(Return(i, Period(4))+(mediumMean[i] == 0 ? 0 : v/mediumMean[i]-1));
                        var impulse = i < Period(8) ? 0 : (histogram[i]-histogram[i-Period(8)])/Period(8);
                        return Clamp(longTerm+mediumTerm+5*impulse+.05*strength[i], 0, 100);
                    }).ToArray()));
                });
            case IndicatorName.InsyncIndex:
                return new("Iidx", new[] { "Iidx" }, bars =>
                {
                    int Period(string name) => Integer(options, name);
                    var prices = Closes(bars); var count = bars.Count;
                    var typical = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var rsi = MotionRsi(prices, Period("RsiLength"));
                    var cci = typical.Select((price, i) =>
                    {
                        if (i + 1 < Period("CciLength")) return 0;
                        var window = Window(typical, i, Period("CciLength")).ToArray();
                        var mean = window[0] + window.Average(v => v - window[0]);
                        var distance = window.Average(v => Math.Abs(v - mean));
                        return distance == 0 ? 0 : (price - mean) / (.015 * distance);
                    }).ToArray();
                    var positive = typical.Select((v, i) => i > 0 && v > typical[i - 1] ? v * bars[i].Volume : 0).ToArray();
                    var negative = typical.Select((v, i) => i > 0 && v < typical[i - 1] ? v * bars[i].Volume : 0).ToArray();
                    var mfi = prices.Select((_, i) =>
                    {
                        var up = Window(positive, i, Period("MfiLength")).Sum();
                        var down = Window(negative, i, Period("MfiLength")).Sum();
                        return down == 0 ? 100 : 100 * up / (up + down);
                    }).ToArray();
                    var fast = Average(prices, Period("FastLength"), 3); var slow = Average(prices, Period("SlowLength"), 3);
                    var macd = fast.Select((v, i) => v - slow[i]).ToArray();
                    var middle = Average(prices, Period("BbLength"), 1);
                    var widths = PopulationVariance(prices, Period("BbLength")).Select(v => Number(options, 2, "StdDevMult") * Math.Sqrt(v)).ToArray();
                    var position = prices.Select((v, i) => widths[i] == 0 ? 0 : 50 + 50 * (v - middle[i]) / widths[i]).ToArray();
                    var dpoAverage = Average(prices, Period("DpoLength"), 1);
                    var delay = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Period("DpoLength") / 2d + 1)));
                    var dpo = dpoAverage.Select((v, i) => (i < delay ? 0 : prices[i - delay]) - v).ToArray();
                    var roc = prices.Select((v, i) => i < Period("RocLength") || prices[i - Period("RocLength")] == 0 ? 0 : 100 * (v / prices[i - Period("RocLength")] - 1)).ToArray();
                    var emv = bars.Select((b, i) => i == 0 || b.Volume == 0 ? 0 : Number(options, 10000, "Divisor")
                        * ((b.High + b.Low - bars[i - 1].High - bars[i - 1].Low) / 2) * (b.High - b.Low) / b.Volume).ToArray();
                    var stochastic = bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, Period("StochLength")).ToArray();
                        var low = window.Min(x => x.Low); var high = window.Max(x => x.High);
                        return high == low ? 0 : 100 * (b.Close - low) / (high - low); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var k = Average(stochastic, Period("StochKLength"), 1);
                    var d = Average(k, Period("StochDLength"), 1);
                    // Explicit numerical tie contract for discrete voting, independent of production helpers.
                    int Order(double a, double b) => Math.Abs(a - b) <= 1e-12 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))) ? 0 : a > b ? 1 : -1;
                    double[] DirectionScores(double[] values) => values.Select((v, i) =>
                    {
                        var mean = Window(values, i, Period("SmaLength")).Average();
                        return Order(mean, 0) > 0 && Order(v, mean) >= 0 ? 5d : Order(mean, 0) < 0 && Order(v, mean) < 0 ? -5d : 0;
                    }).ToArray();
                    var emvScore = DirectionScores(emv); var macdScore = DirectionScores(macd);
                    var rocScore = DirectionScores(roc); var dpoBuy = DirectionScores(dpo);
                    var dpoSell = dpo.Select((v, i) =>
                    {
                        var mean = Window(dpo, i, Period("SmaLength")).Average();
                        return Order(mean, 0) > 0 && Order(v, mean) > 0 ? 5d : Order(mean, 0) < 0 && Order(v, mean) <= 0 ? -5d : 0;
                    }).ToArray();
                    double Threshold(double value, double lower, double upper) => Order(value, lower) < 0 ? -5 : Order(value, upper) > 0 ? 5 : 0;
                    return Outputs(("Iidx", prices.Select((_, i) => 50 + Threshold(position[i], 5, 95) + Threshold(cci[i], -100, 100)
                        + Threshold(rsi[i], 30, 70) + Threshold(mfi[i], 20, 80) + Threshold(k[i], 20, 80) + Threshold(d[i], 20, 80)
                        + emvScore[i] + macdScore[i] + rocScore[i]
                        + (i < Period("SmaLength") ? 0 : dpoBuy[i - Period("SmaLength")] + dpoSell[i - Period("SmaLength")])).ToArray()));
                });
            case IndicatorName.EmaWaveIndicator:
                return new("Wa", new[] { "Wa", "Wb", "Wc" }, bars =>
                {
                    var prices = Closes(bars);
                    double[] Wave(int period)
                    {
                        var average = Average(prices, period, 3);
                        return Average(prices.Select((v, i) => v - average[i]).ToArray(), Integer(options, "SmoothLength", 4), 1);
                    }
                    return Outputs(("Wa", Wave(Integer(options, "Length1", 5))),
                        ("Wb", Wave(Integer(options, "Length2", 25))), ("Wc", Wave(Integer(options, "Length3", 50))));
                });
            case IndicatorName.FunctionToCandles:
                var candleKind = AverageKind(options, 6);
                if (candleKind == 0) return null;
                return new("Close", new[] { "Close", "Open", "High", "Low" }, bars =>
                {
                    double[] Transform(Func<Bar, double> select)
                    {
                        var prices = bars.Select(select).ToArray();
                        var changes = prices.Select((v, i) => i == 0 ? 0 : v - prices[i - 1]).ToArray();
                        var up = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, candleKind);
                        var down = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, candleKind);
                        return up.Select((u, i) => down[i] == 0 ? 100 : 100 * u / (u + down[i])).ToArray();
                    }
                    return Outputs(("Close", Transform(b => b.Close)), ("Open", Transform(b => b.Open)),
                        ("High", Transform(b => b.High)), ("Low", Transform(b => b.Low)));
                });
            case IndicatorName.MomentaRelativeStrengthIndex:
                var momentaKind = AverageKind(options, 3);
                if (momentaKind == 0) return null;
                return new("Mrsi", new[] { "Mrsi", "Signal" }, bars =>
                {
                    var rangePeriod = Integer(options, "Length1", 2);
                    var period = Integer(options, "Length2", 14);
                    var above = bars.Select((b, i) => b.Close - Window(bars, i, rangePeriod).Min(v => v.Close)).ToArray();
                    var below = bars.Select((b, i) => Window(bars, i, rangePeriod).Max(v => v.Close) - b.Close).ToArray();
                    var up = Average(above, period, momentaKind);
                    var down = Average(below, period, momentaKind);
                    var line = up.Select((u, i) => down[i] == 0 ? 100 : 100 * u / (u + down[i])).ToArray();
                    return Outputs(("Mrsi", line), ("Signal", Average(line, period, momentaKind)));
                });
            case IndicatorName.SelfAdjustingRelativeStrengthIndex:
                if (kind == 0) return null;
                return new("SaRsi", new[] { "SaRsi", "Signal", "ObLevel", "OsLevel" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var up = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, kind);
                    var down = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
                    var line = up.Select((u, i) => down[i] == 0 ? 100 : 100 * u / (u + down[i])).ToArray();
                    var width = PopulationVariance(line, length).Select(v => Number(options, 2, "Mult") * Math.Sqrt(v)).ToArray();
                    return Outputs(("SaRsi", line), ("Signal", Average(line, Integer(options, "SmoothingLength", 21), kind)),
                        ("ObLevel", width.Select(v => 50 + v).ToArray()), ("OsLevel", width.Select(v => 50 - v).ToArray()));
                });
            case IndicatorName.PerformanceIndex:
                return new("PerformanceIndex", new[] { "PerformanceIndex" }, bars =>
                    Outputs(("PerformanceIndex", PercentageReturn(Closes(bars), length))));
            case IndicatorName.KnowSureThing:
                if (kind == 0) return null;
                var rocPeriods = new[] { 10, 15, 20, 30 }.Select((period, i) => Integer(options, "RocLength" + (i + 1), period)).ToArray();
                var smoothPeriods = new[] { Integer(options, "Length1", Integer(options, "Length", 10)),
                    Integer(options, "Length2", 10), Integer(options, "Length3", 10), Integer(options, "Length4", 15) };
                return new("Kst", new[] { "Kst", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var legs = rocPeriods.Select((period, i) => Average(PercentageReturn(prices, period), smoothPeriods[i], kind)).ToArray();
                    var line = prices.Select((_, i) => Enumerable.Range(0, 4).Sum(leg => (leg + 1) * legs[leg][i])).ToArray();
                    return Outputs(("Kst", line), ("Signal", Average(line, Integer(options, "SignalLength", 9), kind)));
                });
            case IndicatorName.PringSpecialK:
                if (kind == 0) return null;
                var periods = new[] { 10, 15, 20, 30, 40, 50, 65, 75, 100, 130, 195, 265, 390, 530 }
                    .Select((period, i) => Integer(options, "Length" + (i + 1), period)).ToArray();
                // Pring combines short, intermediate, and long horizons, each weighted 1:2:3:4.
                var lookbacks = new[] { 0, 1, 2, 3, 4, 6, 7, 8, 10, 11, 12, 13 };
                var smoothing = new[] { 0, 0, 0, 1, 5, 6, 7, 8, 9, 9, 9, 10 };
                return new("PringSpecialK", new[] { "PringSpecialK", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var legs = lookbacks.Select((slot, i) => Average(PercentageReturn(prices, periods[slot]), periods[smoothing[i]], kind)).ToArray();
                    var line = prices.Select((_, i) => Enumerable.Range(0, legs.Length).Sum(leg => (leg % 4 + 1) * legs[leg][i])).ToArray();
                    return Outputs(("PringSpecialK", line), ("Signal", Average(line, Integer(options, "SmoothLength", 10), kind)));
                });
            case IndicatorName.InternalBarStrengthIndicator:
                return new("Ibs", new[] { "Ibs", "Signal" }, bars =>
                {
                    var position = bars.Select(b => b.High == b.Low ? 0 : 100 * (b.Close - b.Low) / (b.High - b.Low)).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    var line = position.Select((_, i) => Window(position, i, length).Average()).ToArray();
                    // Fixed signal period three, EMA started at zero rather than the general EMA seed.
                    var signal = line.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => Math.Pow(.5, i - j + 1) * line[j])).ToArray();
                    return Outputs(("Ibs", line), ("Signal", signal));
                });
            case IndicatorName.PercentChangeOscillator:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                return new("Pcco", new[] { "Pcco", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var line = new double[bars.Count];
                    var segmentStart = 1;
                    for (var i = 1; i < line.Length; i++)
                    {
                        if (prices[i - 1] == 0) { segmentStart = i + 1; continue; }
                        line[i] = Enumerable.Range(segmentStart, i - segmentStart + 1)
                            .Sum(j => (prices[j] - prices[j - 1]) / prices[j - 1]);
                    }
                    return Outputs(("Pcco", line), ("Signal", Average(line, length, kind)));
                });
            default: return null;
        }
    }

    private static double[] PercentageReturn(double[] prices, int length) => prices.Select((price, i) =>
        i < length || prices[i - length] == 0 ? 0 : 100 * (price / prices[i - length] - 1)).ToArray();
}
