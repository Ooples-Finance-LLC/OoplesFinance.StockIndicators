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
            jaw = result.Jaws;
            teeth = result.Teeth;
            lips = result.Lips;
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

        var batch = new StockData(StockTestData.Take(200)).CalculateAlligatorIndex();

        foreach (var (name, handle) in new[] { ("Jaws", jaw), ("Teeth", teeth), ("Lips", lips) })
        {
            runtime.Subscribe(handle);
            var series = runtime.GetSeries(handle).ToList();

            series.Count.Should().BeGreaterThan(0, $"{name} should produce output values");
            series.Count(v => !double.IsNaN(v) && v != 0)
                .Should().BeGreaterThan(0, $"{name} should have values that are not all zero or NaN");

            // Against the NAMED batch output, not merely against zero. "Produces values" is true of
            // any three series, including three copies of one - which is the defect this file exists
            // to catch, since five of six multi-output catalog results once returned byte-identical
            // series. Pinning each handle to the output it claims to be is what makes that visible.
            var expected = batch.OutputValues[name];
            var overlap = Math.Min(series.Count, expected.Count);
            series.Take(overlap).Should().Equal(expected.Take(overlap),
                $"the catalog handle for {name} must be that output, not another of them");
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

        jawSeries.Should().NotEqual(teethSeries, "the jaws and the teeth are different series");
        teethSeries.Should().NotEqual(lipsSeries, "the teeth and the lips are different series");

        // The third pair, which was missing. Jaws and Lips are the two the helper bound the wrong
        // way round, so this is exactly the comparison a Jaws/Lips swap or duplication shows up in -
        // and the only one of the three that was absent.
        jawSeries.Should().NotEqual(lipsSeries, "the jaws and the lips are different series");
    }
}
