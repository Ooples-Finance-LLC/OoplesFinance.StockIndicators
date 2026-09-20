using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// A caller's own indicator, run on the same terms as the library's own.
/// </summary>
/// <remarks>
/// The claim being tested is that a custom indicator is not a lesser thing: it goes in the same list, is read
/// back the same way, can take a built-in as a component, and can be chained onto one.
/// </remarks>
public sealed class CustomIndicatorRunTests
{
    private const int BarCount = 120;

    /// <summary>Gap percent: how far this bar opened from the last one's close.</summary>
    private sealed class GapPercent : Indicator
    {
        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            private double _previousClose;

            public void Reset() => _previousClose = 0;

            public double Update(in Bar bar)
            {
                var gap = _previousClose > 0 ? (bar.Open - _previousClose) / _previousClose * 100 : 0;
                _previousClose = bar.Close;
                return gap;
            }
        }
    }

    /// <summary>Doubles whatever it is reading, so a chained value is obvious in the output.</summary>
    private sealed class Doubled : Indicator
    {
        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close * 2;
        }
    }

    /// <summary>The distance from a component average, which proves components arrive computed.</summary>
    private sealed class DistanceFromAverage : Indicator
    {
        public DistanceFromAverage(IMovingAverage average) => Uses(average);

        protected internal override object CreateState() => new State();

        private sealed class State : IComposedIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar, ReadOnlySpan<double> components) => bar.Close - components[0];
        }
    }

    private sealed class HighAndLow : MultiOutputIndicator
    {
        public HighAndLow() : base(2) => (Top, Bottom) = DeclaredOutputs;

        public IIndicatorOutput Top { get; }

        public IIndicatorOutput Bottom { get; }

        protected internal override object CreateState() => new State();

        private sealed class State : IMultiOutputState
        {
            public void Reset() { }

            public void Update(in Bar bar, Span<double> outputs)
            {
                outputs[0] = bar.High;
                outputs[1] = bar.Low;
            }
        }
    }

    private static IReadOnlyList<Bar> SeededBars(int count)
    {
        var random = new Random(23);
        var bars = new List<Bar>(count);
        var start = new DateTime(2022, 5, 2, 13, 30, 0, DateTimeKind.Utc);
        var last = 50d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.4);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.2));
            bars.Add(new Bar(start.AddMinutes(i), open, Math.Max(open, close) + 0.2,
                Math.Max(0.01, Math.Min(open, close) - 0.2), close, 1000));
            last = close;
        }

        return bars;
    }

    [Fact]
    public async Task ACustomIndicatorRunsBesideABuiltInAndIsReadTheSameWay()
    {
        var bars = SeededBars(BarCount);
        var gap = new GapPercent();
        var rsi = new Rsi(14);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(gap, rsi)
            .BuildAsync();

        var gaps = run[gap].ToArray();
        gaps.Should().HaveCount(BarCount);
        gaps[0].Should().Be(0, "there is no previous close on the first bar");

        // The arithmetic is the caller's, so the values must be exactly what it computed.
        for (var i = 1; i < BarCount; i++)
        {
            var expected = (bars[i].Open - bars[i - 1].Close) / bars[i - 1].Close * 100;
            gaps[i].Should().BeApproximately(expected, 1e-12);
        }

        run[rsi].ToArray().Should().HaveCount(BarCount, "the built-in still works alongside it");
    }

    [Fact]
    public async Task AComponentArrivesAlreadyComputedForEachBar()
    {
        var bars = SeededBars(BarCount);
        var average = new Sma(10);
        var distance = new DistanceFromAverage(average);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(distance, average)
            .BuildAsync();

        var averages = run[average].ToArray();
        var distances = run[distance].ToArray();

        for (var i = 0; i < BarCount; i++)
        {
            distances[i].Should().BeApproximately(bars[i].Close - averages[i], 1e-12,
                "the component's value for that bar is what the state was handed");
        }
    }

    [Fact]
    public async Task AComponentIsComputedEvenWhenItWasNeverConfigured()
    {
        var bars = SeededBars(BarCount);

        // The average is only reachable through the indicator that uses it.
        var distance = new DistanceFromAverage(new Sma(10));

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(distance)
            .BuildAsync();

        run[distance].ToArray().Should().HaveCount(BarCount);
        run[distance].ToArray().Should().Contain(v => v != 0);
    }

    [Fact]
    public async Task ACustomIndicatorCanChainOntoABuiltIn()
    {
        var bars = SeededBars(BarCount);
        var sma = new Sma(10);
        var doubled = new Doubled().Of(sma);

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(doubled, sma)
            .BuildAsync();

        var averages = run[sma].ToArray();
        var values = run[doubled].ToArray();

        for (var i = 0; i < BarCount; i++)
        {
            values[i].Should().BeApproximately(averages[i] * 2, 1e-12,
                "Of() substitutes the source's series for the close");
        }
    }

    [Fact]
    public async Task ACustomMultiOutputIndicatorPublishesEverySeries()
    {
        var bars = SeededBars(BarCount);
        var range = new HighAndLow();

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(range)
            .BuildAsync();

        run[range.Top].ToArray().Should().Equal(bars.Select(b => b.High));
        run[range.Bottom].ToArray().Should().Equal(bars.Select(b => b.Low));
    }

    [Fact]
    public async Task TwoRunsOfTheSameIndicatorDoNotShareState()
    {
        var bars = SeededBars(BarCount);
        var gap = new GapPercent();

        using var first = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(gap).BuildAsync();
        using var second = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(gap).BuildAsync();

        // The state is created per run, so the second is not contaminated by the first.
        second[gap].ToArray().Should().Equal(first[gap].ToArray());
    }
}
