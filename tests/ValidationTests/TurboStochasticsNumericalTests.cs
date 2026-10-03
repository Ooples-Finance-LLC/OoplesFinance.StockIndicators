using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TurboStochasticsNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 4, 0, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static StockData Batch(StockData data, bool slow, MovingAvgType kind, int length, int fit, int turbo) => slow ? data.CalculateTurboStochasticsSlow(kind, length, fit, turbo) : data.CalculateTurboStochasticsFast(kind, length, fit, turbo);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, bool slow, MovingAvgType kind, int length, int fit, int turbo, string key) => slow ? IndicatorCompute.ComputeTurboStochasticsSlowFast(data, context, length, fit, turbo, kind, key) : IndicatorCompute.ComputeTurboStochasticsFastFast(data, context, length, fit, turbo, kind, key);
    private static IStreamingIndicatorState State(bool slow, MovingAvgType kind, int length, int fit, int turbo) => slow ? new TurboStochasticsSlowState(kind, length, fit, turbo) : new TurboStochasticsFastState(kind, length, fit, turbo);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(TurboStochasticsFast) || c.IndicatorType == typeof(TurboStochasticsSlow)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRegressions(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TurboStochasticsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesOriginalExtrema(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var o = indicator.CreateOptions(); int P(string name) => (int)o.GetType().GetProperty(name)!.GetValue(o)!;
        var kind = (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!; var slow = indicator.BatchName == IndicatorName.TurboStochasticsSlow;
        var bars = Bars(Enumerable.Range(0, 40).Select(i => 1d + i % 3).ToArray()); var selected = bars.Select((_, i) => i % 7 - 1d).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.TurboStochasticsOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        foreach (var pair in expected) { using var actual = Fast(data, context, slow, kind, P("Length1"), P("Length2"), P("TurboLength"), pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.High), data.HighPrices); Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        Batch(data, slow, kind, P("Length1"), P("Length2"), P("TurboLength")); foreach (var pair in expected) Assert.Equal(pair.Value, data.OutputValues[pair.Key]); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, bool slow = false, int length = 2, int fit = 3, int turbo = 0, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TurboStochasticsValues(bars, length, fit, turbo, kind, slow); var data = Batch(Data(bars), slow, kind, length, fit, turbo);
        foreach (var pair in expected.Outputs) { Assert.Equal(pair.Value, data.OutputValues[pair.Key]); using var context = new ComputeContext(); using var actual = Fast(Data(bars), context, slow, kind, length, fit, turbo, pair.Key); Assert.Equal(pair.Value, actual.ToArray()); }
        Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["Tsf"], data.CustomValuesList);
        var native = State(slow, kind, length, fit, turbo); using var lease = (IDisposable)native; using var raw = new TurboStochasticsWindow(kind, length, fit, turbo, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(1, 3, 2, 0, 4)) { native.Update(Native(b), true, true); raw.Next(b.Close, b.High, b.Low, true); } native.Reset(); raw.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Bars(-9)[0]), false, false); raw.Next(-9, 4, 0, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = native.Update(Native(b), final, true); var direct = raw.Next(b.Close, b.High, b.Low, final);
                    foreach (var pair in expected.Outputs) Assert.Equal(pair.Value[i], point.Outputs![pair.Key]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Outputs["Signal"][i], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentRawSmoothedAndRegressionHands()
    {
        var bars = Bars(0, 4, 0, 4, 2, 1); var fast = Check(bars); var slow = Check(bars, true);
        Assert.Equal(new[] { 0d, 100, 100d / 3, 200d / 3, 75, 125d / 6 }, fast.Outputs["Tsf"]);
        Assert.Equal(new[] { 0d, 50, 175d / 3, 50, 425d / 6, 575d / 12 }, fast.Outputs["Signal"]); Assert.Equal(fast.Outputs["Signal"], slow.Outputs["Tsf"]);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod, MovingAvgType.DoubleExponentialMovingAverage })
        foreach (var variant in new[] { false, true }) Check(Bars(0, 4, 2, 1, 3, 2, 0, 4, 1, 2, 2, 2), variant, 3, 4, -1, kind);
    }
    [Fact]
    public void WideSubnormalAndReversedRangesPreserveOrientation()
    {
        foreach (var slow in new[] { false, true })
        {
            var e = double.Epsilon; var m = double.MaxValue;
            foreach (var scale in new[] { e, 1d, m / 4 })
            { var bars = Bars(0, 4, 2, 1, 3, 0, 4).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, 0, b.Close * scale, 1)).ToArray(); var result = Check(bars, slow); Assert.All(result.Outputs.Values.SelectMany(v => v), v => Assert.True(double.IsFinite(v))); }
            Check(Bars(-m, m, 0, m / 2, -m / 2).Select(b => new Bar(b.Time, b.Open, m, -m, b.Close, 1)).ToArray(), slow);
            Check(Bars(0, 4, 2, 1, 3).Select(b => new Bar(b.Time, b.Open, 0, 4, b.Close, 1)).ToArray(), slow);
            Check(Bars(2, 2, 2).Select(b => new Bar(b.Time, 2, 2, 2, 2, 1)).ToArray(), slow);
        }
    }
    [Fact]
    public void SignedExtremePeriodsAreLazyAndDoNotOverflow()
    {
        Assert.Equal(4294967294L, TurboStochasticsWindow.RegressionPeriod(int.MaxValue, int.MaxValue)); Assert.Equal(1L, TurboStochasticsWindow.RegressionPeriod(int.MaxValue, int.MinValue));
        foreach (var slow in new[] { false, true }) foreach (var length in new[] { int.MinValue, 0, 1, 3, int.MaxValue }) foreach (var turbo in new[] { int.MinValue, -2, 0, 2, int.MaxValue }) Check(Bars(0, 4, 1, 3, 2, 0, 4), slow, length, length, turbo);
        Check(Array.Empty<Bar>()); Check(Array.Empty<Bar>(), true);
    }
    [Fact]
    public void ExpiringExtremaRegressionAndExactSignalThresholds()
    {
        foreach (var slow in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 35).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 7, i % 9 + 1, i % 5 - 2, i % 7, 1)).ToArray(); Check(bars, slow, 3, 4, 1);
            foreach (var price in new[] { Math.BitDecrement(1.2), 1.2, Math.BitIncrement(1.2), Math.BitDecrement(2.8), 2.8, Math.BitIncrement(2.8) }) Check(Bars(0, price, 4, price, 0, price), slow, 1, 1, 0);
        }
    }
    [Fact]
    public void CallbackCountsOrderShortArraysAndDiscoveryRemainDefined()
    {
        foreach (var slow in new[] { false, true }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var mode in new[] { "batch", "Tsf", "Signal" })
        {
            var expectedCount = mode == "batch" ? (kind == MovingAvgType.SimpleMovingAverage ? 0 : 2) + (slow ? 2 : 1) : (slow ? 1 : 0) + (mode == "Signal" ? 1 : 0);
            using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>()))
            {
                if (mode == "batch") Batch(Data(Bars(0, 4, 2)), slow, kind, 2, 1, 0); else { using var context = new ComputeContext(); using var output = Fast(Data(Bars(0, 4, 2)), context, slow, kind, 2, 1, 0, mode); }
                Assert.Equal(expectedCount, ComponentAverage.Requests);
            }
            var slot = 0; var callbacks = Enumerable.Range(0, expectedCount).Select(index => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) =>
            {
                Assert.Equal(index, slot++); var nested = mode == "batch" && kind != MovingAvgType.SimpleMovingAverage && index < 2;
                Assert.Equal(nested ? 3 : 2, period);
                var first = index == 0 || mode == "batch" && kind != MovingAvgType.SimpleMovingAverage && index == 2;
                Assert.Equal(first ? new[] { 0d, 100, 50 } : new[] { 7d, 0, 0 }, values); return new[] { 7d };
            })).ToArray();
            using (ComponentAverage.Arm(callbacks))
            {
                if (mode == "batch") { var result = Batch(Data(Bars(0, 4, 2)), slow, kind, 2, 1, 0); Assert.Equal(slow ? new[] { 7d, 0, 0 } : new[] { 0d, 100, 50 }, result.CustomValuesList); Assert.Equal(new[] { 7d, 0, 0 }, result.OutputValues["Signal"]); }
                else { using var context = new ComputeContext(); using var output = Fast(Data(Bars(0, 4, 2)), context, slow, kind, 2, 1, 0, mode); Assert.Equal(mode == "Signal" || slow ? new[] { 7d, 0, 0 } : new[] { 0d, 100, 50 }, output.ToArray()); }
                Assert.Equal(expectedCount, ComponentAverage.Requests); Assert.Equal(expectedCount, ComponentAverage.Substitutions);
            }
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAnyHistory()
    {
        foreach (var slow in new[] { false, true }) foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var actual = State(slow, MovingAvgType.SimpleMovingAverage, 2, 3, 0); var expected = State(slow, MovingAvgType.SimpleMovingAverage, 2, 3, 0); using var aLease = (IDisposable)actual; using var eLease = (IDisposable)expected;
            actual.Update(Native(Bars(1)[0]), true, true); expected.Update(Native(Bars(1)[0]), true, true); var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(3, 0, 4, 2)) { var a = actual.Update(Native(b), true, true); var e = expected.Update(Native(b), true, true); Assert.Equal(e.Value, a.Value); Assert.Equal(e.Outputs!["Signal"], a.Outputs!["Signal"]); }
        }
    }
}
