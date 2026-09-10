using OoplesFinance.StockIndicators.Builder;

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
