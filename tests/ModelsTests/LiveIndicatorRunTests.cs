using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The same indicators, the same reads, a source whose bars do not run out.
/// </summary>
/// <remarks>
/// The claim under test is the one the whole design rests on: a strategy is one program, and swapping the
/// source is the only difference between backtesting it and running it.
/// </remarks>
public sealed class LiveIndicatorRunTests
{
    private sealed class Doubled : Indicator
    {
        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close * 2;
        }
    }

    private static Bar BarAt(int i, double close) =>
        new(new DateTime(2023, 2, 1, 15, 0, 0, DateTimeKind.Utc).AddMinutes(i),
            close, close + 1, close - 1, close, 1000);

    [Fact]
    public async Task ACustomIndicatorRunsAgainstALiveFeed()
    {
        var feed = Bars.Live();
        var doubled = new Doubled();

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(doubled)
            .BuildAsync();

        run.IsComplete.Should().BeFalse("a live source does not run out");

        for (var i = 0; i < 5; i++)
        {
            feed.Publish(BarAt(i, 10 + i));
        }

        feed.Complete();

        var seen = new List<double>();
        await foreach (var snapshot in run)
        {
            seen.Add(snapshot[doubled]);
        }

        seen.Should().Equal(20, 22, 24, 26, 28);
        run.BarCount.Should().Be(5);
    }

    [Fact]
    public async Task TheLoopBodyIsIdenticalToTheOneOverHistory()
    {
        var bars = Enumerable.Range(0, 20).Select(i => BarAt(i, 100 + i)).ToList();
        var doubled = new Doubled();

        // History.
        using var history = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(doubled)
            .BuildAsync();

        var fromHistory = new List<double>();
        await foreach (var snapshot in history)
        {
            fromHistory.Add(snapshot[doubled]);
        }

        // Live, the same bars pushed through. Only the source line differs.
        var feed = Bars.Live();
        using var live = await new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(doubled)
            .BuildAsync();

        foreach (var bar in bars)
        {
            feed.Publish(bar);
        }

        feed.Complete();

        var fromLive = new List<double>();
        await foreach (var snapshot in live)
        {
            fromLive.Add(snapshot[doubled]);
        }

        fromLive.Should().Equal(fromHistory,
            "the same state over the same bars cannot produce two answers");
    }

    [Fact]
    public async Task ABuiltInRunsLiveThroughItsStreamingState()
    {
        var feed = Bars.Live();
        var sma = new Sma(3);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(sma)
            .BuildAsync();

        foreach (var close in new[] { 10d, 20d, 30d, 40d })
        {
            feed.Publish(BarAt(0, close));
        }

        feed.Complete();

        var values = new List<double>();
        await foreach (var snapshot in run)
        {
            values.Add(snapshot[sma]);
        }

        values.Should().HaveCount(4);
        values[3].Should().BeApproximately(30, 1e-9, "the mean of 20, 30 and 40");
    }

    [Fact]
    public async Task WarmUpBarsAreFedThroughWithoutBeingPublished()
    {
        var warmup = Enumerable.Range(0, 10).Select(i => BarAt(i, 10 + i)).ToList();
        var feed = Bars.Live().WarmedWith(warmup);
        var doubled = new Doubled();

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(doubled)
            .BuildAsync();

        // The warm-up has already been through the states, but none of it is a published bar.
        run.BarCount.Should().Be(0, "warm-up primes the states without being reported as bars");

        feed.Publish(BarAt(99, 50));
        feed.Complete();

        var seen = new List<double>();
        await foreach (var snapshot in run)
        {
            seen.Add(snapshot[doubled]);
        }

        seen.Should().Equal(100);
    }

    [Fact]
    public async Task BuildAsyncDoesNotWaitForALiveSourceToRunOut()
    {
        // Regression: the finite path drains the source into lists before computing, and the live check was
        // placed after that drain - so BuildAsync read a source that never ends and never returned.
        var feed = Bars.Live();
        var build = new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(new Doubled())
            .BuildAsync();

        var finished = await Task.WhenAny(build, Task.Delay(5000));

        finished.Should().BeSameAs(build, "a live source has no end to wait for");
        using var run = await build;
        run.BarCount.Should().Be(0);
    }
    [Fact]
    public async Task LatestIsTheMostRecentBarAndSaysSoBeforeAnyArrive()
    {
        var feed = Bars.Live();
        var doubled = new Doubled();

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(feed)
            .ConfigureIndicators(doubled)
            .BuildAsync();

        var tooEarly = () => run.Latest;
        tooEarly.Should().Throw<InvalidOperationException>().WithMessage("*No bars*");

        feed.Publish(BarAt(0, 7));
        feed.Publish(BarAt(1, 9));
        feed.Complete();

        await foreach (var _ in run)
        {
            // Draining is what advances the run.
        }

        run.Latest[doubled].Should().Be(18);
        run.Latest.Bar.Close.Should().Be(9);
    }
}
