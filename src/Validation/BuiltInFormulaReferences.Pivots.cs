using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Pivots(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name == IndicatorName.PivotPointAverage)
        {
            var options = (OoplesFinance.StockIndicators.Builder.Specs.PivotPointAverageSpecOptions)indicator.CreateOptions();
            var kind = AverageKind(options, 1);
            if (kind == 0) return null;
            var averageKeys = new[] { "Pivot1", "Signal1", "Pivot2", "Signal2", "Pivot3", "Signal3" };
            return new("Pivot1", averageKeys, bars =>
            {
                // Group by full calendar period, then expand each known-at-open level to its bars.
                DateTime Key(DateTime time)
                {
                    if (options.InputLength == InputLength.Year) return new DateTime(time.Year, 1, 1);
                    if (options.InputLength == InputLength.Month) return new DateTime(time.Year, time.Month, 1);
                    if (options.InputLength == InputLength.Week)
                    {
                        var date = time.Date;
                        while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(-1);
                        return date;
                    }
                    var unit = options.InputLength == InputLength.Minute ? TimeSpan.TicksPerMinute
                        : options.InputLength == InputLength.Hour ? TimeSpan.TicksPerHour : TimeSpan.TicksPerDay;
                    return new DateTime(time.Ticks / unit * unit);
                }
                var groups = bars.Select((bar, index) => (bar, index)).GroupBy(x => Key(x.bar.Time)).ToArray();
                var output = averageKeys.ToDictionary(key => key, _ => new double[bars.Count]);
                var pivots = new[] { new double[groups.Length], new double[groups.Length], new double[groups.Length] };
                for (var g = 0; g < groups.Length; g++)
                {
                    var previous = g == 0 ? null : groups[g - 1].Select(x => x.bar).ToArray();
                    var high = previous?.Max(b => b.High) ?? 0;
                    var low = previous?.Min(b => b.Low) ?? 0;
                    var close = previous?.Last().Close ?? 0;
                    var open = groups[g].First().bar.Open;
                    pivots[0][g] = (high + low + close) / 3;
                    pivots[1][g] = (high + low + close + open) / 4;
                    pivots[2][g] = (high + low + open) / 3;
                }
                for (var slot = 0; slot < 3; slot++)
                {
                    var signal = Average(pivots[slot], options.Length, kind);
                    for (var g = 0; g < groups.Length; g++)
                        foreach (var entry in groups[g])
                        {
                            output[averageKeys[slot * 2]][entry.index] = pivots[slot][g];
                            output[averageKeys[slot * 2 + 1]][entry.index] = signal[g];
                        }
                }
                return output;
            });
        }

        if (name != IndicatorName.FloorPivotPoints && name != IndicatorName.FibonacciPivotPoints
            && name != IndicatorName.WoodiePivotPoints && name != IndicatorName.DemarkPivotPoints
            && name != IndicatorName.CamarillaPivotPoints && name != IndicatorName.StandardPivotPoints
            && name != IndicatorName.DynamicPivotPoints) return null;
        // These built-in options publish daily levels. No production aggregation helper is used.
        var levels = name == IndicatorName.CamarillaPivotPoints ? 5 : name == IndicatorName.WoodiePivotPoints ? 4
            : name is IndicatorName.DemarkPivotPoints or IndicatorName.DynamicPivotPoints ? 1 : 3;
        var midpoints = name == IndicatorName.WoodiePivotPoints ? 4 : name is IndicatorName.DemarkPivotPoints or IndicatorName.DynamicPivotPoints ? 0 : 6;
        var keys = new[] { "Pivot" }.Concat(Enumerable.Range(1, levels).Select(j => "S" + j))
            .Concat(Enumerable.Range(1, levels).Select(j => "R" + j))
            .Concat(Enumerable.Range(1, midpoints).Select(j => "M" + j)).ToArray();
        return new("Pivot", keys, bars =>
        {
            var output = keys.ToDictionary(key => key, _ => new double[bars.Count]);
            var current = keys.ToDictionary(key => key, _ => 0d);
            double open = 0, high = 0, low = 0, close = 0;
            for (var i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                if (i == 0 || b.Time.Date != bars[i - 1].Time.Date)
                {
                    if (i > 0) ComputePreviousSession();
                    open = b.Open; high = b.High; low = b.Low;
                }
                else { high = Math.Max(high, b.High); low = Math.Min(low, b.Low); }
                close = b.Close;
                foreach (var key in keys) output[key][i] = current[key];
            }
            return output;

            void ComputePreviousSession()
            {
                var range = high - low;
                var pivot = name == IndicatorName.StandardPivotPoints ? (open + high + low + close) / 4
                    : name == IndicatorName.WoodiePivotPoints ? (high + low + 2 * close) / 4
                    : name == IndicatorName.DemarkPivotPoints
                        ? (high + low + close + (close < open ? low : close > open ? high : close)) / 4
                        : (high + low + close) / 3;
                current["Pivot"] = pivot;
                if (name == IndicatorName.CamarillaPivotPoints)
                {
                    var divisors = new[] { 12d, 6, 4, 2 };
                    for (var j = 1; j <= 4; j++)
                    {
                        current["S" + j] = close - 1.1 * range / divisors[j - 1];
                        current["R" + j] = close + 1.1 * range / divisors[j - 1];
                    }
                    current["R5"] = low == 0 ? 0 : high * close / low;
                    current["S5"] = 2 * close - current["R5"];
                    Mid(1, "S3", "S2"); Mid(2, "S2", "S1"); Mid(3, "R2", "R1");
                    Mid(4, "R3", "R2"); Mid(5, "R3", "R4"); Mid(6, "S4", "S3");
                    return;
                }
                if (name == IndicatorName.FibonacciPivotPoints)
                {
                    var factors = new[] { .382, (Math.Sqrt(5) - 1) / 2, 1 };
                    for (var j = 1; j <= 3; j++)
                    {
                        current["S" + j] = pivot - factors[j - 1] * range;
                        current["R" + j] = pivot + factors[j - 1] * range;
                    }
                }
                else
                {
                    current["S1"] = 2 * pivot - high;
                    current["R1"] = 2 * pivot - low;
                    if (levels == 1) return;
                    current["S2"] = pivot - range; current["R2"] = pivot + range;
                    current["S3"] = name == IndicatorName.StandardPivotPoints ? pivot - range : current["S1"] - range;
                    current["R3"] = name == IndicatorName.StandardPivotPoints ? pivot + range : current["R1"] + range;
                    if (levels == 4)
                    {
                        current["S4"] = current["S3"] - range; current["R4"] = current["R3"] + range;
                        Mid(1, "S1", "S2"); Mid(2, "Pivot", "S1"); Mid(3, "R1", "Pivot"); Mid(4, "R1", "R2");
                        return;
                    }
                }
                Mid(1, "S3", "S2"); Mid(2, "S2", "S1"); Mid(3, "S1", "Pivot");
                Mid(4, "R1", "Pivot"); Mid(5, "R2", "R1"); Mid(6, "R3", "R2");
            }
            void Mid(int number, string first, string second) => current["M" + number] = (current[first] + current[second]) / 2;
        });
    }
}
