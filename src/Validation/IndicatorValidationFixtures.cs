using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class IndicatorValidationFixtures
{
    internal static IEnumerable<(string Name, IReadOnlyList<Bar> Bars)> Create(int count, int warmup, bool isAverage,
        bool includeNumericalExtremes = false)
    {
        yield return ("empty", Array.Empty<Bar>());
        yield return ("single", Series(1, "flat"));
        if (warmup > 1) yield return ("before-warmup", Series(warmup - 1, "alternating"));
        if (warmup > 0) yield return ("warmup-boundary", Series(warmup + 1, "alternating"));
        foreach (var shape in new[] { "flat", "zero-price", "negative-price", "rising", "falling", "alternating", "spike", "zero-volume", "walk-31", "walk-42", "sessions" })
            yield return (shape, Series(count, shape));
        if (includeNumericalExtremes)
            foreach (var fixture in IndicatorAdversarialCases.Generate(count, 244))
                yield return (fixture.Name, fixture.Bars);
        if (isAverage)
        {
            yield return ("settled-flat-100", Series(Math.Max(4000, Math.Max(count, warmup + 1000)), "flat"));
            yield return ("settled-flat-50", Series(Math.Max(4000, Math.Max(count, warmup + 1000)), "flat-50"));
        }
    }

    private static IReadOnlyList<Bar> Series(int count, string shape)
    {
        var random = new Random(shape == "walk-42" ? 42 : 31);
        var bars = new Bar[count];
        var price = 100d;
        var start = new DateTime(2021, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            var previous = price;
            price = shape switch
            {
                "flat" => 100,
                "flat-50" => 50,
                "zero-price" => 0,
                "negative-price" => -100,
                "rising" => 100 + i * 0.1,
                "falling" => 100 / (1 + i * 0.002),
                "alternating" => i % 2 == 0 ? 80 : 120,
                "spike" => i == count / 2 ? 200 : 100,
                _ => Math.Max(1, price + (random.NextDouble() - 0.5) * 4)
            };
            var flat = shape is "flat" or "flat-50" or "zero-price" or "negative-price";
            var open = flat ? price : previous;
            var time = shape == "sessions" ? start.AddDays(i / 4).AddMinutes(i % 4) : start.AddMinutes(i);
            bars[i] = new Bar(time, open,
                flat ? price : Math.Max(open, price) + 0.5,
                flat ? price : Math.Max(0.01, Math.Min(open, price) - 0.5), price,
                shape == "zero-volume" ? 0 : flat ? 1000 : 1000 + random.Next(100000));
        }
        return Array.AsReadOnly(bars);
    }
}
