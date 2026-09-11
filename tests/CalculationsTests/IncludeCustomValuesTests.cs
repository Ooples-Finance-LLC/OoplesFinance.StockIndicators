using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using IndicatorOptions = OoplesFinance.StockIndicators.Models.IndicatorOptions;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// IncludeCustomValues decides whether an indicator publishes its single series, never what it computes.
/// </summary>
/// <remarks>
/// Composite indicators read the series their components leave on <see cref="StockData.CustomValuesList"/>,
/// and a caller chains the next indicator the same way. Turning publication off must hide the series from
/// the caller and nothing else: every indicator completes, and every published output series is the one
/// it publishes with the option on.
/// </remarks>
public sealed class IncludeCustomValuesTests : GlobalTestData
{
    private const int Bars = 251;

    [Fact]
    public void TurningCustomValuesOffChangesNoOutput()
    {
        var bars = StockTestData.Take(Bars).ToList();
        var failed = new List<string>();
        var differs = new List<string>();
        var cannotRun = new List<string>();
        var noMethod = 0;
        var compared = 0;

        foreach (var name in Enum.GetValues(typeof(IndicatorName)).Cast<IndicatorName>().OrderBy(n => n.ToString(), StringComparer.Ordinal))
        {
            var method = IndicatorInvoker.GetMethod(name);
            if (method is null)
            {
                noMethod++;
                continue;
            }

            StockData on;
            try
            {
                on = Invoke(method, bars, new IndicatorOptions());
            }
            catch (Exception ex)
            {
                // Named, not this test's subject: an indicator that cannot run on its defaults at all.
                cannotRun.Add($"{name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            StockData off;
            try
            {
                off = Invoke(method, bars, new IndicatorOptions { IncludeCustomValues = false });
            }
            catch (Exception ex)
            {
                failed.Add($"{name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;
            foreach (var kv in on.OutputValues)
            {
                if (!off.OutputValues.TryGetValue(kv.Key, out var offSeries) || offSeries.Count != kv.Value.Count)
                {
                    differs.Add($"{name} {kv.Key}: missing or a different length");
                    break;
                }

                var bar = Enumerable.Range(0, kv.Value.Count).FirstOrDefault(i => !IsClose(kv.Value[i], offSeries[i]), -1);
                if (bar >= 0)
                {
                    differs.Add($"{name} {kv.Key} at bar {bar}: {kv.Value[bar]} on, {offSeries[bar]} off");
                    break;
                }
            }
        }

        using var scope = new AssertionScope();
        compared.Should().BeGreaterThan(700,
            $"every batch indicator is run both ways; {noMethod} names have no batch method and " +
            $"{cannotRun.Count} cannot run on their defaults: {string.Join(", ", cannotRun)}");
        failed.Should().BeEmpty($"every indicator completes with custom values off ({failed.Count}): {string.Join(", ", failed)}");
        differs.Should().BeEmpty($"custom values off changes no output ({differs.Count}): {string.Join(" | ", differs)}");
    }

    private static StockData Invoke(MethodInfo method, List<TickerData> bars, IndicatorOptions options)
    {
        var ps = method.GetParameters();
        var args = new object?[ps.Length];
        args[0] = new StockData(bars) { Options = options };
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        return (StockData)(method.Invoke(null, args)
            ?? throw new InvalidOperationException($"{method.Name} returned null instead of its StockData"));
    }
}
