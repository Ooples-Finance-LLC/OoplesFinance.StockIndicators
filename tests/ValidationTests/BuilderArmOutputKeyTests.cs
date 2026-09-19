using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

/// <summary>
/// An output an indicator does not publish is refused, rather than answered with its primary series.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BuilderArmBinding"/> used to end by handing back the primary series whenever it could not resolve
/// the output it was asked for. That is the defect #186 removed from <c>SeriesEvaluator</c>, which raises
/// instead and says why in its own words: a series the indicator does not produce "would be a wrong number
/// rather than an error". The substitution survived in the other resolver, defended by a comment claiming the
/// registry invents a key for any indicator - which stopped being true when that registry was changed to stop
/// guessing, and stopped existing when #219 removed it. The fallback outlived its own justification twice.
/// </para>
/// <para>
/// It is reachable from the public Builder, not from tests alone: <c>IndicatorCompute.TryComputeFast</c> sends
/// every typed spec whose options type is not among the verified arms straight to
/// <see cref="BuilderArmBinding"/>, so every unverified arm target can be asked for an output its indicator
/// does not publish.
/// </para>
/// <para>
/// Such a request is not a defect in the tables - it is perfectly correct that an absolute price oscillator has
/// no upper band. What was wrong was the answer given when one was asked for. So this asserts the behaviour
/// rather than the tables: the output is refused. A sample is taken because each call runs the batch indicator,
/// and running several hundred of them would buy nothing over a spread of them.
/// </para>
/// <para>
/// This used to walk the six <c>IndicatorOutput</c> slots and keep the pairs whose key resolved to nothing.
/// With the slots gone there is exactly one way to ask for a series an indicator does not produce - name a key
/// it does not publish - so that is what these now do. See issue #219.
/// </para>
/// </remarks>
public sealed class BuilderArmOutputKeyTests : GlobalTestData
{
    /// <summary>Enough indicators to span the table, few enough that each can run its calculation.</summary>
    private const int Sampled = 20;

    [Fact]
    public void AKeyTheIndicatorDoesNotPublishIsRefused()
    {
        var bars = StockTestData.Take(120).ToList();
        var answered = new List<string>();
        var refused = 0;

        foreach (var (optionsType, target, outputKey) in UnpublishedKeys().Take(Sampled))
        {
            var options = BuilderArmTests.Create(optionsType, alternate: false);
            if (options is null)
            {
                continue;
            }

            var spec = new IndicatorSpec(target.Name, options, outputKey);

            try
            {
                var values = BuilderArmBinding.Compute(new StockData(bars), spec, target);
                answered.Add($"{optionsType.Name} answered '{outputKey}' with {values.Count} values from "
                    + $"{target.Name}, which publishes no such output");
            }
            catch (CalculationException)
            {
                refused++;
            }
        }

        using var scope = new AssertionScope();
        refused.Should().BeGreaterThan(0, "the sample must actually exercise the refusal, or it proves nothing");
        answered.Should().BeEmpty(
            $"a key the indicator does not publish must be refused, not answered with another series: {string.Join(" | ", answered)}");
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
        var (optionsType, target, outputKey) = UnpublishedKeys().First();
        var options = BuilderArmTests.Create(optionsType, alternate: false);
        options.Should().NotBeNull("the first sampled options type must be constructible");

        var spec = new IndicatorSpec(target.Name, options ?? throw new InvalidOperationException("no options"), outputKey);
        var act = () => BuilderArmBinding.Compute(new StockData(bars), spec, target);

        act.Should().Throw<CalculationException>()
            .WithMessage($"*{target.Name}*{outputKey}*Available outputs:*",
                "the message says which indicator, which output was asked for, and what it does publish");
    }

    /// <summary>
    /// A bad key carried by the <em>target</em> rather than the spec is refused as well.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BuilderArmBinding.Compute</c> resolves <c>spec.OutputKey ?? target.OutputKey</c>, so there are two
    /// ways in and the tests above reach only the first. This is the second: the spec names no key, and the
    /// key the target pins names nothing the indicator publishes. That is how <c>"SignalFastK"</c> passed for
    /// the stochastic's D line - a wrong pin in the arm table, not a wrong request from a caller - and since
    /// #226 corrected it there is no longer a live instance of it anywhere, so nothing exercises this branch
    /// by accident and a regression in it would be silent.
    /// </para>
    /// <para>
    /// <see cref="UnpublishedKeys"/> only ever varies the spec's key, and <c>BuilderArmTests</c> compares
    /// <c>TryComputeFast</c> against this same method, so a change here moves both sides of that comparison
    /// together and it keeps agreeing with itself. Raised by review on PR #229.
    /// </para>
    /// <para>
    /// The bad key is handed in through the target rather than registered anywhere, on purpose. A table shared
    /// across the process would have to be mutated and restored, and this assembly declares no collection
    /// behaviour, so xunit runs these classes in parallel: the mutation could answer a different test's
    /// question while it ran, and a finally would not close that window. The target is already a parameter of
    /// the method under test, so nothing global moves.
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

