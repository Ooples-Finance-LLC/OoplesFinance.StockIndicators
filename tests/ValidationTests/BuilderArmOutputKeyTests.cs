using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A Builder slot an indicator does not publish is refused, rather than answered with its primary series.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BuilderArmBinding"/> used to end by handing back the primary series whenever it could not resolve
/// the slot it was asked for. That is the defect #186 removed from <c>SeriesEvaluator</c>, which raises instead
/// and says why in its own words: a slot the indicator does not produce "would be a wrong number rather than an
/// error". The substitution survived in the other resolver, defended by a comment claiming the registry invents
/// a key for any indicator - which stopped being true when <c>IndicatorOutputRegistry</c> was changed to stop
/// guessing. The fallback outlived its own justification.
/// </para>
/// <para>
/// It is reachable from the public Builder, not from tests alone: <c>IndicatorCompute.TryComputeFast</c> sends
/// every typed spec whose (options, output) pair is not among the verified arms straight to
/// <see cref="BuilderArmBinding"/>. Over 845 arm targets and six slots less the 213 verified arms, 4857
/// combinations reach it, and 3594 of those name a slot the indicator publishes no key for.
/// </para>
/// <para>
/// Those 3594 are not defects in the tables - it is perfectly correct that an absolute price oscillator has no
/// upper band. What was wrong was the answer given when one was asked for. So this asserts the behaviour rather
/// than the tables: the slot is refused. A sample is taken because each call runs the batch indicator, and
/// running several thousand of them would buy nothing over a spread of them.
/// </para>
/// </remarks>
public sealed class BuilderArmOutputKeyTests : GlobalTestData
{
    /// <summary>Enough indicators to span the table, few enough that each can run its calculation.</summary>
    private const int Sampled = 20;

    [Fact]
    public void ASlotTheIndicatorPublishesNoKeyForIsRefused()
    {
        var bars = StockTestData.Take(120).ToList();
        var answered = new List<string>();
        var refused = 0;

        foreach (var (optionsType, target, output) in UnpublishedSlots().Take(Sampled))
        {
            var options = BuilderArmTests.Create(optionsType, alternate: false);
            if (options is null)
            {
                continue;
            }

            var spec = new IndicatorSpec(target.Name, options, output);

            try
            {
                var values = BuilderArmBinding.Compute(new StockData(bars), spec, target);
                answered.Add($"{optionsType.Name}.{output} answered with {values.Count} values from {target.Name}, "
                    + "which publishes no key for that slot");
            }
            catch (CalculationException)
            {
                refused++;
            }
        }

        using var scope = new AssertionScope();
        refused.Should().BeGreaterThan(0, "the sample must actually exercise the refusal, or it proves nothing");
        answered.Should().BeEmpty(
            $"a slot with no key must be refused rather than answered with the primary series: {string.Join(" | ", answered)}");
    }

    /// <summary>
    /// The refusal names what the indicator does publish, so a caller can correct the request.
    /// </summary>
    /// <remarks>
    /// The same wording <c>SeriesEvaluator</c> already uses, so a caller cannot tell which resolver refused and
    /// does not get two different accounts of the same mistake.
    /// </remarks>
    [Fact]
    public void TheRefusalNamesTheOutputsTheIndicatorDoesPublish()
    {
        var bars = StockTestData.Take(120).ToList();
        var (optionsType, target, output) = UnpublishedSlots().First();
        var options = BuilderArmTests.Create(optionsType, alternate: false);
        options.Should().NotBeNull("the first sampled options type must be constructible");

        var spec = new IndicatorSpec(target.Name, options ?? throw new InvalidOperationException("no options"), output);
        var act = () => BuilderArmBinding.Compute(new StockData(bars), spec, target);

        act.Should().Throw<CalculationException>()
            .WithMessage($"*{target.Name}*{output}*Available outputs:*",
                "the message says which indicator, which slot, and what it does publish");
    }

    /// <summary>
    /// A key that resolves, but names a series the indicator does not publish, is refused as well.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There are two ways to reach the refusal and the tests above cover only one. That one is a slot with no
    /// key at all. This is the other: a key that resolves to something, where the indicator publishes nothing
    /// under that name. It is how <c>"SignalFastK"</c> passed for the stochastic's D line, and since #226
    /// corrected that pin there is no longer a live instance of it anywhere - so nothing exercises this branch
    /// by accident, and a regression in it would be silent.
    /// </para>
    /// <para>
    /// Neither existing test reaches it. <see cref="UnpublishedSlots"/> yields only slots whose key resolves to
    /// null, and <c>BuilderArmTests</c> compares <c>TryComputeFast</c> against this same method, so a change
    /// here moves both sides of that comparison together and it keeps agreeing with itself. Raised by review
    /// on PR #229.
    /// </para>
    /// <para>
    /// The bad key is handed in through the target rather than registered, on purpose.
    /// <c>IndicatorOutputRegistry</c> is process-wide, and this assembly declares no collection behaviour, so
    /// xunit runs these classes in parallel: a test that mutated the registry could answer a different test's
    /// question while it ran, and restoring it in a finally would not close that window. The target is already
    /// a parameter of the method under test, so nothing global moves.
    /// </para>
    /// <para>
    /// The published key is checked first. Asserting only that an unpublished key raises would pass just as
    /// well if the call raised for some unrelated reason, so the same call is made with a key the indicator
    /// does publish and required to answer. That is what attributes the refusal to the key.
    /// </para>
    /// </remarks>
    [Fact]
    public void AKeyThatResolvesButIsNotPublishedIsRefused()
    {
        var bars = StockTestData.Take(120).ToList();

        // Taken from the table the Builder itself reads, rather than naming the indicator here twice.
        BuilderArmBinding.TryGetTarget(typeof(MacdSpecOptions), out var bound)
            .Should().BeTrue("the MACD options type stands for a batch indicator");

        var spec = new IndicatorSpec(bound.Name, new MacdSpecOptions(12, 26, 9), IndicatorOutput.Primary);
        var published = GeneratedIndicatorOutputs.KeysFor(bound.Name);
        published.Should().NotBeEmpty("the indicator must publish something for this to discriminate");

        var answered = BuilderArmBinding.Compute(new StockData(bars),
            spec, new BuilderArmTarget(bound.Name, published[0]));
        answered.Should().NotBeEmpty($"{published[0]} is published, so asking for it answers with that series");

        var act = () => BuilderArmBinding.Compute(new StockData(bars),
            spec, new BuilderArmTarget(bound.Name, "Missing"));

        act.Should().Throw<CalculationException>()
            .WithMessage($"*{bound.Name}*Available outputs:*",
                "a key naming nothing the indicator publishes must raise; answering with the series it does "
                + "publish is exactly how a wrong pin passed for a real one");
    }

    /// <summary>Every reachable slot whose key resolves to nothing, in a stable order.</summary>
    private static IEnumerable<(Type OptionsType, BuilderArmTarget Target, IndicatorOutput Output)> UnpublishedSlots()
    {
        foreach (var (optionsType, target) in BuilderArmTargets.Targets.OrderBy(t => t.Key.Name, StringComparer.Ordinal))
        {
            foreach (IndicatorOutput output in Enum.GetValues(typeof(IndicatorOutput)))
            {
                // Primary legitimately has no key: that is where a single-output indicator publishes.
                if (output == IndicatorOutput.Primary
                    || BuilderVerifiedArms.Arms.Contains((optionsType, output))
                    || IndicatorOutputRegistry.GetOutputKey(target.Name, output) is not null)
                {
                    continue;
                }

                yield return (optionsType, target, output);
            }
        }
    }
}
