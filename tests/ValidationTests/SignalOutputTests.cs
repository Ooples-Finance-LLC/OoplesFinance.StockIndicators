using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A named signal output has to be the signal. A fast arm that dispatches on the options type alone answers
/// every request with the indicator's primary series, so asking for the signal returned the series it is
/// supposed to smooth - silently, and in more than one indicator. Each one here is held against the v1
/// calculation that publishes both series.
/// </summary>
public sealed class SignalOutputTests
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

    [Fact]
    public void AdxPublishesItsTwoDirectionalIndicatorsAndNotTheAdxLineThreeTimes()
    {
        var bars = Walk(150);
        var adx = new Adx();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(adx)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAverageDirectionalIndex();

        run[adx.DiPlus].ToArray().Should().Equal(batch.OutputValues["DiPlus"].ToArray());
        run[adx.DiMinus].ToArray().Should().Equal(batch.OutputValues["DiMinus"].ToArray());
        run[adx.Value].ToArray().Should().Equal(batch.OutputValues["Adx"].ToArray());
    }

    [Fact]
    public void AroonPublishesItsTwoLegsAndNotTheOscillatorThreeTimes()
    {
        var bars = Walk(150);
        var aroon = new Aroon();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(aroon)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateAroonOscillator();

        var value = run[aroon.Value].ToArray();
        var up = run[aroon.AroonUp].ToArray();
        var down = run[aroon.AroonDown].ToArray();

        value.Should().Equal(batch.OutputValues["Aroon"].ToArray());
        up.Should().Equal(batch.OutputValues["AroonUp"].ToArray());
        down.Should().Equal(batch.OutputValues["AroonDown"].ToArray());

        // The oscillator is the difference of the two legs, so all three carrying one series - which is
        // what the builder used to answer - is arithmetically impossible rather than merely imprecise.
        for (var i = 0; i < value.Length; i++)
        {
            value[i].Should().BeApproximately(up[i] - down[i], 1e-9);
        }
    }

    [Fact]
    public void TheStochasticRsiSignalIsTheSecondSmoothingAndNotTheFirst()
    {
        var bars = Walk(150);
        var srsi = new StochasticRelativeStrengthIndex();

        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(srsi)
            .BuildAsync().GetAwaiter().GetResult();

        var batch = new StockData(
            bars.Select(b => b.Open).ToList(), bars.Select(b => b.High).ToList(),
            bars.Select(b => b.Low).ToList(), bars.Select(b => b.Close).ToList(),
            bars.Select(b => (double)b.Volume).ToList(), bars.Select(b => b.Time).ToList())
            .CalculateStochasticRelativeStrengthIndex();

        var series = run[srsi.StochRsi].ToArray();
        var signal = run[srsi.Signal].ToArray();

        // FastD is the first smoothing of the stochastic and SlowD the second, so the signal is a smoothing
        // of the series rather than the series itself.
        series.Should().Equal(batch.OutputValues["StochRsi"].ToArray());
        signal.Should().Equal(batch.OutputValues["Signal"].ToArray());
        signal.Should().NotEqual(series, "the second smoothing is not the first");
    }
}