        var spec = new IndicatorSpec(bound.Name, new MacdSpecOptions(12, 26, 9));
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

    /// <summary>
    /// A typed spec that names its output key is answered with that series, not with the indicator's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// #218 taught <c>SeriesEvaluator</c> to read <c>spec.OutputKey</c>, and only <c>SeriesEvaluator</c>. This
    /// method used to resolve from the slot alone, and a spec built from a key carried the primary slot by
    /// construction - the key was what said which series it wanted - so the slot lookup returned this target's
    /// own key, which is null, and the caller was answered with the indicator's primary series. Asking for the
    /// signal line and receiving the MACD line is the #186 defect wearing a different hat. See issue #219.
    /// </para>
    /// <para>
    /// Nothing reached it while the slot enum existed, which is why it went unnoticed: the specs that address
    /// by key were built with <c>GenericIndicatorOptions</c>, which is not in <c>BuilderArmTargets</c>, so they
    /// fell past this method to <c>ComputeWithV2</c> - which did honour the key. It is a typed spec addressed
    /// by key that lands here, and converting the catalogue's typed helpers to keys is precisely what created
    /// those. So this was a prerequisite for that conversion rather than a defect anyone could hit yet; with
    /// #219 landed, it is the ordinary path.
    /// </para>
    /// <para>
    /// Held from both sides on purpose. Requiring only that the answer equals the signal line would pass if
    /// the two series happened to coincide, so the fixture is first required to separate them, and the answer
    /// is then required to differ from the MACD line as well as to equal the signal.
    /// </para>
    /// </remarks>
    [Fact]
    public void ATypedSpecAddressedByKeyIsAnsweredWithThatKey()
    {
        var bars = StockTestData.Take(200).ToList();

        BuilderArmBinding.TryGetTarget(typeof(MacdSpecOptions), out var bound)
            .Should().BeTrue("the MACD options type stands for a batch indicator");
        bound.OutputKey.Should().BeNull(
            "this target names no key of its own, so only spec.OutputKey can say which series is wanted");

        // maType is left to default, because Compute passes the method's own default for any parameter the
        // options do not map - naming a different one here would fail the comparison for the wrong reason.
        var batch = new StockData(bars).CalculateMovingAverageConvergenceDivergence(
            fastLength: 12, slowLength: 26, signalLength: 9);
        var signal = batch.OutputValues["Signal"];
        var macdLine = batch.OutputValues["Macd"];
        signal.Should().NotEqual(macdLine,
            "the fixture must separate the two series, or this could not tell them apart");

        var spec = new IndicatorSpec(bound.Name, new MacdSpecOptions(12, 26, 9), "Signal");
        var actual = BuilderArmBinding.Compute(new StockData(bars), spec, bound);

        actual.Should().Equal(signal, "the spec named Signal, so Signal is the series it must be answered with");
        actual.Should().NotEqual(macdLine,
            "answering with the indicator's own line is the substitution issue #219 exists to remove");
    }

    /// <summary>
    /// A key each indicator does not publish, in a stable order.
    /// </summary>
    /// <remarks>
    /// This used to yield the six-slot combinations whose key resolved to nothing. With the slots gone there
    /// is only one way to ask for a series an indicator does not produce - name a key it does not publish -
    /// so that is what this yields. The name is built from the indicator's own name, which no calculation
    /// publishes as an output key. See issue #219.
    /// </remarks>
    private static IEnumerable<(Type OptionsType, BuilderArmTarget Target, string OutputKey)> UnpublishedKeys()
    {
        // Every bound options type, not only the unverified ones. BuilderArmBinding.Compute does not consult
        // BuilderVerifiedArms, so neither does this sample. Tying it to that set made it shrink as arms were
        // promoted, until one arm short of the whole table it held a single type - and that type was the one
        // comparison indicator, which cannot be computed from one series at all, so the sample had stopped
        // exercising the refusal it exists to hold. The reachable path is still the unverified one, through
        // IndicatorCompute.TryComputeFast; the method under test is the same either way.
        foreach (var (optionsType, target) in BuilderArmTargets.Targets.OrderBy(t => t.Key.Name, StringComparer.Ordinal))
        {
            var absent = $"NotPublishedBy{target.Name}";
            if (!GeneratedIndicatorOutputs.KeysFor(target.Name).Contains(absent))
            {
                yield return (optionsType, target, absent);
            }
        }
    }
}
