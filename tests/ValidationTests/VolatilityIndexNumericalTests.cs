using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityIndexNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> values) => values.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChandeVolatilityIndexDynamicAverageIndicator) || c.IndicatorType == typeof(VolatilityIndexDynamicAverageIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentVolatilityNormalization(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.VolatilityIndexOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, double[] Deviation, Signal[] Signals) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int referenceKind = 3, double alpha1 = .2, double alpha2 = .04)
    {
        var expected = BuiltInFormulaReferences.VolatilityIndexValues(bars, length, referenceKind, alpha1, alpha2);
        using var context = new ComputeContext(); foreach (var second in new[] { false, true }) { using var output = IndicatorCompute.ComputeVolatilityIndexDynamicAverageFast(Data(bars), context, kind, length, alpha1, alpha2, second); Assert.Equal(expected.Outputs[second ? "Vida2" : "Vida1"], output.ToArray()); }
        foreach (var chande in new[] { false, true })
        {
            var prefix = chande ? "Cvida" : "Vida"; var batch = chande ? Data(bars).CalculateChandeVolatilityIndexDynamicAverageIndicator(kind, length, alpha1, alpha2) : Data(bars).CalculateVolatilityIndexDynamicAverageIndicator(kind, length, alpha1, alpha2);
            Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Vida1"], batch.OutputValues[prefix + "1"]); Assert.Equal(expected.Outputs["Vida2"], batch.OutputValues[prefix + "2"]);
            IStreamingIndicatorState state = chande ? new ChandeVolatilityIndexDynamicAverageIndicatorState(kind, length, alpha1, alpha2) : new VolatilityIndexDynamicAverageIndicatorState(kind, length, alpha1, alpha2); using var lifetime = state as IDisposable; using var window = new VolatilityIndexWindow(kind, length, alpha1, alpha2);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var b in Bars(new[] { 1d, 4, -2, 7, 3, -8, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(Bars(new[] { -97d })[0]), false, false); window.Next(-97, false);
                    foreach (var final in new[] { false, false, true })
                    {
                        var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Vida1"][i], point.Value); Assert.Equal(point.Value, point.Outputs![prefix + "1"]); Assert.Equal(expected.Outputs["Vida2"][i], point.Outputs[prefix + "2"]);
                        Assert.Equal(point.Value, direct.First); Assert.Equal(expected.Outputs["Vida2"][i], direct.Second); Assert.Equal(expected.Deviation[i], direct.Deviation); Assert.Equal(expected.Signals[i], direct.Signal);
                    }
                }
            }
        }
        return expected;
    }
    [Fact]
    public void WideDeviationNormalizationAndBothRecursionsMatchRationalScans()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var kind in Kinds) foreach (var length in new[] { 2, 7 })
            Check(Bars(Enumerable.Range(0, 17).Select(i => (i % 9 - 4) * scale)), length, kind.Kind, kind.Reference);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 1, -2, 3, 0 }));
        Check(Bars(new[] { 1d, 1 + Math.Pow(2, -52), 1, 1 + Math.Pow(2, -51), 1, 1 }));
    }
    [Fact]
    public void HandSeedsZeroGainsAndUnclampedExtrapolationArePreserved()
    {
        var result = Check(Bars(new[] { 7d, 9 }), 2, alpha1: .25, alpha2: .125); Assert.Equal(new[] { 7d, 8 }, result.Outputs["Vida1"]); Assert.Equal(new[] { 7d, 7.5 }, result.Outputs["Vida2"]);
        result = Check(Bars(new[] { 0d, 2 }), 2, alpha1: 1, alpha2: 2); Assert.Equal(new[] { 0d, 4 }, result.Outputs["Vida1"]); Assert.Equal(new[] { 0d, 8 }, result.Outputs["Vida2"]);
        result = Check(Bars(new[] { 3d, -2, 8, 0, -9 }), 2, alpha1: 0, alpha2: 0); Assert.All(result.Outputs.Values.SelectMany(v => v), value => Assert.Equal(3, value));
        result = Check(Bars(Enumerable.Repeat(double.MaxValue, 9))); Assert.All(result.Deviation, value => Assert.Equal(0, value)); Assert.All(result.Outputs.Values.SelectMany(v => v), value => Assert.Equal(double.MaxValue, value));
    }
    private static (Dictionary<string, double[]> Outputs, double[] Deviation, Signal[] Signals) Custom(Bar[] bars, double[] means, double alpha1, double alpha2)
    {
        var expected = BuiltInFormulaReferences.VolatilityIndexValues(bars, 2, 3, alpha1, alpha2, external: means);
        foreach (var route in new[] { "batch", "chande", "first", "second" })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); Assert.Equal(expected.Deviation, values); calls++; return means; };
            using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext();
            if (route is "batch" or "chande") { var data = Data(bars); if (route == "chande") data.CalculateChandeVolatilityIndexDynamicAverageIndicator(length: 2, alpha1: alpha1, alpha2: alpha2); else data.CalculateVolatilityIndexDynamicAverageIndicator(length: 2, alpha1: alpha1, alpha2: alpha2); var prefix = route == "chande" ? "Cvida" : "Vida"; Assert.Equal(expected.Outputs["Vida1"], data.OutputValues[prefix + "1"]); Assert.Equal(expected.Outputs["Vida2"], data.OutputValues[prefix + "2"]); Assert.Equal(expected.Signals, data.SignalsList); Assert.Empty(data.CustomValuesList); }
            else { using var output = IndicatorCompute.ComputeVolatilityIndexDynamicAverageFast(Data(bars), context, MovingAvgType.ExponentialMovingAverage, 2, alpha1, alpha2, route == "second"); Assert.Equal(expected.Outputs[route == "second" ? "Vida2" : "Vida1"], output.ToArray()); }
            Assert.Equal(1, calls); Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions);
        }
        using var window = new VolatilityIndexWindow(MovingAvgType.ExponentialMovingAverage, 2, alpha1, alpha2, true);
        for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true }) { var point = window.Next(bars[i].Close, final, means[i]); Assert.Equal(expected.Outputs["Vida1"][i], point.First); Assert.Equal(expected.Outputs["Vida2"][i], point.Second); Assert.Equal(expected.Signals[i], point.Signal); }
        return expected;
    }
    [Fact]
    public void TinyGainRetainsHugePriceContributionAndExtendedStateCanRecover()
    {
        var m = double.MaxValue; var result = Custom(Bars(new[] { 0d, m }), new[] { m, m }, double.Epsilon, 2 * double.Epsilon);
        Assert.Equal(m * double.Epsilon / 2, result.Outputs["Vida1"][1]); Assert.Equal(m * double.Epsilon, result.Outputs["Vida2"][1]);
        result = Custom(Bars(new[] { 0d, m, -m, 0, 2 }), Enumerable.Repeat(m, 5).ToArray(), m, 0); Assert.True(double.IsInfinity(result.Outputs["Vida1"][1])); Assert.Equal(2, result.Outputs["Vida1"][4]); Assert.All(result.Outputs["Vida2"], v => Assert.Equal(0, v));
        Custom(Bars(new[] { 1d, 3, 1, 4, 0 }), new[] { 0d, -1, 0, 2, -1 }, .2, .04);
    }
    [Fact]
    public void ExtremePeriodsRemainLazyAndSinglePriceWindowsHoldTheirSeed()
    {
        var bars = Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 });
        foreach (var kind in Kinds) { var result = Check(bars, int.MaxValue, kind.Kind, kind.Reference); Assert.All(result.Outputs.Values.SelectMany(v => v), v => Assert.Equal(-double.MaxValue, v)); Check(bars, 0, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), int.MaxValue, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void SelectedPricesAndDeviationCallbackKeepOriginalCandleFields()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected); var supplied = selected.Select(v => v + 10).ToArray();
        foreach (var custom in new[] { false, true }) foreach (var route in new[] { "batch", "chande", "first", "second" })
        {
            var expected = BuiltInFormulaReferences.VolatilityIndexValues(effective, 2, 3, .2, .04, external: custom ? supplied : null); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); Assert.Equal(expected.Deviation, values); calls++; return supplied; }; using var armed = custom ? ComponentAverage.Arm(new[] { callback }) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (route is "batch" or "chande") { if (route == "chande") data.CalculateChandeVolatilityIndexDynamicAverageIndicator(length: 2); else data.CalculateVolatilityIndexDynamicAverageIndicator(length: 2); var prefix = route == "chande" ? "Cvida" : "Vida"; Assert.Equal(expected.Outputs["Vida1"], data.OutputValues[prefix + "1"]); Assert.Equal(expected.Outputs["Vida2"], data.OutputValues[prefix + "2"]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeVolatilityIndexDynamicAverageFast(data, context, MovingAvgType.ExponentialMovingAverage, 2, second: route == "second"); Assert.Equal(expected.Outputs[route == "second" ? "Vida2" : "Vida1"], output.ToArray()); }
            Assert.Equal(custom ? 1 : 0, calls); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices); Assert.Equal(bars.Select(b => b.High), data.HighPrices);
        }
    }
    [Fact]
    public void LegacyDeviationMeansRetainBothAliasRoutes()
    {
        var bars = Bars(new[] { 2d, -4, 7, -2, 1, 8, -3, 0, 4 }); var kind = MovingAvgType.DoubleExponentialMovingAverage; var batch = Data(bars).CalculateVolatilityIndexDynamicAverageIndicator(kind, 3); using var context = new ComputeContext();
        foreach (var second in new[] { false, true }) { using var output = IndicatorCompute.ComputeVolatilityIndexDynamicAverageFast(Data(bars), context, kind, 3, second: second); Assert.Equal(batch.OutputValues[second ? "Vida2" : "Vida1"], output.ToArray()); }
        using var state = new VolatilityIndexDynamicAverageIndicatorState(kind, 3); using var chande = new ChandeVolatilityIndexDynamicAverageIndicatorState(kind, 3);
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); var alias = chande.Update(Native(bars[i]), true, true); Assert.Equal(batch.OutputValues["Vida1"][i], point.Value); Assert.Equal(point.Value, alias.Value); Assert.Equal(batch.OutputValues["Vida2"][i], point.Outputs!["Vida2"]); Assert.Equal(point.Outputs["Vida2"], alias.Outputs!["Cvida2"]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceDeviationMeanOrEitherLine()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true }) foreach (var chande in new[] { false, true })
        {
            IStreamingIndicatorState state = chande ? new ChandeVolatilityIndexDynamicAverageIndicatorState(length: 2) : new VolatilityIndexDynamicAverageIndicatorState(length: 2); IStreamingIndicatorState control = chande ? new ChandeVolatilityIndexDynamicAverageIndicatorState(length: 2) : new VolatilityIndexDynamicAverageIndicatorState(length: 2); using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var b in Bars(new[] { 1d, 4, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            var prefix = chande ? "Cvida" : "Vida"; foreach (var b in Bars(new[] { -3d, 2, 7, 0 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs![prefix + "2"], actual.Outputs![prefix + "2"]); }
        }
    }
    [Fact]
    public void InvalidAlphasFollowTypedContractsBeforeCallbacks()
    {
        var bars = Bars(new[] { 1d, 2 });
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d }) foreach (var second in new[] { false, true })
        {
            var alpha1 = second ? .2 : bad; var alpha2 = second ? bad : .04;
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChandeVolatilityIndexDynamicAverageIndicatorSpecOptions(alpha1: alpha1, alpha2: alpha2)); Assert.Throws<ArgumentOutOfRangeException>(() => new VolatilityIndexDynamicAverageIndicatorSpecOptions(alpha1: alpha1, alpha2: alpha2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChandeVolatilityIndexDynamicAverageIndicatorState(alpha1: alpha1, alpha2: alpha2)); Assert.Throws<ArgumentOutOfRangeException>(() => new VolatilityIndexDynamicAverageIndicatorState(alpha1: alpha1, alpha2: alpha2));
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, _) => { calls++; return v; }; using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateChandeVolatilityIndexDynamicAverageIndicator(alpha1: alpha1, alpha2: alpha2)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateVolatilityIndexDynamicAverageIndicator(alpha1: alpha1, alpha2: alpha2)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeVolatilityIndexDynamicAverageFast(Data(bars), context, MovingAvgType.ExponentialMovingAverage, 2, alpha1, alpha2)); Assert.Equal(0, calls);
        }
    }
}
