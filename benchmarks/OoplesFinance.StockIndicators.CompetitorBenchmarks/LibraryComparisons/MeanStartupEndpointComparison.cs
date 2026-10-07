using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MeanStartupEndpointComparison
{
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Mma",
        "EndpointWeightedAverage",
        Competitor,
        Ooples,
        Reference,
        CompetitorReference: NativeReference
    );

    private static ComparisonSeries Ooples(CompetitorData data, int period)
    {
        var indicator = new EndpointWeightedAverage(period, true);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Mma(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period)
    {
        var endpoint = WindowRegressionComparison
            .Pairs.Single(p => p.Id == "TaLib.Functions.LinearReg")
            .Reference!(data, period)
            .Outputs["Value"]
            .Values;
        var sum = BigInteger.Zero;
        for (var i = 0; i < Math.Min(period - 1, data.Count); i++)
        {
            sum += Units(data.Closes[i]);
            endpoint[i] = Round(sum, Grid * (i + 1));
        }
        return new(0, endpoint);
    }

    private static ComparisonSeries NativeReference(CompetitorData data, int period)
    {
        var values = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var count = Math.Min(i + 1, period);
            var sum = 0d;
            for (var j = i - count + 1; j <= i; j++)
                sum = Add(sum, data.Closes[j]);
            var mean = Divide(sum, count);
            if (count < period)
            {
                values[i] = mean;
                continue;
            }
            var moment = 0d;
            for (var lag = 0; lag < period; lag++)
                moment = Add(moment, Multiply((period - (2 * lag + 1)) / 2d, data.Closes[i - lag]));
            // The pinned package computes this product in unchecked Int32 arithmetic.
            var denominator = unchecked((period + 1) * period);
            values[i] = Add(mean, Divide(Multiply(6, moment), denominator));
        }
        return new(0, values);
    }
}
