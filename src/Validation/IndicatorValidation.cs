using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

public sealed class IndicatorValidationOptions
{
    /// <summary>Minimum bars in each full fixture. Actual length also covers declared warmup.</summary>
    public int BarsPerFixture { get; set; } = 256;
    /// <summary>Fail rather than silently omit checks for an excessive warmup.</summary>
    public int MaximumBarsPerFixture { get; set; } = 8192;
    /// <summary>Require at least one mathematical contract as well as the universal checks.</summary>
    public bool RequireMathematicalContract { get; set; } = true;
    /// <summary>Require an independent formula reference for every declared output.</summary>
    public bool RequireFormulaReference { get; set; } = true;

    /// <summary>Universal checks only. This mode does not establish formula correctness.</summary>
    public static IndicatorValidationOptions Smoke() => new()
    { RequireMathematicalContract = false, RequireFormulaReference = false };

    /// <summary>Additional replay or adversarial cases within the declared input domain.</summary>
    public IReadOnlyList<IndicatorValidationFixture> AdditionalFixtures { get; set; } = Array.Empty<IndicatorValidationFixture>();
}

public sealed class IndicatorValidationFailure
{
    internal IndicatorValidationFailure(string fixture, string rule, string message)
    { Fixture = fixture; Rule = rule; Message = message; }
    public string Fixture { get; }
    public string Rule { get; }
    public string Message { get; }
    public override string ToString() => Fixture + "/" + Rule + ": " + Message;
}

public sealed class IndicatorValidationReport
{
    internal IndicatorValidationReport(string name, int fixtures, int checkedValues, int mathematicalRules,
        List<IndicatorValidationFailure> failures, IndicatorFormulaCoverage? formulaCoverage, int inputRejectionsChecked = 0,
        IEnumerable<IndicatorFixtureEvidence>? fixtureEvidence = null, int outputOverflowRejectionsChecked = 0)
    {
        OutputOverflowRejectionsChecked = outputOverflowRejectionsChecked;
        InputRejectionsChecked = inputRejectionsChecked; FormulaCoverage = formulaCoverage; Case = name;
        FixturesCompleted = fixtures; ValuesChecked = checkedValues; MathematicalRuleCount = mathematicalRules;
        Failures = failures.AsReadOnly();
        FixtureEvidence = Array.AsReadOnly((fixtureEvidence ?? Array.Empty<IndicatorFixtureEvidence>()).ToArray());
    }
    /// <summary>Declared reference coverage; null if construction or contract inspection failed.</summary>
    public IndicatorFormulaCoverage? FormulaCoverage { get; }
    public string Case { get; }
    public int FixturesCompleted { get; }
    /// <summary>Every attempted input fixture, including failed or interrupted cases.</summary>
    public IReadOnlyList<IndicatorFixtureEvidence> FixtureEvidence { get; }
    public int ValuesChecked { get; }
    /// <summary>Invalid observations explicitly rejected before replaying valid recovery data.</summary>
    public int InputRejectionsChecked { get; }
    /// <summary>Fresh executions rejected at the first independently proven unrepresentable output.</summary>
    public int OutputOverflowRejectionsChecked { get; }
    /// <summary>Counts both properties and references. A positive count does not imply complete formula coverage.</summary>
    public int MathematicalRuleCount { get; }
    public IReadOnlyList<IndicatorValidationFailure> Failures { get; }
    public bool IsValid => Failures.Count == 0 && FixturesCompleted > 0 && ValuesChecked > 0;
    public void ThrowIfInvalid()
    {
        if (!IsValid) throw new IndicatorValidationException(this);
    }
}

public sealed class IndicatorValidationException : Exception
{
    public IndicatorValidationException(IndicatorValidationReport report)
        : base((report ?? throw new ArgumentNullException(nameof(report))).Case + Environment.NewLine
            + string.Join(Environment.NewLine, report.Failures.Select(f => f.ToString()))) => Report = report;
    public IndicatorValidationReport Report { get; }
}

/// <summary>
/// Runs reproducible checks in tests or development tools, never implicitly in production execution.
/// Passing these checks is evidence for the tested properties, not a proof of an arbitrary formula.
/// </summary>
public static class IndicatorValidation
{
    public static async Task ValidateAndThrowAsync(IndicatorValidationCase testCase,
        IndicatorValidationOptions? options = null, CancellationToken cancellationToken = default)
        => (await ValidateAsync(testCase, options, cancellationToken).ConfigureAwait(false)).ThrowIfInvalid();

