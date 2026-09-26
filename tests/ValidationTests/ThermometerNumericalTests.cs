using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ThermometerNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void WideExpansionKeepsFiniteSignalsAndFirstBarConvention()
    {
        var extreme = BarsOf(Enumerable.Repeat(-double.MaxValue, 24).Concat(new[] { double.MaxValue, 0d, 4, 1, 8, 0 }).ToArray());
        extreme[24] = new Bar(extreme[24].Time, 0, double.MaxValue, -double.MaxValue, 0, 1);
        var ordinary = BarsOf(new[] { 2d, 5, 3, 8, 3, 1, 5, 0 }).Select((b, i) => new Bar(b.Time, b.Open, 10 + i % 4, -2 - i % 3, b.Close, 1)).ToArray();
        foreach (var bars in new[] { extreme, ordinary, BarsOf(new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon }) })
        foreach (var period in new[] { 1, 3, 7 })
        foreach (var indicator in new IBuiltInIndicator[] { new ElderMarketThermometer(period), new ElderMarketThermometer(period, new Sma()),
            new ElderMarketThermometer(period, new Wma()), new ElderMarketThermometer(period, new Smma()) })
        {
            var expected = BuiltInFormulaReferences.ThermometerOutputs(bars, indicator); var options = indicator.CreateOptions();
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target)); using var context = new ComputeContext();
            foreach (var pair in expected)
            {
                var spec = new IndicatorSpec(indicator.BatchName, options, pair.Key);
                Assert.Equal(pair.Value, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray());
                using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(pair.Value, arm.Value.ToArray());
                var selected = Data(bars); selected.CustomValuesList = bars.Select(_ => 42d).ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, spec, context); Assert.NotNull(selectedArm); Assert.Equal(pair.Value, selectedArm.Value.ToArray());
                Assert.Equal(pair.Value, BuilderArmBinding.Compute(selected, spec, target).ToArray());
            }
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.ElderMarketThermometer(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core);
            Assert.Equal(expected["Emt"], core);
            var primary = new IndicatorSpec(indicator.BatchName, options);
            foreach (var state in new[] { StatefulIndicatorFactory.Create(primary), StreamingIndicatorFactory.CreateState(primary)! })
            {
                Assert.NotNull(state); using var lifetime = state as IDisposable;
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
        }
        var outputs = BuiltInFormulaReferences.ThermometerOutputs(extreme, new ElderMarketThermometer(7));
        Assert.True(double.IsPositiveInfinity(outputs["Emt"][24])); Assert.True(double.IsFinite(outputs["Signal"][24]));
        Assert.Equal(10, BuiltInFormulaReferences.ThermometerOutputs(ordinary, new ElderMarketThermometer(3))["Emt"][0]);
    }

    private sealed class ConstantAverage(double value) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(value);
        private sealed class State(double value) : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => value; }
    }
    [Fact]
    public async Task CustomerSignalUsesTheSecondAverageForBothOutputRequests()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        var indicator = new ElderMarketThermometer(3, new ConstantAverage(2), new ConstantAverage(7));
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(result[indicator.Outputs[1]].ToArray(), v => Assert.Equal(7, v));
        var builtIn = (IBuiltInIndicator)indicator;
        foreach (var key in new[] { "Emt", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new[] { 2d, 7 }.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, _) => input.Select(_ => v).ToArray())).ToArray());
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(raw); Assert.Equal(2, ComponentAverage.Substitutions);
            if (key == "Signal") Assert.All(raw.Value.ToArray(), v => Assert.Equal(7, v));
            else Assert.Equal(BuiltInFormulaReferences.ThermometerOutputs(bars, new ElderMarketThermometer(3))[key], raw.Value.ToArray());
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
            IStreamingIndicatorState Create() => new ElderMarketThermometerState(MovingAvgType.WeightedMovingAverage, 3);
            var state = Create(); var control = Create(); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "ElderMarketThermometer" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ThermometerOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
