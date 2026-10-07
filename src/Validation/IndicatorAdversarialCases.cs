using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>A named, immutable replay input. Include generator version and seed in generated names.</summary>
public sealed class IndicatorValidationFixture
{
    public IndicatorValidationFixture(string name, IEnumerable<Bar> bars)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A fixture needs a replay name.", nameof(name));
        Name = name;
        Bars = Array.AsReadOnly((bars ?? throw new ArgumentNullException(nameof(bars))).ToArray());
    }
    public string Name { get; }
    public IReadOnlyList<Bar> Bars { get; }
}

/// <summary>Deterministic numerical input classes. Select classes within an indicator's declared domain.</summary>
public static class IndicatorAdversarialCases
{
    public const string GeneratorVersion = "xorshift32-v1";

    public static IReadOnlyList<IndicatorValidationFixture> Generate(int count, uint seed)
    {
        if (count < 2) throw new ArgumentOutOfRangeException(nameof(count));
        var result = new List<IndicatorValidationFixture>();
        foreach (var shape in new[] { "tiny", "large-offset", "large", "alternating-scale", "evicted-spike", "zero", "negative", "subnormal", "overflow-adjacent", "cancelled-spike", "cascaded-cancellation",
            "volume-tiny", "volume-subnormal", "volume-overflow-adjacent", "volume-evicted-spike", "volume-alternating-scale",
            "mixed-ohlc-extremes", "mixed-ohlc-subnormal" })
        {
            var random = seed == 0 ? 0x9e3779b9u : seed;
            var bars = new Bar[count];
            for (var i = 0; i < count; i++)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                var unit = random / (double)uint.MaxValue;
                var close = shape switch
                {
                    "tiny" => 1e-100 * (1 + unit),
                    "large-offset" => 1e12 + Math.Round(unit * 16) / 16,
                    "large" => 1e100 * (1 + unit),
                    "alternating-scale" => i % 2 == 0 ? 1e-100 : 1e100,
                    "evicted-spike" => i == 0 ? 1e100 : 1 + unit,
                    "zero" => 0,
                    "negative" => -1 - unit,
                    "overflow-adjacent" => double.MaxValue * (.5 + unit / 2),
                    "cancelled-spike" => i % 3 == 0 ? 1e100 : i % 3 == 1 ? 1 : -1e100,
                    "cascaded-cancellation" => (i % 4) switch
                    { 0 => 1e100, 1 => 1e84, 2 => -9.9989e99, _ => -1.0998790000010039e96 },
                    _ => double.Epsilon * (1 + random % 16)
                };
                var open = close; var high = close; var low = close;
                var volume = i % 7 == 0 ? 0 : 1d + random % 10000;
                if (shape.StartsWith("volume-", StringComparison.Ordinal))
                {
                    // Isolate volume conditioning from price conditioning and candle range.
                    open = 100 + unit; close = 100 + (1 - unit); high = 102; low = 99;
                    volume = shape switch
                    {
                        "volume-tiny" => 1e-100 * (1 + unit),
                        "volume-subnormal" => double.Epsilon * (1 + random % 16),
                        "volume-overflow-adjacent" => double.MaxValue * (.5 + unit / 2),
                        "volume-evicted-spike" => i == 0 ? 1e100 : 1 + unit,
                        _ => i % 2 == 0 ? 1e-100 : 1e100
                    };
                }
                else if (shape == "mixed-ohlc-extremes")
                {
                    high = double.MaxValue * (.5 + unit / 2); low = -high;
                    open = i % 3 == 0 ? high : i % 3 == 1 ? double.Epsilon : high / 2;
                    close = i % 3 == 0 ? low : i % 3 == 1 ? 2 * double.Epsilon : -high / 4;
                }
                else if (shape == "mixed-ohlc-subnormal")
                {
                    high = 16 * double.Epsilon; low = -high;
                    open = double.Epsilon * (1 + random % 16);
                    close = -double.Epsilon * (1 + (random >> 4) % 16);
                }
                // Every fixture has finite, ordered candles; overflow must come from the calculation.
                bars[i] = new Bar(new DateTime(2021, 1, 4, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                    open, high, low, close, volume);
            }
            result.Add(new IndicatorValidationFixture($"{GeneratorVersion}/seed-{seed}/{shape}", bars));
        }
        return result.AsReadOnly();
    }

    /// <summary>Deterministic deletion shrinking. The predicate must require both domain validity and
    /// the original failure category, so a different exception cannot replace the counterexample.</summary>
    public static IndicatorShrinkResult Shrink(IndicatorValidationFixture fixture,
        Func<IReadOnlyList<Bar>, bool> preservesFailure, int maximumAttempts = 256,
        CancellationToken cancellationToken = default)
    {
        if (fixture is null) throw new ArgumentNullException(nameof(fixture));
        if (preservesFailure is null) throw new ArgumentNullException(nameof(preservesFailure));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        cancellationToken.ThrowIfCancellationRequested();
        var current = fixture.Bars.ToArray();
        var attempts = 1;
        if (!preservesFailure(Array.AsReadOnly(current)))
            throw new ArgumentException("The input does not reproduce the required failure.", nameof(fixture));
        var width = Math.Max(1, current.Length / 2);
        while (current.Length > 0)
        {
            var reduced = false;
            for (var start = 0; start < current.Length; start += width)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (attempts >= maximumAttempts)
                    return new(new IndicatorValidationFixture(fixture.Name + "/shrunk", current), attempts, false);
                var end = Math.Min(current.Length, start + width);
                var candidate = current.Take(start).Concat(current.Skip(end)).ToArray();
                attempts++;
                if (!preservesFailure(Array.AsReadOnly(candidate))) continue;
                current = candidate;
                width = Math.Min(width, Math.Max(1, current.Length));
                reduced = true;
                break;
            }
            if (reduced) continue;
            if (width == 1) break;
            width = Math.Max(1, width / 2);
        }
        return new(new IndicatorValidationFixture(fixture.Name + "/shrunk", current), attempts, true);
    }
}

/// <summary>Complete means deletion-minimal, not globally minimal or mathematically proven.</summary>
public sealed class IndicatorShrinkResult
{
    internal IndicatorShrinkResult(IndicatorValidationFixture fixture, int attempts, bool complete)
    { Fixture = fixture; Attempts = attempts; IsComplete = complete; }
    public IndicatorValidationFixture Fixture { get; }
    public int Attempts { get; }
    public bool IsComplete { get; }
}
