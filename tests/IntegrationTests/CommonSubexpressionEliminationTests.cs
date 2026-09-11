using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using BarTimeframe = OoplesFinance.StockIndicators.Streaming.BarTimeframe;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Identical indicator computations share one node in the graph (#108).
/// </summary>
public sealed class CommonSubexpressionEliminationTests : GlobalTestData
{
    private const int SampleSize = 200;
    private const double Tolerance = 1e-12;

    private static StockIndicatorBuilder CreateBuilder() =>
        new(IndicatorDataSource.FromBatch(new StockData(StockTestData.Take(SampleSize))));

    [Fact]
    public void TheSameIndicatorOnTheSameInputIsComputedOnce()
    {
        var builder = CreateBuilder();
        SeriesHandle first = default;
        SeriesHandle second = default;

        builder.ConfigureIndicators(catalog =>
        {
            first = catalog.Sma(20);
            second = catalog.Sma(20);
        });

        second.Should().Be(first, "the same computation on the same input is one node, not two");
    }

    [Fact]
    public void DifferentParametersAreDifferentNodes()
    {
        var builder = CreateBuilder();
        SeriesHandle twenty = default;
        SeriesHandle fifty = default;

        builder.ConfigureIndicators(catalog =>
        {
            twenty = catalog.Sma(20);
            fifty = catalog.Sma(50);
        });

        fifty.Should().NotBe(twenty, "a different length is a different computation");
    }

    [Fact]
    public void DifferentIndicatorsAreDifferentNodes()
    {
        var builder = CreateBuilder();
        SeriesHandle sma = default;
        SeriesHandle rsi = default;

        builder.ConfigureIndicators(catalog =>
        {
            sma = catalog.Sma(14);
            rsi = catalog.Rsi(14);
        });

        rsi.Should().NotBe(sma);
    }

    [Fact]
    public void DifferentInputsAreDifferentNodes()
    {
        var builder = CreateBuilder();
        SeriesHandle onPrice = default;
        SeriesHandle onAnIndicator = default;

        builder.ConfigureIndicators(catalog =>
        {
            var rsi = catalog.Rsi(14);
            onPrice = catalog.Sma(10);
            onAnIndicator = catalog.Sma(10, rsi);
        });

        onAnIndicator.Should().NotBe(onPrice, "same indicator, different input series");
    }

    /// <summary>
    /// A multi-output indicator asks for one node per output, and those must stay distinct even though
    /// they share a name, an input and an options instance.
    /// </summary>
    [Fact]
    public void DifferentOutputsOfOneIndicatorAreDifferentNodes()
    {
        var builder = CreateBuilder();
        SeriesHandle lips = default;
        SeriesHandle teeth = default;
        SeriesHandle jaws = default;

        builder.ConfigureIndicators(catalog =>
        {
            var alligator = catalog.AlligatorIndex();
            lips = alligator.Lips;
            teeth = alligator.Teeth;
            jaws = alligator.Jaw;
        });

        lips.Should().NotBe(teeth);
        teeth.Should().NotBe(jaws);
        lips.Should().NotBe(jaws);
    }

    [Fact]
    public void SharingANodeDoesNotChangeTheValues()
    {
        var shared = Compute(useElimination: true);
        var separate = Compute(useElimination: false);

        shared.Should().HaveCount(separate.Count);
        for (var i = 0; i < separate.Count; i++)
        {
            shared[i].Should().BeApproximately(separate[i], Tolerance,
                $"eliminating a duplicate node must not move a single value, index {i}");
        }
    }

    [Fact]
    public void BothHandlesReadTheSameSeriesWhenShared()
    {
        var builder = CreateBuilder();
        SeriesHandle first = default;
        SeriesHandle second = default;

        builder.ConfigureIndicators(catalog =>
        {
            first = catalog.Sma(20);
            second = catalog.Sma(20);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(first);
        runtime.Subscribe(second);

        var a = runtime.GetSeries(first).ToList();
        var b = runtime.GetSeries(second).ToList();

        a.Should().NotBeEmpty();
        a.Should().Equal(b);
    }

    [Fact]
    public void EliminationCanBeTurnedOff()
    {
        var builder = CreateBuilder();
        builder.EnableCommonSubexpressionElimination = false;

        SeriesHandle first = default;
        SeriesHandle second = default;
        builder.ConfigureIndicators(catalog =>
        {
            first = catalog.Sma(20);
            second = catalog.Sma(20);
        });

        second.Should().NotBe(first, "with elimination off the graph is built exactly as before");
    }

    /// <summary>
    /// Two different parameter lists never produce the same key, even when the text inside them
    /// contains the characters the key uses as separators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one failure this type must not have. A missed match only costs time - the work
    /// happens twice and both answers are right. A false match hands one computation the other's
    /// numbers, silently, with no exception and no wrong-looking value to notice.
    /// </para>
    /// <para>
    /// Both cases below produced a byte-identical key before the length prefix went in, because a
    /// string was written as <c>"text";</c> and a string containing <c>";</c> could close its own
    /// quote and open the next one. Verified by reverting the fix: both fail, and the
    /// matches-itself test below still passes, so the prefix did not simply make every key unique.
    /// A third case, <c>[""]</c> against <c>["", ""]</c>, was dropped because it passed under the
    /// old scheme too and so proved nothing.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(new object[] { "a\";\"b" }, new object[] { "a", "b" })]
    [InlineData(new object[] { "x\";\"y\";\"z" }, new object[] { "x", "y", "z" })]
    public void TextInsideAKeyCannotForgeAFieldBoundary(object[] first, object[] second)
    {
        var input = new SeriesHandle(7);
        var seriesKey = new SeriesKey(SymbolId.From("AAPL"), BarTimeframe.Minutes(1));

        var firstKey = KeyFor(seriesKey, input, first);
        var secondKey = KeyFor(seriesKey, input, second);

        firstKey.Should().NotBeNull();
        secondKey.Should().NotBeNull();
        secondKey.Should().NotBe(firstKey,
            $"{first.Length} parameters and {second.Length} are different computations");
    }

    /// <summary>
    /// The same awkward parameter list still matches itself, so the length prefix did not simply
    /// make every key unique.
    /// </summary>
    [Fact]
    public void TheSameAwkwardParametersStillMatchThemselves()
    {
        var input = new SeriesHandle(7);
        var seriesKey = new SeriesKey(SymbolId.From("AAPL"), BarTimeframe.Minutes(1));
        var parameters = new object[] { "a\";\"b" };

        var firstKey = KeyFor(seriesKey, input, parameters);
        var secondKey = KeyFor(seriesKey, input, new object[] { "a\";\"b" });

        secondKey.Should().Be(firstKey, "the same computation is the same key whatever it is called");
        secondKey?.GetHashCode().Should().Be(firstKey?.GetHashCode());
    }

    private static IndicatorNodeKey? KeyFor(SeriesKey seriesKey, SeriesHandle input, object[] parameters) =>
        IndicatorNodeKey.TryCreate(seriesKey, input,
            new IndicatorSpec(IndicatorName.SimpleMovingAverage, new GenericIndicatorOptions(parameters),
                IndicatorOutput.Primary));

    private static List<double> Compute(bool useElimination)
    {
        var builder = CreateBuilder();
        builder.EnableCommonSubexpressionElimination = useElimination;

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Sma(20);
            catalog.Rsi(14);
            catalog.Sma(20);
            handle = catalog.Rsi(14);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);

        return runtime.GetSeries(handle).ToList();
    }
}
