using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MacZVwapNumericalTests
{
    private static Bar[] Candles(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MACZ", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MacZVwapIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStandardization(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MacZVwapOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.MacZBudget);
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
    private static Dictionary<string, double[]> Check(Bar[] bars, int fast = 1, int slow = 2, int signal = 2, int length1 = 2, int length2 = 2, double gamma = .02, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.MacZVwapValues(bars, fast, slow, signal, length1, length2, gamma, reference); var batch = Data(bars).CalculateMacZVwapIndicator(kind, fast, slow, signal, length1, length2, gamma);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(batch.OutputValues["Macz"], batch.CustomValuesList);
        foreach (var (key, series) in new[] { ("Macz", IndicatorCompute.MacdSeries.Line), ("Signal", IndicatorCompute.MacdSeries.Signal), ("Histogram", IndicatorCompute.MacdSeries.Histogram) })
        {
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeMacZVwapIndicatorFast(Data(bars), context, fast, slow, signal, length1, length2, gamma, kind, series);
            Assert.Equal(batch.OutputValues[key], output.ToArray());
            for (var i = 0; i < bars.Length; i++) Equal(expected.Outputs[key][i], output.Span[i]);
        }
        using var native = new MacZVwapIndicatorState(kind, fast, slow, signal, length1, length2, gamma);
        using var direct = new MacZVwapWindow(kind, fast, slow, signal, length1, length2, gamma);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Candles(999, -888, 777, 666)) { native.Update(Native(seed), true, false); direct.Next(seed.Close, seed.Volume, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Candles(-888)[0]), false, false); direct.Next(-888, -17, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var raw = direct.Next(bars[i].Close, bars[i].Volume, final);
                    foreach (var key in expected.Outputs.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]);
                    Assert.Equal(batch.OutputValues["Macz"][i], point.Value); Assert.Equal(point.Value, raw.Line); Assert.Equal(batch.OutputValues["Signal"][i], raw.SignalLine); Assert.Equal(batch.OutputValues["Histogram"][i], raw.Histogram); Assert.Equal(expected.Signals[i], raw.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    private static Bar[] Volumes(double[] prices, double[] volumes) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, volumes[i])).ToArray();
    [Fact]
    public void ZeroVolumeHandValuesAndFilterTransfer()
    {
        var output = Check(Volumes(new[] { 2d, 2, 2, 2, 2 }, new double[5]), gamma: 0);
        Assert.Equal(new[] { 0d, 1d / 6, .5, 5d / 6, 1 }, output["Macz"]);
        using var filter = new MacZVwapWindow(MovingAvgType.SimpleMovingAverage, 1, 2, 2, 2, 2, 0);
        var actual = new[] { 0d, 2, 4, 2, 0 }.Select(v => filter.Filter(MacZWindow.Number.Of(v), true).Publish()).ToArray();
        Assert.Equal(new[] { 0d, 1d / 3, 4d / 3, 7d / 3, 7d / 3 }, actual);
        foreach (var gamma in new[] { 0d, .02, 1d, -2d, double.MaxValue })
        { using var seeded = new MacZVwapWindow(MovingAvgType.SimpleMovingAverage, 1, 2, 2, 2, 2, gamma); Assert.Equal(7, seeded.Filter(MacZWindow.Number.Of(7), true).Publish()); if (gamma == 1) Assert.Equal(7, seeded.Filter(MacZWindow.Number.Of(-9), true).Publish()); seeded.Reset(); Assert.Equal(-3, seeded.Filter(MacZWindow.Number.Of(-3), true).Publish()); }
        Assert.All(Check(Candles(2, 4, -1, 5, 3), gamma: 1)["Macz"], v => Assert.Equal(0, v));
    }
    [Fact]
    public void TinyAndOverflowingProductsRetainCompleteQuotients()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 4 })
        {
            var p = new[] { 0, scale, -scale, 2 * scale, scale, -2 * scale };
            foreach (var volume in new[] { double.Epsilon, 1d, double.MaxValue }) Check(Volumes(p, Enumerable.Repeat(volume, p.Length).ToArray()), kind: kind.Kind, reference: kind.Reference);
        }
        Check(Volumes(new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0d }, new[] { 1d, -1 + Math.Pow(2, -53), 1d, -1d }));
        foreach (var volumes in new[] { new[] { 1d, -1, 2, -2, 0, 3 }, new[] { 0d, 0, 1, 0, -1, 0 } }) Check(Volumes(new[] { 2d, 4, -1, 3, 5, -2 }, volumes));
    }
    [Fact]
    public void AlternatingTailDecaysWithoutAStageRoundingFloor()
    {
        foreach (var start in new[] { 0d, 1d })
        {
            var bars = Candles(Enumerable.Range(0, 256).Select(i => i == 0 ? start : i % 2 == 0 ? 1d : -1d).ToArray());
            Check(bars, length1: 1);
            Check(Volumes(bars.Select(b => b.Close).ToArray(), new double[bars.Length]), length1: 1);
        }
        using var constant = new MacZVwapWindow(MovingAvgType.SimpleMovingAverage, 1, 2, 2, 2, 2, double.MaxValue);
        for (var i = 0; i < 8; i++) Assert.Equal(7, constant.Filter(MacZWindow.Number.Of(7), true).Publish());
    }
    [Fact]
    public void GammaExtremesAndAllMeansMatchIndependentRecurrence()
    {
        foreach (var kind in Kinds) foreach (var gamma in new[] { 0d, 1d, 1 - Math.Pow(2, -53), -.5, 2d, double.MaxValue })
            Check(Candles(2, 4, -1, 5, 3, 7), 2, 4, 3, 3, 2, gamma, kind.Kind, kind.Reference);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 5))
        { var p = new[] { 2, 3, 2, 2, 2 }; p[slot] = period; Check(Candles(2, 4, -1, 3), p[0], p[1], p[2], p[3], p[4], .02, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), p[0], p[1], p[2], p[3], p[4], .02, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void LegacyMeansRetainRouteAgreement()
    {
        foreach (var (kind, code) in new[] { (MovingAvgType.DoubleExponentialMovingAverage, 4), (MovingAvgType.TripleExponentialMovingAverage, 5) })
            Check(Candles(2, 4, 1, 5, -1, 3), 2, 3, 2, 2, 2, .02, kind, code);
    }
    [Fact]
    public void ComponentSlotsReceiveOwnPeriodsAndUnpublishedLine()
    {
        var bars = Candles(2, 4, 2); var prices = new[] { 2d, 4, 2 }; var zero = new[] { 0d, 0, 0 };
        var expectedLine = new[] { 0d, (4 + Math.Sqrt(2)) / 6, (9 + 2 * Math.Sqrt(2)) / 6 };
        foreach (var series in new[] { IndicatorCompute.MacdSeries.Line, IndicatorCompute.MacdSeries.Signal, IndicatorCompute.MacdSeries.Histogram })
        {
            var calls = 0; var periods = new[] { 1, 3, 4 };
            using var armed = ComponentAverage.Arm(Enumerable.Range(0, 3).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) =>
            { calls++; Assert.Equal(periods[slot], period); if (slot < 2) Assert.Equal(prices, values); else for (var i = 0; i < values.Count; i++) Equal(expectedLine[i], values[i]); return slot == 0 ? prices : slot == 1 ? zero : new[] { 1d, 1, 1 }; })).ToArray());
            using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeMacZVwapIndicatorFast(Data(bars), context, 1, 3, 4, 2, 2, 0, MovingAvgType.SimpleMovingAverage, series);
            for (var i = 0; i < bars.Length; i++) Equal(series == IndicatorCompute.MacdSeries.Line ? expectedLine[i] : series == IndicatorCompute.MacdSeries.Signal ? 1 : expectedLine[i] - 1, result.Span[i]);
            Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Requests);
        }
        var count = 0; using (ComponentAverage.Arm((v, _) => { count++; return v; })) { Data(bars).CalculateMacZVwapIndicator(); Assert.Equal(0, count); }
    }
    [Fact]
    public void NonfiniteInputsCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MacZVwapIndicatorState(gamma: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Candles(1)).CalculateMacZVwapIndicator(gamma: bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMacZVwapIndicatorFast(Data(Candles(1)), context, gamma: bad));
            foreach (var invalid in new[] { Candles(1, bad), Volumes(new[] { 1d, 2 }, new[] { 1d, bad }) })
            { Assert.Throws<ArgumentOutOfRangeException>(() => Data(invalid).CalculateMacZVwapIndicator()); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMacZVwapIndicatorFast(Data(invalid), context)); }
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var actual = new MacZVwapIndicatorState(fastLength: 1, slowLength: 2, signalLength: 2, length1: 2, length2: 2); using var expected = new MacZVwapIndicatorState(fastLength: 1, slowLength: 2, signalLength: 2, length1: 2, length2: 2);
                var first = Candles(2)[0]; actual.Update(Native(first), true, true); expected.Update(Native(first), true, true);
                var fields = new[] { 4d, 4, 4, 4, 1 }; fields[field] = bad;
                var invalid = new Bar(first.Time, fields[0], fields[1], fields[2], fields[3], fields[4]); Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(invalid), final, true));
                var next = Native(Candles(4)[0]); var a = actual.Update(next, true, true); var e = expected.Update(next, true, true); foreach (var key in e.Outputs!.Keys) Assert.Equal(e.Outputs[key], a.Outputs![key]);
            }
        }
    }
}
