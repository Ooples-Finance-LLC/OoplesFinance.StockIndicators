using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ObvComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        Skender(20),
        new(
            "TaLib.Functions.Obv",
            "SeededOnBalanceVolume(FirstVolume)",
            TaLib,
            (data, _) => Ooples(data, ObvSeed.FirstVolume, null, false),
            (data, _) => Reference(data, ObvSeed.FirstVolume, null, false),
            MinimumInputCount: 2
        ),
        new(
            "Trady.Indicator.OnBalanceVolume",
            "SeededOnBalanceVolume(FirstVolume)",
            (data, _) =>
                new(
                    0,
                    new Trady.Analysis.Indicator.OnBalanceVolume(data.Candles)
                        .Compute()
                        .Select(r => (double?)r.Tick ?? double.NaN)
                        .ToArray()
                ),
            (data, _) => Ooples(data, ObvSeed.FirstVolume, null, false),
            (data, _) => Reference(data, ObvSeed.FirstVolume, null, false)
        ),
    ];

    internal static ComparisonPair Skender(int? averagePeriod) =>
        new(
            "Skender.GetObv",
            "SeededOnBalanceVolume(Zero)",
            (data, _) =>
            {
                var rows = data.Quotes.GetObv(averagePeriod).ToArray();
                return new(
                    new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
                    {
                        ["Obv"] = new(0, rows.Select(r => r.Obv).ToArray()),
                        ["ObvSma"] = new(
                            0,
                            rows.Select(r => r.ObvSma ?? double.NaN).ToArray(),
                            rows.Select(r => r.ObvSma.HasValue).ToArray()
                        ),
                    }
                );
            },
            (data, _) => Ooples(data, ObvSeed.Zero, averagePeriod, true),
            (data, _) => Reference(data, ObvSeed.Zero, averagePeriod, true),
            ["Obv", "ObvSma"]
        );

    private static ComparisonSeries Ooples(
        CompetitorData data,
        ObvSeed seed,
        int? averagePeriod,
        bool skender
    )
    {
        var indicator = new SeededOnBalanceVolume(seed, averagePeriod);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        if (!skender)
            return new(0, values);
        var average = run[indicator.Average].ToArray();
        var present = run[indicator.AverageIsDefined].ToArray().Select(v => v > 0).ToArray();
        for (var i = 0; i < data.Count; i++)
            if (!present[i])
                average[i] = double.NaN;
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Obv"] = new(0, values),
                ["ObvSma"] = new(0, average, present),
            }
        );
    }

    private static ComparisonSeries TaLib(CompetitorData data, int _)
    {
        var values = new double[data.Count];
        var code = Functions.Obv<double>(
            data.Closes,
            data.Volumes,
            System.Range.All,
            values,
            out var range
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (start != 0 || count != data.Count)
            throw new InvalidOperationException("Unexpected OBV alignment.");
        return new(0, values);
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        ObvSeed seed,
        int? averagePeriod,
        bool skender
    )
    {
        var values = new double[data.Count];
        decimal total = seed == ObvSeed.FirstVolume ? (decimal)data.Volumes[0] : 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (i > 0)
                total +=
                    Math.Sign((decimal)data.Closes[i] - (decimal)data.Closes[i - 1])
                    * (decimal)data.Volumes[i];
            values[i] = (double)total;
        }
        if (!skender)
            return new(0, values);
        var averages = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var present = new bool[data.Count];
        if (averagePeriod is int period)
            for (var i = period; i < data.Count; i++)
            {
                averages[i] = (double)
                    Enumerable.Range(i - period + 1, period).Average(j => (decimal)values[j]);
                present[i] = true;
            }
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Obv"] = new(0, values),
                ["ObvSma"] = new(0, averages, present),
            }
        );
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlcv(
            [1d, 2, 3, 4, 5, 6, 7],
            [20d, 21, 22, 23, 24, 25, 26],
            [-20d, -21, -22, -23, -24, -25, -26],
            [-2d, -1, -1, -3, 0, 0, 2],
            [10d, 3, 100, 5, 7, 1000, 0]
        );
}
