using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Streaming;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// A typed Builder spec streams what it computes in batch.
/// </summary>
/// <remarks>
/// The Builder builds a streaming state from a typed spec with a table of its own,
/// <c>StatefulIndicatorFactory</c>, beside the arm binding its batch path uses. The two decided separately which
/// parameter an option sets, so the same spec could compute one indicator in batch and another in streaming:
/// Connors RSI's length set the streak RSI in one and the price RSI in the other, the Schaff Trend Cycle's the
/// fast MACD length in one and its cycle in the other. This holds every spec the table builds, at the same two
/// parameter sets <see cref="BuilderArmTests"/> uses, to the batch indicator bar by bar.
/// </remarks>
public sealed class BuilderStreamingArmTests : GlobalTestData
{
    [Fact]
    public void EveryTypedSpecStreamsItsBatchIndicator()
    {
        var tickers = StockTestData.ToList();
        var bars = tickers.Select(t => new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close,
            t.Volume, isFinal: true)).ToList();
        var failures = new List<string>();
        var compared = 0;

        foreach (var type in BuilderArmTests.OptionTypes)
        {
            if (!BuilderArmBinding.TryGetTarget(type, out var target))
            {
                continue;
            }

            foreach (var alternate in new[] { false, true })
            {
                var options = BuilderArmTests.Create(type, alternate);
                if (options is null)
                {
                    break;
                }

                var spec = new IndicatorSpec(target.Name, options, IndicatorOutput.Primary);
                IStreamingIndicatorState state;
                try
                {
                    state = StatefulIndicatorFactory.Create(spec);
                }
                catch (NotSupportedException)
                {
                    // Not in the typed table: the Builder cannot stream this spec, so there is nothing to disagree.
                    break;
                }

                compared++;
                var label = $"{type.Name}{(alternate ? " (alternate parameters)" : string.Empty)}";
                try
                {
                    var streamed = bars.Select(bar =>
                    {
                        var result = state.Update(bar, isFinal: true, includeOutputs: true);
                        return target.OutputKey is { } key
                            ? result.Outputs is { } outputs && outputs.TryGetValue(key, out var value) ? value : double.NaN
                            : result.Value;
                    }).ToList();
                    var expected = BuilderArmBinding.Compute(new StockData(tickers), spec, target);
                    var first = Enumerable.Range(0, Math.Min(streamed.Count, expected.Count)).FirstOrDefault(i => !IsClose(expected[i], streamed[i]), -1);
                    if (streamed.Count != expected.Count)
                    {
                        failures.Add($"{label}: {streamed.Count} values streamed, batch {expected.Count}");
                    }
                    else if (first >= 0)
                    {
                        failures.Add($"{label} bar {first}: streamed {streamed[first]}, batch {target.Name} {expected[first]}");
                    }
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    failures.Add($"{label}: {inner.GetType().Name} {inner.Message}");
                }
            }
        }

        compared.Should().BeGreaterThan(50, "every typed spec the streaming table builds is compared");
        failures.Should().BeEmpty($"{compared} streamed specs compared: {string.Join(" | ", failures)}");
    }
}
