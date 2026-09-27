using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class GatorNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GATOR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(GatorOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDisplacedMedianLines(IndicatorValidationCase c, string route)
    {
        var options = (GatorOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.GatorOutputs(bars, options.Length), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int jl, int jo, int tl, int to, int ll, int lo, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.GatorOutputs(bars, jl, jo, tl, to, ll, lo, k); var batch = Data(bars).CalculateGatorOscillator(kind, jl, jo, tl, to, ll, lo);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeGatorOscillatorFast(Data(bars), context, jl, jo, tl, to, ll, lo, kind, key == "Top" ? IndicatorCompute.GatorJaw.Top : IndicatorCompute.GatorJaw.Bottom); Assert.Equal(expected[key], raw.ToArray());
        }
        using var state = new GatorOscillatorState(kind, jl, jo, tl, to, ll, lo);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Bars(new[] { double.MaxValue })[0]), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Top"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandOffsetsAndSignedOutputsRemainDistinct()
    {
        var bars = Bars(new[] { 1d, 2, 4, 8 }); var expected = BuiltInFormulaReferences.GatorOutputs(bars, 1, 2, 1, 1, 1, 0);
        Assert.Equal(new[] { 0d, 1, 1, 2 }, expected["Top"]); Assert.Equal(new[] { -1d, -1, -2, -4 }, expected["Bottom"]); Check(bars, 1, 2, 1, 1, 1, 0);
    }
    [Fact]
    public void ExtremeAndSubnormalMediansRecoverAfterDisplacement()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
            {
                var bars = Bars(Enumerable.Range(0, 72).Select(i => (i % 3 - 1) * scale).ToArray()); Check(bars, 2, 3, 3, 2, 4, 1, kind); Check(bars, 13, 8, 8, 5, 5, 3, kind);
            }
            Check(Bars(Enumerable.Range(0, 64).Select(i => .5 + .4 * Math.Sin(i * .37)).ToArray()), 2, 3, 3, 2, 4, 1, kind);
            Check(Array.Empty<Bar>(), 2, 3, 3, 2, 4, 1, kind);
        }
        var recovery = Bars(new[] { double.MaxValue, -double.MaxValue, 0, 0, 0 }); var expected = BuiltInFormulaReferences.GatorOutputs(recovery, 1, 1, 1, 0, 1, 1);
        Assert.True(double.IsPositiveInfinity(expected["Top"][1])); Assert.True(double.IsNegativeInfinity(expected["Bottom"][1])); Assert.Equal(0, expected["Top"][^1]); Check(recovery, 1, 1, 1, 0, 1, 1);
        var mixed = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1)).ToArray(); Check(mixed, 2, 3, 3, 2, 4, 1);
    }
    [Fact]
    public void MaximumOffsetsUseObservedHistory()
    {
        var bars = Bars(new[] { 1d, 2, 3 }); Check(bars, 1, int.MaxValue, 1, int.MaxValue, 1, int.MaxValue);
    }
    [Fact]
    public void SelectedClosesReplaceTheMedianOnly()
    {
        var selected = Enumerable.Range(0, 32).Select(i => (double)(i % 7)).ToArray(); var original = Bars(Enumerable.Repeat(0d, selected.Length).ToArray());
        var expected = BuiltInFormulaReferences.GatorOutputs(Bars(selected), jawLength: 2, selected: true);
        foreach (var key in expected.Keys)
        {
            var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.GatorOscillator, new GatorOscillatorSpecOptions(2), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); batch.CalculateGatorOscillator(jawLength: 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var state = new GatorOscillatorState(jawLength: 2); ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var i = 0; i < selected.Length; i++)
        {
            var b = new Bar(original[i].Time, 0, 0, 0, selected[i], 1); var actual = state.Update(Native(b), true, true);
            foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
        }
    }
    [Fact]
    public void CustomerAveragesKeepThreeMedianInputs()
    {
        var bars = Bars(new[] { 2d, 4 }); var periods = new[] { 2, 3, 4 }; var values = new[] { 1d, 2, 4 };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = Enumerable.Range(0, 3).Select(index => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, period) => { Assert.Equal(periods[index], period); Assert.Equal(new[] { 2d, 4 }, input); return Enumerable.Repeat(values[index], input.Count).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) { var result = Data(bars).CalculateGatorOscillator(jawLength: 2, jawOffset: 0, teethLength: 3, teethOffset: 0, lipsLength: 4, lipsOffset: 0); Assert.Equal(new[] { 1d, 1 }, result.OutputValues["Top"]); Assert.Equal(new[] { -2d, -2 }, result.OutputValues["Bottom"]); }
            else { using var raw = IndicatorCompute.ComputeGatorOscillatorFast(Data(bars), context, 2, 0, 3, 0, 4, 0, jaw: IndicatorCompute.GatorJaw.Bottom); Assert.Equal(new[] { -2d, -2 }, raw.ToArray()); }
            Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyLine()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new GatorOscillatorState(jawLength: 2); using var control = new GatorOscillatorState(jawLength: 2);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(Enumerable.Range(0, 20).Select(i => (double)(i % 7)).ToArray()))
            {
                var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true);
                foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
            }
        }
    }
}
