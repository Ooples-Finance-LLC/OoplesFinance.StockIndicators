using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// The signal line of the Accumulation Distribution Line is an average of the line, and it used to be the
/// line: the fast arm dispatched on the options type alone and answered every request with the primary
/// series, so asking for AdlSignal returned Adl unsmoothed and nothing said so.
/// </summary>
public sealed class AccumulationDistributionSignalTests
{
    private static List<Bar> Walk(int count = 120)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc).AddMinutes(i),
                open, Math.Max(open, close) + 0.5, Math.Min(open, close) - 0.5, close, 1000));
            last = close;
        }

        return bars;
    }

    [Fact]
    public void TheSignalLineIsAnAverageOfTheLineAndNotTheLine()
    {
        var bars = Walk();
        var adl = new AccumulationDistributionLine();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(adl)
            .BuildAsync().GetAwaiter().GetResult();

        var line = run[adl.Adl].ToArray();
        var signal = run[adl.AdlSignal].ToArray();

        // The v1 calculation is the reference both routes reproduce, so the signal is held to it rather
        // than to a number read off a chart.
        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAccumulationDistributionLine();

        line.Should().Equal(batch.OutputValues["Adl"].ToArray(), "the line itself was never in doubt");
        signal.Should().Equal(batch.OutputValues["AdlSignal"].ToArray(),
            "the signal is the average of the line, which is what the batch publishes");
        signal.Should().NotEqual(line, "an average of the line is not the line");
    }
}