    public static async Task<IndicatorValidationReport> ValidateAsync(IndicatorValidationCase testCase,
        IndicatorValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (testCase is null) throw new ArgumentNullException(nameof(testCase));
        options ??= new IndicatorValidationOptions();
        if (options.BarsPerFixture < 2 || options.MaximumBarsPerFixture < options.BarsPerFixture)
            throw new ArgumentOutOfRangeException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        var additionalFixtures = (options.AdditionalFixtures ?? throw new ArgumentNullException(nameof(options.AdditionalFixtures))).ToArray();
        if (additionalFixtures.Any(f => f is null || f.Bars.Count > options.MaximumBarsPerFixture)
            || additionalFixtures.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != additionalFixtures.Length)
            throw new ArgumentException("Additional fixtures must have unique names and fit the configured bar budget.", nameof(options));
        var failures = new List<IndicatorValidationFailure>();
        var fixtureEvidence = new List<IndicatorFixtureEvidence>();
        var fixtures = 0;
        var checkedValues = 0;
        var inputRejections = 0;
        var outputOverflowRejections = 0;
        var mathematicalRules = 0;
        IndicatorFormulaCoverage? formulaCoverage = null;
        var isAverage = false;
        var checkCustomerReset = false;
        var includeNumericalExtremes = false;
        var includeWilliamsOverflowFixtures = false;
        var rateOfChangeOverflowPeriod = 0;
        var volumeRateOverflow = false;
        var momentumOverflow = false;
        var simpleReturnsOverflow = false;
        var facilitationOverflow = false;
        var differenceOverflowPeriod = 0;
        var volumeDifferenceOverflow = false;
        var rangeOverflow = false;
        var balanceOverflow = false;
        var obvOverflow = false;
        var cumulativeOverflow = false;
        var cumulativeVolumeOverflow = false;
        var normalizedVolumePeriod = 0;
        var volumeZonePeriod = 0;
        var dayRangePeriod = 0;
        var moveTrackerOverflow = false;
        var priceChannelPeriod = 0;
        var envelopePeriod = 0;
        IndicatorStartupPolicy[] startupPolicies = Array.Empty<IndicatorStartupPolicy>();
        var instances = new HashSet<IIndicator>(IndicatorReferenceComparer.Instance);
        int warmup;
        IndicatorValidationRule[] rules;
        IndicatorInputDomain inputDomain;
        try
        {
            var probe = Create();
            checkCustomerReset = CustomerStateValidation.RequiresCheck(probe);
            rateOfChangeOverflowPeriod = probe switch { Roc roc => roc.Length, RateOfChange rate => rate.Length, Vroc volumeRate => volumeRate.Length, Momentum momentum => momentum.Length, MomentumOscillator oscillator => oscillator.Length, SimpleReturns simple => simple.Length, _ => 0 };
            volumeRateOverflow = probe is Vroc;
            momentumOverflow = probe is Momentum or MomentumOscillator;
            simpleReturnsOverflow = probe is SimpleReturns;
            facilitationOverflow = probe is MarketFacilitationIndex;
            balanceOverflow = probe is BalanceOfPower;
            obvOverflow = probe is Obv or OnBalanceVolume;
            cumulativeOverflow = probe is CumulativeSum or CumulativeVolumeIndex;
            cumulativeVolumeOverflow = probe is CumulativeVolumeIndex;
            normalizedVolumePeriod = probe is NormalizedVolume normalized ? normalized.Length : 0;
            volumeZonePeriod = probe is VolumeZoneOscillator zone ? zone.Length : 0;
            if (probe is IBuiltInIndicator envelope && BuiltInFormulaReferences.HasRoundedEnvelope(envelope))
                envelopePeriod = (int)envelope.CreateOptions().GetType().GetProperty("Length")!.GetValue(envelope.CreateOptions())!;
            if (probe is IBuiltInIndicator priceChannel && BuiltInFormulaReferences.HasRoundedPriceChannel(priceChannel))
                priceChannelPeriod = (int)priceChannel.CreateOptions().GetType().GetProperty("Length")!.GetValue(priceChannel.CreateOptions())!;
            moveTrackerOverflow = probe is IBuiltInIndicator moveTracker && moveTracker.BatchName == IndicatorName.MoveTracker;
            dayRangePeriod = probe switch { AverageDayRange dayRange => dayRange.Length, Adr adr => adr.Length, _ => 0 };
            differenceOverflowPeriod = probe switch { PriceMomentum price => price.Length, VolumeMomentum volume => volume.Length, _ => 0 };
            volumeDifferenceOverflow = probe is VolumeMomentum;
            rangeOverflow = probe is IBuiltInIndicator rangeIndicator && rangeIndicator.BatchName is IndicatorName.Range or IndicatorName.TrueRange;
            includeWilliamsOverflowFixtures = probe is IBuiltInIndicator williams && williams.BatchName == IndicatorName.WilliamsR;
            includeNumericalExtremes = IncludesNumericalFixtures(probe);
            inputDomain = IndicatorInputDomain.For(probe);
            warmup = probe.WarmupBars;
            startupPolicies = probe.Outputs.Select(output => probe is IIndicatorStartupContract startup
                ? startup.StartupPolicy(output.Slot) : IndicatorStartupPolicy.Finite).ToArray();
            if (startupPolicies.Any(policy => !Enum.IsDefined(typeof(IndicatorStartupPolicy), policy)))
                throw new InvalidOperationException("Unknown startup policy.");
            if (warmup < 0 || warmup > options.MaximumBarsPerFixture - 32)
                throw new InvalidOperationException("Declared warmup is negative or exceeds the validation budget; configure a sufficient MaximumBarsPerFixture.");
            rules = IndicatorFormulaCoverage.Resolve(testCase, probe);
            formulaCoverage = new IndicatorFormulaCoverage(probe, rules);
            if (options.RequireFormulaReference && !formulaCoverage.IsComplete)
                Add("coverage", "FormulaReference", "Missing independent formula references for output slots: "
                    + string.Join(", ", formulaCoverage.MissingOutputSlots));
            if (options.RequireFormulaReference && formulaCoverage.RecurrenceOnlyOutputSlots.Count != 0)
                Add("coverage", "IndependentFormulaTrajectory", "Only observed-history recurrence evidence for output slots: "
                    + string.Join(", ", formulaCoverage.RecurrenceOnlyOutputSlots));
            if (options.RequireFormulaReference && !formulaCoverage.HasCompleteStartupReferences)
                Add("coverage", "StartupFormulaReference", "Missing formula references covering startup for output slots: "
                    + string.Join(", ", formulaCoverage.MissingStartupOutputSlots));
            isAverage = probe is IMovingAverage;
            if (isAverage)
            {
                var primary = probe is IPrimaryOutputIndicator named ? named.PrimaryOutput : probe.Outputs[0];
                rules = rules.Concat(new[] { IndicatorValidationRule.ConstantAverage(primary.Slot) }).ToArray();
                if (options.MaximumBarsPerFixture < Math.Max(4000, warmup + 1000))
                    throw new InvalidOperationException("Constant-average checks require at least 4000 bars and 1000 bars after declared warmup; configure a sufficient MaximumBarsPerFixture.");
            }
            mathematicalRules = rules.Length;
            if (options.RequireMathematicalContract && mathematicalRules == 0)
                Add("coverage", "MathematicalContract", "No mathematical contract was declared; universal checks alone cannot establish formula correctness.");
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Add("construction", "Discovery", ex.GetBaseException().Message);
            return Report();
        }

        var count = Math.Max(options.BarsPerFixture, warmup + 32);
        var allFixtures = IndicatorValidationFixtures.Create(count, warmup, isAverage, includeNumericalExtremes)
            .Concat(inputDomain.BoundaryFixtures(count))
            .Concat(OutputOverflowFixtures())
            .Concat(additionalFixtures.Select(f => (Name: f.Name, Bars: f.Bars))).ToArray();
        if (allFixtures.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != allFixtures.Length)
            throw new ArgumentException("Additional fixture names must not collide with generated fixtures.", nameof(options));
        foreach (var invalid in inputDomain.InvalidExamples())
            await CheckRejection(invalid, "generated-invalid-input").ConfigureAwait(false);

        foreach (var fixture in allFixtures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var initialValues = checkedValues;
            var initialRejections = inputRejections;
            var initialOverflowRejections = outputOverflowRejections;
            var initialFailures = failures.Count;
            var customerResetChecked = false;
            int? overflowBarIndex = null, overflowSlot = null, overflowSign = null;
            var completed = false;
            try
            {
                var accepted = fixture.Bars.ToArray();
                for (var i = 0; i < accepted.Length; i++)
                    if (inputDomain.Violation(accepted[i]) is not null)
                    {
                        await CheckRejection(accepted[i], fixture.Name + "/bar-" + i).ConfigureAwait(false);
                        accepted[i] = inputDomain.ValidExample(accepted[i].Time);
                    }
                var overflow = new List<(int Slot, int Bar, double Value)>();
                foreach (var rule in rules.Where(rule => rule.OverflowReference is not null))
                {
                    var expected = rule.OverflowReference!(accepted);
                    if (expected.Count != accepted.Length)
                        throw new InvalidOperationException("Overflow reference output length differs.");
                    for (var i = 0; i < expected.Count; i++)
                        if (double.IsInfinity(expected[i]))
                        {
                            overflow.Add((rule.ReferenceOutputSlot!.Value, i, expected[i]));
                            break;
                        }
                }
                var prefixLength = overflow.Count == 0 ? accepted.Length : overflow.Min(value => value.Bar);
                var checkedBars = prefixLength == accepted.Length ? accepted : accepted.Take(prefixLength).ToArray();
                var first = await Run(checkedBars).ConfigureAwait(false);
                var second = await Run(checkedBars).ConfigureAwait(false);
                if (first.Count != second.Count)
                    Add(fixture.Name, "Determinism", "The number of outputs changed between fresh runs.");
                for (var slot = 0; slot < first.Count; slot++)
                {
                    var values = first[slot];
                    if (values.Count != checkedBars.Length)
                        Add(fixture.Name, "Length", $"Output {slot}: {values.Count} values for {checkedBars.Length} bars.");
                    if (slot < second.Count && !values.SequenceEqual(second[slot]))
                        Add(fixture.Name, "Determinism", $"Output {slot} differs between fresh runs.");
                    for (var i = 0; i < values.Count; i++)
                    {
                        checkedValues++;
                        var policy = i < warmup ? startupPolicies[slot] : IndicatorStartupPolicy.Finite;
                        if (double.IsInfinity(values[i])
                            || (double.IsNaN(values[i]) && policy == IndicatorStartupPolicy.Finite)
                            || (!double.IsNaN(values[i]) && policy == IndicatorStartupPolicy.NaN))
                        {
                            Add(fixture.Name, "Finite", $"Output {slot}, bar {i}: {values[i]:R}; required policy {policy}.");
                            break;
                        }
                    }
                }
                var context = new IndicatorValidationContext(fixture.Name, checkedBars, first, warmup);
                foreach (var rule in rules)
                {
                    try { rule.Check(context); }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    { Add(fixture.Name, rule.Name, ex.GetBaseException().Message); }
                }
                try
                {
                    if (checkCustomerReset)
                    {
                        await CustomerStateValidation.CheckResetAsync(Create(), checkedBars, first, cancellationToken).ConfigureAwait(false);
                        customerResetChecked = true;
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                { Add(fixture.Name, "Reset", ex.GetBaseException().Message); }
                if (overflow.Count != 0)
                {
                    // Execute only through the first unrepresentable bar. A later exception cannot
                    // stand in for rejection at this bar, nor can an unrelated component failure.
                    var rejectedBars = accepted.Take(prefixLength + 1).ToArray();
                    for (var replay = 0; replay < 2; replay++)
                    {
                        try
                        {
                            await Run(rejectedBars).ConfigureAwait(false);
                            Add(fixture.Name, "OutputOverflow", "Accepted an output independently proven to overflow binary64.");
                        }
                        catch (IndicatorOutputException ex) when (ex.IndicatorType == testCase.IndicatorType
                            && ex.BarIndex == prefixLength && overflow.Any(value => value.Bar == prefixLength
                                && value.Slot == ex.OutputSlot && value.Value.Equals(ex.Value))) // NOSONAR: S1244 - Match the actual rejected output value to its exception evidence.
                        {
                            if (overflowSlot.HasValue && (overflowSlot != ex.OutputSlot || overflowSign != Math.Sign(ex.Value)))
                                Add(fixture.Name, "OutputOverflow", "Fresh runs rejected different output slots or signs.");
                            overflowBarIndex = ex.BarIndex; overflowSlot = ex.OutputSlot; overflowSign = Math.Sign(ex.Value);
                            outputOverflowRejections++;
                        }
                    }
                }
                fixtures++;
                completed = true;
            }
            catch (IndicatorOutputException ex)
            { Add(fixture.Name, "Finite", ex.Message); }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            { Add(fixture.Name, "Execution", ex.GetBaseException().Message); }
            finally
            {
                fixtureEvidence.Add(new(fixture.Name, fixture.Bars.Count, checkedValues - initialValues,
                    inputRejections - initialRejections, completed, completed && failures.Count == initialFailures, customerResetChecked,
                    outputOverflowRejections - initialOverflowRejections, overflowBarIndex, overflowSlot, overflowSign));
            }
        }
        if (checkedValues == 0) Add("coverage", "Finite", "No output values were checked.");
        return Report();

        IIndicator Create()
        {
            var indicator = testCase.Factory();
            if (indicator is null || indicator.GetType() != testCase.IndicatorType)
                throw new InvalidOperationException("The factory must return the declared indicator type.");
            if (!instances.Add(indicator)) throw new InvalidOperationException("The factory reused an indicator; each run needs a fresh instance.");
            if (indicator.Outputs.Count == 0) throw new InvalidOperationException("The indicator declares no outputs.");
            return indicator;
        }

        async Task<IReadOnlyList<IReadOnlyList<double>>> Run(IReadOnlyList<Bar> bars)
        {
            var indicator = Create();
            if (indicator.WarmupBars != warmup) throw new InvalidOperationException("Declared warmup changed between instances.");
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).BuildAsync(cancellationToken).ConfigureAwait(false);
            if (!input.SequenceEqual(bars)) throw new InvalidOperationException("The calculation modified its input bars.");
            var primary = indicator is IPrimaryOutputIndicator named ? named.PrimaryOutput : indicator.Outputs[0];
            var primaryValues = run[primary].ToArray();
            if (!run[indicator].ToArray().SequenceEqual(primaryValues))
                throw new InvalidOperationException("The default series differs from the declared primary output.");
            if (bars.Count > 0 && !run.Latest[indicator].Equals(primaryValues[bars.Count - 1])) // NOSONAR: S1244 - Latest must return the same value published in the primary series.
                throw new InvalidOperationException("The snapshot default differs from the declared primary output.");
            return Array.AsReadOnly(indicator.Outputs.Select(output =>
                (IReadOnlyList<double>)Array.AsReadOnly(run[output].ToArray())).ToArray());
        }

        IEnumerable<(string Name, IReadOnlyList<Bar> Bars)> OutputOverflowFixtures()
        {
            if (envelopePeriod > 0)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("moving-average-envelope-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, envelopePeriod + 3).Select(i => new Bar(new DateTime(2020, 1, 1).AddMinutes(i),
                            sign * double.MaxValue, sign * double.MaxValue, sign * double.MaxValue, sign * double.MaxValue, 1)).ToArray());
            if (priceChannelPeriod > 0)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("price-channel-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, priceChannelPeriod + 3).Select(i => new Bar(new DateTime(2020, 1, 1).AddMinutes(i),
                            sign * double.MaxValue, sign * double.MaxValue, sign * double.MaxValue, sign * double.MaxValue, 1)).ToArray());
            if (moveTrackerOverflow)
                foreach (var sign in new[] { 1, -1 })
                foreach (var signal in new[] { false, true })
                    yield return ("move-tracker-" + (signal ? "signal-" : "primary-") + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        (signal ? new[] { 0d, -sign * double.MaxValue, 0d } : new[] { -sign * double.MaxValue, sign * double.MaxValue })
                        .Select((v, i) => new Bar(new DateTime(2020, 1, 1).AddMinutes(i), v, v, v, v, 1)).ToArray());
            if (dayRangePeriod > 0)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("average-day-range-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, dayRangePeriod).Select(i => new Bar(new DateTime(2020, 1, 1).AddMinutes(i),
                            0, sign * double.MaxValue, -sign * double.MaxValue, 0, 1)).ToArray());
            if (obvOverflow)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("obv-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, 2).Select(i => {
                            var price = sign * (i + 1d);
                            return new Bar(new DateTime(2020, 1, 1).AddMinutes(i), price, price, price, price, double.MaxValue);
                        }).ToArray());
            if (volumeZonePeriod > 1)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("volume-zone-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        new[] { double.MaxValue, -double.MaxValue, 3 * double.Epsilon }.Select((volume, i) => {
                            var price = -sign * i;
                            return new Bar(new DateTime(2020, 1, 1).AddMinutes(i), price, price, price, price, volume);
                        }).ToArray());
            if (cumulativeOverflow)
                foreach (var sign in new[] { 1, -1 })
                    yield return ((cumulativeVolumeOverflow ? "cumulative-volume-" : "cumulative-price-")
                        + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, 3).Select(i => {
                            var price = cumulativeVolumeOverflow ? sign * (i + 1d) : sign * double.MaxValue;
                            return new Bar(new DateTime(2020, 1, 1).AddMinutes(i), price, price, price, price, double.MaxValue);
                        }).ToArray());
            if (normalizedVolumePeriod >= 3)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("normalized-volume-" + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, normalizedVolumePeriod).Select(i => {
                            var volume = i == 0 ? double.Epsilon : i == normalizedVolumePeriod - 1 ? sign * double.MaxValue
                                : i == normalizedVolumePeriod - 2 ? -sign * double.MaxValue : 0;
                            return new Bar(new DateTime(2020, 1, 1).AddMinutes(i), 1, 1, 1, 1, volume);
                        }).ToArray());
            if (balanceOverflow)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("balance-" + (sign > 0 ? "positive" : "negative") + "-output-overflow", new[] {
                        new Bar(new DateTime(2020, 1, 1), 0, 1, 0, 1, 1),
                        new Bar(new DateTime(2020, 1, 2), 0, double.Epsilon, 0, sign * double.MaxValue, 1)
                    });
            if (rangeOverflow)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("range-first-" + (sign > 0 ? "positive" : "negative") + "-output-overflow", new[] {
                        new Bar(new DateTime(2020, 1, 1), 0, sign * double.MaxValue, -sign * double.MaxValue, 0, 1)
                    });
            if (differenceOverflowPeriod > 0)
                foreach (var sign in new[] { 1, -1 })
                    yield return ((volumeDifferenceOverflow ? "volume-difference-" : "price-difference-")
                        + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, differenceOverflowPeriod + 1).Select(i => {
                            var value = (i == differenceOverflowPeriod ? sign : -sign) * double.MaxValue;
                            return volumeDifferenceOverflow
                                ? new Bar(new DateTime(2020, 1, 1).AddMinutes(i), 1, 1, 1, 1, value)
                                : new Bar(new DateTime(2020, 1, 1).AddMinutes(i), value, value, value, value, 1);
                        }).ToArray());
            if (facilitationOverflow)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("market-facilitation-" + (sign > 0 ? "positive" : "negative") + "-output-overflow", new[] {
                        new Bar(new DateTime(2020, 1, 1), 1, 1, 1, 1, 1),
                        new Bar(new DateTime(2020, 1, 2), 0, double.MaxValue, -double.MaxValue, 0, sign * double.Epsilon)
                    });
            if (includeWilliamsOverflowFixtures)
                foreach (var sign in new[] { 1, -1 })
                    yield return ("williams-" + (sign > 0 ? "positive" : "negative") + "-output-overflow", new[] {
                        new Bar(new DateTime(2020, 1, 1), 0, double.Epsilon, 0, 0, 1),
                        new Bar(new DateTime(2020, 1, 2), 0, double.Epsilon, 0, sign * double.MaxValue, 1)
                    });
            if (rateOfChangeOverflowPeriod > 0)
                foreach (var sign in new[] { 1, -1 })
                    yield return ((simpleReturnsOverflow ? "simple-returns-" : momentumOverflow ? "momentum-" : volumeRateOverflow ? "vroc-" : "roc-") + (sign > 0 ? "positive" : "negative") + "-output-overflow",
                        Enumerable.Range(0, rateOfChangeOverflowPeriod + 1).Select(i => {
                            var value = i == rateOfChangeOverflowPeriod ? sign * double.MaxValue : double.Epsilon;
                            return volumeRateOverflow
                                ? new Bar(new DateTime(2020, 1, 1).AddMinutes(i), 1, 1, 1, 1, value)
                                : new Bar(new DateTime(2020, 1, 1).AddMinutes(i), value, value, value, value, 1);
                        }).ToArray());
        }

        async Task CheckRejection(Bar invalid, string label)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { await Run(new[] { invalid }).ConfigureAwait(false); }
            catch (Exception ex) when (ex.GetBaseException() is ArgumentException)
            { inputRejections++; return; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { Add(label, "InputDomain", "Expected an argument rejection: " + ex.GetBaseException().Message); return; }
            Add(label, "InputDomain", "Accepted an observation outside the declared input domain.");
        }

        void Add(string fixture, string rule, string message) => failures.Add(new IndicatorValidationFailure(fixture, rule, message));
        IndicatorValidationReport Report() => new(testCase.ToString(), fixtures, checkedValues, mathematicalRules, failures,
            formulaCoverage, inputRejections, fixtureEvidence, outputOverflowRejections);
    }

    private sealed class IndicatorReferenceComparer : IEqualityComparer<IIndicator>
    {
        internal static readonly IndicatorReferenceComparer Instance = new();
        public bool Equals(IIndicator? x, IIndicator? y) => ReferenceEquals(x, y);
        public int GetHashCode(IIndicator obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    // Shared by execution and the per-configuration migration backlog check.
    internal static bool IncludesNumericalFixtures(IIndicator probe)
    {
        var includeNumericalExtremes = probe is HighLowBands or HighLowMovingAverage or EquityMovingAverage or EhlersLaguerreFilter or EhlersOptimumEllipticFilter or EhlersModifiedOptimumEllipticFilter or CompoundRatioMovingAverage or RepulsionMovingAverage or ElasticVolumeWeightedMovingAverageV1 or ElasticVolumeWeightedMovingAverageV2 or OptimalWeightedMovingAverage or SelfWeightedMovingAverage or ThreeHMA or ThreeHma or TripleHullMovingAverage or _3HMA or Hma or HullMovingAverage or IIRLeastSquaresEstimate or HullEstimate or HoltExponentialMovingAverage or HendersonWeightedMovingAverage or RegularizedEma or DampedSineWaveWeightedFilter or RecursiveMovingTrendAverage or EhlersZeroLagEma or EhlersZeroLagExponentialMovingAverage or ZeroLowLagMovingAverage or DoubleExponentialSmoothing or McNichollMovingAverage or ZeroLagTripleExponentialMovingAverage or QuadrupleExponentialMovingAverage or PentupleExponentialMovingAverage or Trix or GeneralizedDoubleEma or GeneralizedDoubleExponentialMovingAverage or EhlersIirFilter or EhlersInfiniteImpulseResponseFilter or EhlersFirFilter or EhlersFiniteImpulseResponseFilter or Spencer15PointMovingAverage or Spencer21PointMovingAverage or AlphaDecreasingEma or AhrensMovingAverage or OCHistogram or Qstick or ChandeQuickStick or DeltaMovingAverage or WellesWilderSummation or WildersSummationMethod or WilliamsAD or Adl or TrendContinuationFactor or TrendDetection or TrendDetectionIndex or TrendTriggerFactor or StochasticCustomOscillator or StochasticMomentumIndex or MarketMeannessIndex or Pzo or PriceZoneOscillator or Nvi or Pvi or NegativeVolumeIndex or PositiveVolumeIndex or TradeVolumeIndex or Pvt or PriceVolumeTrend or VolumePriceTrend or ModifiedPriceVolumeTrend or EaseOfMovement or Emv or ForceIndex or ElderForceIndex or VortexPositive or VortexNegative or VortexPlus or VortexMinus or VortexIndicatorPlus or VortexIndicatorMinus or VolumeAccumulationPercent or TwiggsMoneyFlow or AccumulationDistributionLine or ChaikinOscillator or WilliamsAccumulationDistribution or SmoothedWilliamsAccumulationDistribution or UlcerIndex or MartinRatio or SortinoRatio or SharpeRatio or InformationRatio or TrendIntensityIndex or PriceVolumeOscillator or ShinoharaIntensityRatioA or ShinoharaIntensityRatioB or RelativeVolumeIndicator or UpsideDownsideVolume or TFSVolumeOscillator or VolumeAccumulationOscillator or TreynorRatio or OmegaRatio or UpsidePotentialRatio or TrendDirectionForceIndex or ReversalPoints or DemarkPressureRatioV1 or DemarkPressureRatioV2 or ElderMarketThermometer or RegressionOscillator or LinearRegressionLine or KendallRankCorrelationCoefficient or LogisticCorrelation or EhlersCorrelationTrendIndicator or EhlersCorrelationCycleIndicator or EhlersCorrelationAngleIndicator or EhlersMarketStateIndicator or EmaWaveIndicator or ErgodicMeanDeviationIndicator or TraderPressureIndex or ZScore or FastZScore or InverseFisherZScore or InverseFisherFastZScore or FisherTransform or EhlersFisherTransform or Cci or WoodieCommodityChannelIndex or EhlersCommodityChannelIndexInverseFisherTransform or EhlersInverseFisherTransform or InverseFisherTransformCore or EhlersRelativeStrengthIndexInverseFisherTransform or QuasiWhiteNoise or ConnorsRsi or ConnorsRelativeStrengthIndex or StochasticConnorsRelativeStrengthIndex or CCTStochRSI or CCTStochRelativeStrengthIndex or StochRsi or StochasticRelativeStrengthIndex or StochasticRsiOscillator or ApirineSlowRelativeStrengthIndex or SelfAdjustingRelativeStrengthIndex or AdaptiveRsi or AdaptiveRelativeStrengthIndex or FoldedRelativeStrengthIndex or Rsi or RapidRelativeStrengthIndex or AsymmetricalRsi or AsymmetricalRelativeStrengthIndex or MomentaRelativeStrengthIndex or DoubleSmoothedRelativeStrengthIndex or RelativeMomentumIndex or IntradayMomentumIndex or ChandeIntradayMomentumIndex or PriceMomentumOscillator or Pmo or DecisionPointPriceMomentumOscillator or CoppockCurve or SmoothedRateOfChange or SmoothedRoc or KnowSureThing or Kst or PringSpecialK or SpecialK or SmoothedDeltaRatioOscillator or DoubleSmoothedMomenta or DirectionalTrendIndex or OscOscillator or WamiOscillator or Tsi or TrueStrengthIndex or ErgodicTrueStrengthIndexV1 or ErgodicTrueStrengthIndexV2 or
            EndPointMovingAverage or SharpModifiedMovingAverage or
            HarmonicMeanMovingAverage or
            CloseToCloseVolatility or ParkinsonVolatility or
            GarmanKlassVolatility or RogersSatchellVolatility or YangZhangVolatility or
            HistoricalVolatility or KaseSerialDependencyIndex or
            HistoricalVolatilityPercentile or
            JapaneseCorrelationCoefficient or
            OceanIndicator or
            MayerMultiple or
            HybridConvolutionFilter or
            EhlersSpectrumDerivedFilterBank or
            EhlersRestoringPullIndicator or
            EhlersGaussianFilter or
            EhlersFMDemodulatorIndicator or
            ElderImpulseSystem or
            GannHiLoActivator or
            LinearChannels or
            LinearTrailingStop or
            SupportResistance or
            TimePriceIndicator or
            TrendExhaustionIndicator or
            VanillaABCDPattern or
            VixTradingSystem or
            VostroIndicator or
            UhlMaCrossoverSystem or
            RelativeDifferenceOfSquaresOscillator or
            NaturalDirectionalCombo or
            NaturalDirectionalIndex or
            NaturalStochasticIndicator or
            NaturalMarketMirror or
            NaturalMarketRiver or
            NaturalMarketCombo or
            NaturalMarketSlope or
            McClellanOscillator or
            DecisionPointBreadthSwenlinTradingOscillator or
            ZweigMarketBreadthIndicator or
            TickLineMomentumOscillator or
            ConditionalAccumulator or
            ContractHigh or
            ContractLow or
            DemarkReversalPoints or
            DemarkSetupIndicator or
            FractalChaosOscillator or
            Dema2Lines or
            KeltnerChannelMiddle or
            SpearmanIndicator or
            PriceVolumeRank or
            EhlersNoiseEliminationTechnology or
            EhlersSpearmanRankIndicator or
            SentimentZoneOscillator or
            TotalPowerIndicator or
            TrendPersistenceRate or
            GuppyCountBackLine or
            GOscillator or
            MultiVoteOnBalanceVolume
            || CustomerStateValidation.RequiresCheck(probe) || probe is IBuiltInIndicator builtIn && builtIn.BatchName is
            IndicatorName.TrendImpulseFilter or IndicatorName.DetrendedSyntheticPrice or IndicatorName.TrendForceHistogram or IndicatorName.AwesomeOscillator or IndicatorName.AcceleratorOscillator or IndicatorName.ImpulseMovingAverageConvergenceDivergence or IndicatorName.ImpulsePercentagePriceOscillator or IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator or IndicatorName.TFSMboIndicator or IndicatorName.TFSMboPercentagePriceOscillator or IndicatorName.MovingAverageConvergenceDivergenceLeader or IndicatorName.PercentagePriceOscillatorLeader or IndicatorName.ReverseMovingAverageConvergenceDivergence or IndicatorName.DiNapoliMovingAverageConvergenceDivergence or IndicatorName.DiNapoliPercentagePriceOscillator or IndicatorName._4MovingAverageConvergenceDivergence or IndicatorName._4PercentagePriceOscillator or IndicatorName.LindaRaschke3_10Oscillator or IndicatorName.MirroredMovingAverageConvergenceDivergence or IndicatorName.MirroredPercentagePriceOscillator or IndicatorName.DidiIndex or IndicatorName.ErgodicMovingAverageConvergenceDivergence or IndicatorName.ErgodicPercentagePriceOscillator or IndicatorName.ElliottWaveOscillator or IndicatorName.VolumeMomentumOscillator or IndicatorName.VolumeOscillator or IndicatorName.NormalizedMacd or IndicatorName.MovingAverageConvergenceDivergence or IndicatorName.PercentageVolumeOscillator or IndicatorName.PercentagePriceOscillator or IndicatorName.DisparityIndex or IndicatorName.PerformanceIndex or IndicatorName.PercentageTrailingStops or IndicatorName.NickRypockTrailingReverse or IndicatorName.QmaSmaDifference or IndicatorName.MovingAverageDisplacedEnvelope or IndicatorName.ChandeForecastOscillator or IndicatorName.MeanAbsoluteErrorBands or IndicatorName.MeanAbsoluteDeviationBands or IndicatorName.InterquartileRangeBands or IndicatorName.RangeBands or IndicatorName.MidpointOscillator or IndicatorName.GuppyDistanceIndicator or IndicatorName.GuppyMultipleMovingAverage or IndicatorName.TypicalPriceVolatility or IndicatorName.DownsideDeviation or IndicatorName.CoefficientOfVariation or IndicatorName.Skewness or IndicatorName.StandardDeviationChannel or IndicatorName.RSquared or IndicatorName.LinearRegression or IndicatorName.SimpleMovingAverage or IndicatorName.ExponentialMovingAverage or IndicatorName.StandardDeviation or IndicatorName.ArnaudLegouxMovingAverage or IndicatorName.StandardError or IndicatorName.StandardErrorOfTheMean or IndicatorName.Variance or IndicatorName.SimplifiedLeastSquaresMovingAverage or IndicatorName.LeastSquaresMovingAverage or IndicatorName.LeoMovingAverage or IndicatorName.EhlersHammingMovingAverage or IndicatorName.DoubleExponentialMovingAverage or IndicatorName.TripleExponentialMovingAverage or IndicatorName.ZeroLagExponentialMovingAverage
            or IndicatorName.WellesWilderMovingAverage or IndicatorName.JsaMovingAverage or IndicatorName.VariableIndexDynamicAverage
            or IndicatorName.ChandeMomentumOscillatorAbsolute
            or IndicatorName.DiNapoliPreferredStochasticOscillator or IndicatorName.WilliamsR or IndicatorName.RateOfChange or IndicatorName.VolumeRateOfChange or IndicatorName.MarketFacilitationIndex or IndicatorName.PriceMomentum or IndicatorName.VolumeMomentum or IndicatorName.Range or IndicatorName.TrueRange or IndicatorName.NetVolume or IndicatorName.NormalizedVolume or IndicatorName.SimpleReturns or IndicatorName.CumulativeSum or IndicatorName.CumulativeVolumeIndex or IndicatorName.VolumeZoneOscillator or IndicatorName.AverageDayRange or IndicatorName.MoneyFlowIndex or IndicatorName.MoveTracker or IndicatorName.LogReturns
            or IndicatorName.ChandeMomentumOscillatorAverage or IndicatorName.ChandeMomentumOscillatorAbsoluteAverage
            or IndicatorName.EhlersHannMovingAverage or IndicatorName.SineWeightedMovingAverage or IndicatorName.NaturalMovingAverage or IndicatorName.DistanceWeightedMovingAverage or IndicatorName.InverseDistanceWeightedMovingAverage or IndicatorName.FareySequenceWeightedMovingAverage or IndicatorName.GeometricMeanMovingAverage or IndicatorName.GeometricMovingAverage or IndicatorName.QuadraticMovingAverage or IndicatorName.KaufmanAdaptiveMovingAverage or IndicatorName.Midpoint or IndicatorName.Midprice
            or IndicatorName.ParabolicWeightedMovingAverage or IndicatorName.CubedWeightedMovingAverage or IndicatorName.QuickMovingAverage or IndicatorName.FibonacciWeightedMovingAverage or IndicatorName.SquareRootWeightedMovingAverage
            or IndicatorName.SymmetricallyWeightedMovingAverage or IndicatorName.EhlersTriangleMovingAverage
            or IndicatorName.HighestHigh or IndicatorName.LowestLow or IndicatorName.RollingMax or IndicatorName.RollingMin or IndicatorName.PercentRank
            or IndicatorName.MedianValue or IndicatorName.Trimean
            or IndicatorName.AroonUp or IndicatorName.AroonDown or IndicatorName.AroonOscillator
            or IndicatorName.PsychologicalLine or IndicatorName.ChandeTrendScore
            or IndicatorName.VolumeWeightedAveragePrice or IndicatorName.WindowedVolumeWeightedMovingAverage or IndicatorName.DonchianChannels or IndicatorName.RangeIdentifier or IndicatorName.WilliamsFractals or IndicatorName.GannSwingOscillator or IndicatorName.GannTrendOscillator or IndicatorName.TFSTetherLineIndicator or IndicatorName.TTMScalperIndicator
            or IndicatorName.IchimokuCloud or IndicatorName.IchimokuChikouSpan
            or IndicatorName.WeightedMovingAverage or IndicatorName.LinearWeightedMovingAverage or IndicatorName.SimplifiedWeightedMovingAverage or IndicatorName.AveragePrice or IndicatorName.MedianPrice
            or IndicatorName.TypicalPrice or IndicatorName.FullTypicalPrice or IndicatorName.WeightedClose;
        includeNumericalExtremes |= probe is IBuiltInIndicator volumeIndicator && (BuiltInFormulaReferences.HasSimpleVolumeMean(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedTriangularMean(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedChande(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedFilteredChande(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedEnvelope(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedPriceChannel(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedObv(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedHighLowIndex(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedBalanceOfPower(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedMomentum(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedApo(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedElderRay(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedDpo(volumeIndicator)
            || BuiltInFormulaReferences.HasRoundedBollinger(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedStochastic(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedSequentialMean(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedMiddleMean(volumeIndicator)
            || BuiltInFormulaReferences.HasBoundedSlowMean(volumeIndicator));
        return includeNumericalExtremes;
    }
}
