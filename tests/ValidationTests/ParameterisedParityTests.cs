using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Compares indicators against their batches at lengths other than the default.
/// </summary>
/// <remarks>
/// <para>
/// The other two parity tests build every indicator with its default constructor and call the batch with
/// <see cref="Type.Missing"/> for every argument, so only the default parameter path was ever compared. An
/// arm that reads the right formula at one length and the wrong one at another would pass both.
/// </para>
/// <para>
/// The pairing is by parameter NAME on both sides - a constructor parameter called <c>length</c> against a
/// batch parameter called <c>length</c> - so it cannot silently transpose two arguments the way matching by
/// position could.
/// </para>
/// <para>
/// Each comparison builds a FRESH <see cref="StockData"/>, because a v1 calculation mutates the instance it
/// is called on: reusing one across calls makes every call after the first read the previous call's output
/// and manufactures divergences that are not there.
/// </para>
/// </remarks>
public sealed class ParameterisedParityTests
{
    private static readonly int[] Lengths = [5, 14, 30];

    // Indicators that disagree with their batch at one or more of the lengths above.
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void EveryLengthTakingIndicatorMatchesItsBatchAtEveryLength()
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
        var comparisons = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(t => !typeof(MultiOutputIndicatorBase).IsAssignableFrom(t))
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var constructor = type.GetConstructors().FirstOrDefault(c =>
                c.GetParameters().Any(p => p.Name == "length") && c.GetParameters().All(p => p.IsOptional));
            if (constructor is null) { continue; }

            IBuiltInIndicator? probe;
            try
            {
                probe = constructor.Invoke(constructor.GetParameters().Select(p => p.DefaultValue).ToArray())
                    as IBuiltInIndicator;
                if (probe is null) { continue; }
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (!calculations.TryGetValue("Calculate" + probe.BatchName, out var method)) { continue; }

            var batchLength = method.GetParameters().FirstOrDefault(p => p.Name == "length");
            if (batchLength is null || batchLength.ParameterType != typeof(int)) { continue; }

            foreach (var length in Lengths)
            {
                IIndicator indicator;
                List<double> theirs;
                double[] mine;
                try
                {
                    indicator = (IIndicator)constructor.Invoke(constructor.GetParameters()
                        .Select(p => p.Name == "length" ? (object?)length : p.DefaultValue).ToArray());
                    var builtIn = (IBuiltInIndicator)indicator;

                    var arguments = method.GetParameters()
                        .Select((p, i) => i == 0 ? (object?)Batch() : p.Name == "length" ? length : Type.Missing)
                        .ToArray();
                    if (method.Invoke(null, arguments) is not StockData result) { continue; }

                    var key = builtIn.BatchOutputKey;
                    theirs = key is not null && result.OutputValues.TryGetValue(key, out var named)
                        ? named
                        : result.CustomValuesList;
                    if (theirs.Count == 0) { continue; }

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

                comparisons++;
                if (mine.Length != theirs.Count || mine.Where((x, i) => Math.Abs(x - theirs[i]) > 1e-8).Any())
                {
                    diverged.Add(type.Name);
                }
            }
        }

        // A positive control: this must actually reach the library at several lengths.
        comparisons.Should().BeGreaterThan(900, "the sweep must run its comparisons for its result to mean anything");

        var appeared = diverged.Except(KnownDivergences).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fixedSince = KnownDivergences.Except(diverged).OrderBy(x => x, StringComparer.Ordinal).ToList();

        appeared.Should().BeEmpty("these indicators disagree with their batch away from the default length");
        fixedSince.Should().BeEmpty("these now agree at every length, so delete them from KnownDivergences");
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