using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class EnvelopeComparison
{
    internal static readonly string[] Names = ["Centerline", "UpperEnvelope", "LowerEnvelope"];
    internal static readonly ComparisonPair Pair = Create(EnvelopeAverage.Sma);

    internal static ComparisonPair Create(EnvelopeAverage average, double percent = 2.5)
    {
        var nativeType = Enum.Parse<MaType>(average.ToString(), true);
        return new(
            "Skender.GetMaEnvelopes",
            nameof(WindowAverageEnvelope),
            (d, p) => FromRows(d.Quotes.GetMaEnvelopes(p, percent, nativeType)),
            (d, p) => Owned(d.IndicatorBars, p, percent, average),
            (d, p) => Reference(d.Closes, p, percent, average, false),
            Names,
            CompetitorReference: (d, p) =>
                Reference(
                    d.Quotes.Select(q => (double)q.Close).ToArray(),
                    p,
                    percent,
                    average,
                    true
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );
    }

    internal static ComparisonSeries FromRows(IEnumerable<MaEnvelopeResult> rows)
    {
        var values = rows.ToArray();
        return Series([
            values.Select(r => r.Centerline).ToArray(),
            values.Select(r => r.UpperEnvelope).ToArray(),
            values.Select(r => r.LowerEnvelope).ToArray(),
        ]);
    }

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(x => x ?? double.NaN).ToArray(),
                                values[slot].Select(x => x.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(x => x.Key, x => x.Value)
        );

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int period,
        double percent,
        EnvelopeAverage average
    )
    {
        var indicator = new WindowAverageEnvelope(period, percent, average);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(slot =>
                    run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                        .ToArray()
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Reference(
        double[] prices,
        int period,
        double percent,
        EnvelopeAverage average,
        bool native
    )
    {
        var center = Centers(prices, period, average, native);
        var values = new[] { center, new double?[prices.Length], new double?[prices.Length] };
        for (var i = 0; i < prices.Length; i++)
        {
            if (!center[i].HasValue)
                continue;
            var value = center[i]!.Value;
            if (native)
            {
                var distance = Multiply(value, Divide(percent, 100));
                values[1][i] = Add(value, distance);
                values[2][i] = Subtract(value, distance);
            }
            else
            {
                var price = Units(value);
                var offset = Units(percent);
                values[1][i] = Round(price * (100 * Grid + offset), 100 * Grid * Grid);
                values[2][i] = Round(price * (100 * Grid - offset), 100 * Grid * Grid);
            }
        }
        return Series(values);
    }

    private static double?[] Unmask(ComparisonSeries series)
    {
        var output = series.Outputs["Value"];
        return output.Values.Select((x, i) => output.Present![i] ? (double?)x : null).ToArray();
    }

    private static double?[] Centers(
        double[] prices,
        int period,
        EnvelopeAverage average,
        bool native
    )
    {
        if (average == EnvelopeAverage.Alma)
            return Unmask(AlmaComparison.Reference(prices, period, true, .85, 6, native));
        if (average == EnvelopeAverage.Hma)
            return Unmask(
                native
                    ? HullComparison.SkenderReference(prices, period)
                    : HullComparison.Reference(prices, period, HullWindowConvention.FullWindowFloor)
            );
        var result = new double?[prices.Length];
        if (prices.Length < period)
            return result;
        if (
            average
            is EnvelopeAverage.Ema
                or EnvelopeAverage.Dema
                or EnvelopeAverage.Tema
                or EnvelopeAverage.Smma
        )
            return Recurrence(prices, period, average, native);
        for (var end = period - 1; end < prices.Length; end++)
        {
            if (native && average == EnvelopeAverage.Epma)
            {
                result[end] = Endpoint(prices, end, period);
                continue;
            }
            BigInteger sum = 0,
                mass = 0;
            var rounded = 0d;
            for (var index = end - period + 1; index <= end; index++)
            {
                var weight = average switch
                {
                    EnvelopeAverage.Wma => index - end + period,
                    EnvelopeAverage.Epma => 2L * period - 1 - 3L * (end - index),
                    _ => 1,
                };
                if (native)
                    rounded = Add(
                        rounded,
                        average == EnvelopeAverage.Wma
                            ? Divide(
                                Multiply(prices[index], weight),
                                (double)period * (period + 1L) / 2
                            )
                            : prices[index]
                    );
                else
                {
                    sum += Units(prices[index]) * weight;
                    mass += weight;
                }
            }
            result[end] = native
                ? average == EnvelopeAverage.Sma
                    ? Divide(rounded, period)
                    : rounded
                : Round(sum, mass * Grid);
        }
        return result;
    }

    private static double?[] Recurrence(
        double[] prices,
        int period,
        EnvelopeAverage average,
        bool native
    )
    {
        var output = new double?[prices.Length];
        var order =
            average == EnvelopeAverage.Tema ? 3
            : average == EnvelopeAverage.Dema ? 2
            : 1;
        var seed = native
            ? Divide(prices.Take(period).Aggregate(0d, Add), period)
            : Round(
                prices.Take(period).Aggregate(BigInteger.Zero, (s, p) => s + Units(p)),
                period * Grid
            );
        var stages = Enumerable.Repeat(seed, order).ToArray();
        var alpha = Divide(2, period + 1L);
        for (var i = period - 1; i < prices.Length; i++)
        {
            if (i >= period)
            {
                var input = prices[i];
                for (var stage = 0; stage < order; stage++)
                {
                    if (average == EnvelopeAverage.Smma)
                        stages[stage] = native
                            ? Divide(Add(Multiply(stages[stage], period - 1), input), period)
                            : Round(
                                (period - 1) * Units(stages[stage]) + Units(input),
                                period * Grid
                            );
                    else
                        stages[stage] = native
                            ? Add(stages[stage], Multiply(alpha, Subtract(input, stages[stage])))
                            : Round(
                                (period - 1) * Units(stages[stage]) + 2 * Units(input),
                                (period + 1L) * Grid
                            );
                    input = stages[stage];
                }
            }
            if (order == 1)
                output[i] = stages[0];
            else if (native)
                output[i] =
                    order == 2
                        ? Subtract(Multiply(2, stages[0]), stages[1])
                        : Add(Subtract(Multiply(3, stages[0]), Multiply(3, stages[1])), stages[2]);
            else
                output[i] = Round(
                    order * Units(stages[0])
                        - (order == 2 ? 1 : 3) * Units(stages[1])
                        + (order == 3 ? Units(stages[2]) : BigInteger.Zero),
                    Grid
                );
        }
        return output;
    }

    private static double Endpoint(double[] prices, int end, int period)
    {
        var start = end - period + 1;
        var meanX = Divide(
            Enumerable.Range(start, period).Aggregate(0d, (s, i) => Add(s, i + 1)),
            period
        );
        var meanY = Divide(prices.Skip(start).Take(period).Aggregate(0d, Add), period);
        double xx = 0,
            xy = 0;
        for (var i = start; i <= end; i++)
        {
            var dx = Subtract(i + 1, meanX);
            xx = Add(xx, Multiply(dx, dx));
            xy = Add(xy, Multiply(dx, Subtract(prices[i], meanY)));
        }
        var slope = Divide(xy, xx);
        var intercept = Subtract(meanY, Multiply(slope, meanX));
        return Add(Multiply(slope, end + 1), intercept);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([
            1,
            2,
            4,
            8,
            -3,
            -7,
            -2,
            0,
            0,
            3,
            1,
            -9,
            4,
            2,
            3,
            8,
            1,
            -1,
            0,
            7,
            9,
            2,
        ]);
}
