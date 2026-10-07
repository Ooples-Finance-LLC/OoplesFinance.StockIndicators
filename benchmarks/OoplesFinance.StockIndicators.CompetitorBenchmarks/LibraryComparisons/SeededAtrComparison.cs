using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SeededAtrComparison
{
    private static readonly string[] Names = ["Tr", "Atr", "Atrp"];
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "Skender.GetAtr",
            "AverageTrueRangeWithDetails",
            Skender,
            (d, p) => Ooples(d, p, true),
            (d, p) => Reference(d, p, true, 0),
            Names,
            CompetitorReference: (d, p) => Reference(d, p, true, 1)
        ),
        new(
            "TaLib.Functions.Atr",
            "SeededAverageTrueRange",
            TaLib,
            (d, p) => Ooples(d, p, false),
            (d, p) => Reference(d, p, false, 0),
            CompetitorReference: (d, p) => Reference(d, p, false, 2)
        ),
        new(
            "Trady.Indicator.AverageTrueRange",
            "SeededAverageTrueRange",
            (d, p) =>
                new(
                    Math.Min(p, d.Count),
                    new Trady.Analysis.Indicator.AverageTrueRange(d.Candles, p)
                        .Compute()
                        .Select(r => (double?)r.Tick ?? double.NaN)
                        .ToArray()
                ),
            (d, p) => Ooples(d, p, false),
            (d, p) => Reference(d, p, false, 0),
            CompetitorReference: (d, p) => Reference(d, p, false, 3)
        ),
    ];

    private static ComparisonSeries Ooples(CompetitorData data, int period, bool details)
    {
        IIndicator indicator = details
            ? new AverageTrueRangeWithDetails(period)
            : new SeededAverageTrueRange(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        if (!details)
            return new(Math.Min(period, data.Count), run[indicator.Outputs[0]].ToArray());
        var values = Enumerable
            .Range(0, 3)
            .Select(i => run[indicator.Outputs[i]].ToArray())
            .ToArray();
        var presence = Enumerable
            .Range(3, 3)
            .Select(i => run[indicator.Outputs[i]].ToArray().Select(v => v > 0).ToArray())
            .ToArray();
        return DetailSeries(values, presence, period);
    }

    private static ComparisonSeries Skender(CompetitorData data, int period)
    {
        var rows = data.Quotes.GetAtr(period).ToArray();
        var nullable = new[]
        {
            rows.Select(r => r.Tr).ToArray(),
            rows.Select(r => r.Atr).ToArray(),
            rows.Select(r => r.Atrp).ToArray(),
        };
        return DetailSeries(
            nullable.Select(a => a.Select(v => v ?? double.NaN).ToArray()).ToArray(),
            nullable.Select(a => a.Select(v => v.HasValue).ToArray()).ToArray(),
            period
        );
    }

    private static ComparisonSeries DetailSeries(double[][] values, bool[][] present, int period) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                Math.Min(slot == 0 ? 1 : period, values[slot].Length),
                                values[slot]
                                    .Select((v, i) => present[slot][i] ? v : double.NaN)
                                    .ToArray(),
                                present[slot]
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries TaLib(CompetitorData data, int period)
    {
        var packed = new double[data.Count];
        var code = Functions.Atr<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        var first = Math.Min(period, data.Count);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(first, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib ATR returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected ATR alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    // mode: exact Ooples, Skender converted binary64, TA-Lib binary64, Trady decimal.
    internal static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool details,
        int mode
    )
    {
        var values = Enumerable.Range(0, 3).Select(_ => new double[data.Count]).ToArray();
        var present = Enumerable.Range(0, 3).Select(_ => new bool[data.Count]).ToArray();
        var seed = BigInteger.Zero;
        var seedBinary = 0d;
        decimal seedDecimal = 0,
            averageDecimal = 0;
        for (var i = 1; i < data.Count; i++)
        {
            var high = mode == 1 ? (double)(decimal)data.Highs[i] : data.Highs[i];
            var low = mode == 1 ? (double)(decimal)data.Lows[i] : data.Lows[i];
            var close = mode == 1 ? (double)(decimal)data.Closes[i] : data.Closes[i];
            var previous = mode == 1 ? (double)(decimal)data.Closes[i - 1] : data.Closes[i - 1];
            var rangeUnits = BigInteger.Max(
                Units(high) - Units(low),
                BigInteger.Max(
                    BigInteger.Abs(Units(high) - Units(previous)),
                    BigInteger.Abs(Units(low) - Units(previous))
                )
            );
            decimal rangeDecimal = 0;
            values[0][i] =
                mode == 0
                    ? Round(rangeUnits, Grid)
                    : Math.Max(
                        Subtract(high, low),
                        Math.Max(
                            Math.Abs(Subtract(high, previous)),
                            Math.Abs(Subtract(low, previous))
                        )
                    );
            if (mode == 3)
            {
                var h = (decimal)high;
                var l = (decimal)low;
                var c = (decimal)previous;
                rangeDecimal = Math.Max(h - l, Math.Max(Math.Abs(h - c), Math.Abs(l - c)));
                values[0][i] = (double)rangeDecimal;
            }
            present[0][i] = true;
            if (i <= period)
            {
                seed += Units(values[0][i]);
                seedBinary = Add(seedBinary, values[0][i]);
                seedDecimal += rangeDecimal;
            }
            if (i < period)
                continue;
            present[1][i] = true;
            if (mode == 0)
                values[1][i] = Round(
                    i == period
                        ? seed
                        : Units(values[1][i - 1]) * (period - 1) + Units(values[0][i]),
                    Grid * period
                );
            else if (mode == 3)
            {
                averageDecimal =
                    i == period
                        ? seedDecimal / period
                        : averageDecimal + (1m / period) * (rangeDecimal - averageDecimal);
                values[1][i] = (double)averageDecimal;
            }
            else
                values[1][i] =
                    i == period
                        ? Divide(seedBinary, period)
                        : Divide(Add(Multiply(values[1][i - 1], period - 1), values[0][i]), period);
            if (close == 0)
                continue;
            present[2][i] = true;
            values[2][i] =
                mode == 0
                    ? Round(Units(values[1][i]) * 100, Units(close))
                    : Multiply(Divide(values[1][i], close), 100);
        }
        return details
            ? DetailSeries(values, present, period)
            : new(Math.Min(period, data.Count), values[1]);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [4, 20, -8, 3, 0, 6, 1, 0, -2],
            [10, 25, -2, 10, 0, 6, 8, 0, 0],
            [0, 18, -12, -5, 0, 6, -2, 0, -4],
            [6, 22, -4, 7, 0, 6, 3, 0, -2]
        );
}
