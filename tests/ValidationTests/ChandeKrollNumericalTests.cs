using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChandeKrollNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> values) => values.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChandeKrollRSquaredIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCorrelation(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ChandeKrollOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static (Dictionary<string, double[]> Outputs, double[] Raw, Signal[] Signals) Check(Bar[] bars, int length = 3, int smooth = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int referenceKind = 1)
    {
        var expected = BuiltInFormulaReferences.ChandeKrollValues(bars, length, smooth, referenceKind); var batch = Data(bars).CalculateChandeKrollRSquaredIndex(kind, length, smooth);
        Assert.Equal(expected.Outputs["Ckrsi"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Ckrsi"], batch.OutputValues["Ckrsi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeChandeKrollRSquaredIndexFast(Data(bars), context, length, kind, smooth); Assert.Equal(expected.Outputs["Ckrsi"], fast.ToArray());
        using var state = new ChandeKrollRSquaredIndexState(kind, length, smooth); using var window = new ChandeKrollWindow(kind, length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(new[] { 1d, 4, -2, 7, 3, -8, 2 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -97d })[0]), false, false); window.Next(-97, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Ckrsi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ckrsi"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Raw[i], direct.Raw); Assert.Equal(expected.Signals[i], direct.Signal); }
            }
        }
        return expected;
    }
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    [Fact]
    public void WideCorrelationSquareAndMeansMatchIndependentCenteredScans()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var length in new[] { 2, 3, 7 }) foreach (var kind in Kinds)
            Check(Bars(Enumerable.Range(0, 19).Select(i => (i % 9 - 4) * scale)), length, 3, kind.Kind, kind.Reference);
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 1, -2, 3, 0 }));
        Check(Bars(new[] { 1d, 1 + Math.Pow(2, -52), 1, 1 + Math.Pow(2, -51), 1, 1 }));
    }
    [Fact]
    public void HandPartialWindowsPearsonRoundingAndConstantExpiryArePreserved()
    {
        var result = Check(Bars(new[] { 0d, 0, 1 }), 3, 1); Assert.Equal(new[] { 0d, 0, .7499999999999999 }, result.Raw);
        result = Check(Bars(new[] { 1d, 2, 1 }), 2); Assert.Equal(new[] { 0d, 0, 2d / 3 }, result.Outputs["Ckrsi"]);
        result = Check(Bars(new[] { 1d, 2, 3, 4 }), 3, 1); Assert.Equal(new[] { 0d, 1, 1, 1 }, result.Raw);
        result = Check(Bars(new[] { 4d, 3, 2, 1 }), 3, 1); Assert.Equal(new[] { 0d, 1, 1, 1 }, result.Raw);
        result = Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 0, 0, 0, 0 }), 3, 1); Assert.Equal(0, result.Raw[^1]);
        result = Check(Bars(Enumerable.Repeat(double.MaxValue, 8))); Assert.All(result.Raw, v => Assert.Equal(0, v));
        Assert.Equal(Signal.StrongBuy, Check(Bars(new[] { 1d, 2 }), 2, 1).Signals[1]);
    }
    [Fact]
    public void ExtremePeriodsGrowOnlyWithObservedPricesAndMeans()
    {
        var bars = Bars(new[] { -double.MaxValue, double.MaxValue, 0, 2, -3, 7 });
        foreach (var kind in Kinds) { Check(bars, int.MaxValue, 3, kind.Kind, kind.Reference); Check(bars, 3, int.MaxValue, kind.Kind, kind.Reference); Check(bars, 0, 0, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), int.MaxValue, int.MaxValue, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void SelectedInputAndSingleCustomAveragePreserveRawCorrelation()
    {
        var selected = new[] { -2d, 0, 4, 3, -1, 8, -3, 2 }; var bars = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, i + 1)).ToArray(); var effective = Bars(selected); var supplied = selected.Select(v => v / 10).ToArray();
        foreach (var custom in new[] { false, true }) foreach (var fast in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.ChandeKrollValues(effective, 3, 2, 1, custom ? supplied : null); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); Assert.Equal(expected.Raw, values); calls++; return supplied; };
            using var armed = custom ? ComponentAverage.Arm(new[] { callback }) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeChandeKrollRSquaredIndexFast(data, context, 3, smoothLength: 2); Assert.Equal(expected.Outputs["Ckrsi"], output.ToArray()); }
            else { data.CalculateChandeKrollRSquaredIndex(length: 3, smoothLength: 2); Assert.Equal(expected.Outputs["Ckrsi"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(custom ? 1 : 0, calls); if (custom) { Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions); }
        }
    }
    [Fact]
    public void LegacyAveragesPreserveNativeAndBatchParity()
    {
        var bars = Bars(new[] { 2d, -4, 7, -2, 1, 8, -3, 0, 4 });
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateChandeKrollRSquaredIndex(kind, 3, 2); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeChandeKrollRSquaredIndexFast(Data(bars), context, 3, kind, 2); Assert.Equal(batch.CustomValuesList, output.ToArray());
            using var state = new ChandeKrollRSquaredIndexState(kind, 3, 2); for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceCorrelationOrSmoothing()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new ChandeKrollRSquaredIndexState(length: 3, smoothLength: 2); using var control = new ChandeKrollRSquaredIndexState(length: 3, smoothLength: 2);
            foreach (var b in Bars(new[] { 1d, 4, -2, 7 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Bars(new[] { -3d, 2, 7, 0 })) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
