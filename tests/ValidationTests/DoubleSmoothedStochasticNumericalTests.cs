using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DoubleSmoothedStochasticNumericalTests
{
    private static readonly string[] Keys = { "Dss", "Signal" };
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("DSS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DoubleSmoothedStochastic)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDoubleSmoothing(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DoubleStochasticOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int length1 = 3, int length2 = 2, int length3 = 5, int length4 = 4, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.DoubleStochasticValues(bars, length1, length2, length3, length4, Kind(kind)); var batch = Data(bars).CalculateDoubleSmoothedStochastic(kind, length1, length2, length3, length4); Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Dss"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in Keys) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var output = IndicatorCompute.ComputeDoubleSmoothedStochasticFast(Data(bars), context, length1, kind, length2, length3, key, length4); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new DoubleSmoothedStochasticState(kind, length1, length2, length3, length4);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 8d, -3, 4, 1, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Dss"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) External(double[] numerator, double[] denominator, double[] signal)
    {
        var bars = Bars(Enumerable.Range(0, numerator.Length).Select(i => (double)i)); var supplied = new[] { numerator, denominator, numerator, denominator, signal }; var expected = BuiltInFormulaReferences.DoubleStochasticValues(bars, 3, 2, 5, 4, 3, supplied);
        foreach (var route in Keys.Append("batch"))
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 2, 2, 5, 5, 4 }[calls], period); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 5).ToArray()); using var context = new ComputeContext();
            if (route == "batch") { var batch = Data(bars).CalculateDoubleSmoothedStochastic(length1: 3, length2: 2, length3: 5, length4: 4); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList); }
            else { using var output = IndicatorCompute.ComputeDoubleSmoothedStochasticFast(Data(bars), context, 3, length2: 2, length3: 5, outputKey: route, length4: 4); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(5, calls); Assert.Equal(5, ComponentAverage.Substitutions);
        }
        return expected;
    }
    [Fact]
    public void WideOffsetsRangesAndBothAveragePassesMatchRationalStages()
    {
        // A high provisional candle must not become part of committed extrema.
        using var revised = new DoubleSmoothedStochasticWindow(MovingAvgType.ExponentialMovingAverage, 3, 1, 1, 1);
        Assert.Equal(0, revised.Next(0, 0, 0, true).Dss);
        Assert.Equal(100, revised.Next(10, 10, 10, true).Dss);
        Assert.Equal(5, revised.Next(100, 5, 5, false).Dss);
        Assert.Equal(50, revised.Next(5, 5, 5, false).Dss);
        Assert.Equal(50, revised.Next(5, 5, 5, true).Dss);
        foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, 2, 5, 4, kind);
    }
    [Fact]
    public void HandRangesAndSeparateSmoothingStagesKeepBothOutputs()
    {
        var bars = new[] { 1d, 2, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var one = Check(bars, 1, 1, 1, 1); foreach (var values in one.Values) Assert.Equal(new[] { 50d, 50, 50 }, values);
        var two = Check(bars, 2, 1, 1, 1); foreach (var values in two.Values) Assert.Equal(new[] { 50d, 200d / 3, 100d / 3 }, values);
        var staged = Check(bars, 1, 2, 3, 2, MovingAvgType.SimpleMovingAverage); Assert.Equal(new[] { 0d, 0, 50 }, staged["Dss"]); Assert.Equal(new[] { 0d, 0, 25 }, staged["Signal"]);
    }
    [Fact]
    public void ExtendedDifferencesAndProductsNormalizeBeforeProjection()
    {
        var max = double.MaxValue; var bars = new[] { -max, 0d, max, 0, -max }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, max, -max, v, 1)).ToArray(); Assert.Equal(new[] { 0d, 50, 100, 50, 0 }, Check(bars, 1, 1, 1, 1)["Dss"]); foreach (var kind in Kinds) Check(bars, 3, 2, 5, 4, kind);
        var values = External(new[] { max, max / 2, 0, -max }, new[] { max, max, max, max }, new double[4]).Outputs; Assert.Equal(new[] { 100d, 50, 0, 0 }, values["Dss"]);
        var tiny = double.Epsilon; Assert.Equal(new[] { 0d, 50, 100 }, External(new[] { 0d, tiny, 2 * tiny }, new[] { 2 * tiny, 2 * tiny, 2 * tiny }, new double[3]).Outputs["Dss"]);
    }
    [Fact]
    public void FlatScaleClampingAndStrictRsiCrossingsKeepSignalOrder()
    {
        Assert.Equal(new[] { 0d, 100, 0, 50 }, External(new[] { -1d, 2, 3, -1 }, new[] { 1d, 1, 0, -2 }, new double[4]).Outputs["Dss"]);
        var crossings = External(new[] { 20d, 40, 80, 60 }, Enumerable.Repeat(100d, 4).ToArray(), new[] { 20d, 40, 80, 60 }); Assert.Equal(new[] { Signal.None, Signal.Buy, Signal.None, Signal.Sell }, crossings.Signals);
        var equality = External(new[] { 20d, 30, 40, 80, 70, 60 }, Enumerable.Repeat(100d, 6).ToArray(), new[] { 20d, 30, 40, 80, 70, 60 }); Assert.All(equality.Signals, value => Assert.Equal(Signal.None, value));
        var strength = External(new[] { 10d, 20, 30, 20 }, Enumerable.Repeat(100d, 4).ToArray(), new[] { 0d, 15, 0, 25 }); Assert.Equal(new[] { Signal.StrongBuy, Signal.Buy, Signal.StrongBuy, Signal.StrongSell }, strength.Signals);
    }
    [Fact]
    public void ExtremaExpiryAndPowerOfTwoScalingPreserveWindowRatios()
    {
        Assert.Equal(new[] { 0d, 100, 0, 100 }, Check(Bars(new[] { 0d, 100, 1, 2 }), 2, 1, 1, 1)["Dss"]);
        var bars = Bars(new[] { 2d, 4, 6, 2, -4, 1, 5, 0, 3, -1 }); var expected = Check(bars); foreach (var factor in new[] { Math.Pow(2, -600), Math.Pow(2, 600) }) { var actual = Check(Bars(bars.Select(v => v.Close * factor))); foreach (var key in Keys) Assert.Equal(expected[key], actual[key]); }
    }
    [Fact]
    public void AllFourExtremePeriodsKeepFiveAveragesAndExtremaLazy()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds) foreach (var position in Enumerable.Range(0, 4)) { var lengths = new[] { 3, 2, 5, 4 }; lengths[position] = period; foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }) }) Check(bars, lengths[0], lengths[1], lengths[2], lengths[3], kind); }
    }
    [Fact]
    public void FiveCustomComponentsPreserveStageOrderAndSelectedRanges()
    {
        var bars = new[] { 2d, 4, 6, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 10, -10, v, 1)).ToArray(); var selected = new[] { -2d, 0, 4, 3 }; var supplied = new[] { new[] { 1d, 2, 3, 4 }, new[] { 5d, 6, 7, 8 }, new[] { 1d, 2, 3, 4 }, new[] { 2d, 4, 4, 8 }, new[] { 30d, 40, 50, 60 } }; var expected = BuiltInFormulaReferences.DoubleStochasticValues(bars, 3, 2, 5, 4, 3, supplied);
        foreach (var route in Keys.Append("batch"))
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 2, 2, 5, 5, 4 }[calls], period); Assert.Equal(calls == 0 ? new[] { 8d, 10, 14, 13 } : calls == 1 ? new[] { 20d, 20, 20, 20 } : calls == 2 ? supplied[0] : calls == 3 ? supplied[1] : expected.Outputs["Dss"], values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 5).ToArray()); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            if (route == "batch") { data.CalculateDoubleSmoothedStochastic(length1: 3, length2: 2, length3: 5, length4: 4); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); } else { using var output = IndicatorCompute.ComputeDoubleSmoothedStochasticFast(data, context, 3, length2: 2, length3: 5, outputKey: route, length4: 4); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(5, calls); Assert.Equal(5, ComponentAverage.Substitutions);
        }
        var outside = new[] { 30d, -40, 4, 20 }; var effective = bars.Select((b, i) => { var p = outside[i]; var previous = i == 0 ? p : outside[i - 1]; var inside = p >= b.Low && p <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(p, previous), inside ? b.Low : Math.Min(p, previous), p, b.Volume); }).ToArray(); var reference = BuiltInFormulaReferences.DoubleStochasticValues(effective, 3, 2, 5, 4, 3); var source = Data(bars); source.SetCustomValues(outside.ToList()); source.CalculateDoubleSmoothedStochastic(length1: 3, length2: 2, length3: 5, length4: 4); Assert.Equal(reference.Signals, source.SignalsList); using var ctx = new ComputeContext(); using var native = new DoubleSmoothedStochasticState(length1: 3, length2: 2, length3: 5, length4: 4);
        foreach (var key in Keys) { Assert.Equal(reference.Outputs[key], source.OutputValues[key]); var data = Data(bars); data.SetCustomValues(outside.ToList()); using var output = IndicatorCompute.ComputeDoubleSmoothedStochasticFast(data, ctx, 3, length2: 2, length3: 5, outputKey: key, length4: 4); Assert.Equal(reference.Outputs[key], output.ToArray()); } for (var i = 0; i < bars.Length; i++) { var point = native.Update(Native(effective[i]), true, true); foreach (var key in Keys) Assert.Equal(reference.Outputs[key][i], point.Outputs![key]); }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = Data(bars).CalculateDoubleSmoothedStochastic(kind, 3, 2, 5, 4); using var legacyState = new DoubleSmoothedStochasticState(kind, 3, 2, 5, 4); foreach (var key in Keys) { using var output = IndicatorCompute.ComputeDoubleSmoothedStochasticFast(Data(bars), ctx, 3, kind, 2, 5, key, 4); Assert.Equal(legacy.OutputValues[key], output.ToArray()); } for (var i = 0; i < bars.Length; i++) { var point = legacyState.Update(Native(bars[i]), true, true); foreach (var key in Keys) Assert.Equal(legacy.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherSmoothingPass()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new DoubleSmoothedStochasticState(length1: 3, length2: 2, length3: 5, length4: 4); using var control = new DoubleSmoothedStochasticState(length1: 3, length2: 2, length3: 5, length4: 4); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); Assert.Equal(b.Value, a.Value); foreach (var key in Keys) Assert.Equal(b.Outputs![key], a.Outputs![key]); }
        }
    }
}
