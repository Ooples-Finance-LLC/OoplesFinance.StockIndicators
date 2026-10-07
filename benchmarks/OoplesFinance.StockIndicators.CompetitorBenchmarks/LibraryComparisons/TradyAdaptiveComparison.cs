using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TradyAdaptiveComparison
{
    internal static ComparisonPair Pair(int fast = 2, int slow = 30) =>
        new(
            "Trady.Indicator.KaufmanAdaptiveMovingAverage",
            nameof(SeededAdaptiveAverage),
            (d, p) =>
                Series(
                    new Trady.Analysis.Indicator.KaufmanAdaptiveMovingAverage(
                        d.Candles,
                        p,
                        fast,
                        slow
                    )
                        .Compute()
                        .Select(r => (double?)r.Tick)
                        .ToArray()
                ),
            (d, p) => Owned(d.IndicatorBars, p, fast, slow),
            (d, p) =>
                Series(
                    SeededAdaptiveComparison.ReferenceValues(
                        d.Closes,
                        p,
                        fast,
                        slow,
                        false,
                        false,
                        false
                    )[0]
                ),
            ["Kama"],
            MinimumInputCount: 0,
            CompetitorReference: (d, p) =>
                Series(
                    DecimalReference(
                            d.Candles.Select(c => (decimal?)c.Close).ToArray(),
                            p,
                            fast,
                            slow
                        )
                        .Select(v => (double?)v)
                        .ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[] values) =>
        RetrospectivePriceComparison.Series(["Kama"], [values]);

    internal static ComparisonSeries Owned(Bar[] bars, int p, int fast, int slow)
    {
        var indicator = new SeededAdaptiveAverage(p, fast, slow, resetFlat: false);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var v = run[indicator.Average].ToArray();
        var f = run[indicator.AverageIsDefined].ToArray();
        return Series(v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray());
    }

    internal static decimal?[] DecimalReference(decimal?[] input, int p, int fast, int slow)
    {
        var r = new decimal?[input.Length];
        if (p > input.Length)
            return r;
        r[p - 1] = input[p - 1];
        for (var i = p; i < input.Length; i++)
        {
            decimal? change =
                input[i].HasValue && input[i - p].HasValue
                    ? Math.Abs(input[i]!.Value - input[i - p]!.Value)
                    : null;
            decimal volatility = 0;
            for (var j = i - p + 1; j <= i; j++)
                if (input[j].HasValue && input[j - 1].HasValue)
                    volatility += Math.Abs(input[j]!.Value - input[j - 1]!.Value);
            decimal? er = volatility > 0 ? change / volatility : null;
            // The pinned native smoothing delegate dereferences ER, including its flat-window null.
            if (!er.HasValue)
                throw new InvalidOperationException("Nullable object must have a value.");
            var rate = 2.0m / (slow + 1) + er.Value * (2.0m / (fast + 1) - 2.0m / (slow + 1));
            var square = rate * rate;
            r[i] = r[i - 1] + square * (input[i] - r[i - 1]);
        }
        return r;
    }

    internal static int Check(ComparisonPair pair, CompetitorData d, int p)
    {
        var flat = false;
        for (var i = p; i < d.Count && !flat; i++)
        {
            var window = d.Candles.Skip(i - p).Take(p + 1).Select(v => v.Close);
            flat = window.Distinct().Count() == 1;
        }
        if (!flat)
            return ComparisonVerifier.Check(pair, d, p);
        var expected = pair.Reference!(d, p);
        ComparisonVerifier.Compare(
            expected,
            pair.Ooples(d, p),
            pair.Id + " flat owned",
            IndicatorErrorBudget.Exact
        );
        try
        {
            pair.Competitor(d, p);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == "Nullable object must have a value.")
        {
            return expected.Outputs["Kama"].Present!.Count(v => v);
        }
        throw new InvalidOperationException("Expected Trady's missing flat efficiency exception.");
    }
}
