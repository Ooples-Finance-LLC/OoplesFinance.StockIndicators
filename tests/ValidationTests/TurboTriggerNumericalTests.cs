using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TurboTriggerNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TURBO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TurboTrigger)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentEightStageFormula(IndicatorValidationCase c, string route)
    {
        var options = (TurboTriggerSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 1;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TurboTriggerOutputs(bars, options.Length, kind: kind), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length, int smoothing = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.TurboTriggerOutputs(bars, length, smoothing, k); var batch = Data(bars).CalculateTurboTrigger(kind, length, smoothing);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeTurboTriggerFast(Data(bars), context, length, smoothing, kind, key); Assert.Equal(expected[key], raw.ToArray());
        }
        using var state = new TurboTriggerState(kind, length, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["BullLine"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandStagesKeepMidpointBullAndTriggerDistinct()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.TurboTriggerOutputs(bars, 1);
        Assert.Equal(new[] { 0d, 1, 1 }, expected["BullLine"]); Assert.Equal(new[] { 0d, 0, 0 }, expected["Trigger"]); Check(bars, 1);
        var constant = Bars(new[] { double.MaxValue, double.MaxValue, double.MaxValue });
        Assert.All(BuiltInFormulaReferences.TurboTriggerOutputs(constant, 1, 1)["BullLine"], value => Assert.Equal(0d, value)); Check(constant, 1, 1);
    }
    [Fact]
    public void ExtendedDifferencesRecoverAcrossFourSmootherKinds()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 3, 14 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
            {
                Check(Bars(new[] { scale, -scale, scale, 0, 0, -scale, scale, 0, 0, 0, 0, 0 }), length, 2, kind);
                Check(Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), -scale, scale, -scale, i % 3 == 0 ? scale : -scale, 1)).ToArray(), length, 1, kind);
                Check(Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), scale, scale, -scale, i % 3 == 0 ? -scale : scale, 1)).ToArray(), length, 1, kind);
            }
            Check(Array.Empty<Bar>(), length, 2, kind);
        }
        var recovery = new[] { new Bar(DateTime.UnixEpoch, -double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1), new Bar(DateTime.UnixEpoch.AddDays(1), 0, 0, 0, 0, 1) };
        var expected = BuiltInFormulaReferences.TurboTriggerOutputs(recovery, 1, 1); Assert.Equal(double.PositiveInfinity, expected["BullLine"][0]); Assert.Equal(0d, expected["BullLine"][1]); Check(recovery, 1, 1);
    }
    [Fact]
    public void FullDefaultWindowsAdvancePastEverySmoothingStartup()
    {
        Check(Enumerable.Range(0, 430).Select(i => { var v = 10 + Math.Sin(i * .37); return new Bar(DateTime.UnixEpoch.AddDays(i), v + .13, v + .51, v - .37, v, 1); }).ToArray(), 100);
    }
    [Fact]
    public void CustomerStagesReceiveEightInputsInFormulaOrder()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 6, 0, 3, 1), new Bar(DateTime.UnixEpoch.AddDays(1), 2, 8, -1, 4, 1) };
        var inputs = new[] { new[] { 3d, 4 }, new[] { 1d, 2 }, new[] { 6d, 8 }, new[] { 0d, -1 }, new[] { 4d, 4 }, new[] { 3d, 3 }, new[] { 6d, 6 }, new[] { 9d, 9 } };
        var outputs = new[] { 3d, 5, 10, 1, 7, 11, 2, 99 };
        foreach (var route in new[] { "batch", "BullLine", "Trigger" })
        {
            var callbacks = Enumerable.Range(0, 8).Select(index => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, period) => { Assert.Equal(index < 4 ? 2 : 3, period); Assert.Equal(inputs[index], input); return Enumerable.Repeat(outputs[index], input.Count).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (route == "batch") { var result = Data(bars).CalculateTurboTrigger(length: 3); Assert.Equal(new[] { 11d, 11 }, result.OutputValues["BullLine"]); Assert.Equal(new[] { 99d, 99 }, result.OutputValues["Trigger"]); }
            else { using var raw = IndicatorCompute.ComputeTurboTriggerFast(Data(bars), context, 3, outputKey: route); Assert.Equal(Enumerable.Repeat(route == "Trigger" ? 99d : 11, 2), raw.ToArray()); }
            Assert.Equal(8, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void RawSelectedClosesKeepOtherCandleFields()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 };
        var original = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 10, -10, 0, 1)).ToArray();
        var projected = original.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TurboTriggerOutputs(projected, 3);
        foreach (var key in expected.Keys)
        {
            var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeTurboTriggerFast(data, context, 3, outputKey: key); Assert.Equal(expected[key], raw.ToArray());
        }
        var batchData = Data(original); batchData.SetCustomValues(selected.ToList()); var result = batchData.CalculateTurboTrigger(length: 3);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], result.OutputValues[key]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnySmoothingStage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new TurboTriggerState(length: 3); using var control = new TurboTriggerState(length: 3);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0, 8, 2, 9, 3, 0 }))
            {
                var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true);
                foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
            }
        }
    }
}
