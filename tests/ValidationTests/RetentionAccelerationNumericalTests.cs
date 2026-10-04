using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RetentionAccelerationNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RetentionAccelerationFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    private static Bar B(double p, double h, double l) => new(DateTime.UnixEpoch, p, h, l, p, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("RAF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCancellation(IndicatorValidationCase c, string route)
    {
        var o = (RetentionAccelerationFilterSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { ["Raf"] = BuiltInFormulaReferences.RetentionValues(bars, o.Length) }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesRetainOriginalRanges(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public void PublishedOutputsRejectFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    private static double[] Check(Bar[] bars, int length)
    {
        var expected = BuiltInFormulaReferences.RetentionValues(bars, length);
        Assert.Equal(expected, Data(bars).CalculateRetentionAccelerationFilter(length).OutputValues["Raf"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeRetentionAccelerationFilterFast(Data(bars), context, length);
        Assert.Equal(expected, fast.ToArray());
        using var state = new RetentionAccelerationFilterState(length);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, 101, 98)), false, false);
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Outputs!["Raf"]);
            }
        }
        return expected;
    }
    [Fact]
    public void NestedRangesHaveIndependentQuarterFactorHand()
    {
        var bars = new[] { B(0, 16, 0) }.Concat(Enumerable.Repeat(B(4, 4, 2), 8)).ToArray();
        var actual = Check(bars, 4);
        Assert.Equal(new[] { 0d, 0, 0, 0, 1d / 16, 127d / 1024, 12097d / 65536, 1024255d / 4194304, 1024255d / 4194304 }, actual);
        Assert.Equal(new[] { 0d, 4 }, Check(new[] { B(0, 16, 0), B(4, 4, -16) }, 1));
    }
    [Fact]
    public void SingularAndExtremeRangesRemainFinite()
    {
        foreach (var length in new[] { 0, 1, 2, 4, int.MaxValue })
        foreach (var bars in new[] {
            new[] { B(double.MaxValue, double.MaxValue, -double.MaxValue), B(-double.MaxValue, 4, 2), B(double.Epsilon, 4, 2), B(0, 4, 2), B(1, 4, 2) },
            new[] { B(1, 1, 1), B(2, 1, .5), B(3, -1, -2), B(4, 2 * double.Epsilon, double.Epsilon) } })
            Assert.All(Check(bars, length), v => Assert.True(double.IsFinite(v)));
        Assert.Empty(Check([], 4));
    }
    [Fact]
    public void ExactSingularRangesFreezeThePreviousOutput()
    {
        Assert.Equal(new[] { 7d, 7 }, Check(new[] { B(7, 16, 0), B(4, 4, 3.5) }, 1));
        Assert.Equal(new[] { 7d, 7 }, Check(new[] { B(7, 1, .5), B(4, 1, .875) }, 1));
        Assert.Equal(new[] { 7d, 7 }, Check(new[] { B(7, 16, 0), B(4, 4, 4) }, 1));
        Assert.Equal(new[] { 7d, 7 }, Check(new[] { B(7, 16, 0), B(4, 16, 0) }, 1));
        Assert.Equal(new[] { 7d, 7 }, Check(new[] { B(7, 16, -16), B(4, 0, -4) }, 1));
    }
    [Fact]
    public void PublishedResidualSignalsRetainOverflowingDifferences()
    {
        var bars = new[] { B(-double.MaxValue, 16, 0), B(double.MaxValue, 16, 0), B(0, 16, 0), B(-double.MaxValue, 16, 0) };
        var result = Data(bars).CalculateRetentionAccelerationFilter(4);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.None }, result.SignalsList);
        var ordinary = new[] { B(0, 16, 0) }.Concat(Enumerable.Repeat(B(4, 4, 2), 8)).ToArray();
        result = Data(ordinary).CalculateRetentionAccelerationFilter(4);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy, Signal.Buy }, result.SignalsList);
    }
    [Fact]
    public void RejectionPreservesCommittedWindows()
    {
        using var a = new RetentionAccelerationFilterState(2); using var b = new RetentionAccelerationFilterState(2);
        a.Update(Native(B(0, 16, 0)), true, false); b.Update(Native(B(0, 16, 0)), true, false);
        foreach (var bad in new[] { B(double.NaN, 4, 2), B(4, double.PositiveInfinity, 2), B(4, 4, double.NaN) })
        foreach (var final in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => a.Update(Native(bad), final, false));
        foreach (var bar in Enumerable.Repeat(B(4, 4, 2), 5)) Assert.Equal(b.Update(Native(bar), true, false).Value, a.Update(Native(bar), true, false).Value);
    }
}
