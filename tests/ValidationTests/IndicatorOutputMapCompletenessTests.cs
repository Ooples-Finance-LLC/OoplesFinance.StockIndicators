using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// An indicator the library can compute must also be addressable through the Builder.
/// </summary>
/// <remarks>
/// <para>
/// The two engines learn an indicator's name from different places. <c>IndicatorInvoker</c> keys off the
/// method name, so it can call anything named <c>Calculate*</c>; the Builder resolves a slot through the
/// generated output map, which is read from the <c>stockData.IndicatorName</c> assignment inside the
/// calculation. When those two disagree the indicator computes perfectly and is unreachable at the same
/// time, and nothing fails - the Builder simply reports "Available outputs: none".
/// </para>
/// <para>
/// Both ways of disagreeing have now been found. Three calculations stamped another indicator's name
/// outright, one of which also overwrote its victim's entry with seven keys belonging to something else.
/// Four more handed their name and keys to a shared private helper, where the generator saw a parameter
/// rather than a literal. That is eleven output keys across seven indicators, none of which failed a test,
/// because every test drove the calculations directly. See issues #199 and PR #198.
/// </para>
/// <para>
/// This asserts the agreement itself rather than any particular indicator, so it covers the one added
/// tomorrow without anyone remembering to add it here - the same reason IndicatorInvariantTests takes its
/// set from GetSupportedIndicators rather than from a list of names.
/// </para>
/// </remarks>
public sealed class IndicatorOutputMapCompletenessTests
{
    [Fact]
    public void EveryComputableIndicatorPublishesAtLeastOneOutput()
    {
        var unreachable = IndicatorInvoker.GetSupportedIndicators()
            .Where(name => name != IndicatorName.None)
            .Where(name => GeneratedIndicatorOutputs.KeysFor(name).Count == 0)
            .Select(name => name.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unreachable.Should().BeEmpty(
            "an indicator with a Calculate method but no entry in the generated output map cannot be reached "
            + "through the Builder at any named slot, however correct its calculation is. Either its "
            + "IndicatorName assignment names a different indicator, or it hands the name to a helper the "
            + $"generator cannot follow. Unreachable: {string.Join(", ", unreachable)}");
    }

    /// <summary>
    /// The four that reach their name through a shared helper, pinned by the keys they publish.
    /// </summary>
    /// <remarks>
    /// The test above would pass if a helper-routed indicator were given any entry at all, including a wrong
    /// one. These are the keys their streaming states publish, so this also holds the two engines to the same
    /// names rather than merely to having some name.
    /// </remarks>
    [Theory]
    [InlineData(IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator, "Cvida1", "Cvida2")]
    [InlineData(IndicatorName.VolatilityIndexDynamicAverageIndicator, "Vida1", "Vida2")]
    [InlineData(IndicatorName.RelativeVolatilityIndexHigh, "RviHigh", "")]
    [InlineData(IndicatorName.RelativeVolatilityIndexLow, "RviLow", "")]
    public void AnIndicatorRoutedThroughAHelperPublishesItsOwnKeys(
        IndicatorName indicator, string first, string second)
    {
        var keys = GeneratedIndicatorOutputs.KeysFor(indicator);

        keys.Should().Contain(first, "the helper publishes this key for this caller");

        if (second.Length > 0)
        {
            keys.Should().Contain(second, "the helper publishes both of this caller's keys");
        }
    }
}
