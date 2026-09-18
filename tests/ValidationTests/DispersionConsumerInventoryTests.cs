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
/// code's mistakes. The two count assertions hold the emitted constants to the emitted rows.
/// </para>
/// <para>
/// The inventory is now empty, because #190's conversion is complete: no calculation takes its dispersion
/// from <c>CalculateStandardDeviationVolatility</c> any more. So the assertion this file used to make - that
/// the two helper-routed indicators appear in it - is false by design rather than by defect, and has been
/// replaced by the emptiness assertion below. The generator no longer returns early on an empty set for the
/// same reason: an inventory that exists and is empty is a regression guard, and one that is absent is
/// silence of exactly the kind #222 was about.
/// </para>
/// </remarks>
public sealed class DispersionConsumerInventoryTests
{
    /// <summary>
    /// Nothing takes its dispersion from the wrong quantity any more, and this is what keeps it that way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces an assertion that <c>ChandeVolatilityIndexDynamicAverageIndicator</c> and
    /// <c>VolatilityIndexDynamicAverageIndicator</c> appear in the inventory. They were the helper-routed
    /// shape #222 found missing, and holding them there was right while the migration was in progress; both
    /// are converted now, so requiring their presence would require the defect to persist.
    /// </para>
    /// <para>
    /// The guard it leaves behind is stronger than the one it replaces. A new consumer of
    /// <c>CalculateStandardDeviationVolatility</c> - or an old one reintroduced - puts a row back into the
    /// inventory and fails this, whichever indicator it belongs to and whether it names its own dispersion or
    /// reaches one through a shared helper.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoCalculationStillTakesTheOldDispersionQuantity()
    {
        GeneratedDispersionConsumers.All.Should().BeEmpty(
            "every consumer has been moved to the deviation of its own window (#190), so a row here is a "
            + "calculation that has gone back to the mean squared residual from a moving-average line: "
            + string.Join(", ", GeneratedDispersionConsumers.All.Select(u => u.Indicator.ToString())));
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
