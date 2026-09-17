using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;


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

    /// <summary>
    /// Every handle of every multi-output catalog result, held to the output it names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Alligator above was found by accident. These cover the rest of the surface, because the defects all
    /// sat in the same place: of the nineteen handles the catalog hands out, the ones a test drove were
    /// correct and the ones nothing drove were not. Stochastic's D, all four of Ichimoku's non-primary
    /// spans and Keltner's parameters had no test between them.
    /// </para>
    /// <para>
    /// Each handle is compared against the indicator's own named output rather than merely asserted to
    /// produce values. "Produces values" is true of any series, including another handle's - which is the
    /// defect this file was created for and the one it caught again here.
    /// </para>
    /// </remarks>
    private static List<double[]> Evaluate(Func<IndicatorCatalog, SeriesHandle[]> configure)
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        var handles = Array.Empty<SeriesHandle>();
        builder.ConfigureIndicators(catalog => handles = configure(catalog));

        using var runtime = builder.Build();
        runtime.Start();

        var series = new List<double[]>(handles.Length);
        foreach (var handle in handles)
        {
            runtime.Subscribe(handle);
            series.Add(runtime.GetSeries(handle).ToArray());
        }

        return series;
    }

    /// <summary>Holds each handle to the batch output it claims to be, and to being its own series.</summary>
    private static void AssertHandlesAre(List<double[]> actual, StockData batch, params string[] keys)
    {
        actual.Should().HaveCount(keys.Length, "one series per handle");

        for (var i = 0; i < keys.Length; i++)
        {
            var expected = batch.OutputValues[keys[i]];
            var overlap = Math.Min(actual[i].Length, expected.Count);

            overlap.Should().BeGreaterThan(0, $"the {keys[i]} handle must produce values");
            actual[i].Take(overlap).Should().Equal(expected.Take(overlap),
                $"the catalog handle for {keys[i]} must be that output, not another of them");
        }

        // Two handles returning one series is the failure mode, so it is asserted directly rather than
        // left to follow from the comparisons above.
        for (var i = 0; i < actual.Count; i++)
        {
            for (var j = i + 1; j < actual.Count; j++)
            {
                actual[i].Should().NotEqual(actual[j],
                    $"{keys[i]} and {keys[j]} are different outputs and must be different series");
            }
        }
    }

    [Fact]
    public void Macd_EveryHandleIsTheOutputItNames()
    {
        var actual = Evaluate(c => { var r = c.Macd(12, 26, 9); return new[] { r.Primary, r.Signal, r.Histogram }; });
        var batch = new StockData(StockTestData.Take(200))
            .CalculateMovingAverageConvergenceDivergence(MovingAvgType.ExponentialMovingAverage, 12, 26, 9);

        AssertHandlesAre(actual, batch, "Macd", "Signal", "Histogram");
    }

    [Fact]
    public void BollingerBands_EveryHandleIsTheOutputItNames()
    {
        var actual = Evaluate(c => { var r = c.BollingerBands(20, 2); return new[] { r.Upper, r.Middle, r.Lower }; });
        var batch = new StockData(StockTestData.Take(200))
            .CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, 20, 2);

        AssertHandlesAre(actual, batch, "UpperBand", "MiddleBand", "LowerBand");
    }

    /// <summary>
    /// The D line is the discriminating one: the registry pinned it to "SignalFastK", which this
    /// indicator does not publish - it publishes FastK, FastD and SlowD.
    /// </summary>
    [Fact]
    public void Stochastic_EveryHandleIsTheOutputItNames()
    {
        var actual = Evaluate(c => { var r = c.Stochastic(14, 3); return new[] { r.K, r.D }; });
        var batch = new StockData(StockTestData.Take(200))
            .CalculateStochasticOscillator(MovingAvgType.SimpleMovingAverage, 14, 3, 3);

        AssertHandlesAre(actual, batch, "FastK", "FastD");
    }

    [Fact]
    public void DonchianChannels_EveryHandleIsTheOutputItNames()
    {
        var actual = Evaluate(c => { var r = c.DonchianChannels(20); return new[] { r.Upper, r.Middle, r.Lower }; });
        var batch = new StockData(StockTestData.Take(200)).CalculateDonchianChannels(20);

        AssertHandlesAre(actual, batch, "UpperChannel", "MiddleChannel", "LowerChannel");
    }

    /// <summary>
    /// Keltner's second catalog argument is a band multiplier, and the generated factory reads the second
    /// generic parameter as the ATR length.
    /// </summary>
    /// <remarks>
    /// The batch side is given the parameters the catalog means - a 20-bar basis, a 10-bar ATR and a
    /// multiplier of 2 - rather than the ones it currently produces, so a multiplier that lands on the
    /// wrong parameter shows up as a value mismatch. The existing ordering test cannot see this: upper
    /// stays above middle stays above lower whatever the ATR length is.
    /// </remarks>
    [Fact]
    public void KeltnerChannels_EveryHandleIsTheOutputItNames()
    {
        var actual = Evaluate(c => { var r = c.KeltnerChannels(20, 2); return new[] { r.Upper, r.Middle, r.Lower }; });
        var batch = new StockData(StockTestData.Take(200)).CalculateKeltnerChannels(
            MovingAvgType.ExponentialMovingAverage, 20, 10, 2, MovingAvgType.WildersSmoothingMethod);

        AssertHandlesAre(actual, batch, "UpperBand", "MiddleBand", "LowerBand");
    }

    /// <summary>
    /// Ichimoku's four published spans, each held to its own output.
    /// </summary>
    /// <remarks>
    /// The registry has no Histogram entry for this indicator, so the fifth handle falls through to the
    /// generated map, which puts SenkouSpanA on Histogram - two handles, one series. The distinctness
    /// assertion is what shows that; comparing each against its named output alone would not, since four
    /// of the five would still match.
    /// </remarks>
    [Fact]
    public void Ichimoku_TheFourPublishedSpansAreTheOutputsTheyName()
    {
        var actual = Evaluate(c =>
        {
            var r = c.Ichimoku(9, 26, 52);
            return new[] { r.TenkanSen, r.KijunSen, r.SenkouSpanA, r.SenkouSpanB };
        });
        var batch = new StockData(StockTestData.Take(200)).CalculateIchimokuCloud(9, 26, 52);

        AssertHandlesAre(actual, batch, "TenkanSen", "KijunSen", "SenkouSpanA", "SenkouSpanB");
    }

    /// <summary>
    /// The Chikou span is the close itself, and CalculateIchimokuCloud does not publish it at all - the
    /// library computes it as a separate indicator.
    /// </summary>
    [Fact]
    public void Ichimoku_ChikouSpanIsTheChikouSpan()
    {
        var actual = Evaluate(c =>
        {
            var r = c.Ichimoku(9, 26, 52);
            return new[] { r.ChikouSpan, r.SenkouSpanA };
        });
        var batch = new StockData(StockTestData.Take(200)).CalculateIchimokuChikouSpan();
        var expected = batch.OutputValues["ChikouSpan"];
        var overlap = Math.Min(actual[0].Length, expected.Count);

        overlap.Should().BeGreaterThan(0, "the Chikou span handle must produce values");
        actual[0].Take(overlap).Should().Equal(expected.Take(overlap),
            "the ChikouSpan handle must be the Chikou span, which IchimokuCloud does not publish");
        actual[0].Should().NotEqual(actual[1],
            "the Chikou span and Senkou Span A are different series, not one series behind two handles");
    }
}
