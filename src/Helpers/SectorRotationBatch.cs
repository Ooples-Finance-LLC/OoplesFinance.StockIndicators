using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal static class SectorRotationBatch
{
    internal static Dictionary<string, List<double>> Run(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        MovingAvgType average, int length1, int length2)
    {
        var p = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var b = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        using var state = new SectorRotationModelState(p, b, average, length1, length2);
        var context = new MultiSeriesContext(new SeriesStore());
        var result = new Dictionary<string, List<double>>();
        var values = new List<double>(primary.Count); var signals = new List<double>(primary.Count);
        for (var i = 0; i < primary.Count; i++)
        {
            var time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            OhlcvBar Bar(SeriesKey key, double value) => new(key.Symbol, key.Timeframe, time, time, value, value, value, value, 0, true);
            state.UpdateValues(context, b, Bar(b, benchmark[i]), true, out _);
            var point = state.UpdateValues(context, p, Bar(p, primary[i]), true, out var signal);
            if (!point.HasValue) throw new InvalidOperationException("An aligned pair did not publish outputs.");
            if (i == 0) { result.Add("Srm", values); result.Add("Signal", signals); }
            values.Add(point.Value); signals.Add(signal);
        }
        return result;
    }
}
