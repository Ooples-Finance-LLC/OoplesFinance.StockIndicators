using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Compares every series the builder publishes against the same key from the indicator's v1 batch.
/// </summary>
/// <remarks>
/// <para>
/// The rest of the suite checks invariants - a constant series in, that constant out - and route parity,
/// which compares the fast path against the stateful twin and the batch. None of that compares a published
/// series against v1 KEY BY KEY, so three routes agreeing on the wrong number stayed green. That is how a
/// defect where every named output answered with the indicator's primary series survived: the primary was
/// usually right, so every route agreed, and nothing ever asked what UpperBand or Signal actually held.
/// </para>
/// <para>
/// The known divergences below are an INVERTED allow-list. A divergence that is not listed fails the test,
/// so a new one cannot be introduced; and a listed entry that now agrees ALSO fails it, so the list cannot
/// go stale and has to shrink as each is fixed. Never add an entry to silence a new failure - the entry is
/// a record of a defect that was already there when the sweep was first committed.
/// </para>
/// </remarks>
public sealed class PublishedOutputParityTests
{
    // Every entry is a series the builder publishes that does not match its v1 batch. Delete an entry when
    // the indicator is fixed; the test tells you to.
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void EveryPublishedSeriesMatchesItsBatchExceptTheKnownDivergences()
    {
        var bars = Walk(150);

        StockData Batch() => new(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        static bool Same(double[] mine, List<double> theirs) =>
            mine.Length == theirs.Count && mine.Select((x, i) => double.IsFinite(x) && double.IsFinite(theirs[i])
                && Math.Abs(x - theirs[i]) <= 1e-8).All(equal => equal);

        var diverged = new HashSet<string>(StringComparer.Ordinal);
        var swallowed = new List<string>();
        var compared = 0;

        foreach (var testCase in IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
            .Where(c => c.Name == "default" && typeof(MultiOutputIndicatorBase).IsAssignableFrom(c.IndicatorType)
                && typeof(IBuiltInIndicator).IsAssignableFrom(c.IndicatorType)))
        {
            var type = testCase.IndicatorType;
            IIndicator indicator;
            IBuiltInIndicator builtIn;
            try
            {
                indicator = testCase.Factory();
                builtIn = Assert.IsAssignableFrom<IBuiltInIndicator>(indicator);
            }
            catch (Exception ex)
            {
                swallowed.Add(type.Name + ": " + (ex.InnerException ?? ex).GetType().Name);
                continue;
            }

            var options = builtIn.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target),
                type.Name + " has no batch target.");

            Dictionary<string, List<double>> published;
            try
            {
                published = GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName).ToDictionary(key => key,
                    key => BuilderArmBinding.Compute(Batch(), new IndicatorSpec(builtIn.BatchName, options, key), target));
            }
            catch (Exception ex)
            {
                swallowed.Add(type.Name + ": " + (ex.InnerException ?? ex).GetType().Name);
                continue;
            }

            double[][] mine;
            try
            {
                using var run = new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator)
                    .BuildAsync().GetAwaiter().GetResult();
                mine = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
            }
            catch (Exception ex)
            {
                swallowed.Add(type.Name + ": " + ex.GetType().Name);
                continue;
            }

            Assert.True(mine.Length >= 2, type.Name + " must expose multiple outputs.");

            // Outputs are in the order the batch publishes its keys, so the two line up by position.
            var keys = published.Keys.ToList();
            Assert.Equal(keys.Count, mine.Length);
            for (var slot = 0; slot < mine.Length; slot++)
            {
                compared++;
                if (!Same(mine[slot], published[keys[slot]]))
                {
                    diverged.Add(type.Name + "." + keys[slot]);
                }
            }
        }

        // A positive control: the sweep has to be reaching enough series for its verdict to mean anything.
        compared.Should().BeGreaterThan(500, "the sweep must actually compare series for its result to mean anything");
        // An indicator whose construction or batch call THROWS was dropped before it could be compared,
        // and a count of what was compared cannot show that. Measured at 0 today; the ceiling is a ratchet.
        swallowed.Should().HaveCountLessThanOrEqualTo(0,
            "an indicator that throws is never compared: " + string.Join(", ", swallowed));

        var appeared = diverged.Except(KnownDivergences).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fixedSince = KnownDivergences.Except(diverged).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(appeared.Count == 0,
            "Published outputs differ from their batch keys: " + string.Join(", ", appeared));

        fixedSince.Should().BeEmpty(
            "these series now match their batch, so delete them from KnownDivergences - the list has to shrink");
    }

    private static List<Bar> Walk(int count)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var last = 100d;
        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i),
                open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }
}
