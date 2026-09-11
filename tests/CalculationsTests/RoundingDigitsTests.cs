using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using IndicatorOptions = OoplesFinance.StockIndicators.Models.IndicatorOptions;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// RoundingDigits rounds what an indicator publishes, and nothing it computes on.
/// </summary>
/// <remarks>
/// A composite that hands a component an intermediate series through <c>SetCustomValues</c> hands it that
/// series rounded, since publication rounds. The final values are then the rounding of a different
/// computation. The check needs no tolerance: rounding once, after the same computation, gives exactly the
/// unrounded values passed through <see cref="Math.Round(double, int)"/>, so any difference at all is an
/// intermediate that was rounded.
/// </remarks>
public sealed class RoundingDigitsTests : GlobalTestData
{
    private const int Bars = 251;
    private const int Digits = 4;

    [Fact]
    public void RoundingChangesOnlyThePublishedDigits()
    {
        var bars = StockTestData.Take(Bars).ToList();
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

            StockData raw;
            StockData rounded;
            try
            {
                raw = Invoke(method, bars, new IndicatorOptions());
                rounded = Invoke(method, bars, new IndicatorOptions { RoundingDigits = Digits });
            }
            catch (Exception ex)
            {
                // Named, not this test's subject: an indicator that cannot run on its defaults at all.
                cannotRun.Add($"{name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;
            if (!new HashSet<string>(raw.OutputValues.Keys, StringComparer.Ordinal).SetEquals(rounded.OutputValues.Keys))
            {
                differs.Add($"{name}: output keys differ");
                continue;
            }

            var series = raw.OutputValues.Select(kv => (Key: kv.Key, Raw: kv.Value, Rounded: rounded.OutputValues[kv.Key]))
                .Append(("<primary>", raw.CustomValuesList, rounded.CustomValuesList));
            foreach (var (key, rawValues, roundedValues) in series)
            {
                if (rawValues.Count != roundedValues.Count)
                {
                    differs.Add($"{name} {key}: {rawValues.Count} values unrounded, {roundedValues.Count} rounded");
                    break;
                }

                var bar = Enumerable.Range(0, rawValues.Count)
                    .FirstOrDefault(i => !Same(Math.Round(rawValues[i], Digits), roundedValues[i]), -1);
                if (bar >= 0)
                {
                    differs.Add($"{name} {key} at bar {bar}: {Math.Round(rawValues[bar], Digits)} expected, {roundedValues[bar]} published");
                    break;
                }
            }
        }

        using var scope = new AssertionScope();
        compared.Should().BeGreaterThan(700,
            $"every batch indicator is run both ways; {cannotRun.Count} cannot run on their defaults: {string.Join(", ", cannotRun)}");
        differs.Should().BeEmpty(
            $"rounding changes only the published digits; each of these rounded something it computed on ({differs.Count}): " +
            string.Join(" | ", differs));
    }

    private static bool Same(double expected, double actual) =>
        expected.Equals(actual) || (double.IsNaN(expected) && double.IsNaN(actual));

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
