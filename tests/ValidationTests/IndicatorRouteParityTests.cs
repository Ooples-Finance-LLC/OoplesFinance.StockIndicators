using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// An indicator is computed one of two ways: a zero-allocation fast path when it has one, and its stateful
/// twin otherwise. Which route runs is an implementation detail nobody configures, so the two have to be the
/// same arithmetic - a backtest that took one and a live run that took the other would disagree about the
/// same bars, which is the defect this layer exists to prevent.
/// </summary>
/// <remarks>
/// MovingAvgTypeRouteParityTests asks this of a MovingAvgType. This asks it of an indicator, which is the
/// larger surface: 130 of them have both routes.
/// </remarks>
public sealed class IndicatorRouteParityTests
{
    private const double Tolerance_ = 1e-8;

    private static StockData Walk(int count = 200)
    {
        var random = new Random(31);
        var o = new List<double>(count); var h = new List<double>(count); var l = new List<double>(count);
        var c = new List<double>(count); var v = new List<double>(count); var d = new List<DateTime>(count);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            o.Add(open);
            h.Add(Math.Max(open, close) + random.NextDouble());
            l.Add(Math.Max(0.01, Math.Min(open, close) - random.NextDouble()));
            c.Add(close);
            v.Add(random.Next(1000, 400000));
            d.Add(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i));
            last = close;
        }

        return new StockData(o, h, l, c, v, d);
    }

    [Fact]
    public void EveryIndicatorWithAFastPathComputesWhatItsStatefulTwinComputes()
    {
        var disagreed = new List<string>();
        var failed = new List<string>();
        var noTwin = new List<string>();
        var compared = 0;

        foreach (var type in typeof(IIndicator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(t => t.Namespace == "OoplesFinance.StockIndicators.Indicators")
            .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var ctor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().All(p => p.IsOptional));
            if (ctor is null)
            {
                continue;
            }

            IBuiltInIndicator builtIn;
            IndicatorSpec spec;
            try
            {
                if (ctor.Invoke(ctor.GetParameters().Select(p => p.DefaultValue).ToArray())
                    is not IBuiltInIndicator instance)
                {
                    continue;
                }

                builtIn = instance;
                spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            }
            catch (Exception ex)
            {
                // A route that THROWS is a failure, not an indicator out of scope. Swallowing it removed the
                // indicator before it was counted, so the sweep could stay green while a route was broken.
                if (ex is NotSupportedException) { noTwin.Add(type.Name); } else { failed.Add(type.Name + ": " + ex.GetType().Name + " - " + ex.Message); }
                continue;
            }

            double[] viaFastPath;
            try
            {
                using var context = new ComputeContext();
                var buffer = IndicatorCompute.TryComputeFast(Walk(), spec, context);
                if (buffer is null)
                {
                    continue;
                }

                using (buffer.Value)
                {
                    viaFastPath = buffer.Value.Span.ToArray();
                }
            }
            catch (Exception ex)
            {
                // A route that THROWS is a failure, not an indicator out of scope. Swallowing it removed the
                // indicator before it was counted, so the sweep could stay green while a route was broken.
                if (ex is NotSupportedException) { noTwin.Add(type.Name); } else { failed.Add(type.Name + ": " + ex.GetType().Name + " - " + ex.Message); }
                continue;
            }

            double[] viaState;
            try
            {
                var state = StatefulIndicatorFactory.Create(spec);
                if (state is null)
                {
                    continue;
                }

                // The slot the spec asks for. Without the key, ComputeAll answers with the primary series
                // whatever was requested, so a named output reads as its sibling - VortexNegative reported
                // ViPlus under the ViMinus key, which looked like a defect in the library rather than in
                // the question being asked of it.
                viaState = BatchCompute.ComputeAll(Walk(), state, builtIn.BatchOutputKey);
            }
            catch (Exception ex)
            {
                // A route that THROWS is a failure, not an indicator out of scope. Swallowing it removed the
                // indicator before it was counted, so the sweep could stay green while a route was broken.
                if (ex is NotSupportedException) { noTwin.Add(type.Name); } else { failed.Add(type.Name + ": " + ex.GetType().Name + " - " + ex.Message); }
                continue;
            }

            if (viaState is null || viaFastPath.Length != viaState.Length)
            {
                continue;
            }

            compared++;
            for (var i = 0; i < viaFastPath.Length; i++)
            {
                if (!double.IsFinite(viaFastPath[i]) || !double.IsFinite(viaState[i]))
                {
                    disagreed.Add(type.Name + ": bar " + i + " is " + viaFastPath[i] + " by the fast path and "
                        + viaState[i] + " by its state");
                    break;
                }

                if (Math.Abs(viaFastPath[i] - viaState[i]) > Tolerance_)
                {
                    disagreed.Add(type.Name + ": bar " + i + " is " + viaFastPath[i].ToString("G8")
                        + " by the fast path and " + viaState[i].ToString("G8") + " by its state");
                    break;
                }
            }
        }

        // Reported before the count, because a route that threw was previously dropped before being
        // counted: the sweep could satisfy its own threshold while a route was broken.
        failed.Should().BeEmpty("a route that throws is a failure, not an indicator out of scope");

        // The out-of-scope count is asserted too: an indicator that quietly stops having a stateful twin
        // would otherwise leave the sweep smaller without anything saying so.
        noTwin.Should().HaveCountLessThanOrEqualTo(635,
            "635 indicators have no stateful twin and so are not compared here at all - this sweep covers "
            + "the ones that do, and the ceiling is a ratchet so the covered set cannot quietly shrink");

        compared.Should().BeGreaterThan(100, "the indicators with both routes are what this exists to check");
        disagreed.Should().BeEmpty("which route runs is not something a caller chooses; " + compared
            + " indicators compared, " + disagreed.Count + " disagree: " + string.Join(" | ", disagreed.Take(12)));
    }
}
