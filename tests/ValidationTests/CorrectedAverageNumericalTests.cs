using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CorrectedAverageNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(CorrectedMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStagedCorrection(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.CorrectedAverageOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static double[] Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int referenceKind = 1)
    {
        var expected = BuiltInFormulaReferences.CorrectedAverageValues(bars, length, referenceKind); var batch = Data(bars).CalculateCorrectedMovingAverage(kind, length);
        Assert.Equal(expected.Outputs["Cma"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Cma"], batch.OutputValues["Cma"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeCorrectedMovingAverageFast(Data(bars), context, length, kind); Assert.Equal(expected.Outputs["Cma"], fast.ToArray());
        if (kind == MovingAvgType.SimpleMovingAverage) { var core = new double[bars.Length + 1]; core[^1] = 97; MovingAverageCore.CorrectedMovingAverage(bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected.Outputs["Cma"], core.Take(bars.Length)); Assert.Equal(97, core[^1]); }
        using var state = new CorrectedMovingAverageState(kind, length); using var window = new CorrectedAverageWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 1d, 4, -2, 7, 3, -8, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -97d })[0]), false, false); window.Next(-97, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Cma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Cma"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected.Outputs["Cma"];
    }
    [Fact]
    public void WideMomentsDisplacementsAndRoundedStagesMatchIndependentFractions()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 8 }) foreach (var kind in Kinds)
            Check(Bars(Enumerable.Range(0, 19).Select(i => (i % 9 - 4) * scale)), 3, kind.Kind, kind.Reference);
        foreach (var kind in Kinds) Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 0, 0, 0, 1, -2, 3, 4, 5 }), 2, kind.Kind, kind.Reference);
        Check(Bars(new[] { 1d, Math.BitIncrement(1), 1, Math.BitIncrement(Math.BitIncrement(1)), 1, 1, 1 }), 2);
    }
    [Fact]
    public void HandWarmupFlatVarianceAndZeroGainStayExact()
    {
        Assert.Equal(new[] { 0d, 1.5, 2.25, 3.3 }, Check(Bars(new[] { 1d, 2, 3, 4 }), 2));
        Assert.Equal(new[] { 0d, 5, 5, 5 }, Check(Bars(Enumerable.Repeat(5d, 4)), 2));
        Assert.Equal(new[] { 1d, -2, 3 }, Check(Bars(new[] { 1d, -2, 3 }), 1));
        var boundary = new[] { 1d, 3 }.Concat(Enumerable.Range(0, 12).Select(i => i % 2 == 0 ? 2d : 4d)).ToArray();
        Assert.Equal(new[] { 0d }.Concat(Enumerable.Repeat(2d, boundary.Length - 1)), Check(Bars(boundary), 2));
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), length, kind.Kind, kind.Reference); Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 }), length, kind.Kind, kind.Reference); }
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Custom(Bar[] bars, double[] means, int length)
    {
        var expected = BuiltInFormulaReferences.CorrectedAverageValues(bars, length, 1, means); var prices = bars.Select(b => b.Close).ToArray();
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; using var armed = ComponentAverage.Arm((values, period) => { calls++; Assert.Equal(Math.Max(1, length), period); Assert.Equal(prices, values); return means; }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeCorrectedMovingAverageFast(Data(bars), context, length); Assert.Equal(expected.Outputs["Cma"], output.ToArray()); }
            else { var batch = Data(bars).CalculateCorrectedMovingAverage(length: length); Assert.Equal(expected.Outputs["Cma"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); }
            Assert.Equal(1, calls); Assert.Equal(1, ComponentAverage.Substitutions);
        }
        using var window = new CorrectedAverageWindow(MovingAvgType.SimpleMovingAverage, length, true);
        for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true }) { var point = window.Next(bars[i].Close, final, means[i]); Assert.Equal(expected.Outputs["Cma"][i], point.Value); Assert.Equal(expected.Signals[i], point.Signal); }
        return expected;
    }
    [Fact]
    public void CustomMeansPreserveInitialResidualAndWideCorrections()
    {
        Assert.Equal(Signal.Sell, Custom(Bars(new[] { 1d }), new[] { 2d }, 3).Signals[0]);
        Assert.Equal(Signal.Buy, Custom(Bars(new[] { -1d }), new[] { -2d }, 3).Signals[0]);
        Custom(Bars(new[] { 1d, 2, 4, 3, 8, -2 }), new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue, 1 }, 2);
        Custom(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, 0, 0 }), new[] { 0d, 0, 1, 2, 1, 3 }, 2);
        Custom(Bars(new[] { 1d, 2, 3, 4, 5, 6 }), new[] { 0d, 1, 1.5, 4, 4.000000000000001, 1 }, 2);
    }
    [Fact]
    public void SelectedPricesAndLegacyMeansPreserveAllRoutes()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected);
        foreach (var fast in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.CorrectedAverageValues(effective, 3, 2); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeCorrectedMovingAverageFast(data, context, 3, MovingAvgType.WeightedMovingAverage); Assert.Equal(expected.Outputs["Cma"], output.ToArray()); }
            else { data.CalculateCorrectedMovingAverage(MovingAvgType.WeightedMovingAverage, 3); Assert.Equal(expected.Outputs["Cma"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var batch = Data(effective).CalculateCorrectedMovingAverage(kind, 3); using var context2 = new ComputeContext(); using var output2 = IndicatorCompute.ComputeCorrectedMovingAverageFast(Data(effective), context2, 3, kind); Assert.Equal(batch.CustomValuesList, output2.ToArray());
        using var state = new CorrectedMovingAverageState(kind, 3); for (var i = 0; i < effective.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(effective[i]), true, true).Value);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMomentsOrCorrection()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new CorrectedMovingAverageState(length: 3); using var control = new CorrectedMovingAverageState(length: 3);
            foreach (var b in Bars(new[] { 1d, 4, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Bars(new[] { -3d, 2, 7, 0 })) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
        Assert.Throws<ArgumentException>(() => MovingAverageCore.CorrectedMovingAverage(new[] { 1d }, Array.Empty<double>(), 3));
    }
}
