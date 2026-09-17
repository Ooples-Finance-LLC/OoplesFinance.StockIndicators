using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// The generated inventory of dispersion consumers has to see every one of them, including the indicators
/// that reach the call through a shared helper.
/// </summary>
/// <remarks>
/// <para>
/// <c>GeneratedDispersionConsumers</c> is read from the calculations so that it cannot drift from them, and
/// issue #190's decision about which consumers should change quantity rests on it. An inventory that
/// silently omits a consumer would leave that indicator out of the migration, and nothing would say so.
/// </para>
/// <para>
/// Nothing guarded it at all until now. The generator missed the helper-routed shape - two wrappers over
/// <c>CalculateVolatilityIndexDynamicAverage</c> hand it their name as an argument, so the helper's body
/// names a parameter and neither wrapper's body names anything - and both indicators were absent from the
/// inventory while every test passed. A reader that has stopped seeing something looks exactly like one
/// with nothing to see, which is the same reason <c>StreamingStateAnalyzer</c> needed tests in #189. See
/// issue #222.
/// </para>
/// <para>
/// What is deliberately not asserted here is "every consumer is listed", because deriving the expected set
/// means reimplementing the generator, and a reference built from the code under test agrees with that
/// code's mistakes. The two named indicators are the shape that was missed; the two count assertions hold
/// the emitted constants to the emitted rows.
/// </para>
/// </remarks>
public sealed class DispersionConsumerInventoryTests
{
    /// <summary>
    /// The indicators whose dispersion call lives in a shared helper rather than in their own body.
    /// </summary>
    /// <remarks>
    /// One call site inside <c>CalculateVolatilityIndexDynamicAverage</c> serves both, so it is recorded once
    /// for each. Both were missing entirely before #222 - not merely attributed to the wrong indicator - so
    /// this is the assertion the old generator fails.
    /// </remarks>
    [Theory]
    [InlineData(IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator)]
    [InlineData(IndicatorName.VolatilityIndexDynamicAverageIndicator)]
    public void AnIndicatorRoutedThroughAHelperIsInTheInventory(IndicatorName indicator)
    {
        GeneratedDispersionConsumers.Indicators.Should().Contain(indicator,
            $"{indicator} takes its dispersion from a shared helper, and a consumer the inventory cannot see "
            + "is a consumer #190's migration would silently leave behind");
    }

    /// <summary>The count the inventory publishes is the number of rows it actually holds.</summary>
    /// <remarks>
    /// A count written beside the data rather than derived from it goes stale the moment an indicator is
    /// added. That is not hypothetical here: the generator's own remarks claimed 41 call sites and 25 chained
    /// while it was emitting 32 and 19. The remarks are prose and no test can hold them, but these two are
    /// emitted constants and can be held to the rows they describe.
    /// </remarks>
    [Fact]
    public void TheEmittedCountMatchesTheEmittedSites()
    {
        GeneratedDispersionConsumers.Count.Should().Be(GeneratedDispersionConsumers.All.Count,
            "the published count is the number of call sites read from the source, not a number kept by hand");
    }

    /// <summary>The chained count is the number of rows that actually name a chained series.</summary>
    /// <remarks>
    /// The chained series is what decides whether a consumer's replacement is the deviation of the price
    /// input or of some other list - a true range, a log return, an on balance volume - so a wrong count here
    /// misdescribes the very thing the inventory exists to record.
    /// </remarks>
    [Fact]
    public void TheEmittedChainedCountMatchesTheSitesThatChain()
    {
        var chained = GeneratedDispersionConsumers.All.Count(use => use.ChainedSeries.Length > 0);

        GeneratedDispersionConsumers.ChainedCount.Should().Be(chained,
            "a site asking for the dispersion of a chained series is one whose ChainedSeries is not empty");
    }
}
