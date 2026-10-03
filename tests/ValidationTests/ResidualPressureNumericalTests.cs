using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ResidualPressureNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void AllOutputsKeepWideResidualsExactPressureAndDistinctPeriods()
    {
        var prices = Enumerable.Repeat(-double.MaxValue, 24).Concat(new[] { double.MaxValue, 0d, double.Epsilon, -double.Epsilon, 2d, 7, 4, 3, 8, 1 }).ToArray();
        foreach (var bars in new[] { BarsOf(prices), BarsOf(prices).Select(b => new Bar(b.Time, 0, double.MaxValue, -double.MaxValue, b.Close, 1)).ToArray(),
            BarsOf(prices).Select((b, i) => new Bar(b.Time, 0, 10 + i % 5, -2 + i % 3, b.Close, 1)).ToArray() })
        foreach (var indicator in new IBuiltInIndicator[] { new EmaWaveIndicator(7, 11, 50, 4), new ErgodicMeanDeviationIndicator(50, 1, 1, 7), new ErgodicMeanDeviationIndicator(7, 3, 5, 2), new TraderPressureIndex(3, 2, 4) })
        {
            var expected = BuiltInFormulaReferences.ResidualPressureOutputs(bars, indicator); var options = indicator.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new IndicatorSpec(indicator.BatchName, options, pair.Key);
                Assert.Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), spec, context); Assert.NotNull(raw); Assert.Equal(pair.Value, raw.Value.ToArray());
                using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                var selected = Data(BarsOf(Enumerable.Repeat(42d, prices.Length).ToArray())); selected.CustomValuesList = prices.ToList();
                if (indicator.BatchName != IndicatorName.TraderPressureIndex)
                {
                    using var selectedRaw = IndicatorCompute.TryComputeFast(selected, spec, context); Assert.NotNull(selectedRaw);
                    Assert.Equal(BuiltInFormulaReferences.ResidualPressureOutputs(BarsOf(prices), indicator)[pair.Key], selectedRaw.Value.ToArray());
                    using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm);
                    Assert.Equal(BuiltInFormulaReferences.ResidualPressureOutputs(BarsOf(prices), indicator)[pair.Key], selectedArm.Value.ToArray());
                }
            }
            var primary = new IndicatorSpec(indicator.BatchName, options);
            var native = StatefulIndicatorFactory.Create(primary); var live = StreamingIndicatorFactory.CreateState(primary); Assert.NotNull(live);
            using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
            foreach (var state in new[] { native, live! })
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { 100d })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
                    }
                }
            }
        }
        var overflowCase = BuiltInFormulaReferences.ResidualPressureOutputs(BarsOf(prices), new ErgodicMeanDeviationIndicator(50, 1, 1, 7));
        Assert.True(double.IsPositiveInfinity(overflowCase["Emdi"][24])); Assert.True(double.IsFinite(overflowCase["Signal"][24]));
        Assert.Equal(100, TraderPressureWindow.Pressure(double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, true));
        Assert.Equal(100, TraderPressureWindow.Pressure(double.Epsilon, 0, 0, 0, double.Epsilon, 0, true));
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task EveryCustomerAverageStageControlsItsPublishedOutputs()
    {
        var bars = BarsOf(new[] { 1d, 3, 2, 4, 0, 6 });
        foreach (var pressure in new[] { false, true })
        {
            IIndicator indicator = pressure ? new TraderPressureIndex(3, 2, 4, new ConstantAverage(2), new ConstantAverage(4), new ConstantAverage(6))
                : new ErgodicMeanDeviationIndicator(3, 2, 4, 5, new ConstantAverage(2), new ConstantAverage(4), new ConstantAverage(6), new ConstantAverage(8));
            var expected = pressure ? new Dictionary<string, double> { { "Tpx", 6 }, { "Bulls", 2 }, { "Bears", 4 } } : new Dictionary<string, double> { { "Emdi", 6 }, { "Signal", 8 } };
            using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            var builtIn = (IBuiltInIndicator)indicator; var slot = 0;
            foreach (var pair in expected)
            {
                Assert.All(result[indicator.Outputs[slot++]].ToArray(), value => Assert.Equal(pair.Value, value));
                var values = pressure ? new[] { 2d, 4, 6 } : new[] { 2d, 4, 6, 8 };
                var averages = values.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) => input.Select(_ => v).ToArray())).ToArray();
                using var armed = ComponentAverage.Arm(averages); using var context = new ComputeContext();
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), pair.Key), context);
                Assert.NotNull(raw); Assert.Equal(values.Length, ComponentAverage.Substitutions);
                Assert.All(raw.Value.ToArray(), value => Assert.Equal(pair.Value, value));
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var variant in Enumerable.Range(0, 3))
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant switch { 0 => new EmaWaveIndicatorState(2, 3, 4, 2), 1 => new ErgodicMeanDeviationIndicatorState(length1: 3, length2: 2, length3: 4, signalLength: 2), _ => new TraderPressureIndexState(length1: 3, length2: 2, smoothLength: 4) };
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "EmaWaveIndicator", "ErgodicMeanDeviationIndicator", "TraderPressureIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ResidualPressureOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
