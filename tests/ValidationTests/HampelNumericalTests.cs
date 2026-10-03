using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HampelNumericalTests
{
    private static Bar B(double value, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("HF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(HampelFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRationalReference(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HampelOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourceRetainsTheFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static StockData Check(double[] values, int length = 3, double factor = 3)
    {
        var bars = values.Select((v, i) => B(v, i)).ToArray(); var expected = BuiltInFormulaReferences.HampelValues(bars, length, factor)["Hf"];
        var data = Data(bars).CalculateHampelFilter(length, factor);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeHampelFilterFast(Data(bars), context, length, factor);
        using var state = new HampelFilterState(length, factor);
        Assert.Equal(expected, data.OutputValues["Hf"]); Assert.Equal(expected, fast.ToArray());
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
        return data;
    }
    [Fact]
    public void IndependentOutlierWarmupAndSignalHands()
    {
        var data = Check(new[] { 0d, 2, 100 }); Assert.Equal(new[] { 0d, 1, 1.5 }, data.OutputValues["Hf"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongBuy }, data.SignalsList);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy }, Check(new[] { 2d, 2, 2 }).SignalsList);
        var tiny = Check(new[] { double.Epsilon, 0 }, 2, 0);
        Assert.Equal(new[] { double.Epsilon, double.Epsilon }, tiny.OutputValues["Hf"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell }, tiny.SignalsList);
    }
    [Fact]
    public void ExactThresholdEqualityDiffersFromAdjacentFactor()
    {
        Assert.Equal(1.25, Check(new[] { 0d, 1, 2 }, factor: 1).OutputValues["Hf"][2]);
        Assert.Equal(.625, Check(new[] { 0d, 1, 2 }, factor: Math.BitDecrement(1)).OutputValues["Hf"][2]);
    }
    [Theory, InlineData(1), InlineData(-1)]
    public void ExtremeMediansDeviationsAndSignalsStayFinite(int sign)
    {
        var m = sign * double.MaxValue; var data = Check(new[] { -m, m, -m, m, 0 });
        Assert.Equal(m / 4, data.OutputValues["Hf"][1]); Assert.All(data.OutputValues["Hf"], v => Assert.True(double.IsFinite(v)));
    }
    [Theory, InlineData(int.MaxValue), InlineData(int.MaxValue - 1)]
    public void HugePeriodsAllocateOnlyObservedHistory(int length)
        => Check(new[] { 1d, 5, -2, 4 }, length);
    [Fact]
    public void PreviewExpiryAndResetPreserveCommittedHistory()
    {
        using var state = new HampelFilterState(3); using var control = new HampelFilterState(3);
        foreach (var value in new[] { 1d, 2, 100, 4, -20, 8 })
        {
            state.Update(Native(B(double.MaxValue)), false, true); state.Update(Native(B(-double.MaxValue)), false, false);
            Assert.Equal(control.Update(Native(B(value)), true, true).Value, state.Update(Native(B(value)), true, true).Value);
        }
        state.Reset(); using var fresh = new HampelFilterState(3);
        Assert.Equal(fresh.Update(Native(B(9)), true, true).Value, state.Update(Native(B(9)), true, true).Value);
    }
    [Fact]
    public void InvalidParametersAndCandlesAreRejectedWithoutAdvancing()
    {
        using var state = new HampelFilterState(3); using var control = new HampelFilterState(3); using var context = new ComputeContext();
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new HampelFilterState(3, bad));
            Assert.ThrowsAny<ArgumentException>(() => Data(Array.Empty<Bar>()).CalculateHampelFilter(3, bad));
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeHampelFilterFast(Data(Array.Empty<Bar>()), context, 3, bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var v = new[] { 1d, 1, 1, 1, 1 }; v[field] = bad; var bar = new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4]);
                Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), final, true));
                Assert.ThrowsAny<ArgumentException>(() => Data(new[] { bar }).CalculateHampelFilter());
                Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeHampelFilterFast(Data(new[] { bar }), context));
            }
        }
        Assert.Equal(control.Update(Native(B(7)), true, true).Value, state.Update(Native(B(7)), true, true).Value);
    }
    [Fact]
    public void DirectSelectedInputAndCallbackBypassAreDiscriminating()
    {
        var bars = new[] { 100d, 101, 102, 99 }.Select((v, i) => B(v, i)).ToArray(); var selected = new[] { 1d, -3, 20, 2 };
        var expected = BuiltInFormulaReferences.HampelValues(selected.Select((v, i) => B(v, i)).ToArray(), 3)["Hf"];
        Assert.False(expected.SequenceEqual(BuiltInFormulaReferences.HampelValues(bars, 3)["Hf"]));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using (ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Hampel has a fixed EMA")))
        {
            using var fast = IndicatorCompute.ComputeHampelFilterFast(data, context, 3); Assert.Equal(expected, fast.ToArray());
            data.CalculateHampelFilter(3); Assert.Equal(expected, data.OutputValues["Hf"]); Assert.Equal(0, ComponentAverage.Requests);
        }
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
}
