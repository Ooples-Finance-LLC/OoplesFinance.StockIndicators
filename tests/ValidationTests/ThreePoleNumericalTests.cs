using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ThreePoleNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    private static IBuiltInIndicator Indicator(int variant, int length) => variant switch
    {
        0 => new Ehlers3PoleButterworthFilterV1(length), 1 => new Ehlers3PoleButterworthFilterV2(length),
        _ => new Ehlers3PoleSuperSmootherFilter(length)
    };
    private static IStreamingIndicatorState State(int variant, int length) => variant switch
    {
        0 => new Ehlers3PoleButterworthFilterV1State(length), 1 => new Ehlers3PoleButterworthFilterV2State(length),
        _ => new Ehlers3PoleSuperSmootherFilterState(length)
    };
    [Fact]
    public void PoleRecurrencesPreserveExtendedRangeAndPreviewReset()
    {
        foreach (var variant in Enumerable.Range(0, 3))
        foreach (var length in new[] { 1, 2, 3, 7, 100, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, -4, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0 },
            new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 80).ToArray() })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.ThreePoleOutputs(bars, Indicator(variant, length)).Values.Single();
            using var context = new ComputeContext(); var data = Data(bars);
            using var raw = variant switch
            {
                0 => IndicatorCompute.ComputeEhlers3PoleButterworthFilterV1Fast(data, context, length),
                1 => IndicatorCompute.ComputeEhlers3PoleButterworthFilterV2Fast(data, context, length),
                _ => IndicatorCompute.ComputeEhlers3PoleSuperSmootherFilterFast(data, context, length)
            };
            Assert.Equal(expected, raw.Span.ToArray());
            var kind = variant switch { 0 => MovingAvgType.Ehlers3PoleButterworthFilterV1, 1 => MovingAvgType.Ehlers3PoleButterworthFilterV2, _ => MovingAvgType.Ehlers3PoleSuperSmootherFilter };
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(kind)!.Compute(prices, core, length); Assert.Equal(expected, core);
            var state = State(variant, length); using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(new[] { 99d, -31, 7 })) state.Update(Native(seed), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }
    [Fact]
    public void RawRoutesUseSelectedInputsAndConfiguredPeriods()
    {
        foreach (var variant in Enumerable.Range(0, 3))
        {
            var selected = new[] { 1d, 3, -2, 4, 7 }; var data = Data(BarsOf(new[] { 20d, 40, 30, 80, 90 })); data.SetCustomValues(selected.ToList());
            var expected = BuiltInFormulaReferences.ThreePoleOutputs(BarsOf(selected), Indicator(variant, 7)).Values.Single();
            using var context = new ComputeContext();
            using var raw = variant switch
            {
                0 => IndicatorCompute.ComputeEhlers3PoleButterworthFilterV1Fast(data, context, 7),
                1 => IndicatorCompute.ComputeEhlers3PoleButterworthFilterV2Fast(data, context, 7),
                _ => IndicatorCompute.ComputeEhlers3PoleSuperSmootherFilterFast(data, context, 7)
            };
            Assert.Equal(expected, raw.Span.ToArray());
        }
    }
    [Fact]
    public void OrdinaryResponsesAgreeWithIndependentComplexPoleSolution()
    {
        foreach (var variant in Enumerable.Range(0, 3))
        foreach (var length in new[] { 2, 7, 20, 100 })
        {
            var prices = Enumerable.Range(0, 100).Select(i => (i * 17 % 19) - 9d).ToArray();
            var expected = BuiltInFormulaReferences.PoleTrajectory(prices, length, 3, 1, variant == 1 ? 3 : 0, variant == 0 ? 0 : 4);
            var actual = BuiltInFormulaReferences.ThreePoleOutputs(BarsOf(prices), Indicator(variant, length)).Values.Single();
            for (var i = 0; i < prices.Length; i++) Assert.InRange(Math.Abs(expected[i] - actual[i]), 0, 1e-8);
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
            var state = State(variant, 3); var control = State(variant, 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
            Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bad), final, true));
            var next = Native(BarsOf(new[] { 5d })[0]); Assert.Equal(control.Update(next, true, true).Value, state.Update(next, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => new[] { typeof(Ehlers3PoleButterworthFilterV1), typeof(Ehlers3PoleButterworthFilterV2), typeof(Ehlers3PoleSuperSmootherFilter) }.Contains(c.IndicatorType)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ThreePoleOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
