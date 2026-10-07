using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PriceWindowChannelComparison
{
    internal static readonly string[] ChannelNames =
    [
        "UpperBand",
        "LowerBand",
        "Centerline",
        "Width",
    ];
    internal static readonly string[] Ids =
    [
        "Skender.GetDonchian",
        "Skender.GetWilliamsR",
        "TaLib.Functions.WillR",
        "Trady.Indicator.HighestHighLowestLowDifference",
    ];
    internal static readonly ComparisonPair[] Pairs = Ids.Select(id => new ComparisonPair(
            id,
            id == Ids[0] ? "PriorPriceChannel"
                : id == Ids[3] ? "WindowPriceRange"
                : "WilliamsR",
            (d, p) => Native(id, d, p),
            (d, p) => Owned(id, d, p),
            (d, p) => Reference(id, d, p, false),
            id == Ids[0] ? ChannelNames : ["Value"],
            CompetitorReference: (d, p) => Reference(id, d, p, true)
        ))
        .ToArray();

    private static ComparisonSeries Channel(double?[][] values) =>
        new(
            ChannelNames
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(v => v ?? double.NaN).ToArray(),
                                values[slot].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries Owned(string id, CompetitorData data, int period)
    {
        IIndicator indicator =
            id == Ids[0] ? new PriorPriceChannel(period)
            : id == Ids[3] ? new WindowPriceRange(period)
            : new WilliamsR(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        if (id != Ids[0])
            return new(Math.Min(period - 1, data.Count), run[indicator.Outputs[0]].ToArray());
        return Channel(
            Enumerable
                .Range(0, 4)
                .Select(slot =>
                {
                    var values = run[indicator.Outputs[slot]].ToArray();
                    var presence = run[indicator.Outputs[slot + 4]].ToArray();
                    return values.Select((v, i) => presence[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(string id, CompetitorData data, int period)
    {
        if (id == Ids[0])
        {
            var rows = data.Quotes.GetDonchian(period).ToArray();
            return Channel([
                rows.Select(r => (double?)r.UpperBand).ToArray(),
                rows.Select(r => (double?)r.LowerBand).ToArray(),
                rows.Select(r => (double?)r.Centerline).ToArray(),
                rows.Select(r => (double?)r.Width).ToArray(),
            ]);
        }
        var first = Math.Min(period - 1, data.Count);
        if (id == Ids[1])
            return new(
                first,
                data.Quotes.GetWilliamsR(period).Select(r => r.WilliamsR ?? double.NaN).ToArray()
            );
        if (id == Ids[3])
            return new(
                first,
                new Trady.Analysis.Indicator.HighestHighLowestLowDifference(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick ?? double.NaN)
                    .ToArray()
            );
        var packed = new double[data.Count];
        var code = Functions.WillR<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(first, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib WillR returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected Williams alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        bool native
    )
    {
        if (id == Ids[0])
            return ChannelReference(data, period, native);
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        for (var i = period - 1; i < data.Count; i++)
        {
            var high = data.Highs.Skip(i - period + 1).Take(period).Max();
            var low = data.Lows.Skip(i - period + 1).Take(period).Min();
            var close = data.Closes[i];
            if (!native)
            {
                var h = Units(high);
                var l = Units(low);
                var c = Units(close);
                values[i] =
                    id == Ids[3] ? Round(h - l, Grid)
                    : h == l ? -100
                    : Round(100 * (c - h), h - l);
            }
            else if (id == Ids[3])
                values[i] = (double)((decimal)high - (decimal)low);
            else if (id == Ids[1])
            {
                high = (double)(decimal)high;
                low = (double)(decimal)low;
                close = (double)(decimal)close;
                var span = Subtract(high, low);
                values[i] =
                    Math.Abs(span) > 0
                        ? Subtract(Divide(Multiply(100, Subtract(close, low)), span), 100)
                        : -100;
            }
            else
            {
                var divisor = Divide(Subtract(high, low), -100);
                values[i] = Math.Abs(divisor) > 0 ? Divide(Subtract(high, close), divisor) : 0;
            }
        }
        return new(Math.Min(period - 1, data.Count), values);
    }

    private static ComparisonSeries ChannelReference(CompetitorData data, int period, bool native)
    {
        var values = Enumerable.Range(0, 4).Select(_ => new double?[data.Count]).ToArray();
        for (var i = period; i < data.Count; i++)
        {
            var high = data.Highs.Skip(i - period).Take(period).Max();
            var low = data.Lows.Skip(i - period).Take(period).Min();
            if (native)
            {
                // Native decimal arithmetic has a zero initial maximum, even for negative windows.
                var h = Math.Max(0, (decimal)high);
                var l = (decimal)low;
                var center = (h + l) / 2;
                values[0][i] = (double)h;
                values[1][i] = (double)l;
                values[2][i] = (double)center;
                values[3][i] = center == 0 ? null : (double)((h - l) / center);
            }
            else
            {
                var h = Units(high);
                var l = Units(low);
                values[0][i] = high;
                values[1][i] = low;
                values[2][i] = Round(h + l, 2 * Grid);
                values[3][i] = (h + l).IsZero ? null : Round(2 * (h - l), h + l);
            }
        }
        return Channel(values);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [2, 4, 3, -3, -4, -2, 0, 0, 4, 2, 2, 7, 1],
            [3, 5, 4, -2, -3, -1, 0, 0, 6, 2, 3, 8, 2],
            [1, 2, 2, -4, -5, -3, 0, 0, 2, 2, 1, 6, 0],
            [2, 3, 3, -3, -4, -2, 0, 0, 4, 2, 2, 7, 1]
        );

    internal static CompetitorData PointFixture(params double[] values) =>
        CompetitorData.FromOhlc(values, values, values, values);

    internal static CompetitorData CollapsedQuoteFixture() =>
        PointFixture(.1, Math.BitIncrement(.1), .1, Math.BitIncrement(.1), .1);
}
