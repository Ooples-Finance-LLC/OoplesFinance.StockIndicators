using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChandeCompositeNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> values) => values.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChandeCompositeMomentumIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWeightedMomentum(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ChandeCompositeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[][] Ratios) Check(Bar[] bars, int first = 2, int second = 3, int third = 5, int smooth = 3, MovingAvgType kind = MovingAvgType.DoubleExponentialMovingAverage, int referenceKind = 4)
    {
        var expected = BuiltInFormulaReferences.ChandeCompositeValues(bars, first, second, third, smooth, referenceKind);
        var data = Data(bars).CalculateChandeCompositeMomentumIndex(kind, first, second, third, smooth);
        Assert.Equal(expected.Outputs["Ccmi"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext();
        foreach (var key in new[] { "Ccmi", "Signal" }) { using var fast = IndicatorCompute.ComputeChandeCompositeMomentumIndexFast(Data(bars), context, first, second, third, kind, smooth, key == "Signal"); Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        using var state = new ChandeCompositeMomentumIndexState(kind, first, second, third, smooth); using var window = new ChandeCompositeWindow(kind, first, second, third, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 13d, -2, 7, -8, 1, 4, -3 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -91d })[0]), false, false); window.Next(-91, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Ccmi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ccmi"]); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Signal); Assert.Equal(expected.Ratios[0][i], direct.Ratio1); Assert.Equal(expected.Ratios[1][i], direct.Ratio2); Assert.Equal(expected.Ratios[2][i], direct.Ratio3);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void WideDifferencesWeightsAndRecursiveStagesMatchRationalScans()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 })
        foreach (var average in new[] { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.DoubleExponentialMovingAverage, 4), (MovingAvgType.WildersSmoothingMethod, 6) })
            Check(Bars(Enumerable.Range(0, 27).Select(i => (i % 9 - 4) * scale)), kind: average.Item1, referenceKind: average.Item2);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue, 0, 1, -2, 3, 0, -1, 2, 4, -3, 0 }));
    }
    [Fact]
    public void HandStartupWeightsAndPartialSignalArePreserved()
    {
        var result = Check(Bars(new[] { 0d, 2, 4, 6 }), 2, 2, 2, 1);
        Assert.Equal(new[] { 0d, 100, 100, 100 }, result.Outputs["Ccmi"]); Assert.Equal(new[] { 0d, 50, 100, 100 }, result.Outputs["Signal"]);
        result = Check(Bars(new[] { 0d, -2, -4, -6 }), 2, 2, 2, 1); Assert.Equal(new[] { 0d, -100, -100, -100 }, result.Outputs["Ccmi"]);
        result = Check(Bars(Enumerable.Repeat(double.MaxValue, 12))); Assert.All(result.Outputs.Values.SelectMany(v => v), v => Assert.Equal(0, v));
        Check(Bars(new[] { 0d, 3, 1, 5, -2, 4, 0, 8, -3 }), 2, 3, 5, 2);
    }
    [Fact]
    public void AllFourExtremePeriodsGrowOnlyWithObservedBars()
    {
        var bars = Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 });
        Check(bars, int.MaxValue); Check(bars, second: int.MaxValue); Check(bars, third: int.MaxValue); Check(bars, smooth: int.MaxValue);
        Check(bars, 0, -2, 0, 0);
    }
    [Fact]
    public void InvalidCandleDoesNotAdvanceAnyLeg()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new ChandeCompositeMomentumIndexState(length1: 2, length2: 3, length3: 5); using var control = new ChandeCompositeMomentumIndexState(length1: 2, length2: 3, length3: 5);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 4d, -2, 6, 0, -1 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); Assert.Equal(b.Value, a.Value); Assert.Equal(b.Outputs!["Signal"], a.Outputs!["Signal"]); }
        }
    }
    [Fact]
    public void SelectedInputAndThreeCustomMeansPreserveComponentOrder()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected);
        var supplied = new[] { selected.Select(v => v * 2).ToArray(), selected.Select(v => -v).ToArray(), selected.Select(v => v + 7).ToArray() };
        foreach (var custom in new[] { false, true }) foreach (var route in new[] { "batch", "Ccmi", "Signal" })
        {
            var expected = BuiltInFormulaReferences.ChandeCompositeValues(effective, 2, 3, 5, 3, 4, custom ? supplied : null); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(3, period); Assert.Equal(expected.Ratios[calls], values); return supplied[calls++]; };
            using var armed = custom ? ComponentAverage.Arm(new[] { callback, callback, callback }) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (route == "batch") { data.CalculateChandeCompositeMomentumIndex(length1: 2, length2: 3, length3: 5); Assert.Equal(expected.Outputs["Ccmi"], data.OutputValues["Ccmi"]); Assert.Equal(expected.Outputs["Signal"], data.OutputValues["Signal"]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeChandeCompositeMomentumIndexFast(data, context, 2, 3, 5, signal: route == "Signal"); Assert.Equal(expected.Outputs[route], output.ToArray()); }
            Assert.Equal(custom ? 3 : 0, calls); if (custom) { Assert.Equal(3, ComponentAverage.Requests); Assert.Equal(3, ComponentAverage.Substitutions); }
        }
    }
    [Fact]
    public void LegacyAveragesRetainBatchFastAndNativeParity()
    {
        var bars = Bars(new[] { 2d, -4, 7, -2, 1, 8, -3, 0, 4 }); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateChandeCompositeMomentumIndex(kind, 2, 3, 5, 3); using var context = new ComputeContext(); using var state = new ChandeCompositeMomentumIndexState(kind, 2, 3, 5, 3);
        foreach (var key in new[] { "Ccmi", "Signal" }) { using var output = IndicatorCompute.ComputeChandeCompositeMomentumIndexFast(Data(bars), context, 2, 3, 5, kind, 3, key == "Signal"); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); Assert.Equal(batch.CustomValuesList[i], point.Value); Assert.Equal(batch.OutputValues["Signal"][i], point.Outputs!["Signal"]); }
    }
}
