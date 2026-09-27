using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SmiErgodicNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SMI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SMIErgodicIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSignedAndAbsoluteSmoothing(IndicatorValidationCase c, string route)
    {
        var options = (SMIErgodicIndicatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SmiErgodicOutputs(bars, options.FastLength, options.SlowLength, options.SignalLength, kind), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int fast, int slow, int signal, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.SmiErgodicOutputs(bars, fast, slow, signal, k); var batch = Data(bars).CalculateSMIErgodicIndicator(kind, fast, slow, signal);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.SMIErgodicIndicator, new SMIErgodicIndicatorSpecOptions(fast, slow, signal, kind), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        using var state = new SMIErgodicIndicatorState(kind, fast, slow, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Bars(new[] { double.MaxValue })[0]), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Smi"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandZeroOriginAndSignalRemainDistinct()
    {
        var bars = Bars(new[] { 2d, 4, 2 }); var expected = BuiltInFormulaReferences.SmiErgodicOutputs(bars, 1, 1, 2);
        Assert.Equal(new[] { 0d, 100, -100 }, expected["Smi"]); Assert.Equal(new[] { 0d, 50, -50 }, expected["Signal"]); Check(bars, 1, 1, 2);
    }
    [Fact]
    public void ExtremeSignedAndAbsoluteStagesStayBounded()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var periods in new[] { (1, 1, 1), (2, 3, 4), (5, 20, 5) })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Bars(Enumerable.Range(0, 48).Select(i => (i % 3 - 1) * scale).ToArray()), periods.Item1, periods.Item2, periods.Item3, kind);
            Check(Bars(Enumerable.Range(0, 64).Select(i => 0.5 + 0.4 * Math.Sin(i * .37)).ToArray()), periods.Item1, periods.Item2, periods.Item3, kind);
            Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, periods.Item3, kind);
        }
    }
    [Fact]
    public void RawSelectedPricesFeedBothOutputs()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var bars = Bars(Enumerable.Repeat(0d, selected.Length).ToArray());
        var expected = BuiltInFormulaReferences.SmiErgodicOutputs(Bars(selected), 2, 3, 4);
        foreach (var key in expected.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.SMIErgodicIndicator, new SMIErgodicIndicatorSpecOptions(2, 3, 4), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        var batchData = Data(bars); batchData.SetCustomValues(selected.ToList()); var result = batchData.CalculateSMIErgodicIndicator(fastLength: 2, slowLength: 3, signalLength: 4);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], result.OutputValues[key]);
    }
    [Fact]
    public void CustomerStagesKeepFiveInputsAndPeriodsInOrder()
    {
        var bars = Bars(new[] { 2d, 4 }); var inputs = new[] { new[] { 0d, 2 }, new[] { 1d, 1 }, new[] { 0d, 2 }, new[] { 4d, 4 }, new[] { 25d, 25 } };
        var outputs = new[] { 1d, 2, 4, 8, 7 }; var periods = new[] { 2, 3, 2, 3, 4 };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = Enumerable.Range(0, 5).Select(index => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, period) => { Assert.Equal(periods[index], period); Assert.Equal(inputs[index], input); return Enumerable.Repeat(outputs[index], input.Count).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) { var result = Data(bars).CalculateSMIErgodicIndicator(fastLength: 2, slowLength: 3, signalLength: 4); Assert.Equal(new[] { 25d, 25 }, result.OutputValues["Smi"]); Assert.Equal(new[] { 7d, 7 }, result.OutputValues["Signal"]); }
            else { using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.SMIErgodicIndicator, new SMIErgodicIndicatorSpecOptions(2, 3, 4), "Signal"), context); Assert.NotNull(raw); Assert.Equal(new[] { 7d, 7 }, raw.Value.ToArray()); }
            Assert.Equal(5, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceEitherOutput()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SMIErgodicIndicatorState(fastLength: 2, slowLength: 3, signalLength: 4); using var control = new SMIErgodicIndicatorState(fastLength: 2, slowLength: 3, signalLength: 4);
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
