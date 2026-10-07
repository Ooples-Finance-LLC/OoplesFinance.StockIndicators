using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveStochasticNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AdaptiveStochastic)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRegressionRanges(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveStochasticOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static double[] Check(Bar[] bars, int length = 3, int fast = 2, int slow = 5)
    {
        var expected = BuiltInFormulaReferences.AdaptiveStochasticValues(bars, length, fast, slow);
        var batch = Data(bars).CalculateAdaptiveStochastic(length, fast, slow); Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Ast"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Ast"], batch.OutputValues["Ast"]);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeAdaptiveStochasticFast(Data(bars), context, fast, slow, length); Assert.Equal(expected.Outputs["Ast"], output.ToArray());
        using var state = new AdaptiveStochasticState(length, fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -4, 7, 1, -2, 8 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Ast"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ast"]); } }
        }
        return expected.Outputs["Ast"];
    }

    [Fact]
    public void WideRegressionEfficiencyAndEveryRangeStayFinite()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 8 })
            foreach (var periods in new[] { (3, 2, 5), (2, 6, 2), (4, 3, 3), (1, 1, 4) })
                Assert.All(Check(Bars(Enumerable.Range(0, 25).Select(i => (i % 7 - 3) * scale)), periods.Item1, periods.Item2, periods.Item3), v => Assert.InRange(v, 0, 1));
    }
    [Fact]
    public void HandSeedsFlatWindowsAndEfficiencyExtremesArePreserved()
    {
        Assert.Equal(new[] { 0d, 1, 1, 0, 0 }, Check(Bars(new[] { 0d, 1, 2, 1, 0 }), 1, 2, 4));
        Assert.Equal(new double[6], Check(Bars(Enumerable.Repeat(3d, 6)), 1, 2, 4));
        Check(Bars(new[] { 0d, 8, 8, 8, 2, 2, 2, 9, 9, 9, 1 }), 3, 1, 5);
        Check(Bars(new[] { 0d, 8, 8, 8, 8, 8, 8, 2, 2, 2, 2, 2, 2 }), 2, 1, 5);
    }
    [Fact]
    public void OverflowingFittedEndpointsRecoverAfterRangeExpiry()
    {
        var max = double.MaxValue;
        var values = Check(Bars(new[] { -max, max, max, max, -max, -max }.Concat(Enumerable.Repeat(0d, 12))), 50, 1, 4);
        Assert.Equal(1, values[2]); Assert.Equal(0, values[^1]); Assert.All(values, v => Assert.InRange(v, 0, 1));
        Check(Bars(new[] { -max, max, -max, max, 0, 0, 0, max, -max }), 2, 2, 5);
    }
    [Fact]
    public void EveryPeriodGrowsHistoryOnlyWithObservedPrices()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in Enumerable.Range(0, 3))
        { var lengths = new[] { 3, 2, 5 }; lengths[position] = period; foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }) }) Check(bars, lengths[0], lengths[1], lengths[2]); }
    }
    [Fact]
    public void SelectedPricesFeedBothComponentsWithoutAverageCallbacks()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8, 10, 12 }); var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var expected = BuiltInFormulaReferences.AdaptiveStochasticValues(Bars(selected), 2, 2, 5);
        using var context = new ComputeContext();
        foreach (var fast in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("No moving-average component is part of Adaptive Stochastic."));
            if (fast) { using var output = IndicatorCompute.ComputeAdaptiveStochasticFast(data, context, 2, 5, 2); Assert.Equal(expected.Outputs["Ast"], output.ToArray()); }
            else { data.CalculateAdaptiveStochastic(2, 2, 5); Assert.Equal(expected.Outputs["Ast"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRegressionOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AdaptiveStochasticState(2, 2, 5); using var control = new AdaptiveStochasticState(2, 2, 5);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
