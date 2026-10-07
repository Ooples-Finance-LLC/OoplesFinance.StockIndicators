using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SchaffShkNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SHK", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SchaffTrendCycleShk)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentTwoPassWindow(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SchaffShkOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int fast = 2, int slow = 5, int cycle = 3, int d1 = 3, int d2 = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.SchaffShkValues(bars, fast, slow, cycle, d1, d2, Kind(kind)); var batch = Data(bars).CalculateSchaffTrendCycleShk(kind, fast, slow, cycle, d1, d2);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Stc"], batch.CustomValuesList); Assert.All(expected.Outputs["Stc"], v => Assert.InRange(v, 0, 100));
        using var context = new ComputeContext(); foreach (var key in new[] { "Stc", "Macd" }) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var output = IndicatorCompute.ComputeSchaffShkFast(Data(bars), context, kind, fast, slow, cycle, d1, d2, key == "Macd"); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new SchaffTrendCycleShkState(kind, fast, slow, cycle, d1, d2);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 8d, -3, 4, 1, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Stc"][i], point.Value); foreach (var key in new[] { "Stc", "Macd" }) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    private static Dictionary<string, double[]> External(double[] fast, double[] slow, int cycle, int d1 = 1, int d2 = 1)
    {
        var bars = Bars(Enumerable.Range(0, fast.Length).Select(i => (double)i)); var expected = BuiltInFormulaReferences.SchaffShkValues(bars, 2, 5, cycle, d1, d2, 3, new[] { fast, slow });
        foreach (var route in new[] { "batch", "Stc", "Macd" })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, period) => { Assert.Equal(calls == 0 ? 2 : 5, period); return calls++ == 0 ? fast : slow; }; using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext();
            if (route == "batch") { var batch = Data(bars).CalculateSchaffTrendCycleShk(fastLength: 2, slowLength: 5, cycleLength: cycle, d1Length: d1, d2Length: d2); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList); }
            else { using var output = IndicatorCompute.ComputeSchaffShkFast(Data(bars), context, MovingAvgType.ExponentialMovingAverage, 2, 5, cycle, d1, d2, route == "Macd"); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Substitutions);
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideAveragesAndBothStochasticPassesMatchRationalStages()
    {
        foreach (var kind in Kinds) foreach (var cycle in new[] { 1, 3, 7 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Bars(new[] { 2d, 4, 6, 2, -4, -6, 3, 0, 2, 7, -5, 1, 1, 0, 4, 2 }.Select(p => p * scale)), 2, 5, cycle, 2, 4, kind);
        // A high provisional middle must not survive a lower final revision.
        using var revised = new SchaffCycleKernel(MovingAvgType.ExponentialMovingAverage, 1, 2, 3, 3, 1, true);
        Assert.Equal(0, revised.Next(0, true, 0, 0).Stc);
        Assert.Equal(100, revised.Next(0, true, 10, 0).Stc);
        Assert.Equal(50, revised.Next(0, true, 0, 0).Stc);
        Assert.Equal(100, revised.Next(0, false, 100, 0).Stc);
        Assert.Equal(50, revised.Next(0, false, 5, 0).Stc);
        Assert.Equal(50, revised.Next(0, true, 5, 0).Stc);
    }
    [Fact]
    public void HandTurnsFlatCarryAndSmoothingSeedsArePreserved()
    {
        Assert.Equal(new[] { 0d, 100, 0 }, Check(Bars(new[] { 1d, 2, 1 }), 1, 2, 2, 1, 1)["Stc"]);
        Assert.Equal(new[] { 0d, 50, 25 }, Check(Bars(new[] { 1d, 2, 1 }), 1, 2, 2)["Stc"]);
        Assert.Equal(new[] { 0d, 100, 100, 100 }, External(new[] { 0d, 1, 1, 1 }, new double[4], 2)["Stc"]);
        Assert.All(Check(Bars(new[] { 1d, 3, 1, 7, -2, 9 }), cycle: 1)["Stc"], value => Assert.Equal(0, value));
        Assert.All(Check(Bars(new[] { 1d, 3, 1, 7, -2, 9 }), fast: 3, slow: 3)["Stc"], value => Assert.Equal(0, value));
    }
    [Fact]
    public void InclusiveFlatnessUsesAbsoluteMeanScaleAndExpiry()
    {
        var boundary = Math.Pow(2, -45);
        Assert.Equal(new[] { 0d, 0, 0 }, External(new[] { 1d, 1, 1 }, new[] { -1d, -1 + boundary, -1 }, 3)["Stc"]);
        Assert.Equal(new[] { 0d, 0, 0 }, External(new[] { -1d, -1 + boundary, -1 }, new[] { 1d, 1, 1 }, 3)["Stc"]);
        Assert.Equal(new[] { 0d, 0, 100 }, External(new[] { 1d, 1, 1 }, new[] { -1d, -1 + 2 * boundary, -1 }, 3)["Stc"]);
        Assert.Equal(new[] { 0d, 100, 0 }, External(new[] { -1d, -1 + 2 * boundary, -1 }, new[] { 1d, 1, 1 }, 3)["Stc"]);
        var max = double.MaxValue; Assert.Equal(new[] { 0d, 0, 0, 100 }, External(new[] { max, 1d, 1, 1 }, new[] { max, 0d, 1, 0 }, 2)["Stc"]);
    }
    [Fact]
    public void ExtendedMacdCanOverflowWhileTheOscillatorStaysFiniteAndRecovers()
    {
        var max = double.MaxValue; var values = External(new[] { max, max, -max, 0 }, new[] { max, -max, max, 0 }, 4);
        Assert.Equal(new[] { 0d, double.PositiveInfinity, double.NegativeInfinity, 0 }, values["Macd"]); Assert.Equal(new[] { 0d, 100, 0, 50 }, values["Stc"]);
        Check(Bars(new[] { -max, max, max, -max, 0d, max, 0 }), 1, 2, 3, 2, 4, MovingAvgType.SimpleMovingAverage);
        var prices = new[] { 2d, 4, 6, 2, -4, 1, 5, 0, 3, -1 }; var expected = Check(Bars(prices))["Stc"]; foreach (var scale in new[] { Math.Pow(2, -600), Math.Pow(2, 600) }) Assert.Equal(expected, Check(Bars(prices.Select(p => p * scale)))["Stc"]);
    }
    [Fact]
    public void ResidualSurvivesShrinkingMiddleRanges()
    {
        foreach (var kind in Kinds) Check(Bars(Enumerable.Range(0, 180).Select(i => i < 10 ? (double)(i % 3) : 10d)), 3, 7, 5, 3, 7, kind);
        Check(Bars(Enumerable.Range(0, 160).Select(i => 100 + .1 * i)), 4, 7, 10, 3, 3);
    }
    [Fact]
    public void AllFiveExtremePeriodsKeepHistoriesLazy()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue - 1000, int.MaxValue }) foreach (var kind in Kinds) foreach (var position in Enumerable.Range(0, 5))
        { var lengths = new[] { 2, 5, 3, 2, 4 }; lengths[position] = period; foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }) }) Check(bars, lengths[0], lengths[1], lengths[2], lengths[3], lengths[4], kind); }
    }
    [Fact]
    public void CustomComponentsKeepSelectedInputAndFastSlowOrder()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8 }); var selected = new[] { -2d, 0, 4, 3 }; var supplied = new[] { new[] { 1d, 1, -1, 0 }, new[] { 0d, -1, 1, 0 } }; var expected = BuiltInFormulaReferences.SchaffShkValues(Bars(selected), 2, 5, 3, 2, 4, 3, supplied);
        foreach (var route in new[] { "batch", "Stc", "Macd" })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(selected, values); Assert.Equal(calls == 0 ? 2 : 5, period); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback }); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            if (route == "batch") { data.CalculateSchaffTrendCycleShk(fastLength: 2, slowLength: 5, cycleLength: 3, d1Length: 2, d2Length: 4); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeSchaffShkFast(data, context, MovingAvgType.ExponentialMovingAverage, 2, 5, 3, 2, 4, route == "Macd"); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Substitutions);
        }
        var selectedReference = BuiltInFormulaReferences.SchaffShkValues(Bars(selected), 2, 5, 3, 2, 4, 3).Outputs;
        using (var context = new ComputeContext()) foreach (var key in new[] { "Stc", "Macd" }) { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeSchaffShkFast(data, context, MovingAvgType.ExponentialMovingAverage, 2, 5, 3, 2, 4, key == "Macd"); Assert.Equal(selectedReference[key], output.ToArray()); }
        using var native = new SchaffTrendCycleShkState(MovingAvgType.DoubleExponentialMovingAverage, 2, 5, 3, 2, 4); var legacy = Data(bars).CalculateSchaffTrendCycleShk(MovingAvgType.DoubleExponentialMovingAverage, 2, 5, 3, 2, 4); using var ctx = new ComputeContext();
        foreach (var key in new[] { "Stc", "Macd" }) { using var fast = IndicatorCompute.ComputeSchaffShkFast(Data(bars), ctx, MovingAvgType.DoubleExponentialMovingAverage, 2, 5, 3, 2, 4, key == "Macd"); Assert.Equal(legacy.OutputValues[key], fast.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = native.Update(Native(bars[i]), true, true); foreach (var key in new[] { "Stc", "Macd" }) Assert.Equal(legacy.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherPass()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SchaffTrendCycleShkState(fastLength: 2, slowLength: 3, cycleLength: 2); using var control = new SchaffTrendCycleShkState(fastLength: 2, slowLength: 3, cycleLength: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); Assert.Equal(b.Value, a.Value); Assert.Equal(b.Outputs!["Macd"], a.Outputs!["Macd"]); }
        }
    }
}
