using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Of() and Uses() are the whole point of the typed graph, and a built-in used to ignore both: it was
/// registered against price with whatever its options said, so new Sma(5).Of(rsi) averaged close prices and
/// looked like it had worked. These are the positive controls - each one fails if the built-in goes back to
/// the evaluator, rather than passing for the wrong reason.
/// </summary>
public sealed class BuiltInGraphTests
{
    private static IReadOnlyList<Bar> Walk(int count = 200)
    {
        var random = new Random(31);
        var bars = new List<Bar>(count);
        var start = new DateTime(2021, 1, 4, 14, 30, 0, DateTimeKind.Utc);
        var last = 100d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.6);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.8));
            bars.Add(new Bar(start.AddMinutes(i), open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, random.Next(1000, 400000)));
            last = close;
        }

        return bars;
    }

    [Fact]
    public async Task ABuiltInGivenASourceReadsThatSourceAndNotThePrice()
    {
        var bars = Walk();
        var rsi = new Rsi(14);
        var overRsi = new Sma(5).Of(rsi);
        var overPrice = new Sma(5);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(rsi, overRsi, overPrice)
            .BuildAsync();

        var chained = run[overRsi].ToArray();
        var plain = run[overPrice].ToArray();
        var source = run[rsi].ToArray();

        // The decisive one. An RSI sits in [0, 100] and these closes sit near 100, so averaging the wrong
        // series is not a subtle difference - but assert the equality that has to hold, not just that the
        // two disagree, because disagreeing is also what a broken third answer does.
        for (var i = 5; i < chained.Length; i++)
        {
            var expected = (source[i] + source[i - 1] + source[i - 2] + source[i - 3] + source[i - 4]) / 5;
            chained[i].Should().BeApproximately(expected, 1e-9,
                "a 5 bar average of the RSI is the mean of the last five RSI values");
        }

        chained.Should().NotEqual(plain, "averaging the RSI is not averaging the close");
    }

    [Fact]
    public async Task ABuiltInGivenACallersOwnAverageUsesIt()
    {
        var bars = Walk();
        var mine = new AlwaysSeven();
        var withMine = new BollingerBands(20, 2, mine);
        var withDefault = new BollingerBands(20, 2);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(withMine, withDefault)
            .BuildAsync();

        var middle = run[withMine.Middle].ToArray();
        var standard = run[withDefault.Middle].ToArray();

        // A component whose answer is a constant nobody could reach by accident: if the middle band is 7,
        // the caller's own average was used. If it is the 20 bar mean of the closes, it was discarded.
        middle.Skip(25).Should().AllSatisfy(v => v.Should().BeApproximately(7, 1e-9),
            "the middle band is the average the caller supplied");
        middle.Should().NotEqual(standard, "the supplied average is not the default one");
    }

    /// <summary>An average that is not one of ours and answers a constant, so using it is unmistakable.</summary>
    private sealed class AlwaysSeven : IndicatorBase, IMovingAverage
    {
        public override int WarmupBars => 0;

        protected internal override IIndicatorState CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => 7;
        }
    }
}
