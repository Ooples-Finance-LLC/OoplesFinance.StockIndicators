using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RandomWalkNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RWI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RandomWalkIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCascade(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RandomWalkOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar Candle(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 14, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod)
    {
        var expected = BuiltInFormulaReferences.RandomWalkValues(bars, length, kind);
        var batch = Data(bars).CalculateRandomWalkIndex(kind, length);
        foreach (var entry in expected.Outputs) Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Empty(batch.CustomValuesList);
        foreach (var key in expected.Outputs.Keys)
        {
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeRandomWalkIndexFast(Data(bars), context, length, kind, key);
            Assert.Equal(expected.Outputs[key], actual.ToArray());
        }
        using var state = new RandomWalkIndexState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(3, -1, 8)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["RwiHigh"][i], point.Value);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void CompleteRootRatioRoundsOnceAtIndependentSqrtTwoHand()
    {
        var output = Check(new[] { Candle(2, 0, 1) }, 2);
        Assert.Equal(Math.Sqrt(2), output["RwiHigh"][0]); Assert.Equal(0, output["RwiLow"][0]);
        Assert.NotEqual(2 / Math.Sqrt(2), output["RwiHigh"][0]);
    }
    [Fact]
    public void LagAndWilderStartupHaveIndependentRationalHand()
    {
        var bars = Enumerable.Repeat(Candle(4, 0, 2), 4).Append(Candle(6, 2, 4)).ToArray(); var output = Check(bars, 4);
        Assert.Equal(new[] { 2d, 8d / 7, 32d / 37, 128d / 175, 768d / 781 }, output["RwiHigh"]);
        Assert.Equal(new[] { 0d, 0, 0, 0, 256d / 781 }, output["RwiLow"]);
        var unit = Check(Bars(0, 1, -1, 1), 1); Assert.Equal(new[] { 0d, 1, -1, 1 }, unit["RwiHigh"]); Assert.Equal(new[] { 0d, -1, 1, -1 }, unit["RwiLow"]);
    }
    [Fact]
    public void ExtremeAndSubnormalRangesRetainCompleteRatios()
    {
        var bars = new[] { Candle(3, -1, 1), Candle(4, 0, 2), Candle(1, -3, -1), Candle(5, 1, 3), Candle(0, -4, -2), Candle(4, 0, 2) };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var ordinary = Check(bars, 2, kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
            {
                var scaled = Check(bars.Select(b => Candle(b.High * scale, b.Low * scale, b.Close * scale)).ToArray(), 2, kind);
                foreach (var entry in ordinary) Assert.Equal(entry.Value, scaled[entry.Key]);
            }
            Check(new[] { Candle(double.MaxValue, -double.MaxValue, 0), Candle(double.MaxValue, 0, 1), Candle(0, -double.MaxValue, -1) }, 2, kind);
            Check(new[] { Candle(Math.BitIncrement(1), 1, 1), Candle(1, Math.BitDecrement(1), 1) }, 2, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), period, kind); Check(Bars(1, 4, -2, 3, 0), period, kind); }
    }
    [Fact]
    public void ExpiryAndRecursiveTransitionsPreservePreviewsAndReset()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Enumerable.Range(0, 40).Select(i => Candle(i % 7 + 1, i % 7 - 2, i % 7)).ToArray(), 3, kind); Check(Bars(2, 2, 2, 2, 2, 2), 2, kind); }
    }
    [Fact]
    public void OpeningSelectedCloseAndGapsDefineTrueRange()
    {
        var output = Check(new[] { Candle(2, 0, 5), Candle(3, 1, 2), Candle(7, 6, 6) }, 1);
        Assert.Equal(new[] { 2d / 5, 3d / 4, 6d / 5 }, output["RwiHigh"]);
        Assert.Equal(new[] { 0d, 1d / 4, -3d / 5 }, output["RwiLow"]);
    }
    [Fact]
    public void CallbackReceivesOneTrueRangeStageForEitherOutput()
    {
        foreach (var key in new[] { "RwiHigh", "RwiLow" })
        {
            var requests = new List<(double[] Values, int Period)>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return Enumerable.Repeat(3d, values.Count).ToArray(); };
            var data = Data(Bars(0, 2, -1)); var prior = data.ChainedValues.ToArray();
            using var armed = ComponentAverage.Arm(callback); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRandomWalkIndexFast(data, context, 4, outputKey: key);
            Assert.Equal(1, ComponentAverage.Substitutions); Assert.Single(requests); Assert.Equal(4, requests[0].Period); Assert.Equal(new[] { 0d, 2, 3 }, requests[0].Values);
            Assert.Equal(key == "RwiHigh" ? new[] { 0d, 1d / 3, -1d / 6 } : new[] { 0d, -1d / 3, 1d / 6 }, actual.ToArray());
            Assert.Equal(prior, data.ChainedValues);
        }
    }
    [Fact]
    public void CallbackAtrAndUnpublishedPriceDifferenceCannotOverflow()
    {
        foreach (var key in new[] { "RwiHigh", "RwiLow" })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, _) => Enumerable.Repeat(double.MaxValue, values.Count).ToArray();
            using var armed = ComponentAverage.Arm(callback); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRandomWalkIndexFast(Data(Bars(-double.MaxValue, double.MaxValue)), context, 1, outputKey: key);
            Assert.Equal(key == "RwiHigh" ? new[] { -1d, 2 } : new[] { 1d, -2 }, actual.ToArray()); Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RandomWalkIndexState(); using var control = new RandomWalkIndexState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
