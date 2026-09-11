using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;
using IndicatorOptions = OoplesFinance.StockIndicators.Models.IndicatorOptions;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// IncludeOutputValues decides whether an indicator publishes its named series, never what it computes.
/// </summary>
/// <remarks>
/// Composites read their components' named series - a MACD's signal line, a band's upper band - from
/// <see cref="StockData.OutputValues"/>. Turning publication off must hide them from the caller only: every
/// indicator completes, and its single series is the one it publishes with the option on.
/// </remarks>
public sealed class IncludeOutputValuesTests : GlobalTestData
{
    private const int Bars = 251;

    [Fact]
    public void TurningOutputValuesOffChangesNoResult()
    {
        var bars = StockTestData.Take(Bars).ToList();
        var failed = new List<string>();
        var differs = new List<string>();
        var cannotRun = new List<string>();
        var compared = 0;

        foreach (var name in Enum.GetValues(typeof(IndicatorName)).Cast<IndicatorName>().OrderBy(n => n.ToString(), StringComparer.Ordinal))
        {
            var method = IndicatorInvoker.GetMethod(name);
            if (method is null)
            {
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
                off = Invoke(method, bars, new IndicatorOptions { IncludeOutputValues = false });
            }
            catch (Exception ex)
            {
                failed.Add($"{name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;
            if (off.OutputValues.Count != 0)
            {
                differs.Add($"{name}: published {off.OutputValues.Count} named series with the option off");
            }

            if (on.CustomValuesList.Count != off.CustomValuesList.Count)
            {
                differs.Add($"{name}: {on.CustomValuesList.Count} values on, {off.CustomValuesList.Count} off");
                continue;
            }

            var bar = Enumerable.Range(0, on.CustomValuesList.Count)
                .FirstOrDefault(i => !IsClose(on.CustomValuesList[i], off.CustomValuesList[i]), -1);
            if (bar >= 0)
            {
                differs.Add($"{name} at bar {bar}: {on.CustomValuesList[bar]} on, {off.CustomValuesList[bar]} off");
            }
        }

        using var scope = new AssertionScope();
        compared.Should().BeGreaterThan(700,
            $"every batch indicator is run both ways; {cannotRun.Count} cannot run on their defaults: {string.Join(", ", cannotRun)}");
        failed.Should().BeEmpty($"every indicator completes with output values off ({failed.Count}): {string.Join(", ", failed)}");
        differs.Should().BeEmpty($"output values off changes no result ({differs.Count}): {string.Join(" | ", differs)}");
    }

    private static StockData Invoke(MethodInfo method, List<TickerData> bars, IndicatorOptions options)
    {
        var ps = method.GetParameters();
        var args = new object?[ps.Length];
        args[0] = new StockData(bars) { Options = options };
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        return method.Invoke(null, args) as StockData
            ?? throw new InvalidOperationException($"{method.Name} returned null instead of its StockData");
    }
}
