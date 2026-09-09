using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// Every batch indicator must return one value per input bar.
///
/// <para><b>The invariant.</b> A <see cref="StockData"/> carries <c>TickerDataList</c> and the value lists an
/// indicator writes back into it. Callers — including this library's own chained calculations — index those in
/// parallel. When an indicator writes a SHORTER list, index <c>i</c> no longer means the same bar in both, and
/// every downstream calculation on that result is silently reading misaligned data.</para>
///
/// <para><b>How it was found.</b> A consumer's alignment guard rejected
/// <c>WoodiePivotPoints</c> for returning 195 rows against 319 bars. The pivot family aggregates bars into
/// calendar periods via <c>GetInputValuesList(stockData, inputLength)</c> and stored the per-PERIOD series
/// straight back onto a per-BAR StockData.</para>
///
/// <para><b>Why bar-aligned is the right contract</b>, rather than teaching callers to cope: it is what
/// TA-Lib, pandas-ta and Tulip all do — an indicator returns a series the length of its input, padded through
/// the warmup — and it is what this library already assumes everywhere it chains one calculation onto another.
/// A period indicator is still period-based; its levels are simply carried across the bars they apply to.</para>
/// </summary>
public sealed class BarAlignedOutputTests
{
    private const int BarCount = 240;

    [Fact]
    public void Every_batch_indicator_returns_one_value_per_bar()
    {
        var offenders = new List<string>();
        var checkedCount = 0;

        foreach (var method in BatchIndicatorMethods())
        {
            var stockData = new StockData(CreateIntradayBars(BarCount));

            object? result;
            try
            {
                result = method.Invoke(null, BuildArguments(method, stockData));
            }
            catch (Exception)
            {
                // An indicator that cannot run on this shape is a different concern; this test is about the
                // LENGTH of what it returns when it does run.
                continue;
            }

            if (result is not StockData produced || produced.CustomValuesList.Count == 0)
            {
                continue;
            }

            checkedCount++;
            if (produced.CustomValuesList.Count != BarCount)
            {
                offenders.Add(
                    $"{method.DeclaringType?.Name}.{method.Name}: {produced.CustomValuesList.Count} values for {BarCount} bars");
            }
        }

        checkedCount.Should().BeGreaterThan(100, "the reflection sweep should reach most of the catalog");
        offenders.Should().BeEmpty(
            "every indicator must return one value per bar, or callers indexing it alongside TickerDataList "
            + "read misaligned data:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Public <c>Calculate*</c> extension methods taking a StockData and returning one.</summary>
    private static IEnumerable<MethodInfo> BatchIndicatorMethods() =>
        typeof(StockData).Assembly.GetTypes()
            .Where(t => t.IsSealed && t.IsAbstract && t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("Calculate", StringComparison.Ordinal)
                        && m.ReturnType == typeof(StockData)
                        && m.GetParameters().Length > 0
                        && m.GetParameters()[0].ParameterType == typeof(StockData)
                        // Multi-instrument indicators need a second StockData and are out of scope here.
                        && m.GetParameters().Skip(1).All(p => p.ParameterType != typeof(StockData)))
            .OrderBy(m => m.DeclaringType!.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Name, StringComparer.Ordinal);

    /// <summary>The StockData plus every other parameter's default, so each indicator runs as documented.</summary>
    private static object?[] BuildArguments(MethodInfo method, StockData stockData)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = stockData;

        for (var i = 1; i < parameters.Length; i++)
        {
            args[i] = parameters[i].HasDefaultValue
                ? parameters[i].DefaultValue
                : parameters[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(parameters[i].ParameterType)
                    : null;
        }

        return args;
    }

    /// <summary>
    /// Hourly bars spanning several calendar days.
    /// </summary>
    /// <remarks>
    /// INTRADAY ON PURPOSE. A period-aggregating indicator collapses to one row per calendar period, so on
    /// daily bars its output length coincidentally equals the bar count and the defect is invisible. It only
    /// shows when more than one bar shares a period — which is exactly the case the consuming platform hit.
    /// </remarks>
    private static List<TickerData> CreateIntradayBars(int count)
    {
        var bars = new List<TickerData>(count);
        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var price = 100.0;

        for (var i = 0; i < count; i++)
        {
            // Deterministic, no RNG: a length check must not be able to fail intermittently.
            price += Math.Sin(i / 7.0) * 0.5;
            var high = price + 0.75;
            var low = price - 0.75;

            bars.Add(new TickerData
            {
                Date = start.AddHours(i),
                Open = price - 0.25,
                High = high,
                Low = low,
                Close = price,
                Volume = 1000 + (i * 10),
            });
        }

        return bars;
    }
}
