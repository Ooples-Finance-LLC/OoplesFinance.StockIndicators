using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Asking an indicator for an output it does not publish is an error, not a different series.
/// </summary>
/// <remarks>
/// <para>
/// Resolving a slot used to fall through to the primary series when the indicator published no key for
/// it. That returns a number, and the number is wrong: the caller asked for the signal line and received
/// the indicator itself. Alligator, Gator, Aroon, Elder Ray and Trix each handed back the same series for
/// every band that way, which is why <c>GetOutputKey</c> now returns null rather than guessing at a
/// literal key, and why the resolver raises instead of substituting.
/// </para>
/// <para>
/// The subject is a single-output indicator on purpose. 484 of the indicators in the generated map publish
/// exactly one slot, so this is the common shape rather than a corner: every one of them would have
/// answered a request for a signal line with its own primary values.
/// </para>
/// </remarks>
public sealed class MissingOutputTests : GlobalTestData
{
    private const IndicatorName SingleOutput = IndicatorName.AlphaDecreasingExponentialMovingAverage;

    [Fact]
    public void AskingForASlotTheIndicatorDoesNotPublishRaises()
    {
        var act = () => Evaluate(IndicatorOutput.Signal);

        act.Should().Throw<CalculationException>(
                "an indicator asked for an output it does not publish must say so, not answer with another series")
            .WithMessage($"*{SingleOutput}*")
            .WithMessage("*Signal*");
    }

    /// <summary>The message names what the indicator does publish, so the caller can correct the request.</summary>
    [Fact]
    public void TheErrorNamesTheOutputsTheIndicatorDoesPublish()
    {
        var act = () => Evaluate(IndicatorOutput.Histogram);

        act.Should().Throw<CalculationException>().WithMessage("*Ema*");
    }

