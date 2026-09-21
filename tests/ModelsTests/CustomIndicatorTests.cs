using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// What someone writing their own indicator has to do, and what they get without doing anything.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the v1 extension point, which was <c>StockData Run(StockData)</c> with
/// <c>Publish(string, List&lt;double&gt;)</c>. Each guarantee it asserted is restated here against the v2 run,
/// except two that the v2 shape makes unrepresentable rather than merely detectable:
/// </para>
/// <list type="bullet">
/// <item><c>NoPrimaryDeclared</c> - a single-output indicator IS its own output, so there is no primary to
/// forget to set.</item>
/// <item><c>PublishesWrongLength</c> - a state returns one value per bar, so a series cannot be the wrong
/// length. The v1 shape could only catch that once something indexed past the end.</item>
/// </list>
/// </remarks>
public sealed class CustomIndicatorTests
{
    private const int SampleSize = 200;

    /// <summary>A moving average with bands a multiple of the true range away.</summary>
    [Indicator("Range Bands", Description = "A moving average with bands a multiple of ATR away.")]
    private sealed class RangeBands : MultiOutputIndicatorBase
    {
        private readonly double _multiplier;

        public RangeBands(int length = 20, double multiplier = 2)
            : base(3)
        {
            Length = length;
            _multiplier = multiplier;
            Uses(new Sma(length), new Atr(length));
            (UpperBand, MiddleBand, LowerBand) = DeclaredOutputs;
        }

        public int Length { get; }

        public IIndicatorOutput UpperBand { get; }

        public IIndicatorOutput MiddleBand { get; }

        public IIndicatorOutput LowerBand { get; }

        public override int WarmupBars => Length;

        protected internal override object CreateState() => new State(_multiplier);

        private sealed class State(double multiplier) : IComposedMultiOutputState
        {
            public void Reset() { }

            public void Update(in Bar bar, ReadOnlySpan<double> components, Span<double> outputs)
            {
                var basis = components[0];
                var range = components[1] * multiplier;

                outputs[0] = basis + range;
                outputs[1] = basis;
                outputs[2] = basis - range;
            }
        }
    }

    private static IReadOnlyList<Bar> Bars_(int count)
    {
        var random = new Random(5);
        var bars = new List<Bar>(count);
        var start = new DateTime(2020, 6, 1, 13, 30, 0, DateTimeKind.Utc);
        var last = 75d;

        for (var i = 0; i < count; i++)
        {
            var open = last + ((random.NextDouble() - 0.5) * 0.5);
            var close = Math.Max(1, open + ((random.NextDouble() - 0.5) * 1.5));
            bars.Add(new Bar(start.AddMinutes(i), open, Math.Max(open, close) + random.NextDouble(),
                Math.Max(0.01, Math.Min(open, close) - random.NextDouble()), close, 5000));
            last = close;
        }

        return bars;
    }

    private static async Task<IIndicatorRun> RunAsync(params IIndicator[] indicators) =>
        await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(Bars_(SampleSize)))
            .ConfigureIndicators(indicators)
            .BuildAsync();

    [Fact]
    public async Task CustomIndicator_PublishesItsOutputs()
    {
        var bands = new RangeBands(20, 2);

        using var run = await RunAsync(bands);

        run[bands.UpperBand].ToArray().Should().HaveCount(SampleSize);
        run[bands.MiddleBand].ToArray().Should().HaveCount(SampleSize);
        run[bands.LowerBand].ToArray().Should().HaveCount(SampleSize);

        // What SetPrimary used to decide is now structural: the first declared output is the indicator's own
        // series, which is what run[bands] returns without naming one.
        run[bands].ToArray().Should().Equal(run[bands.UpperBand].ToArray());
    }

    [Fact]
    public async Task CustomIndicator_ChainsBothWaysWithTheBuiltIns()
    {
        var bands = new RangeBands(10);
        var smoothed = new Sma(5).Of(bands);

        using var run = await RunAsync(bands, smoothed);

        run[bands.MiddleBand].ToArray().Should().HaveCount(SampleSize);
        run[smoothed].ToArray().Should().HaveCount(SampleSize,
            "a built-in continues from a custom indicator's series");

        // And the other direction: the custom indicator is built from built-ins it declared as components.
        bands.Components.Should().HaveCount(2);
        bands.Components[0].Should().BeOfType<Sma>();
        bands.Components[1].Should().BeOfType<Atr>();
    }

    [Fact]
    public async Task CustomIndicator_DoesNotModifyTheBarsItWasHanded()
    {
        var bars = Bars_(SampleSize);
        var closes = bars.Select(b => b.Close).ToArray();

        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new RangeBands(20, 2))
            .BuildAsync();

        bars.Select(b => b.Close).Should().Equal(closes, "a run must not modify the bars it was given");
    }

    [Fact]
    public async Task CustomIndicator_BranchesFromEachOutputIndependently()
    {
        var bands = new RangeBands(20, 2);
        var fromUpper = new Rsi(14).Of(bands);

        using var run = await RunAsync(bands, fromUpper);

        // Each output is a distinct series, addressed by a typed member rather than a name.
        run[bands.UpperBand].ToArray().Should().NotEqual(run[bands.LowerBand].ToArray());
        run[fromUpper].ToArray().Should().HaveCount(SampleSize);
    }

    [Fact]
    public async Task CustomIndicator_ReceivesItsComponentsAlreadyComputed()
    {
        var bands = new RangeBands(20, 2);
        var basis = new Sma(20);

        using var run = await RunAsync(bands, basis);

        // The middle band is exactly the component average, which is only true if the component's value for
        // each bar is what the state was handed.
        run[bands.MiddleBand].ToArray().Should().Equal(run[basis].ToArray());
    }

    [Fact]
    public async Task CustomIndicator_IsIndistinguishableFromABuiltInToTheBuilder()
    {
        var mine = new RangeBands(20, 2);
        var theirs = new BollingerBands(20, 2);

        using var run = await RunAsync(mine, theirs);

        run[mine.UpperBand].ToArray().Should().HaveCount(SampleSize);
        run[theirs.Upper].ToArray().Should().HaveCount(SampleSize);
        run.BarCount.Should().Be(SampleSize);
    }
}
