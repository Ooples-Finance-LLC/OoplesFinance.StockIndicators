using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class BalanceOfPowerComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "TaLib.Functions.Bop",
            "BalanceOfPower",
            TaLib,
            (data, _) => Ooples(data, 1, false),
            (data, _) => Reference(data, 1, false),
            MinimumInputCount: 2
        ),
        new(
            "Skender.GetBop",
            "BalanceOfPowerWithValidity",
            (data, period) => Mask(data.Quotes.GetBop(period).Select(r => r.Bop).ToArray(), period),
            (data, period) => Ooples(data, period, true),
            (data, period) => Reference(data, period, true)
        ),
    ];

    private static ComparisonSeries Ooples(CompetitorData data, int period, bool missingRanges)
    {
        IIndicator indicator = missingRanges
            ? new BalanceOfPowerWithValidity(period)
            : new BalanceOfPower(1);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Outputs[0]].ToArray();
        if (!missingRanges)
            return new(0, values);
        var present = run[indicator.Outputs[1]].ToArray();
        return Mask(values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray(), period);
    }

    private static ComparisonSeries Mask(double?[] values, int period) =>
        new(
            new Dictionary<string, ComparisonOutput>
            {
                ["Value"] = new(
                    Math.Min(period - 1, values.Length),
                    values.Select(v => v ?? double.NaN).ToArray(),
                    values.Select(v => v.HasValue).ToArray()
                ),
            }
        );

    private static ComparisonSeries TaLib(CompetitorData data, int _)
    {
        var values = new double[data.Count];
        var code = Functions.Bop<double>(
            data.Opens,
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            values,
            out var range
        );
        if (code != TALib.Core.RetCode.Success || range.GetOffsetAndLength(data.Count) != (0, data.Count))
            throw new InvalidOperationException("Unexpected TA-Lib BOP output: " + code);
        return new(0, values);
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool missingRanges)
    {
        var raw = new decimal?[data.Count];
        for (var i = 0; i < raw.Length; i++)
        {
            var range = (decimal)data.Highs[i] - (decimal)data.Lows[i];
            raw[i] =
                range == 0
                    ? missingRanges
                        ? null
                        : 0
                    : ((decimal)data.Closes[i] - (decimal)data.Opens[i]) / range;
        }
        if (!missingRanges)
            return new(0, raw.Select(v => (double)v!.Value).ToArray());
        var values = new double?[data.Count];
        for (var i = period - 1; i < values.Length; i++)
        {
            var window = raw.Skip(i - period + 1).Take(period).ToArray();
            if (window.All(v => v.HasValue))
                values[i] = (double)(window.Sum(v => v!.Value) / period);
        }
        return Mask(values, period);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [1, 2, 2, 0, 3, 0, 1, 1],
            [2, 2, 4, 2, 4, 2, 2, 2],
            [0, 2, 0, -2, 0, -2, 0, 0],
            [2, 2, 0, 1, 4, -1, 0, 2]
        );
}
