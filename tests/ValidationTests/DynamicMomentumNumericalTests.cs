using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DynamicMomentumNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Ref)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DynamicMomentumIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveWindows(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DynamicMomentumOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Periods, double[] Deviations) Check(Bar[] bars, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, int deviation = 3, int average = 4, int baseline = 5, int minimum = 1, int maximum = 7)
    {
        var expected = BuiltInFormulaReferences.DynamicMomentumValues(bars, reference, deviation, average, baseline, minimum, maximum); var batch = Data(bars).CalculateDynamicMomentumIndex(kind, deviation, average, baseline, maximum, minimum);
        Assert.Equal(expected.Outputs["Dmi"], batch.CustomValuesList); foreach (var key in new[] { "Dmi", "Signal", "Histogram" }) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in new[] { "Dmi", "Signal", "Histogram" }) { using var output = IndicatorCompute.ComputeDynamicMomentumIndexFast(Data(bars), context, kind, deviation, average, baseline, maximum, minimum, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new DynamicMomentumIndexState(kind, deviation, average, baseline, maximum, minimum); using var window = new DynamicMomentumWindow(kind, deviation, average, baseline, minimum, maximum);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 7d, -3, 2, 11, -4 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -99d })[0]), false, false); window.Next(-99, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Dmi"][i], point.Value); Assert.Equal(point.Value, direct.Line); foreach (var key in new[] { "Dmi", "Signal", "Histogram" }) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Periods[i], direct.Period); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandSubnormalLossesAndWideChangesCannotDisappear()
    {
        var tiny = Check(Bars(new[] { 0d, double.Epsilon, 0 }), minimum: 3, maximum: 3);
        Assert.Equal(new[] { 100d, 100, 50 }, tiny.Outputs["Dmi"]); Assert.Equal(83.33333333333333, tiny.Outputs["Signal"][2]); Assert.Equal(Signal.Buy, tiny.Signals[0]); Assert.Equal(Signal.StrongSell, tiny.Signals[2]);
        Assert.Equal(new[] { 100d, 100, 50 }, Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue }), minimum: 3, maximum: 3).Outputs["Dmi"]);
        Assert.Equal(new[] { 100d, 0, 0 }, Check(Bars(new[] { 3d, 2, 1 }), minimum: 3, maximum: 3).Outputs["Dmi"]);
        Assert.All(Check(Bars(Enumerable.Repeat(2d, 6))).Outputs["Dmi"], v => Assert.Equal(100, v));
    }
    [Fact]
    public void WidePopulationMomentsAndAllMeanKindsMatchFractions()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Bars(Enumerable.Range(0, 23).Select(i => (i % 9 - 4) * scale)), kind.Kind, kind.Ref);
        foreach (var kind in Kinds) Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, double.MaxValue, -double.MaxValue, 1, -2, 3, 4, 4, 4, -2 }), kind.Kind, kind.Ref);
        Check(Bars(Enumerable.Range(0, 30).Select(i => 1 + (i % 7) * Math.Pow(2, -52))));
    }
    [Fact]
    public void AdaptiveExpansionAndCompactionRetainOlderObservations()
    {
        var bars = Bars(Enumerable.Range(0, 2110).Select(i => (double)((i * 7) % 13 - 6))); var means = bars.Select((_, i) => i % 4 == 0 ? .001 : i % 4 == 1 ? 100d : i % 4 == 2 ? 0d : 1d).ToArray();
        var expected = BuiltInFormulaReferences.DynamicMomentumValues(bars, 1, 2, 3, 3, 1, 7, means); using var window = new DynamicMomentumWindow(MovingAvgType.SimpleMovingAverage, 2, 3, 3, 1, 7);
        Assert.Contains(1, expected.Periods); Assert.Contains(7, expected.Periods);
        for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, true }) { var point = window.Next(bars[i].Close, final, means[i]); Assert.Equal(expected.Periods[i], point.Period); Assert.Equal(expected.Outputs["Dmi"][i], point.Line); Assert.Equal(expected.Outputs["Signal"][i], point.SignalLine); Assert.Equal(expected.Signals[i], point.Trade); }
    }
    [Fact]
    public void PeriodBoundaryToleranceAndNormalizedLimitsAreExplicit()
    {
        Assert.Equal(7, DynamicMomentumPeriod.Calculate(2, 1, 14, 1, 30));
        Assert.Equal(7, DynamicMomentumPeriod.Calculate(2, Math.BitDecrement(1), 14, 1, 30));
        Assert.Equal(6, DynamicMomentumPeriod.Calculate(2, 1 - 1e-9, 14, 1, 30));
        Assert.Equal(30, DynamicMomentumPeriod.Calculate(0, 1, 14, 5, 30)); Assert.Equal(14, DynamicMomentumPeriod.Calculate(1, 0, 14, 5, 30));
        Assert.Equal(30, DynamicMomentumPeriod.Calculate(double.Epsilon, double.MaxValue, 14, 5, 30));
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), kind.Kind, kind.Ref, length, length, length, length, length); Check(Bars(new[] { -3d, 2, -1, 0, 4 }), kind.Kind, kind.Ref, length, length, length, length, length); }
        Check(Bars(new[] { 1d, 3, 2, 5, 1 }), minimum: 7, maximum: 2);
    }
    [Fact]
    public void CoreUsesTheSameAdaptiveFormulaAndSupportsInPlace()
    {
        var bars = Bars(Enumerable.Range(0, 23).Select(i => (double)(i % 7 - 3)));
        foreach (var bounds in new[] { (5, 30), (1, 1), (int.MaxValue, int.MaxValue), (7, 2) })
        {
            var expected = BuiltInFormulaReferences.DynamicMomentumValues(bars, 1, 5, 10, 14, bounds.Item1, bounds.Item2); var input = bars.Select(b => b.Close).ToArray(); var output = new double[input.Length + 1]; output[^1] = 97;
            OscillatorCore.DynamicMomentumIndex(input, output, bounds.Item1, bounds.Item2); Assert.Equal(expected.Outputs["Dmi"], output.Take(input.Length)); Assert.Equal(97, output[^1]);
            OscillatorCore.DynamicMomentumIndex(input, input, bounds.Item1, bounds.Item2); Assert.Equal(expected.Outputs["Dmi"], input);
        }
        Assert.Throws<ArgumentException>(() => OscillatorCore.DynamicMomentumIndex(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void CallbackDeviationInputAndBatchZeroCallbackContractArePreserved()
    {
        var bars = Bars(new[] { 1d, 2, -1, 3, 0, 2, -3, 4 }); var means = new[] { 0d, 1, 0, 100, .001, 1, 100, 0 }; var expected = BuiltInFormulaReferences.DynamicMomentumValues(bars, 1, 2, 3, 3, 1, 7, means);
        foreach (var key in new[] { "Dmi", "Signal", "Histogram" })
        {
            var calls = 0; using var armed = ComponentAverage.Arm((values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(expected.Deviations, values); return means; }); using var context = new ComputeContext(); var data = Data(bars); using var output = IndicatorCompute.ComputeDynamicMomentumIndexFast(data, context, length1: 2, length2: 3, length3: 3, upLimit: 7, dnLimit: 1, outputKey: key); Assert.Equal(expected.Outputs[key], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(bars.Select(b => b.Close), data.InputValues);
        }
        foreach (var kind in Kinds) { var calls = 0; using var armed = ComponentAverage.Arm((values, _) => { calls++; return values; }); var batch = Data(bars).CalculateDynamicMomentumIndex(kind.Kind, 2, 3, 3, 7, 1); Assert.Equal(BuiltInFormulaReferences.DynamicMomentumValues(bars, kind.Ref, 2, 3, 3, 1, 7).Outputs["Dmi"], batch.CustomValuesList); Assert.Equal(0, calls); }
    }
    [Fact]
    public void SelectedSourcesAndLegacyMeanKindsAgree()
    {
        var bars = Bars(new[] { 1d, 2, -1, 3, 0, 2, -3, 4, 7, 1 }); var kind = MovingAvgType.DoubleExponentialMovingAverage; var batch = Data(bars).CalculateDynamicMomentumIndex(kind, 2, 3, 3, 7, 1); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeDynamicMomentumIndexFast(Data(bars), context, kind, 2, 3, 3, 7, 1); Assert.Equal(batch.CustomValuesList, fast.ToArray());
        using var state = new DynamicMomentumIndexState(kind, 2, 3, 3, 7, 1); for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
        var selected = bars.Select((_, i) => (double)(i % 3 - 1)).ToArray(); var custom = Data(bars); custom.SetCustomValues(selected.ToList()); var customBatch = Data(bars); customBatch.SetCustomValues(selected.ToList()); customBatch.CalculateDynamicMomentumIndex(); using var result = IndicatorCompute.ComputeDynamicMomentumIndexFast(custom, context); Assert.Equal(customBatch.CustomValuesList, result.ToArray()); Assert.Equal(selected, custom.ChainedValues);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceDeviationOrAdaptiveHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new DynamicMomentumIndexState(); using var control = new DynamicMomentumIndexState(); foreach (var b in Bars(new[] { 1d, 3, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 0d, -3, 2 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
