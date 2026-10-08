using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class McGinleyNumericalTests
{
    private static Bar B(double value, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("HF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(McGinleyDynamic)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRationalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.McGinleyOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourceRetainsTheFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static StockData Check(double[] prices, int length = 10, double factor = .6)
    {
        var bars = prices.Select((v, i) => B(v, i)).ToArray(); var expected = BuiltInFormulaReferences.McGinleyValues(bars, length, factor)["Mdi"];
        var data = Data(bars).CalculateMcGinleyDynamicIndicator(length, factor); Assert.Equal(expected, data.OutputValues["Mdi"]);
        var state = new McGinleyDynamicIndicatorState(length, factor);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
        if (factor == .6) { using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeMcGinleyDynamicFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray()); }
        return data;
    }
    [Fact]
    public void IndependentSeedZeroResetAndConvexStepHands()
    {
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy }, Check(new[] { 1d, 2, 2, 2 }).SignalsList);
        Assert.Equal(new[] { -8d, 0, 4 }, Check(new[] { -8d, 8, 4 }, 2, 1).OutputValues["Mdi"]);
        Assert.Equal(new[] { 0d, 10, 0, -5 }, Check(new[] { 0d, 10, 0, -5 }).OutputValues["Mdi"]);
        foreach (var factor in new[] { 0d, -1, -double.MaxValue }) Assert.Equal(new[] { 1d, -2, 3 }, Check(new[] { 1d, -2, 3 }, factor: factor).OutputValues["Mdi"]);
    }
    [Theory, InlineData(1), InlineData(-1)]
    public void ExtremeDifferencesFourthPowersAndSignalsRemainFinite(int sign)
    {
        var m = sign * double.MaxValue; var data = Check(new[] { -m, m, 0, m / 2, -m / 4, m });
        Assert.All(data.OutputValues["Mdi"], v => Assert.True(double.IsFinite(v)));
        Assert.Equal(sign > 0 ? Signal.StrongBuy : Signal.StrongSell, data.SignalsList[1]);
        foreach (var factor in new[] { double.Epsilon, double.MaxValue }) Check(new[] { -m, m, m / 2, 0 }, int.MaxValue, factor);
    }
    [Fact]
    public void SubnormalPricesRetainSeedsAndRecover()
    {
        var e = double.Epsilon; var data = Check(new[] { e, 2 * e, -e, 0, 10, e });
        Assert.Equal(e, data.OutputValues["Mdi"][0]); Assert.Equal(10, data.OutputValues["Mdi"][4]);
    }
    [Theory, InlineData(int.MaxValue), InlineData(int.MaxValue - 1)]
    public void HugePeriodsRemainFinite(int length) => Check(new[] { 1d, 2, -3, 0, 4 }, length);
    [Fact]
    public void PreviewAndResetPreserveFeedback()
    {
        var state = new McGinleyDynamicIndicatorState(3); var control = new McGinleyDynamicIndicatorState(3);
        foreach (var value in new[] { 1d, 2, 100, 4, -20, 8 })
        {
            state.Update(Native(B(double.MaxValue)), false, true); state.Update(Native(B(-double.MaxValue)), false, false);
            Assert.Equal(control.Update(Native(B(value)), true, true).Value, state.Update(Native(B(value)), true, true).Value);
        }
        state.Reset(); Assert.Equal(9, state.Update(Native(B(9)), true, true).Value);
    }
    [Fact]
    public void InvalidParametersAndCandlesAreRejectedWithoutAdvancing()
    {
        var state = new McGinleyDynamicIndicatorState(3); var control = new McGinleyDynamicIndicatorState(3); using var context = new ComputeContext();
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new McGinleyDynamicIndicatorState(3, bad));
            Assert.ThrowsAny<ArgumentException>(() => Data(Array.Empty<Bar>()).CalculateMcGinleyDynamicIndicator(3, bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var v = new[] { 1d, 1, 1, 1, 1 }; v[field] = bad; var bar = new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4]);
                Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), final, true));
                Assert.ThrowsAny<ArgumentException>(() => Data(new[] { bar }).CalculateMcGinleyDynamicIndicator());
                Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeMcGinleyDynamicFast(Data(new[] { bar }), context));
            }
        }
        Assert.Equal(control.Update(Native(B(7)), true, true).Value, state.Update(Native(B(7)), true, true).Value);
    }
    [Fact]
    public void DirectSelectedInputAndFixedRecurrenceBypassCallbacks()
    {
        var bars = new[] { 100d, 101, 102, 99 }.Select((v, i) => B(v, i)).ToArray(); var selected = new[] { 1d, -3, 20, 2 };
        var expected = BuiltInFormulaReferences.McGinleyValues(selected.Select((v, i) => B(v, i)).ToArray(), 3)["Mdi"];
        Assert.False(expected.SequenceEqual(BuiltInFormulaReferences.McGinleyValues(bars, 3)["Mdi"]));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using (ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Fixed recurrence")))
        {
            using var fast = IndicatorCompute.ComputeMcGinleyDynamicFast(data, context, 3); Assert.Equal(expected, fast.ToArray());
            data.CalculateMcGinleyDynamicIndicator(3); Assert.Equal(expected, data.OutputValues["Mdi"]); Assert.Equal(0, ComponentAverage.Requests);
        }
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
}
