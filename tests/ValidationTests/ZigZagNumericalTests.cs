using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ZigZagNumericalTests
{
    private static Bar B(double value, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ZigZag)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void PublicRoutesMatchIndependentLegSearch(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ZigZagOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourceUsesResolvedRangesInWholeSeries(IndicatorValidationCase c)
    {
        var source = new Sma(2); var indicator = ((IndicatorBase)c.Factory()).Of(source);
        var bars = new[] { 10d, 20, 5, 30, 10 }.Select((v, i) => B(v, i)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray(); var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.ZigZagOutputs(projected, (IBuiltInIndicator)indicator)["ZigZag"];
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        var deviation = (double)((IBuiltInIndicator)c.Factory()).CreateOptions().GetType().GetProperty("Deviation")!.GetValue(((IBuiltInIndicator)c.Factory()).CreateOptions())!;
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeZigZagFast(data, context, deviation);
        Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateZigZag(deviation); Assert.Equal(expected, data.OutputValues["ZigZag"]); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    private static double[] Check(Bar[] bars, double deviation = 5)
    {
        var expected = BuiltInFormulaReferences.ZigZagValues(bars, deviation)["ZigZag"];
        var data = Data(bars).CalculateZigZag(deviation); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeZigZagFast(Data(bars), context, deviation);
        Assert.Equal(expected, data.OutputValues["ZigZag"]); Assert.Equal(expected, fast.ToArray()); Assert.All(expected, v => Assert.True(double.IsFinite(v)));
        Assert.Equal(BuiltInFormulaReferences.ZigZagDetails(bars, deviation).Signals, data.SignalsList);
        return data.OutputValues["ZigZag"].ToArray();
    }
    [Fact]
    public void IndependentExtremaInterpolationAndTailHands()
    {
        var prices = new[] { 10d, 12, 14, 13, 10, 8, 9, 11 };
        Assert.Equal(new[] { 10d, 12, 14, 12, 10, 8, 9.5, 11 }, Check(prices.Select((v, i) => B(v, i)).ToArray(), 10));
        Assert.Equal(new[] { 10d, 12, 12 }, Check(new[] { B(10), B(12), B(11) }, 10));
        Assert.Equal(new[] { 10d, 34d / 3, 38d / 3, 14 }, Check(new[] { B(10), B(12), B(11), B(14) }, 10));
        Assert.Equal(new[] { -20d, -20 }, Check(new[] { B(-20), B(-22) }, 10));
    }
    [Fact]
    public void ThresholdEqualityAndAdjacentPricesHaveDifferentPivots()
    {
        Assert.Equal(new[] { 20d, 20 }, Check(new[] { B(20), B(18) }, 10));
        var lower = Math.BitDecrement(18d); Assert.Equal(new[] { 20d, lower }, Check(new[] { B(20), B(lower) }, 10));
        var negative = Math.BitDecrement(-22d); Assert.Equal(new[] { -20d, negative }, Check(new[] { B(-20), B(negative) }, 10));
        Assert.Equal(new[] { 20d, 10, 11 }, Check(new[] { B(20), B(10), B(11) }, 0));
    }
    [Fact]
    public void UnpublishedHalfSubnormalSeedChangesTheInterpolatedNeighbor()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, 0, 1), B(0), B(double.Epsilon) };
        Assert.Equal(new[] { 0d, double.Epsilon, double.Epsilon }, Check(bars, 100));
        Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.Buy }, Data(bars).CalculateZigZag(100).SignalsList);
    }
    [Fact]
    public void ExtremeThresholdsInterpolationAndSlopesRemainFinite()
    {
        var max = double.MaxValue;
        Assert.Equal(new[] { max, 0, -max }, Check(new[] { B(max), B(0), B(-max) }, 10));
        Assert.Equal(new[] { max, -max }, Check(new[] { B(max), B(-max) }, 150));
        Assert.Equal(new[] { max, max, max }, Check(new[] { B(max), B(0), B(-max) }, double.MaxValue));
        Check(new[] { B(-max), B(max), B(-max), B(0), B(max) }, 0);
    }
    [Fact]
    public void DirectSelectedRangesAreDiscriminating()
    {
        var bars = Enumerable.Range(0, 6).Select(i => B(100, i)).ToArray(); var selected = new[] { -10d, 10, -5, 8, 3, -9 };
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.ZigZagValues(projected, 5, true)["ZigZag"]; Assert.False(expected.All(v => v == 100));
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeZigZagFast(data, context);
        Assert.Equal(expected, fast.ToArray()); data.CalculateZigZag(); Assert.Equal(expected, data.OutputValues["ZigZag"]);
    }
    [Fact]
    public void HelperSpansAreAliasSafeAndValidationIsAtomic()
    {
        var prices = new[] { 10d, 12, 14, 13, 8, 11 }; var expected = Check(prices.Select((v, i) => B(v, i)).ToArray(), 10);
        var output = new double[prices.Length + 1]; prices.CopyTo(output, 0); output[^1] = 777;
        ZigZagPath.Compute(output.AsSpan(0, prices.Length), prices, output.AsSpan(1), 10); Assert.Equal(expected, output.Skip(1)); Assert.Equal(10, output[0]);
        var guard = new[] { 12d, 34 }; Assert.ThrowsAny<ArgumentException>(() => ZigZagPath.Compute(new[] { 1d, double.NaN }, new[] { 1d, 2 }, guard, 5)); Assert.Equal(new[] { 12d, 34 }, guard);
        Assert.Throws<ArgumentException>(() => ZigZagPath.Compute(new[] { 1d }, Array.Empty<double>(), guard, 5));
        Assert.Throws<ArgumentException>(() => ZigZagPath.Compute(new[] { 1d }, new[] { 1d }, Array.Empty<double>(), 5));
        ZigZagPath.Compute(Array.Empty<double>(), Array.Empty<double>(), guard, 5); Assert.Equal(new[] { 12d, 34 }, guard);
    }
    [Fact]
    public void InvalidDeviationAndCandlesAreRejectedEvenForEmptyInput()
    {
        using var context = new ComputeContext();
        foreach (var deviation in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => Data(Array.Empty<Bar>()).CalculateZigZag(deviation));
            Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeZigZagFast(Data(Array.Empty<Bar>()), context, deviation));
        }
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5))
        {
            var values = new[] { 1d, 1, 1, 1, 1 }; values[field] = bad; var bars = new[] { new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]) };
            Assert.ThrowsAny<ArgumentException>(() => Data(bars).CalculateZigZag()); Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeZigZagFast(Data(bars), context));
        }
    }
}
