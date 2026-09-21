using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Compares indicators against their batches when told to use an average that is neither simple nor
/// exponential.
/// </summary>
/// <remarks>
/// <para>
/// The other three sweeps compare at the DEFAULT average, and an arm that picks its average from a switch
/// over two enum members and falls through for the rest is correct exactly there. Thirteen arms in
/// IndicatorCompute carry such a switch, so an indicator configured with any other average silently gets a
/// simple or exponential one and nothing noticed.
/// </para>
/// <para>
/// A weighted average is the substitute because it is neither of the two a fall-through lands on. The same
/// average goes to both sides - the instance to the indicator, its <c>AvgType</c> to the batch - so a
/// difference is the arm rather than the two being configured differently.
/// </para>
/// </remarks>
public sealed class MovingAverageTypeParityTests
{
    // Indicators that ignore the average they are given. Delete an entry when the arm is fixed.
    private static readonly HashSet<string> KnownDivergences = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void EveryIndicatorHonoursTheAverageItIsGiven()
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

        var substituteType = typeof(IIndicator).Assembly.GetTypes()
            .Single(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators" && t.Name == "Wma");
        var substituteConstructor = substituteType.GetConstructors()
            .First(c => c.GetParameters().All(p => p.IsOptional));

        object Substitute() => substituteConstructor.Invoke(
            substituteConstructor.GetParameters().Select(p => p.DefaultValue).ToArray());

        var avgType = ((IBuiltInMovingAverage)Substitute()).AvgType;

        var diverged = new HashSet<string>(StringComparer.Ordinal);
        var paired = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .Where(t => !typeof(MultiOutputIndicatorBase).IsAssignableFrom(t))
            .Where(typeof(IIndicator).IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var constructor = type.GetConstructors().FirstOrDefault(c =>
                c.GetParameters().All(p => p.IsOptional)
                && c.GetParameters().Any(p => typeof(IMovingAverage).IsAssignableFrom(p.ParameterType)));
            if (constructor is null) { continue; }

            IIndicator indicator;
            IBuiltInIndicator builtIn;
            try
            {
                indicator = (IIndicator)constructor.Invoke(constructor.GetParameters()
                    .Select(p => typeof(IMovingAverage).IsAssignableFrom(p.ParameterType)
                        ? Substitute()
                        : p.DefaultValue)
                    .ToArray());
                if (indicator is not IBuiltInIndicator built) { continue; }
                builtIn = built;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (!calculations.TryGetValue("Calculate" + builtIn.BatchName, out var method)) { continue; }

            // Only indicators whose batch takes an average can be asked to honour one.
            if (method.GetParameters().All(p => p.ParameterType != typeof(MovingAvgType))) { continue; }
            paired++;

            List<double> theirs;
            double[] mine;
            try
            {
                var arguments = method.GetParameters()
                    .Select((p, i) => i == 0 ? (object?)Batch()
                        : p.ParameterType == typeof(MovingAvgType) ? avgType : Type.Missing)
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

            if (mine.Length != theirs.Count || mine.Where((x, i) => Math.Abs(x - theirs[i]) > 1e-8).Any())
            {
                diverged.Add(type.Name);
            }
        }

        paired.Should().BeGreaterThan(150, "the sweep must reach the indicators that take an average");

        var appeared = diverged.Except(KnownDivergences).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fixedSince = KnownDivergences.Except(diverged).OrderBy(x => x, StringComparer.Ordinal).ToList();

        appeared.Should().BeEmpty("these indicators stopped honouring the average they were given");
        fixedSince.Should().BeEmpty("these honour it now, so delete them from KnownDivergences");
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