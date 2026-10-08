namespace OoplesFinance.StockIndicators.Helpers;
internal static class DailyPivotMath
{
    private static RocBankValue Combine(RocBankValue a, RocBankValue b, int weight = 1, int divisor = 1)
    {
        var sum = new ExactMeanAccumulator(); a.AddTo(ref sum); b.AddTo(ref sum, weight);
        return RocBankValue.Round(sum, count: divisor);
    }
    internal static double[] Levels(double open, double high, double low, double close, bool standard)
    {
        var total = new ExactMeanAccumulator(); total.Add(high); total.Add(low); total.Add(close); if (standard) total.Add(open);
        var pivot = RocBankValue.Round(total, count: standard ? 4 : 3);
        var twice = new ExactMeanAccumulator(); pivot.AddTo(ref twice, 2);
        var left = twice; left.Add(high, -1); var right = twice; right.Add(low, -1);
        var s1 = RocBankValue.Round(left); var r1 = RocBankValue.Round(right);
        if (!standard) return new[] { pivot.Publish(), s1.Publish(), r1.Publish() };
        var range = Combine(new(high), new(low), -1);
        var s2 = Combine(pivot, range, -1); var r2 = Combine(pivot, range);
        var range2 = Combine(r1, s1, -1);
        var s3 = Combine(pivot, range2, -1); var r3 = Combine(pivot, range2);
        return new[] { pivot.Publish(), s1.Publish(), s2.Publish(), s3.Publish(), r1.Publish(), r2.Publish(), r3.Publish(),
            Combine(s3,s2,divisor:2).Publish(), Combine(s2,s1,divisor:2).Publish(), Combine(s1,pivot,divisor:2).Publish(),
            Combine(r1,pivot,divisor:2).Publish(), Combine(r2,r1,divisor:2).Publish(), Combine(r3,r2,divisor:2).Publish() };
    }
}
internal static class PivotPeriodInputs
{
    internal static (List<double> Close,List<double> High,List<double> Low,List<double> Open,List<double> Volume) Read(StockData data, InputLength period)
    {
        var groups = CalculationsHelper.GetInputLengthGroupIndexes(data, period);
        var selected = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        List<double> close = new(), high = new(), low = new(), open = new();
        for (var i = 0; i < data.Count; i++)
        {
            var g = groups[i];
            if (g == close.Count) { close.Add(selected[i]); high.Add(data.HighPrices[i]); low.Add(data.LowPrices[i]); open.Add(data.OpenPrices[i]); }
            else { close[g] = selected[i]; high[g] = Math.Max(high[g],data.HighPrices[i]); low[g] = Math.Min(low[g],data.LowPrices[i]); }
        }
        return (close,high,low,open,new List<double>());
    }
}
