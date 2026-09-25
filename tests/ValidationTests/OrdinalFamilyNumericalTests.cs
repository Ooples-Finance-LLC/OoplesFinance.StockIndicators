using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OrdinalFamilyNumericalTests
{
    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "ConditionalAccumulator", "ContractHigh", "ContractLow", "DemarkReversalPoints", "DemarkSetupIndicator",
        "FractalChaosOscillator", "Dema2Lines", "KeltnerChannelMiddle", "SpearmanIndicator", "PriceVolumeRank",
        "EhlersNoiseEliminationTechnology", "EhlersSpearmanRankIndicator", "SentimentZoneOscillator",
        "TotalPowerIndicator", "TrendPersistenceRate", "GuppyCountBackLine", "GOscillator", "MultiVoteOnBalanceVolume"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var keys = builtIn.BatchOutputKey is { } key ? new[] { key } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName).ToArray();
        var bars = Enumerable.Range(0, Math.Max(64, indicator.WarmupBars + 8)).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i % 5, 16 + i % 7, 2 + i % 3, 10 + i % 4, i % 3)).ToArray();
        var options = builtIn.CreateOptions();
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        var output = keys.Select(k => BuilderArmBinding.Compute(Data(bars), new IndicatorSpec(builtIn.BatchName, options, k), target).ToArray()).ToArray();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        foreach (var rule in rules) rule.Check(new IndicatorValidationContext("fault-baseline", bars, output, 0));
        for (var slot = 0; slot < keys.Length; slot++)
        {
            var corrupted = output.Select(values => values.ToArray()).ToArray();
            corrupted[slot][^1] += 1;
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var rule in rules) rule.Check(new IndicatorValidationContext("injected-output", bars, corrupted, 0));
            });
        }
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ORDINAL", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)
    {
        var source = new Sma(3);
        var original = testCase.Factory();
        IIndicator indicator = original switch
        {
            IndicatorBase single => single.Of(source),
            MultiOutputIndicatorBase multiple => multiple.Of(source),
            _ => throw new InvalidOperationException("Missing input-composition coverage.")
        };
        var bars = Enumerable.Range(0, Math.Max(64, indicator.WarmupBars + 8)).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, 15 + i % 7, -5 - i % 3, 2 + i % 11, i % 4)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var output = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("selected-source", projected, output, 0));

        var feed = Bars.Live();
        using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup()
            .ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var bar in bars) feed.Publish(bar);
        feed.Complete();
        var liveOutput = indicator.Outputs.Select(_ => new List<double>()).ToArray();
        await foreach (var snapshot in live)
            for (var slot = 0; slot < liveOutput.Length; slot++) liveOutput[slot].Add(snapshot[indicator.Outputs[slot]]);
        Assert.All(liveOutput, values => Assert.Equal(bars.Length, values.Count));
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("live-selected-source", projected, liveOutput.Select(v => v.ToArray()).ToArray(), 0));
    }

    [Fact]
    public void FullKeltnerMiddleDoesNotReconstructItsMeanFromOverflowingEndpoints()
    {
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 1)).ToArray();
        var result = Data(bars).CalculateKeltnerChannels(length1: 3, length2: 2);
        Assert.All(result.OutputValues["MiddleBand"], value => Assert.Equal(double.MaxValue, value));
    }

    [Fact]
    public async Task WeightedComponentHasPublicLiveRouteWithRequestedPeriod()
    {
        var indicator = new Wma(3);
        var feed = Bars.Live();
        using var run = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup()
            .ConfigureIndicators(indicator).BuildAsync();
        foreach (var value in new[] { 2d, 4, 6, 8 })
            feed.Publish(new Bar(DateTime.UnixEpoch.AddMinutes(value), value, value, value, value, 1));
        feed.Complete();
        var actual = new List<double>();
        await foreach (var snapshot in run) actual.Add(snapshot[indicator]);
        Assert.Equal(new[] { 1d, 8d / 3, 14d / 3, 20d / 3 }, actual);
    }

    private static IEnumerable<IndicatorValidationFixture> Fixtures(int count)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(count, 244)) yield return fixture;
        var levels = new[] { double.MaxValue, -double.MaxValue, -double.MaxValue, 0d, double.Epsilon, 1d, 1d, -1d };
        yield return new("signed-extreme-ties", Enumerable.Range(0, count).Select(i =>
        {
            var v = levels[i % levels.Length];
            return new Bar(DateTime.UnixEpoch.AddDays(i * 7), v, v, v, v, i % 3 == 0 ? 0 : 1);
        }));
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase, new()
        {
            RequireFormulaReference = true,
            AdditionalFixtures = Fixtures(Math.Max(64, testCase.Factory().WarmupBars + 8))
                .Select(f => new IndicatorValidationFixture("ordinal/" + f.Name, f.Bars)).ToArray()
        });
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route)
        => CheckRoutes(testCase, route);

    internal void CheckRoutes(IndicatorValidationCase testCase, string route,
        Func<IReadOnlyList<Bar>, IReadOnlyDictionary<string, double[]>>? overflowReference = null)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var keys = builtIn.BatchOutputKey is { } key ? new[] { key } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName).ToArray();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        Assert.Equal(keys.Length, rules.Length);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in Fixtures(Math.Max(32, indicator.WarmupBars + 2)))
        {
            var bars = fixture.Bars;
            var output = keys.Select(_ => new double[bars.Count]).ToArray();
            if (route is "batch" or "fast")
            {
                for (var slot = 0; slot < keys.Length; slot++)
                {
                    var outputSpec = new IndicatorSpec(builtIn.BatchName, options, keys[slot]);
                    if (route == "batch") output[slot] = BuilderArmBinding.Compute(Data(bars), outputSpec, target).ToArray();
                    else
                    {
                        using var context = new ComputeContext();
                        using var buffer = IndicatorCompute.TryComputeFast(Data(bars), outputSpec, context);
                        Assert.NotNull(buffer);
                        output[slot] = buffer.Value.ToArray();
                    }
                }
                Check();
            }
            else
            {
                var state = route == "native" ? StatefulIndicatorFactory.Create(spec) : StreamingIndicatorFactory.CreateState(spec);
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var alternate = new Bar(b.Time, 3, 4, -2, -1, 7);
                        state.Update(Native(alternate), false, true);
                        var preview = state.Update(Native(b), false, true);
                        var final = state.Update(Native(b), true, true);
                        for (var slot = 0; slot < keys.Length; slot++)
                        {
                            var expectedPreview = preview.Outputs is not null ? preview.Outputs[keys[slot]] : preview.Value;
                            output[slot][i] = final.Outputs is not null ? final.Outputs[keys[slot]] : final.Value;
                            Assert.Equal(expectedPreview, output[slot][i]);
                            Assert.True(keys.Length == 1 || final.Outputs is not null);
                        }
                    }
                    Check();
                }
            }
            void Check()
            {
                if (overflowReference is not null)
                {
                    var expected = overflowReference(bars);
                    var budget = new IndicatorErrorBudget(1e-9, 1e-9);
                    for (var slot = 0; slot < keys.Length; slot++)
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var value = expected[keys[slot]][i];
                        if (!double.IsFinite(value)) Assert.Equal(value, output[slot][i]);
                        else Assert.True(budget.Accepts(value, output[slot][i]), $"{route}/{fixture.Name}/{keys[slot]}/{i}: expected {value:R}, got {output[slot][i]:R}");
                    }
                    return;
                }
                Assert.All(output.SelectMany(values => values), value => Assert.True(double.IsFinite(value), fixture.Name));
                foreach (var rule in rules) rule.Check(new IndicatorValidationContext(route + "/" + fixture.Name, bars, output, 0));
            }
        }
    }
}
