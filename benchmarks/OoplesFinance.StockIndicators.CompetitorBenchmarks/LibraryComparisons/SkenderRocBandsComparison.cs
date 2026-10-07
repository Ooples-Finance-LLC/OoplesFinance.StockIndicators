using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SkenderRocBandsComparison
{
    internal static readonly ComparisonPair Pair = Create(3);

    internal static ComparisonPair Create(int emaPeriod) =>
        new(
            "Skender.GetRocWb",
            "RateOfChangeRmsBands",
            (data, period) => Competitor(data, period, emaPeriod),
            (data, period) => Ooples(data, period, emaPeriod),
            (data, period) => Reference(data, period, emaPeriod),
            ["Roc", "RocEma", "UpperBand", "LowerBand"]
        );

    private static int First(int period, int extra, int count) =>
        (int)Math.Min(count, (long)period + extra - 1);

    private static ComparisonSeries Ooples(CompetitorData data, int period, int emaPeriod)
    {
        var deviation = Math.Min(3, period);
        var indicator = new RateOfChangeRmsBands(period, emaPeriod, deviation);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        ComparisonOutput Read(IIndicatorOutput value, IIndicatorOutput flag, int first)
        {
            var values = run[value].ToArray();
            var flags = run[flag].ToArray();
            var present = new bool[data.Count];
            for (var i = 0; i < data.Count; i++)
            {
                present[i] = flags[i] > 0;
                if (!present[i])
                    values[i] = double.NaN;
            }
            return new(first, values, present);
        }
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Roc"] = Read(indicator.Roc, indicator.RocIsDefined, First(period, 1, data.Count)),
                ["RocEma"] = Read(
                    indicator.Ema,
                    indicator.EmaIsDefined,
                    First(period, emaPeriod, data.Count)
                ),
                ["UpperBand"] = Read(
                    indicator.UpperBand,
                    indicator.BandsAreDefined,
                    First(period, deviation, data.Count)
                ),
                ["LowerBand"] = Read(
                    indicator.LowerBand,
                    indicator.BandsAreDefined,
                    First(period, deviation, data.Count)
                ),
            }
        );
    }

    private static ComparisonSeries Competitor(CompetitorData data, int period, int emaPeriod)
    {
        var deviation = Math.Min(3, period);
        var rows = data.Quotes.GetRocWb(period, emaPeriod, deviation).ToArray();
        ComparisonOutput Read(Func<RocWbResult, double?> select, int extra) =>
            new(
                First(period, extra, data.Count),
                rows.Select(r => select(r) ?? double.NaN).ToArray(),
                rows.Select(r => select(r).HasValue).ToArray()
            );
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Roc"] = Read(r => r.Roc, 1),
                ["RocEma"] = Read(r => r.RocEma, emaPeriod),
                ["UpperBand"] = Read(r => r.UpperBand, deviation),
                ["LowerBand"] = Read(r => r.LowerBand, deviation),
            }
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, int emaPeriod)
    {
        var deviation = Math.Min(3, period);
        var roc = new decimal?[data.Count];
        var ema = new decimal?[data.Count];
        var upper = new double?[data.Count];
        var lower = new double?[data.Count];
        for (var i = period; i < data.Count; i++)
            if (data.Closes[i - period] != 0)
                roc[i] = 100 * ((decimal)data.Closes[i] / (decimal)data.Closes[i - period] - 1);
        var firstEma = First(period, emaPeriod, data.Count);
        for (var i = firstEma; i < data.Count; i++)
            if (i == firstEma)
            {
                decimal? total = 0;
                for (var j = period; j <= i; j++)
                    total += roc[j];
                ema[i] = total / emaPeriod;
            }
            else
                ema[i] = (2 * roc[i] + (emaPeriod - 1m) * ema[i - 1]) / (emaPeriod + 1m);
        var firstBand = First(period, deviation, data.Count);
        for (var i = firstBand; i < data.Count; i++)
        {
            decimal? sum = 0;
            for (var j = i - deviation + 1; j <= i; j++)
                sum += roc[j] * roc[j];
            if (!sum.HasValue)
                continue;
            upper[i] = Math.Sqrt((double)(sum.Value / deviation));
            lower[i] = -upper[i];
        }
        ComparisonOutput Output(IEnumerable<double?> values, int first)
        {
            var all = values.ToArray();
            return new(
                first,
                all.Select(v => v ?? double.NaN).ToArray(),
                all.Select(v => v.HasValue).ToArray()
            );
        }
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Roc"] = Output(roc.Select(v => (double?)v), First(period, 1, data.Count)),
                ["RocEma"] = Output(ema.Select(v => (double?)v), firstEma),
                ["UpperBand"] = Output(upper, firstBand),
                ["LowerBand"] = Output(lower, firstBand),
            }
        );
    }
}
