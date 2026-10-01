using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MacZNumericalTests
{
    private static Bar[] Candles(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MACZ", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MacZIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStandardization(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MacZOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MacZBudget);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static void Equal(double expected, double actual)
    {
        if (expected == 0 || double.IsInfinity(expected) || Math.Abs(expected) <= 16 * double.Epsilon) Assert.Equal(expected, actual);
        else Assert.True(double.IsFinite(actual) && Math.Sign(expected) == Math.Sign(actual) && Math.Abs((actual - expected) / expected) <= 4e-15, $"Expected {expected:R}, actual {actual:R}");
    }
    private static Dictionary<string, double[]> Check(Bar[] bars, int fast = 1, int slow = 2, int signal = 2, int length = 2, double mult = 1, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.MacZValues(bars, fast, slow, signal, length, mult, reference); var batch = Data(bars).CalculateMacZIndicator(kind, fast, slow, signal, length, mult: mult);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(batch.OutputValues["Macz"], batch.CustomValuesList);
        foreach (var key in expected.Outputs.Keys)
        {
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeMacZIndicatorFast(Data(bars), context, fast, slow, length, mult, kind, signal, key);
            Assert.Equal(batch.OutputValues[key], output.ToArray());
            for (var i = 0; i < bars.Length; i++) Equal(expected.Outputs[key][i], output.Span[i]);
        }
        using var native = new MacZIndicatorState(kind, fast, slow, signal, length, mult: mult);
        using var direct = new MacZWindow(kind, fast, slow, signal, length, mult);
        for (var pass = 0; pass < 2; pass++)
        {
            native.Update(Native(Candles(999)[0]), true, false); direct.Next(999, true); native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Candles(-888)[0]), false, false); direct.Next(-888, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var raw = direct.Next(bars[i].Close, final);
                    foreach (var key in expected.Outputs.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]);
                    Assert.Equal(batch.OutputValues["Macz"][i], point.Value); Assert.Equal(point.Value, raw.Line); Assert.Equal(batch.OutputValues["Signal"][i], raw.SignalLine); Assert.Equal(batch.OutputValues["Histogram"][i], raw.Histogram); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void HandValuesIncludeSignalHistogramAndWarmup()
    {
        var values = Check(Candles(2, 4, 2)); Assert.Equal(new[] { 0d, 2.5, -1.25 }, values["Macz"]); Assert.Equal(new[] { 0d, 1.25, .625 }, values["Signal"]); Assert.Equal(new[] { 0d, 1.25, -1.875 }, values["Histogram"]);
        Check(Candles(2, 4, 2, 3, -1, 7, 4, 1), 2, 4, 3, 3);
        Assert.All(Check(Candles(2, 2, 2, 2))["Macz"], v => Assert.Equal(0, v));
    }
    [Fact]
    public void TinyMeansAndDeviationSurviveUntilCompleteQuotient()
    {
        var e = double.Epsilon;
        Assert.Equal(2, Check(Candles(0, e))["Macz"][1]);
        Assert.Equal(-2, Check(Candles(0, -e))["Macz"][1]);
        foreach (var kind in Kinds) Check(Candles(e, 2 * e, -e, 3 * e, 0, 4 * e), 2, 3, 2, 2, 1, kind.Kind, kind.Reference);
        Assert.Equal(e, Check(Candles(0, e), mult: e / 1)["Signal"][1]);
    }
    [Fact]
    public void WideDifferencesProductsAndUnpublishedSignals()
    {
        var m = double.MaxValue;
        foreach (var kind in Kinds) foreach (var mult in new[] { -m, -2d, 0, double.Epsilon, .5, m })
            Check(Candles(m, -m, m / 2, -m / 4, 0, m / 8, m / 2), 2, 3, 3, 2, mult, kind.Kind, kind.Reference);
        var value = 1e200; var adjacent = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);
        Check(Candles(value, adjacent, value, adjacent, value), 1, 2, 2, 2);
        var extended = Check(Candles(0, 1), signal: 2, mult: m); Assert.Equal(double.PositiveInfinity, extended["Macz"][1]); Assert.Equal(m, extended["Signal"][1]); Assert.Equal(m, extended["Histogram"][1]);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 4))
        { var p = new[] { 2, 3, 2, 2 }; p[slot] = period; Check(Candles(2, 4, -1, 3), p[0], p[1], p[2], p[3], 1, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), p[0], p[1], p[2], p[3], 1, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void ComponentSlotsReceiveTheirOwnPeriodsAndSources()
    {
        var bars = Candles(2, 4, 2); var prices = new[] { 2d, 4, 2 }; var zero = new[] { 0d, 0, 0 };
        foreach (var key in new[] { "Macz", "Signal", "Histogram" })
        {
            var calls = 0; var periods = new[] { 1, 3, 2, 4 }; var supplied = new[] { zero, zero, zero, new[] { 1d, 1, 1 } };
            using var armed = ComponentAverage.Arm(Enumerable.Range(0, 4).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) => { calls++; Assert.Equal(periods[slot], period); Assert.Equal(slot < 3 ? prices : new[] { 0d, 4, 2 }, values); return supplied[slot]; })).ToArray());
            using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeMacZIndicatorFast(Data(bars), context, 1, 3, 2, 1, MovingAvgType.SimpleMovingAverage, 4, key);
            Assert.Equal(key == "Macz" ? new[] { 0d, 4, 2 } : key == "Signal" ? new[] { 1d, 1, 1 } : new[] { -1d, 3, 1 }, result.ToArray()); Assert.Equal(4, calls); Assert.Equal(4, ComponentAverage.Requests);
        }
        var count = 0; using (ComponentAverage.Arm((v, _) => { count++; return v; })) { Data(bars).CalculateMacZIndicator(); Assert.Equal(0, count); }
    }
    [Fact]
    public void LegacyAverageAndUnusedGammaRetainRouteAgreement()
    {
        var bars = Candles(2, 4, 1, 5, -1, 3); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateMacZIndicator(kind, 2, 3, 2, 2); using var state = new MacZIndicatorState(kind, 2, 3, 2, 2);
        foreach (var key in batch.OutputValues.Keys) { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeMacZIndicatorFast(Data(bars), context, 2, 3, 2, 1, kind, 2, key); Assert.Equal(batch.OutputValues[key], result.ToArray()); }
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Equal(batch.OutputValues[key][i], point.Outputs![key]); }
        foreach (var (legacy, code) in new[] { (MovingAvgType.DoubleExponentialMovingAverage, 4), (MovingAvgType.TripleExponentialMovingAverage, 5) })
        {
            var reference = BuiltInFormulaReferences.MacZValues(bars, 2, 3, 2, 2, 1, code).Outputs;
            var actual = Data(bars).CalculateMacZIndicator(legacy, 2, 3, 2, 2);
            foreach (var key in reference.Keys) for (var i = 0; i < bars.Length; i++) Equal(reference[key][i], actual.OutputValues[key][i]);
        }
        foreach (var gamma in new[] { double.NaN, -100d, 0, 100 }) Assert.Equal(batch.CustomValuesList, Data(bars).CalculateMacZIndicator(kind, 2, 3, 2, 2, gamma).CustomValuesList);
    }
    [Fact]
    public void NonfiniteInputsCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacZIndicatorState(mult: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Candles(1)).CalculateMacZIndicator(mult: bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMacZIndicatorFast(Data(Candles(1)), context, mult: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Candles(1, bad)).CalculateMacZIndicator());
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMacZIndicatorFast(Data(Candles(1, bad)), context));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var actual = new MacZIndicatorState(fastLength: 1, slowLength: 2, signalLength: 2, length: 2); using var expected = new MacZIndicatorState(fastLength: 1, slowLength: 2, signalLength: 2, length: 2);
                var first = Candles(2)[0]; actual.Update(Native(first), true, true); expected.Update(Native(first), true, true);
                var fields = new[] { 4d, 4, 4, 4, 1 }; fields[field] = bad;
                var invalid = new Bar(first.Time, fields[0], fields[1], fields[2], fields[3], fields[4]); Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(invalid), final, true));
                var next = Native(Candles(4)[0]); var a = actual.Update(next, true, true); var e = expected.Update(next, true, true); foreach (var key in e.Outputs!.Keys) Assert.Equal(e.Outputs[key], a.Outputs![key]);
            }
        }
    }
}
