using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// A Builder slot served by a typed spec's batch indicator answers with the series it was asked for, or fails -
/// never with the primary series wearing another slot's name.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BuilderArmBinding"/> ends by answering an unresolved slot with the indicator's primary series:
/// </para>
/// <code>
/// if (key is null) { return result.CustomValuesList; }
/// return result.ChainedOutputs.TryGetValue(key, out var series) ? series : result.ChainedValues;
/// </code>
/// <para>
/// That is the defect #186 removed from <c>SeriesEvaluator</c>, which now raises instead, saying so in its own
/// words: answering a slot the indicator does not produce "would be a wrong number rather than an error". The
/// same substitution survived here, in the other resolver.
/// </para>
/// <para>
/// It is reachable from the public Builder rather than from tests alone. <c>IndicatorCompute.TryComputeFast</c>
/// routes every typed spec whose (options, output) pair is not among the verified arms straight to
/// <see cref="BuilderArmBinding"/>, so of the arm table's slots only the verified ones compute themselves and
/// all the rest land on this fallback.
/// </para>
/// <para>
/// The comment defending the fallback - that the registry "answers UpperBand, MiddleBand, LowerBand, Signal and
/// Histogram for any indicator, whether or not it publishes one" - describes behaviour that no longer exists.
/// <c>IndicatorOutputRegistry.GetOutputKey</c> was changed to stop guessing, and says so: "a slot either has a
/// key the indicator genuinely publishes or it has none". The fallback outlived its own justification.
/// </para>
/// <para>
/// This resolves each slot exactly as <c>BuilderArmBinding.Compute</c> does and holds the answer to a key the
/// indicator publishes. It reads the tables rather than running indicators, so it is a census rather than a
/// sample: every reachable combination is judged, not the handful a fixture happens to exercise.
/// </para>
/// </remarks>
public sealed class BuilderArmOutputKeyTests
{
    [Fact]
    public void NoArmSlotAnswersWithASeriesItWasNotAskedFor()
    {
        var answeredWithPrimary = new List<string>();
        var unpublishedKey = new List<string>();
        var reachable = 0;

        foreach (var (optionsType, target) in BuilderArmTargets.Targets)
        {
            var published = GeneratedIndicatorOutputs.KeysFor(target.Name);

            foreach (IndicatorOutput output in Enum.GetValues(typeof(IndicatorOutput)))
            {
                // A verified arm computes itself through ComputeArm; only the rest reach BuilderArmBinding.
                if (BuilderVerifiedArms.Arms.Contains((optionsType, output)))
                {
                    continue;
                }

                reachable++;

                var key = output == IndicatorOutput.Primary
                    ? target.OutputKey
                    : IndicatorOutputRegistry.GetOutputKey(target.Name, output);

                if (key is null)
                {
                    // Primary legitimately has no key: that is where a single-output indicator publishes.
                    if (output != IndicatorOutput.Primary)
                    {
                        answeredWithPrimary.Add($"{optionsType.Name}.{output} -> {target.Name} publishes no key for that slot");
                    }

                    continue;
                }

                if (!published.Contains(key))
                {
                    unpublishedKey.Add($"{optionsType.Name}.{output} -> \"{key}\", which {target.Name} does not publish");
                }
            }
        }

        reachable.Should().BeGreaterThan(0, "the arm table has slots that reach this fallback");

        using var scope = new AssertionScope();
        answeredWithPrimary.Should().BeEmpty(
            $"a slot the indicator has no key for must fail rather than answer with the primary series "
            + $"({answeredWithPrimary.Count} of {reachable} reachable slots): {Sample(answeredWithPrimary)}");
        unpublishedKey.Should().BeEmpty(
            $"a pinned key the indicator does not publish must fail rather than answer with the primary series "
            + $"({unpublishedKey.Count} of {reachable} reachable slots): {Sample(unpublishedKey)}");
    }

    /// <summary>Enough to diagnose, not so much that the failure is unreadable.</summary>
    private static string Sample(IReadOnlyList<string> items) =>
        string.Join(" | ", items.Take(12))
        + (items.Count > 12 ? $" ... and {items.Count - 12} more" : string.Empty);
}
