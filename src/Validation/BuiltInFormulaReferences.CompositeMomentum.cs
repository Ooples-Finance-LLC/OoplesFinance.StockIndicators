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
                return new("Iidx", new[] { "Iidx" }, bars => InsyncOutputs(bars, indicator));
            case IndicatorName.EmaWaveIndicator:
                return new("Wa", new[] { "Wa", "Wb", "Wc" }, bars => ResidualPressureOutputs(bars, indicator));
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
                if (momentaKind is 1 or 2 or 3 or 6) return new("Mrsi", new[] { "Mrsi", "Signal" }, bars => RangeGainLossOutputs(bars, indicator));
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
                if (kind is 1 or 2 or 3 or 6) return new("SaRsi", new[] { "SaRsi", "Signal", "ObLevel", "OsLevel" }, bars => SelfAdjustingRsiOutputs(bars, indicator));
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
                if (kind is 1 or 2 or 3 or 6) return new("Kst", new[] { "Kst", "Signal" }, bars => RocBankOutputs(bars, indicator));
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
                if (kind is 1 or 2 or 3 or 6) return new("PringSpecialK", new[] { "PringSpecialK", "Signal" }, bars => RocBankOutputs(bars, indicator));
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
