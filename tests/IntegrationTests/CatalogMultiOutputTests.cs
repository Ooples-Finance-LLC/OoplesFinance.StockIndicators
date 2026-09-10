using OoplesFinance.StockIndicators.Builder;


namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// The v2 catalog's multi-output result types, checked against what the indicators actually publish.
/// </summary>
/// <remarks>
/// The catalog generator assigns a multi-output indicator's outputs positionally to
/// <c>IndicatorOutput.Primary</c>, <c>Signal</c> and <c>Histogram</c>. <c>SeriesEvaluator.GetOutputKey</c>
/// then maps <c>Signal</c> to the literal key "Signal" and <c>Histogram</c> to "Histogram" unless an
/// override is registered for that indicator. Alligator publishes "Lips", "Teeth" and "Jaws", and has no
/// overrides, so the keys the catalog asks for are not keys it publishes.
/// </remarks>
public sealed class CatalogMultiOutputTests : GlobalTestData
{
    private static (IndicatorRuntime Runtime, SeriesHandle Jaw, SeriesHandle Teeth, SeriesHandle Lips) BuildAlligator()
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        SeriesHandle jaw = default;
        SeriesHandle teeth = default;
        SeriesHandle lips = default;

        builder.ConfigureIndicators(catalog =>
        {
            var result = catalog.AlligatorIndex();
            jaw = result.Lips;
            teeth = result.Teeth;
            lips = result.Jaws;
        });

        var runtime = builder.Build();
        runtime.Start();

        return (runtime, jaw, teeth, lips);
    }

    [Fact]
    public void AlligatorIndex_PublishesJawsTeethAndLips()
    {
        var alligator = new StockData(StockTestData.Take(200)).CalculateAlligatorIndex();

        alligator.OutputValues.Should().ContainKeys("Jaws", "Teeth", "Lips");
        alligator.OutputValues.Should().NotContainKey("Signal");
        alligator.OutputValues.Should().NotContainKey("Histogram");
    }

    [Fact]
    public void AlligatorIndex_EveryCatalogHandleProducesValues()
    {
        var (runtime, jaw, teeth, lips) = BuildAlligator();

        foreach (var (name, handle) in new[] { ("Lips", jaw), ("Teeth", teeth), ("Lips", lips) })
        {
            runtime.Subscribe(handle);
            var series = runtime.GetSeries(handle);

            series.Count.Should().BeGreaterThan(0, $"{name} should produce output values");
            series.Count(v => !double.IsNaN(v) && v != 0)
                .Should().BeGreaterThan(0, $"{name} should have values that are not all zero or NaN");
        }
    }

    [Fact]
    public void AlligatorIndex_TheThreeCatalogHandlesAreDifferentSeries()
    {
        var (runtime, jaw, teeth, lips) = BuildAlligator();

        runtime.Subscribe(jaw);
        runtime.Subscribe(teeth);
        runtime.Subscribe(lips);

        var jawSeries = runtime.GetSeries(jaw).ToList();
        var teethSeries = runtime.GetSeries(teeth).ToList();
        var lipsSeries = runtime.GetSeries(lips).ToList();

        jawSeries.Should().NotEqual(teethSeries, "the lips and the teeth are different series");
        teethSeries.Should().NotEqual(lipsSeries, "the teeth and the jaws are different series");
    }
}
