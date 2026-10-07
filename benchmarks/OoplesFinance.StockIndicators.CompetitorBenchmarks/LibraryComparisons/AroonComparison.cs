using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AroonComparison
{
    internal static readonly string[] Names = ["Up", "Down", "Oscillator"];
    internal static readonly string[] Ids =
    [
        "Skender.GetAroon",
        "TaLib.Functions.Aroon",
        "TaLib.Functions.AroonOsc",
        "Trady.Indicator.Aroon",
        "Trady.Indicator.AroonOscillator",
    ];
    internal static readonly ComparisonPair[] Pairs = Ids.Select(id => new ComparisonPair(
            id,
            "WindowAroon",
            (d, p) => Native(id, d, p),
            (d, p) => Owned(id, d, p),
            (d, p) => Reference(id, d, p, false),
            OutputNames(id),
            CompetitorReference: (d, p) => Reference(id, d, p, true)
        ))
        .ToArray();

    private static bool Trady(string id) => id.StartsWith("Trady.", StringComparison.Ordinal);

    private static string[] OutputNames(string id) =>
        id == Ids[0] ? Names
        : id.EndsWith("Osc", StringComparison.Ordinal)
        || id.EndsWith("Oscillator", StringComparison.Ordinal)
            ? [Names[2]]
        : [Names[0], Names[1]];

    internal static WindowAroon Create(string id, int period) =>
        new(period, !Trady(id), id == Ids[0]);

    private static ComparisonSeries Series(string id, double?[][] values) =>
        new(
            OutputNames(id)
                .ToDictionary(
                    name => name,
                    name =>
                    {
                        var row = values[Array.IndexOf(Names, name)];
                        return new ComparisonOutput(
                            0,
                            row.Select(v => v ?? double.NaN).ToArray(),
                            row.Select(v => v.HasValue).ToArray()
                        );
                    }
                )
        );

    private static double?[][] Empty(int count) =>
        Enumerable.Range(0, 3).Select(_ => new double?[count]).ToArray();

    private static ComparisonSeries Owned(string id, CompetitorData data, int period)
    {
        var indicator = Create(id, period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = Empty(data.Count);
        for (var slot = 0; slot < 3; slot++)
        {
            var row = run[indicator.Outputs[slot]].ToArray();
            var present = run[indicator.Outputs[slot + 3]].ToArray();
            for (var i = 0; i < data.Count; i++)
                values[slot][i] = present[i] > 0 ? row[i] : null;
        }
        return Series(id, values);
    }

    private static ComparisonSeries Native(string id, CompetitorData data, int period)
    {
        if (id == Ids[0])
        {
            var rows = data.Quotes.GetAroon(period).ToArray();
            return Series(
                id,
                [
                    rows.Select(r => r.AroonUp).ToArray(),
                    rows.Select(r => r.AroonDown).ToArray(),
                    rows.Select(r => r.Oscillator).ToArray(),
                ]
            );
        }
        var values = Empty(data.Count);
        if (Trady(id))
        {
            if (id == Ids[3])
            {
                var rows = new T.Aroon(data.Candles, period).Compute().ToArray();
                values[0] = rows.Select(r => (double?)r.Tick.Up).ToArray();
                values[1] = rows.Select(r => (double?)r.Tick.Down).ToArray();
            }
            else
                values[2] = new T.AroonOscillator(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray();
            return Series(id, values);
        }
        var down = new double[data.Count];
        var up = new double[data.Count];
        System.Range range;
        var code =
            id == Ids[1]
                ? Functions.Aroon<double>(
                    data.Highs,
                    data.Lows,
                    System.Range.All,
                    down,
                    up,
                    out range,
                    period
                )
                : Functions.AroonOsc<double>(
                    data.Highs,
                    data.Lows,
                    System.Range.All,
                    up,
                    out range,
                    period
                );
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return Series(id, values);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib Aroon returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - Math.Min(period, data.Count) || count > 0 && start != period)
            throw new InvalidOperationException("Unexpected Aroon alignment.");
        for (var i = 0; i < count; i++)
        {
            if (id == Ids[1])
            {
                values[0][start + i] = up[i];
                values[1][start + i] = down[i];
            }
            else
                values[2][start + i] = up[i];
        }
        return Series(id, values);
    }

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        bool native
    )
    {
        var values = Empty(data.Count);
        var first = Trady(id) ? period - 1 : period;
        for (var i = first; i < data.Count; i++)
        {
            var start = i - period + (Trady(id) ? 1 : 0);
            var high = start;
            var low = start;
            var oldest = id == Ids[0];
            // The package's nonpositive-high seed is intentionally confined to its oracle.
            var bestHigh = native && oldest ? 0d : Price(data.Highs[start], id, native);
            var bestLow = Price(data.Lows[start], id, native);
            if (native && oldest)
                high = -1;
            for (var j = start; j <= i; j++)
            {
                var h = Price(data.Highs[j], id, native);
                var l = Price(data.Lows[j], id, native);
                if (oldest ? h > bestHigh : h >= bestHigh)
                {
                    bestHigh = h;
                    high = j;
                }
                if (oldest ? l < bestLow : l <= bestLow)
                {
                    bestLow = l;
                    low = j;
                }
            }
            var upAge = (long)period - (i - high);
            var downAge = (long)period - (i - low);
            if (!native)
            {
                values[0][i] = Round(100 * upAge, period);
                values[1][i] = Round(100 * downAge, period);
                values[2][i] = Round(100L * (high - low), period);
            }
            else if (Trady(id))
            {
                var up = 100m * upAge / period;
                var down = 100m * downAge / period;
                values[0][i] = (double)up;
                values[1][i] = (double)down;
                values[2][i] = (double)(up - down);
            }
            else if (oldest)
            {
                var up = Divide(Multiply(100, upAge), period);
                var down = Divide(Multiply(100, downAge), period);
                values[0][i] = up;
                values[1][i] = down;
                values[2][i] = Subtract(up, down);
            }
            else
            {
                var factor = Divide(100, period);
                values[0][i] = Multiply(factor, upAge);
                values[1][i] = Multiply(factor, downAge);
                values[2][i] = Multiply(factor, high - low);
            }
        }
        return Series(id, values);
    }

    private static double Price(double value, string id, bool native) =>
        native && !id.StartsWith("TaLib.", StringComparison.Ordinal)
            ? (double)(decimal)value
            : value;

    internal static CompetitorData Fixture() =>
        PriceWindowChannelComparison.PointFixture(
            3,
            1,
            3,
            3,
            1,
            1,
            4,
            4,
            2,
            0,
            0,
            -2,
            -2,
            -4,
            -4,
            -1,
            -1,
            2,
            2
        );
}
