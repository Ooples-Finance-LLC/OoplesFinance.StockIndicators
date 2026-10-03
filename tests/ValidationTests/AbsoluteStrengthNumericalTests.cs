using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AbsoluteStrengthNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExtremeReturnsAndAllPeriodsPreserveTheCumulativeFormula()
    {
        foreach (var length in new[] { 1, 2, 7, 20 })
        foreach (var meanLength in new[] { 1, 3, 21 })
        foreach (var signalLength in new[] { 1, 2, 7, 34 })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, 9, 9, 9, 9 }, new[] { double.Epsilon, double.MaxValue, double.Epsilon, double.MaxValue, double.MaxValue, 1 }, new[] { double.Epsilon, 3 * double.Epsilon, double.Epsilon, double.Epsilon }, new[] { 4d, 2, 1, 1, 2 } })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.AbsoluteStrengthOutputs(bars, length, meanLength, signalLength)["Asi"];
            Assert.Equal(expected, Data(bars).CalculateAbsoluteStrengthIndex(length, meanLength, signalLength).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAbsoluteStrengthIndexFast(Data(bars), context, length, meanLength, signalLength);
            Assert.Equal(expected, raw.Span.ToArray());
            var state = new AbsoluteStrengthIndexState(length, meanLength, signalLength);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
    }
    [Fact]
    public void SelectedPositiveInputsAndDomainRejectionsAreConsistent()
    {
        var prices = new[] { 1d, 2, 4 }; var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAbsoluteStrengthIndexFast(data, context, 2);
        Assert.Equal(BuiltInFormulaReferences.AbsoluteStrengthOutputs(BarsOf(prices), 2, 21, 34)["Asi"], raw.Span.ToArray());
        foreach (var bad in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            var bars = BarsOf(new[] { 1d, bad });
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateAbsoluteStrengthIndex());
            Assert.Throws<ArgumentOutOfRangeException>(() => { using var rejected = IndicatorCompute.ComputeAbsoluteStrengthIndexFast(Data(bars), context); });
            var state = new AbsoluteStrengthIndexState(); var control = new AbsoluteStrengthIndexState();
            state.Update(Native(bars[0]), true, false); control.Update(Native(bars[0]), true, false);
            foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(bars[1]), final, true));
            var good = Native(BarsOf(new[] { 3d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new AbsoluteStrengthIndexState(length: 3);
            IStreamingIndicatorState control = new AbsoluteStrengthIndexState(length: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AbsoluteStrengthIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AbsoluteStrengthOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
public async Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)
    {
        var source = new Ema(3);
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
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IEnumerable<IndicatorValidationFixture> Fixtures(int count) => IndicatorAdversarialCases.Generate(count, 244);
    private void CheckRoutes(IndicatorValidationCase testCase, string route,
        Func<IReadOnlyList<Bar>, IReadOnlyDictionary<string, double[]>>? overflowReference = null, IndicatorErrorBudget? overflowBudget = null)
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
            var bars = fixture.Bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, Math.Max(double.Epsilon, Math.Abs(b.Close)), b.Volume)).ToArray();
            var output = keys.Select(_ => new double[bars.Length]).ToArray();
            if (route is "batch" or "fast" or "arm")
            {
                for (var slot = 0; slot < keys.Length; slot++)
                {
                    var outputSpec = new IndicatorSpec(builtIn.BatchName, options, keys[slot]);
                    if (route == "batch") output[slot] = BuilderArmBinding.Compute(Data(bars), outputSpec, target).ToArray();
                    else
                    {
                        using var context = new ComputeContext();
                        using var buffer = route == "arm" ? IndicatorCompute.ComputeArm(Data(bars), outputSpec, context)
                            : IndicatorCompute.TryComputeFast(Data(bars), outputSpec, context);
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
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var alternate = new Bar(b.Time, 3, 4, -2, 1, 7);
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
                    var budget = overflowBudget ?? new IndicatorErrorBudget(1e-9, 1e-9);
                    for (var slot = 0; slot < keys.Length; slot++)
                    for (var i = 0; i < bars.Length; i++)
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
