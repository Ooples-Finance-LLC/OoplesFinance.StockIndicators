using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AccumulationDistributionComparison
{
    private static readonly string[] DetailOutputs =
    [
        "MoneyFlowMultiplier",
        "MoneyFlowVolume",
        "Adl",
        "AdlSma",
    ];
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "TaLib.Functions.Ad",
            "Adl",
            TaLib,
            (data, _) => OoplesLine(data, false),
            (data, _) => Reference(data, null, false, false),
            MinimumInputCount: 2,
            CompetitorReference: (data, _) => BinaryReference(data, null, false)
        ),
        new(
            "Trady.Indicator.AccumulationDistributionLine",
            "PriceAdjustedAccumulationDistribution",
            (data, _) =>
                Mask(
                    new Trady.Analysis.Indicator.AccumulationDistributionLine(data.Candles)
                        .Compute()
                        .Select(r => (double?)r.Tick)
                        .ToArray()
                ),
            (data, _) => OoplesLine(data, true),
            (data, _) => Reference(data, null, true, false),
            CompetitorReference: (data, _) => TradyReference(data)
        ),
        Skender(20),
    ];

    internal static ComparisonPair Skender(int? averagePeriod) =>
        new(
            "Skender.GetAdl",
            "AccumulationDistributionWithAverage",
            (data, _) =>
            {
                var rows = data.Quotes.GetAdl(averagePeriod).ToArray();
                return new(
                    new Dictionary<string, ComparisonOutput>
                    {
                        [DetailOutputs[0]] = Nullable(
                            rows.Select(r => r.MoneyFlowMultiplier).ToArray()
                        ),
                        [DetailOutputs[1]] = Nullable(
                            rows.Select(r => r.MoneyFlowVolume).ToArray()
                        ),
                        [DetailOutputs[2]] = new(0, rows.Select(r => r.Adl).ToArray()),
                        [DetailOutputs[3]] = Nullable(
                            rows.Select(r => r.AdlSma).ToArray(),
                            averagePeriod is int period ? Math.Min(period - 1, data.Count) : 0
                        ),
                    }
                );
            },
            (data, _) => OoplesDetails(data, averagePeriod),
            (data, _) => Reference(data, averagePeriod, false, true),
            DetailOutputs,
            CompetitorReference: (data, _) => BinaryReference(data, averagePeriod, true)
        );

    private static ComparisonSeries OoplesLine(CompetitorData data, bool priceAdjusted)
    {
        IIndicator indicator = priceAdjusted
            ? new PriceAdjustedAccumulationDistribution()
            : new Adl(1);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Outputs[0]].ToArray();
        if (!priceAdjusted)
            return new(0, values);
        var flags = run[indicator.Outputs[1]].ToArray();
        return Mask(values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray());
    }

    private static ComparisonSeries OoplesDetails(CompetitorData data, int? averagePeriod)
    {
        var indicator = new AccumulationDistributionWithAverage(averagePeriod);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var columns = indicator.Outputs.Select(output => run[output].ToArray()).ToArray();
        return new(
            new Dictionary<string, ComparisonOutput>
            {
                [DetailOutputs[0]] = new(0, columns[0]),
                [DetailOutputs[1]] = new(0, columns[1]),
                [DetailOutputs[2]] = new(0, columns[2]),
                [DetailOutputs[3]] = Nullable(
                    columns[3].Select((v, i) => columns[4][i] > 0 ? (double?)v : null).ToArray(),
                    averagePeriod is int period ? Math.Min(period - 1, data.Count) : 0
                ),
            }
        );
    }

    private static ComparisonSeries TaLib(CompetitorData data, int _)
    {
        var values = new double[data.Count];
        var code = Functions.Ad<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            data.Volumes,
            System.Range.All,
            values,
            out var range
        );
        if (
            code != TALib.Core.RetCode.Success
            || range.GetOffsetAndLength(data.Count) != (0, data.Count)
        )
            throw new InvalidOperationException("Unexpected TA-Lib AD output: " + code);
        return new(0, values);
    }

    private static ComparisonSeries Mask(double?[] values) =>
        new(new Dictionary<string, ComparisonOutput> { ["Value"] = Nullable(values) });

    private static ComparisonOutput Nullable(double?[] values, int first = 0) =>
        new(
            first,
            values.Select(v => v ?? double.NaN).ToArray(),
            values.Select(v => v.HasValue).ToArray()
        );

    private static ComparisonSeries Reference(
        CompetitorData data,
        int? averagePeriod,
        bool adjusted,
        bool details
    )
    {
        var multipliers = new double[data.Count];
        var flows = new double[data.Count];
        var lines = new double?[data.Count];
        var averages = new double?[data.Count];
        var total = BigInteger.Zero;
        var defined = true;
        for (var i = 0; i < data.Count; i++)
        {
            if (adjusted && i == 0)
                total = Units(data.Volumes[i]);
            else if (defined)
            {
                var high = Units(data.Highs[i]);
                var low = Units(data.Lows[i]);
                var close = Units(data.Closes[i]);
                var numerator = 2 * close - high - low;
                var denominator = high - low;
                if (denominator.IsZero && adjusted)
                {
                    denominator = Units(data.Closes[i - 1]);
                    numerator = close - denominator;
                    if (denominator.IsZero)
                        defined = false;
                }
                if (!denominator.IsZero)
                {
                    multipliers[i] = Round(numerator, denominator);
                    flows[i] = Round(numerator * Units(data.Volumes[i]), denominator * Grid);
                    total += Units(flows[i]);
                }
            }
            if (defined)
                lines[i] = Round(total, Grid);
            if (averagePeriod is int period && i >= period - 1)
            {
                var sum = lines
                    .Skip(i - period + 1)
                    .Take(period)
                    .Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v!.Value));
                averages[i] = Round(sum, Grid * period);
            }
        }
        return Pack(multipliers, flows, lines, averages, averagePeriod, details, adjusted);
    }

    private static ComparisonSeries BinaryReference(
        CompetitorData data,
        int? averagePeriod,
        bool skender
    )
    {
        var multipliers = new double[data.Count];
        var flows = new double[data.Count];
        var lines = new double?[data.Count];
        var averages = new double?[data.Count];
        double Price(double value) => skender ? (double)(decimal)value : value;
        double total = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var high = Price(data.Highs[i]);
            var low = Price(data.Lows[i]);
            var close = Price(data.Closes[i]);
            var range = Subtract(high, low);
            if (range != 0)
            {
                var up = Subtract(close, low);
                var down = Subtract(high, close);
                multipliers[i] = Divide(Subtract(up, down), range);
                flows[i] = Multiply(multipliers[i], Price(data.Volumes[i]));
            }
            total = Add(total, flows[i]);
            lines[i] = total;
            if (averagePeriod is int period && i >= period - 1)
            {
                var sum = lines
                    .Skip(i - period + 1)
                    .Take(period)
                    .Aggregate(0d, (sum, v) => Add(sum, v!.Value));
                averages[i] = Round(Units(sum), Grid * period);
            }
        }
        return Pack(multipliers, flows, lines, averages, averagePeriod, skender, false);
    }

    private static ComparisonSeries TradyReference(CompetitorData data)
    {
        var values = new double?[data.Count];
        decimal total = 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (i == 0)
                total = (decimal)data.Volumes[i];
            else
            {
                var high = (decimal)data.Highs[i];
                var low = (decimal)data.Lows[i];
                var close = (decimal)data.Closes[i];
                decimal ratio;
                if (high == low)
                {
                    var previous = (decimal)data.Closes[i - 1];
                    if (previous == 0)
                        break;
                    ratio = close / previous - 1;
                }
                else
                    ratio = ((close - low) - (high - close)) / (high - low);
                total += ratio * (decimal)data.Volumes[i];
            }
            values[i] = (double)total;
        }
        return Mask(values);
    }

    private static ComparisonSeries Pack(
        double[] multipliers,
        double[] flows,
        double?[] lines,
        double?[] averages,
        int? averagePeriod,
        bool details,
        bool adjusted
    )
    {
        if (!details)
            return adjusted ? Mask(lines) : new(0, lines.Select(v => v!.Value).ToArray());
        return new(
            new Dictionary<string, ComparisonOutput>
            {
                [DetailOutputs[0]] = new(0, multipliers),
                [DetailOutputs[1]] = new(0, flows),
                [DetailOutputs[2]] = new(0, lines.Select(v => v!.Value).ToArray()),
                [DetailOutputs[3]] = Nullable(
                    averages,
                    averagePeriod is int period ? Math.Min(period - 1, lines.Length) : 0
                ),
            }
        );
    }

    internal static CompetitorData BenchmarkFixture(int count)
    {
        var source = CompetitorData.Create(count);
        static double[] Common(double[] values) =>
            values.Select(v => Math.Round(v * 1024) / 1024).ToArray();
        return CompetitorData.FromOhlcv(
            Common(source.Opens),
            Common(source.Highs),
            Common(source.Lows),
            Common(source.Closes),
            source.Volumes
        );
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlcv(
            [1, 2, 4, 0, 1, 0],
            [2, 2, 4, 0, 1, 2],
            [0, 2, 0, 0, 1, 0],
            [1, 2, 4, 0, 1, 0],
            [10, 4, 6, 2, 3, 5]
        );
}
