using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DynamicMomentumComparison
{
    internal const string Id = "Trady.Indicator.DynamicMomentumIndex";

    internal static ComparisonPair Pair(
        int sd = 5,
        int smooth = 10,
        int rsi = 14,
        int upper = 30,
        int lower = 5
    ) =>
        new(
            Id,
            nameof(DynamicMomentumSnapshot),
            (d, _) =>
                VolumePriceComparison.Mask(
                    new Trady.Analysis.Indicator.DynamicMomentumIndex(
                        d.Candles,
                        sd,
                        smooth,
                        rsi,
                        upper,
                        lower
                    )
                        .Compute()
                        .Select(v => (double?)v.Tick)
                        .ToArray()
                ),
            (d, _) =>
                VolumePriceComparison.Mask(
                    DynamicMomentumSnapshot
                        .FromValues(
                            d.Closes.Select(x => (double?)x).ToArray(),
                            sd,
                            smooth,
                            rsi,
                            upper,
                            lower
                        )
                        .ToArray()
                ),
            (d, _) =>
                VolumePriceComparison.Mask(
                    Reference(
                        d.Closes.Select(x => (double?)x).ToArray(),
                        sd,
                        smooth,
                        rsi,
                        upper,
                        lower
                    )
                ),
            CompetitorReference: (d, _) =>
                VolumePriceComparison.Mask(
                    NativeReference(
                            d.Candles.Select(c => (decimal?)c.Close).ToArray(),
                            sd,
                            smooth,
                            rsi,
                            upper,
                            lower
                        )
                        .Select(x => (double?)x)
                        .ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double?[] Reference(
        double?[] input,
        int sd,
        int smooth,
        int rsi,
        int upper,
        int lower
    )
    {
        var deviations = new double?[input.Length];
        var result = new double?[input.Length];
        var cache = new Dictionary<int, double?[]>();
        for (var i = sd - 1; i < input.Length; i++)
        {
            var window = input
                .Skip(i - sd + 1)
                .Take(sd)
                .Where(v => v.HasValue)
                .Select(v => Units(v!.Value))
                .ToArray();
            if (window.Length == 0)
            {
                deviations[i] = 0;
                continue;
            }
            // Pairwise distances give the centered sum without the production rolling moments.
            BigInteger distance = 0;
            foreach (var x in window)
            foreach (var y in window)
                distance += (x - y) * (x - y);
            deviations[i] = DispersionReferenceArithmetic.Sqrt(
                distance,
                2 * (BigInteger)window.Length * sd * Grid * Grid
            );
        }
        for (var i = smooth - 1; i < input.Length; i++)
        {
            var window = deviations
                .Skip(i - smooth + 1)
                .Take(smooth)
                .Where(v => v.HasValue)
                .Select(v => Units(v!.Value))
                .ToArray();
            if (window.Length == 0 || !deviations[i].HasValue)
                continue;
            var mean = Round(
                window.Aggregate(BigInteger.Zero, (a, b) => a + b),
                window.Length * Grid
            );
            if (mean == 0)
                continue;
            var v = Round(Units(deviations[i]!.Value), Units(mean));
            if (v == 0)
                continue;
            var period = double.IsPositiveInfinity(v)
                ? lower
                : (int)
                    BigInteger.Min(
                        upper,
                        BigInteger.Max(lower, ((BigInteger)rsi * Grid) / Units(v))
                    );
            if (!cache.TryGetValue(period, out var trajectory))
                cache[period] = trajectory = NullableStrengthComparison.OwnedReference(
                    input,
                    period,
                    NullableStrengthConvention.RelativeStrengthIndex,
                    1
                );
            result[i] = trajectory[i];
        }
        return result;
    }

    internal static decimal?[] NativeReference(
        decimal?[] input,
        int sd,
        int smooth,
        int rsi,
        int upper,
        int lower
    )
    {
        var deviations = new decimal?[input.Length];
        var result = new decimal?[input.Length];
        var cache = new Dictionary<int, decimal?[]>();
        for (var i = sd - 1; i < input.Length; i++)
        {
            var w = input.Skip(i - sd + 1).Take(sd).ToArray();
            var mean = w.Average();
            deviations[i] = Convert.ToDecimal(
                Math.Sqrt((double)(w.Select(x => (x - mean) * (x - mean)).Sum()!.Value / sd))
            );
        }
        for (var i = smooth - 1; i < input.Length; i++)
        {
            var mean = deviations.Skip(i - smooth + 1).Take(smooth).Average();
            var v = mean == 0 ? null : deviations[i] / mean;
            if (!v.HasValue || v == 0)
                continue;
            var period = Math.Max(Math.Min((int)Math.Floor(rsi / v.Value), upper), lower);
            if (!cache.TryGetValue(period, out var trajectory))
                cache[period] = trajectory = NullableStrengthComparison.NativeReference(
                    input,
                    period,
                    NullableStrengthConvention.RelativeStrengthIndex,
                    1
                );
            result[i] = trajectory[i];
        }
        return result;
    }
}
