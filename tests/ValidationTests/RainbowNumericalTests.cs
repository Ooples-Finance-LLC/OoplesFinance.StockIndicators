using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RainbowNumericalTests
{
    private static Bar[] Bars(params double[] values) => values.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RAINBOW", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RainbowOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCascade(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RainbowOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 2, int range = 10, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.RainbowValues(bars, length, range, kind);
        var batch = Data(bars).CalculateRainbowOscillator(kind, length, range);
        foreach (var entry in expected.Outputs) Assert.Equal(entry.Value, batch.OutputValues[entry.Key]);
        Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Ro"], batch.CustomValuesList);
        foreach (var key in expected.Outputs.Keys)
        {
            using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeRainbowOscillatorFast(Data(bars), context, length, range, kind, key);
            Assert.Equal(expected.Outputs[key], actual.ToArray());
        }
        using var state = new RainbowOscillatorState(kind, length, range);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Bars(3, -1, 8)) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(double.MaxValue)[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Ro"][i], point.Value);
                    foreach (var entry in expected.Outputs) Assert.Equal(entry.Value[i], point.Outputs![entry.Key]);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void OpeningCascadeHasIndependentThreeOutputHand()
    {
        var values = Check(Bars(1, 2)); Assert.Equal(87055d / 512, values["Ro"][1]);
        Assert.Equal(38325d / 256, values["UpperBand"][1]); Assert.Equal(-38325d / 256, values["LowerBand"][1]);
        Assert.All(values.Values, v => Assert.Equal(0, v[0]));
        var zero = Check(Bars(0, 2)); Assert.Equal(46085d / 512, zero["Ro"][1]); Assert.Equal(12775d / 256, zero["UpperBand"][1]);
    }
    [Fact]
    public void UnitCascadePeriodCancelsBeforePublication()
    {
        foreach (var range in new[] { 1, 2, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var outputs = Check(Bars(double.MaxValue, -double.MaxValue, 1, double.Epsilon, -double.Epsilon, 0), 1, range, kind);
            foreach (var values in outputs.Values) Assert.All(values, v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void ExtremeAndSubnormalRangesRetainCompleteRatios()
    {
        var prices = new[] { 0d, 3, -2, 1, 1, -4, 2, 0, 0, 3 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            var ordinary = Check(Bars(prices), 2, 3, kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) })
            {
                var scaled = Check(Bars(prices.Select(v => v * scale).ToArray()), 2, 3, kind);
                foreach (var entry in ordinary) Assert.Equal(entry.Value, scaled[entry.Key]);
            }
            Check(Bars(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, double.Epsilon), 3, 2, kind);
            Check(Bars(1, Math.BitIncrement(1), 1, Math.BitDecrement(1), 1), 3, 2, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsAreLazyAndNormalizeConsistently()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
        { Check(Array.Empty<Bar>(), period, period, kind); Check(Bars(1, 4, -2, 3, 0), period, period, kind); }
    }
    [Fact]
    public void ExpiryAndRecursiveTransitionsPreservePreviewsAndReset()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Bars(Enumerable.Range(0, 40).Select(i => (double)(i % 13 - 5)).ToArray()), 3, 4, kind); Check(Bars(2, 2, 2, 2, 2, 2), 2, 2, kind); }
    }
    [Fact]
    public void CallbackGraphUsesTenOrderedStagesForEveryOutput()
    {
        foreach (var key in new[] { "Ro", "UpperBand", "LowerBand" })
        {
            var requests = new List<(double[] Values, int Period)>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return Enumerable.Repeat((double)requests.Count, values.Count).ToArray(); };
            var data = Data(Bars(0, 2, 1)); var prior = data.ChainedValues.ToArray();
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 10).ToArray()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRainbowOscillatorFast(data, context, 3, 3, outputKey: key);
            Assert.Equal(10, ComponentAverage.Substitutions); Assert.Equal(10, requests.Count); Assert.All(requests, v => Assert.Equal(3, v.Period));
            Assert.Equal(new[] { 0d, 2, 1 }, requests[0].Values);
            for (var stage = 1; stage < 10; stage++) Assert.All(requests[stage].Values, v => Assert.Equal(stage, v));
            Assert.Equal(key == "Ro" ? new[] { 0d, -175, -225 } : key == "UpperBand" ? new[] { 0d, 450, 450 } : new[] { 0d, -450, -450 }, actual.ToArray());
            Assert.Equal(prior, data.ChainedValues);
        }
    }
    [Fact]
    public void CallbackCascadeSumCannotOverflowBeforeCancellation()
    {
        foreach (var key in new[] { "Ro", "UpperBand", "LowerBand" })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, _) => Enumerable.Repeat(double.MaxValue, values.Count).ToArray();
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, 10).ToArray()); using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeRainbowOscillatorFast(Data(Bars(-double.MaxValue, double.MaxValue)), context, outputKey: key);
            Assert.Equal(new[] { 0d, 0 }, actual.ToArray()); Assert.Equal(10, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidBarsCannotAdvanceState()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new RainbowOscillatorState(); using var control = new RainbowOscillatorState();
            foreach (var b in Bars(1, 3, -1)) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 1d, 3, -1, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(0, 3, -2)) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
}
