using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RelativeVolumeNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void PopulationScorePreservesWideResidualsAndExactDemandThreshold()
    {
        foreach (var volumes in new[] {
            new[] { 0d, 0, 0, 0, 10, 0, 0, 0, 0, 20, 0, 0 },
            new[] { 0d, 0, 0, 1, 10, 3, 2, -1, 1, 0 },
            new[] { -double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue, 0, 0, 0 },
            new[] { 0d, 0, 0, 0, double.Epsilon, 0, -double.Epsilon, 0, 0 } })
        foreach (var period in new[] { 1, 3, 5, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new RelativeVolumeIndicator(period), new RelativeVolumeIndicator(period, new Ema()),
            new RelativeVolumeIndicator(period, new Wma()), new RelativeVolumeIndicator(period, new Smma()) })
        {
            var bars = volumes.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 2, i, i + 1, v)).ToArray();
            var expected = BuiltInFormulaReferences.RelativeVolumeOutputs(bars, indicator); var options = indicator.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new IndicatorSpec(indicator.BatchName, options, pair.Key);
                Assert.Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
                using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                var selected = Data(bars); selected.CustomValuesList = bars.Select(_ => 42d).ToList();
                var projected = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray();
                var selectedExpected = BuiltInFormulaReferences.RelativeVolumeOutputs(projected, indicator)[pair.Key];
                using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(selectedExpected, selectedArm.Value.ToArray());
                Assert.Equal(selectedExpected, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            }
            var primary = new IndicatorSpec(indicator.BatchName, options);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(primary), StreamingIndicatorFactory.CreateState(primary)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        state.Update(Native(new Bar(bars[i].Time, 100, 100, 100, 100, 100)), false, true);
                        foreach (var final in new[] { false, false, true })
                        {
                            var actual = state.Update(Native(bars[i]), final, true);
                            foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                        }
                    }
                }
            }
        }
        var threshold = Enumerable.Range(0, 5).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 1, i + 1, i + 1, i == 4 ? double.Epsilon : 0)).ToArray();
        var outputs = BuiltInFormulaReferences.RelativeVolumeOutputs(threshold, new RelativeVolumeIndicator(5));
        Assert.Equal(2, outputs["Rvi"][4]); Assert.Equal(4, outputs["Dpl"][4]);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerVolumeMeanControlsBothScoreAndDemandPrice()
    {
        var bars = Enumerable.Range(0, 10).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 2, i, i + 1, i % 4 + 1)).ToArray();
        var indicator = new RelativeVolumeIndicator(3, new ConstantAverage(0));
        var expected = BuiltInFormulaReferences.RelativeVolumeOutputs(bars, (IBuiltInIndicator)indicator, bars.Select(_ => 0d).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        for (var slot = 0; slot < 2; slot++) Assert.Equal(expected[slot == 0 ? "Rvi" : "Dpl"], run[indicator.Outputs[slot]].ToArray());
        var builtIn = (IBuiltInIndicator)indicator;
        foreach (var key in expected.Keys)
        {
            using var armed = ComponentAverage.Arm((input, _) => input.Select(_ => 0d).ToArray());
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(raw); Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(expected[key], raw.Value.ToArray());
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 1))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => new RelativeVolumeIndicatorState(MovingAvgType.WeightedMovingAverage, 3);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "RelativeVolumeIndicator" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RelativeVolumeOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);

    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);

    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
