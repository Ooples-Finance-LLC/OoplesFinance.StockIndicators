using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BrownCompositeNumericalTests
{
    private static readonly string[] Keys = { "Cbci", "FastSignal", "SlowSignal" };
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BROWN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ConstanceBrownCompositeIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRsiMomentum(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BrownCompositeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int fast = 2, int slow = 4, int length = 3, int lag = 2, int smooth = 2)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.BrownCompositeValues(bars, fast, slow, length, lag, smooth, code);
        var data = Data(bars).CalculateConstanceBrownCompositeIndex(kind, fast, slow, length, lag, smooth);
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["Cbci"], data.CustomValuesList);
        using var context = new ComputeContext();
        foreach (var key in Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); using var output = IndicatorCompute.ComputeConstanceBrownCompositeIndexFast(Data(bars), context, length, lag, smooth, kind, fast, slow, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new ConstanceBrownCompositeIndexState(kind, fast, slow, length, lag, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 5d, 0, 9, -3, 2, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Cbci"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs["Cbci"];
    }
    [Fact]
    public void WidePricesAndAllCoreMeansRetainBoundedMomentum()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 4 })
                Assert.All(Check(Bars(Enumerable.Range(0, 27).Select(i => (i % 7 - 3) * scale)), kind), v => Assert.InRange(v, -100, 200));
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 1, 1, -1, double.Epsilon, -double.Epsilon }));
    }
    [Fact]
    public void HandMomentumSeedsAndFlatCarryArePreserved()
    {
        var values = Check(Bars(new[] { 1d, 2, 1, 2 }), fast: 2, slow: 3, length: 2, lag: 1, smooth: 2);
        Assert.Equal(0, values[0]); Assert.Equal(100, values[1]); Assert.InRange(Math.Abs(values[2]), 0, 2e-14); Assert.InRange(Math.Abs(values[3] - 1900d / 21), 0, 3e-14);
        Assert.Equal(new[] { 0d, 100, 100, 100, 100 }, Check(Bars(Enumerable.Repeat(3d, 5))));
        Check(Bars(new[] { 0d, 0, 2, 0, 1, 1, 1, 1, 1, 0, 0 }), length: 3, lag: 1, smooth: 3);
    }
    [Fact]
    public void AllFiveExtremePeriodsGrowOnlyWithObservedBars()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var position in Enumerable.Range(0, 5))
            { var lengths = new[] { 2, 4, 3, 2, 2 }; lengths[position] = period; foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 0d, 3, -2, 7, 1, 1, 4 }) }) Check(bars, kind, lengths[0], lengths[1], lengths[2], lengths[3], lengths[4]); }
    }
    [Fact]
    public void BatchCustomMeansKeepLevelFastSlowOrder()
    {
        var original = Bars(new[] { 8d, 9, 10, 11, 12 }); var selected = new[] { 1d, 2, 1, 2, 1 }; var data = Data(original); data.SetCustomValues(selected.ToList());
        var external = new[] { Enumerable.Repeat(10d, 5).ToArray(), Enumerable.Repeat(20d, 5).ToArray(), Enumerable.Repeat(30d, 5).ToArray() };
        var expected = BuiltInFormulaReferences.BrownCompositeValues(Bars(selected), 2, 4, 3, 1, 2, 1, external); var slot = 0;
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 2, 2, 4 }[slot], period); if (slot == 0) { Assert.Equal(100, values[0]); Assert.Equal(100, values[1]); Assert.InRange(Math.Abs(values[2] - 100d / 3), 0, 1e-14); } else Assert.Equal(expected.Outputs["Cbci"], values.ToArray()); return external[slot++]; }; using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 3).ToArray());
        data.CalculateConstanceBrownCompositeIndex(fastLength: 2, slowLength: 4, length1: 3, length2: 1, smoothLength: 2);
        Assert.Equal(3, slot); Assert.Equal(3, ComponentAverage.Requests); Assert.Equal(3, ComponentAverage.Substitutions); Assert.Equal(expected.Signals, data.SignalsList); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]);
    }
    [Fact]
    public void FastCustomMeansRetainRsiStagesAndRequestedSignal()
    {
        var bars = Bars(new[] { 1d, 2, 1, 2 }); using var context = new ComputeContext();
        foreach (var key in Keys)
        {
            var slot = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 3, 3, 2, 2, 2, key == "FastSignal" ? 2 : 4 }[slot], period); if (slot < 4) Assert.Equal(slot % 2 == 0 ? new[] { 0d, 1, 0, 1 } : new[] { 0d, 0, 1, 0 }, values.ToArray()); else if (slot == 4) Assert.Equal(Enumerable.Repeat(42.857142857142854, 4), values.ToArray()); else Assert.Equal(new[] { 10d, 10, 10, 10 }, values.ToArray()); var substitute = Enumerable.Repeat(slot == 0 ? 1d : slot == 1 ? 2d : slot == 2 ? 3d : slot == 3 ? 4d : slot == 4 ? 10d : 20d, 4).ToArray(); slot++; return substitute; }; using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, key == "Cbci" ? 5 : 6).ToArray());
            using var output = IndicatorCompute.ComputeConstanceBrownCompositeIndexFast(Data(bars), context, 3, 1, 2, MovingAvgType.SimpleMovingAverage, 2, 4, key);
            Assert.Equal(Enumerable.Repeat(key == "Cbci" ? 10d : 20d, 4), output.ToArray()); Assert.Equal(key == "Cbci" ? 5 : 6, slot); Assert.Equal(slot, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void SelectedPricesAndLegacyMeansAgreeAcrossAllOutputs()
    {
        var bars = Bars(Enumerable.Range(0, 24).Select(i => (double)i + 10)); var selected = Enumerable.Range(0, 24).Select(i => (double)(i % 7 - 3)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.DoubleExponentialMovingAverage })
        {
            var baseline = Data(Bars(selected)).CalculateConstanceBrownCompositeIndex(kind, 2, 4, 3, 2, 2); var data = Data(bars); data.SetCustomValues(selected.ToList()); data.CalculateConstanceBrownCompositeIndex(kind, 2, 4, 3, 2, 2);
            using var context = new ComputeContext(); using var state = new ConstanceBrownCompositeIndexState(kind, 2, 4, 3, 2, 2);
            foreach (var key in Keys) { Assert.Equal(baseline.OutputValues[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeConstanceBrownCompositeIndexFast(source, context, 3, 2, 2, kind, 2, 4, key); Assert.Equal(baseline.OutputValues[key], output.ToArray()); }
            for (var i = 0; i < selected.Length; i++) { var point = state.Update(Native(Bars(new[] { selected[i] })[0]), true, true); foreach (var key in Keys) Assert.Equal(baseline.OutputValues[key][i], point.Outputs![key]); }
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherRsiLagOrMeans()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ConstanceBrownCompositeIndexState(fastLength: 2, slowLength: 4, length1: 3, length2: 2, smoothLength: 2); using var control = new ConstanceBrownCompositeIndexState(fastLength: 2, slowLength: 4, length1: 3, length2: 2, smoothLength: 2);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
}
