using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// Asking an indicator for an output it does not publish is an error, not a different series.
/// </summary>
/// <remarks>
/// <para>
/// Resolution used to fall through to the primary series when the indicator published nothing under the
/// name asked for. That returns a number, and the number is wrong: the caller asked for the signal line and
/// received the indicator itself. Alligator, Gator, Aroon, Elder Ray and Trix each handed back the same
/// series for every band that way, which is why resolution raises instead of substituting.
/// </para>
/// <para>
/// The subject is a single-output indicator on purpose. Most of the indicators in the generated map publish
/// exactly one key, so this is the common shape rather than a corner: every one of them would have answered
/// a request for a signal line with its own values.
/// </para>
/// <para>
/// These used to ask through <c>IndicatorOutput</c>, a six-member enum the generated map filled positionally.
/// Every request here now names the published key directly. That is not a translation of the same question:
/// a slot was answered by whichever key happened to land in that position, so a test could name a band and be
/// handed something that was not one. See issue #219.
/// </para>
/// </remarks>
public sealed class MissingOutputTests : GlobalTestData
{
    private const IndicatorName SingleOutput = IndicatorName.AlphaDecreasingExponentialMovingAverage;

    /// <summary>
    /// This indicator publishes exactly one key, <c>Ema</c>, so <c>Signal</c> names nothing it produces.
    /// </summary>
    [Fact]
    public void AskingForAnOutputThisIndicatorDoesNotPublishRaises()
    {
        var act = () => EvaluateSingleOutput("Signal");

        act.Should().Throw<CalculationException>(
                "an indicator asked for an output it does not publish must say so, not answer with another series")
            .WithMessage($"*{SingleOutput}*")
            .WithMessage("*Signal*");
    }

    /// <summary>The message names what the indicator does publish, so the caller can correct the request.</summary>
    [Fact]
    public void TheErrorNamesTheOutputsTheIndicatorDoesPublish()
    {
        var act = () => EvaluateSingleOutput("Histogram");

        act.Should().Throw<CalculationException>().WithMessage("*Ema*");
    }

    /// <summary>
    /// A spec naming no key at all resolves to the indicator's own series.
    /// </summary>
    /// <remarks>
    /// This is what the primary slot used to mean, and it is the one part of the slot enum that carried real
    /// behaviour rather than a position: a caller who names no output wants the indicator itself. Naming no
    /// key now says that directly, so the meaning survived the enum. See issue #219.
    /// </remarks>
    [Fact]
    public void TheIndicatorsOwnSeriesResolvesWithoutAKey()
    {
        var series = Evaluate(SingleOutput, new object[] { 14 });

        series.Should().NotBeEmpty("a spec naming no key resolves to the indicator's own series");
        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "that series must carry values, not only warm-up zeros");
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
    [InlineData("UpperBand")]
    [InlineData("MiddleBand")]
    [InlineData("LowerBand")]
    public void TheMeanAbsoluteDeviationBandsResolveEveryBandTheyPublish(string outputKey)
    {
        var series = Evaluate(IndicatorName.MeanAbsoluteDeviationBands, new object[] { 20, 2.0 }, outputKey);

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
    [InlineData("UpperBand")]
    [InlineData("MiddleBand")]
    [InlineData("LowerBand")]
    public void TheTrendTraderBandsResolveTheirOwnBands(string outputKey)
    {
        var series = Evaluate(IndicatorName.TrendTraderBands, Array.Empty<object>(), outputKey);

        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, "the band must carry the trend trader bands' own values");
    }

    /// <summary>An indicator that stamped another's name was unreachable through the Builder entirely.</summary>
    /// <remarks>
    /// <para>
    /// Neither of these appears in the output map while its calculation stamps a different indicator, so a
    /// named output raises "Available outputs: none" even though the calculation itself is correct.
    /// </para>
    /// <para>
    /// A named output is the subject on purpose. Naming no key does not discriminate: resolution answers with
    /// the indicator's own series in that case, as <see cref="TheIndicatorsOwnSeriesResolvesWithoutAKey"/>
    /// records, so this test passed with the defect in place until it was pointed at a named output.
    /// </para>
    /// <para>
    /// The key is now given per indicator rather than taken from a slot, and that changed what is being
    /// asserted. This used to ask both indicators for <c>IndicatorOutput.Signal</c> and claim each "publishes
    /// a signal series of its own" - but neither does. The slot was answered by whichever key sat second in
    /// the generated map, which is <c>Ch-1</c> here and <c>Roc</c> there: an inner lower channel line and a
    /// rate of change, neither of them a signal line. The assertion held while its own description of the
    /// indicators was false. See issue #219.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(IndicatorName.TimeAndMoneyChannel, "Ch-1")]
    [InlineData(IndicatorName.EhlersSimpleWindowIndicator, "Roc")]
    public void AnIndicatorThatStampedAnothersNameIsReachable(IndicatorName indicator, string outputKey)
    {
        var series = Evaluate(indicator, Array.Empty<object>(), outputKey);

        series.Count(v => !double.IsNaN(v) && v != 0)
            .Should().BeGreaterThan(0, $"{indicator} publishes '{outputKey}' and it must be reachable by name");
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

    private static double[] EvaluateSingleOutput(string outputKey) =>
        Evaluate(SingleOutput, new object[] { 14 }, outputKey);

    /// <summary>The whole Builder path, for a spec that names no output of its own.</summary>
    private static double[] Evaluate(IndicatorName indicator, object[] parameters) =>
        Evaluate(indicator, parameters, outputKey: null);

    /// <summary>The same path, asking for a published output by name.</summary>
    private static double[] Evaluate(IndicatorName indicator, object[] parameters, string? outputKey)
    {
        var stockData = new StockData(StockTestData.Take(200));
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(stockData));

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog =>
        {
            var options = new GenericIndicatorOptions(parameters);
            var spec = outputKey is null
                ? IndicatorSpecs.Create(indicator, options)
                : IndicatorSpecs.Create(indicator, options, outputKey);
            var price = catalog.Price();
            handle = builder.AddIndicator(spec, price, builder.ResolveSeriesKey(price), key: null);
        });

        using var runtime = builder.Build();
        runtime.Start();
        runtime.Subscribe(handle);

        return runtime.GetSeries(handle).ToArray();
    }
}
