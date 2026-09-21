using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Compares every single-output indicator's series against the series its v1 batch stands for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PublishedOutputParityTests"/> covers the multi-output types; these were compared against
/// nothing at all, which left the larger half of the library resting on route parity alone - and route
/// parity only asks whether the fast path, the stateful twin and the batch agree with each other.
/// </para>
/// <para>
/// The batch marks the series it stands for with <c>SetCustomValues</c>, except where the indicator binds
/// to one named key of a multi-output calculation, which <c>IBuiltInIndicator.BatchOutputKey</c> names.
/// </para>
/// <para>
/// As in the multi-output test, the known divergences are an INVERTED allow-list: an unlisted divergence
/// fails as a regression, and a listed entry that now agrees also fails, so the list cannot go stale.
/// </para>
/// </remarks>
public sealed class SingleOutputParityTests
{
    // Series that do not match their batch. Delete an entry when the indicator is fixed; the test says so.
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.Ordinal)
    {
        "SmoothedVolatilityBands.MiddleBand",
    };

    [Fact]
    public void EverySingleOutputIndicatorMatchesItsBatchExceptTheKnownDivergences()
    {
        var bars = Walk(150);

        StockData Batch() => new(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList());

        var calculations = typeof(StockData).Assembly.GetTypes()
            .Where(t => t.IsAbstract && t.IsSealed && t.Name == "Calculations")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("Calculate", StringComparison.Ordinal))
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var diverged = new HashSet<string>(StringComparer.Ordinal);
        var compared = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(t => !typeof(MultiOutputIndicatorBase).IsAssignableFrom(t))
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().All(p => p.IsOptional));
            if (constructor is null) { continue; }

            IIndicator indicator;
            IBuiltInIndicator builtIn;
            try
            {
                indicator = (IIndicator)constructor.Invoke(constructor.GetParameters().Select(p => p.DefaultValue).ToArray());
                if (indicator is not IBuiltInIndicator built) { continue; }
                builtIn = built;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (!calculations.TryGetValue("Calculate" + builtIn.BatchName, out var method)) { continue; }

            List<double> theirs;
            try
            {
                var arguments = method.GetParameters()
                    .Select((p, i) => i == 0 ? (object?)Batch() : Type.Missing).ToArray();
                if (method.Invoke(null, arguments) is not StockData result) { continue; }

                var key = builtIn.BatchOutputKey;
                theirs = key is not null && result.OutputValues.TryGetValue(key, out var named)
                    ? named
                    : result.CustomValuesList;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (theirs.Count == 0) { continue; }

            double[] mine;
            try
            {
                using var run = new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator)
                    .BuildAsync().GetAwaiter().GetResult();
                mine = run[indicator].ToArray();
            }
            catch (Exception)
            {
                continue;
            }

            compared++;
            if (mine.Length != theirs.Count || mine.Where((x, i) => Math.Abs(x - theirs[i]) > 1e-8).Any())
            {
                diverged.Add(type.Name + (builtIn.BatchOutputKey is null ? string.Empty : "." + builtIn.BatchOutputKey));
            }
        }

        // A positive control: this must reach most of the library, or its silence means nothing.
        compared.Should().BeGreaterThan(400, "the sweep must actually compare indicators for its result to mean anything");

        var appeared = diverged.Except(KnownDivergences).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fixedSince = KnownDivergences.Except(diverged).OrderBy(x => x, StringComparer.Ordinal).ToList();

        appeared.Should().BeEmpty("these indicators stopped matching the series their batch stands for");
        fixedSince.Should().BeEmpty("these now match their batch, so delete them from KnownDivergences");
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