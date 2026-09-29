using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChandeDisparityNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> values) => values.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChandeMomentumOscillatorAverageDisparityIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDisparities(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ChandeDisparityOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int first = 2, int second = 3, int third = 5, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int referenceKind = 3)
    {
        var expected = BuiltInFormulaReferences.ChandeDisparityValues(bars, first, second, third, referenceKind); var batch = Data(bars).CalculateChandeMomentumOscillatorAverageDisparityIndex(kind, first, second, third);
        Assert.Equal(expected.Outputs["Cmoadi"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Cmoadi"], batch.OutputValues["Cmoadi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeChandeMomentumOscillatorAverageDisparityIndexFast(Data(bars), context, first, second, third, kind); Assert.Equal(expected.Outputs["Cmoadi"], fast.ToArray());
        using var state = new ChandeMomentumOscillatorAverageDisparityIndexState(kind, first, second, third); using var window = new ChandeDisparityWindow(kind, first, second, third);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 1d, 4, -2, 7, 3, -8, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -97d })[0]), false, false); window.Next(-97, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Cmoadi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Cmoadi"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    [Fact]
    public void WideMeansDisparitiesAndSignalsMatchIndependentFractions()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var kind in Kinds)
            Check(Bars(Enumerable.Range(0, 19).Select(i => (i % 9 - 4) * scale)), kind: kind.Kind, referenceKind: kind.Reference);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 1, -2, 3, 0 }));
        Check(Bars(new[] { 1d, 1 + Math.Pow(2, -52), 1, 1 + Math.Pow(2, -51), 1, 1 }));
    }
    [Fact]
    public void HandZeroPriceAndThreeMeanSeedsArePreserved()
    {
        Assert.Equal(new[] { 0d, 25 }, Check(Bars(new[] { 2d, 4 }), 2, 2, 2).Outputs["Cmoadi"]);
        Assert.Equal(new[] { 0d, 25 }, Check(Bars(new[] { -2d, -4 }), 2, 2, 2).Outputs["Cmoadi"]);
        var result = Check(Bars(new[] { 1d, 0, 2, 0, -3, 0 })); Assert.Equal(0, result.Outputs["Cmoadi"][1]); Assert.Equal(0, result.Outputs["Cmoadi"][3]); Assert.Equal(0, result.Outputs["Cmoadi"][5]);
        result = Check(Bars(new[] { 1d, -2, 3, 0 }), 1, 1, 1); Assert.All(result.Outputs["Cmoadi"], v => Assert.Equal(0, v));
        Assert.Equal(Signal.StrongBuy, Check(Bars(new[] { 2d, 4 }), 2, 2, 2).Signals[1]);
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Custom(Bar[] bars, double[][] supplied)
    {
        var expected = BuiltInFormulaReferences.ChandeDisparityValues(bars, 2, 3, 5, 3, supplied); var prices = bars.Select(b => b.Close).ToArray();
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; var periods = new[] { 2, 3, 5 }; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(periods[calls], period); Assert.Equal(prices, values); return supplied[calls++]; };
            using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeChandeMomentumOscillatorAverageDisparityIndexFast(Data(bars), context, 2, 3, 5); Assert.Equal(expected.Outputs["Cmoadi"], output.ToArray()); }
            else { var batch = Data(bars).CalculateChandeMomentumOscillatorAverageDisparityIndex(length1: 2, length2: 3, length3: 5); Assert.Equal(expected.Outputs["Cmoadi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); }
            Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Requests); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        using var window = new ChandeDisparityWindow(MovingAvgType.ExponentialMovingAverage, 2, 3, 5, true);
        for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true }) { var point = window.Next(bars[i].Close, final, supplied[0][i], supplied[1][i], supplied[2][i]); Assert.Equal(expected.Outputs["Cmoadi"][i], point.Value); Assert.Equal(expected.Signals[i], point.Signal); }
        return expected;
    }
    [Fact]
    public void OpposingWideCustomMeansRetainCommonTinyPriceTerm()
    {
        var bars = Bars(new[] { double.Epsilon, double.MaxValue, 0d, -double.Epsilon, 1, -1 }); var m = double.MaxValue;
        var supplied = new[] { new[] { m, -m, m, m, -1d, -1 }, new[] { -m, -m, m, -m, 1d, 1 }, new[] { double.Epsilon, -m, m, -double.Epsilon, 1d, -1 } };
        var result = Custom(bars, supplied).Outputs["Cmoadi"]; Assert.Equal(200d / 3, result[0]); Assert.Equal(200, result[1]); Assert.Equal(0, result[2]); Assert.Equal(200d / 3, result[3]);
        using var reset = new ChandeDisparityWindow(MovingAvgType.ExponentialMovingAverage, 2, 3, 5, true);
        Assert.Equal(Signal.StrongBuy, reset.Next(1, true, 0, 0, 0).Signal); reset.Reset(); Assert.Equal(Signal.StrongBuy, reset.Next(1, true, 0, 0, 0).Signal);
    }
    [Fact]
    public void AllThreeExtremePeriodsGrowOnlyWithObservedMeans()
    {
        var bars = Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 });
        foreach (var kind in Kinds) { Check(bars, int.MaxValue, 3, 5, kind.Kind, kind.Reference); Check(bars, 2, int.MaxValue, 5, kind.Kind, kind.Reference); Check(bars, 2, 3, int.MaxValue, kind.Kind, kind.Reference); Check(bars, 0, -1, 0, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), int.MaxValue, int.MaxValue, int.MaxValue, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void GenuinePublishedOverflowRecoversWithoutPoisoningSignalMemory()
    {
        var bars = Bars(new[] { double.MaxValue, double.Epsilon, double.MaxValue, 0, -double.MaxValue, -double.Epsilon, 1 }); var result = Check(bars);
        Assert.True(double.IsInfinity(result.Outputs["Cmoadi"][1])); Assert.True(double.IsFinite(result.Outputs["Cmoadi"][2])); Assert.Equal(0, result.Outputs["Cmoadi"][3]);
    }
    [Fact]
    public void SelectedInputsAndLegacyMeansPreserveAllRoutes()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected);
        foreach (var fast in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.ChandeDisparityValues(effective, 2, 3, 5, 3); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeChandeMomentumOscillatorAverageDisparityIndexFast(data, context, 2, 3, 5); Assert.Equal(expected.Outputs["Cmoadi"], output.ToArray()); }
            else { data.CalculateChandeMomentumOscillatorAverageDisparityIndex(length1: 2, length2: 3, length3: 5); Assert.Equal(expected.Outputs["Cmoadi"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var batch = Data(effective).CalculateChandeMomentumOscillatorAverageDisparityIndex(kind, 2, 3, 5); using var context2 = new ComputeContext(); using var output2 = IndicatorCompute.ComputeChandeMomentumOscillatorAverageDisparityIndexFast(Data(effective), context2, 2, 3, 5, kind); Assert.Equal(batch.CustomValuesList, output2.ToArray());
        using var state = new ChandeMomentumOscillatorAverageDisparityIndexState(kind, 2, 3, 5); for (var i = 0; i < effective.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(effective[i]), true, true).Value);
    }
    [Fact]
    public void InvalidCandleCannotAdvanceAnyMean()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new ChandeMomentumOscillatorAverageDisparityIndexState(length1: 2, length2: 3, length3: 5); using var control = new ChandeMomentumOscillatorAverageDisparityIndexState(length1: 2, length2: 3, length3: 5);
            foreach (var b in Bars(new[] { 1d, 4, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Bars(new[] { -3d, 2, 7, 0 })) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