    /// <summary>
    /// The primary slot still resolves, because that is where a single-output indicator publishes its
    /// value. The fix narrows the fallback rather than removing it.
    /// </summary>
    [Fact]
    public void ThePrimarySlotStillResolves()
    {
        var series = Evaluate(IndicatorOutput.Primary);

        series.Should().NotBeEmpty("the primary slot is the one output this indicator publishes");
        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "the primary series must carry values, not only warm-up zeros");
    }

    /// <summary>The mean absolute deviation bands resolve every band they publish.</summary>
    /// <remarks>
    /// The same failure from the other side. <c>CalculateMeanAbsoluteDeviationBands</c> ended by stamping
    /// itself <c>IndicatorName.MeanAbsoluteErrorBands</c>, the different indicator defined sixty lines above
    /// it, and that stamp is what the output map is read from. So the map held no entry for the deviation
    /// bands at all and every one of them raised "Available outputs: none" - an indicator that computes
    /// perfectly through the catalog, which keys off the method name instead and so never noticed, while
    /// being unreachable through the Builder. Asking for all three bands is what pins the stamp.
    /// </remarks>
    [Theory]
    [InlineData(IndicatorOutput.UpperBand)]
    [InlineData(IndicatorOutput.MiddleBand)]
    [InlineData(IndicatorOutput.LowerBand)]
    public void TheMeanAbsoluteDeviationBandsResolveEveryBandTheyPublish(IndicatorOutput output)
    {
        var series = Evaluate(IndicatorName.MeanAbsoluteDeviationBands, new object[] { 20, 2.0 }, output);

        series.Should().NotBeEmpty("the deviation bands publish an upper, a middle and a lower band");
        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "a resolved band must carry values, not only warm-up zeros");
    }

    /// <summary>The trend trader bands resolve their own bands, not another indicator's keys.</summary>
    /// <remarks>
    /// The same stamp defect, except this one corrupted a second indicator as well.
    /// <c>CalculateTimeAndMoneyChannel</c> stamped itself <c>IndicatorName.TrendTraderBands</c>, and it
    /// publishes seven keys - Ch+1 through Ch-3, and Median - against the three the real trend trader bands
    /// publish. The generator keeps the richest set per indicator, so the victim's map entry became the
    /// impostor's seven: asking TrendTraderBands for its upper band resolved to the key "Ch-2", which its
    /// own output dictionary does not contain.
    /// </remarks>
    [Theory]
    [InlineData(IndicatorOutput.UpperBand)]
    [InlineData(IndicatorOutput.MiddleBand)]
    [InlineData(IndicatorOutput.LowerBand)]
    public void TheTrendTraderBandsResolveTheirOwnBands(IndicatorOutput output)
    {
        var series = Evaluate(IndicatorName.TrendTraderBands, Array.Empty<object>(), output);

        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "the band must carry the trend trader bands' own values");
    }

    /// <summary>An indicator that stamped another's name was unreachable through the Builder entirely.</summary>
    /// <remarks>
    /// Neither of these appears in the output map while its calculation stamps a different indicator, so a
    /// named slot raises "Available outputs: none" even though the calculation itself is correct.
    /// <para>
    /// The signal slot is the subject on purpose. Asking for Primary does not discriminate: the resolver
    /// still falls back to the primary series for that one slot, as <see cref="ThePrimarySlotStillResolves"/>
    /// records, so this test passed with the defect in place until it was pointed at a named slot.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(IndicatorName.TimeAndMoneyChannel)]
    [InlineData(IndicatorName.EhlersSimpleWindowIndicator)]
    public void AnIndicatorThatStampedAnothersNameIsReachable(IndicatorName indicator)
    {
        var series = Evaluate(indicator, Array.Empty<object>(), IndicatorOutput.Signal);

        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "the indicator publishes a signal series of its own");
    }

    /// <summary>
    /// Every key an indicator publishes is reachable by name, including the ones no slot can address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>IndicatorOutput</c> has six members and the generated map fills them positionally, so an indicator
    /// publishing more keys than that has the remainder permanently unaddressable. CamarillaPivotPoints
    /// publishes seventeen; eleven of them could not be named at all. See issue #201.
    /// </para>
    /// <para>
    /// All seventeen are asserted rather than a sample, because the defect was a ceiling: any subset that
    /// happened to fall inside the first six would pass while the feature was still absent.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("Pivot")]
    [InlineData("S1")]
    [InlineData("S2")]
    [InlineData("S3")]
    [InlineData("S4")]
    [InlineData("S5")]
    [InlineData("R1")]
    [InlineData("R2")]
    [InlineData("R3")]
    [InlineData("R4")]
    [InlineData("R5")]
    [InlineData("M1")]
    [InlineData("M2")]
    [InlineData("M3")]
    [InlineData("M4")]
    [InlineData("M5")]
    [InlineData("M6")]
    public void EveryPublishedKeyIsReachableByName(string outputKey)
    {
        var series = Evaluate(IndicatorName.CamarillaPivotPoints, Array.Empty<object>(), outputKey);

        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, $"CamarillaPivotPoints publishes '{outputKey}' and it must be reachable");
    }

    /// <summary>
    /// The key that falls off the end of the six slots resolves by name.
    /// </summary>
    /// <remarks>
    /// TimeAndMoneyChannel publishes seven keys, so Median - the seventh - was dropped from the map while
    /// <c>BuilderArmTargets</c> names it as that spec's output key and the state returns it as its primary
    /// value. The arm and the map disagreed about what this indicator's output is; naming the key settles it.
    /// </remarks>
    [Fact]
    public void TheKeyThatFallsOffTheSlotsResolvesByName()
    {
        var median = Evaluate(IndicatorName.TimeAndMoneyChannel, Array.Empty<object>(), "Median");

        median.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "Median is published and must be reachable even though no slot holds it");
    }

    /// <summary>
    /// Two keys of one indicator are two different series, not one series returned twice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What this shows is that the resolver reads each key out of the indicator's own published outputs: two
    /// keys give two genuinely different series rather than one series answered twice.
    /// </para>
    /// <para>
    /// It is deliberately NOT the guard against node collision, and cannot be: each call to
    /// <c>Evaluate</c> builds its own <c>StockIndicatorBuilder</c>, so the two specs live in separate node
    /// graphs and no node could be shared between them whatever the identity said. That guard is
    /// <c>CommonSubexpressionEliminationTests.DifferentNamedOutputsOfOneIndicatorAreDifferentNodes</c>, which
    /// asks one builder for both. Measured rather than assumed: with the output key removed from
    /// <c>IndicatorNodeKey.TryCreate</c>, that test fails with both handles resolving to Series:2 and this
    /// one still passes.
    /// </para>
    /// </remarks>
    [Fact]
    public void TwoKeysOfOneIndicatorAreDifferentSeries()
    {
        var s1 = Evaluate(IndicatorName.CamarillaPivotPoints, Array.Empty<object>(), "S1");
        var r5 = Evaluate(IndicatorName.CamarillaPivotPoints, Array.Empty<object>(), "R5");

        s1.Should().NotBeEmpty();
        r5.Should().NotBeEmpty();
        s1.Should().NotEqual(r5, "a support level and a resistance level are different series");
    }

    /// <summary>Asking for a key the indicator does not publish raises, and says what it does publish.</summary>
    [Fact]
    public void AskingForAKeyTheIndicatorDoesNotPublishRaises()
    {
        var act = () => Evaluate(IndicatorName.CamarillaPivotPoints, Array.Empty<object>(), "NotAKey");

        act.Should().Throw<CalculationException>(
                "a key the indicator does not publish must be refused, not answered with another series")
            .WithMessage("*NotAKey*")
            .WithMessage("*Pivot*");
    }

    private static double[] Evaluate(IndicatorOutput output) =>
        Evaluate(SingleOutput, new object[] { 14 }, output);

    private static double[] Evaluate(IndicatorName indicator, object[] parameters, IndicatorOutput output)
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog =>
        {
            var spec = IndicatorSpecs.Create(indicator, new GenericIndicatorOptions(parameters), output);
            var price = catalog.Price();
            handle = builder.AddIndicator(spec, price, builder.ResolveSeriesKey(price), key: null);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);

        return runtime.GetSeries(handle).ToArray();
    }

    /// <summary>The same path, asking for a published output by name rather than by slot.</summary>
    private static double[] Evaluate(IndicatorName indicator, object[] parameters, string outputKey)
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog =>
        {
            var spec = IndicatorSpecs.Create(indicator, new GenericIndicatorOptions(parameters), outputKey);
            var price = catalog.Price();
            handle = builder.AddIndicator(spec, price, builder.ResolveSeriesKey(price), key: null);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);

        return runtime.GetSeries(handle).ToArray();
    }
}
